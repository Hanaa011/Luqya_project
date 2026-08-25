namespace LostFound.AI.AiService
{
    // Bound from configuration section "LostFound:AiService". Kept separate
    // from AIProviderOptions ("LostFound:AI") deliberately: this is not a
    // classification/embedding provider plugged into AiMatchingService's own
    // pipeline, it's a whole alternate search implementation called from
    // AiSearchAppService.
    public class AiServiceOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:8000";

        public int TimeoutSeconds { get; set; } = 20;
    }
}
