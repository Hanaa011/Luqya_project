import os
from typing import Any

import httpx
from dotenv import load_dotenv

from app.models.schemas import ItemData


load_dotenv()

BACKEND_API_URL = os.getenv("BACKEND_API_URL")
BACKEND_API_TOKEN = os.getenv("BACKEND_API_TOKEN")

DEFAULT_MAX_RESULT_COUNT = 100
MAX_ALLOWED_RESULT_COUNT = 1000
REQUEST_TIMEOUT = 30.0


def get_backend_base_url() -> str:
    if not BACKEND_API_URL:
        raise RuntimeError(
            "BACKEND_API_URL is missing from the .env file."
        )

    return BACKEND_API_URL.rstrip("/")


def get_headers() -> dict[str, str]:
    headers = {
        "Accept": "application/json",
    }

    if BACKEND_API_TOKEN:
        headers["Authorization"] = (
            f"Bearer {BACKEND_API_TOKEN}"
        )

    return headers


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
    }:
        return None

    return normalized_value


def normalize_report_id(
    value: Any,
) -> str | None:
    return normalize_optional_text(value)


def normalize_boolean(
    value: Any,
) -> bool | None:
    if isinstance(value, bool):
        return value

    if value is None:
        return None

    if isinstance(value, int):
        if value == 1:
            return True

        if value == 0:
            return False

    normalized_value = str(value).strip().lower()

    if normalized_value in {
        "true",
        "1",
        "yes",
        "found",
    }:
        return True

    if normalized_value in {
        "false",
        "0",
        "no",
        "lost",
    }:
        return False

    return None


def extract_reports_list(
    response_data: Any,
) -> list[dict]:
    if isinstance(response_data, list):
        return [
            report
            for report in response_data
            if isinstance(report, dict)
        ]

    if not isinstance(response_data, dict):
        raise ValueError(
            "Backend reports response must be a list or object."
        )

    for key in (
        "items",
        "data",
        "results",
        "reports",
    ):
        reports = response_data.get(key)

        if isinstance(reports, list):
            return [
                report
                for report in reports
                if isinstance(report, dict)
            ]

    raise ValueError(
        "Could not find reports list in Backend response."
    )


async def get_reports(
    report_type: int | None = None,
    status: int | None = None,
    max_result_count: int = DEFAULT_MAX_RESULT_COUNT,
    skip_count: int = 0,
) -> list[dict]:
    if max_result_count <= 0:
        raise ValueError(
            "max_result_count must be greater than zero."
        )

    if max_result_count > MAX_ALLOWED_RESULT_COUNT:
        raise ValueError(
            f"max_result_count cannot exceed "
            f"{MAX_ALLOWED_RESULT_COUNT}."
        )

    if skip_count < 0:
        raise ValueError(
            "skip_count cannot be negative."
        )

    params: dict[str, int] = {
        "MaxResultCount": max_result_count,
        "SkipCount": skip_count,
    }

    if report_type is not None:
        params["Type"] = report_type

    if status is not None:
        params["Status"] = status

    url = (
        f"{get_backend_base_url()}"
        "/api/app/report"
    )

    timeout = httpx.Timeout(
        connect=10.0,
        read=REQUEST_TIMEOUT,
        write=REQUEST_TIMEOUT,
        pool=10.0,
    )

    async with httpx.AsyncClient(
        timeout=timeout,
        follow_redirects=True,
    ) as client:
        response = await client.get(
            url,
            params=params,
            headers=get_headers(),
        )

        response.raise_for_status()

        try:
            response_data = response.json()

        except ValueError as exc:
            raise ValueError(
                "Backend API returned invalid JSON."
            ) from exc

    return extract_reports_list(
        response_data
    )


async def get_all_reports(
    report_type: int | None = None,
    status: int | None = None,
    page_size: int = 100,
    max_pages: int = 20,
) -> list[dict]:
    if page_size <= 0:
        raise ValueError(
            "page_size must be greater than zero."
        )

    if max_pages <= 0:
        raise ValueError(
            "max_pages must be greater than zero."
        )

    all_reports: list[dict] = []
    skip_count = 0

    for _ in range(max_pages):
        reports = await get_reports(
            report_type=report_type,
            status=status,
            max_result_count=page_size,
            skip_count=skip_count,
        )

        all_reports.extend(
            reports
        )

        if len(reports) < page_size:
            break

        skip_count += page_size

    return all_reports


def map_report_to_item(
    report: dict,
) -> ItemData:
    if not isinstance(report, dict):
        raise TypeError(
            "Backend report must be an object."
        )

    report_type = (
        report.get("aiObjectType")
        or report.get("objectType")
        or report.get("itemType")
        or report.get("typeName")
    )

    raw_type = report.get("type")

    if not report_type and isinstance(raw_type, str):
        report_type = raw_type

    status = (
        report.get("statusName")
        or report.get("status")
    )

    return ItemData(
        report_id=normalize_report_id(
            report.get("id")
            or report.get("reportId")
            or report.get("report_id")
        ),
        reporter_id=normalize_report_id(
            report.get("reporterId")
            or report.get("reporter_id")
        ),
        location_id=normalize_report_id(
            report.get("locationId")
            or report.get("location_id")
        ),
        type=normalize_optional_text(
            report_type
        ),
        description=normalize_optional_text(
            report.get("description")
        ),
        color=normalize_optional_text(
            report.get("color")
        ),
        lost_found_date=(
            report.get("lostFoundDate")
            or report.get("lost_found_date")
        ),
        image_path=normalize_optional_text(
            report.get("imagePath")
            or report.get("image_path")
        ),
        is_item_with_finder=normalize_boolean(
            (
                report.get("isItemWithFinder")
                if "isItemWithFinder" in report
                else report.get("is_item_with_finder")
            )
        ),
        pickup_location=normalize_optional_text(
            report.get("pickupLocation")
            or report.get("pickup_location")
        ),
        status=normalize_optional_text(
            status
        ),
        location_name=normalize_optional_text(
            report.get("locationDetails")
            or report.get("locationName")
            or report.get("location_name")
        ),
    )


async def get_mapped_reports(
    report_type: int | None = None,
    status: int | None = None,
    page_size: int = 100,
) -> list[ItemData]:
    reports = await get_all_reports(
        report_type=report_type,
        status=status,
        page_size=page_size,
    )

    mapped_reports: list[ItemData] = []

    for report in reports:
        try:
            item = map_report_to_item(
                report
            )

        except (
            TypeError,
            ValueError,
        ):
            continue

        if item.report_id:
            mapped_reports.append(
                item
            )

    return mapped_reports