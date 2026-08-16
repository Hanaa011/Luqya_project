using System.Collections.Generic;

namespace LostFound.AI.Configuration
{
    // Bound from configuration section "LostFound:AI:Ranking". Every
    // feature weight is independently adjustable without recompilation
    // (spec's "Adaptive Weights" requirement). HistoricalSuccess/Popularity
    // default to 0 - no real signal source exists yet (see RankingFeatures'
    // own remarks), so weighting them would only add noise.
    public class RankingOptions
    {
        public Dictionary<string, double> FeatureWeights { get; set; } = new()
        {
            ["EmbeddingSimilarity"] = 1.0,
            ["MetadataEmbeddingSimilarity"] = 0.4,
            ["Bm25Score"] = 0.8,
            ["KnowledgeGraphSimilarity"] = 0.7,
            ["ObjectTypeSimilarity"] = 0.9,
            ["CategorySimilarity"] = 0.5,
            ["BrandSimilarity"] = 0.6,
            ["ColorSimilarity"] = 0.4,
            ["MaterialSimilarity"] = 0.3,
            ["LocationSimilarity"] = 0.3,
            ["TimeProximity"] = 0.2,
            ["AliasMatch"] = 0.6,
            ["ExactMatch"] = 0.9,
            ["HistoricalSuccess"] = 0.0,
            ["Popularity"] = 0.0
        };

        // Diminishing-returns compression scale (1 - e^(-x/scale)) for the
        // two raw features that aren't already on a 0-100 scale.
        public double Bm25NormalizationScale { get; set; } = 10.0;
        public double GraphNormalizationScale { get; set; } = 1.5;

        // Raw cosine-similarity percentage (CosineSimilarityCalculator's
        // (cosine+1)/2*100) below which EmbeddingSimilarity is treated as
        // noise (0) rather than signal - see ScoreNormalizer's remarks.
        // Calibrated against real production embeddings: genuinely unrelated
        // same-language reports landed at 79-80%, a truly matching one at
        // 81% - 75 sits just below that observed noise floor.
        public double EmbeddingSimilarityFloor { get; set; } = 75.0;

        // ConfidenceCalibrator's sigmoid input is LinearLearningToRankEngine's
        // rawScore: a weighted average across ALL 14 configured features
        // (rescaled to 0-100 by the SAME fixed total weight, ~7.2 with the
        // defaults above, regardless of how many of those 14 actually have
        // any signal for a given candidate - see LinearLearningToRankEngine's
        // remarks). HistoricalSuccess/Popularity are permanently zero-weighted
        // placeholders, and structured attributes (ObjectType/Category/Brand/
        // Color/Material/Alias/Exact) are frequently zero too whenever AI
        // classification degraded (see PHASE-VALIDATION-02's findings), and
        // EmbeddingSimilarity itself now only reaches its full 0-1 normalized
        // range once raw similarity clears EmbeddingSimilarityFloor - so a
        // realistic, even excellent, match's rawScore very rarely approaches
        // 50, or even 20. A midpoint of 50 (assuming most/all features
        // routinely fire at once, on an uncompressed embedding scale) made
        // the sigmoid's steep transition zone practically unreachable,
        // compressing every candidate's confidence toward the low end
        // regardless of true match quality. 8/5 places the transition zone
        // where rawScore actually lands for realistic single-to-few-signal
        // matches (verified against real report data - see the
        // PHASE-VALIDATION-03 report's Confidence Calibration Analysis).
        public double ConfidenceMidpoint { get; set; } = 8.0;
        public double ConfidenceTemperature { get; set; } = 5.0;
        public int ExpectedSignalCount { get; set; } = 4;

        public int CrossEncoderTopN { get; set; } = 20;
    }
}
