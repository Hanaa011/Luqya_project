// Shared client-side image validation, used by ReportLost.jsx, ReportFound.jsx,
// and SmartSearch.jsx (Task B/C) — one rule set so a rejection never differs
// between screens. Mirrors LostFound.Application/Reports/ImageValidator.cs's
// magic-byte checks exactly, so a client-side rejection reason matches what
// the backend would say for the same file (see Task A2 / Luqya-System-Reference.md
// §20/§38 Issue #15 — no size/format validation existed anywhere before this).
//
// This is a content-inspection check (file size + magic bytes), not just the
// browser's `accept="image/*"` hint, which only looks at the OS file picker's
// own MIME guess and can be spoofed/wrong.

// Must match the backend default (LostFound.Application/Reports/ImageValidationOptions.cs,
// bound from "LostFound:ImageValidation:MaxSizeBytes") — there is no public
// endpoint that exposes the configured value, so this is kept in sync by hand.
export const MAX_IMAGE_SIZE_BYTES = 8 * 1024 * 1024; // 8 MB

const REASONS = {
  EMPTY: "empty",
  TOO_LARGE: "tooLarge",
  INVALID_FORMAT: "invalidFormat",
};

export { REASONS as ImageValidationReason };

async function readHeadBytes(file, length) {
  const buffer = await file.slice(0, length).arrayBuffer();
  return new Uint8Array(buffer);
}

function isJpeg(b) {
  return b.length >= 3 && b[0] === 0xff && b[1] === 0xd8 && b[2] === 0xff;
}

function isPng(b) {
  return (
    b.length >= 8 &&
    b[0] === 0x89 && b[1] === 0x50 && b[2] === 0x4e && b[3] === 0x47 &&
    b[4] === 0x0d && b[5] === 0x0a && b[6] === 0x1a && b[7] === 0x0a
  );
}

function isWebp(b) {
  return (
    b.length >= 12 &&
    b[0] === 0x52 && b[1] === 0x49 && b[2] === 0x46 && b[3] === 0x46 && // "RIFF"
    b[8] === 0x57 && b[9] === 0x45 && b[10] === 0x42 && b[11] === 0x50 // "WEBP"
  );
}

/**
 * Validates a File before it's ever converted to base64/uploaded.
 * Returns `null` when valid, or one of ImageValidationReason's string codes
 * when not — callers localize the reason themselves (see the tr({ar,en,ur})
 * pattern already used throughout this app for dynamic messages).
 */
export async function validateImageFile(file) {
  if (!file || file.size === 0) {
    return REASONS.EMPTY;
  }

  if (file.size > MAX_IMAGE_SIZE_BYTES) {
    return REASONS.TOO_LARGE;
  }

  const head = await readHeadBytes(file, 12);
  if (!(isJpeg(head) || isPng(head) || isWebp(head))) {
    return REASONS.INVALID_FORMAT;
  }

  return null;
}
