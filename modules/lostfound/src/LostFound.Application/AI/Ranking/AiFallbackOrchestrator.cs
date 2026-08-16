using System;
using System.Linq;

namespace LostFound.AI.Ranking
{
    /// <summary>
    /// Classifies which tier of Phase 1 Part 7's 8-tier ladder characterizes
    /// a completed search, from signals already present in RankingDiagnostics.
    /// </summary>
    /// <remarks>
    /// Honest limitation: <see cref="FallbackTier.CachedAi"/> is never
    /// returned by this implementation - nothing in the ranking pipeline
    /// currently threads through a "was this embedding served from
    /// QueryProcessingCache" signal for this method to inspect, so
    /// guessing at it would be fabricating a result rather than reporting
    /// one. Every other tier is derived from a real, checkable condition.
    /// </remarks>
    internal sealed class AiFallbackOrchestrator : IAIFallbackOrchestrator
    {
        public FallbackTier DetermineTier(RankingDiagnostics diagnostics, string embeddingEngineName, bool crossEncoderUsed)
        {
            if (crossEncoderUsed)
            {
                return FallbackTier.LocalCrossEncoder;
            }

            var featureValues = diagnostics.FeatureValuesByReport.Values;

            if (featureValues.Any(f => f.EmbeddingSimilarity > 0))
            {
                var isLocalEmbedding = embeddingEngineName.StartsWith("Local:", StringComparison.Ordinal);
                return isLocalEmbedding ? FallbackTier.LocalEmbeddings : FallbackTier.ExternalAi;
            }

            if (featureValues.Any(f => f.KnowledgeGraphSimilarity > 0 || f.AliasMatch > 0))
            {
                return FallbackTier.KnowledgeGraph;
            }

            var hasBm25Signal = featureValues.Any(f => f.Bm25Score > 0);
            var hasAnyAttributeSignal = featureValues.Any(f =>
                f.ObjectTypeSimilarity > 0 || f.CategorySimilarity > 0 || f.BrandSimilarity > 0 ||
                f.ColorSimilarity > 0 || f.MaterialSimilarity > 0 || f.LocationSimilarity > 0 ||
                f.TimeProximity > 0 || f.ExactMatch > 0);

            if (hasBm25Signal && hasAnyAttributeSignal)
            {
                return FallbackTier.HybridRanking;
            }

            if (hasBm25Signal)
            {
                return FallbackTier.Bm25Only;
            }

            return FallbackTier.KeywordMatchOnly;
        }
    }
}
