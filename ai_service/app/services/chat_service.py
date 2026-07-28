import json
import os
from typing import Any, Optional

from dotenv import load_dotenv
from openai import OpenAI


load_dotenv()

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")

if not OPENAI_API_KEY:
    raise RuntimeError(
        "OPENAI_API_KEY is missing. Add it to the .env file."
    )

client = OpenAI(
    api_key=OPENAI_API_KEY
)


SYSTEM_PROMPT = """
You are a friendly conversational assistant for an airport lost-and-found
application.

Your responsibilities are:

1. Respond naturally and briefly to the user's message.
2. Extract lost-item information when the message contains useful details.

Your role is limited to airport lost-and-found assistance.

Conversation rules:

- Always provide a natural and helpful reply.
- Reply in the same language used by the user.
- If the user writes Arabic, reply in Arabic.
- If the user writes English, reply in English.
- Keep the reply short and suitable for a chat application.
- For unrelated questions, politely explain your role and guide the user
  back to describing a lost item.
- Do not claim that an item was found.
- Do not claim that matching was completed.
- Do not claim certainty that a matching report exists.
- Matching is performed by another service after this response.

Extraction rules:

- Do not invent information.
- Return null for any information that was not mentioned.
- Standardize extracted item values to English.
- Keep the original meaning.
- Normalize the item type to lowercase English.
- Preserve all useful and distinctive details inside description.
- Include brand, model, size, material, serial number, stickers, scratches,
  accessories, contents, logos, cases and unique marks inside description.
- If multiple colors are mentioned, include all colors in description and
  place only the primary color in color.
- Location must contain only the place where the item was lost.
- Do not include conversational filler inside extracted fields.

Possible normalized item types include:

bag
backpack
handbag
wallet
phone
laptop
tablet
watch
passport
id card
keys
earphones
glasses
jewelry
bottle
clothing
luggage

Matching rules:

- Set should_match to true when the message contains a recognizable item type
  or a meaningful item description.
- Set should_match to false for greetings, thanks, unrelated messages,
  empty messages or messages without useful item information.
- A location or color alone is not enough to start matching.
- At minimum, a recognizable item type or meaningful item description
  must exist.
- If the item information is too vague, ask one useful clarification question.
"""


RESPONSE_SCHEMA = {
    "type": "json_schema",
    "json_schema": {
        "name": "lost_item_extraction",
        "strict": True,
        "schema": {
            "type": "object",
            "properties": {
                "reply": {
                    "type": "string"
                },
                "should_match": {
                    "type": "boolean"
                },
                "type": {
                    "type": [
                        "string",
                        "null"
                    ]
                },
                "description": {
                    "type": [
                        "string",
                        "null"
                    ]
                },
                "color": {
                    "type": [
                        "string",
                        "null"
                    ]
                },
                "location": {
                    "type": [
                        "string",
                        "null"
                    ]
                }
            },
            "required": [
                "reply",
                "should_match",
                "type",
                "description",
                "color",
                "location"
            ],
            "additionalProperties": False
        }
    }
}


def _clean_optional_text(value: Any) -> Optional[str]:
    if value is None:
        return None

    if not isinstance(value, str):
        value = str(value)

    cleaned_value = value.strip()

    if not cleaned_value:
        return None

    if cleaned_value.lower() in {
        "null",
        "none",
        "unknown",
        "not specified",
        "غير معروف",
        "غير محدد"
    }:
        return None

    return cleaned_value


def _normalize_item_type(value: Any) -> Optional[str]:
    cleaned_value = _clean_optional_text(value)

    if cleaned_value is None:
        return None

    return cleaned_value.lower()


def extract_item_from_message(message: str) -> dict:
    if not isinstance(message, str) or not message.strip():
        return {
            "reply": (
                "اكتب لي وصف الغرض المفقود، مثل نوعه ولونه "
                "ومكان فقده إن أمكن."
            ),
            "should_match": False,
            "type": None,
            "description": None,
            "color": None,
            "location": None
        }

    cleaned_message = message.strip()

    try:
        response = client.chat.completions.create(
            model="gpt-4o-mini",
            messages=[
                {
                    "role": "system",
                    "content": SYSTEM_PROMPT
                },
                {
                    "role": "user",
                    "content": cleaned_message
                }
            ],
            response_format=RESPONSE_SCHEMA,
            temperature=0
        )

    except Exception as exc:
        raise RuntimeError(
            "Unable to communicate with the AI service."
        ) from exc

    content = response.choices[0].message.content

    if not content:
        raise ValueError(
            "AI returned an empty response."
        )

    try:
        result = json.loads(content)

    except json.JSONDecodeError as exc:
        raise ValueError(
            "AI returned invalid JSON."
        ) from exc

    if not isinstance(result, dict):
        raise ValueError(
            "AI response must be a JSON object."
        )

    required_fields = {
        "reply",
        "should_match",
        "type",
        "description",
        "color",
        "location"
    }

    missing_fields = required_fields - result.keys()

    if missing_fields:
        raise ValueError(
            f"AI response is missing fields: {sorted(missing_fields)}"
        )

    reply = _clean_optional_text(
        result.get("reply")
    )

    item_type = _normalize_item_type(
        result.get("type")
    )

    description = _clean_optional_text(
        result.get("description")
    )

    color = _clean_optional_text(
        result.get("color")
    )

    location = _clean_optional_text(
        result.get("location")
    )

    should_match_value = result.get("should_match")

    if not isinstance(should_match_value, bool):
        should_match = False
    else:
        should_match = should_match_value

    has_meaningful_item_data = bool(
        item_type or description
    )

    if not has_meaningful_item_data:
        should_match = False

    if not reply:
        if should_match:
            reply = (
                "فهمت وصف الغرض، وسأستخدم هذه التفاصيل "
                "للبحث عن بلاغات مشابهة."
            )
        else:
            reply = (
                "صف لي نوع الغرض المفقود وأبرز تفاصيله "
                "ومكان فقده إن أمكن."
            )

    return {
        "reply": reply,
        "should_match": should_match,
        "type": item_type,
        "description": description,
        "color": color,
        "location": location
    }