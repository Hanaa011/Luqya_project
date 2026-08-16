namespace LostFound.AI.Configuration
{
    // Bound from "LostFound:AI:HybridPipeline". Enabled defaults to true -
    // the Phase 2B query/retrieval/ranking pipeline is the live default for
    // text-only search (confirmed live via PHASE-VALIDATION-05); the legacy
    // deterministic scoring in AiMatchingService is still used for
    // image-based search and as the fallback if this is explicitly set to
    // false. Fully reversible via one config value, no redeploy needed
    // either direction.
    public class HybridPipelineOptions
    {
        public bool Enabled { get; set; } = false;

        // IHybridSearchEngine.SearchAsync's maxResults truncates the fused
        // candidate set BEFORE ranking; requesting only the caller's final
        // maxResults would starve the ranking engine of anything to
        // actually rerank. Overfetch, then let SemanticSearchOrchestrator
        // truncate to maxResults only after ranking.
        public double RetrievalOverfetchMultiplier { get; set; } = 3.0;
        public int MinRetrievalCandidates { get; set; } = 20;

        // PHASE-VALIDATION-06: applied by RankingEngine directly against the
        // RAW ranking score, BEFORE candidates are sorted and truncated to
        // maxResults - unlike the earlier design (see git history), these no
        // longer merely adjust the number shown to the user after the order
        // was already committed. An UnrelatedCluster candidate (e.g. "Car
        // Keys" ranked against a "Laptop" query) is now pushed toward the
        // bottom of the actual result order, not just displayed with a lower
        // percentage while still occupying a top slot. Values raised from
        // the previous 10/25 per the validation doc's "much stronger
        // penalties... completely unrelated object classes should almost
        // never appear among top candidates".
        public double RelatedClusterConfidencePenalty { get; set; } = 15.0;
        public double UnrelatedClusterConfidencePenalty { get; set; } = 40.0;

        // PHASE-VALIDATION-06: additional metadata-mismatch penalties,
        // applied only when the query explicitly recognized an entity of
        // that type (via IEntityRecognizer) AND the candidate report has a
        // conflicting value for the same field - absence of data on either
        // side is treated as "unknown", never as a mismatch, so a report
        // with no recorded brand/color is not penalized just for being
        // sparse. See RankingEngine.ComputeMetadataPenalty.
        public double CategoryMismatchPenalty { get; set; } = 6.0;
        public double BrandMismatchPenalty { get; set; } = 6.0;
        public double ColorMismatchPenalty { get; set; } = 5.0;
    }
}
