# ADR-0003: Retire the DataWarehouse module

**Status:** Accepted — 2025-11-02

## Context

`ParcelFlow.DataWarehouse` ran a nightly pipeline that read the day's task
activity across all tenants in one pass and produced per-tenant report
exports. It was our only consumer of the repository's cross-tenant query
path, and its operational cost (a dedicated nightly job, its own API, stale
data by up to 24h) was no longer justified for the report volume we serve.

## Decision

Retire the module. Serve reports on demand from the main API instead. The
DW's aggregation logic moves into `ParcelFlow.Services` largely as-is, to be
cleaned up in a follow-up (tracked as PF-902).

## Consequences

- Reports are now real-time rather than day-old exports.
- `QueryAllTenantsAsync` remains on the repository interface for the
  migration tooling; it should be removed once the last legacy consumer is
  gone.
- **Follow-up (PF-902, open):** the ported aggregation code still reflects
  DW-era assumptions (single pass over all tenants, splitting downstream).
  It needs to be reworked to the standard tenant-scoped request pattern.
