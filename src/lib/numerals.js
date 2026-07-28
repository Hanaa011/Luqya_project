const EASTERN_ARABIC_DIGITS = ["٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩"];

// The brand guide requires Eastern Arabic-Indic numerals (١٢٣) in the
// Arabic interface specifically — Urdu and English keep Western digits.
export function localizeDigits(value, locale) {
  const str = String(value);
  if (locale !== "ar") return str;
  return str.replace(/[0-9]/g, (d) => EASTERN_ARABIC_DIGITS[Number(d)]);
}
