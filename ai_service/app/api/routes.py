import json
from datetime import datetime
from typing import Any

import httpx
from fastapi import (
    APIRouter,
    File,
    Form,
    HTTPException,
    UploadFile,
)

from app.models.schemas import ItemData
from app.services.chat_service import extract_item_from_message
from app.services.image_service import analyze_item_image
from app.services.matching_service import (
    classify_match,
    find_matches,
)
from app.services.report_api_service import get_mapped_reports


router = APIRouter(
    prefix="/api/ai",
    tags=["AI Matching"],
)


def normalize_report_id(
    value: Any,
) -> str | None:
    if value is None:
        return None

    normalized_value = str(value).strip().lower()

    if not normalized_value:
        return None

    return normalized_value


def normalize_optional_text(
    value: Any,
) -> str | None:
    if value is None:
        return None

    normalized_value = " ".join(
        str(value).strip().split()
    )

    if not normalized_value:
        return None

    if normalized_value.lower() in {
        "none",
        "null",
        "unknown",
        "not specified",
    }:
        return None

    return normalized_value


def normalize_datetime(
    value: Any,
) -> datetime | None:
    if value is None:
        return None

    if isinstance(value, datetime):
        return value

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

    except ValueError as exc:
        raise HTTPException(
            status_code=422,
            detail=(
                "Invalid date format. Use ISO format, "
                "for example: 2026-07-28."
            ),
        ) from exc


async def fetch_mapped_reports() -> list[ItemData]:
    try:
        return await get_mapped_reports(
            page_size=100,
        )

    except httpx.HTTPStatusError as exc:
        backend_message = exc.response.text

        raise HTTPException(
            status_code=502,
            detail=(
                "Backend API returned an unsuccessful response: "
                f"{backend_message}"
            ),
        ) from exc

    except httpx.TimeoutException as exc:
        raise HTTPException(
            status_code=504,
            detail="Backend API request timed out.",
        ) from exc

    except httpx.RequestError as exc:
        raise HTTPException(
            status_code=503,
            detail="Could not connect to the Backend API.",
        ) from exc

    except RuntimeError as exc:
        raise HTTPException(
            status_code=500,
            detail=str(exc),
        ) from exc

    except ValueError as exc:
        raise HTTPException(
            status_code=502,
            detail=str(exc),
        ) from exc


def get_opposite_reports(
    selected_report: ItemData,
    reports: list[ItemData],
) -> list[ItemData]:
    if selected_report.is_item_with_finder is None:
        raise HTTPException(
            status_code=422,
            detail=(
                "The selected report does not specify whether "
                "the item is lost or found."
            ),
        )

    selected_report_id = normalize_report_id(
        selected_report.report_id
    )

    candidate_reports: list[ItemData] = []

    for report in reports:
        report_id = normalize_report_id(
            report.report_id
        )

        if not report_id:
            continue

        if report_id == selected_report_id:
            continue

        if report.is_item_with_finder is None:
            continue

        if (
            report.is_item_with_finder
            == selected_report.is_item_with_finder
        ):
            continue

        candidate_reports.append(
            report
        )

    return candidate_reports


def get_found_reports(
    reports: list[ItemData],
) -> list[ItemData]:
    return [
        report
        for report in reports
        if (
            report.report_id is not None
            and report.is_item_with_finder is True
        )
    ]


def serialize_match_result(
    match: Any,
) -> dict:
    if hasattr(match, "model_dump"):
        return match.model_dump()

    if hasattr(match, "dict"):
        return match.dict()

    if isinstance(match, dict):
        return match

    raise TypeError(
        "Unsupported match result type."
    )


def get_match_decision(
    best_match: dict | None,
) -> dict | None:
    if not best_match:
        return None

    similarity_score = best_match.get(
        "similarity_score"
    )

    if similarity_score is None:
        return None

    try:
        score = float(
            similarity_score
        )

    except (TypeError, ValueError):
        return None

    return classify_match(
        score
    )


def run_matching(
    selected_item: ItemData,
    candidate_items: list[ItemData],
) -> list[dict]:
    try:
        matches = find_matches(
            lost_item=selected_item,
            found_items=candidate_items,
        )

    except ValueError as exc:
        raise HTTPException(
            status_code=422,
            detail=str(exc),
        ) from exc

    except RuntimeError as exc:
        raise HTTPException(
            status_code=502,
            detail=str(exc),
        ) from exc

    except Exception as exc:
        raise HTTPException(
            status_code=502,
            detail="Unable to calculate item matches.",
        ) from exc

    return [
        serialize_match_result(match)
        for match in matches
    ]


def build_matching_response(
    selected_report: ItemData,
    candidate_reports: list[ItemData],
) -> dict:
    report_kind = (
        "found"
        if selected_report.is_item_with_finder is True
        else "lost"
    )

    candidate_kind = (
        "lost"
        if report_kind == "found"
        else "found"
    )

    if not candidate_reports:
        return {
            "report": selected_report.model_dump(),
            "report_kind": report_kind,
            "candidate_kind": candidate_kind,
            "candidate_count": 0,
            "matches": [],
            "best_match": None,
            "decision": None,
            "message": (
                "لا توجد بلاغات من النوع المقابل "
                "متاحة للمقارنة حاليًا."
            ),
        }

    serialized_matches = run_matching(
        selected_item=selected_report,
        candidate_items=candidate_reports,
    )

    best_match = (
        serialized_matches[0]
        if serialized_matches
        else None
    )

    return {
        "report": selected_report.model_dump(),
        "report_kind": report_kind,
        "candidate_kind": candidate_kind,
        "candidate_count": len(candidate_reports),
        "matches": serialized_matches,
        "best_match": best_match,
        "decision": get_match_decision(
            best_match
        ),
    }


@router.get("/health")
async def ai_health():
    return {
        "status": "ok",
        "service": "Luqya AI Service",
    }


@router.get("/test-reports")
async def test_reports():
    reports = await fetch_mapped_reports()

    found_reports_count = sum(
        1
        for report in reports
        if report.is_item_with_finder is True
    )

    lost_reports_count = sum(
        1
        for report in reports
        if report.is_item_with_finder is False
    )

    unknown_reports_count = sum(
        1
        for report in reports
        if report.is_item_with_finder is None
    )

    return {
        "count": len(reports),
        "found_reports_count": found_reports_count,
        "lost_reports_count": lost_reports_count,
        "unknown_reports_count": unknown_reports_count,
        "mapped_reports": [
            report.model_dump()
            for report in reports
        ],
    }


@router.post("/match-report/{report_id}")
async def match_report(
    report_id: str,
):
    normalized_report_id = normalize_report_id(
        report_id
    )

    if not normalized_report_id:
        raise HTTPException(
            status_code=400,
            detail="A valid report ID is required.",
        )

    reports = await fetch_mapped_reports()

    selected_report = next(
        (
            report
            for report in reports
            if normalize_report_id(
                report.report_id
            ) == normalized_report_id
        ),
        None,
    )

    if selected_report is None:
        raise HTTPException(
            status_code=404,
            detail="The requested report was not found.",
        )

    candidate_reports = get_opposite_reports(
        selected_report=selected_report,
        reports=reports,
    )

    return build_matching_response(
        selected_report=selected_report,
        candidate_reports=candidate_reports,
    )


@router.post("/match-image")
async def match_image(
    image: UploadFile = File(...),
    location_name: str | None = Form(None),
    lost_found_date: str | None = Form(None),
):
    if (
        not image.content_type
        or not image.content_type.startswith("image/")
    ):
        raise HTTPException(
            status_code=400,
            detail="The uploaded file must be an image.",
        )

    image_bytes = await image.read()

    if not image_bytes:
        raise HTTPException(
            status_code=400,
            detail="The uploaded image is empty.",
        )

    try:
        image_data = analyze_item_image(
            image_bytes=image_bytes,
            mime_type=image.content_type,
        )

    except json.JSONDecodeError as exc:
        raise HTTPException(
            status_code=502,
            detail="AI returned invalid image analysis data.",
        ) from exc

    except ValueError as exc:
        raise HTTPException(
            status_code=422,
            detail=str(exc),
        ) from exc

    except RuntimeError as exc:
        raise HTTPException(
            status_code=502,
            detail=str(exc),
        ) from exc

    item_type = normalize_optional_text(
        image_data.get("type")
    )

    description = normalize_optional_text(
        image_data.get("description")
    )

    color = normalize_optional_text(
        image_data.get("color")
    )

    if not item_type and not description:
        raise HTTPException(
            status_code=422,
            detail=(
                "The image does not contain "
                "a clearly identifiable item."
            ),
        )

    lost_item = ItemData(
        report_id=None,
        reporter_id=None,
        location_id=None,
        type=item_type,
        description=description,
        color=color,
        lost_found_date=normalize_datetime(
            lost_found_date
        ),
        image_path=None,
        is_item_with_finder=False,
        pickup_location=None,
        status=None,
        location_name=normalize_optional_text(
            location_name
        ),
    )

    reports = await fetch_mapped_reports()

    found_reports = get_found_reports(
        reports
    )

    if not found_reports:
        return {
            "analyzed_item": lost_item.model_dump(),
            "report_kind": "lost",
            "candidate_kind": "found",
            "candidate_count": 0,
            "matches": [],
            "best_match": None,
            "decision": None,
            "message": (
                "لا توجد بلاغات أغراض موجودة "
                "متاحة للمقارنة حاليًا."
            ),
        }

    serialized_matches = run_matching(
        selected_item=lost_item,
        candidate_items=found_reports,
    )

    best_match = (
        serialized_matches[0]
        if serialized_matches
        else None
    )

    return {
        "analyzed_item": lost_item.model_dump(),
        "report_kind": "lost",
        "candidate_kind": "found",
        "candidate_count": len(found_reports),
        "matches": serialized_matches,
        "best_match": best_match,
        "decision": get_match_decision(
            best_match
        ),
    }


@router.post("/chat-search")
async def chat_search(
    message: str = Form(...),
    location_name: str | None = Form(None),
    lost_found_date: str | None = Form(None),
):
    cleaned_message = normalize_optional_text(
        message
    )

    if not cleaned_message:
        raise HTTPException(
            status_code=400,
            detail="Message is required.",
        )

    try:
        extracted_data = extract_item_from_message(
            cleaned_message
        )

    except json.JSONDecodeError as exc:
        raise HTTPException(
            status_code=502,
            detail="AI returned invalid chat extraction data.",
        ) from exc

    except ValueError as exc:
        raise HTTPException(
            status_code=422,
            detail=str(exc),
        ) from exc

    except RuntimeError as exc:
        raise HTTPException(
            status_code=502,
            detail=str(exc),
        ) from exc

    should_match = (
        extracted_data.get("should_match") is True
    )

    if not should_match:
        return {
            "reply": extracted_data.get("reply"),
            "should_match": False,
            "extracted_item": {
                "type": extracted_data.get("type"),
                "description": extracted_data.get(
                    "description"
                ),
                "color": extracted_data.get("color"),
                "location": extracted_data.get(
                    "location"
                ),
            },
            "candidate_count": 0,
            "matches": [],
            "best_match": None,
            "decision": None,
        }

    item_type = normalize_optional_text(
        extracted_data.get("type")
    )

    description = normalize_optional_text(
        extracted_data.get("description")
    )

    color = normalize_optional_text(
        extracted_data.get("color")
    )

    if not item_type and not description:
        return {
            "reply": (
                extracted_data.get("reply")
                or (
                    "فضلاً اذكري نوع الغرض أو وصفًا "
                    "واضحًا له حتى أتمكن من البحث."
                )
            ),
            "should_match": False,
            "extracted_item": {
                "type": item_type,
                "description": description,
                "color": color,
                "location": None,
            },
            "candidate_count": 0,
            "matches": [],
            "best_match": None,
            "decision": None,
        }

    extracted_location = (
        normalize_optional_text(
            location_name
        )
        or normalize_optional_text(
            extracted_data.get("location")
        )
    )

    lost_item = ItemData(
        report_id=None,
        reporter_id=None,
        location_id=None,
        type=item_type,
        description=description,
        color=color,
        lost_found_date=normalize_datetime(
            lost_found_date
        ),
        image_path=None,
        is_item_with_finder=False,
        pickup_location=None,
        status=None,
        location_name=extracted_location,
    )

    reports = await fetch_mapped_reports()

    found_reports = get_found_reports(
        reports
    )

    if not found_reports:
        return {
            "reply": extracted_data.get("reply"),
            "should_match": True,
            "extracted_item": lost_item.model_dump(),
            "candidate_count": 0,
            "matches": [],
            "best_match": None,
            "decision": None,
            "message": (
                "لا توجد بلاغات أغراض موجودة "
                "متاحة للمقارنة حاليًا."
            ),
        }

    serialized_matches = run_matching(
        selected_item=lost_item,
        candidate_items=found_reports,
    )

    best_match = (
        serialized_matches[0]
        if serialized_matches
        else None
    )

    return {
        "reply": extracted_data.get("reply"),
        "should_match": True,
        "extracted_item": lost_item.model_dump(),
        "candidate_count": len(found_reports),
        "matches": serialized_matches,
        "best_match": best_match,
        "decision": get_match_decision(
            best_match
        ),
    }