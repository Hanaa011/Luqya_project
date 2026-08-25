import json
from datetime import datetime
from typing import Any

import httpx
from fastapi import APIRouter, File, Form, HTTPException, UploadFile

from app.models.schemas import ItemData
from app.services.chat_service import extract_item_from_message
from app.services.image_service import analyze_item_image
from app.services.matching_service import classify_match, find_matches
from app.services.report_api_service import get_mapped_reports
from app.utils import clean_id, clean_text

router = APIRouter(prefix="/api/ai", tags=["AI Matching"])


def normalize_datetime(value: Any) -> datetime | None:
    if value is None:
        return None

    if isinstance(value, datetime):
        return value

    text = str(value).strip()

    if not text:
        return None

    try:
        return datetime.fromisoformat(text.replace("Z", "+00:00"))
    except ValueError as exc:
        raise HTTPException(
            status_code=422,
            detail="Invalid date format. Use ISO format, for example: 2026-07-28.",
        ) from exc


async def fetch_mapped_reports() -> list[ItemData]:
    try:
        return await get_mapped_reports(page_size=100)
    except httpx.HTTPStatusError as exc:
        raise HTTPException(
            status_code=502,
            detail=f"Backend API returned an unsuccessful response: {exc.response.text}",
        ) from exc
    except httpx.TimeoutException as exc:
        raise HTTPException(status_code=504, detail="Backend API request timed out.") from exc
    except httpx.RequestError as exc:
        raise HTTPException(status_code=503, detail="Could not connect to the Backend API.") from exc
    except (RuntimeError, ValueError) as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc


def get_opposite_reports(selected_report: ItemData, reports: list[ItemData]) -> list[ItemData]:
    if selected_report.is_item_with_finder is None:
        raise HTTPException(
            status_code=422,
            detail="The selected report does not specify whether the item is lost or found.",
        )

    selected_report_id = clean_id(selected_report.report_id)

    return [
        report
        for report in reports
        if clean_id(report.report_id)
        and clean_id(report.report_id) != selected_report_id
        and report.is_item_with_finder is not None
        and report.is_item_with_finder != selected_report.is_item_with_finder
    ]


def get_found_reports(reports: list[ItemData]) -> list[ItemData]:
    return [report for report in reports if report.report_id is not None and report.is_item_with_finder is True]


def serialize_match_result(match: Any) -> dict:
    if hasattr(match, "model_dump"):
        return match.model_dump()

    if isinstance(match, dict):
        return match

    raise TypeError("Unsupported match result type.")


def get_match_decision(best_match: dict | None) -> dict | None:
    if not best_match or best_match.get("similarity_score") is None:
        return None

    try:
        score = float(best_match["similarity_score"])
    except (TypeError, ValueError):
        return None

    return classify_match(score)


def run_matching(selected_item: ItemData, candidate_items: list[ItemData]) -> list[dict]:
    try:
        matches = find_matches(lost_item=selected_item, found_items=candidate_items)
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=502, detail="Unable to calculate item matches.") from exc

    return [serialize_match_result(match) for match in matches]


def build_search_response(
    selected_item: ItemData,
    candidates: list[ItemData],
    empty_message: str,
    extra_fields: dict,
) -> dict:
    if not candidates:
        return {
            **extra_fields,
            "candidate_count": 0,
            "matches": [],
            "best_match": None,
            "decision": None,
            "message": empty_message,
        }

    matches = run_matching(selected_item=selected_item, candidate_items=candidates)
    best_match = matches[0] if matches else None

    return {
        **extra_fields,
        "candidate_count": len(candidates),
        "matches": matches,
        "best_match": best_match,
        "decision": get_match_decision(best_match),
    }


@router.get("/health")
async def ai_health():
    return {"status": "ok", "service": "Luqya AI Service"}


@router.get("/test-reports")
async def test_reports():
    reports = await fetch_mapped_reports()

    return {
        "count": len(reports),
        "found_reports_count": sum(1 for r in reports if r.is_item_with_finder is True),
        "lost_reports_count": sum(1 for r in reports if r.is_item_with_finder is False),
        "unknown_reports_count": sum(1 for r in reports if r.is_item_with_finder is None),
        "mapped_reports": [report.model_dump() for report in reports],
    }


@router.post("/match-report/{report_id}")
async def match_report(report_id: str):
    normalized_report_id = clean_id(report_id)

    if not normalized_report_id:
        raise HTTPException(status_code=400, detail="A valid report ID is required.")

    reports = await fetch_mapped_reports()

    selected_report = next(
        (report for report in reports if clean_id(report.report_id) == normalized_report_id),
        None,
    )

    if selected_report is None:
        raise HTTPException(status_code=404, detail="The requested report was not found.")

    candidate_reports = get_opposite_reports(selected_report=selected_report, reports=reports)
    report_kind = "found" if selected_report.is_item_with_finder is True else "lost"
    candidate_kind = "lost" if report_kind == "found" else "found"

    return build_search_response(
        selected_item=selected_report,
        candidates=candidate_reports,
        empty_message="لا توجد بلاغات من النوع المقابل متاحة للمقارنة حاليًا.",
        extra_fields={
            "report": selected_report.model_dump(),
            "report_kind": report_kind,
            "candidate_kind": candidate_kind,
        },
    )


@router.post("/match-image")
async def match_image(
    image: UploadFile = File(...),
    location_name: str | None = Form(None),
    lost_found_date: str | None = Form(None),
):
    if not image.content_type or not image.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="The uploaded file must be an image.")

    image_bytes = await image.read()

    if not image_bytes:
        raise HTTPException(status_code=400, detail="The uploaded image is empty.")

    try:
        image_data = analyze_item_image(image_bytes=image_bytes, mime_type=image.content_type)
    except json.JSONDecodeError as exc:
        raise HTTPException(status_code=502, detail="AI returned invalid image analysis data.") from exc
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc

    item_type = clean_text(image_data.get("type"))
    description = clean_text(image_data.get("description"))
    color = clean_text(image_data.get("color"))

    if not item_type and not description:
        raise HTTPException(status_code=422, detail="The image does not contain a clearly identifiable item.")

    lost_item = ItemData(
        type=item_type,
        description=description,
        color=color,
        lost_found_date=normalize_datetime(lost_found_date),
        is_item_with_finder=False,
        location_name=clean_text(location_name),
    )

    found_reports = get_found_reports(await fetch_mapped_reports())

    return build_search_response(
        selected_item=lost_item,
        candidates=found_reports,
        empty_message="لا توجد بلاغات أغراض موجودة متاحة للمقارنة حاليًا.",
        extra_fields={
            "analyzed_item": lost_item.model_dump(),
            "report_kind": "lost",
            "candidate_kind": "found",
        },
    )


@router.post("/chat-search")
async def chat_search(
    message: str = Form(...),
    location_name: str | None = Form(None),
    lost_found_date: str | None = Form(None),
):
    cleaned_message = clean_text(message)

    if not cleaned_message:
        raise HTTPException(status_code=400, detail="Message is required.")

    try:
        extracted_data = extract_item_from_message(cleaned_message)
    except json.JSONDecodeError as exc:
        raise HTTPException(status_code=502, detail="AI returned invalid chat extraction data.") from exc
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc

    if extracted_data.get("should_match") is not True:
        return {
            "reply": extracted_data.get("reply"),
            "should_match": False,
            "extracted_item": {
                "type": extracted_data.get("type"),
                "description": extracted_data.get("description"),
                "color": extracted_data.get("color"),
                "location": extracted_data.get("location"),
            },
            "candidate_count": 0,
            "matches": [],
            "best_match": None,
            "decision": None,
        }

    item_type = clean_text(extracted_data.get("type"))
    description = clean_text(extracted_data.get("description"))
    color = clean_text(extracted_data.get("color"))

    if not item_type and not description:
        return {
            "reply": extracted_data.get("reply") or "فضلاً اذكري نوع الغرض أو وصفًا واضحًا له حتى أتمكن من البحث.",
            "should_match": False,
            "extracted_item": {"type": item_type, "description": description, "color": color, "location": None},
            "candidate_count": 0,
            "matches": [],
            "best_match": None,
            "decision": None,
        }

    lost_item = ItemData(
        type=item_type,
        description=description,
        color=color,
        lost_found_date=normalize_datetime(lost_found_date),
        is_item_with_finder=False,
        location_name=clean_text(location_name) or clean_text(extracted_data.get("location")),
    )

    found_reports = get_found_reports(await fetch_mapped_reports())

    return build_search_response(
        selected_item=lost_item,
        candidates=found_reports,
        empty_message="لا توجد بلاغات أغراض موجودة متاحة للمقارنة حاليًا.",
        extra_fields={
            "reply": extracted_data.get("reply"),
            "should_match": True,
            "extracted_item": lost_item.model_dump(),
        },
    )
