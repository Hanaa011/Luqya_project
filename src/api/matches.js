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
