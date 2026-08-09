import { api } from "./httpClient";

// ReporterAppService.GetAsync(id) -> GET api/app/reporter/{id} -> ReporterDto
// Used only by the authenticated contact screen so report details never
// render contact information inline.
export function getReporter(id, signal) {
  return api.get(`/api/app/reporter/${id}`, undefined, signal);
}
