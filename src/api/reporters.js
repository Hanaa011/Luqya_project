import { api } from "./httpClient";

// ReporterAppService.GetAsync(id) -> GET api/app/reporter/{id} -> ReporterDto
// Used only by the authenticated contact screen so report details never
// render contact information inline.
export function getReporter(id, signal) {
  return api.get(`/api/app/reporter/${id}`, undefined, signal);
}

// ReporterAppService.ConfirmClaimAsync(ConfirmReporterClaimDto) ->
// POST api/app/reporter/confirm-claim -> ConfirmReporterClaimResultDto.
// Redeems the one-time link emailed to a guest report's original reporter
// (triggered by ConversationAppService.OpenAsync) once they're logged in.
export function confirmReporterClaim(token) {
  return api.post("/api/app/reporter/confirm-claim", { token });
}
