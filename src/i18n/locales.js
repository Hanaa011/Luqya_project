/**
 * Locale registry — the single place that knows which languages Luqya
 * supports. Adding a new language later means adding one entry here plus
 * one resources/<code>.js file — nothing else in the app hardcodes a
 * language list, which is what makes this scale past three.
 */
export const LOCALES = [
  {
    code: "ar",
    dir: "rtl",
    nativeName: "العربية",
    englishName: "Arabic",
    flag: "🇸🇦",
    font: "font-arabic",
  },
  {
    code: "en",
    dir: "ltr",
    nativeName: "English",
    englishName: "English",
    flag: "🇺🇸",
    font: "font-body",
  },
  {
    code: "ur",
    dir: "rtl",
    nativeName: "اردو",
    englishName: "Urdu",
    flag: "🇵🇰",
    font: "font-urdu",
  },
];

export const DEFAULT_LOCALE = "ar";

// If a key is missing in the active locale, fall back to this one before
// falling back to the raw key itself — keeps a partially-translated new
// language usable from day one instead of blocking launch on 100% coverage.
export const FALLBACK_LOCALE = "en";

export function getLocaleMeta(code) {
  return (
    LOCALES.find((locale) => locale.code === code) ??
    LOCALES.find((locale) => locale.code === FALLBACK_LOCALE)
  );
}

export function isRtl(code) {
  return getLocaleMeta(code).dir === "rtl";
}
