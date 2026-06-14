# Development Guide

## Prerequisites

- .NET 10 SDK
- Azure subscription with:
  - Azure AI Search (Standard S1)
  - Azure OpenAI (GPT-4o + text-embedding-3-large deployments)
  - Azure Storage Account
- Docker (optional, for containerised run)

## Local Setup

```bash
# Clone
git clone https://github.com/milesbusiness/trade-intelligence-platform
cd trade-intelligence-platform

# Configure
cp src/TradeIntelligence.Api/appsettings.json src/TradeIntelligence.Api/appsettings.Development.json
```

Edit `appsettings.Development.json`:
```json
{
  "AzureSearch": {
    "Endpoint": "https://your-search.search.windows.net",
    "ApiKey": "your-admin-key",
    "IndexName": "trade-documents"
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-openai.openai.azure.com",
    "ApiKey": "your-key",
    "ChatDeployment": "gpt-4o",
    "EmbeddingDeployment": "text-embedding-3-large"
  },
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;..."
  }
}
```

## Running Locally

```bash
cd src/TradeIntelligence.Api
dotnet run

# API available at:
# http://localhost:5000/swagger  ← Swagger UI
# http://localhost:5000/health   ← Health check
```

## Running with Docker

```bash
docker build -t trade-intelligence-platform .
docker run -p 8080:8080 \
  -e AzureSearch__Endpoint=https://... \
  -e AzureSearch__ApiKey=... \
  -e AzureOpenAI__Endpoint=https://... \
  -e AzureOpenAI__ApiKey=... \
  -e AzureStorage__ConnectionString=... \
  trade-intelligence-platform
```

## Project Structure

```
trade-intelligence-platform/
├── src/TradeIntelligence.Api/
│   ├── Controllers/
│   │   ├── QueryController.cs       ← POST /api/query
│   │   ├── DocumentsController.cs   ← POST /api/documents/ingest
│   │   └── ComplianceController.cs  ← POST /api/compliance/check
│   ├── Services/
│   │   ├── RagQueryService.cs       ← RAG pipeline (SK + AI Search)
│   │   ├── DocumentIngestionService.cs ← PDF → chunks → index
│   │   ├── ComplianceCheckService.cs   ← MiFID II/EMIR checking
│   │   └── AzureSearchIndexService.cs  ← Index creation/management
│   ├── Models/
│   │   └── Models.cs                ← Request/response DTOs + index document
│   └── Program.cs                   ← DI wiring, middleware
├── infra/
│   └── main.bicep                   ← Azure infrastructure as code
└── docs/
    ├── ARCHITECTURE.md
    └── DEVELOPMENT.md
```

## Quick API Test

```bash
# Ingest a document
curl -X POST http://localhost:5000/api/documents/ingest \
  -F "file=@mifid2-guidance.pdf"

# Query it
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"question": "What are the best execution requirements under MiFID II Article 27?"}'

# Compliance check
curl -X POST http://localhost:5000/api/compliance/check \
  -H "Content-Type: application/json" \
  -d '{"documentText": "Trade executed at 10:32 UTC. Instrument: EURUSD. Notional: 5,000,000 EUR.", "regulations": ["MiFID II", "EMIR"]}'
```
