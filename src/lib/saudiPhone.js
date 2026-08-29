// src/lib/saudiPhone.js

const SAUDI_LOCAL_PATTERN = /^05\d{8}$/;

export function toWesternDigits(value) {
  return String(value ?? "")
    .replace(/[٠-٩]/g, (digit) =>
      String(digit.charCodeAt(0) - "٠".charCodeAt(0))
    )
    .replace(/[۰-۹]/g, (digit) =>
      String(digit.charCodeAt(0) - "۰".charCodeAt(0))
    );
}

export function normalizeSaudiPhoneInput(value) {
  return toWesternDigits(value)
    .replace(/[^\d+\s().-]/g, "")
    .replace(/(?!^)\+/g, "");
}

function compact(value) {
  return toWesternDigits(value)
    .trim()
    .replace(/[\s().-]/g, "");
}

function toLocalSaudiMobile(value) {
  let phone = compact(value);

  // 009665XXXXXXXX
  if (/^009665\d{8}$/.test(phone)) {
    return `0${phone.slice(5)}`;
  }

  // +9665XXXXXXXX
  if (/^\+9665\d{8}$/.test(phone)) {
    return `0${phone.slice(4)}`;
  }

  // 9665XXXXXXXX
  if (/^9665\d{8}$/.test(phone)) {
    return `0${phone.slice(3)}`;
  }

  // 5XXXXXXXX
  if (/^5\d{8}$/.test(phone)) {
    return `0${phone}`;
  }

  return phone;
}

export function isValidSaudiMobile(value) {
  return SAUDI_LOCAL_PATTERN.test(toLocalSaudiMobile(value));
}

export function normalizeSaudiMobile(value) {
  const local = toLocalSaudiMobile(value);

  if (!SAUDI_LOCAL_PATTERN.test(local)) {
    return compact(value);
  }

  return `+966${local.slice(1)}`;
}