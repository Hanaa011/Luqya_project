namespace LostFound.AI
{
    // Bound from configuration section "LostFound:AI".
    public class AIProviderOptions
    {
        // Default = Gemini: free tier, hosted, supports both text embedding
        // and vision (image captioning + classification) out of the box.
        public string Provider { get; set; } = "Gemini";

        public GeminiOptions Gemini { get; set; } = new();
        public OllamaOptions Ollama { get; set; } = new();
        public HuggingFaceOptions HuggingFace { get; set; } = new();
        public OpenAIOptions OpenAI { get; set; } = new();
    }

    public class GeminiOptions
    {
        public string ApiKey { get; set; } = string.Empty;

        public string? EmbeddingModel { get; set; }
        public string? TextModel { get; set; }
    }

    public class OllamaOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "nomic-embed-text";

        // Vision-capable model for image captioning/classification, e.g.
        // `ollama pull llava`.
        public string VisionModel { get; set; } = "llava";
    }

    public class HuggingFaceOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "sentence-transformers/all-MiniLM-L6-v2";
        public string ImageCaptionModel { get; set; } = "nlpconnect/vit-gpt2-image-captioning";
    }

    public class OpenAIOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "text-embedding-3-small";
    }
}
