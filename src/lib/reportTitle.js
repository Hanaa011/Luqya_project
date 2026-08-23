// Phase 4 Part 6 (Task C): a short, meaningful heading extracted from a
// report's own free-typed description, instead of the generic
// AI-classified object type (e.g. "Smartphone"). Simple, local,
// presentation-layer text truncation only - no AI call, no backend
// logic, per the task's explicit constraint.
const MAX_LENGTH = 60;

// Sentence-ending punctuation, Arabic and Latin: . ! ? ، ؛ ؟ , ;
const SENTENCE_END_RE = /[.!?،؛؟,;\n]/;

export function extractShortTitle(description, maxLength = MAX_LENGTH) {
  const text = String(description ?? "").trim();
  if (!text) return null;

  if (text.length <= maxLength) {
    // Even a short description may still read better cut at its first
    // sentence-ending punctuation than shown whole.
    const punctIndex = text.search(SENTENCE_END_RE);
    return punctIndex > 0 ? text.slice(0, punctIndex).trim() : text;
  }

  const punctIndex = text.search(SENTENCE_END_RE);
  if (punctIndex > 0 && punctIndex <= maxLength) {
    return text.slice(0, punctIndex).trim();
  }

  // No usable punctuation within range - cut at the last whole word at or
  // before maxLength (never split a word in half), and mark the
  // truncation with an ellipsis since real content was cut off.
  const truncated = text.slice(0, maxLength);
  const lastSpace = truncated.lastIndexOf(" ");
  const safe = lastSpace > 0 ? truncated.slice(0, lastSpace) : truncated;
  return `${safe.trim()}…`;
}

// Old (pre-Phase-4-Part-5) reports may still contain the retired
// "title — description" format as one plain string - extractShortTitle
// has no special case for it, and none is needed: it just extracts the
// first short segment of whatever text is there, which for the old
// format naturally lands on (or very near) the original title itself.
export function reportHeadingTitle(report, fallback) {
  return extractShortTitle(report?.description) || report?.aiObjectType || fallback;
}
