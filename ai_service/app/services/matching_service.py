import math
import re
import unicodedata
from datetime import date, datetime
from difflib import SequenceMatcher
from typing import Any

from app.models.schemas import ItemData, MatchResult
from app.services.embedding_service import get_embeddings


MAX_MATCH_RESULTS = 5


TYPE_ALIASES = {
    "شنطة": "bag",
    "شنطه": "bag",
    "حقيبة": "bag",
    "حقيبه": "bag",
    "bag": "bag",

    "شنطة ظهر": "backpack",
    "شنطه ظهر": "backpack",
    "حقيبة ظهر": "backpack",
    "حقيبه ظهر": "backpack",
    "backpack": "backpack",
    "rucksack": "backpack",

    "حقيبة يد": "handbag",
    "حقيبه يد": "handbag",
    "شنطة يد": "handbag",
    "شنطه يد": "handbag",
    "handbag": "handbag",

    "شنطة سفر": "luggage",
    "شنطه سفر": "luggage",
    "حقيبة سفر": "luggage",
    "حقيبه سفر": "luggage",
    "suitcase": "luggage",
    "travel bag": "luggage",
    "luggage": "luggage",
    "امتعة": "luggage",
    "أمتعة": "luggage",

    "جوال": "phone",
    "جوالا": "phone",
    "جوالًا": "phone",
    "هاتف": "phone",
    "هاتف محمول": "phone",
    "موبايل": "phone",
    "mobile": "phone",
    "smartphone": "phone",
    "iphone": "phone",
    "ايفون": "phone",
    "آيفون": "phone",
    "phone": "phone",

    "سماعة": "earphones",
    "سماعه": "earphones",
    "سماعات": "earphones",
    "سماعات اذن": "earphones",
    "سماعات أذن": "earphones",
    "earphones": "earphones",
    "earbuds": "earphones",
    "airpods": "earphones",

    "سماعة رأس": "headphones",
    "سماعه رأس": "headphones",
    "سماعات رأس": "headphones",
    "headphones": "headphones",

    "محفظة": "wallet",
    "محفظه": "wallet",
    "wallet": "wallet",
    "purse": "wallet",

    "مفتاح": "keys",
    "مفاتيح": "keys",
    "key": "keys",
    "keys": "keys",

    "لابتوب": "laptop",
    "لاب توب": "laptop",
    "حاسب محمول": "laptop",
    "كمبيوتر محمول": "laptop",
    "notebook": "laptop",
    "laptop": "laptop",

    "تابلت": "tablet",
    "جهاز لوحي": "tablet",
    "ايباد": "tablet",
    "آيباد": "tablet",
    "ipad": "tablet",
    "tablet": "tablet",

    "ساعة": "watch",
    "ساعه": "watch",
    "ساعة يد": "watch",
    "ساعه يد": "watch",
    "smartwatch": "watch",
    "watch": "watch",

    "نظارة": "glasses",
    "نظاره": "glasses",
    "نظارات": "glasses",
    "نظارة شمسية": "glasses",
    "نظاره شمسيه": "glasses",
    "glasses": "glasses",
    "sunglasses": "glasses",

    "بطاقة": "card",
    "بطاقه": "card",
    "card": "card",

    "هوية": "id_card",
    "هويه": "id_card",
    "بطاقة هوية": "id_card",
    "بطاقه هويه": "id_card",
    "identity card": "id_card",
    "id card": "id_card",
    "id_card": "id_card",

    "جواز": "passport",
    "جواز سفر": "passport",
    "passport": "passport",

    "مجوهرات": "jewelry",
    "مجوهرات ذهبية": "jewelry",
    "خاتم": "jewelry",
    "سلسال": "jewelry",
    "قلادة": "jewelry",
    "اسوارة": "jewelry",
    "سوار": "jewelry",
    "jewelry": "jewelry",

    "زجاجة": "bottle",
    "زجاجه": "bottle",
    "قارورة": "bottle",
    "قاروره": "bottle",
    "bottle": "bottle",

    "ملابس": "clothing",
    "قطعة ملابس": "clothing",
    "قطعه ملابس": "clothing",
    "clothes": "clothing",
    "clothing": "clothing",

    "شاحن": "charger",
    "charger": "charger",

    "كاميرا": "camera",
    "camera": "camera",

    "مظلة": "umbrella",
    "مظله": "umbrella",
    "umbrella": "umbrella",

    "كتاب": "book",
    "book": "book",

    "مستند": "document",
    "وثيقة": "document",
    "وثيقه": "document",
    "document": "document",
}


TYPE_FAMILIES = {
    "bag": "bags",
    "backpack": "bags",
    "handbag": "bags",
    "luggage": "bags",

    "phone": "electronics",
    "laptop": "electronics",
    "tablet": "electronics",
    "camera": "electronics",

    "earphones": "audio",
    "headphones": "audio",

    "card": "documents",
    "id_card": "documents",
    "passport": "documents",
    "document": "documents",

    "watch": "accessories",
    "jewelry": "accessories",
    "glasses": "accessories",
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
    "dark blue": "navy",

    "سماوي": "light blue",
    "light blue": "light blue",

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
    "golden": "gold",

    "فضي": "silver",
    "silver": "silver",

    "وردي": "pink",
    "زهري": "pink",
    "pink": "pink",

    "بنفسجي": "purple",
    "purple": "purple",

    "برتقالي": "orange",
    "orange": "orange",

    "شفاف": "transparent",
    "transparent": "transparent",

    "متعدد الالوان": "multicolor",
    "متعدد الألوان": "multicolor",
    "multicolor": "multicolor",
}


NEAR_COLORS = {
    frozenset(("blue", "navy")),
    frozenset(("blue", "light blue")),
    frozenset(("black", "navy")),
    frozenset(("gray", "silver")),
    frozenset(("brown", "beige")),
    frozenset(("red", "pink")),
    frozenset(("yellow", "gold")),
    frozenset(("white", "silver")),
}


MATCH_WEIGHTS = {
    "semantic": 0.55,
    "type": 0.18,
    "color": 0.10,
    "location": 0.10,
    "date": 0.07,
}


def normalize_text(value: Any) -> str:
    if value is None:
        return ""

    normalized = unicodedata.normalize(
        "NFKC",
        str(value),
    )

    normalized = normalized.lower().strip()

    normalized = re.sub(
        r"[^\w\s\u0600-\u06FF]",
        " ",
        normalized,
    )

    normalized = re.sub(
        r"\s+",
        " ",
        normalized,
    )

    return normalized.strip()


def normalize_type(value: Any) -> str:
    normalized = normalize_text(value)

    if not normalized:
        return ""

    if normalized in TYPE_ALIASES:
        return TYPE_ALIASES[normalized]

    sorted_aliases = sorted(
        TYPE_ALIASES.items(),
        key=lambda item: len(item[0]),
        reverse=True,
    )

    for alias, canonical in sorted_aliases:
        if re.search(
            rf"\b{re.escape(alias)}\b",
            normalized,
        ):
            return canonical

    return normalized.replace(" ", "_")


def normalize_color(value: Any) -> str:
    normalized = normalize_text(value)

    if not normalized:
        return ""

    if normalized in COLOR_ALIASES:
        return COLOR_ALIASES[normalized]

    sorted_aliases = sorted(
        COLOR_ALIASES.items(),
        key=lambda item: len(item[0]),
        reverse=True,
    )

    for alias, canonical in sorted_aliases:
        if re.search(
            rf"\b{re.escape(alias)}\b",
            normalized,
        ):
            return canonical

    return normalized


def build_item_text(item: ItemData) -> str:
    parts = [
        normalize_type(item.type),
        normalize_text(item.description),
        normalize_color(item.color),
    ]

    text = " | ".join(
        part
        for part in parts
        if part
    )

    return text.strip()


def cosine_similarity(
    vector1: list[float],
    vector2: list[float],
) -> float:
    if not vector1 or not vector2:
        return 0.0

    if len(vector1) != len(vector2):
        raise ValueError(
            "Embedding vectors must have the same dimensions."
        )

    dot_product = sum(
        first * second
        for first, second in zip(
            vector1,
            vector2,
        )
    )

    magnitude1 = math.sqrt(
        sum(
            value * value
            for value in vector1
        )
    )

    magnitude2 = math.sqrt(
        sum(
            value * value
            for value in vector2
        )
    )

    if magnitude1 == 0 or magnitude2 == 0:
        return 0.0

    similarity = dot_product / (
        magnitude1 * magnitude2
    )

    return max(
        0.0,
        min(similarity, 1.0),
    )


def string_similarity(
    first: Any,
    second: Any,
) -> float | None:
    first_normalized = normalize_text(first)
    second_normalized = normalize_text(second)

    if not first_normalized or not second_normalized:
        return None

    if first_normalized == second_normalized:
        return 1.0

    first_words = set(
        first_normalized.split()
    )

    second_words = set(
        second_normalized.split()
    )

    union = first_words | second_words

    jaccard_score = (
        len(first_words & second_words) / len(union)
        if union
        else 0.0
    )

    sequence_score = SequenceMatcher(
        None,
        first_normalized,
        second_normalized,
    ).ratio()

    containment_score = 0.0

    if (
        first_normalized in second_normalized
        or second_normalized in first_normalized
    ):
        containment_score = 0.85

    return max(
        jaccard_score,
        sequence_score,
        containment_score,
    )


def type_similarity(
    first: Any,
    second: Any,
) -> float | None:
    first_normalized = normalize_type(first)
    second_normalized = normalize_type(second)

    if not first_normalized or not second_normalized:
        return None

    if first_normalized == second_normalized:
        return 1.0

    first_family = TYPE_FAMILIES.get(
        first_normalized
    )

    second_family = TYPE_FAMILIES.get(
        second_normalized
    )

    if (
        first_family
        and second_family
        and first_family == second_family
    ):
        if first_family == "bags":
            return 0.78

        if first_family == "audio":
            return 0.80

        if first_family == "documents":
            return 0.65

        if first_family == "electronics":
            return 0.45

        if first_family == "accessories":
            return 0.35

    text_score = string_similarity(
        first_normalized,
        second_normalized,
    )

    if text_score is None:
        return None

    if text_score >= 0.85:
        return 0.75

    return 0.0


def color_similarity(
    first: Any,
    second: Any,
) -> float | None:
    first_normalized = normalize_color(first)
    second_normalized = normalize_color(second)

    if not first_normalized or not second_normalized:
        return None

    if first_normalized == second_normalized:
        return 1.0

    color_pair = frozenset(
        (
            first_normalized,
            second_normalized,
        )
    )

    if color_pair in NEAR_COLORS:
        return 0.70

    if (
        first_normalized == "multicolor"
        or second_normalized == "multicolor"
    ):
        return 0.40

    return 0.0


def location_similarity(
    first: Any,
    second: Any,
) -> float | None:
    return string_similarity(
        first,
        second,
    )


def best_location_similarity(
    first_item: ItemData,
    second_item: ItemData,
) -> float | None:
    comparisons = [
        location_similarity(
            first_item.location_name,
            second_item.location_name,
        ),
        location_similarity(
            first_item.location_name,
            second_item.pickup_location,
        ),
        location_similarity(
            first_item.pickup_location,
            second_item.location_name,
        ),
        location_similarity(
            first_item.pickup_location,
            second_item.pickup_location,
        ),
    ]

    available_scores = [
        score
        for score in comparisons
        if score is not None
    ]

    if not available_scores:
        return None

    return max(available_scores)


def parse_datetime(
    value: datetime | date | str | None,
) -> datetime | None:
    if value is None:
        return None

    if isinstance(value, datetime):
        return value

    if isinstance(value, date):
        return datetime.combine(
            value,
            datetime.min.time(),
        )

    normalized_value = str(value).strip()

    if not normalized_value:
        return None

    try:
        return datetime.fromisoformat(
            normalized_value.replace(
                "Z",
                "+00:00",
            )
        )

    except ValueError:
        pass

    supported_formats = [
        "%Y-%m-%d",
        "%d-%m-%Y",
        "%d/%m/%Y",
        "%Y/%m/%d",
    ]

    for date_format in supported_formats:
        try:
            return datetime.strptime(
                normalized_value,
                date_format,
            )

        except ValueError:
            continue

    return None


def date_similarity(
    first: datetime | date | str | None,
    second: datetime | date | str | None,
) -> float | None:
    first_date = parse_datetime(first)
    second_date = parse_datetime(second)

    if not first_date or not second_date:
        return None

    difference = abs(
        (
            first_date.date()
            - second_date.date()
        ).days
    )

    if difference == 0:
        return 1.0

    if difference <= 1:
        return 0.90

    if difference <= 3:
        return 0.75

    if difference <= 7:
        return 0.50

    if difference <= 14:
        return 0.25

    return 0.0


def weighted_score(
    values: dict[str, float | None],
    weights: dict[str, float],
) -> float:
    available_values = {
        key: value
        for key, value in values.items()
        if (
            value is not None
            and key in weights
        )
    }

    if not available_values:
        return 0.0

    total_weight = sum(
        weights[key]
        for key in available_values
    )

    if total_weight <= 0:
        return 0.0

    score = sum(
        available_values[key] * weights[key]
        for key in available_values
    ) / total_weight

    return max(
        0.0,
        min(score, 1.0),
    )


def calculate_penalty(
    type_score: float | None,
    color_score: float | None,
    location_score: float | None,
    date_score: float | None,
    semantic_score: float,
) -> float:
    penalty = 0.0

    if type_score is not None:
        if type_score == 0.0:
            penalty += 0.25
        elif type_score < 0.50:
            penalty += 0.10

    if (
        color_score is not None
        and color_score == 0.0
    ):
        penalty += 0.08

    if (
        location_score is not None
        and location_score < 0.25
    ):
        penalty += 0.04

    if (
        date_score is not None
        and date_score == 0.0
    ):
        penalty += 0.04

    if semantic_score < 0.40:
        penalty += 0.08

    return min(
        penalty,
        0.45,
    )


def apply_score_limits(
    score: float,
    semantic_score: float,
    type_score: float | None,
) -> float:
    limited_score = score

    if (
        type_score is not None
        and type_score == 0.0
    ):
        limited_score = min(
            limited_score,
            49.0,
        )

    if (
        semantic_score < 0.35
        and (
            type_score is None
            or type_score < 0.75
        )
    ):
        limited_score = min(
            limited_score,
            39.0,
        )

    return max(
        0.0,
        min(limited_score, 100.0),
    )


def build_match_reasons(
    semantic_score: float,
    type_score: float | None,
    color_score: float | None,
    location_score: float | None,
    date_score: float | None,
) -> list[str]:
    reasons: list[str] = []

    if semantic_score >= 0.88:
        reasons.append(
            "تشابه دلالي مرتفع جدًا في وصف الغرض"
        )

    elif semantic_score >= 0.74:
        reasons.append(
            "تشابه دلالي مرتفع في وصف الغرض"
        )

    elif semantic_score >= 0.58:
        reasons.append(
            "يوجد تشابه دلالي في وصف الغرض"
        )

    if type_score is not None:
        if type_score >= 0.95:
            reasons.append(
                "تطابق واضح في نوع الغرض"
            )

        elif type_score >= 0.70:
            reasons.append(
                "تقارب كبير في نوع الغرض"
            )

        elif type_score >= 0.40:
            reasons.append(
                "تقارب جزئي في نوع الغرض"
            )

        elif type_score == 0.0:
            reasons.append(
                "نوع الغرض مختلف"
            )

    if color_score is not None:
        if color_score >= 0.95:
            reasons.append(
                "تطابق في اللون"
            )

        elif color_score >= 0.65:
            reasons.append(
                "تقارب في اللون"
            )

        elif color_score == 0.0:
            reasons.append(
                "اللون مختلف"
            )

    if location_score is not None:
        if location_score >= 0.90:
            reasons.append(
                "تطابق في الموقع"
            )

        elif location_score >= 0.65:
            reasons.append(
                "تقارب في الموقع"
            )

    if date_score is not None:
        if date_score >= 0.90:
            reasons.append(
                "تقارب كبير في تاريخ البلاغ"
            )

        elif date_score >= 0.50:
            reasons.append(
                "تقارب في تاريخ البلاغ"
            )

    return reasons


def normalize_report_id(
    value: Any,
) -> str | None:
    if value is None:
        return None

    normalized_value = str(value).strip()

    if not normalized_value:
        return None

    return normalized_value


def resolve_report_ids(
    first_item: ItemData,
    second_item: ItemData,
) -> tuple[str | None, str | None]:
    first_id = normalize_report_id(
        first_item.report_id
    )

    second_id = normalize_report_id(
        second_item.report_id
    )

    if (
        first_item.is_item_with_finder is False
        and second_item.is_item_with_finder is True
    ):
        return first_id, second_id

    if (
        first_item.is_item_with_finder is True
        and second_item.is_item_with_finder is False
    ):
        return second_id, first_id

    return first_id, second_id


def validate_matching_input(
    selected_item: ItemData,
    candidate_items: list[ItemData],
) -> None:
    selected_text = build_item_text(
        selected_item
    )

    if not selected_text:
        raise ValueError(
            "لا توجد معلومات كافية عن الغرض المطلوب مطابقته."
        )

    if not isinstance(candidate_items, list):
        raise TypeError(
            "candidate_items must be a list."
        )


def find_matches(
    lost_item: ItemData,
    found_items: list[ItemData],
) -> list[MatchResult]:
    if not found_items:
        return []

    validate_matching_input(
        selected_item=lost_item,
        candidate_items=found_items,
    )

    selected_text = build_item_text(
        lost_item
    )

    valid_candidates: list[ItemData] = []
    candidate_texts: list[str] = []

    for candidate in found_items:
        candidate_text = build_item_text(
            candidate
        )

        if not candidate_text:
            continue

        valid_candidates.append(
            candidate
        )

        candidate_texts.append(
            candidate_text
        )

    if not valid_candidates:
        return []

    embeddings = get_embeddings(
        [selected_text] + candidate_texts
    )

    expected_embeddings_count = (
        len(valid_candidates) + 1
    )

    if len(embeddings) != expected_embeddings_count:
        raise ValueError(
            "عدد الـ embeddings المستلمة لا يطابق عدد الأغراض."
        )

    selected_embedding = embeddings[0]
    candidate_embeddings = embeddings[1:]

    results: list[MatchResult] = []

    for candidate, candidate_embedding in zip(
        valid_candidates,
        candidate_embeddings,
    ):
        semantic_score = cosine_similarity(
            selected_embedding,
            candidate_embedding,
        )

        item_type_score = type_similarity(
            lost_item.type,
            candidate.type,
        )

        item_color_score = color_similarity(
            lost_item.color,
            candidate.color,
        )

        item_location_score = best_location_similarity(
            lost_item,
            candidate,
        )

        item_date_score = date_similarity(
            lost_item.lost_found_date,
            candidate.lost_found_date,
        )

        score_values = {
            "semantic": semantic_score,
            "type": item_type_score,
            "color": item_color_score,
            "location": item_location_score,
            "date": item_date_score,
        }

        base_score = weighted_score(
            values=score_values,
            weights=MATCH_WEIGHTS,
        )

        penalty = calculate_penalty(
            type_score=item_type_score,
            color_score=item_color_score,
            location_score=item_location_score,
            date_score=item_date_score,
            semantic_score=semantic_score,
        )

        final_score = (
            base_score - penalty
        ) * 100

        final_score = apply_score_limits(
            score=final_score,
            semantic_score=semantic_score,
            type_score=item_type_score,
        )

        final_score = round(
            final_score,
            2,
        )

        reasons = build_match_reasons(
            semantic_score=semantic_score,
            type_score=item_type_score,
            color_score=item_color_score,
            location_score=item_location_score,
            date_score=item_date_score,
        )

        match_status = classify_match(
            final_score
        )

        lost_report_id, found_report_id = (
            resolve_report_ids(
                first_item=lost_item,
                second_item=candidate,
            )
        )

        results.append(
            MatchResult(
                lost_report_id=lost_report_id,
                found_report_id=found_report_id,
                similarity_score=final_score,
                match_reason=(
                    "، ".join(reasons)
                    if reasons
                    else "لا توجد مؤشرات تطابق كافية"
                ),
                status=match_status["status"],
            )
        )

    results.sort(
        key=lambda result: result.similarity_score,
        reverse=True,
    )

    return results[:MAX_MATCH_RESULTS]


def classify_match(
    score: float,
) -> dict:
    normalized_score = max(
        0.0,
        min(float(score), 100.0),
    )

    if normalized_score >= 85:
        return {
            "match_level": "high",
            "status": "potential_match",
            "message": "يوجد تطابق محتمل قوي جدًا",
        }

    if normalized_score >= 70:
        return {
            "match_level": "medium_high",
            "status": "potential_match",
            "message": "يوجد تطابق محتمل قوي",
        }

    if normalized_score >= 52:
        return {
            "match_level": "medium",
            "status": "possible_match",
            "message": (
                "يوجد تطابق محتمل ويحتاج إلى التحقق"
            ),
        }

    return {
        "match_level": "low",
        "status": "weak_match",
        "message": "نسبة التطابق منخفضة",
    }