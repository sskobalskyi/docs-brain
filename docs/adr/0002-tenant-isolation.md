# 2. Tenant isolation with a tenant claim and EF Core query filters

## Status
Accepted

## Context
All tenants share one database. A missing filter must never leak another tenant's data.

## Decision
- The JWT carries a `tenant_id` claim. The default authorization policy requires it, so every protected endpoint has a tenant.
- `ITenantContext` exposes the claim. `DocsBrainDbContext` applies a global query filter `TenantId == CurrentTenantId` to every tenant-owned entity and stamps `TenantId` on insert.
- A caller without a tenant matches nothing on reads and cannot save tenant-owned entities (fail closed).
- Cross-tenant reads return 404, not 403, so ids are not confirmable.

## Consequences
- Queries that must cross tenants (background jobs, admin) have to opt out explicitly with `IgnoreQueryFilters()`.
- Schema is created with `EnsureCreated` for now; migrations come with the ingestion tables.
- Postgres row-level security could be added later as defence in depth.
