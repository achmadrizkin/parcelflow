# ParcelFlow — Architecture Overview

> Maintained by the platform team. Some sections may lag behind the code —
> when the code and this document disagree, the code wins. PRs to fix drift
> are welcome.

## §1 — What ParcelFlow is

ParcelFlow is a multi-tenant SaaS platform for last-mile parcel delivery.
Carrier companies (our **tenants**) use it to register parcels, dispatch
drivers, track delivery tasks through their lifecycle, and report on
operations.

**Multi-tenancy is the single most important architectural constraint.**
All tenants share one database; the `TenantId` field on every document is the
only isolation boundary. See ADR-0002.

## §2 — Solution map

| Project | Role |
|---|---|
| `LegacyCourier.Common` | Shared primitives (Result, IdGenerator, Clock, Guard). Named after the original product; kept for compatibility. |
| `ParcelFlow.Domain` | Entities, the delivery task state machine, domain events. No I/O. |
| `ParcelFlow.Storage` | Tenant-scoped repository abstraction + MongoDB implementation. |
| `ParcelFlow.Services` | Business operations: parcels, task lifecycle, assignment, shifts, reporting. |
| `ParcelFlow.Events` | In-process event pipeline: events → rules → notification actions. |
| `ParcelFlow.Workers` | Background jobs (currently the pending-assignments sweep). |
| `ParcelFlow.Api` | ASP.NET Core host: REST API, tenant middleware, DI wiring, hosts the workers. |
| `ParcelFlow.DataWarehouse` | Analytics pipeline: nightly per-tenant report exports. |

## §3 — Request flow

```
Client ──► ParcelFlow.Api
              │  TenantResolutionMiddleware (X-Tenant-Id → ITenantContext)
              ▼
          Controllers ──► Services ──► ITenantScopedRepository ──► store
                              │
                              └──► IEventDispatcher ──► rules ──► actions (sms/email/webhook)
```

Every request carries an `X-Tenant-Id` header. The middleware validates it
against the tenant directory and populates the scoped `ITenantContext`;
services take the tenant from the context, never from request payloads.

## §4 — Delivery task lifecycle

The task state machine lives in
`ParcelFlow.Domain/StateMachine/DeliveryTaskStateMachine.cs` and is the only
sanctioned way to change a task's status. Current states: Created, Assigned,
PickedUp, InTransit, AttemptFailed, Delivered (terminal), Cancelled
(terminal), ReturnScheduled, ReturnCompleted (terminal).

Failed attempts increment `AttemptCount` and raise `DeliveryAttemptFailedEvent`.
The tenant's ops channel gets an alert from the second failed attempt (see
`RepeatedFailureOpsAlertRule`). From the **3rd** failed attempt, the task is
automatically moved to `ReturnScheduled` — no more retries — the recipient is
SMS'd, and ops gets a second, specific alert (see
`ScheduleReturnOnThirdFailedAttemptRule`, registered like every other
`IEventRule`). A driver later completes the return at the hub via
`POST /api/tasks/{id}/complete-return`, which moves the task to the terminal
`ReturnCompleted` state. A scheduled return can still be cancelled
(`ReturnScheduled → Cancelled`), same as any other open task.

## §5 — Events

Domain events are dispatched in-process and synchronously by
`EventDispatcher`. Rules are DI-registered `IEventRule` implementations; rule
failures are logged and never propagate to the caller. Notification actions
(email/SMS/ops-webhook) are logging stubs in this codebase — the integration
seam is what matters.

In production this dispatcher sits behind a message bus. Keep rules idempotent.

## §6 — Reporting

The DataWarehouse module described here previously was retired (ADR-0003);
reporting is served live from the main API by `ReportService`, scoped to the
current tenant like every other service (see ADR-0005, which closed the
PF-902 follow-up after a cross-tenant leak — PF-1287 — was traced to this
code still running an unscoped, DW-era query).

- `GET /api/reports/daily-summary?day=YYYY-MM-DD` — every task that reached
  a terminal state or had a failed attempt on the given UTC day, for the
  requesting tenant.
- `GET /api/reports/weekly-driver-performance?asOf=YYYY-MM-DD` — per-driver
  tasks delivered, failed attempts, and average hours from assignment to
  delivery over the trailing 7 days, as CSV.

## §7 — Storage

`ITenantScopedRepository<T>` is backed by MongoDB (`ParcelFlow.Storage.Mongo`);
run `docker-compose up -d mongodb`. The repo's `seed/` fixtures are imported
into Mongo on API startup (upsert by Id — see `MongoSeeder`), unless
`Storage:Mongo:SeedOnStartup` is set to `false`. `ITenantDirectory` is
likewise Mongo-backed (`MongoTenantDirectory`, `tenants` collection).

Repository methods take an explicit `tenantId` and guarantee scoping. The
interface also carries a legacy `QueryAllTenantsAsync` used by migration
tooling — it must never be called from request paths.

## §8 — Background workers

`PendingAssignmentsWorker` sweeps all tenants every 30 seconds and
auto-assigns `Created` tasks to the driver with the most spare capacity among
those on an open shift. Note the scope-per-tenant pattern: a fresh DI scope
(and thus a fresh `TenantContext`) per tenant per pass.
