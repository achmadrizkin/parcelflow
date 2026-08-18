# ParcelFlow

ParcelFlow is a multi-tenant SaaS platform for last-mile parcel delivery.
Carrier companies (tenants) register parcels, dispatch drivers, track
delivery tasks through a strict lifecycle, and report on operations.

> **Candidates:** start with [TAKEHOME.md](TAKEHOME.md) — it explains the
> assignment. Then come back here to get the system running.

## Getting started

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and a local
MongoDB (`docker-compose up -d mongodb`). Storage is Mongo-only; `seed/` is
imported into it at startup.

```bash
docker-compose up -d mongodb
dotnet build
dotnet test
dotnet run --project src/ParcelFlow.Api
```

The API listens on `http://localhost:5000` (Swagger UI at `/swagger`).

## Working with the API

Every request (except `/health` and `/swagger`) must carry an `X-Tenant-Id`
header. The seeded tenants:

| Tenant id | Name | Country |
|---|---|---|
| `nusantara-express` | Nusantara Express | ID |
| `manila-swift` | Manila Swift Logistics | PH |
| `garuda-cargo` | Garuda Cargo Lines | ID |

Examples:

```bash
# List a tenant's delivery tasks
curl -H "X-Tenant-Id: nusantara-express" "http://localhost:5000/api/tasks"

# Register a parcel (also opens its delivery task)
curl -X POST -H "X-Tenant-Id: nusantara-express" -H "Content-Type: application/json" \
  -d '{"reference":"NE-2026-99999","recipientName":"Test Person","city":"Jakarta","weightKg":1.5}' \
  "http://localhost:5000/api/parcels"

# Daily ops report
curl -H "X-Tenant-Id: nusantara-express" "http://localhost:5000/api/reports/daily-summary?day=2026-07-01"
```

The `PendingAssignmentsWorker` runs inside the API host and sweeps for
unassigned tasks every 30 seconds — you will see auto-assignments in the logs
for tenants that have drivers on an open shift.

## Where things live

Read `docs/ARCHITECTURE.md` for the map, `docs/DOMAIN_GLOSSARY.md` for the
vocabulary, and `docs/adr/` for why things are the way they are.

```
src/
  LegacyCourier.Common/    shared primitives (legacy name, still ours)
  ParcelFlow.Domain/       entities, state machine, domain events
  ParcelFlow.Storage/      tenant-scoped repositories (MongoDB)
  ParcelFlow.Services/     business operations & reporting
  ParcelFlow.Events/       event → rule → action pipeline
  ParcelFlow.Workers/      background jobs
  ParcelFlow.Api/          REST API host, tenant middleware, DI wiring
tests/
  ParcelFlow.Tests/        xunit test suite (each test gets its own throwaway Mongo database)
seed/                      fixture data imported into MongoDB at startup
```

## Running the API

Storage is MongoDB-only. Start a local instance, then run the API:

```bash
docker-compose up -d mongodb
dotnet run --project src/ParcelFlow.Api
```

On startup the API imports `seed/` into the `parcelflow` database (upsert by
Id, safe to re-run). Set `Storage:Mongo:SeedOnStartup` to `false` in
`src/ParcelFlow.Api/appsettings.json` to skip this.

Tests also need a local MongoDB — they connect to
`mongodb://localhost:27017` by default (override with
`PARCELFLOW_TEST_MONGO_URI`) and create/drop a uniquely-named database per
test.
