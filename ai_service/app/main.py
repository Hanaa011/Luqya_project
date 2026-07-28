import os

from dotenv import load_dotenv
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api.routes import router as ai_router


load_dotenv()


def get_allowed_origins() -> list[str]:
    origins_value = os.getenv(
        "ALLOWED_ORIGINS",
        "http://localhost:3000,http://localhost:5173",
    )

    return [
        origin.strip()
        for origin in origins_value.split(",")
        if origin.strip()
    ]


app = FastAPI(
    title="Luqya AI Service",
    description=(
        "AI service for airport lost-and-found item matching "
        "using image analysis, semantic search, and OpenAI."
    ),
    version="1.0.0",
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_url="/openapi.json",
)


app.add_middleware(
    CORSMiddleware,
    allow_origins=get_allowed_origins(),
    allow_credentials=True,
    allow_methods=[
        "GET",
        "POST",
        "OPTIONS",
    ],
    allow_headers=["*"],
)


app.include_router(
    ai_router
)


@app.get(
    "/",
    tags=["Service"],
)
async def root():
    return {
        "service": app.title,
        "version": app.version,
        "status": "running",
        "docs": app.docs_url,
        "health": "/health",
    }


@app.get(
    "/health",
    tags=["Service"],
)
async def health_check():
    return {
        "status": "healthy",
        "service": app.title,
        "version": app.version,
    }