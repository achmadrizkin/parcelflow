# SOLUTION.md

## Overview

Three pieces of work: fixed a critical cross-tenant data leak in the daily
summary report (Part A), added an automatic "Return to Sender" lifecycle
after the 3rd failed delivery attempt (Part B), and added a weekly
per-driver performance CSV report (Part C). Git history is organized as one
commit per part plus the initial baseline; each commit message has the full
rationale, this file is the summary. `AI-USAGE.md` covers tooling.

One environment note up front: this sandbox has no local .NET SDK and no
running Docker daemon, so I could not run `dotnet build` / `dotnet test`
myself here. Everything below was written and reviewed carefully against
the existing code (constructor signatures, DI wiring, test helper shapes),
but it has **not been compiled**. Please run `docker-compose up -d mongodb`
and `dotnet test` before relying on it — see "Known limitations" below.

## Part A — PF-1287 (cross-carrier data in the daily summary)

**How I found it:** started from `docs/ARCHITECTURE.md`, which points at
`ITenantScopedRepository<T>` and ADR-0002 ("tenant isolation is a security
boundary, not a convention") as the load-bearing invariant of the whole
codebase. `ADR-0003` (retiring the DataWarehouse module) has an explicit
open follow-up, PF-902: the ported reporting code "still reflects DW-era
assumptions (single pass over all tenants...)". That's the bug ticket,
basically pre-announced. `ReportService.GetDailySummaryAsync` confirmed it:
all three of its queries (`tasks`, `parcels`, `drivers`) called
`QueryAllTenantsAsync` — the repository method whose own doc comment says
"must never be called from request-handling code paths." It's the *only*
caller of that method anywhere in `src/`.

**Root cause:** the DataWarehouse pipeline used to run one pass over every
tenant and split the output downstream per tenant. When it was retired and
its aggregation logic "moved into `ParcelFlow.Services` largely as-is" (per
ADR-0003), the single-pass shape moved with it, into a method that is now a
live, per-request API endpoint with no downstream split. Every tenant's
`daily-summary` call returned every tenant's tasks, with parcels/drivers
looked up by a flat `Id` dictionary that made no attempt to disambiguate
tenants either.

**Fix:** `ReportService` now takes `ITenantContext` and scopes every query
to `_tenant.TenantId` via the same `QueryAsync(tenantId, predicate)` every
other service already uses.

**Beyond the single symptom:** the ticket asks whether the fix prevents the
*class* of bug, not just this instance. My answer: partially by
construction, fully by an enforced check. `QueryAllTenantsAsync` is
explicitly retained (ADR-0003, which is immutable — see ADR-0001 — so I
didn't edit it) for migration tooling that doesn't currently exist in this
repo. That means the method is a loaded gun sitting on the repository
interface with no legitimate caller today. Removing it would exceed what
this ticket asked for and would contradict a standing ADR I have no new
information to override. Instead I added `ArchitectureRulesTests`, which
scans `ParcelFlow.Services` and `ParcelFlow.Api` source for calls to
`QueryAllTenantsAsync` and fails the build if it finds one — turning "must
never be called from request-handling code" from a comment a developer has
to remember into something `dotnet test` enforces. `ADR-0005` records this
and formally closes PF-902.

**Proof:** `ReportServiceTests.Daily_summary_never_includes_another_tenants_data`
reproduces the ticket almost literally — a second tenant with a different
city and driver name, sharing the store — and asserts none of it leaks into
tenant A's report. This test fails against the pre-fix code and passes
after.

**What I'd tell the customer:** their data was never exposed to other
carriers through normal use of the product — the bug was in a read path
that mixed everyone's rows together in the *response*, not a storage or
access-control breach, and it's now fixed and covered by a regression test
plus a standing architectural check. What I'd tell the team: this is a
direct consequence of porting code across an architectural boundary
"as-is" and not closing the follow-up ticket before it shipped — worth
treating `NOTE(PF-xxx): due a clean-up` comments as blockers, not FYIs,
next time a module gets retired.

## Part B — Return to Sender

**Design:** two new states, `ReturnScheduled` and `ReturnCompleted`
(terminal). `AttemptFailed → ReturnScheduled` (automatic, on the 3rd failed
attempt) and `ReturnScheduled → {ReturnCompleted, Cancelled}`. The state
machine remains the single enforcement point — see
`DeliveryTaskStateMachine.cs`.

The automatic trigger is a new `IEventRule`
(`ScheduleReturnOnThirdFailedAttemptRule`) reacting to
`DeliveryAttemptFailedEvent` where `AttemptNumber >= 3`, registered in DI
exactly like the two existing rules. One structural constraint shaped this:
`ParcelFlow.Events` has no project reference to `ParcelFlow.Services`
(and can't get one without creating a cycle, since `Services` already
references `Events`), so the rule can't call `DeliveryTaskService`. It
mutates the task directly through `DeliveryTaskStateMachine.Transition` +
the repository — which is in fact the actual documented invariant
("any status change... MUST go through `Transition`", not "must go through
`DeliveryTaskService`"), so this isn't a workaround, it's the correct
enforcement point.

No extra idempotency flag was needed: the state machine only allows
`AttemptFailed → ReturnScheduled`, so once a task is scheduled, a further
`RecordFailedAttemptAsync` call fails at the state-machine layer before a
new event is even raised.

**Calls made where the spec was silent:**
- **Can a scheduled return be cancelled?** Yes — `ReturnScheduled →
  Cancelled` is allowed, consistent with every other non-terminal state in
  the machine already permitting cancellation. An ops team abandoning a
  return (parcel lost, customer resolved it directly, etc.) seemed more
  likely than not, and blocking it would be an inconsistent special case.
- **Endpoint shape:** `POST /api/tasks/{id}/complete-return` with an
  optional `{ "note": "..." }` body, mirroring the existing
  `DeliveredRequest.PodNote` pattern (`{id}/delivered`).
- **Notification wording:** kept plain and factual ("unable to deliver...
  being returned to the sender") rather than inventing a tone/brand voice
  that isn't specified anywhere else in the stub actions.
- **A second ops alert:** `RepeatedFailureOpsAlertRule` already fires from
  the 2nd failed attempt onward, so the 3rd attempt produces *two*
  ops-webhook messages (the generic repeated-failure alert, and this
  feature's specific "scheduled for return" alert). I left both — they say
  different things and ops-webhook is a stub — but flagging it as a minor
  duplication worth a product call if this were real.

**Proof:** state-machine transition-table tests for the two new states;
`DeliveryTaskServiceTests` for `CompleteReturnAsync` (happy path +
rejection from the wrong state); `ReturnToSenderTests` covers the rule in
isolation, an end-to-end test that wires a **real** `EventDispatcher` (not
`TestWorld`'s default recording one) and drives three real failed attempts
through `DeliveryTaskService` to prove the trigger is genuinely automatic
through the actual pipeline, and a multi-tenant test sharing one store
across two `TenantContext`s to prove the automatic return never crosses
tenant lines.

## Part C — Weekly driver performance

`GET /api/reports/weekly-driver-performance?asOf=YYYY-MM-DD` returns CSV:
per driver, tasks delivered, failed attempts, and average hours from
assignment to delivery, for the 7 days ending at `asOf` (defaults to now).
`ReportService.GetWeeklyDriverPerformanceAsync` walks each task's audit
`History` rather than filtering tasks by `UpdatedUtc`, so a failed attempt
inside the window still counts even if the same task was later delivered —
and its `UpdatedUtc` bumped — outside the window. Kept to the timebox: no
CSV library dependency (a five-column hand-rolled writer is enough, and is
independently unit-tested), no pagination/streaming, one query per report
call — all fine at this data scale, and any of it can be revisited if the
report grows.

**How I'd run this every Monday in production:** a scheduled trigger (a
Kubernetes CronJob, or a Hangfire/Quartz recurring job inside the API host)
firing early Monday UTC, iterating active tenants via the existing
`ITenantDirectory.GetAllActiveAsync`, and calling this endpoint once per
tenant with a fresh `ITenantContext` scope — the same per-tenant-scope
pattern `PendingAssignmentsWorker` already uses for its sweep. Delivery: the
CSV attached via the existing `EmailNotificationAction` stub to the
tenant's ops distribution list (or uploaded to object storage with a link
emailed, if attachments turn out to be too large). Failure handling: retry
with backoff per tenant so one tenant's failure doesn't block the rest, and
after N retries, page platform ops via the `OpsWebhookAction` channel rather
than silently dropping a tenant's report. The job is naturally idempotent
(pure read, no side effects), so a retry or an accidental re-run is safe —
worth just re-sending the email rather than building dedup logic for it.

## Trade-offs, known limitations, what I'd do next

- **Not compiled/run.** No .NET SDK and no running Docker daemon in this
  environment — see the note at the top. Run `docker-compose up -d mongodb`
  then `dotnet test` before trusting this. I reviewed every changed
  constructor call, DI registration, and using-directive by hand, and
  caught one real design mistake in the process this way — see
  `AI-USAGE.md` — but a compiler is a compiler.
- **The double ops-alert on the 3rd failed attempt** (Part B) is a minor,
  known duplication — see above.
- **Weekly report performance:** `GetWeeklyDriverPerformanceAsync` loads
  every task for the tenant and walks its full history in memory. Fine for
  the data volumes here; a tenant with a very large task backlog would want
  this pushed into the query (e.g. an aggregation pipeline, or storing
  attempt/delivery events as their own tenant-scoped collection instead of
  embedded history) — exactly the kind of thing PF-902 was a cautionary
  tale about, so I'd rather flag it now than let it become the next one.
- **`QueryAllTenantsAsync` still exists.** Kept per ADR-0003 (immutable),
  now guarded by `ArchitectureRulesTests`. If the migration tooling it was
  reserved for is confirmed dead, a follow-up ADR removing it outright
  would be the cleaner long-term fix.
- **With more time:** an integration test that boots the actual
  `ParcelFlow.Api` host (`WebApplicationFactory`) and hits the new
  endpoints over HTTP, to catch DI wiring mistakes that unit tests against
  `TestWorld` can't see; and turning `ArchitectureRulesTests`'s pattern
  into a small reusable Roslyn-based check if more invariants like it show
  up, rather than hand-rolled regex per rule.
