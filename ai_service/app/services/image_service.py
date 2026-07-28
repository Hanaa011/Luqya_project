import base64
import json
import os
from typing import Any, Optional

import openai
from dotenv import load_dotenv
from openai import OpenAI


load_dotenv()

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")

if not OPENAI_API_KEY:
    raise RuntimeError(
        "OPENAI_API_KEY is missing. Add it to the .env file."
    )


VISION_MODEL = "gpt-4.1-mini"

SUPPORTED_MIME_TYPES = {
    "image/jpeg",
    "image/png",
    "image/webp",
    "image/gif",
}

MAX_IMAGE_SIZE_BYTES = 10 * 1024 * 1024


client = OpenAI(
    api_key=OPENAI_API_KEY,
    timeout=60.0,
    max_retries=2,
)


IMAGE_ANALYSIS_INSTRUCTIONS = """
You are an expert visual analysis system for an airport lost-and-found
application.

Analyze the uploaded image and extract information about the main visible item.

Rules:

- Describe only information that is clearly visible.
- Never invent, assume or infer hidden information.
- Use concise English for all extracted values.
- Normalize the item type to lowercase English.
- Focus on the main lost-and-found item.
- Ignore the background and unrelated surrounding objects.
- If no identifiable item is visible, return null for type and color, and
  briefly explain the issue in description.
- Do not identify a person.
- Do not include uncertain brand or model names.
- Include visible brand or model information only when clearly readable.
- Preserve all useful visible identifying characteristics in description.
- Do not return markdown or additional commentary.

Possible normalized item types include:

bag
backpack
handbag
wallet
phone
laptop
tablet
watch
keys
passport
id card
earphones
headphones
glasses
bottle
clothing
jewelry
luggage
charger
camera
book
document
umbrella
other

Description should include clearly visible characteristics such as:

- brand
- model
- material
- shape
- pattern
- approximate size
- logos
- stickers
- scratches
- damage
- accessories
- attached objects
- distinctive marks
- printed text
- visible numbers
- case or cover
- unique appearance

Color rules:

- Return the primary visible color only in color.
- Put secondary colors inside description.
- Use common lowercase English color names.
"""


IMAGE_RESPONSE_FORMAT = {
    "format": {
        "type": "json_schema",
        "name": "lost_item_image_analysis",
        "description": (
            "Structured visual information extracted from a lost-and-found "
            "item image."
        ),
        "strict": True,
        "schema": {
            "type": "object",
            "properties": {
                "type": {
                    "type": ["string", "null"]
                },
                "description": {
                    "type": "string"
                },
                "color": {
                    "type": ["string", "null"]
                }
            },
            "required": [
                "type",
                "description",
                "color"
            ],
            "additionalProperties": False
        }
    }
}


def encode_image(image_bytes: bytes) -> str:
    if not isinstance(image_bytes, bytes):
        raise TypeError(
            "image_bytes must be bytes."
        )

    if not image_bytes:
        raise ValueError(
            "The uploaded image is empty."
        )

    return base64.b64encode(
        image_bytes
    ).decode("utf-8")


def clean_optional_text(
    value: Any
) -> Optional[str]:
    if value is None:
        return None

    if not isinstance(value, str):
        value = str(value)

    cleaned_value = " ".join(
        value.strip().split()
    )

    if not cleaned_value:
        return None

    if cleaned_value.lower() in {
        "null",
        "none",
        "unknown",
        "not visible",
        "not specified",
    }:
        return None

    return cleaned_value


def normalize_item_type(
    value: Any
) -> Optional[str]:
    cleaned_value = clean_optional_text(value)

    if cleaned_value is None:
        return None

    return cleaned_value.lower()


def normalize_color(
    value: Any
) -> Optional[str]:
    cleaned_value = clean_optional_text(value)

    if cleaned_value is None:
        return None

    return cleaned_value.lower()


def validate_image(
    image_bytes: bytes,
    mime_type: str
) -> str:
    if not isinstance(image_bytes, bytes):
        raise TypeError(
            "image_bytes must be bytes."
        )

    if not image_bytes:
        raise ValueError(
            "The uploaded image is empty."
        )

    if len(image_bytes) > MAX_IMAGE_SIZE_BYTES:
        raise ValueError(
            "The uploaded image exceeds the 10 MB size limit."
        )

    if not isinstance(mime_type, str):
        raise TypeError(
            "mime_type must be a string."
        )

    normalized_mime_type = (
        mime_type
        .split(";", maxsplit=1)[0]
        .strip()
        .lower()
    )

    if normalized_mime_type not in SUPPORTED_MIME_TYPES:
        raise ValueError(
            "Unsupported image type. Use JPEG, PNG, WEBP or GIF."
        )

    return normalized_mime_type


def analyze_item_image(
    image_bytes: bytes,
    mime_type: str
) -> dict:
    normalized_mime_type = validate_image(
        image_bytes=image_bytes,
        mime_type=mime_type,
    )

    base64_image = encode_image(
        image_bytes
    )

    image_data_url = (
        f"data:{normalized_mime_type};base64,{base64_image}"
    )

    try:
        response = client.responses.create(
            model=VISION_MODEL,
            instructions=IMAGE_ANALYSIS_INSTRUCTIONS,
            input=[
                {
                    "role": "user",
                    "content": [
                        {
                            "type": "input_text",
                            "text": (
                                "Analyze the main visible lost-and-found "
                                "item in this image."
                            ),
                        },
                        {
                            "type": "input_image",
                            "image_url": image_data_url,
                            "detail": "high",
                        },
                    ],
                }
            ],
            text=IMAGE_RESPONSE_FORMAT,
            max_output_tokens=300,
        )

    except openai.AuthenticationError as exc:
        raise RuntimeError(
            "OpenAI authentication failed. Check OPENAI_API_KEY."
        ) from exc

    except openai.BadRequestError as exc:
        raise ValueError(
            "The image could not be processed by the AI model."
        ) from exc

    except openai.RateLimitError as exc:
        raise RuntimeError(
            "OpenAI rate limit was exceeded. Try again later."
        ) from exc

    except openai.APIConnectionError as exc:
        raise RuntimeError(
            "Could not connect to the OpenAI API."
        ) from exc

    except openai.APIStatusError as exc:
        raise RuntimeError(
            f"OpenAI API returned status code {exc.status_code}."
        ) from exc

    except openai.APIError as exc:
        raise RuntimeError(
            "An unexpected OpenAI API error occurred."
        ) from exc

    text = response.output_text

    if not text or not text.strip():
        raise ValueError(
            "AI returned an empty response."
        )

    try:
        result = json.loads(
            text
        )

    except json.JSONDecodeError as exc:
        raise ValueError(
            "AI returned invalid JSON."
        ) from exc

    if not isinstance(result, dict):
        raise ValueError(
            "AI response must be a JSON object."
        )

    required_fields = {
        "type",
        "description",
        "color",
    }

    missing_fields = required_fields - result.keys()

    if missing_fields:
        raise ValueError(
            f"AI response is missing fields: "
            f"{sorted(missing_fields)}"
        )

    item_type = normalize_item_type(
        result.get("type")
    )

    description = clean_optional_text(
        result.get("description")
    )

    color = normalize_color(
        result.get("color")
    )

    if not description:
        description = (
            "No clear identifying details were visible in the image."
        )

    return {
        "type": item_type,
        "description": description,
        "color": color,
    }