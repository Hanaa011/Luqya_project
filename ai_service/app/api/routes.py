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


def get_reports_by_kind(reports: list[ItemData], is_item_with_finder: bool) -> list[ItemData]:
    return [
        report
        for report in reports
        if report.report_id is not None and report.is_item_with_finder is is_item_with_finder
    ]


def parse_report_kind(report_kind: str) -> bool:
    """True = the searcher FOUND an item (candidates are lost reports),
    False = the searcher LOST an item (candidates are found reports)."""
    if report_kind not in ("lost", "found"):
        raise HTTPException(status_code=400, detail="report_kind must be 'lost' or 'found'.")

    return report_kind == "found"


def build_known_context(
    context_type: str | None,
    context_description: str | None,
    context_color: str | None,
    context_location: str | None,
    context_report_kind: str | None = None,
    context_item_name_local: str | None = None,
) -> dict | None:
    known_context = {
        "type": clean_text(context_type),
        "description": clean_text(context_description),
        "color": clean_text(context_color),
        "location": clean_text(context_location),
        # Not text to clean - either "lost", "found", or absent. Carries a
        # confirmed direction (revealed by an earlier message such as
        # "وجدت قلادة حمراء") forward across turns that don't restate it
        # (e.g. a later "في المول" reply) - see chat_search's fallback.
        "report_kind": context_report_kind if context_report_kind in ("lost", "found") else None,
        # The exact original-language item word extracted on an earlier
        # turn (e.g. "الشماغ") - carried forward VERBATIM (plain Python
        # fallback in chat_service, not re-derived by the model) so a later
        # turn's search never loses it to a re-guessed synonym. Measured:
        # asking the model to reconstruct this from just the known English
        # type on a bare follow-up ("في قاعة نوف") is not reliable at
        # temperature=0 - it produced "الشماغ"/"الشال"/"الوشاح" across
        # otherwise-identical calls, which swung a real match's score
        # between ~30 and ~90 depending on which synonym happened to come
        # back. Verbatim passthrough removes that variance entirely.
        "item_name_local": clean_text(context_item_name_local),
    }
    return known_context if any(known_context.values()) else None


def merge_known_context(
    item_type: str | None,
    description: str | None,
    color: str | None,
    location: str | None,
    known_context: dict | None,
) -> tuple[str | None, str | None, str | None, str | None]:
    if not known_context:
        return item_type, description, color, location

    # Fallback only, never concatenated - matches chat_service.py's own
    # merge so a description never grows across turns either way.
    return (
        item_type or known_context.get("type"),
        description or known_context.get("description"),
        color or known_context.get("color"),
        location or known_context.get("location"),
    )


def run_image_analysis(image: UploadFile, image_bytes: bytes) -> dict:
    try:
        return analyze_item_image(image_bytes=image_bytes, mime_type=image.content_type)
    except json.JSONDecodeError as exc:
        raise HTTPException(status_code=502, detail="AI returned invalid image analysis data.") from exc
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc


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


async def run_matching(selected_item: ItemData, candidate_items: list[ItemData]) -> list[dict]:
    try:
        matches = await find_matches(lost_item=selected_item, found_items=candidate_items)
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=502, detail="Unable to calculate item matches.") from exc

    return [serialize_match_result(match) for match in matches]


async def build_search_response(
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

    matches = await run_matching(selected_item=selected_item, candidate_items=candidates)
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

    return await build_search_response(
        selected_item=selected_report,
        candidates=candidate_reports,
        empty_message="لا توجد بلاغات من النوع المقابل متاحة للمقارنة حاليًا.",
        extra_fields={
            "report": selected_report.model_dump(),
            "report_kind": report_kind,
            "candidate_kind": candidate_kind,
        },
    )


@router.post("/analyze-image")
async def analyze_image(image: UploadFile = File(...)):
    if not image.content_type or not image.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="The uploaded file must be an image.")

    image_bytes = await image.read()

    if not image_bytes:
        raise HTTPException(status_code=400, detail="The uploaded image is empty.")

    image_data = run_image_analysis(image, image_bytes)

    return {
        "type": clean_text(image_data.get("type")),
        "description": clean_text(image_data.get("description")),
        "color": clean_text(image_data.get("color")),
    }


@router.post("/match-image")
async def match_image(
    image: UploadFile = File(...),
    message: str | None = Form(None),
    location_name: str | None = Form(None),
    lost_found_date: str | None = Form(None),
    report_kind: str = Form("lost"),
    context_type: str | None = Form(None),
    context_description: str | None = Form(None),
    context_color: str | None = Form(None),
    context_location: str | None = Form(None),
    context_report_kind: str | None = Form(None),
):
    is_finder = parse_report_kind(report_kind)

    if not image.content_type or not image.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="The uploaded file must be an image.")

    image_bytes = await image.read()

    if not image_bytes:
        raise HTTPException(status_code=400, detail="The uploaded image is empty.")

    image_data = run_image_analysis(image, image_bytes)

    item_type = clean_text(image_data.get("type"))
    description = clean_text(image_data.get("description"))
    color = clean_text(image_data.get("color"))

    message_text = clean_text(message)
    if message_text:
        description = f"{description}. {message_text}" if description else message_text

    known_context = build_known_context(
        context_type, context_description, context_color, context_location, context_report_kind
    )
    item_type, description, color, location_name = merge_known_context(
        item_type, description, color, clean_text(location_name), known_context
    )

    # Image search has no text to detect intent from itself - if an earlier
    # turn in this conversation already confirmed a direction (e.g. "وجدت
    # قلادة" followed by an attached photo), keep using it instead of
    # falling back to the caller-supplied pill.
    known_report_kind = (known_context or {}).get("report_kind")
    if known_report_kind:
        is_finder = parse_report_kind(known_report_kind)
    resolved_report_kind = "found" if is_finder else "lost"

    extracted_item = {
        "type": item_type,
        "description": description,
        "color": color,
        "location": location_name,
    }

    if not item_type and not description:
        return {
            "reply": "لم أتمكن من تمييز الغرض بوضوح في الصورة. هل يمكنك وصفه بكلمات؟",
            "should_match": False,
            "extracted_item": extracted_item,
            "follow_up_prompt": None,
            "report_kind": resolved_report_kind,
            "candidate_count": 0,
            "matches": [],
            "best_match": None,
            "decision": None,
        }

    query_item = ItemData(
        type=item_type,
        description=description,
        color=color,
        lost_found_date=normalize_datetime(lost_found_date),
        is_item_with_finder=is_finder,
        location_name=location_name,
    )

    candidates = get_reports_by_kind(await fetch_mapped_reports(), not is_finder)

    # Location never gates matching (Task: image search UX) - it only makes
    # the results more precise, so it's offered as an optional refinement
    # alongside whatever results already came back, never instead of them.
    follow_up_prompt = (
        None
        if location_name
        else "تم العثور على نتائج بناءً على الصورة. هل يمكنك إضافة الموقع لتحسين النتائج؟"
    )

    return await build_search_response(
        selected_item=query_item,
        candidates=candidates,
        empty_message=(
            "لا توجد بلاغات أغراض مفقودة متاحة للمقارنة حاليًا."
            if is_finder
            else "لا توجد بلاغات أغراض موجودة متاحة للمقارنة حاليًا."
        ),
        extra_fields={
            "reply": None,
            "should_match": True,
            "extracted_item": extracted_item,
            "follow_up_prompt": follow_up_prompt,
            "analyzed_item": query_item.model_dump(),
            "report_kind": resolved_report_kind,
            "candidate_kind": "lost" if is_finder else "found",
        },
    )


@router.post("/chat-search")
async def chat_search(
    message: str = Form(...),
    location_name: str | None = Form(None),
    lost_found_date: str | None = Form(None),
    report_kind: str = Form("lost"),
    context_type: str | None = Form(None),
    context_description: str | None = Form(None),
    context_color: str | None = Form(None),
    context_location: str | None = Form(None),
    context_report_kind: str | None = Form(None),
    context_item_name_local: str | None = Form(None),
):
    is_finder = parse_report_kind(report_kind)
    cleaned_message = clean_text(message)

    if not cleaned_message:
        raise HTTPException(status_code=400, detail="Message is required.")

    known_context = build_known_context(
        context_type, context_description, context_color, context_location,
        context_report_kind, context_item_name_local,
    )

    try:
        extracted_data = await extract_item_from_message(cleaned_message, known_context)
    except json.JSONDecodeError as exc:
        raise HTTPException(status_code=502, detail="AI returned invalid chat extraction data.") from exc
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc

    # The pill/caller-supplied direction (report_kind form field, already
    # parsed into is_finder above) stays authoritative UNLESS natural-
    # language intent confidently says otherwise - extract_item_from_message
    # already resolves this fully (this message's own verb, falling back to
    # a role already confirmed earlier in the conversation via known_context
    # - see its own report_kind merge), so this is single-sourced from its
    # result, not re-derived here.
    detected_report_kind = extracted_data.get("report_kind")
    if detected_report_kind == "found":
        is_finder = True
    elif detected_report_kind == "lost":
        is_finder = False

    # Echoed back by the frontend as the next turn's context_report_kind /
    # context_item_name_local respectively - see build_known_context.
    resolved_report_kind = detected_report_kind
    resolved_item_name_local = extracted_data.get("item_name_local")

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
            "report_kind": resolved_report_kind,
            "item_name_local": resolved_item_name_local,
            "follow_up_prompt": None,
            "candidate_count": 0,
            "matches": [],
            "best_match": None,
            "decision": None,
        }

    item_type = clean_text(extracted_data.get("type"))
    description = clean_text(extracted_data.get("description"))
    color = clean_text(extracted_data.get("color"))
    location = clean_text(location_name) or clean_text(extracted_data.get("location"))

    if not item_type and not description:
        return {
            "reply": extracted_data.get("reply") or "فضلاً اذكري نوع الغرض أو وصفًا واضحًا له حتى أتمكن من البحث.",
            "should_match": False,
            "extracted_item": {"type": item_type, "description": description, "color": color, "location": location},
            "report_kind": resolved_report_kind,
            "item_name_local": resolved_item_name_local,
            "follow_up_prompt": None,
            "candidate_count": 0,
            "matches": [],
            "best_match": None,
            "decision": None,
        }

    query_item = ItemData(
        type=item_type,
        description=description,
        color=color,
        lost_found_date=normalize_datetime(lost_found_date),
        is_item_with_finder=is_finder,
        location_name=location,
        native_name=resolved_item_name_local,
    )

    candidates = get_reports_by_kind(await fetch_mapped_reports(), not is_finder)

    return await build_search_response(
        selected_item=query_item,
        candidates=candidates,
        empty_message=(
            "لا توجد بلاغات أغراض مفقودة متاحة للمقارنة حاليًا."
            if is_finder
            else "لا توجد بلاغات أغراض موجودة متاحة للمقارنة حاليًا."
        ),
        extra_fields={
            "reply": extracted_data.get("reply"),
            "should_match": True,
            "extracted_item": {"type": item_type, "description": description, "color": color, "location": location},
            "report_kind": resolved_report_kind,
            "item_name_local": resolved_item_name_local,
            "follow_up_prompt": None,
        },
    )
