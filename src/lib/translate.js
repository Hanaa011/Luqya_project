import { FALLBACK_LOCALE } from "../i18n/locales";
import { resources } from "../i18n/resources";

/**
 * Exported standalone (not just through the useI18n hook) so that code
 * outside the React tree — the admin notification composer, an email or
 * SMS template, a server-side job — can render a string in *any*
 * supported language regardless of what language the current viewer's
 * app is set to. That separation (viewer language vs. delivery language)
 * is exactly what "admins can choose the language a notification is
 * sent in" requires.
 */
export function translate(localeCode, key, vars) {
  const table = resources[localeCode] ?? resources[FALLBACK_LOCALE];
  const fallbackTable = resources[FALLBACK_LOCALE];

  let str = table?.[key] ?? fallbackTable?.[key] ?? key;

  if (vars) {
    for (const [name, value] of Object.entries(vars)) {
      str = str.replaceAll(`{${name}}`, String(value));
    }
  }

  return str;
}

// Small helper for the handful of call sites that need an inline
// three-language literal (dynamic content not worth a resource key)
// instead of a dictionary lookup — same fallback chain as translate().
export function pick(localeCode, map) {
  return map[localeCode] ?? map[FALLBACK_LOCALE] ?? Object.values(map)[0];
}
