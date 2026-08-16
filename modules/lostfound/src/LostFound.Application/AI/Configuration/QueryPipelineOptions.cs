namespace LostFound.AI.Configuration
{
    // Bound from configuration section "LostFound:AI:QueryPipeline".
    public class QueryPipelineOptions
    {
        public int MaxCacheEntries { get; set; } = 500;
    }
}
