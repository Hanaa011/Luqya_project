// src/lib/saudiPhone.js
// Saudi mobile helper.
// Accepts Western, Arabic-Indic, and Persian/Urdu digits.

const SAUDI_LOCAL_PATTERN = /^05\d{8}$/;

export function toWesternDigits(value) {
  return String(value ?? "")
    // Arabic-Indic: ٠١٢٣٤٥٦٧٨٩
    .replace(/[٠-٩]/g, (digit) =>
      String(digit.charCodeAt(0) - "٠".charCodeAt(0))
    )
    // Persian / Urdu: ۰۱۲۳۴۵۶۷۸۹
    .replace(/[۰-۹]/g, (digit) =>
      String(digit.charCodeAt(0) - "۰".charCodeAt(0))
    );
}

// Use this in input onChange.
// It immediately converts Arabic digits to 0-9 and removes characters that
// cannot belong to a Saudi phone number.
export function normalizeSaudiPhoneInput(value) {
  const western = toWesternDigits(value);

  return western
    .replace(/[^\d+\s()-]/g, "")
    .replace(/(?!^)\+/g, "");
}

function compact(value) {
  return toWesternDigits(value)
    .trim()
    .replace(/[\s().-]/g, "");
}

function toLocalSaudiMobile(value) {
  let phone = compact(value);

  // 009665XXXXXXXX -> 05XXXXXXXX
  if (/^009665\d{8}$/.test(phone)) {
    phone = `0${phone.slice(5)}`;
  }
  // +9665XXXXXXXX -> 05XXXXXXXX
  else if (/^\+9665\d{8}$/.test(phone)) {
    phone = `0${phone.slice(4)}`;
  }
  // 9665XXXXXXXX -> 05XXXXXXXX
  else if (/^9665\d{8}$/.test(phone)) {
    phone = `0${phone.slice(3)}`;
  }
  // 5XXXXXXXX -> 05XXXXXXXX
  else if (/^5\d{8}$/.test(phone)) {
    phone = `0${phone}`;
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
