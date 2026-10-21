# Docs-Brain

Multi-tenant RAG assistant: upload documents, ask questions, get streamed answers with source citations.

> Status: early scaffold. See the plan below.

## Planned features
- Modular monolith in ASP.NET Core, tenant isolation (EF Core query filters and a tenant claim), Minimal APIs, Result pattern.
- Ingestion: upload, text extraction (PdfPig, OpenXML), chunking, embeddings.
- Hybrid search (vector and keyword) in PostgreSQL with pgvector.
- LLM through `Microsoft.Extensions.AI`: Ollama or a mock provider by default, per-tenant budgets, rate limiting, caching, retrieval-only fallback.
- React UI with streamed answers (SSE), citations, `en` and `uk` locales.
- Evals: a small question and answer set run in CI against the mock provider.

## Run
```powershell
docker compose up -d postgres
dotnet run --project src/DocsBrain.Api
```
Health check: `GET /health`.

## Architecture decisions
- [ADR 0001: Modular monolith on PostgreSQL with pgvector](docs/adr/0001-modular-monolith-with-pgvector.md)
