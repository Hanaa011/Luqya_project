from typing import Any

NULLISH_TEXT = {
    "none", "null", "unknown", "not specified", "not visible",
    "unclear", "unidentified", "غير معروف", "غير محدد",
}


def clean_text(value: Any) -> str | None:
    """Collapse whitespace and drop null-ish placeholder values."""
    if value is None:
        return None

    text = " ".join(str(value).strip().split())

    if not text or text.lower() in NULLISH_TEXT:
        return None

    return text


def clean_lower(value: Any) -> str | None:
    """Like clean_text, lowercased and dash-normalized for type/color fields."""
    text = clean_text(value)
    return text.lower().replace("-", " ") if text else None


def clean_id(value: Any) -> str | None:
    """Like clean_text, lowercased for case-insensitive ID comparisons."""
    text = clean_text(value)
    return text.lower() if text else None
