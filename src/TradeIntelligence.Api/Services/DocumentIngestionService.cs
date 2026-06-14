using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using TradeIntelligence.Api.Models;

namespace TradeIntelligence.Api.Services;

public interface IDocumentIngestionService
{
    Task<IngestionResult> IngestAsync(IFormFile file, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default);
    Task DeleteDocumentAsync(string documentId, CancellationToken ct = default);
}

public class DocumentIngestionService : IDocumentIngestionService
{
    private readonly BlobServiceClient _blobClient;
    private readonly SearchClient _searchClient;
    private readonly IConfiguration _config;
    private readonly ILogger<DocumentIngestionService> _logger;
    private const string ContainerName = "trade-documents";

    public DocumentIngestionService(
        BlobServiceClient blobClient,
        SearchClient searchClient,
        IConfiguration config,
        ILogger<DocumentIngestionService> logger)
    {
        _blobClient = blobClient;
        _searchClient = searchClient;
        _config = config;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0)
            throw new ArgumentException("Empty file");
        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only PDF files are supported");
        if (file.Length > 50 * 1024 * 1024)
            throw new ArgumentException("File too large (max 50MB)");

        var documentId = Guid.NewGuid().ToString();
        var blobName = $"{documentId}/{file.FileName}";

        // Upload to Blob Storage
        var container = _blobClient.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = container.GetBlobClient(blobName);
        using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);

        _logger.LogInformation("Uploaded {FileName} to blob storage as {BlobName}", file.FileName, blobName);

        // Extract text and chunk (simplified — production would use Azure AI Foundry)
        var chunks = await ExtractAndChunkAsync(file, documentId, ct);

        // Index chunks in Azure AI Search
        var batch = IndexDocumentsBatch.Upload(chunks);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: ct);

        _logger.LogInformation("Indexed {ChunkCount} chunks for {FileName}", chunks.Count, file.FileName);

        return new IngestionResult
        {
            DocumentId = documentId,
            FileName = file.FileName,
            ChunksIndexed = chunks.Count,
            BlobUrl = blob.Uri.ToString()
        };
    }

    public async Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default)
    {
        var container = _blobClient.GetBlobContainerClient(ContainerName);
        var summaries = new List<DocumentSummary>();

        await foreach (var blob in container.GetBlobsAsync(cancellationToken: ct))
        {
            var parts = blob.Name.Split('/');
            if (parts.Length < 2) continue;
            summaries.Add(new DocumentSummary
            {
                DocumentId = parts[0],
                FileName = parts[1],
                SizeBytes = blob.Properties.ContentLength ?? 0,
                UploadedAt = blob.Properties.CreatedOn?.UtcDateTime ?? DateTime.UtcNow
            });
        }

        return summaries;
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken ct = default)
    {
        // Delete from search index
        var searchOptions = new SearchOptions { Filter = $"documentId eq '{documentId}'" };
        var results = await _searchClient.SearchAsync<DocumentChunk>("*", searchOptions, ct);
        var ids = new List<string>();
        await foreach (var r in results.Value.GetResultsAsync())
            ids.Add(r.Document.Id);

        if (ids.Any())
        {
            var batch = IndexDocumentsBatch.Delete("id", ids);
            await _searchClient.IndexDocumentsAsync(batch, ct);
        }

        // Delete blobs
        var container = _blobClient.GetBlobContainerClient(ContainerName);
        await foreach (var blob in container.GetBlobsAsync(prefix: documentId + "/", cancellationToken: ct))
            await container.GetBlobClient(blob.Name).DeleteAsync(cancellationToken: ct);

        _logger.LogInformation("Deleted document {DocumentId}", documentId);
    }

    private async Task<List<DocumentChunk>> ExtractAndChunkAsync(IFormFile file, string documentId, CancellationToken ct)
    {
        // In production: use Azure AI Foundry Document Intelligence for proper PDF extraction
        // Here: simplified chunking for demonstration
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(ct);

        const int chunkSize = 1000;
        const int overlap = 100;
        var chunks = new List<DocumentChunk>();
        var pageNum = 1;

        for (int i = 0; i < content.Length; i += chunkSize - overlap)
        {
            var end = Math.Min(i + chunkSize, content.Length);
            var chunkContent = content[i..end].Trim();
            if (string.IsNullOrWhiteSpace(chunkContent)) continue;

            chunks.Add(new DocumentChunk
            {
                Id = $"{documentId}-{chunks.Count}",
                DocumentId = documentId,
                DocumentName = file.FileName,
                Content = chunkContent,
                PageNumber = pageNum++,
                Regulation = DetectRegulation(chunkContent)
            });
        }

        return chunks;
    }

    private static string DetectRegulation(string text)
    {
        var lower = text.ToLower();
        if (lower.Contains("mifid") || lower.Contains("best execution") || lower.Contains("transaction reporting"))
            return "MiFID II";
        if (lower.Contains("emir") || lower.Contains("derivative") || lower.Contains("margin requirement"))
            return "EMIR";
        if (lower.Contains("ucits") || lower.Contains("aifmd"))
            return "UCITS/AIFMD";
        return "General";
    }
}
