# Take-Home Assignment — Backend Engineer (Mid Level)

Welcome! This project is a slice of **ParcelFlow**, a fictional
multi-tenant last-mile delivery platform. It is deliberately built the way
our real platform is built: multi-tenant on a shared store, a strict state
machine for the delivery lifecycle, an event → rule → action pipeline, and
background workers. Treat it as a production codebase you just joined.

## Ground rules

- **Time window:** you have **3 days (72 hours)** from the moment you clicked
  "Start assessment" on your personal link (the deadline was shown on that page).
  We do NOT expect 3 days of work — a focused effort of roughly **6–8 hours** is
  the intended scope, and Part C is explicitly timeboxed. The calendar time is
  there so you can fit this around your life, not so you fill it.
- **Earlier is better.** Finishing comfortably inside the window counts in your
  favour. Quality comes first, though — a solid submission on day 3 beats a
  rushed one on day 1. And if something genuinely gets in the way, tell us before
  the deadline rather than going quiet: we would still rather read late, solid
  work than none, and how you handle it says as much as the code does.
- **Work the way you normally work.** AI tools are welcome — free tiers
  (Claude, ChatGPT, Gemini, Copilot, Gemini CLI) are more than enough, and
  no paid subscription is expected. We only ask that you document how you
  used them (see Deliverables). What we assess is your engineering; tool
  choice is never scored.
- **Commit as you go, and take notes as you go.** Use git locally, and jot down
  decisions while they are fresh — you will need them for your `SOLUTION.md`
  write-up (see Deliverables), and reconstructing them at the end is harder.
  We care about your thinking, not polished prose.
- **You may change anything** — code, tests, docs. If you find the docs and
  the code disagree, that is not an accident of the exercise; handle it the
  way a good engineer handles it in a real codebase.
- If something is ambiguous, make a sensible assumption and write it down.
  Part of the exercise is seeing how you handle underspecified requirements.

## Before you start

Get the system running (see [README.md](README.md)), skim
`docs/ARCHITECTURE.md`, and orient yourself. Budget real time for this —
understanding an unfamiliar codebase quickly is one of the skills we are
assessing.

---

## Part A — Bug: "We saw another carrier's parcels" (required)

The following ticket arrived from customer support this morning. It is real
and reproducible in this repo:

> **PF-1287 — CRITICAL — Cross-carrier data in daily summary report**
>
> Reported by: Nusantara Express (tenant `nusantara-express`)
>
> "We pulled our daily summary for 1 July 2026 and the report contains
> parcel references we don't recognise, delivery rows for cities we don't
> even operate in (Manila? Cebu?), and driver names that are not our
> people. Please explain what is going on — if our data is visible to other
> carriers the same way, this is a contract-level problem for us."

Your job:

1. **Reproduce** the issue and find the root cause.
2. **Fix it properly.** Think beyond the single symptom: is the fix you
   made enough to prevent this *class* of bug from reoccurring in this
   codebase? Whatever your view, act on it and explain your reasoning.
3. **Prove it.** Add test(s) that would have caught this bug before it
   shipped and that guard your fix.
4. In your video, walk through: how you found it, what the root cause was,
   what you changed, and what you would say back to the customer and to the
   team.

## Part B — Feature: Return to Sender (required)

Product has signed off on the following behaviour:

> When a parcel fails delivery for the **3rd time**, we stop retrying:
> the task is automatically scheduled for return, the recipient is notified
> by SMS that delivery is being returned to the sender, and the tenant's
> ops channel gets an alert. A driver later completes the return at the
> hub, which closes the task.

Requirements:

1. Extend the delivery lifecycle with the states needed for returns
   (schedule → completed at hub). Keep the state machine the single source
   of truth; illegal transitions must remain impossible.
2. The "3rd failed attempt ⇒ schedule return" behaviour must happen
   automatically, using the existing event pipeline (look at how the current
   rules work).
3. Expose an API endpoint for the hub to mark a return as completed.
4. Notifications: recipient SMS + ops-webhook alert on scheduling, via the
   existing action stubs.
5. Tests for the new states, the automatic trigger, and the endpoint.
   Multi-tenant correctness applies here as everywhere.
6. Update any docs your change makes stale.

Where requirements are silent (naming, exact payloads, whether a scheduled
return can be cancelled, ...), decide and document your choice.

## Part C — Ops automation (timeboxed: max ~1 hour)

Every Monday, each tenant's ops team asks us for a **weekly driver
performance summary**: per driver — tasks delivered, failed attempts, and
average hours from assignment to delivery, for the previous 7 days.
Someone currently builds this by hand. Automate it.

- Any form works: an endpoint, a console command, a script against the API —
  your call. Output should be something an ops person can use (CSV is fine).
- Do not gold-plate it. We want to see how you turn a repetitive manual
  request into a tool under a timebox.
- In your `SOLUTION.md`, briefly explain how you would run this automatically
  every Monday in production (scheduling, delivery to the tenant, failure
  handling) — describing it is enough, do not build it.

---

## Deliverables

1. **A single `.zip` of the project folder** with all your changes (code,
   tests, docs). Please delete build output (`bin/`, `obj/`) before zipping
   to keep it small. Name it `parcelflow-<your-name>.zip`.
   - Include an **`AI-USAGE.md`**: which tools you used, for what, at least
     one concrete example where the AI was wrong or suboptimal and what you
     did about it. "I didn't use AI" is a valid entry but tell us why.
   - Include a **`SOLUTION.md`** (1–2 pages) — the written counterpart to your
     video. It covers the same ground, so that we can understand your work by
     reading before we watch. Suggested structure:
     - **Overview** — what you did, in a few sentences.
     - **Part A** — how you found the bug, the root cause, what you changed
       and why, and how you proved it.
     - **Part B** — your design, and the calls you made where the spec was
       silent.
     - **Part C** — what you built, plus the "how I'd run it every Monday"
       explanation.
     - **Trade-offs, known limitations, and what you'd do next** with more time.

     Plain prose is fine — this is not a writing contest. Bullets beat essays.
   - **Use git locally while you work.** Run `git init` in the project and
     commit in meaningful steps with clear messages — we read the history to
     understand how you approached the problem. Include the `.git` folder in
     your zip. There is no remote and nothing to push; everything stays in
     the zip.
2. **A video walkthrough, 5–10 minutes, with your camera on** (screen
   recording — Loom, OBS, Meet recording, anything):
   - Demo the bug fix and the new feature actually working (terminal/Swagger
     is fine).
   - Talk us through your key decisions in English, as if presenting to our
     engineering team.
   - State which AI tools you used and how they helped — or say clearly that
     you used none.

   **Camera on is required.** The video is where we meet you and see you
   present your work, so a screen-only recording or a voice-over does not
   count. The video and `SOLUTION.md` overlap on purpose: the document is how
   we take in your reasoning, the video is where we see you present it. Don't
   script the video from the document word-for-word — just talk us through the
   work.

## How to submit

Videos are large, so **don't email us the files** — send us a link instead:

1. Upload **both** your `.zip` and your video to **your own Google Drive** (a
   single folder is easiest).
2. Set sharing to **"Anyone with the link"** — anything more restrictive means
   we cannot open it, and we may not be able to chase you before the deadline.
3. **Check the link works** — open it in a private/incognito window. A link we
   have to ask you to re-share costs you time you do not have.
4. **Reply to your invitation email** with the link, using the subject line
   **`Zyllem Backend — [Your Name]`**.

Keep the files in place until you hear back from us.

## How we evaluate (summary)

Correctness under multi-tenancy; quality of your root-cause analysis;
state-machine and event-pipeline integration; tests that prove the important
things; how you navigated and understood an unfamiliar codebase; pragmatism
under a timebox (Part C); transparency and judgment in AI usage; clarity of
the video. We do **not** score visual polish, exhaustive feature-completeness
beyond the spec, or framework trivia.

One thing worth knowing upfront: the technical interview builds on this
submission — you will walk through it, extend it live, and discuss your
decisions. However you produce your solution, make sure you understand it
deeply and can stand behind every part of it.

Good luck — we are looking forward to seeing how you think.
