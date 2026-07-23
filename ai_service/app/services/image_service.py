import base64
import json
import os

from dotenv import load_dotenv
from openai import OpenAI


load_dotenv()

client = OpenAI(
    api_key=os.getenv("OPENAI_API_KEY")
)


def encode_image(image_bytes: bytes) -> str:
    return base64.b64encode(image_bytes).decode("utf-8")


def analyze_item_image(
    image_bytes: bytes,
    mime_type: str
) -> dict:

    base64_image = encode_image(image_bytes)

    response = client.responses.create(
        model="gpt-4.1-mini",
        input=[
            {
                "role": "user",
                "content": [
                    {
                        "type": "input_text",
                        "text": """
Analyze this lost-and-found item image.

Return ONLY valid JSON with these fields:

{
  "type": "",
  "description": "",
  "color": ""
}

Rules:
- Describe only visible characteristics.
- Include any visible brand or distinguishing details inside the description.
- Do not invent information.
- Use concise English values.
"""
                    },
                    {
                        "type": "input_image",
                        "image_url": (
                            f"data:{mime_type};base64,{base64_image}"
                        )
                    }
                ]
            }
        ]
    )

    text = response.output_text.strip()

    return json.loads(text)