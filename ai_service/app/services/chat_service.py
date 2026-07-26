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
    You are an expert AI system for airport lost and found matching.

    Your task is to extract structured information from the user's message.

    Return ONLY valid JSON.

    Schema:

    {{
        "type": null,
        "description": null,
        "color": null,
        "location": null
    }}

    Rules:

    - Do NOT invent information.
    - If a field is not explicitly mentioned, return null.
    - Standardize values to English.
    - Keep the original meaning.
    - Preserve every distinctive detail inside description.
    - Include brand, model, size, material, serial numbers, stickers, scratches, accessories, contents, logos and unique marks inside description.
    - If the user describes multiple colors, include them all inside description and put only the primary color in "color".
    - Normalize item types.

    Examples:

    Handbag
    Backpack
    Bag
    Wallet
    Phone
    Laptop
    Tablet
    Watch
    Passport
    ID Card
    Keys
    Earphones
    Glasses
    Jewelry
    Bottle
    Clothing
    Luggage

    Location should only contain the place where the item was lost.

    Good examples:

    Input:
    "I lost my black leather handbag with a gold chain near Gate 14."

    Output:

    {{
        "type":"bag",
        "description":"black leather handbag with gold chain",
        "color":"black",
        "location":"Gate 14"
    }}

    Input:
    "ضاع جوالي ايفون 15 برو اسود عليه كفر شفاف"

    Output:

    {{
        "type":"phone",
        "description":"iPhone 15 Pro with transparent case",
        "color":"black",
        "location":null
    }}

    Input:
    "شنطة فيها لابتوب HP ودفاتر"

    Output:

    {{
        "type":"bag",
        "description":"bag containing HP laptop and notebooks",
        "color":null,
        "location":null
    }}

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