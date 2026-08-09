import { listReporters } from "../api/reporters";

/**
 * Ownership chain (strict, no shortcuts):
 *
 *   IdentityUser.Id (userId)
 *     -> Reporter.IdentityUserId === userId   (STRICT equality only)
 *     -> Reporter.Id
 *     -> Report.ReporterId
 *
 * This module only ever answers "which Reporter row(s) actually declare
 * themselves owned by this userId" — it does not decide what to do about
 * duplicates or which one is canonical. That policy decision lives in
 * resolveOwnershipReporterId() below, which also folds in the
 * just-created-a-report signal from the current session.
 *
 * No caching here on purpose — every call asks the API fresh, walking
 * every page (GET /api/app/reporter has no IdentityUserId filter
 * server-side — verified: ReporterAppService.GetListAsync takes a plain
 * PagedAndSortedResultRequestDto, no filter fields at all), using the
 * real `totalCount` to know when every page has been checked.
 */
export async function findMyReporterCandidates(userId, { pageSize = 100, maxPages = 50, signal } = {}) {
  if (!userId) return [];

  const matches = [];
  let skipCount = 0;

  for (let page = 0; page < maxPages; page += 1) {
    let res;
    try {
      res = await listReporters({ sorting: "creationTime asc", skipCount, maxResultCount: pageSize }, signal);
    } catch {
      break;
    }

    const items = res?.items ?? [];
    for (const r of items) {
      if (r.identityUserId === userId) matches.push(r);
    }

    const totalCount = res?.totalCount ?? items.length;
    skipCount += items.length;
    if (items.length === 0 || skipCount >= totalCount) break;
  }

  return matches;
}

/**
 * Ownership resolver with the priority order this codebase now requires:
 *
 *  A) A ReporterId returned by a report THIS session already created for
 *     THIS exact userId (see lib/sessionReporter.js) — the strongest
 *     possible signal, since it comes straight from a just-completed
 *     authenticated write, not a guess over a possibly-messy list.
 *  B) Otherwise, resolve via the strict identityUserId match above.
 *     - 0 matches -> genuinely owns nothing yet: { reporterId: null }
 *     - 1 match   -> that Reporter.Id
 *     - 2+ matches -> ambiguous. Known backend data-quality issue this
 *       frontend cannot safely resolve on its own (see ISSUE 12 in the
 *       task this implements) — reports are NEVER merged/combined across
 *       duplicate Reporter rows. Surfaced as `ambiguous: true` so the UI
 *       can show an explicit inconsistency notice instead of silently
 *       picking one (or worse, showing the union of both).
 *
 * Never uses CreatorId, phone, or email at any point.
 */
export async function resolveOwnershipReporterId(userId, { sessionReporterId, signal } = {}) {
  if (!userId) return { reporterId: null, ambiguous: false, candidateCount: 0 };

  if (sessionReporterId) {
    return { reporterId: sessionReporterId, ambiguous: false, candidateCount: 1, source: "session" };
  }

  const candidates = await findMyReporterCandidates(userId, { signal });

  if (candidates.length === 0) {
    return { reporterId: null, ambiguous: false, candidateCount: 0, source: "list" };
  }

  if (candidates.length > 1) {
    return { reporterId: null, ambiguous: true, candidateCount: candidates.length, source: "list" };
  }

  return { reporterId: candidates[0].id, ambiguous: false, candidateCount: 1, source: "list" };
}
