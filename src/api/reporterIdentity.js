const STORAGE_KEY = "luqya-known-reporter-id";

/**
 * There is no "get my reporter" endpoint on the backend (confirmed absent
 * in ReporterAppService — see audit). The only reporterId the frontend
 * ever legitimately learns is the one returned on ReportDto.reporterId
 * after *this* browser successfully creates a report. This module just
 * remembers that real, verified value — it never fabricates one.
 */
export function getKnownReporterId() {
  return window.localStorage.getItem(STORAGE_KEY) || null;
}

export function setKnownReporterId(reporterId) {
  if (reporterId) {
    window.localStorage.setItem(STORAGE_KEY, reporterId);
  }
}

export function clearKnownReporterId() {
  window.localStorage.removeItem(STORAGE_KEY);
}
