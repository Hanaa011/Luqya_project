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
You are an expert AI system for airport lost and found.

Analyze the uploaded image and extract information about the visible item.

Return ONLY valid JSON.

Schema:

{
  "type": "",
  "description": "",
  "color": ""
}

Rules:

- Describe only information that is clearly visible.
- Never guess or infer hidden details.
- Use concise English.
- Standardize the item type.

Possible item types include:

Bag
Backpack
Handbag
Wallet
Phone
Laptop
Tablet
Watch
Keys
Passport
ID Card
Earphones
Glasses
Bottle
Clothing
Jewelry
Luggage

Description must include every visible identifying characteristic such as:

- Brand
- Model
- Material
- Shape
- Pattern
- Size
- Logos
- Stickers
- Scratches
- Damage
- Accessories
- Attached objects
- Distinctive marks
- Printed text
- Visible numbers
- Unique appearance

For color:

- Return only the primary color.
- If multiple colors exist, keep secondary colors inside description.

Examples:

Image:
Black leather handbag with a gold chain.

Output:

{
  "type":"bag",
  "description":"black leather handbag with gold chain",
  "color":"black"
}

Image:
Silver HP laptop with a cracked top cover and university sticker.

Output:

{
  "type":"laptop",
  "description":"silver HP laptop with cracked top cover and university sticker",
  "color":"silver"
}

Image:
Blue Adidas backpack with white stripes.

Output:

{
  "type":"bag",
  "description":"blue Adidas backpack with white stripes",
  "color":"blue"
}
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

    if not text:
        raise ValueError(
            "AI returned an empty response."
        )

    return json.loads(text)