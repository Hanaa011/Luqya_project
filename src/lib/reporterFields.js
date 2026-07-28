import { normalizeSaudiMobile } from "./saudiPhone";
import { PreferredContactType } from "../api/enums";

/**
 * ISSUE 1 FIX — root cause (confirmed against the real backend source,
 * ReportAppService.CreateAsync):
 *
 *   input.ReporterPhone ?? CurrentUser.PhoneNumber ?? string.Empty
 *
 * The frontend previously sent nothing at all for an authenticated
 * user (`...(profile ? {} : {...guestFields})`), on the assumption —
 * taken from CreateReportDto's own comment "ignored for authenticated
 * users" — that the backend didn't need it. That comment is stale: the
 * actual code falls back to `CurrentUser.PhoneNumber`, which is read
 * from the access token's claims, not a live database lookup — so if
 * the token was issued before the phone was set, or the phone claim
 * simply isn't included, it's null, and `Reporter.SetPhone` throws
 * "phone can not be null, empty or white space." exactly as observed.
 *
 * Fix: always send the phone explicitly from the already-fetched,
 * database-accurate ProfileDto when authenticated — this takes
 * priority in the backend's own `??` chain, so the token-claims gap
 * never matters. Returns null when there's genuinely no phone to send
 * (profile has none), so the caller can prompt instead of submitting
 * an empty string.
 */
export function buildReporterFields({ profile, guest }) {
  if (profile) {
    const phone = (profile.phoneNumber || "").trim();
    if (!phone) return null;

    const name = [profile.name, profile.surname].filter(Boolean).join(" ").trim() || profile.userName;

    return {
      reporterName: name || undefined,
      reporterPhone: normalizeSaudiMobile(phone),
      reporterEmail: profile.email || undefined,
      preferredContact: PreferredContactType.PHONE,
    };
  }

  return {
    reporterName: guest.reporterName,
    reporterPhone: normalizeSaudiMobile(guest.reporterPhone),
    reporterEmail: guest.reporterEmail || undefined,
    preferredContact: guest.preferredContact,
  };
}
