using System.Collections.Generic;
using LostFound.AI.Retrieval;

namespace LostFound.AI.Configuration
{
    // Bound from configuration section "LostFound:AI:Retrieval". Every
    // retriever can be individually enabled/disabled and weighted without
    // recompilation (spec's "Configuration" requirement) via
    // EnabledStrategies/StrategyWeights, keyed by IRetrievalStrategy.StrategyName.
    public class RetrievalOptions
    {
        public int PerStrategyLimit { get; set; } = 100;

        public FusionMethod FusionMethod { get; set; } = FusionMethod.ReciprocalRankFusion;

        // Matches Phase 1 Part 7's chosen RRF constant (k=60) - see that
        // deliverable for why RRF was chosen over weighted linear fusion by
        // default (BM25 and cosine similarity aren't on a comparable scale).
        public int RrfK { get; set; } = 60;

        // Cosine-similarity percentage (0-100, matching CosineSimilarityCalculator's
        // own scale) above which two distinct reports are treated as
        // semantic duplicates by IDuplicateResolver.
        public double SemanticDuplicateThreshold { get; set; } = 95.0;

        public Dictionary<string, bool> EnabledStrategies { get; set; } = new()
        {
            ["BM25"] = true, ["Vector"] = true, ["MetadataVector"] = true, ["Graph"] = true, ["Fuzzy"] = true, ["Exact"] = true,
            ["Category"] = true, ["Brand"] = true, ["Color"] = true, ["Material"] = true,
            ["Location"] = true, ["Time"] = true
        };

        public Dictionary<string, double> StrategyWeights { get; set; } = new()
        {
            ["BM25"] = 1.0, ["Vector"] = 1.0, ["MetadataVector"] = 0.5, ["Graph"] = 0.8, ["Fuzzy"] = 0.6, ["Exact"] = 1.2,
            ["Category"] = 0.5, ["Brand"] = 0.5, ["Color"] = 0.4, ["Material"] = 0.3,
            ["Location"] = 0.3, ["Time"] = 0.2
        };
    }
}
