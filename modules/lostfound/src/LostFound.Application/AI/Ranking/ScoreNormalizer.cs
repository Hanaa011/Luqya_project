using System;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;

namespace LostFound.AI.Ranking
{
    // Eleven of the fourteen features are already 0-100 (either a direct
    // percentage, or a "matched/not matched" 0-or-100 that IFeatureExtractor
    // already produced) - a plain /100 is correct for those. Bm25Score and
    // KnowledgeGraphSimilarity are raw, UNBOUNDED sums (BM25 typically 0-15,
    // graph-expansion-weight sums typically 0-3 for this workspace's data) -
    // a plain /100 would nearly always round to ~0 for either, so both get a
    // diminishing-returns compression (1 - e^(-x/scale)) instead, each with
    // its own scale constant reflecting its own typical range.
    //
    // EmbeddingSimilarity gets its own floor-rescale rather than a plain
    // /100: CosineSimilarityCalculator.CalculatePercentage maps raw cosine
    // similarity linearly ((cosine+1)/2*100), and empirically, cosine
    // similarity between genuinely UNRELATED same-language sentences from
    // this embedding model rarely drops much below ~0.5-0.6 (verified
    // against real production data - see the PHASE-VALIDATION-03 report's
    // Semantic Quality Analysis: unrelated reports were landing at 79-80%
    // raw similarity, barely below a truly matching report's 81%). A plain
    // /100 treats that whole narrow, mostly-baseline-noise band as 79-81%
    // "meaningful" similarity, which is why unrelated candidates were
    // ranking almost identically to the correct one. EmbeddingSimilarityFloor
    // (as a raw 0-100 percentage) is the calibrated cut-in point below which
    // similarity is treated as noise (0); everything above it is rescaled to
    // fill the full 0-100 range, restoring the discriminative power the
    // linear mapping was destroying. Local to search ranking only - report
    // matching's threshold (LostFound:AI:MatchThreshold) and duplicate
    // detection still read CosineSimilarityCalculator's raw percentage
    // directly and are unaffected.
    internal sealed class ScoreNormalizer(IOptions<RankingOptions> options) : IScoreNormalizer
    {
        public RankingFeatures Normalize(RankingFeatures raw) => raw with
        {
            EmbeddingSimilarity = RescaleAboveFloor(raw.EmbeddingSimilarity, options.Value.EmbeddingSimilarityFloor),
            // Same local embedding model, same raw-cosine-percentage scale,
            // same baseline-noise phenomenon as EmbeddingSimilarity above -
            // reuses the identical calibrated floor rather than a plain
            // /100 (which would suffer the exact discriminative-power loss
            // already diagnosed and fixed for the Description channel).
            // Revisit with its own floor constant if benchmark evidence
            // shows the metadata channel's noise floor differs.
            MetadataEmbeddingSimilarity = RescaleAboveFloor(raw.MetadataEmbeddingSimilarity, options.Value.EmbeddingSimilarityFloor),
            Bm25Score = Compress(raw.Bm25Score, options.Value.Bm25NormalizationScale),
            KnowledgeGraphSimilarity = Compress(raw.KnowledgeGraphSimilarity, options.Value.GraphNormalizationScale),
            ObjectTypeSimilarity = Clamp01(raw.ObjectTypeSimilarity / 100.0),
            CategorySimilarity = Clamp01(raw.CategorySimilarity / 100.0),
            BrandSimilarity = Clamp01(raw.BrandSimilarity / 100.0),
            ColorSimilarity = Clamp01(raw.ColorSimilarity / 100.0),
            MaterialSimilarity = Clamp01(raw.MaterialSimilarity / 100.0),
            LocationSimilarity = Clamp01(raw.LocationSimilarity / 100.0),
            TimeProximity = Clamp01(raw.TimeProximity / 100.0),
            AliasMatch = Clamp01(raw.AliasMatch / 100.0),
            ExactMatch = Clamp01(raw.ExactMatch / 100.0),
            HistoricalSuccess = Clamp01(raw.HistoricalSuccess / 100.0),
            Popularity = Clamp01(raw.Popularity / 100.0)
        };

        private static double Compress(double rawValue, double scale) =>
            rawValue <= 0 ? 0.0 : Clamp01(1.0 - Math.Exp(-rawValue / scale));

        private static double RescaleAboveFloor(double rawPercentage, double floorPercentage) =>
            floorPercentage >= 100.0 ? 0.0 : Clamp01((rawPercentage - floorPercentage) / (100.0 - floorPercentage));

        private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
    }
}
