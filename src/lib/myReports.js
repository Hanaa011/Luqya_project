import { listReports } from "../api/reports";

/**
 * Ownership source of truth: Report.CreatorId, the ABP audit field
 * populated automatically at creation time by whichever IdentityUser was
 * authenticated. Confirmed fixed on the backend (ReportAppService.MapToDto
 * now actually copies CreatorId onto the DTO — it previously omitted it,
 * which is why this was temporarily switched to ReporterId).
 *
 * ReporterId is NOT used for ownership anymore — it remains purely a
 * contact/reporter-record concept (see lib/reporterFields.js / report
 * creation), not an account-isolation mechanism.
 *
 * Known limitation, unchanged from before ReporterId was ever introduced:
 * GetReportListDto has no CreatorId query parameter (verified against the
 * actual backend source — ReportAppService.GetListAsync's .WhereIf chain
 * never references CreatorId), so there is still no real server-side
 * filter for "my reports" by creator. This fetches a page and filters
 * client-side by the report's own creatorId field instead. That means a
 * user's reports beyond `maxResultCount` in the global feed won't appear
 * here without a real backend filter — a real, deliberate trade-off
 * accepted by reverting to CreatorId, not something new.
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
