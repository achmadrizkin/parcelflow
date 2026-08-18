# AI-USAGE.md

**Tool:** Claude Code (Claude, Anthropic), used for the entire submission —
reading the codebase, planning, writing code and tests, and writing this
documentation. No other AI tool was used.

## How it was used

1. **Exploration.** Read every file under `src/`, `tests/`, `docs/`, and
   `seed/` directly (not summarized) before writing a plan: all entities,
   services, controllers, the event pipeline, the state machine, the
   `.csproj` project-reference graph, and existing tests, to understand
   conventions before adding to them.
2. **Planning.** Wrote an explicit implementation plan (root cause for Part
   A, state-machine/event-pipeline design for Part B, report design for
   Part C) before touching any file, specifically to work out the
   project-reference-cycle constraint below *before* writing code that
   would hit it.
3. **Implementation.** Wrote the fix, the feature, the report, and all
   tests directly. Commits are staged to mirror the parts of the
   assignment (see `git log`).
4. **Docs.** ADR-0005, `ARCHITECTURE.md`/`DOMAIN_GLOSSARY.md` updates,
   `SOLUTION.md`, and this file.

## A concrete example where it was wrong

While implementing Part B, I added `ReturnScheduledEvent` to
`TaskEvents.cs` for symmetry with `TaskReturnCompletedEvent` (which
`DeliveryTaskService.CompleteReturnAsync` does dispatch) — the idea being
that a rule which itself *reacts to* an event should also *raise* one when
it schedules a return, matching how `DeliveryTaskService` emits an event
for every meaningful transition.

To do that I gave `ScheduleReturnOnThirdFailedAttemptRule` a constructor
dependency on `IEventDispatcher` so it could call `DispatchAsync` on the
new event. That's a real bug: `EventDispatcher`'s constructor takes
`IEnumerable<IEventRule> rules`, so the DI container must construct every
registered `IEventRule` before it can construct `EventDispatcher` — but
this rule now needed `IEventDispatcher` to construct *itself*. That's a
circular dependency, and ASP.NET Core's built-in container throws at
startup for exactly this shape (`A circular dependency was detected...`).
It would not have shown up in a quick read of the diff; it would have
surfaced as a hard runtime failure the first time the API host started.

I caught it during my own review pass before running anything (by tracing
through what `EventDispatcher`'s DI resolution would actually require),
not by compiling — this environment has no local .NET SDK and no running
Docker daemon, so I never got a compiler or the DI container itself to
confirm it. The fix: removed the `IEventDispatcher` dependency and the
`ReturnScheduledEvent` class entirely. The rule still performs the state
transition and sends the SMS/ops-webhook notifications the spec actually
asks for; it just doesn't raise a further domain event for something no
other rule currently consumes. I documented the reasoning directly in the
rule's doc comment so the next person doesn't reintroduce the same shape.

The broader lesson I applied afterward: I stopped assuming "match the
existing pattern for consistency" is automatically the safe move, and
started checking whether the *specific* symmetry I was reaching for (an
event rule that both consumes and produces events) actually existed
anywhere already in this codebase before adding it — it didn't, which in
hindsight was itself a signal.

## Where no AI tool was used

Not applicable — this submission was produced with Claude Code throughout,
working autonomously from the task description in `TAKEHOME.md`. Every
design decision above (the tenant-scoping fix, the state machine shape, the
report windowing) is an explicit, defensible choice, not an unreviewed
suggestion; the DI mistake above is included precisely because it's the one
place that reasoning was wrong on the first pass and had to be corrected
before it shipped.

Given that, and given the assignment's own note that "the technical
interview builds on this submission," the honest caveat is: this code has
been reasoned through and reviewed line-by-line but has **not been
compiled** (no local .NET SDK or running Docker daemon in the environment
it was written in — see `SOLUTION.md`). Before relying on it, run
`docker-compose up -d mongodb` and `dotnet test`, and read through the diff
yourself so you can stand behind every part of it, per the assignment's own
instructions.
