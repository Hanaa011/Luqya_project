// Same lightweight format check as the backend's [EmailAddress]
// DataAnnotation - client-side only guards presence + shape before
// submit, the backend remains the source of truth.
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function isValidEmail(raw) {
  return EMAIL_PATTERN.test((raw || "").trim());
}
