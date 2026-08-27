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

        // Was 20s, paired with a 3-attempt retry - measured real searches
        // (LLM extraction + report-list fetch, sometimes + vision analysis)
        // legitimately taking 20-30s+, so that combination meant a normal,
        // successful search could get aborted mid-flight and retried,
        // compounding into the reported "close to a minute" cases. Now a
        // single attempt (see AiServiceClient's chat-search/match-image
        // calls), sized to comfortably cover realistic processing time
        // instead of needing a retry to finish at all.
        public int TimeoutSeconds { get; set; } = 45;
    }
}
