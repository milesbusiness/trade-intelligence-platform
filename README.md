# Trade Intelligence Platform

> **AI-powered document intelligence for trading compliance — answer regulatory questions in seconds, not hours. Built on Azure AI Search, GPT-4o, and Semantic Kernel.**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![Azure OpenAI](https://img.shields.io/badge/Azure_OpenAI-GPT--4o-0089D6?logo=microsoft-azure)](https://azure.microsoft.com/products/ai-services/openai-service)
[![Azure AI Search](https://img.shields.io/badge/Azure_AI_Search-Standard_S1-0089D6?logo=microsoft-azure)](https://azure.microsoft.com/products/ai-services/ai-search)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

---

## The Problem

Compliance teams at investment banks and asset managers operate under thousands of pages of regulatory documentation — MiFID II, EMIR, internal policies, audit reports, client agreements. Finding a specific requirement or checking whether a trade satisfies a regulatory obligation currently takes hours of manual search.

**The cost:** Senior compliance professionals spending 30–40% of their time on document retrieval rather than analysis. That is €40,000+ per person per year in unproductive time, plus the regulatory risk of missing something.

## The Solution

Upload your regulatory documents once. Ask questions in plain English forever. Get cited, auditable answers in under 30 seconds.

The platform uses **hybrid search** — combining keyword precision (critical for article references like "Art. 26") with semantic understanding (critical for conceptual questions) — and GPT-4o to generate accurate, cited responses.

---

## What It Does

### Natural Language Regulatory Q&A
```
Question: "What are the transaction reporting obligations for derivatives under MiFID II?"

Answer: "Under MiFID II Article 26, investment firms that execute transactions in financial
instruments must report complete and accurate details of the transaction to the competent
authority no later than the close of the following working day..."

Citations: [MiFID-II-Consolidated-2024.pdf, page 47] [ESMA-QA-March-2024.pdf, page 12]
```

### Automated Compliance Scoring
Submit a trade record or policy document. Receive an objective compliance score (0–100) against MiFID II and EMIR, with specific findings and remediation guidance.

### Multi-Document Synthesis
Ask questions that span multiple documents — the platform retrieves and synthesises information across your entire document library simultaneously.

### Audit-Ready Citations
Every answer includes the exact document, page number, and text used to generate the response. Fully auditable and traceable.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                   Trade Intelligence Platform                    │
│                                                                  │
│  PDF Upload ──► Azure Blob Storage (immutable archive)          │
│                        │                                         │
│                        ▼                                         │
│              Document Ingestion Pipeline                         │
│              ├── Text extraction (AI Document Intelligence)      │
│              ├── Chunking (1,000 tokens, 100 overlap)           │
│              ├── Embedding (text-embedding-3-large, 1536d)      │
│              └── Indexing (Azure AI Search)                     │
│                        │                                         │
│  User Query ──────────►│                                         │
│                        ▼                                         │
│              RAG Pipeline (Semantic Kernel)                      │
│              ├── Hybrid search (BM25 + vector + reranker)       │
│              ├── Top-5 chunks assembled as context              │
│              ├── GPT-4o generates cited answer                  │
│              └── Compliance flags detected                       │
│                        │                                         │
│                        ▼                                         │
│              Response: answer + citations + flags                │
└─────────────────────────────────────────────────────────────────┘
```

---

## API Reference

### Query Documents
```http
POST /api/query
Content-Type: application/json

{
  "question": "What are the best execution requirements for retail clients under MiFID II?",
  "filters": { "category": "regulatory" }
}
```

**Response:**
```json
{
  "answer": "Under MiFID II Article 27, firms executing orders on behalf of retail clients must take all sufficient steps to obtain the best possible result, considering price, costs, speed, likelihood of execution, and size...",
  "citations": [
    {
      "document": "MiFID-II-Level1-2014.pdf",
      "page": 47,
      "excerpt": "Investment firms shall take all sufficient steps to obtain..."
    }
  ],
  "complianceFlags": [],
  "processingTimeMs": 1840
}
```

### Ingest Document
```http
POST /api/documents/ingest
Content-Type: multipart/form-data

file: [PDF file]
```

### Compliance Check
```http
POST /api/compliance/check
Content-Type: application/json

{
  "documentText": "Order executed at 10:32 UTC. EURUSD. Notional EUR 5,000,000. Venue: EBS.",
  "regulations": ["MiFID II", "EMIR"]
}
```

**Response:**
```json
{
  "score": 62,
  "status": "NeedsReview",
  "findings": [
    "EMIR Art. 9 transaction reporting not evidenced",
    "MiFID II Art. 26 trade report timestamp format unclear"
  ],
  "summary": "Trade record incomplete for regulatory reporting purposes..."
}
```

---

## Technology Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| API | ASP.NET Core (.NET 10) Minimal API | High-performance REST API, non-root container |
| AI Orchestration | Microsoft Semantic Kernel 1.30 | Prompt management, LLM abstraction, plugin system |
| Language Model | Azure OpenAI GPT-4o | Answer generation with citation awareness |
| Embedding Model | Azure OpenAI text-embedding-3-large (1536d) | Semantic document vectorisation |
| Search | Azure AI Search Standard S1 | Hybrid BM25 + HNSW vector + L2 semantic reranker |
| Document Storage | Azure Blob Storage (GRS) | Immutable regulatory archive, geo-redundant |
| Secrets | Azure Key Vault | API keys, connection strings, certificate management |
| Hosting | Azure Container Apps | Serverless, auto-scale 0–10, HTTPS, managed identity |
| Infrastructure | Azure Bicep | One-command full environment provisioning |

---

## Compliance Coverage

Built-in detection patterns for:

| Regulation | Articles | Key Requirements |
|-----------|---------|-----------------|
| MiFID II | Art. 25, 26, 27, 31 | Suitability, transaction reporting, best execution, monitoring |
| EMIR | Art. 4, 9, 11 | Clearing obligation, reporting, risk mitigation |
| MAR | Art. 16 | Suspicious transaction reporting |

---

## Deployment

### One-Command Azure Provisioning
```bash
az group create --name rg-trade-intelligence --location westeurope

az deployment group create \
  --resource-group rg-trade-intelligence \
  --template-file infra/main.bicep \
  --parameters environment=prod
```

Provisions: Container Apps, AI Search S1, Azure OpenAI, Blob Storage, Key Vault.

---

## Business Case

| Metric | Before | After |
|--------|--------|-------|
| Answer a regulatory question | 2–4 hours | < 30 seconds |
| Pre-audit compliance review | 3–5 days | 2–4 hours |
| New analyst regulatory onboarding | 3–6 months | Days |
| Regulatory fine risk | High (manual, error-prone) | Low (automated, auditable) |

**ROI for 10-person compliance team:** €400,000+/year in recovered productive capacity.

---

## Documentation

| Document | Description |
|----------|-------------|
| [Executive Summary](docs/EXECUTIVE_SUMMARY.md) | Business case, ROI analysis, stakeholder guide |
| [Architecture Guide](docs/ARCHITECTURE.md) | RAG pipeline, hybrid search design, decisions |
| [Development Guide](docs/DEVELOPMENT.md) | Local setup, API testing, extending the platform |
| [Deployment Guide](docs/DEPLOYMENT.md) | Azure provisioning, Container Apps, scaling |

---

## About

Built to demonstrate enterprise-grade RAG architecture for regulated financial services, targeting Principal Architect and AI Solution Architect roles at European financial institutions.

**Author:** Dilip Kumar Jena | **Platform:** Microsoft Azure | **Regulation:** MiFID II, EMIR, MAR
