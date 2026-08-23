import { api } from "./httpClient";

// ForgeService.MatchAsync(...) -> GET api/app/match -> PagedResultDto<MatchDto>
export function listMatches({ sorting, skipCount, maxResultCount } = {}, signal) {
  return api.get("/api/app/match", { sorting, skipCount, maxResultCount }, signal);
}

// ForgeService.AcceptAsync(id) -> POST api/app/match/{id}/accept -> MatchDto
export function acceptMatch(id) {
  return api.post(`/api/app/match/${id}/accept`);
}

// ForgeService.RejectAsync(id) -> POST api/app/match/{id}/reject -> MatchDto
export function rejectMatch(id) {
  return api.post(`/api/app/match/${id}/reject`);
}

// Phase 4 Part 3: IMatchAppService.ClaimAsync(ClaimMatchDto) -> POST
// api/app/match/claim -> ClaimResultDto. The "this is my item"/"not my
// item" action from a report's detail page — searchResultReportId is the
// result being claimed, ownReportId is whichever of the caller's own
// reports it relates to, observedScorePercentage is the exact score
// already shown for that result (never recomputed client- or
// server-side). Phase 4 Part 6 (Task B): ownReportId may be omitted
// (sent as null) when the caller has no eligible report of their own —
// the backend then grants contact access via a narrower per-report claim
// instead of a full two-sided Match. Phase 4 Part 8 (Task B): for "not my
// item" (isMine: false), ownReportId is always ignored server-side now —
// a dismissal never depends on any report the caller owns, so this app
// no longer even prompts for one (see Match.jsx's simple confirm/cancel
// UI for that action).
export function claimMatch({ searchResultReportId, ownReportId = null, observedScorePercentage, isMine }) {
  return api.post("/api/app/match/claim", {
    searchResultReportId,
    ownReportId,
    observedScorePercentage,
    isMine,
  });
}

// Phase 4 Part 8 (Task B): IMatchAppService.GetMyDismissedReportIdsAsync()
// -> GET api/app/match/my-dismissed-report-ids -> Guid[]. Report ids the
// current user has recorded a "not my item" disposition toward — used by
// SmartSearch.jsx's search-time exclusion filter so a dismissed result
// never resurfaces, now regardless of whether the dismissing user owns
// any report at all (the older Match-based exclusion, still also
// checked, could only ever key off the user's own reports).
export function getMyDismissedReportIds(signal) {
  return api.get("/api/app/match/my-dismissed-report-ids", undefined, signal);
}
