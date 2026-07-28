// Accepts 05XXXXXXXX, 5XXXXXXXX, +9665XXXXXXXX, 9665XXXXXXXX (spaces/
// dashes allowed while typing) and normalizes to the +9665XXXXXXXX form
// the backend should receive.
const SAUDI_MOBILE_PATTERN = /^(?:\+?966|0)?5\d{8}$/;

export function isValidSaudiMobile(raw) {
  const cleaned = (raw || "").replace(/[\s-]/g, "");
  return SAUDI_MOBILE_PATTERN.test(cleaned);
}

export function normalizeSaudiMobile(raw) {
  const cleaned = (raw || "").replace(/[\s-]/g, "");
  if (!SAUDI_MOBILE_PATTERN.test(cleaned)) return cleaned;

  const digits = cleaned.replace(/^\+/, "").replace(/^966/, "").replace(/^0/, "");
  return `+966${digits}`;
}
