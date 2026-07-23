import json
import os
from dotenv import load_dotenv
from openai import OpenAI


load_dotenv()

client = OpenAI(
    api_key=os.getenv("OPENAI_API_KEY")
)


def extract_item_from_message(message: str) -> dict:

    prompt = f"""
You are an AI assistant for a lost and found system.

Extract information about the lost item from the user's message.

Return ONLY valid JSON using exactly these fields:

{{
    "type": null,
    "description": null,
    "color": null,
    "location": null
}}

Rules:
- Translate standardized values to English when possible.
- Keep all important item details from the user's message in description.
- Extract the location only if explicitly mentioned.
- If information is not mentioned, return null.
- Do not invent information.

User message:
{message}
"""

    response = client.chat.completions.create(
        model="gpt-4o-mini",
        messages=[
            {
                "role": "user",
                "content": prompt
            }
        ],
        response_format={
            "type": "json_object"
        }
    )

    content = response.choices[0].message.content

    if not content:
        raise ValueError(
            "AI returned an empty response."
        )

    return json.loads(content)