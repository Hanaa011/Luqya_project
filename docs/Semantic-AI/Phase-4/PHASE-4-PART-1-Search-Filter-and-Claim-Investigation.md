# Phase 4 — Part 1: Search Confidence Filtering + Claim-Action Investigation

## Context — Read First

Read, in order:

C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Luqya-System-Reference.md
C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Image-Search-Implementation-Report.md
C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Phase-3-Part-2-Real-World-Validation-Report.md
C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Phase-3-Part-3-Image-Search-Thumbnails-and-Score-Quality-Report.md
C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Phase-3-Part-4-Full-Image-Only-Search-Fix-Report.md

This task has exactly two parts, with very different expectations:

- **Task A is a real implementation task** — build it, verify it live, ship it.
- **Task B is investigation-and-recommendation ONLY** — do NOT implement anything
  for Task B. Research the existing backend thoroughly, identify the realistic
  options, and write up your findings and a clear recommendation in the final
  report so the user can decide which direction to take in a future session. Do
  not write any new code for Task B under any circumstances in this session.

Follow `CLAUDE.md`'s Modification Scope: any code changes happen only inside
`modules\lostfound\src` (backend) or `Luqya_project\Luqya_project\src` (frontend).
Do not touch Angular, the Python `ai_service`, or the older nested `.NET` backend
snapshot.

Before starting, check for and stop any stale backend/IIS Express process left
running from a prior session (this has happened in every prior Phase 3 session) —
confirm via `netstat`/process list that no conflicting process holds the expected
port before starting your own.

---

# TASK A — Only Show Search Results Above 55% Match Confidence (Implement)

1. Confirm exactly how filtering currently works end to end: recall from the prior
   audit that `AiSearchAppService.SearchAsync` hardcodes `minimumScorePercentage` to
   `70` internally regardless of what the caller sends, and that the frontend has no
   UI control for this at all. Re-verify this is still the current behavior before
   changing anything (it may have changed since the audit, per Phase 3's work).
2. Change the threshold to **55**, applied server-side (do not rely on a frontend-
   only filter — the backend must never return a result below 55% at all). Prefer a
   named, documented constant/configuration value over a bare magic number if
   that's a small, low-risk change consistent with the surrounding code's style.
3. Ensure this applies uniformly to text search, image search, and combined
   search — all three query shapes from the recently-completed image-search work
   (Phase 3 Parts 1–4) must respect this same floor.
4. The comparison must be inclusive (`>= 55`, not `> 55`).
5. **Verify live, explicitly, through the real frontend**: run real searches
   (text, image-only, and combined) and directly confirm — by reading the actual
   rendered results in the browser and the raw API response — that no result below
   55% ever appears anywhere in the UI, and that legitimate results at or above 55%
   still appear correctly ranked. Capture concrete evidence (actual scores observed,
   and ideally a screenshot) for this confirmation.
6. Full regression check after this change:
   - `dotnet build Forge.slnx` — must be 0 errors/0 warnings.
   - `dotnet test` on `LostFound.Application.Tests` — confirm no new regressions
     versus the established 84/85 baseline.
   - `npm run build` and `npx eslint` on any changed frontend file — both clean.
   - Confirm the recently-fixed image-only search behavior (Phase 3 Part 4) and the
     thumbnail/score-quality fixes (Phase 3 Part 3) are still intact and unaffected.

---

# TASK B — Investigate a Post-Search Claim/Action Feature (Research Only — Do NOT Implement)

**Do not write any implementation code for this task.** Your entire deliverable for
Task B is a thorough, evidence-based investigation and a clear written
recommendation in the final report. This is intentional — the user wants to review
your findings before deciding what to build.

## What to investigate

The user wants to eventually let a logged-in user, after selecting a high-confidence
result from Smart Search, take a real follow-up action — potentially one or a
combination of:

- Directly revealing contact information for the other party.
- A "this is my item" / "not my item" confirmation choice.
- Creating a real match/report link between the selected result and the user's own
  report, so it then shows up for **both** parties in their existing
  Matches/Dashboard view.

Investigate the current backend thoroughly and answer, with real evidence (actual
method signatures, actual field names, actual live API responses — not assumption):

1. **`Match` entity/table**: its complete current field list, every status/state
   value it can actually hold today, and whether background auto-matching
   (`ReportMatchingBackgroundJob` → `MatchManager`) already creates `Match` rows for
   high-confidence pairs automatically (per prior reports, it does) — and if so,
   whether a `Match` row would likely already exist for a pair the user is about to
   "claim" from search, or would need to be created fresh.
2. **`MatchAppService`**: every existing method, with real signatures. Does anything
   already exist that could represent "confirm this match" or "create a match
   between these two reports" without needing new backend code?
3. **`NotificationAppService`**: every existing method, and exactly how/when
   notifications are currently triggered by `MatchManager`. Could an existing call
   already produce a real notification to the other party as a side effect, or would
   a new trigger be needed?
4. **The existing "Contact" screen/flow** (`/match/:id/contact`): what it currently
   requires to show contact information (does it need the match to have a specific
   status, or does it already work for any existing `Match` row once the user is
   authenticated?).
5. **Linking a search result to "the user's own report"**: does the current app
   already know which of a logged-in user's reports is relevant when they're
   viewing search results (e.g., are they searching *from* one of their own existing
   reports' context), or would this need to be figured out/asked for as part of any
   future implementation? Investigate how the frontend currently identifies "the
   current user's own reports" (per the prior audit, this is done via client-side
   filtering of a bounded page — confirm this is still accurate).
6. Based on all of the above, identify **every realistic implementation option**,
   even ones simpler than what the user described — including the possibility that
   the entire feature could be built using only endpoints/services that already
   exist today, with zero new backend code (e.g., if a `Match` row already exists or
   can be created via an existing method, and the existing Contact screen already
   works for it, "this is my item" might just mean "call an existing method if
   needed, then navigate the user to the already-working Contact screen").

## What to write in the report

For Task B, produce a clear, structured section covering:

1. **What already exists** (from your investigation above), with real evidence.
2. **Realistic implementation options**, ranked from "uses only what already exists,
   zero new backend code" to "requires new backend work," with a short explanation
   of what each option would actually let the user do, and how much new work each
   would need.
3. **Your recommendation**: which option you'd suggest and why, given what already
   exists and how well each option matches what the user described.
4. Explicitly flag anything that's ambiguous or needs a product decision (e.g., "the
   user hasn't specified whether declining an item should be remembered, or if a
   session-local dismissal is sufficient" — surface real open questions rather than
   guessing at answers on the user's behalf).

Do not build any of this. This section exists purely to inform a future decision.

---

# Deliverable

Produce a report, saved to:

C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Phase-4-Part-1-Search-Confidence-Filter-and-Claim-Action-Investigation-Report.md

Covering:

1. **Task A**: exact threshold-enforcement mechanism and where, with live
   verification evidence (real scores observed, before/after), and full regression
   check results.
2. **Task B**: the complete investigation write-up as specified above — findings,
   ranked options, and your recommendation. No implementation, no code changes for
   this section.
3. Any deviations from this prompt, with justification.

Do not stop until this report is written and saved.