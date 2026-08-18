# ADR-0004: MongoDB as the only storage provider

**Status:** Accepted — 2026-07-14

## Context

ParcelFlow previously shipped a process-local, dictionary-backed
`ITenantScopedRepository<T>` as the local-dev default, with MongoDB as an
opt-in alternative behind `Storage:Provider`. This left two implementations
to keep in sync, and the tenant directory (`ITenantDirectory`) always used
the dictionary-backed store regardless of the configured provider — so
switching to Mongo never actually made tenant lookups or seed data live in
Mongo.

## Decision

Remove the dictionary-backed storage provider entirely. `ParcelFlow.Api`
always wires `MongoTenantScopedRepository<T>` and `MongoTenantDirectory`.
`MongoSeeder` imports `seed/` into Mongo collections on startup (upsert by
Id), gated by `Storage:Mongo:SeedOnStartup`. The test suite (`TestWorld`)
now runs against a real MongoDB too, giving each test its own throwaway
database, dropped on completion.

## Consequences

- A local MongoDB (`docker-compose up -d mongodb`) is required to run the
  API or the test suite — `dotnet run`/`dotnet test` no longer work with
  zero external dependencies.
- Only one `ITenantScopedRepository<T>` implementation to maintain.
- Tenant lookups and seed data are consistently backed by Mongo, in both
  the API and tests.
- Test runs create disposable `parcelflow_test_*` databases; each test
  drops its own on completion via `TestWorld.Dispose()`.
