import os
from typing import Any

import httpx
from dotenv import load_dotenv

from app.models.schemas import ItemData
from app.utils import clean_id, clean_text

load_dotenv()

BACKEND_API_URL = os.getenv("BACKEND_API_URL")
BACKEND_API_TOKEN = os.getenv("BACKEND_API_TOKEN")

DEFAULT_MAX_RESULT_COUNT = 100
MAX_ALLOWED_RESULT_COUNT = 1000
REQUEST_TIMEOUT = 30.0


def get_backend_base_url() -> str:
    if not BACKEND_API_URL:
        raise RuntimeError("BACKEND_API_URL is missing from the .env file.")

    return BACKEND_API_URL.rstrip("/")


def get_headers() -> dict[str, str]:
    headers = {"Accept": "application/json"}

    if BACKEND_API_TOKEN:
        headers["Authorization"] = f"Bearer {BACKEND_API_TOKEN}"

    return headers


def normalize_boolean(value: Any) -> bool | None:
    if isinstance(value, bool):
        return value

    if isinstance(value, int):
        return True if value == 1 else False if value == 0 else None

    if value is None:
        return None

    normalized_value = str(value).strip().lower()

    if normalized_value in {"true", "1", "yes", "found"}:
        return True

    if normalized_value in {"false", "0", "no", "lost"}:
        return False

    return None


def extract_reports_list(response_data: Any) -> list[dict]:
    if isinstance(response_data, list):
        return [report for report in response_data if isinstance(report, dict)]

    if not isinstance(response_data, dict):
        raise ValueError("Backend reports response must be a list or object.")

    for key in ("items", "data", "results", "reports"):
        reports = response_data.get(key)

        if isinstance(reports, list):
            return [report for report in reports if isinstance(report, dict)]

    raise ValueError("Could not find reports list in Backend response.")


async def get_reports(
    report_type: int | None = None,
    status: int | None = None,
    max_result_count: int = DEFAULT_MAX_RESULT_COUNT,
    skip_count: int = 0,
) -> list[dict]:
    if max_result_count <= 0:
        raise ValueError("max_result_count must be greater than zero.")

    if max_result_count > MAX_ALLOWED_RESULT_COUNT:
        raise ValueError(f"max_result_count cannot exceed {MAX_ALLOWED_RESULT_COUNT}.")

    if skip_count < 0:
        raise ValueError("skip_count cannot be negative.")

    params: dict[str, int] = {"MaxResultCount": max_result_count, "SkipCount": skip_count}

    if report_type is not None:
        params["Type"] = report_type

    if status is not None:
        params["Status"] = status

    url = f"{get_backend_base_url()}/api/app/report"

    timeout = httpx.Timeout(connect=10.0, read=REQUEST_TIMEOUT, write=REQUEST_TIMEOUT, pool=10.0)

    async with httpx.AsyncClient(timeout=timeout, follow_redirects=True) as client:
        response = await client.get(url, params=params, headers=get_headers())
        response.raise_for_status()

        try:
            response_data = response.json()
        except ValueError as exc:
            raise ValueError("Backend API returned invalid JSON.") from exc

    return extract_reports_list(response_data)


async def get_all_reports(
    report_type: int | None = None,
    status: int | None = None,
    page_size: int = 100,
    max_pages: int = 20,
) -> list[dict]:
    if page_size <= 0:
        raise ValueError("page_size must be greater than zero.")

    if max_pages <= 0:
        raise ValueError("max_pages must be greater than zero.")

    all_reports: list[dict] = []
    skip_count = 0

    for _ in range(max_pages):
        reports = await get_reports(
            report_type=report_type,
            status=status,
            max_result_count=page_size,
            skip_count=skip_count,
        )
        all_reports.extend(reports)

        if len(reports) < page_size:
            break

        skip_count += page_size

    return all_reports


def pick(report: dict, *keys: str) -> Any:
    for key in keys:
        value = report.get(key)

        if value:
            return value

    return None


def map_report_to_item(report: dict) -> ItemData:
    if not isinstance(report, dict):
        raise TypeError("Backend report must be an object.")

    return ItemData(
        report_id=clean_id(pick(report, "id", "reportId", "report_id")),
        reporter_id=clean_id(pick(report, "reporterId", "reporter_id")),
        location_id=clean_id(pick(report, "locationId", "location_id")),
        type=clean_text(pick(report, "aiObjectType", "objectType", "itemType", "typeName", "type")),
        description=clean_text(report.get("description")),
        color=clean_text(report.get("color")),
        lost_found_date=pick(report, "lostFoundDate", "lost_found_date"),
        image_path=clean_text(pick(report, "imagePath", "image_path")),
        is_item_with_finder=normalize_boolean(
            report.get("isItemWithFinder") if "isItemWithFinder" in report else report.get("is_item_with_finder")
        ),
        pickup_location=clean_text(pick(report, "pickupLocation", "pickup_location")),
        status=clean_text(pick(report, "statusName", "status")),
        location_name=clean_text(pick(report, "locationDetails", "locationName", "location_name")),
    )


async def get_mapped_reports(
    report_type: int | None = None,
    status: int | None = None,
    page_size: int = 100,
) -> list[ItemData]:
    reports = await get_all_reports(report_type=report_type, status=status, page_size=page_size)

    mapped_reports: list[ItemData] = []

    for report in reports:
        try:
            item = map_report_to_item(report)
        except (TypeError, ValueError):
            continue

        if item.report_id:
            mapped_reports.append(item)

    return mapped_reports
