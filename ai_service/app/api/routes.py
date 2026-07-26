import json

from fastapi import APIRouter, File, Form, HTTPException, UploadFile

from app.models.schemas import ItemData, MatchRequest, MatchResponse
from app.services.chat_service import extract_item_from_message
from app.services.image_service import analyze_item_image
from app.services.matching_service import classify_match, find_matches


router = APIRouter(
    prefix="/api/ai",
    tags=["AI Matching"]
)


@router.post("/match-image")
async def match_image(
    image: UploadFile = File(...),
    found_items_json: str = Form(...)
):
    if not image.content_type or not image.content_type.startswith("image/"):
        raise HTTPException(
            status_code=400,
            detail="The uploaded file must be an image."
        )

    image_bytes = await image.read()

    if not image_bytes:
        raise HTTPException(
            status_code=400,
            detail="The uploaded image is empty."
        )

    try:
        image_data = analyze_item_image(
            image_bytes=image_bytes,
            mime_type=image.content_type
        )
    except (ValueError, json.JSONDecodeError) as exc:
        raise HTTPException(
            status_code=502,
            detail="Unable to analyze the uploaded image."
        ) from exc

    lost_item = ItemData(
        type=image_data.get("type") or "unknown",
        description=image_data.get("description") or "",
        color=image_data.get("color")
    )

    try:
        found_items_data = json.loads(found_items_json)
    except json.JSONDecodeError as exc:
        raise HTTPException(
            status_code=400,
            detail="found_items_json must contain valid JSON."
        ) from exc

    if not isinstance(found_items_data, list):
        raise HTTPException(
            status_code=400,
            detail="found_items_json must contain a JSON array."
        )

    try:
        found_items = [
            ItemData(**item)
            for item in found_items_data
        ]
    except (TypeError, ValueError) as exc:
        raise HTTPException(
            status_code=422,
            detail="One or more found items contain invalid data."
        ) from exc

    try:
        matches = find_matches(
            lost_item=lost_item,
            found_items=found_items
        )
    except Exception as exc:
        raise HTTPException(
            status_code=502,
            detail="Unable to calculate item matches."
        ) from exc

    best_match = matches[0] if matches else None

    decision = None

    if best_match:
        decision = classify_match(
            best_match.similarity_score
        )

    return {
        "analyzed_item": lost_item.model_dump(),
        "matches": matches,
        "best_match": best_match,
        "decision": decision
    }


@router.post(
    "/match",
    response_model=MatchResponse
)
def match_items(request: MatchRequest):
    try:
        matches = find_matches(
            lost_item=request.lost_item,
            found_items=request.found_items
        )
    except Exception as exc:
        raise HTTPException(
            status_code=502,
            detail="Unable to calculate item matches."
        ) from exc

    return MatchResponse(
        matches=matches
    )


@router.post("/chat-search")
async def chat_search(request: dict):
    message = request.get("message")
    found_items_data = request.get("found_items", [])

    if not isinstance(message, str) or not message.strip():
        raise HTTPException(
            status_code=400,
            detail="Message is required."
        )

    if not isinstance(found_items_data, list):
        raise HTTPException(
            status_code=400,
            detail="found_items must contain a JSON array."
        )

    try:
        extracted_data = extract_item_from_message(
            message.strip()
        )
    except (ValueError, json.JSONDecodeError) as exc:
        raise HTTPException(
            status_code=502,
            detail="Unable to extract item information from the message."
        ) from exc

    lost_item = ItemData(
        type=extracted_data.get("type") or "unknown",
        description=extracted_data.get("description") or "",
        color=extracted_data.get("color"),
        location_name=extracted_data.get("location")
    )

    try:
        found_items = [
            ItemData(**item)
            for item in found_items_data
        ]
    except (TypeError, ValueError) as exc:
        raise HTTPException(
            status_code=422,
            detail="One or more found items contain invalid data."
        ) from exc

    try:
        matches = find_matches(
            lost_item=lost_item,
            found_items=found_items
        )
    except Exception as exc:
        raise HTTPException(
            status_code=502,
            detail="Unable to calculate item matches."
        ) from exc

    best_match = matches[0] if matches else None

    decision = None

    if best_match:
        decision = classify_match(
            best_match.similarity_score
        )

    return {
        "message": message,
        "extracted_item": lost_item.model_dump(),
        "matches": matches,
        "best_match": best_match,
        "decision": decision
    }