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
// only valid with isMine: true; the backend then grants contact access
// via a narrower per-report claim instead of a full two-sided Match.
export function claimMatch({ searchResultReportId, ownReportId = null, observedScorePercentage, isMine }) {
  return api.post("/api/app/match/claim", {
    searchResultReportId,
    ownReportId,
    observedScorePercentage,
    isMine,
  });
}
