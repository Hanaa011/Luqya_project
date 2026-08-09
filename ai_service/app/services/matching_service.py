import math
import re
import unicodedata
from typing import Any

from app.models.schemas import ItemData, MatchResult
from app.services.embedding_service import get_embeddings

MAX_MATCH_RESULTS = 20
RELEVANT_SCORE_FLOOR = 55.0

# Small set of common cross-language synonyms so everyday items normalize
# consistently. The matcher does NOT depend on this list: items outside it
# are still compared on normalized raw text and on semantic similarity.
TYPE_ALIASES = {
    "شنطة": "backpack", "شنطه": "backpack", "حقيبة": "backpack", "حقيبه": "backpack", "bag": "backpack",
    "شنطة ظهر": "backpack", "حقيبة ظهر": "backpack", "backpack": "backpack", "rucksack": "backpack",
    "حقيبة يد": "handbag", "شنطة يد": "handbag", "handbag": "handbag",
    "شنطة سفر": "luggage", "حقيبة سفر": "luggage", "suitcase": "luggage", "luggage": "luggage",
    "محفظة": "wallet", "محفظه": "wallet", "wallet": "wallet", "purse": "wallet", "card holder": "wallet",
    "جوال": "phone", "هاتف": "phone", "موبايل": "phone", "mobile": "phone", "smartphone": "phone",
    "ايفون": "phone", "آيفون": "phone", "iphone": "phone", "phone": "phone",
    "لابتوب": "laptop", "لاب توب": "laptop", "laptop": "laptop",
    "تابلت": "tablet", "جهاز لوحي": "tablet", "ايباد": "tablet", "آيباد": "tablet", "ipad": "tablet", "tablet": "tablet",
    "ساعة": "watch", "ساعه": "watch", "ساعة يد": "watch", "watch": "watch", "smartwatch": "watch",
    "سماعة": "earphones", "سماعات": "earphones", "سماعات اذن": "earphones",
    "earphones": "earphones", "earbuds": "earphones", "airpods": "earphones",
    "سماعات رأس": "headphones", "سماعة رأس": "headphones", "headphones": "headphones",
    "نظارة": "glasses", "نظارات": "glasses", "glasses": "glasses", "sunglasses": "glasses",
    "مفتاح": "keys", "مفاتيح": "keys", "key": "keys", "keys": "keys",
    "جواز": "passport", "جواز سفر": "passport", "passport": "passport",
    "هوية": "id card", "بطاقة هوية": "id card", "id card": "id card",
    "مق": "mug", "مج": "mug", "كوب": "mug", "كوب قهوة": "mug", "mug": "mug", "cup": "mug",
    "زجاجة": "bottle", "قارورة": "bottle", "bottle": "bottle",
    "قلم": "pen", "pen": "pen",
    "دفتر": "notebook", "مفكرة": "notebook", "notebook": "notebook",
    "كتاب": "book", "book": "book",
    "شاحن": "charger", "charger": "charger",
    "كاميرا": "camera", "camera": "camera",
    "مظلة": "umbrella", "umbrella": "umbrella",
    "عطر": "perfume", "perfume": "perfume",
    "لعبة": "toy", "toy": "toy",
    "ملابس": "clothing", "clothing": "clothing",
    "حذاء": "shoes", "shoe": "shoes", "shoes": "shoes",
    "مجوهرات": "jewelry", "خاتم": "jewelry", "سوار": "jewelry", "jewelry": "jewelry",
    "سلسال": "necklace", "قلادة": "necklace", "necklace": "necklace",
}

COLOR_ALIASES = {
    "اسود": "black", "أسود": "black", "سوداء": "black", "black": "black",
    "ابيض": "white", "أبيض": "white", "بيضاء": "white", "white": "white",
    "ازرق": "blue", "أزرق": "blue", "زرقاء": "blue", "blue": "blue",
    "كحلي": "navy", "navy": "navy",
    "احمر": "red", "أحمر": "red", "حمراء": "red", "red": "red",
    "اخضر": "green", "أخضر": "green", "خضراء": "green", "green": "green",
    "اصفر": "yellow", "أصفر": "yellow", "صفراء": "yellow", "yellow": "yellow",
    "رمادي": "gray", "grey": "gray", "gray": "gray",
    "بني": "brown", "بنية": "brown", "brown": "brown",
    "بيج": "beige", "beige": "beige",
    "وردي": "pink", "وردية": "pink", "زهري": "pink", "زهرية": "pink", "زهريه": "pink", "ورديه": "pink", "pink": "pink",
    "بنفسجي": "purple", "بنفسجية": "purple", "purple": "purple",
    "برتقالي": "orange", "برتقالية": "orange", "اورانج": "orange", "orange": "orange",
    "ذهبي": "gold", "ذهبية": "gold", "gold": "gold",
    "فضي": "silver", "فضية": "silver", "silver": "silver",
}

KNOWN_TYPES = set(TYPE_ALIASES.values())


SIM_CEIL = 0.55


TYPE_MATCH_BASE = 75.0
TYPE_UNKNOWN_BASE = 40.0
TYPE_CONFLICT_BASE = 10.0
FIELD_BONUS = 15.0
DESC_BONUS_MAX = 20.0
CONFLICT_CEILING = 35.0


def normalize_text(value: Any) -> str:
    if value is None:
        return ""

    text = unicodedata.normalize("NFKC", str(value)).lower().strip()
    text = re.sub(r"[ً-ٰٟ]", "", text)
    text = text.replace("أ", "ا").replace("إ", "ا").replace("آ", "ا").replace("ى", "ي")
    text = re.sub(r"[^\w\s؀-ۿ]", " ", text)

    return " ".join(text.split())


def normalize_alias(value: Any, aliases: dict[str, str]) -> str:
    text = normalize_text(value)

    if not text:
        return ""

    for alias, canonical in sorted(aliases.items(), key=lambda pair: len(pair[0]), reverse=True):
        normalized_alias = normalize_text(alias)

        if text == normalized_alias or re.search(rf"(?<!\w){re.escape(normalized_alias)}(?!\w)", text):
            return canonical

    return text


def normalize_type(value: Any) -> str:
    return normalize_alias(value, TYPE_ALIASES)


def normalize_color(value: Any) -> str:
    return normalize_alias(value, COLOR_ALIASES)


def semantic_text(item: ItemData) -> str:
    """Description is the richest signal; fall back to type when it's the
    only text available (e.g. an open-vocabulary type with no description)."""
    return normalize_text(item.description) or normalize_type(item.type)


def cosine_similarity(first: list[float], second: list[float]) -> float:
    if not first or not second:
        return 0.0

    dot = sum(a * b for a, b in zip(first, second))
    magnitude_first = math.sqrt(sum(v * v for v in first))
    magnitude_second = math.sqrt(sum(v * v for v in second))

    if magnitude_first == 0 or magnitude_second == 0:
        return 0.0

    return max(0.0, min(dot / (magnitude_first * magnitude_second), 1.0))


def field_score(value_a: str, value_b: str, known: set[str] | None = None, fuzzy: bool = False) -> float | None:
    """1.0 = agreement, 0.0 = confirmed conflict, None = evidence unavailable
    (a value is missing, or an open-vocabulary mismatch we can't confidently
    call a conflict)."""
    if not value_a or not value_b:
        return None

    if value_a == value_b or (fuzzy and (value_a in value_b or value_b in value_a)):
        return 1.0

    if known is None or (value_a in known and value_b in known):
        return 0.0

    return None


def calculate_match_score(
    first_item: ItemData,
    second_item: ItemData,
    semantic_similarity: float,
) -> tuple[float, dict[str, float | None]]:
    type_score = field_score(normalize_type(first_item.type), normalize_type(second_item.type), KNOWN_TYPES)
    color_score = field_score(normalize_color(first_item.color), normalize_color(second_item.color))
    location_score = field_score(
        normalize_text(first_item.location_name), normalize_text(second_item.location_name), fuzzy=True
    )

    has_description = bool(normalize_text(first_item.description)) and bool(normalize_text(second_item.description))
    desc_similarity = max(0.0, min((semantic_similarity - SIM_FLOOR) / (SIM_CEIL - SIM_FLOOR), 1.0))

    evidence = {
        "type": type_score,
        "color": color_score,
        "location": location_score,
        "description": desc_similarity if has_description else None,
    }

    desc_bonus = desc_similarity * DESC_BONUS_MAX if has_description else 0.0

    if type_score == 0.0:

        score = round(min(TYPE_CONFLICT_BASE + desc_bonus, CONFLICT_CEILING), 2)
    else:
        base = TYPE_MATCH_BASE if type_score == 1.0 else TYPE_UNKNOWN_BASE
        bonus = (FIELD_BONUS if color_score == 1.0 else 0.0) + (FIELD_BONUS if location_score == 1.0 else 0.0)
        score = round(min(base + bonus + desc_bonus, 100.0), 2)

    return score, evidence


def build_match_reason(evidence: dict[str, float | None]) -> str:
    if evidence["type"] == 0.0:
        return "نوع الغرض مختلف"

    reasons = ["تطابق في نوع الغرض"] if evidence["type"] == 1.0 else []

    if evidence["color"] == 1.0:
        reasons.append("تطابق في اللون")
    elif evidence["color"] == 0.0:
        reasons.append("اللون مختلف")

    if evidence["location"] == 1.0:
        reasons.append("تطابق في الموقع")
    elif evidence["location"] == 0.0:
        reasons.append("الموقع مختلف")

    description = evidence["description"]

    if description is not None:
        if description >= 0.75:
            reasons.append("تشابه دلالي مرتفع جدًا في الوصف")
        elif description >= 0.45:
            reasons.append("تشابه دلالي في الوصف")

    return "، ".join(reasons) if reasons else "لا توجد مؤشرات تطابق كافية"


def resolve_report_ids(first_item: ItemData, second_item: ItemData) -> tuple[str | None, str | None]:
    first_id = str(first_item.report_id) if first_item.report_id is not None else None
    second_id = str(second_item.report_id) if second_item.report_id is not None else None

    if first_item.is_item_with_finder is False and second_item.is_item_with_finder is True:
        return first_id, second_id

    if first_item.is_item_with_finder is True and second_item.is_item_with_finder is False:
        return second_id, first_id

    return first_id, second_id


def find_matches(lost_item: ItemData, found_items: list[ItemData]) -> list[MatchResult]:
    if not found_items:
        return []

    selected_text = semantic_text(lost_item)

    if not selected_text:
        return []

    valid_items = [item for item in found_items if semantic_text(item)]

    if not valid_items:
        return []

    candidate_texts = [semantic_text(item) for item in valid_items]
    embeddings = get_embeddings([selected_text] + candidate_texts)

    if len(embeddings) != len(valid_items) + 1:
        raise ValueError("Embedding response size mismatch.")

    selected_embedding = embeddings[0]
    results = []

    for candidate, candidate_embedding in zip(valid_items, embeddings[1:]):
        semantic_similarity = cosine_similarity(selected_embedding, candidate_embedding)
        score, evidence = calculate_match_score(lost_item, candidate, semantic_similarity)
        lost_report_id, found_report_id = resolve_report_ids(lost_item, candidate)

        results.append(
            MatchResult(
                lost_report_id=lost_report_id,
                found_report_id=found_report_id or "",
                similarity_score=score,
                match_reason=build_match_reason(evidence),
                status=classify_match(score)["status"],
            )
        )

    results.sort(key=lambda result: result.similarity_score, reverse=True)
    relevant = [result for result in results if result.similarity_score >= RELEVANT_SCORE_FLOOR]

    return (relevant or results[:1])[:MAX_MATCH_RESULTS]


def classify_match(score: float) -> dict:
    if score >= 90:
        return {"match_level": "high", "status": "potential_match", "message": "يوجد تطابق محتمل قوي جدًا"}

    if score >= 75:
        return {"match_level": "medium_high", "status": "potential_match", "message": "يوجد تطابق محتمل قوي"}

    if score >= 55:
        return {"match_level": "medium", "status": "possible_match", "message": "يوجد تطابق محتمل ويحتاج إلى التحقق"}

    return {"match_level": "low", "status": "weak_match", "message": "نسبة التطابق منخفضة"}
