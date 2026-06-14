using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TradeIntelligence.Api.Models;

namespace TradeIntelligence.Api.Services;

public interface IRagQueryService
{
    Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<QueryHistoryItem>> GetHistoryAsync(int limit = 50, CancellationToken ct = default);
}

public class RagQueryService : IRagQueryService
{
    private readonly SearchClient _searchClient;
    private readonly Kernel _kernel;
    private readonly ILogger<RagQueryService> _logger;
    private static readonly List<QueryHistoryItem> _history = new();

    public RagQueryService(SearchClient searchClient, Kernel kernel, ILogger<RagQueryService> logger)
    {
        _searchClient = searchClient;
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Hybrid search: keyword + semantic
        var searchOptions = new SearchOptions
        {
            QueryType = SearchQueryType.Semantic,
            SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = "trade-semantic-config",
                QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
                QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive)
            },
            Size = 5,
            Select = { "id", "content", "documentName", "pageNumber", "regulation", "date" }
        };

        if (request.Filters != null)
        {
            var filters = new List<string>();
            if (!string.IsNullOrEmpty(request.Filters.Regulation))
                filters.Add($"regulation eq '{request.Filters.Regulation}'");
            if (request.Filters.DateFrom.HasValue)
                filters.Add($"date ge {request.Filters.DateFrom:yyyy-MM-dd}");
            if (filters.Any())
                searchOptions.Filter = string.Join(" and ", filters);
        }

        var searchResults = await _searchClient.SearchAsync<DocumentChunk>(request.Question, searchOptions, ct);
        var chunks = new List<DocumentChunk>();
        await foreach (var result in searchResults.Value.GetResultsAsync())
            chunks.Add(result.Document);

        if (chunks.Count == 0)
            return new QueryResponse
            {
                Answer = "No relevant documents found. Please ingest trade documents first.",
                Citations = [],
                ComplianceFlags = [],
                ProcessingTimeMs = (int)sw.ElapsedMilliseconds
            };

        // Build context for GPT-4o
        var context = string.Join("\n\n---\n\n", chunks.Select((c, i) =>
            $"[Source {i + 1}: {c.DocumentName}, Page {c.PageNumber}]\n{c.Content}"));

        var prompt = $"""
            You are a trade compliance and document intelligence assistant specialising in MiFID II, EMIR, and trading regulations.

            Using ONLY the provided sources, answer the question. Always cite which source you used.
            If the answer is not in the sources, say so clearly.

            SOURCES:
            {context}

            QUESTION: {request.Question}

            Provide a clear, accurate answer with specific citations.
            """;

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage(prompt);
        var response = await chat.GetChatMessageContentAsync(history, cancellationToken: ct);
        var answer = response.Content ?? "Unable to generate answer.";

        var citations = chunks.Select(c => new Citation
        {
            DocumentName = c.DocumentName,
            PageNumber = c.PageNumber,
            Excerpt = c.Content.Length > 200 ? c.Content[..200] + "..." : c.Content
        }).ToList();

        var complianceFlags = DetectComplianceFlags(answer, chunks);
        sw.Stop();

        var queryResponse = new QueryResponse
        {
            Answer = answer,
            Citations = citations,
            ComplianceFlags = complianceFlags,
            ProcessingTimeMs = (int)sw.ElapsedMilliseconds
        };

        _history.Insert(0, new QueryHistoryItem
        {
            Question = request.Question,
            Answer = answer,
            Timestamp = DateTime.UtcNow,
            CitationCount = citations.Count
        });

        _logger.LogInformation("RAG query completed in {Ms}ms, {Citations} citations", sw.ElapsedMilliseconds, citations.Count);
        return queryResponse;
    }

    public Task<IReadOnlyList<QueryHistoryItem>> GetHistoryAsync(int limit = 50, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<QueryHistoryItem>>(_history.Take(limit).ToList());

    private static List<string> DetectComplianceFlags(string answer, List<DocumentChunk> chunks)
    {
        var flags = new List<string>();
        var text = (answer + " " + string.Join(" ", chunks.Select(c => c.Content))).ToLower();

        if (text.Contains("best execution") && text.Contains("violation"))
            flags.Add("MiFID II Art. 27 — Best Execution potential violation detected");
        if (text.Contains("transaction reporting") && (text.Contains("missing") || text.Contains("late")))
            flags.Add("MiFID II Art. 26 — Transaction reporting gap detected");
        if (text.Contains("emir") && text.Contains("unreported"))
            flags.Add("EMIR — Unreported derivative transaction detected");
        if (text.Contains("position limit") && text.Contains("exceeded"))
            flags.Add("MiFID II Art. 57 — Position limit breach detected");

        return flags;
    }
}
