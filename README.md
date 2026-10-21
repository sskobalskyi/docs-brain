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
Health check: `GET /health`. In Development, `POST /dev/token` with `{"tenantId":"11111111-1111-1111-1111-111111111111"}` (Acme) or `2222...` (Globex) returns a JWT for `/api/documents`.

Upload a PDF, DOCX, TXT or MD file: `POST /api/documents/upload` (multipart field `file`). Inspect the result with `GET /api/documents/{id}/chunks`.

Search: `GET /api/search?q=...`. Embeddings come from Ollama by default: `docker compose --profile llm up -d` starts it and pulls `nomic-embed-text`. Set `Ai__Provider=mock` to run without models.

## Architecture decisions
- [ADR 0001: Modular monolith on PostgreSQL with pgvector](docs/adr/0001-modular-monolith-with-pgvector.md)
- [ADR 0002: Tenant isolation](docs/adr/0002-tenant-isolation.md)
- [ADR 0003: Ingestion pipeline](docs/adr/0003-ingestion-pipeline.md)
- [ADR 0004: Embeddings and hybrid search](docs/adr/0004-embeddings-and-hybrid-search.md)
