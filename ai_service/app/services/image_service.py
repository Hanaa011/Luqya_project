import base64
import json
import os

import openai
from dotenv import load_dotenv
from openai import OpenAI

from app.utils import clean_lower, clean_text

load_dotenv()

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")

if not OPENAI_API_KEY:
    raise RuntimeError("OPENAI_API_KEY is missing. Add it to the .env file.")

VISION_MODEL = "gpt-4.1-mini"

SUPPORTED_MIME_TYPES = {"image/jpeg", "image/png", "image/webp", "image/gif"}
MAX_IMAGE_SIZE_BYTES = 10 * 1024 * 1024

client = OpenAI(api_key=OPENAI_API_KEY, timeout=60.0, max_retries=2)


IMAGE_ANALYSIS_INSTRUCTIONS = """
You are an expert visual analysis system for an airport lost-and-found
application.

Analyze the uploaded image and extract structured information about the
main visible physical item.

General rules:

- Describe only information that is clearly visible.
- Never invent, assume or infer hidden information.
- Focus on the main lost-and-found item.
- Ignore background objects and unrelated surroundings.
- Do not identify or describe people.
- Use concise English for extracted structured values.
- Preserve useful visible identifying characteristics.
- Do not return markdown or commentary outside the required JSON.

Item type rules:

- The item type is OPEN VOCABULARY.
- Do NOT restrict the item to a predefined list.
- Any clearly identifiable physical object is valid.
- Examples include:
  bag, backpack, handbag, wallet, phone, laptop, tablet, watch,
  passport, id card, keys, earphones, headphones, glasses,
  bottle, mug, cup, thermos, clothing, jewelry, luggage,
  charger, cable, power bank, camera, book, notebook, document,
  umbrella, pen, toy, perfume, shoes, hat, remote control,
  mouse, keyboard, case and many others.
- If the object is clear, return a short lowercase English type.
- Use the most specific common object name that is visually supported.
- Do not force an unknown object into the closest predefined category.
- For example:
  mug must remain mug, not bottle.
  wallet must remain wallet, not bag.
  notebook must remain notebook, not book, when clearly identifiable.
- If there is no common exact category, return a concise lowercase English
  object name based on what is visibly identifiable.
- If the object cannot be confidently identified, return null for type.

Description rules:

- Description must describe the visible item itself.
- Include every clearly visible identifying characteristic that could help
  matching.
- Useful details may include:
  brand
  model
  material
  shape
  pattern
  approximate size
  logos
  stickers
  scratches
  damage
  accessories
  attached objects
  distinctive marks
  printed text
  visible numbers
  case or cover
  handles
  straps
  closures
  surface texture
  unique appearance
- Include brand or model only when clearly readable or visually certain.
- Do not guess hidden contents.
- Do not guess ownership or usage.
- Keep the description concise but sufficiently detailed.

Color rules:

- Return the primary visible color only in the color field.
- Use a common lowercase English color name.
- Put secondary colors inside description.
- Do not guess a color if lighting makes it uncertain.
- If no reliable primary color is visible, return null.

Unclear image rules:

- If no clear object can be identified:
  type must be null.
  color may be null.
  description should briefly explain that no clear identifiable item
  was visible.
"""


IMAGE_RESPONSE_FORMAT = {
    "format": {
        "type": "json_schema",
        "name": "lost_item_image_analysis",
        "description": (
            "Structured visual information extracted from a lost-and-found item image."
        ),
        "strict": True,
        "schema": {
            "type": "object",
            "properties": {
                "type": {"type": ["string", "null"]},
                "description": {"type": "string"},
                "color": {"type": ["string", "null"]},
            },
            "required": ["type", "description", "color"],
            "additionalProperties": False,
        },
    }
}


def validate_image(image_bytes: bytes, mime_type: str) -> str:
    if not isinstance(image_bytes, bytes):
        raise TypeError("image_bytes must be bytes.")

    if not image_bytes:
        raise ValueError("The uploaded image is empty.")

    if len(image_bytes) > MAX_IMAGE_SIZE_BYTES:
        raise ValueError("The uploaded image exceeds the 10 MB size limit.")

    if not isinstance(mime_type, str):
        raise TypeError("mime_type must be a string.")

    normalized_mime_type = mime_type.split(";", maxsplit=1)[0].strip().lower()

    if normalized_mime_type not in SUPPORTED_MIME_TYPES:
        raise ValueError("Unsupported image type. Use JPEG, PNG, WEBP or GIF.")

    return normalized_mime_type


def analyze_item_image(image_bytes: bytes, mime_type: str) -> dict:
    normalized_mime_type = validate_image(image_bytes=image_bytes, mime_type=mime_type)
    image_data_url = f"data:{normalized_mime_type};base64,{base64.b64encode(image_bytes).decode('utf-8')}"

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
                                "Identify and describe the main physical lost-and-found item "
                                "visible in this image. Use the exact common object category "
                                "when possible and do not force it into a predefined list."
                            ),
                        },
                        {"type": "input_image", "image_url": image_data_url, "detail": "high"},
                    ],
                }
            ],
            text=IMAGE_RESPONSE_FORMAT,
            max_output_tokens=350,
        )
    except openai.AuthenticationError as exc:
        raise RuntimeError("OpenAI authentication failed. Check OPENAI_API_KEY.") from exc
    except openai.BadRequestError as exc:
        raise ValueError("The image could not be processed by the AI model.") from exc
    except openai.RateLimitError as exc:
        raise RuntimeError("OpenAI rate limit was exceeded. Try again later.") from exc
    except openai.APIConnectionError as exc:
        raise RuntimeError("Could not connect to the OpenAI API.") from exc
    except openai.APIStatusError as exc:
        raise RuntimeError(f"OpenAI API returned status code {exc.status_code}.") from exc
    except openai.APIError as exc:
        raise RuntimeError("An unexpected OpenAI API error occurred.") from exc

    text = response.output_text

    if not text or not text.strip():
        raise ValueError("AI returned an empty response.")

    try:
        result = json.loads(text)
    except json.JSONDecodeError as exc:
        raise ValueError("AI returned invalid JSON.") from exc

    if not isinstance(result, dict):
        raise ValueError("AI response must be a JSON object.")

    required_fields = {"type", "description", "color"}
    missing_fields = required_fields - result.keys()

    if missing_fields:
        raise ValueError(f"AI response is missing fields: {sorted(missing_fields)}")

    item_type = clean_lower(result.get("type"))
    description = clean_text(result.get("description"))
    color = clean_lower(result.get("color"))

    if not description:
        description = (
            f"Visible {item_type} with no additional clear identifying details."
            if item_type
            else "No clear identifiable lost-and-found item was visible in the image."
        )

    return {"type": item_type, "description": description, "color": color}
