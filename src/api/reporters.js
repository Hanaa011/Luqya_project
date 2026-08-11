import { api } from "./httpClient";

export function getReporter(id, signal) {
  return api.get(`/api/app/reporter/${id}`, undefined, signal);
}
