# Phase 4 — Part 2: URGENT Security Fix — Unprotected Contact/Reporter Endpoint

## Context — Read First

Read:

C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Phase-4-Part-1-Search-Confidence-Filter-and-Claim-Action-Investigation-Report.md

Section 1.4 of that report found a real, live security gap: `ReporterAppService.GetAsync`
(backing `GET /api/app/reporter/{id}`, which the `/match/:id/contact` page calls to
reveal a reporter's phone/email) has **no `[Authorize]` attribute and no
ownership/relatedness check anywhere in the class, its interface, or module
registration.** Today, the only thing preventing an authenticated-but-unrelated user
from reading any reporter's contact information is a frontend UI convention (the
Contact link is only *shown* when a related `Match` exists) — not a backend-enforced
rule. Any authenticated user who obtains or guesses a report id can call this endpoint
directly and get real phone/email data for someone they have no relationship to.

**This is a production security defect and takes priority over any other feature
work.** Fix it now, before Phase 4 Part 3's claim-action feature is built on top of
this endpoint.

Follow `CLAUDE.md`'s Modification Scope: backend fixes only inside
`modules\lostfound\src`. Frontend changes only if genuinely required (e.g., handling a
new 403 response gracefully), inside `Luqya_project\Luqya_project\src`.

---

# TASK A — Investigate the Exact Current Exposure

1. Confirm live, with a real API call (an authenticated user calling
   `GET /api/app/reporter/{id}` for a report id they have no relationship to
   whatsoever — not a party to any `Match`, not the report's own owner), that this
   currently succeeds and returns real contact data. Capture this as concrete
   "before" evidence.
2. Confirm whether an **unauthenticated** call also succeeds (i.e., is there any
   auth requirement at all today, or none whatsoever) — test this too.
3. Check every other place `ReporterAppService` or reporter contact fields
   (phone/email) might be exposed elsewhere in the API surface (e.g., is contact
   data ever embedded directly in `ReportDto`/`MatchDto` responses, bypassing this
   endpoint entirely?) — the fix must cover every real exposure path, not just the
   one endpoint already identified.

---

# TASK B — Design and Implement the Correct Access Rule

Decide, and implement, the correct rule for who may see a reporter's contact
information. Based on how the system already works (per the Part 1 investigation),
the correct rule should be: **a caller may see a reporter's contact information only
if they are the authenticated owner of a report that is linked, via an existing
`Match` row, to that reporter's report** — i.e., the same relationship `Match.jsx`'s
frontend already uses to decide whether to *show* the Contact link, now enforced
for real at the backend.

1. Implement this as a genuine authorization check inside
   `ReporterAppService.GetAsync` (or via an ABP authorization policy/handler if that
   fits the codebase's existing conventions better — check how authorization is
   handled elsewhere in this module first, and follow the same pattern rather than
   inventing a new one).
2. The check must verify: the current user is authenticated, AND the current user
   owns at least one report that has an existing `Match` row (any status — do not
   over-restrict to only `Accepted` matches unless you have a strong reason,
   since the existing frontend already shows Contact for `Pending` matches too;
   confirm this against `Match.jsx`'s actual current logic before deciding) linking
   their report to the report whose reporter is being requested.
3. On failure, return a proper `403 Forbidden` (or ABP's equivalent
   `AbpAuthorizationException`), not a silent empty result or a generic error — the
   frontend's `Contact.jsx` was already found to defensively handle a `403` case, so
   this should integrate cleanly.
4. If Task A found other exposure paths (contact data embedded elsewhere), apply the
   same rule consistently there too — do not fix only the one endpoint if the data
   leaks through a second path.
5. Do not break legitimate access: a user who genuinely owns a report matched to the
   report in question must still be able to see contact info exactly as before —
   this is a tightening, not a lockdown that breaks the existing, working Contact
   feature for legitimate users.

---

# TASK C — Live Verification

1. Re-run Task A's original "unrelated user" test — confirm it now correctly fails
   with `403`/authorization error, not real data.
2. Re-run Task A's "unauthenticated" test — confirm it correctly fails.
3. Test the legitimate case: a real user who owns a report with a genuine `Match` to
   another report successfully retrieves that report's reporter contact info,
   exactly as before — no regression for real, legitimate use.
4. Test through the real frontend, real browser: open `/match/:id/contact` for a
   genuine match (should still work) and, if you can construct the scenario, for an
   unrelated report id via direct navigation (should now correctly fail/redirect/
   show an error rather than leak data).
5. Run the full regression suite: `dotnet build Forge.slnx` (0 errors/0 warnings),
   `dotnet test` on `LostFound.Application.Tests` (confirm no new regressions versus
   the established 84/85 baseline).

---

# Deliverable

Produce a report, saved to:

C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Phase-4-Part-2-Contact-Endpoint-Security-Fix-Report.md

Covering:

1. The exact exposure confirmed in Task A, with real before-evidence (redact any
   real phone/email values you capture as evidence — replace with a clear
   placeholder in the report; do not publish real personal data in a saved
   markdown file).
2. The exact fix implemented (Task B), with the authorization rule stated plainly.
3. Live verification evidence for both the blocked (unrelated/unauthenticated) and
   allowed (legitimate) cases (Task C), again with any real contact values redacted.
4. Full regression results.
5. Any other exposure paths found and fixed, or confirmed not to exist.
6. Any deviations from this prompt, with justification.

Do not stop until this report is written and saved, and the exposure is confirmed
closed with live evidence.