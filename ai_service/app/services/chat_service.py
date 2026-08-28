import asyncio
import json
import os
import time

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

1. Extract lost/found-item information when the message contains useful
   details.
2. For messages that are NOT about a lost/found item (greetings, thanks,
   unrelated questions), give a short, natural reply in the user's own
   language explaining your role and inviting them to describe an item.

Your role is limited to airport lost-and-found assistance. For everything
ELSE (a message that identifies an item, however partially), your `reply`
field is not shown to the user as-is - the application builds the actual
follow-up question itself from the structured fields you extract, in a
fixed, dependable wording. Still fill `reply` with a reasonable natural
sentence (used as a fallback), but the fields below matter far more than
its exact wording for anything but a genuine greeting/unrelated message.

Extraction rules:

- Do not invent information.
- Return null for any information that was not mentioned.
- Keep the original meaning of the user's description.
- Preserve all useful and distinctive item details.
- Include brand, model, size, material, serial number, stickers, scratches,
  accessories, contents, logos, cases and unique marks inside description.
- If multiple colors are mentioned, include all colors in description and
  place only the primary color in color.
- Location must contain only the place where the item was lost/found.
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

Item name (local language) rules:

- item_name_local is a SHORT, natural noun phrase naming the item, in the
  SAME language the user is writing in (not translated) - e.g. "الكتاب",
  "القلادة", "the wallet", "چیز". It is inserted into a follow-up question
  the application builds itself (e.g. "Where did you lose ___?"), so it
  must read naturally in that slot, with a natural definite article/phrasing
  for that language (Arabic: include "ال"; English: include "the").
- Reconstruct it from the FULL known item so far (this message plus any
  "[Known so far: ...]" line), not only from words in this exact message -
  e.g. if the known type is "book" and this message is just "الملز", still
  return "الكتاب" (from the known type, translated into this message's own
  language), not null.
- If no item has been identified yet at all (this message and any known
  context), return null.

Language rules:

- language is which of "ar" (Arabic), "en" (English), or "ur" (Urdu) this
  message is written in. Judge it from the CURRENT message's own script/
  words, even if it is short (e.g. a single place name).

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
- Set should_match to false for greetings, thanks, unrelated messages,
  empty messages, or messages without any identifiable item.
- If the message continues an earlier exchange (it repeats or adds to a
  previously described item), combine all details mentioned so far into one
  extraction - use the "[Known so far: ...]" line the same way described
  above for item_name_local.
- The user may report an item they FOUND instead of lost (e.g. "وجدت",
  "لقيت", "I found", "I have found"). Treat this exactly like a lost-item
  report: apply the same extraction and completeness rules above.

Report kind rules:

- report_kind identifies whether the person WRITING this message is
  reporting something they LOST or something they FOUND - this is about
  the writer's own role, separate from the item's type/color/location.
- Set report_kind to "found" when the message clearly says the writer
  found/has an item that isn't theirs - Arabic verbs like "وجدت", "لقيت",
  "عثرت على", or English "I found", "I have found", "I've found", "I got",
  meaning they picked something up. Urdu equivalents (e.g. "مجھے ملا",
  "میں نے پایا") also mean found.
- Set report_kind to "lost" when the message clearly says the writer lost
  their own item - Arabic "فقدت", "ضاع مني", "ضيعت", or English "I lost",
  "I've lost", "I'm missing", "I can't find my". Urdu equivalents (e.g.
  "میں نے کھو دیا", "گم ہو گیا") also mean lost.
- If a "[Known so far: ..., report_kind=lost|found]" line is present, and
  THIS message gives no new verb of its own, carry that known role forward
  unchanged rather than guessing.
- If the message gives no verb clearly indicating either role, and no known
  role was carried forward either, set report_kind to null. Do not guess -
  null means "unclear", not "assume lost". Never default an unclear message
  to "lost".

Examples (User -> Output):

"السلام عليكم" -> {"reply": "وعليكم السلام! صف لي الغرض المفقود وسأساعدك في البحث عنه.", "should_match": false, "type": null, "description": null, "color": null, "location": null, "report_kind": null, "item_name_local": null, "language": "ar"}
"فقدت محفظة" -> {"reply": "وش لون المحفظة ووين فقدتها؟", "should_match": false, "type": "wallet", "description": null, "color": null, "location": null, "report_kind": "lost", "item_name_local": "المحفظة", "language": "ar"}
"فقدت كتاب أبيض" -> {"reply": "تمام، وين فقدت الكتاب؟", "should_match": false, "type": "book", "description": "white book", "color": "white", "location": null, "report_kind": "lost", "item_name_local": "الكتاب", "language": "ar"}
"فقدت محفظة في جدة بارك" -> {"reply": "وش لون المحفظة؟", "should_match": false, "type": "wallet", "description": null, "color": null, "location": "جدة بارك", "report_kind": "lost", "item_name_local": "المحفظة", "language": "ar"}
"[Known so far: type=book, color=white, description=white book, report_kind=lost]\\nالملز" -> {"reply": "تمام، سأبحث باستخدام هذه المعلومات.", "should_match": true, "type": "book", "description": "white book", "color": "white", "location": "الملز", "report_kind": "lost", "item_name_local": "الكتاب", "language": "ar"}
"وجدت قلادة حمراء" -> {"reply": "تمام، وين وجدت القلادة؟", "should_match": false, "type": "necklace", "description": "red necklace", "color": "red", "location": null, "report_kind": "found", "item_name_local": "القلادة", "language": "ar"}
"[Known so far: type=necklace, color=red, description=red necklace, report_kind=found]\\nفي المول" -> {"reply": "تمام، سأبحث باستخدام هذه المعلومات.", "should_match": true, "type": "necklace", "description": "red necklace", "color": "red", "location": "المول", "report_kind": "found", "item_name_local": "القلادة", "language": "ar"}
"فقدت كوب في جدة بارك بس ما أتذكر لونه بالضبط" -> {"reply": "تمام، بأبحث عن الكوب باستخدام المعلومات المتوفرة.", "should_match": true, "type": "mug", "description": null, "color": null, "location": "جدة بارك", "report_kind": "lost", "item_name_local": "الكوب", "language": "ar"}
"ضاع مني شيء أزرق" -> {"reply": "ما نوع الغرض الأزرق الذي فقدته؟", "should_match": false, "type": null, "description": "blue item", "color": "blue", "location": null, "report_kind": "lost", "item_name_local": null, "language": "ar"}
"لقيت شنطة سوداء" -> {"reply": "تمام، وين لقيت الشنطة؟", "should_match": false, "type": "backpack", "description": "black backpack", "color": "black", "location": null, "report_kind": "found", "item_name_local": "الشنطة", "language": "ar"}
"I found a black wallet near gate 5" -> {"reply": "Got it, I will use this description to search for matching reports.", "should_match": true, "type": "wallet", "description": "black wallet", "color": "black", "location": "gate 5", "report_kind": "found", "item_name_local": "the wallet", "language": "en"}
"I lost my white book" -> {"reply": "Got it, where did you lose the book?", "should_match": false, "type": "book", "description": "white book", "color": "white", "location": null, "report_kind": "lost", "item_name_local": "the book", "language": "en"}
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
                "report_kind": {"type": ["string", "null"], "enum": ["lost", "found", None]},
                "item_name_local": {"type": ["string", "null"]},
                "language": {"type": "string", "enum": ["ar", "en", "ur"]},
            },
            "required": [
                "reply", "should_match", "type", "description", "color", "location",
                "report_kind", "item_name_local", "language",
            ],
            "additionalProperties": False,
        },
    },
}


# Deterministic follow-up wording - see build_conversational_reply. Kept
# entirely separate from the LLM: given the same (language, report_kind,
# missing-field) inputs, the question is always worded the same dependable
# way, regardless of which item it's about - no per-item special-casing,
# and no risk of the malformed/mismatched-verb phrasing a free-form LLM
# reply could produce.
_SEARCHING_MESSAGES = {
    "ar": "تمام، سأستخدم هذه التفاصيل للبحث عن بلاغات مشابهة.",
    "en": "Got it, I'll use these details to search for matching reports.",
    "ur": "ٹھیک ہے، میں انہی تفصیلات سے ملتی جلتی رپورٹس تلاش کروں گا۔",
}

_ITEM_FALLBACK = {"ar": "الغرض", "en": "it", "ur": "چیز"}

_MISSING_FIELD_TEMPLATES = {
    "ar": {
        ("lost", "location"): "وين فقدت {item}؟",
        ("found", "location"): "وين وجدت {item}؟",
        ("lost", "color"): "وش لون {item}؟",
        ("found", "color"): "وش لون {item}؟",
        ("lost", "both"): "وش لون {item}، ووين فقدت {item}؟",
        ("found", "both"): "وش لون {item}، ووين وجدت {item}؟",
    },
    "en": {
        ("lost", "location"): "Where did you lose {item}?",
        ("found", "location"): "Where did you find {item}?",
        ("lost", "color"): "What color is {item}?",
        ("found", "color"): "What color is {item}?",
        ("lost", "both"): "What color is {item}, and where did you lose it?",
        ("found", "both"): "What color is {item}, and where did you find it?",
    },
    "ur": {
        ("lost", "location"): "آپ نے {item} کہاں کھویا؟",
        ("found", "location"): "آپ کو {item} کہاں ملا؟",
        ("lost", "color"): "{item} کا رنگ کیا ہے؟",
        ("found", "color"): "{item} کا رنگ کیا ہے؟",
        ("lost", "both"): "{item} کا رنگ کیا ہے، اور آپ نے {item} کہاں کھویا؟",
        ("found", "both"): "{item} کا رنگ کیا ہے، اور آپ کو {item} کہاں ملا؟",
    },
}


def build_conversational_reply(
    *,
    should_match: bool,
    language: str,
    report_kind: str | None,
    item_name_local: str | None,
    color: str | None,
    location: str | None,
    llm_reply: str | None,
) -> str:
    """The single source of truth for what the user actually sees on a
    reply-only turn. should_match=True -> a fixed "searching now" message.
    should_match=False with a missing color/location -> a fixed question
    for exactly the missing field(s), built from the resolved report_kind
    (never re-derived from free text, so it can never mismatch the actual
    search direction) and item_name_local (never a hardcoded item name).
    Only a genuine no-item-identified turn (greeting/unrelated) falls back
    to the model's own natural-language reply.
    """
    templates = _MISSING_FIELD_TEMPLATES.get(language, _MISSING_FIELD_TEMPLATES["en"])
    searching = _SEARCHING_MESSAGES.get(language, _SEARCHING_MESSAGES["en"])

    if should_match:
        return searching

    if not report_kind and not item_name_local:
        # Nothing identified yet at all - a genuine greeting/unrelated/
        # too-vague-to-say-anything-structured turn. The model's own
        # natural reply is appropriate here; it never has a direction/verb
        # to get wrong since there's no item context to phrase around.
        return llm_reply or searching

    item = item_name_local or _ITEM_FALLBACK.get(language, _ITEM_FALLBACK["en"])
    verb_key = report_kind or "lost"
    missing = "both" if not color and not location else "location" if not location else "color"

    return templates.get((verb_key, missing), templates[("lost", missing)]).format(item=item)


def _known_context_prefix(known_context: dict | None) -> str:
    if not known_context:
        return ""

    parts = [
        f"{key}={value}"
        for key, value in (
            ("type", known_context.get("type")),
            ("color", known_context.get("color")),
            ("location", known_context.get("location")),
            ("description", known_context.get("description")),
            ("report_kind", known_context.get("report_kind")),
        )
        if value
    ]

    if not parts:
        return ""

    return "[Known so far: " + ", ".join(parts) + "]\n"


# Single-flight + short TTL cache, same rationale and pattern as
# report_api_service.get_mapped_reports: when the type filter is unset, one
# user message fires two ai_service calls (one per direction) with the
# IDENTICAL message/known_context - extraction is direction-independent
# (temperature=0, same input -> same output), so the second call should
# reuse the first's in-flight/just-finished LLM call instead of paying for
# a second, redundant one. Priority #1 from the latency investigation.
_EXTRACTION_CACHE_TTL_SECONDS = 5.0
_extraction_cache: dict[tuple, tuple[float, dict]] = {}
_extraction_inflight: dict[tuple, asyncio.Task] = {}


def _extraction_cache_key(message: str, known_context: dict | None) -> tuple:
    context_items = tuple(sorted((known_context or {}).items()))
    return (message.strip(), context_items)


async def extract_item_from_message(message: str, known_context: dict | None = None) -> dict:
    if not isinstance(message, str) or not message.strip():
        return {
            "reply": "اكتب لي وصف الغرض المفقود، مثل نوعه ولونه ومكان فقده إن أمكن.",
            "should_match": False,
            "type": None,
            "description": None,
            "color": None,
            "location": None,
            "report_kind": None,
            "item_name_local": None,
        }

    cache_key = _extraction_cache_key(message, known_context)

    cached = _extraction_cache.get(cache_key)
    if cached is not None:
        cached_at, cached_result = cached
        if time.monotonic() - cached_at < _EXTRACTION_CACHE_TTL_SECONDS:
            return cached_result

    inflight = _extraction_inflight.get(cache_key)
    if inflight is not None and not inflight.done():
        return await asyncio.shield(inflight)

    task = asyncio.ensure_future(_extract_item_from_message_uncached(message, known_context))
    _extraction_inflight[cache_key] = task

    try:
        result = await asyncio.shield(task)
    finally:
        if _extraction_inflight.get(cache_key) is task:
            del _extraction_inflight[cache_key]

    _extraction_cache[cache_key] = (time.monotonic(), result)

    return result


async def _extract_item_from_message_uncached(message: str, known_context: dict | None) -> dict:
    user_content = _known_context_prefix(known_context) + message.strip()

    try:
        response = await asyncio.to_thread(
            client.chat.completions.create,
            model="gpt-4o-mini",
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": user_content},
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

    required_fields = {
        "reply", "should_match", "type", "description", "color", "location",
        "report_kind", "item_name_local", "language",
    }
    missing_fields = required_fields - result.keys()

    if missing_fields:
        raise ValueError(f"AI response is missing fields: {sorted(missing_fields)}")

    llm_reply = clean_text(result.get("reply"))
    item_type = clean_lower(result.get("type"))
    description = clean_text(result.get("description"))
    color = clean_lower(result.get("color"))
    location = clean_text(result.get("location"))
    item_name_local = clean_text(result.get("item_name_local"))
    language = result.get("language") if result.get("language") in ("ar", "en", "ur") else "ar"
    # Schema-constrained to exactly "lost"/"found"/null - never guessed when
    # the message doesn't clearly indicate the writer's own role, and never
    # defaulted to "lost" (see Report kind rules in SYSTEM_PROMPT).
    report_kind = result.get("report_kind") if result.get("report_kind") in ("lost", "found") else None

    if known_context:
        # Fallback only - never concatenated. A field already extracted this
        # turn always wins; the known value only fills a gap this turn left
        # empty, so description stays a single concise current value instead
        # of accumulating every prior turn's wording. report_kind gets the
        # exact same treatment: once a turn reveals the writer's role, it
        # must survive every later turn that doesn't restate it (e.g. a
        # bare "في المول") - this is what previously let a later turn
        # silently fall back to a mismatched caller-supplied direction.
        item_type = item_type or clean_lower(known_context.get("type"))
        description = description or clean_text(known_context.get("description"))
        color = color or clean_lower(known_context.get("color"))
        location = location or clean_text(known_context.get("location"))
        report_kind = report_kind or (
            known_context.get("report_kind") if known_context.get("report_kind") in ("lost", "found") else None
        )
        # Deliberately the OPPOSITE priority from the fields above: the
        # model is asked to re-derive item_name_local every turn (needed so
        # a first-turn message that DOES name the item gets a value at
        # all), but on a later turn that doesn't restate the item, that
        # re-derivation is a genuine RE-GUESS, not new information - and it
        # is not reliably the same word twice (see build_known_context's
        # comment: "الشماغ"/"الشال"/"الوشاح" observed for identical
        # follow-ups). Once a turn has already locked in a value, prefer
        # that verbatim original over any later turn's re-guess.
        known_native_name = clean_text(known_context.get("item_name_local"))
        if known_native_name:
            item_name_local = known_native_name

    should_match = result.get("should_match") is True
    has_meaningful_item_data = bool(item_type or (description and len(description) >= 3))

    if not has_meaningful_item_data:
        should_match = False

    if item_type and color and location:
        should_match = True

    if item_type is None and description and should_match:
        item_type = clean_lower(description)

    # The user-facing text is built deterministically from the resolved
    # fields above, not trusted from the model's own free-form wording -
    # see build_conversational_reply's docstring for why (wrong verb on
    # later turns, occasionally malformed combined questions, sometimes
    # re-asking for an already-known field).
    reply = build_conversational_reply(
        should_match=should_match,
        language=language,
        report_kind=report_kind,
        item_name_local=item_name_local,
        color=color,
        location=location,
        llm_reply=llm_reply,
    )

    return {
        "reply": reply,
        "should_match": should_match,
        "type": item_type,
        "description": description,
        "color": color,
        "location": location,
        "report_kind": report_kind,
        # Original-language item name (e.g. "شماغ") - see ItemData.native_name
        # and matching_service.semantic_text for why this matters for search
        # quality, not just reply wording.
        "item_name_local": item_name_local,
    }
