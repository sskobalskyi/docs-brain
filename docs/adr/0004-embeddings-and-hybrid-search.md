# 4. Embeddings through Microsoft.Extensions.AI and hybrid search with RRF

## Status
Accepted

## Context
Retrieval must work offline and for free, must not depend on a single vendor, and must keep working when the model server is down.

## Decision
- Embeddings use `IEmbeddingGenerator<string, Embedding<float>>`. `Ai:Provider` selects `ollama` (OllamaSharp, default model `nomic-embed-text`, 768 dimensions) or `mock` (a deterministic hashed bag of words, used by tests and CI).
- Chunks store `vector(768)` with an HNSW cosine index, plus a generated `tsvector` column (`simple` configuration, no stemming, so it works for `en` and `uk`) with a GIN index.
- Search runs the vector and keyword rankings separately and merges them with Reciprocal Rank Fusion (`1 / (60 + rank)`), top 20 candidates from each.
- Raw SQL bypasses EF query filters, so the tenant id is an explicit predicate and the chunks are reloaded through the filtered `DbSet` afterwards.
- Embedding is best effort. If the provider fails at ingestion, chunks are stored without a vector; if it fails at query time, search is keyword-only.

## Consequences
- The vector dimension is fixed in the schema. Changing the model to another size needs a schema change and re-embedding.
- `nomic-embed-text` is English-centric. `bge-m3` (1024 dimensions) is better for Ukrainian but heavier; switching is a schema and config change.
- Chunks stored without a vector are not re-embedded automatically yet.
