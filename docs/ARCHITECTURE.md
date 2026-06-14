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
