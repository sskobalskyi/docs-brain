# 1. Modular monolith on PostgreSQL with pgvector

## Status
Accepted

## Context
Docs-Brain is a multi-tenant RAG assistant. It needs relational data (tenants, documents, budgets), vector search and keyword search. It must run locally with `docker compose up`.

## Decision
- One ASP.NET Core host with internal modules (ingestion, retrieval, chat, tenancy) instead of microservices. The module boundaries are namespaces and interfaces, so a module can be extracted later.
- PostgreSQL with `pgvector` stores chunks, embeddings and a `tsvector` column, so hybrid search (vector + full text) runs in one database and one transaction scope.
- The vector store sits behind an interface, so Qdrant can be swapped in.

## Consequences
- One database to run, back up and test with Testcontainers.
- Vector search scale is bounded by Postgres. That is acceptable for the project's scope.
