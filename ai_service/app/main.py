from fastapi import FastAPI

from app.api.routes import router as ai_router


app = FastAPI(
    title="Luqya AI Service",
    description="AI service for lost and found item matching",
    version="1.0.0",
)


app.include_router(ai_router)


@app.get("/")
def root():
    return {
        "service": "Luqya AI Service",
        "status": "running"
    }


@app.get("/health")
def health_check():
    return {
        "status": "healthy"
    }