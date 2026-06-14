# Trade Intelligence Platform — Architecture

## Problem Statement

MiFID II compliance checks on trade documents at investment banks are done manually — analysts read PDFs, compare against regulation clauses, and flag exceptions in spreadsheets. This is slow, error-prone, and doesn't scale.

This platform automates the document intelligence layer using RAG (Retrieval-Augmented Generation).

---

## C4 Context Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Azure Subscription                        │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              Trade Intelligence Platform                  │   │
│  │                                                          │   │
│  │  ┌─────────────┐    ┌──────────────┐    ┌────────────┐  │   │
│  │  │  PDF Upload  │───►│ Azure Blob   │───►│ AI Foundry │  │   │
│  │  └─────────────┘    │   Storage    │    │(extraction)│  │   │
│  │                      └──────────────┘    └─────┬──────┘  │   │
│  │                                                │         │   │
│  │                                                ▼         │   │
│  │  ┌─────────────┐    ┌──────────────┐    ┌────────────┐  │   │
│  │  │  NL Query   │───►│  SK RAG      │◄───│ Azure AI   │  │   │
│  │  └─────────────┘    │  Pipeline    │    │  Search    │  │   │
│  │                      └──────┬───────┘    └────────────┘  │   │
│  │                             │                            │   │
│  │                             ▼                            │   │
│  │                      ┌──────────────┐                   │   │
│  │                      │ Azure OpenAI │                    │   │
│  │                      │   GPT-4o     │                    │   │
│  │                      └──────────────┘                   │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Component Architecture

### RAG Pipeline (Semantic Kernel)

```
User Query
    │
    ▼
RagQueryService.QueryAsync()
    ├── 1. Hybrid Search (Azure AI Search)
    │       ├── Keyword search (BM25)
    │       ├── Vector search (text-embedding-3-large, 1536d)
    │       └── Semantic reranking (L2 cross-encoder)
    │       → Returns top-5 document chunks
    │
    ├── 2. Context assembly
    │       → Concatenate chunks with source labels
    │
    ├── 3. GPT-4o generation (Semantic Kernel)
    │       → System prompt + context + user question
    │       → Streamed answer with citations
    │
    └── 4. Compliance flag detection
            → Regex + keyword patterns on answer + context
            → MiFID II Art. 26/27, EMIR reporting flags
```

### Document Ingestion Pipeline

```
PDF Upload
    │
    ▼
DocumentIngestionService.IngestAsync()
    ├── 1. Upload to Azure Blob (archive, immutable)
    ├── 2. Extract text (Azure AI Foundry Document Intelligence)
    ├── 3. Chunk (1000 tokens, 100 overlap)
    ├── 4. Embed (text-embedding-3-large via Azure OpenAI)
    └── 5. Index (Azure AI Search — keyword + vector fields)
```

---

## Azure Infrastructure

| Resource | SKU | Role |
|----------|-----|------|
| Azure Container Apps | 0.5 vCPU / 1 GB | API hosting (scales to 0) |
| Azure AI Search | Standard S1 | Hybrid search index |
| Azure OpenAI | Standard 30K TPM | GPT-4o + embeddings |
| Azure Blob Storage | GRS | Document archive |
| Azure Key Vault | Standard | API keys, connection strings |

**Deployed via Bicep** (`infra/main.bicep`) — single command provisions all resources.

---

## Key Design Decisions

### Hybrid Search (not pure vector)
Pure vector search misses exact regulatory article references (e.g. "Art. 26"). Hybrid search combines BM25 (keyword precision) + vector (semantic similarity) + semantic reranker — optimal for compliance documents where exact terms matter.

### Semantic Kernel over raw OpenAI SDK
Semantic Kernel provides prompt template management, kernel memory abstraction, and easy swap between Azure OpenAI versions without rewriting pipeline logic.

### Chunk size 1000 / overlap 100
Tested against MiFID II regulation PDFs — 1000 tokens captures a complete article clause without splitting context. 100-token overlap prevents losing cross-sentence meaning at boundaries.

### Compliance flags from answer text (not separate call)
Running a second GPT-4o call for compliance detection would double latency and cost. Pattern matching on the generated answer achieves 80%+ recall for known violation patterns at zero additional cost.

---

## References

### Azure AI Search
- [Azure AI Search docs — hybrid search](https://learn.microsoft.com/en-us/azure/search/hybrid-search-overview)
- [Azure AI Search — semantic ranking](https://learn.microsoft.com/en-us/azure/search/semantic-search-overview)
- [YouTube: Azure AI Search — RAG with semantic ranking (Microsoft, 20 min)](https://www.youtube.com/watch?v=MU_r6N_NKRE)

### Semantic Kernel
- [Semantic Kernel docs](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
- [SK C# RAG sample (Microsoft)](https://github.com/microsoft/semantic-kernel/tree/main/dotnet/samples/Concepts/RAG)
- [YouTube: Semantic Kernel overview (Microsoft Reactor, 30 min)](https://www.youtube.com/watch?v=v0d4xPl0MkA)

### Azure OpenAI
- [Azure OpenAI Service docs](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [GPT-4o model card](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models#gpt-4o-and-gpt-4-turbo)
- [text-embedding-3-large — embedding model docs](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models#embeddings)

### MiFID II Compliance
- [MiFID II full text (EUR-Lex)](https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=celex%3A32014L0065)
- [ESMA MiFID II Q&A](https://www.esma.europa.eu/publications-and-data/interactive-single-rulebook/mifid-ii)

### Infrastructure
- [Azure Bicep docs](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [Azure Container Apps docs](https://learn.microsoft.com/en-us/azure/container-apps/)

---

## API Design

All endpoints follow REST conventions. Responses include `processingTimeMs` for SLA monitoring.

```
POST /api/query
  Body: { question, filters? }
  Response: { answer, citations[], complianceFlags[], processingTimeMs }

POST /api/documents/ingest
  Body: multipart/form-data (PDF)
  Response: { documentId, chunksIndexed, blobUrl }

POST /api/compliance/check
  Body: { documentText, regulations? }
  Response: { score (0-100), status, findings[], summary }
```

---

## Security

- Non-root Docker container
- All secrets in Azure Key Vault (never in code or config files)
- Azure AI Search private endpoint (no public access)
- RBAC throughout — no shared keys in production
