namespace LostFound.AI.Configuration
{
    // Bound from configuration section "LostFound:AI:KnowledgeGraph".
    public class KnowledgeGraphOptions
    {
        public string DatabasePath { get; set; } = "AI-Data/knowledge.db";

        public int ConceptCacheMaxEntries { get; set; } = 5000;
    }
}
