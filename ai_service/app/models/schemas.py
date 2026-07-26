from datetime import datetime
from typing import List, Optional
from pydantic import BaseModel


class ItemData(BaseModel):
    report_id: Optional[int] = None
    reporter_id: Optional[int] = None
    location_id: Optional[int] = None
    type: str
    description: str
    color: Optional[str] = None
    lost_found_date: Optional[datetime] = None
    image_path: Optional[str] = None
    is_item_with_finder: Optional[bool] = None
    pickup_location: Optional[str] = None
    status: Optional[str] = None
    location_name: Optional[str] = None


class MatchRequest(BaseModel):
    lost_item: ItemData
    found_items: List[ItemData]


class MatchResult(BaseModel):
    lost_report_id: Optional[int] = None
    found_report_id: int
    similarity_score: float
    match_reason: str
    status: str


class MatchResponse(BaseModel):
    matches: List[MatchResult]