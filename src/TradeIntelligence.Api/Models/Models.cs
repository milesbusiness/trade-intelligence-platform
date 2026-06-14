using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace TradeIntelligence.Api.Models;

public class QueryRequest
{
    public string Question { get; set; } = string.Empty;
    public QueryFilters? Filters { get; set; }
}

public class QueryFilters
{
    public string? Regulation { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo   { get; set; }
}

public class QueryResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = [];
    public List<string> ComplianceFlags { get; set; } = [];
    public int ProcessingTimeMs { get; set; }
}

public class Citation
{
    public string DocumentName { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string Excerpt { get; set; } = string.Empty;
}

public class QueryHistoryItem
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int CitationCount { get; set; }
}

public class IngestionResult
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int ChunksIndexed { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
}

public class DocumentSummary
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class ComplianceCheckRequest
{
    public string DocumentText { get; set; } = string.Empty;
    public List<string>? Regulations { get; set; }
}

public class ComplianceCheckResult
{
    public int Score { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<ComplianceFinding> Findings { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
}

public class ComplianceFinding
{
    public string Severity { get; set; } = string.Empty;
    public string Article { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

// Azure AI Search index document
public class DocumentChunk
{
    [SimpleField(IsKey = true, IsFilterable = true)]
    public string Id { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    public string DocumentId { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsSortable = true)]
    public string DocumentName { get; set; } = string.Empty;

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnMicrosoft)]
    public string Content { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true, IsSortable = true)]
    public int PageNumber { get; set; }

    [SimpleField(IsFilterable = true, IsFacetable = true)]
    public string Regulation { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true, IsSortable = true)]
    public DateTimeOffset Date { get; set; } = DateTimeOffset.UtcNow;
}
