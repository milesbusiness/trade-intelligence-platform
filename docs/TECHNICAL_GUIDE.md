# Technical Guide — Trade Intelligence Platform

> This guide explains every technology used, how to learn it, how to install the project, what every file does, and how to run and view output.

---

## Table of Contents

1. [Technologies Used](#1-technologies-used)
2. [Where to Learn Each Technology](#2-where-to-learn-each-technology)
3. [Installation — Step by Step](#3-installation--step-by-step)
4. [Project File Structure](#4-project-file-structure)
5. [Code Walkthrough — Every File Explained](#5-code-walkthrough--every-file-explained)
6. [How to Run and View Output](#6-how-to-run-and-view-output)

---

## 1. Technologies Used

| Technology | Version | What it is | Why it is used here |
|-----------|---------|-----------|-------------------|
| **.NET 9** | 9.0 | Microsoft's cross-platform runtime for C# | The API host |
| **ASP.NET Core MVC** | 9.0 | HTTP framework with Controllers | Organises endpoints into `DocumentsController`, `QueryController`, `ComplianceController` |
| **Microsoft Semantic Kernel** | 1.21+ | AI orchestration SDK for .NET | Connects to Azure OpenAI GPT-4o for answer generation |
| **Azure AI Search** | — | Microsoft's cloud search service | Stores document chunks as vectors; performs hybrid BM25 + semantic search |
| **Azure Blob Storage** | — | Microsoft's object storage | Stores uploaded PDF files (the source documents) |
| **Azure OpenAI GPT-4o** | — | Microsoft-hosted GPT-4o | Generates answers from retrieved document chunks |
| **Swagger / Swashbuckle** | — | OpenAPI documentation library | Auto-generates interactive API docs at `/swagger` |
| **Azure Bicep** | — | Infrastructure-as-Code for Azure (Microsoft's alternative to Terraform for Azure-only) | Provisions Azure resources in the `infra/` folder |
| **SearchQueryType.Semantic** | Azure.Search.Documents | Azure AI Search semantic ranking | Re-ranks keyword+vector results using language understanding |
| **`QueryCaption` / `QueryAnswer`** | Azure.Search.Documents | Extractive features | Extracts the most relevant sentence from each chunk for display |
| **Docker** | — | Container runtime | Packages the app for deployment |

**Official Links:**
- Azure AI Search .NET SDK: https://learn.microsoft.com/azure/search/search-howto-dotnet-sdk
- Azure AI Search semantic search: https://learn.microsoft.com/azure/search/semantic-search-overview
- Azure Blob Storage .NET SDK: https://learn.microsoft.com/azure/storage/blobs/storage-quickstart-blobs-dotnet
- Azure Bicep: https://learn.microsoft.com/azure/azure-resource-manager/bicep/overview
- Semantic Kernel: https://learn.microsoft.com/semantic-kernel/overview

---

## 2. Where to Learn Each Technology

### ASP.NET Core MVC Controllers

**Official:**
- https://learn.microsoft.com/aspnet/core/web-api/ — Web API guide
- https://learn.microsoft.com/aspnet/core/tutorials/first-web-api — Full tutorial

**YouTube:**
- "ASP.NET Core Web API" by Nick Chapsas — https://www.youtube.com/@nickchapsas (best .NET content on YouTube)

**Difference from Minimal API** (used in trading-arch-agent): Minimal API puts routes in `Program.cs`. MVC Controllers put routes in separate class files with `[HttpGet]` / `[HttpPost]` attributes. Controllers are better for larger projects with many endpoints.

### Azure AI Search (Hybrid + Semantic)

**Official:**
- https://learn.microsoft.com/azure/search/search-get-started-portal — Portal quickstart
- https://learn.microsoft.com/azure/search/hybrid-search-overview — How hybrid search works
- https://learn.microsoft.com/azure/search/semantic-search-overview — Semantic ranker

**Key concepts:**
- **BM25** — traditional keyword matching score (Term Frequency × Inverse Document Frequency)
- **Vector similarity** — cosine similarity between embedding vectors (captures meaning)
- **Hybrid fusion** — Reciprocal Rank Fusion (RRF) combines both scores
- **Semantic ranker** — a re-ranking model trained by Microsoft that reads the top results and reorders them based on language understanding

### Azure Bicep

**Official:**
- https://learn.microsoft.com/azure/azure-resource-manager/bicep/learn-bicep — Learn Bicep module
- https://learn.microsoft.com/azure/azure-resource-manager/bicep/compare-template-syntax — Bicep vs ARM vs Terraform

**YouTube:**
- "Azure Bicep Tutorial" by John Savill — https://www.youtube.com/@NTFAQGuy

**Bicep vs Terraform:** Bicep is Microsoft's own language, Azure-only, simpler syntax. Terraform works across multiple cloud providers and has a larger ecosystem. Both produce Azure resources — this project uses Bicep; trading-azure-landing-zone uses Terraform.

---

## 3. Installation — Step by Step

### Step 1 — Install .NET 9 SDK

```powershell
winget install Microsoft.DotNet.SDK.9
dotnet --version
# Should show: 9.0.xxx
```

Download: https://dotnet.microsoft.com/download/dotnet/9.0

### Step 2 — Clone the Repository

```powershell
git clone https://github.com/milesbusiness/trade-intelligence-platform
cd trade-intelligence-platform
```

### Step 3 — Set Up Azure Resources

You need:
1. **Azure AI Search** (Standard tier — required for semantic search)
2. **Azure OpenAI** with `gpt-4o` deployed
3. **Azure Blob Storage** account

Create them in the Azure Portal: https://portal.azure.com

Or deploy with the included Bicep template:
```powershell
az group create --name rg-trade-intelligence --location westeurope
az deployment group create `
  --resource-group rg-trade-intelligence `
  --template-file infra/main.bicep
```

### Step 4 — Configure Credentials

Create `src/TradeIntelligence.Api/appsettings.Development.json`:

```json
{
  "AzureSearch": {
    "Endpoint": "https://your-search.search.windows.net",
    "ApiKey": "your-search-key",
    "IndexName": "trade-intelligence"
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-oai.openai.azure.com/",
    "ApiKey": "your-oai-key",
    "ChatDeployment": "gpt-4o"
  },
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=..."
  }
}
```

**This file is gitignored — credentials never go to GitHub.**

### Step 5 — Run

```powershell
cd src/TradeIntelligence.Api
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7000
info: TradeIntelligence.Api.Services.AzureSearchIndexService[0]
      Index 'trade-intelligence' ready
```

---

## 4. Project File Structure

```
trade-intelligence-platform/
├── infra/
│   └── main.bicep                          ← Bicep template: creates Azure AI Search + OpenAI + Blob
│
└── src/
    └── TradeIntelligence.Api/
        ├── Controllers/
        │   ├── DocumentsController.cs       ← Upload / list / delete documents (PDF ingestion)
        │   ├── QueryController.cs           ← POST /api/query — ask questions
        │   └── ComplianceController.cs      ← GET /api/compliance — run compliance scans
        ├── Services/
        │   ├── AzureSearchIndexService.cs   ← Creates the Azure AI Search index schema on startup
        │   ├── DocumentIngestionService.cs  ← Uploads PDF to Blob + chunks + indexes in Search
        │   ├── RagQueryService.cs           ← Hybrid search + GPT-4o answer generation
        │   └── ComplianceCheckService.cs    ← Scans for MiFID II / EMIR regulatory gaps
        ├── Models/
        │   └── Models.cs                    ← All C# data classes (QueryRequest, Citation, etc.)
        ├── Program.cs                       ← DI setup, service registration, startup
        └── appsettings.json                 ← Default config (no secrets)
```

---

## 5. Code Walkthrough — Every File Explained

### `Program.cs` — Service Registration

```csharp
builder.Services.AddSingleton(sp =>
{
    var cfg = builder.Configuration;
    return new SearchIndexClient(
        new Uri(cfg["AzureSearch:Endpoint"]!),
        new AzureKeyCredential(cfg["AzureSearch:ApiKey"]!));
});
builder.Services.AddSingleton(sp =>
{
    return new SearchClient(
        new Uri(cfg["AzureSearch:Endpoint"]!),
        cfg["AzureSearch:IndexName"]!,
        new AzureKeyCredential(cfg["AzureSearch:ApiKey"]!));
});
```
Two Azure Search clients registered:
- `SearchIndexClient` — manages the index schema (create, delete, update the index structure). Used by `AzureSearchIndexService`.
- `SearchClient` — performs search queries and document operations. Used by `RagQueryService` and `DocumentIngestionService`.

```csharp
builder.Services.AddSingleton(sp =>
{
    return Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(
            deploymentName: cfg["AzureOpenAI:ChatDeployment"]!,
            endpoint: cfg["AzureOpenAI:Endpoint"]!,
            apiKey: cfg["AzureOpenAI:ApiKey"]!)
        .Build();
});
```
Creates the Semantic Kernel with Azure OpenAI connection. `AddSingleton` — one Kernel shared across all requests, which is correct because the Kernel itself is thread-safe and stateless.

```csharp
// Ensure search index exists on startup
using (var scope = app.Services.CreateScope())
{
    var indexService = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();
    await indexService.EnsureIndexExistsAsync();
}
```
On startup, the application creates the Azure AI Search index if it doesn't exist. The index schema includes fields for content, documentName, pageNumber, regulation, and the vector field `contentVector` (1536 dimensions for text-embedding-3-large).

---

### `Services/RagQueryService.cs` — The RAG Core

```csharp
var searchOptions = new SearchOptions
{
    QueryType = SearchQueryType.Semantic,
    SemanticSearch = new SemanticSearchOptions
    {
        SemanticConfigurationName = "trade-semantic-config",
        QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
        QueryAnswer  = new QueryAnswer(QueryAnswerType.Extractive)
    },
    Size = 5,
    Select = { "id", "content", "documentName", "pageNumber", "regulation", "date" }
};
```
This configures a **semantic search query**:
- `QueryType.Semantic` — uses Microsoft's semantic ranker on top of BM25 + vector results
- `SemanticConfigurationName` — the name of the semantic configuration defined in the index (which fields to use for semantic ranking)
- `QueryCaption.Extractive` — extracts the most relevant sentence from each result for display
- `QueryAnswer.Extractive` — tries to extract a direct answer from the top result

```csharp
if (request.Filters?.Regulation != null)
    filters.Add($"regulation eq '{request.Filters.Regulation}'");
if (request.Filters?.DateFrom.HasValue == true)
    filters.Add($"date ge {request.Filters.DateFrom:yyyy-MM-dd}");
if (filters.Any())
    searchOptions.Filter = string.Join(" and ", filters);
```
OData filter syntax — Azure AI Search uses OData for filtering. You can filter by any field in the index. Here: filter to only search documents tagged as `"MiFID II"` or only documents after a certain date.

```csharp
var context = string.Join("\n\n---\n\n", chunks.Select((c, i) =>
    $"[Source {i + 1}: {c.DocumentName}, Page {c.PageNumber}]\n{c.Content}"));

var prompt = $"""
    You are a trade compliance and document intelligence assistant...
    Using ONLY the provided sources, answer the question. Always cite which source you used.
    If the answer is not in the sources, say so clearly.

    SOURCES:
    {context}

    QUESTION: {request.Question}
    """;
```
The GPT-4o prompt includes all retrieved chunks labelled with their source. "Using ONLY the provided sources" is the anti-hallucination instruction — it forces the model to cite or admit it doesn't know, not invent an answer.

```csharp
private static List<string> DetectComplianceFlags(string answer, List<DocumentChunk> chunks)
{
    var text = (answer + " " + string.Join(" ", chunks.Select(c => c.Content))).ToLower();

    if (text.Contains("best execution") && text.Contains("violation"))
        flags.Add("MiFID II Art. 27 — Best Execution potential violation detected");
    if (text.Contains("transaction reporting") && (text.Contains("missing") || text.Contains("late")))
        flags.Add("MiFID II Art. 26 — Transaction reporting gap detected");
    ...
}
```
Keyword-based compliance flag detection — searches both the answer and the retrieved chunks for combinations of keywords that suggest regulatory issues. Compliance flags are added to the response for the user to investigate.

---

### `Services/DocumentIngestionService.cs` — Document Processing

```csharp
if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Only PDF files are supported");
if (file.Length > 50 * 1024 * 1024)
    throw new ArgumentException("File too large (max 50MB)");
```
Input validation at the boundary — validates file type and size before any processing. This is the correct place to validate (API boundary, before it touches any storage or AI service).

```csharp
var blob = container.GetBlobClient(blobName);
using var stream = file.OpenReadStream();
await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
```
Uploads the original PDF to Azure Blob Storage as an immutable archive. The blob name is `{documentId}/{filename}` — so you can find all chunks for a document using the prefix `{documentId}/`.

```csharp
for (int i = 0; i < content.Length; i += chunkSize - overlap)
{
    var end = Math.Min(i + chunkSize, content.Length);
    var chunkContent = content[i..end].Trim();
    chunks.Add(new DocumentChunk
    {
        Id = $"{documentId}-{chunks.Count}",
        Regulation = DetectRegulation(chunkContent)
    });
}
var batch = IndexDocumentsBatch.Upload(chunks);
await _searchClient.IndexDocumentsAsync(batch, cancellationToken: ct);
```
1,000-character chunks with 100-character overlap. `IndexDocumentsBatch.Upload` — batch operation (more efficient than uploading one document at a time).

**Note:** In production, you would use Azure AI Foundry Document Intelligence (formerly Form Recognizer) for proper PDF text extraction with page numbers. The current implementation reads the file as text, which works for text-based PDFs but not scanned/image PDFs.

---

### `Controllers/QueryController.cs` — The API Layer

```csharp
[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Query([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "question is required" });

        var result = await _ragService.QueryAsync(request, ct);
        return Ok(result);
    }
```
`[Route("api/[controller]")]` — `[controller]` is replaced by "Query" (the controller class name minus "Controller"). So this controller handles routes starting with `/api/query`.

`[FromBody]` — tells ASP.NET Core to deserialise the JSON request body into `QueryRequest`. `CancellationToken ct` — automatically wired to the HTTP request's cancellation token — if the client disconnects, the Azure Search query and OpenAI call are cancelled.

---

## 6. How to Run and View Output

### Start the Server

```powershell
cd src/TradeIntelligence.Api
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run
```

### Open Swagger UI

Browser: **http://localhost:5000/swagger**

You will see three sections:
- **Documents** — Upload PDF / list all documents / delete a document
- **Query** — Ask a question about your documents
- **Compliance** — Run a compliance scan

### Step 1: Upload a Trade Document

In Swagger UI, expand `POST /api/documents`, click "Try it out", select a PDF, click "Execute".

Or via PowerShell:
```powershell
$form = @{
    file = Get-Item "C:\path\to\your-document.pdf"
}
Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/documents" `
  -Form $form
```

Response:
```json
{
  "documentId": "3f7a2c1b-...",
  "fileName": "your-document.pdf",
  "chunksIndexed": 23,
  "blobUrl": "https://..."
}
```

### Step 2: Query Your Documents

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/query" `
  -Headers @{"Content-Type"="application/json"} `
  -Body '{"question": "What are the transaction reporting requirements under MiFID II Article 26?"}'
```

Response:
```json
{
  "answer": "According to [Source 1: mifid-compliance.pdf, Page 12], MiFID II Article 26 requires investment firms to report complete and accurate details of executed transactions to the competent authority no later than the close of the following working day...",
  "citations": [
    {
      "documentName": "mifid-compliance.pdf",
      "pageNumber": 12,
      "excerpt": "Transaction reporting obligations apply to all financial instruments..."
    }
  ],
  "complianceFlags": [],
  "processingTimeMs": 1842
}
```

### Step 3: Query with Regulatory Filter

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/query" `
  -Headers @{"Content-Type"="application/json"} `
  -Body '{
    "question": "What are the margin requirements?",
    "filters": {
      "regulation": "EMIR"
    }
  }'
```

This only searches documents tagged as "EMIR" — filtering out MiFID II and UCITS content.

### Step 4: View Query History

```powershell
Invoke-RestMethod "http://localhost:5000/api/query/history"
```

### Step 5: List All Indexed Documents

```powershell
Invoke-RestMethod "http://localhost:5000/api/documents"
```

### Verify Azure AI Search (Azure Portal)

1. Go to https://portal.azure.com
2. Find your Azure AI Search resource
3. Click "Search explorer"
4. Run: `*` to see all indexed documents
5. Run: `MiFID II` to test keyword search
6. Select "Semantic" query type to test semantic search

---

## Common Issues

| Problem | Cause | Fix |
|---------|-------|-----|
| `AzureSearch:Endpoint not found` | `appsettings.Development.json` missing or ASPNETCORE_ENVIRONMENT not set | Create the file; set `$env:ASPNETCORE_ENVIRONMENT="Development"` before `dotnet run` |
| `Only PDF files are supported` error | File is not a PDF | Convert to PDF first |
| `Answers say "No relevant documents found"` | No documents uploaded | Upload documents first via `POST /api/documents` |
| `400 Bad Request` on query | `question` field is empty in JSON | Ensure JSON body has `"question": "your question here"` |
| Semantic search returns irrelevant results | Semantic configuration not set up | Create a semantic configuration in Azure AI Search portal named `"trade-semantic-config"` |
