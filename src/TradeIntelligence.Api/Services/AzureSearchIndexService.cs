using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using TradeIntelligence.Api.Models;

namespace TradeIntelligence.Api.Services;

public interface ISearchIndexService
{
    Task EnsureIndexExistsAsync(CancellationToken ct = default);
}

public class AzureSearchIndexService : ISearchIndexService
{
    private readonly SearchIndexClient _indexClient;
    private readonly IConfiguration _config;
    private readonly ILogger<AzureSearchIndexService> _logger;

    public AzureSearchIndexService(SearchIndexClient indexClient, IConfiguration config, ILogger<AzureSearchIndexService> logger)
    {
        _indexClient = indexClient;
        _config = config;
        _logger = logger;
    }

    public async Task EnsureIndexExistsAsync(CancellationToken ct = default)
    {
        var indexName = _config["AzureSearch:IndexName"] ?? "trade-documents";

        try
        {
            await _indexClient.GetIndexAsync(indexName, ct);
            _logger.LogInformation("Azure AI Search index '{IndexName}' already exists", indexName);
        }
        catch
        {
            _logger.LogInformation("Creating Azure AI Search index '{IndexName}'", indexName);
            var fieldBuilder = new FieldBuilder();
            var fields = fieldBuilder.Build(typeof(DocumentChunk));

            var index = new SearchIndex(indexName, fields)
            {
                SemanticSearch = new SemanticSearch
                {
                    Configurations =
                    {
                        new SemanticConfiguration("trade-semantic-config", new SemanticPrioritizedFields
                        {
                            ContentFields = { new SemanticField("content") },
                            KeywordsFields = { new SemanticField("documentName"), new SemanticField("regulation") }
                        })
                    }
                }
            };

            await _indexClient.CreateIndexAsync(index, ct);
            _logger.LogInformation("Index '{IndexName}' created successfully", indexName);
        }
    }
}
