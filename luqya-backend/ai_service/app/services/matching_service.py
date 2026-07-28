import math
import re
import unicodedata
from datetime import datetime
from difflib import SequenceMatcher

from app.models.schemas import ItemData, MatchResult
from app.services.embedding_service import get_embeddings


TYPE_ALIASES = {
    "شنطة": "bag",
    "حقيبة": "bag",
    "حقيبه": "bag",
    "handbag": "bag",
    "backpack": "bag",
    "rucksack": "bag",
    "suitcase": "luggage",
    "travel bag": "luggage",
    "امتعة": "luggage",
    "أمتعة": "luggage",
    "جوال": "phone",
    "جوالًا": "phone",
    "هاتف": "phone",
    "موبايل": "phone",
    "mobile": "phone",
    "smartphone": "phone",
    "iphone": "phone",
    "ايفون": "phone",
    "آيفون": "phone",
    "سماعة": "earphones",
    "سماعات": "earphones",
    "airpods": "earphones",
    "earbuds": "earphones",
    "headphones": "earphones",
    "محفظة": "wallet",
    "محفظه": "wallet",
    "wallet": "wallet",
    "purse": "wallet",
    "مفتاح": "keys",
    "مفاتيح": "keys",
    "keys": "keys",
    "key": "keys",
    "لابتوب": "laptop",
    "لاب توب": "laptop",
    "حاسب محمول": "laptop",
    "notebook": "laptop",
    "كمبيوتر محمول": "laptop",
    "ساعة": "watch",
    "ساعه": "watch",
    "smartwatch": "watch",
    "watch": "watch",
    "نظارة": "glasses",
    "نظاره": "glasses",
    "glasses": "glasses",
    "sunglasses": "glasses",
    "بطاقة": "card",
    "بطاقه": "card",
    "card": "card",
    "هوية": "id_card",
    "هويه": "id_card",
    "identity card": "id_card",
    "id card": "id_card",
    "جواز": "passport",
    "جواز سفر": "passport",
    "passport": "passport",
}


COLOR_ALIASES = {
    "اسود": "black",
    "أسود": "black",
    "black": "black",
    "ابيض": "white",
    "أبيض": "white",
    "white": "white",
    "احمر": "red",
    "أحمر": "red",
    "red": "red",
    "ازرق": "blue",
    "أزرق": "blue",
    "blue": "blue",
    "كحلي": "navy",
    "navy": "navy",
    "اخضر": "green",
    "أخضر": "green",
    "green": "green",
    "اصفر": "yellow",
    "أصفر": "yellow",
    "yellow": "yellow",
    "رمادي": "gray",
    "رصاصي": "gray",
    "grey": "gray",
    "gray": "gray",
    "بني": "brown",
    "brown": "brown",
    "بيج": "beige",
    "beige": "beige",
    "ذهبي": "gold",
    "gold": "gold",
    "فضي": "silver",
    "silver": "silver",
    "وردي": "pink",
    "زهري": "pink",
    "pink": "pink",
    "بنفسجي": "purple",
    "purple": "purple",
    "برتقالي": "orange",
    "orange": "orange",
}


NEAR_COLORS = {
    frozenset(("blue", "navy")),
    frozenset(("black", "navy")),
    frozenset(("gray", "silver")),
    frozenset(("brown", "beige")),
    frozenset(("red", "pink")),
}


def normalize_text(value: str | None) -> str:
    if not value:
        return ""

    value = unicodedata.normalize("NFKC", str(value))
    value = value.lower().strip()
    value = re.sub(r"[^\w\s\u0600-\u06FF]", " ", value)
    value = re.sub(r"\s+", " ", value)

    return value.strip()


def normalize_type(value: str | None) -> str:
    normalized = normalize_text(value)

    if not normalized:
        return ""

    if normalized in TYPE_ALIASES:
        return TYPE_ALIASES[normalized]

    for alias, canonical in TYPE_ALIASES.items():
        if alias in normalized:
            return canonical

    return normalized


def normalize_color(value: str | None) -> str:
    normalized = normalize_text(value)

    if not normalized:
        return ""

    if normalized in COLOR_ALIASES:
        return COLOR_ALIASES[normalized]

    for alias, canonical in COLOR_ALIASES.items():
        if alias in normalized:
            return canonical

    return normalized


def build_item_text(item: ItemData) -> str:
    parts = [
        normalize_type(item.type),
        normalize_text(item.description),
        normalize_color(item.color),
        normalize_text(item.location_name),
        normalize_text(item.pickup_location),
    ]

    return " | ".join(
        part
        for part in parts
        if part
    )


def cosine_similarity(
    vector1: list[float],
    vector2: list[float]
) -> float:
    dot_product = sum(
        first * second
        for first, second in zip(vector1, vector2)
    )

    magnitude1 = math.sqrt(
        sum(value * value for value in vector1)
    )

    magnitude2 = math.sqrt(
        sum(value * value for value in vector2)
    )

    if magnitude1 == 0 or magnitude2 == 0:
        return 0.0

    similarity = dot_product / (
        magnitude1 * magnitude2
    )

    return max(0.0, min(similarity, 1.0))


def string_similarity(
    first: str | None,
    second: str | None
) -> float | None:
    first_normalized = normalize_text(first)
    second_normalized = normalize_text(second)

    if not first_normalized or not second_normalized:
        return None

    if first_normalized == second_normalized:
        return 1.0

    first_words = set(first_normalized.split())
    second_words = set(second_normalized.split())

    union = first_words | second_words

    jaccard_score = (
        len(first_words & second_words) / len(union)
        if union
        else 0.0
    )

    sequence_score = SequenceMatcher(
        None,
        first_normalized,
        second_normalized
    ).ratio()

    return max(jaccard_score, sequence_score)


def type_similarity(
    first: str | None,
    second: str | None
) -> float | None:
    first_normalized = normalize_type(first)
    second_normalized = normalize_type(second)

    if not first_normalized or not second_normalized:
        return None

    if first_normalized == second_normalized:
        return 1.0

    return string_similarity(
        first_normalized,
        second_normalized
    )


def color_similarity(
    first: str | None,
    second: str | None
) -> float | None:
    first_normalized = normalize_color(first)
    second_normalized = normalize_color(second)

    if not first_normalized or not second_normalized:
        return None

    if first_normalized == second_normalized:
        return 1.0

    if frozenset(
        (first_normalized, second_normalized)
    ) in NEAR_COLORS:
        return 0.7

    return 0.0


def location_similarity(
    first: str | None,
    second: str | None
) -> float | None:
    return string_similarity(first, second)


def parse_datetime(
    value: datetime | str | None
) -> datetime | None:
    if value is None:
        return None

    if isinstance(value, datetime):
        return value

    try:
        return datetime.fromisoformat(
            str(value).replace("Z", "+00:00")
        )
    except ValueError:
        return None


def date_similarity(
    first: datetime | str | None,
    second: datetime | str | None
) -> float | None:
    first_date = parse_datetime(first)
    second_date = parse_datetime(second)

    if not first_date or not second_date:
        return None

    difference = abs(
        (first_date.date() - second_date.date()).days
    )

    if difference == 0:
        return 1.0

    if difference <= 1:
        return 0.9

    if difference <= 3:
        return 0.75

    if difference <= 7:
        return 0.5

    if difference <= 14:
        return 0.25

    return 0.0


def weighted_score(
    values: dict[str, float | None],
    weights: dict[str, float]
) -> float:
    available_values = {
        key: value
        for key, value in values.items()
        if value is not None
    }

    if not available_values:
        return 0.0

    total_weight = sum(
        weights[key]
        for key in available_values
    )

    if total_weight == 0:
        return 0.0

    score = sum(
        available_values[key] * weights[key]
        for key in available_values
    ) / total_weight

    return max(0.0, min(score, 1.0))


def calculate_penalty(
    type_score: float | None,
    color_score: float | None,
    semantic_score: float
) -> float:
    penalty = 0.0

    if (
        type_score is not None
        and type_score < 0.35
        and semantic_score < 0.80
    ):
        penalty += 0.18

    if (
        color_score is not None
        and color_score == 0.0
    ):
        penalty += 0.08

    return penalty


def build_match_reasons(
    semantic_score: float,
    type_score: float | None,
    color_score: float | None,
    location_score: float | None,
    date_score: float | None
) -> list[str]:
    reasons = []

    if semantic_score >= 0.85:
        reasons.append(
            "تشابه دلالي مرتفع جدًا في وصف الغرض"
        )
    elif semantic_score >= 0.72:
        reasons.append(
            "تشابه دلالي مرتفع في وصف الغرض"
        )
    elif semantic_score >= 0.58:
        reasons.append(
            "يوجد تشابه دلالي في وصف الغرض"
        )

    if type_score is not None:
        if type_score >= 0.9:
            reasons.append(
                "تطابق واضح في نوع الغرض"
            )
        elif type_score >= 0.65:
            reasons.append(
                "تقارب في نوع الغرض"
            )

    if color_score is not None:
        if color_score >= 0.9:
            reasons.append(
                "تطابق في اللون"
            )
        elif color_score >= 0.6:
            reasons.append(
                "تقارب في اللون"
            )

    if location_score is not None:
        if location_score >= 0.9:
            reasons.append(
                "تطابق في الموقع"
            )
        elif location_score >= 0.65:
            reasons.append(
                "تقارب في الموقع"
            )

    if date_score is not None:
        if date_score >= 0.9:
            reasons.append(
                "تقارب كبير في تاريخ البلاغ"
            )
        elif date_score >= 0.5:
            reasons.append(
                "تقارب في تاريخ البلاغ"
            )

    return reasons


def find_matches(
    lost_item: ItemData,
    found_items: list[ItemData]
) -> list[MatchResult]:
    if not found_items:
        return []

    lost_text = build_item_text(lost_item)

    found_texts = [
        build_item_text(item)
        for item in found_items
    ]

    embeddings = get_embeddings(
        [lost_text] + found_texts
    )

    if len(embeddings) != len(found_items) + 1:
        raise ValueError(
            "عدد الـembeddings المستلمة غير صحيح"
        )

    lost_embedding = embeddings[0]
    found_embeddings = embeddings[1:]

    weights = {
        "semantic": 0.55,
        "type": 0.18,
        "color": 0.10,
        "location": 0.10,
        "date": 0.07,
    }

    results = []

    for found_item, found_embedding in zip(
        found_items,
        found_embeddings
    ):
        semantic_score = cosine_similarity(
            lost_embedding,
            found_embedding
        )

        item_type_score = type_similarity(
            lost_item.type,
            found_item.type
        )

        item_color_score = color_similarity(
            lost_item.color,
            found_item.color
        )

        item_location_score = location_similarity(
            lost_item.location_name,
            found_item.location_name
        )

        if item_location_score is None:
            item_location_score = location_similarity(
                lost_item.pickup_location,
                found_item.pickup_location
            )

        item_date_score = date_similarity(
            lost_item.lost_found_date,
            found_item.lost_found_date
        )

        score_values = {
            "semantic": semantic_score,
            "type": item_type_score,
            "color": item_color_score,
            "location": item_location_score,
            "date": item_date_score,
        }

        base_score = weighted_score(
            score_values,
            weights
        )

        penalty = calculate_penalty(
            item_type_score,
            item_color_score,
            semantic_score
        )

        final_score = round(
            max(
                0.0,
                min(
                    (base_score - penalty) * 100,
                    100.0
                )
            ),
            2
        )

        reasons = build_match_reasons(
            semantic_score,
            item_type_score,
            item_color_score,
            item_location_score,
            item_date_score
        )

        match_status = classify_match(
            final_score
        )

        results.append(
            MatchResult(
                lost_report_id=lost_item.report_id,
                found_report_id=found_item.report_id or 0,
                similarity_score=final_score,
                match_reason=(
                    "، ".join(reasons)
                    if reasons
                    else "لا توجد مؤشرات تطابق كافية"
                ),
                status=match_status["status"]
            )
        )

    results.sort(
        key=lambda result: result.similarity_score,
        reverse=True
    )

    return results[:5]


def classify_match(
    score: float
) -> dict:
    if score >= 85:
        return {
            "match_level": "high",
            "status": "potential_match",
            "message": "يوجد تطابق محتمل قوي جدًا"
        }

    if score >= 70:
        return {
            "match_level": "medium_high",
            "status": "potential_match",
            "message": "يوجد تطابق محتمل قوي"
        }

    if score >= 52:
        return {
            "match_level": "medium",
            "status": "possible_match",
            "message": "يوجد تطابق محتمل ويحتاج إلى التحقق"
        }

    return {
        "match_level": "low",
        "status": "weak_match",
        "message": "نسبة التطابق منخفضة"
    }