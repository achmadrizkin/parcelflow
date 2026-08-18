# ADR-0005: Tenant-scope ReportService; close PF-902

**Status:** Accepted — 2026-08-18

## Context

PF-1287 (critical): Nusantara Express pulled their daily summary report and
saw parcel references, cities, and driver names that were not theirs. Root
cause: `ReportService.GetDailySummaryAsync` called
`ITenantScopedRepository<T>.QueryAllTenantsAsync` for tasks, parcels, and
drivers — the repository method explicitly documented as legacy,
migration-tooling-only, and "must never be called from request-handling
code paths" (see docs/adr/0002-tenant-isolation-by-tenantid.md). It was the
only caller of that method anywhere in `src/`.

This is precisely the follow-up ADR-0003 flagged as PF-902: the
DataWarehouse-era aggregation code was ported into `ParcelFlow.Services`
"largely as-is," keeping its single-pass-over-all-tenants shape instead of
being reworked to the standard tenant-scoped request pattern. It shipped,
and a tenant hit it.

## Decision

1. `ReportService` now takes `ITenantContext` and scopes every query
   (`GetDailySummaryAsync`, and the new `GetWeeklyDriverPerformanceAsync`
   added for Part C of this work) to `_tenant.TenantId` via the standard
   `QueryAsync(tenantId, predicate)`, exactly like every other service in
   the codebase.
2. `QueryAllTenantsAsync` itself is **not removed** — ADR-0003 (immutable)
   retains it for migration tooling, and no reason has emerged to revisit
   that. Instead, `ArchitectureRulesTests` now fails the build if any
   `.cs` file under `ParcelFlow.Services` or `ParcelFlow.Api` calls it, so
   the invariant ADR-0002 already documented is enforced by the test suite
   rather than by developers remembering a doc comment.

## Consequences

- Daily-summary and weekly-driver-performance reports are correctly
  tenant-scoped; PF-1287 cannot recur through these code paths.
- Any future request-handling code that reaches for
  `QueryAllTenantsAsync` fails `dotnet test` immediately, with a message
  pointing at ADR-0002 and this ADR, instead of shipping silently.
- PF-902 is closed: no remaining DW-era cross-tenant query exists in
  `ParcelFlow.Services`.
