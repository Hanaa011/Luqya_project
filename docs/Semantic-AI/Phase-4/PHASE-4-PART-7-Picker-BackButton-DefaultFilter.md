# Phase 4 — Part 7: Picker "None of These" Option, Contact Back-Button State Fix, Default Search Filter

## Context — Read First

Read, in order:

C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Phase-4-Part-3-Claim-Action-Implementation-Report.md
C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Phase-4-Part-5-Title-Field-Removal-and-Claim-Relocation-Report.md
C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Phase-4-Part-6-Login-Redirect-Contact-Access-Title-Fix-Report.md

This is a **frontend-only** task. All changes happen inside
`Luqya_project\Luqya_project\src`. Do not modify any backend file inside
`modules\lostfound\src` — Task A specifically must be achievable using the existing
`ReportClaim`/no-own-report contact path Phase 4 Part 6 already built
(`ClaimAsync` with `OwnReportId: null`) — do not add new backend capability for this
task; if you find during investigation that this genuinely isn't sufficient, stop and
report that rather than adding backend code.

Follow `CLAUDE.md`'s Modification Scope. Use the real running frontend, driven
through a real browser (Playwright/Chromium — if the MCP server isn't available, use
a locally-installed Playwright package driving a real Chromium instance, per Phase 4
Part 6's precedent; disclose whichever approach was used). Before starting, check for
and stop any stale backend/frontend process left running from a prior session.

---

# TASK A — "None of These" Option in the Report Picker

## Problem

When a user clicks "This is my item" and owns one or more eligible reports of the
opposite type, `Match.jsx`'s picker (per Phase 4 Part 3/5) forces them to select one
of those existing reports — even when none of them actually relates to the item they're
currently trying to claim. There is currently no way to say "none of my existing
reports match this — I haven't reported this specific item yet."

## Design (already decided — implement this, do not re-litigate)

1. The picker must **always** include an additional option, distinct from the user's
   real reports — something like *"لا شيء من هذه — لم أرفع بلاغًا لهذا الغرض بعد"* /
   *"None of these — I haven't reported this item yet"* (exact wording your
   judgment, localized ar/en/ur matching the app's existing i18n conventions).
2. This option must appear **every time** the picker is shown — including when the
   user has exactly one eligible report. Per this task's design, the existing
   "exactly one eligible report → auto-select, skip the picker entirely" shortcut
   (Phase 4 Part 5) must be **removed** — the picker (now always including "none of
   these" alongside any real eligible reports) should always be shown when at least
   one eligible report exists, so the user always has the chance to say none of them
   apply. (The picker was already correctly skipped only when *zero* eligible
   reports exist at all, per Phase 4 Part 6's "this is my item" fix — that case is
   unaffected; there's no list to show a picker for at all in that case.)
3. Selecting "none of these" must behave **exactly like the existing "zero eligible
   reports" path already does** (Phase 4 Part 6): call `claimMatch` with
   `ownReportId: null`, grant immediate contact access via the existing `ReportClaim`
   mechanism, show the same honest "this won't appear as a linked match in either
   Dashboard, but you can contact them now" note Part 6 already built, and navigate
   to Contact — reuse this exact existing code path, do not build a second, separate
   implementation of it.
4. Optionally (matching Part 6's own precedent — never required, never blocking):
   after granting contact access via "none of these," you may surface a link to
   create a new report for this specific item, for a user who wants the full
   both-parties/notification experience later — reuse whatever similar affordance
   Part 6 already established for the zero-eligible-reports case, if any, rather
   than inventing new wording for this case specifically.

## Live verification

1. As a user with **zero** eligible reports: confirm behavior is unchanged from
   Phase 4 Part 6 (no picker shown at all, immediate contact access) — this task
   must not affect that case.
2. As a user with **exactly one** eligible report: confirm the picker **now
   appears** (previously it auto-skipped) showing that one real report **and** the
   new "none of these" option.
3. As a user with **multiple** eligible reports: confirm the picker shows all real
   reports plus "none of these," as before Part 5/6 but now with the new option
   added.
4. Select "none of these" in a real test and confirm: `claimMatch` is called with
   `ownReportId: null`, the same honest no-Match note renders, and Contact access is
   granted immediately — identical behavior to the zero-eligible-reports case.
5. Confirm selecting a **real** report from the picker still works exactly as
   before (full `Match` creation/reuse, both-parties visibility) — this must not
   regress.

---

# TASK B — Fix Contact Page's Internal Back Button to Preserve Navigation State

## Problem

The browser's own native Back button already works correctly (confirmed by the
user): returning from `/match/:id/contact` to `/match/:id` this way still shows "This
is my item" correctly and it can be clicked again. The problem is specifically the
**in-app "back" control built inside `Contact.jsx`** — clicking it to return to the
detail page causes the claim action to disappear (the carried-forward score from
`location.state`, per Phase 4 Part 5/6, is lost), so re-entering Contact isn't
possible without repeating the whole flow.

## Investigate and fix

1. Find the exact current implementation of Contact.jsx's in-app back
   control — confirm it very likely constructs a fresh navigation (e.g.
   `navigate('/match/' + reportId)` or a plain `<Link to={...}>`) rather than
   using the browser history's own back mechanism, which is why it doesn't carry
   `location.state` the way the native Back button does.
2. Fix it to behave like a real "go back," preserving whatever state the page was
   reached with — the standard, idiomatic way to do this with React Router is
   `navigate(-1)` (pops the actual history entry, carrying its original state
   along, exactly like the native browser Back button) rather than building a fresh
   destination path. Use this approach unless you find a concrete reason it doesn't
   fit this specific page (e.g., if Contact can sometimes be reached with no
   meaningful "previous page" in history at all — investigate whether that's a real
   case here, and if so, handle it with a sensible fallback destination rather than
   breaking `navigate(-1)`'s behavior for the common case).
3. Confirm this fix doesn't affect any other page's back-navigation button if
   similar controls exist elsewhere in the app — check whether this exact pattern
   (a custom in-app "back" link/button reconstructing a path instead of using
   history) exists on other pages too, and note this in your report even if you
   don't fix others outside Contact.jsx's own scope, unless fixing them is trivial
   and low-risk to include.

## Live verification

1. Real browser: search → detail page → "This is my item" → land on Contact
   (score-carrying flow intact, per Part 6).
2. Click Contact.jsx's own in-app back control (not the browser's native Back
   button) → confirm you land back on the detail page **with the claim action
   still available** (or, if already claimed, whatever the correct post-claim state
   should show — confirm this is sensible too).
3. Re-enter Contact from there (either by clicking the claim action again if it's
   still offered, or by any other correct path this state should now offer) and
   confirm it works without needing to restart the whole search-to-claim flow.

---

# TASK C — Default Search Filter Should Be "All," Not "Found"

## Fix

In `SmartSearch.jsx` (or wherever the type-filter default is set), change the
default selected filter from "Found" (عُثر عليه) to "All" (الكل) — a first-time
visitor to `/search` should see results across both Lost and Found by default,
without needing to manually change the filter.

## Verify live

1. Open `/search` fresh (no prior filter selection in this session) and confirm the
   "All" option is the one visually selected/active by default.
2. Run a real search with no filter change and confirm results include both Lost
   and Found type reports where both genuinely exist for the query (not just one
   type).
3. Confirm manually selecting "Found" or "Lost" still works exactly as before —
   this is only a default-value change, not a removal of the other filter options.

---

# TASK D — Full Regression Check

1. `npm run build` and `npx eslint` on every changed frontend file — both clean.
2. `dotnet build Forge.slnx` — must be 0 errors/0 warnings (should be unaffected, but
   confirm, since this is a frontend-only task).
3. Re-verify Phase 4 Parts 3/5/6's claim mechanics (has-own-report path, and the
   zero/none-eligible no-own-report path) are all still intact after Task A's
   picker changes.
4. Re-verify Phase 4 Part 2's Contact security model is unaffected (this task
   doesn't touch backend authorization, but confirm the frontend still correctly
   handles a denied case if you can trigger one).

---

# Deliverable

Produce a report, saved to:

C:\Users\Windows 11\Desktop\GitHubProjectHnaa_Backend\Luqya_project\SemanticReports\Phase-4-Part-7-Picker-None-Option-Back-Button-Default-Filter-Report.md

Covering:

1. Task A: the exact picker change, and live verification for zero/one/multiple
   eligible-report cases plus the "none of these" and "real report" selection paths.
2. Task B: the exact root cause and fix for the in-app back button, with live
   before/after evidence, and whether the same pattern exists elsewhere (fixed or
   just noted).
3. Task C: confirmation of the new default, live.
4. Full Task D regression results.
5. Any deviations from this prompt, with justification.
6. Anything discovered but deliberately not fixed, with justification.

Do not stop until this report is written and saved.