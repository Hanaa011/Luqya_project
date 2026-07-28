/**
 * Verified directly against the backend source (LostFound.Domain.Shared):
 * Reports/ReportType.cs, Reports/ReportStatus.cs, Matches/MatchStatus.cs,
 * Reporters/PreferredContactType.cs. Both the numeric values and the
 * labels below are confirmed, not inferred — this replaces an earlier
 * version of this file that had guessed labels ("Resolved", "Both").
 */

export const ReportType = {
  LOST: 0,
  FOUND: 1,
};

export const ReportStatus = {
  OPEN: 0,
  MATCHED: 1,
  CLOSED: 2,
};

export const MatchStatus = {
  PENDING: 0,
  ACCEPTED: 1,
  REJECTED: 2,
};

export const PreferredContactType = {
  PHONE: 0,
  EMAIL: 1,
  WHATSAPP: 2,
};

// Return an i18n dictionary key (not raw text) so callers localize via
// t(reportStatusLabelKey(status)) instead of ever showing an untranslated
// enum name in the UI.
export function reportStatusLabelKey(value) {
  return { [ReportStatus.OPEN]: "statusOpen", [ReportStatus.MATCHED]: "statusMatched", [ReportStatus.CLOSED]: "statusClosed" }[value] ?? "statusUnknown";
}

export function matchStatusLabelKey(value) {
  return { [MatchStatus.PENDING]: "matchPending", [MatchStatus.ACCEPTED]: "matchAccepted", [MatchStatus.REJECTED]: "matchRejected" }[value] ?? "statusUnknown";
}

export function preferredContactLabelKey(value) {
  return { [PreferredContactType.PHONE]: "contactPhone", [PreferredContactType.EMAIL]: "contactEmail", [PreferredContactType.WHATSAPP]: "contactWhatsapp" }[value] ?? "statusUnknown";
}
