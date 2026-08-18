# ADR-0002: Tenant isolation by TenantId on a shared database

**Status:** Accepted — 2024-03-19

## Context

ParcelFlow serves many carrier companies from one deployment. We evaluated
database-per-tenant, collection-per-tenant, and a shared store with a
discriminator field.

## Decision

One shared database. Every document carries a `TenantId` field, and **that
field is the entire isolation boundary**. All data access goes through
`ITenantScopedRepository<T>`, whose methods require an explicit `tenantId`
and apply it to every operation.

## Consequences

- Operationally simple: one database to run, back up, and migrate.
- **The trade-off is discipline:** nothing at the infrastructure level stops
  a query that forgets the tenant filter. A single unscoped query can expose
  one tenant's data to another. Treat any unscoped access as a
  release-blocking security defect, not a code smell.
- The repository interface is the enforcement point. Raw access to the
  underlying store from feature code is prohibited.
