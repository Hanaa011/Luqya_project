import os
from typing import Optional

import openai
from dotenv import load_dotenv
from openai import OpenAI


load_dotenv()

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")

if not OPENAI_API_KEY:
    raise RuntimeError(
        "OPENAI_API_KEY is missing. Add it to the .env file."
    )


EMBEDDING_MODEL = "text-embedding-3-small"
MAX_BATCH_SIZE = 100


client = OpenAI(
    api_key=OPENAI_API_KEY,
    timeout=30.0,
    max_retries=2,
)


def prepare_text(text: Optional[str]) -> str:
    if text is None:
        return ""

    if not isinstance(text, str):
        text = str(text)

    return " ".join(
        text.strip().split()
    )


def get_embeddings(
    texts: list[str],
) -> list[list[float]]:
    if not isinstance(texts, list):
        raise TypeError(
            "texts must be a list of strings."
        )

    if not texts:
        return []

    if len(texts) > MAX_BATCH_SIZE:
        raise ValueError(
            f"A maximum of {MAX_BATCH_SIZE} texts "
            "can be embedded in one request."
        )

    cleaned_texts = [
        prepare_text(text)
        for text in texts
    ]

    empty_indexes = [
        index
        for index, text in enumerate(cleaned_texts)
        if not text
    ]

    if empty_indexes:
        raise ValueError(
            "Embedding input contains empty text at indexes: "
            f"{empty_indexes}"
        )

    try:
        response = client.embeddings.create(
            model=EMBEDDING_MODEL,
            input=cleaned_texts,
            encoding_format="float",
        )

    except openai.AuthenticationError as exc:
        raise RuntimeError(
            "OpenAI authentication failed. Check OPENAI_API_KEY."
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

    if len(response.data) != len(cleaned_texts):
        raise ValueError(
            "Embedding response size does not match input size."
        )

    sorted_embeddings = sorted(
        response.data,
        key=lambda item: item.index,
    )

    embeddings = [
        item.embedding
        for item in sorted_embeddings
    ]

    if any(not embedding for embedding in embeddings):
        raise ValueError(
            "OpenAI returned one or more empty embeddings."
        )

    return embeddings