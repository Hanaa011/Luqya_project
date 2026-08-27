from datetime import datetime

from pydantic import BaseModel, Field


class ItemData(BaseModel):
    report_id: str | None = None
    reporter_id: str | None = None
    location_id: str | None = None

    type: str | None = None
    description: str | None = None
    color: str | None = None
    # Item name in the ORIGINAL language the searcher wrote in (e.g. "شماغ"),
    # kept separate from the (often English-translated) description - see
    # matching_service.semantic_text. Query-side only; candidates already
    # carry their original-language wording inside their own description
    # when relevant, so this is never populated for a candidate ItemData.
    native_name: str | None = None

    lost_found_date: datetime | None = None
    image_path: str | None = None
    is_item_with_finder: bool | None = None
    pickup_location: str | None = None
    status: str | None = None
    location_name: str | None = None


class MatchRequest(BaseModel):
    lost_item: ItemData
    found_items: list[ItemData] = Field(default_factory=list)


class MatchResult(BaseModel):
    lost_report_id: str | None = None
    found_report_id: str | None = None
    similarity_score: float = Field(ge=0, le=100)
    match_reason: str
    status: str


class MatchResponse(BaseModel):
    matches: list[MatchResult] = Field(default_factory=list)
