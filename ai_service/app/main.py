from fastapi import FastAPI

from app.api.routes import router as ai_router


app = FastAPI(
    title="Luqya AI Service",
    description="AI service for lost and found item matching using OpenAI and semantic search.",
    version="1.0.0",
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_url="/openapi.json"
)


app.include_router(ai_router)


@app.get("/")
def root():
    return {
        "service": "Luqya AI Service",
        "version": app.version,
        "status": "running",
        "docs": "/docs"
    }


@app.get("/health")
def health_check():
    return {
        "status": "healthy"
    }