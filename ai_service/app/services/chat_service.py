import json
import os

from dotenv import load_dotenv
from openai import OpenAI

from app.utils import clean_lower, clean_text

load_dotenv()

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")

if not OPENAI_API_KEY:
    raise RuntimeError("OPENAI_API_KEY is missing. Add it to the .env file.")

client = OpenAI(api_key=OPENAI_API_KEY)


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
- Keep the original meaning of the user's description.
- Preserve all useful and distinctive item details.
- Include brand, model, size, material, serial number, stickers, scratches,
  accessories, contents, logos, cases and unique marks inside description.
- If multiple colors are mentioned, include all colors in description and
  place only the primary color in color.
- Location must contain only the place where the item was lost.
- Do not include conversational filler inside extracted fields.

Item type rules:

- The item type is OPEN VOCABULARY.
- Do NOT limit the item to a predefined list.
- Any physical lost item is valid.
- Examples include bag, wallet, phone, laptop, watch, passport, keys,
  earphones, glasses, bottle, mug, cup, pen, notebook, charger, toy,
  perfume, shoes, umbrella, remote control and many others.
- If the item is clear, return a short lowercase English type.
- Examples:
  "محفظة" -> "wallet"
  "مق" -> "mug"
  "كوب قهوة" -> "mug"
  "قلم" -> "pen"
  "دفتر" -> "notebook"
  "عطر" -> "perfume"
- If there is no exact common English category, use a concise lowercase
  English description of the item instead of forcing it into another type.
- Never change an unknown item into the closest predefined category.
- Do not classify a mug as a bottle, a wallet as a bag, or similar
  substitutions just because they are semantically related.

Description rules:

- Description should describe the item itself.
- Keep meaningful details even when type and color are already extracted.
- Preserve useful identifying details from the user's wording.
- Translate the description to concise English when possible.
- Do not remove distinctive details merely because they also appear in
  another field.

Color rules:

- Return the primary color in lowercase English when explicitly mentioned.
- Do not guess a color.
- If no color was provided, return null.

Matching rules:

- Item type is the primary signal. Color and location are secondary details
  that make matching far more accurate, so collect them before searching.
- If the message gives a type but no color and no location, set should_match
  to false and ask for both in one short question.
- If the message gives a type and color but no location, set should_match to
  false and ask for the location only.
- If the message gives a type and location but no color, set should_match to
  false and ask for the color only.
- If the message gives a type with both color and location, set should_match
  to true.
- If the user says in any way that they don't know, don't remember, or
  aren't sure about a missing detail, stop asking and set should_match to
  true using whatever is available. Never insist on a detail the user has
  already said they don't have.
- Set should_match to false for greetings, thanks, unrelated messages, empty
  messages, or messages without any identifiable item.
- If the message continues an earlier exchange (it repeats or adds to a
  previously described item), combine all details mentioned so far into one
  extraction.
- The user may report an item they FOUND instead of lost (e.g. "وجدت",
  "لقيت", "I found", "I have found"). Treat this exactly like a lost-item
  report: apply the same extraction and completeness rules above.

Examples (User -> Output):

"السلام عليكم" -> {"reply": "وعليكم السلام! صف لي الغرض المفقود وسأساعدك في البحث عنه.", "should_match": false, "type": null, "description": null, "color": null, "location": null}
"فقدت محفظة" -> {"reply": "وش لون المحفظة ووين تقريبًا فقدتها؟", "should_match": false, "type": "wallet", "description": null, "color": null, "location": null}
"فقدت محفظة زرقاء صغيرة" -> {"reply": "تمام، وين تقريبًا فقدت المحفظة؟", "should_match": false, "type": "wallet", "description": "small blue wallet", "color": "blue", "location": null}
"فقدت سلسال ذهبي" -> {"reply": "تمام، وين تقريبًا فقدت السلسال؟", "should_match": false, "type": "necklace", "description": "gold necklace", "color": "gold", "location": null}
"فقدت محفظة في جدة بارك" -> {"reply": "وش لون المحفظة؟", "should_match": false, "type": "wallet", "description": null, "color": null, "location": "جدة بارك"}
"فقدت محفظة زرقاء صغيرة في جدة بارك" -> {"reply": "فهمت، سأستخدم وصف المحفظة للبحث عن بلاغات مشابهة.", "should_match": true, "type": "wallet", "description": "small blue wallet", "color": "blue", "location": "جدة بارك"}
"فقدت كوب في جدة بارك بس ما أتذكر لونه بالضبط" -> {"reply": "تمام، بأبحث عن الكوب باستخدام المعلومات المتوفرة.", "should_match": true, "type": "mug", "description": null, "color": null, "location": "جدة بارك"}
"فقدت جوال اسود بس ما اتذكر مكانه" -> {"reply": "تمام، بأبحث عن الجوال باستخدام المعلومات المتوفرة.", "should_match": true, "type": "phone", "description": null, "color": "black", "location": null}
"ضاع مني شيء أزرق" -> {"reply": "ما نوع الغرض الأزرق الذي فقدته؟", "should_match": false, "type": null, "description": "blue item", "color": "blue", "location": null}
"وجدت شنطة ظهر سوداء عند البوابة" -> {"reply": "تمام، سأستخدم وصف الشنطة للبحث عن بلاغات مشابهة.", "should_match": true, "type": "backpack", "description": "black backpack", "color": "black", "location": "البوابة"}
"I found a black wallet near gate 5" -> {"reply": "Got it, I will use this description to search for matching reports.", "should_match": true, "type": "wallet", "description": "black wallet", "color": "black", "location": "gate 5"}
"""


RESPONSE_SCHEMA = {
    "type": "json_schema",
    "json_schema": {
        "name": "lost_item_extraction",
        "strict": True,
        "schema": {
            "type": "object",
            "properties": {
                "reply": {"type": "string"},
                "should_match": {"type": "boolean"},
                "type": {"type": ["string", "null"]},
                "description": {"type": ["string", "null"]},
                "color": {"type": ["string", "null"]},
                "location": {"type": ["string", "null"]},
            },
            "required": ["reply", "should_match", "type", "description", "color", "location"],
            "additionalProperties": False,
        },
    },
}


def extract_item_from_message(message: str) -> dict:
    if not isinstance(message, str) or not message.strip():
        return {
            "reply": "اكتب لي وصف الغرض المفقود، مثل نوعه ولونه ومكان فقده إن أمكن.",
            "should_match": False,
            "type": None,
            "description": None,
            "color": None,
            "location": None,
        }

    try:
        response = client.chat.completions.create(
            model="gpt-4o-mini",
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": message.strip()},
            ],
            response_format=RESPONSE_SCHEMA,
            temperature=0,
        )
    except Exception as exc:
        raise RuntimeError("Unable to communicate with the AI service.") from exc

    content = response.choices[0].message.content

    if not content:
        raise ValueError("AI returned an empty response.")

    try:
        result = json.loads(content)
    except json.JSONDecodeError as exc:
        raise ValueError("AI returned invalid JSON.") from exc

    if not isinstance(result, dict):
        raise ValueError("AI response must be a JSON object.")

    required_fields = {"reply", "should_match", "type", "description", "color", "location"}
    missing_fields = required_fields - result.keys()

    if missing_fields:
        raise ValueError(f"AI response is missing fields: {sorted(missing_fields)}")

    reply = clean_text(result.get("reply"))
    item_type = clean_lower(result.get("type"))
    description = clean_text(result.get("description"))
    color = clean_lower(result.get("color"))
    location = clean_text(result.get("location"))

    should_match = result.get("should_match") is True
    has_meaningful_item_data = bool(item_type or (description and len(description) >= 3))

    if not has_meaningful_item_data:
        should_match = False


    if item_type and color and location:
        should_match = True

    if item_type is None and description and should_match:
        item_type = clean_lower(description)

    if not reply or (should_match and ("؟" in reply or "?" in reply)):
        reply = (
            "فهمت وصف الغرض، وسأستخدم هذه التفاصيل للبحث عن بلاغات مشابهة."
            if should_match
            else "صف لي نوع الغرض المفقود وأبرز تفاصيله ومكان فقده إن أمكن."
        )

    return {
        "reply": reply,
        "should_match": should_match,
        "type": item_type,
        "description": description,
        "color": color,
        "location": location,
    }
