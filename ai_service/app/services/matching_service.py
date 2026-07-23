import math

from app.models.schemas import ItemData, MatchResult
from app.services.embedding_service import get_embeddings


def build_item_text(item: ItemData) -> str:
    parts = [
        f"type: {item.type}",
        f"description: {item.description}",
        f"color: {item.color or ''}",
        f"location: {item.location_name or ''}",
        f"pickup_location: {item.pickup_location or ''}",
    ]

    return " | ".join(parts)


def cosine_similarity(
    vector1: list[float],
    vector2: list[float]
) -> float:

    dot_product = sum(
        a * b
        for a, b in zip(vector1, vector2)
    )

    magnitude1 = math.sqrt(
        sum(a * a for a in vector1)
    )

    magnitude2 = math.sqrt(
        sum(b * b for b in vector2)
    )

    if magnitude1 == 0 or magnitude2 == 0:
        return 0.0

    return dot_product / (
        magnitude1 * magnitude2
    )


def find_matches(
    lost_item: ItemData,
    found_items: list[ItemData]
) -> list[MatchResult]:

    if not found_items:
        return []

    lost_text = build_item_text(
        lost_item
    )

    found_texts = [
        build_item_text(item)
        for item in found_items
    ]

    embeddings = get_embeddings(
        [lost_text] + found_texts
    )

    lost_embedding = embeddings[0]
    found_embeddings = embeddings[1:]

    results = []

    for found_item, found_embedding in zip(
        found_items,
        found_embeddings
    ):
        similarity = cosine_similarity(
            lost_embedding,
            found_embedding
        )

        score = similarity * 80
        reasons = []

        if similarity >= 0.75:
            reasons.append(
                "تشابه دلالي مرتفع في مواصفات الغرض"
            )

        elif similarity >= 0.55:
            reasons.append(
                "يوجد تشابه في مواصفات الغرض"
            )

        if (
            lost_item.location_name
            and found_item.location_name
            and lost_item.location_name.lower()
            == found_item.location_name.lower()
        ):
            score += 10
            reasons.append(
                "تطابق في الموقع"
            )

        if (
            lost_item.type.lower()
            == found_item.type.lower()
        ):
            score += 10
            reasons.append(
                "تطابق في نوع الغرض"
            )

        final_score = round(
            min(score, 100),
            2
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
                    else "تشابه منخفض"
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

    if score >= 75:
        return {
            "match_level": "high",
            "status": "potential_match",
            "message": "يوجد تطابق محتمل قوي"
        }

    elif score >= 50:
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