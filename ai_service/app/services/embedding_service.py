import os

from dotenv import load_dotenv
from openai import OpenAI


load_dotenv()

client = OpenAI(
    api_key=os.getenv("OPENAI_API_KEY")
)


def prepare_text(text: str) -> str:
    return " ".join(
        str(text).strip().split()
    )


def get_embeddings(
    texts: list[str]
) -> list[list[float]]:

    if not texts:
        return []

    cleaned_texts = [
        prepare_text(text)
        for text in texts
    ]

    response = client.embeddings.create(
        model="text-embedding-3-small",
        input=cleaned_texts
    )

    if len(response.data) != len(cleaned_texts):
        raise ValueError(
            "Embedding response size mismatch."
        )

    return [
        embedding.embedding
        for embedding in response.data
    ]