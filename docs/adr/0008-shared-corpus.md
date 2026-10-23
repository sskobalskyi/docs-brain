# 8. Shared corpus owned by a reserved tenant

## Status
Accepted. Extends [ADR 2](0002-tenant-isolation.md).

## Context
In the Greenhaus ecosystem every user is their own tenant, but plant care knowledge is the same for everyone. Duplicating it per tenant would waste storage and make updates impossible.

## Decision
- Shared documents belong to a reserved tenant, `SharedTenant.Id` (`00000000-0000-0000-0000-000000000001`), so every row still has a `TenantId`.
- Reads: the query filter for documents and chunks becomes `TenantId == current OR TenantId == shared`, and the raw hybrid search SQL uses `TenantId IN (current, shared)`. A caller without a tenant sees nothing, shared documents included.
- Writes: a request's context always stamps the request's tenant, so a tenant cannot create a shared row. Shared rows are written only by `SharedCorpus`, which opens its own context for the shared tenant, behind `/api/shared/documents`, which requires the service role (`Auth:ServiceRole`).
- The reserved id can never be a caller's tenant: a token claim or an `X-Tenant-Id` naming it resolves to no tenant and is forbidden.
- `PUT /api/shared/documents/{title}` is idempotent. The new version is ingested first and the previous one with that title is removed afterwards, so an unreadable file leaves the old version in place. `GET` lists and `DELETE /{id}` removes.
- `GET /api/documents` of a tenant lists only its own documents. Shared passages appear as sources with `shared: true` (search hits and the chat `sources` event); the UI marks them with a badge.

## Consequences
- The daily token budget and usage stay per tenant; the shared corpus has no budget of its own.
- The text search uses the `simple` configuration without stemming, so "orchid" does not match "orchids". Shared texts should use the words people ask with (or both forms).
- Updating a shared document replaces it, and cached passages in a client may be stale until the next search.
- A deleted shared document disappears for all tenants at once.
