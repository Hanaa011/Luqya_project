import { listReports } from "../api/reports";

/**
 * IMPORTANT — corrected after a live bug report (see conversation history):
 * `creatorId` is NOT a real server-side filter on GET /api/app/report.
 * Verified directly against the current backend source:
 *   - GetReportListDto.cs only declares Filter/Type/Status/LocationId/
 *     ReporterId/CategoryId — there is no CreatorId property at all.
 *   - ReportAppService.GetListAsync's `.WhereIf(...)` chain never
 *     references CreatorId.
 * So passing `creatorId` as a query param was always a silent no-op —
 * the server ignored it and returned the same *global, unfiltered* page
 * to every caller regardless of who was logged in. That's what caused
 * "Smart Search returns nothing when logged in" (this function's result
 * set was actually every report in the system, not just the user's own,
 * so Smart Search's own-report exclusion filtered out everything) and
 * "Account B sees Account A's reports" (Dashboard/Browse were reading
 * the same unfiltered global list for every account).
 *
 * `ReportDto` DOES include a real `creatorId` on every item (inherited
 * from ABP's `AuditedEntityDto<Guid>`, populated automatically at
 * creation time) — so real per-user filtering is only possible
 * client-side, by fetching a page and keeping the rows whose own
 * `creatorId` matches the current user.
 *
 * Known limitation (frontend cannot fix this without backend support):
 * this only sees whatever it fetches. Once total report volume exceeds
 * `maxResultCount`, this will silently miss a user's older reports. A
 * real `CreatorId` filter parameter on GetReportListDto is the correct
 * long-term fix and would need a backend change.
 */
export async function fetchMyReports({ userId, maxResultCount = 500, sorting = "creationTime desc", signal } = {}) {
  if (!userId) {
    return { reports: [], totalCount: 0, reliable: false, reason: "no-user-id" };
  }

  try {
    const result = await listReports({ sorting, maxResultCount }, signal);
    const mine = (result?.items ?? []).filter((r) => r.creatorId === userId);
    return {
      reports: mine,
      totalCount: mine.length,
      reliable: true,
      reason: null,
    };
  } catch {
    return { reports: [], totalCount: 0, reliable: false, reason: "fetch-failed" };
  }
}
