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
// api/app/match/claim -> MatchDto. The "this is my item"/"not my item"
// action from a Smart Search result — searchResultReportId is the result
// being claimed, ownReportId is whichever of the caller's own reports it
// relates to, observedScorePercentage is the exact score already shown
// for that result card (never recomputed client- or server-side).
export function claimMatch({ searchResultReportId, ownReportId, observedScorePercentage, isMine }) {
  return api.post("/api/app/match/claim", {
    searchResultReportId,
    ownReportId,
    observedScorePercentage,
    isMine,
  });
}
