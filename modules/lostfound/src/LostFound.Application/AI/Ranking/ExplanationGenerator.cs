using System;
using System.Collections.Generic;
using System.Linq;

namespace LostFound.AI.Ranking
{
    // "No opaque scoring" (spec) - every RankedResult must be able to
    // explain itself: which signals fired, how strong each was, and
    // whether semantic (embedding) or graph evidence contributed, not just
    // a final number. FeatureContribution.FeatureName stays a stable
    // English identifier (matches RankingFeatures' property names, so it
    // can still be matched on programmatically) - only the human-readable
    // Summary/StrongestSignals text is localized, following the query's
    // detected language (see IExplanationGenerator).
    internal sealed class ExplanationGenerator : IExplanationGenerator
    {
        private static readonly IReadOnlyDictionary<string, Func<RankingFeatures, double>> FeatureSelectors =
            new Dictionary<string, Func<RankingFeatures, double>>
            {
                ["EmbeddingSimilarity"] = f => f.EmbeddingSimilarity,
                ["Bm25Score"] = f => f.Bm25Score,
                ["KnowledgeGraphSimilarity"] = f => f.KnowledgeGraphSimilarity,
                ["ObjectTypeSimilarity"] = f => f.ObjectTypeSimilarity,
                ["CategorySimilarity"] = f => f.CategorySimilarity,
                ["BrandSimilarity"] = f => f.BrandSimilarity,
                ["ColorSimilarity"] = f => f.ColorSimilarity,
                ["MaterialSimilarity"] = f => f.MaterialSimilarity,
                ["LocationSimilarity"] = f => f.LocationSimilarity,
                ["TimeProximity"] = f => f.TimeProximity,
                ["AliasMatch"] = f => f.AliasMatch,
                ["ExactMatch"] = f => f.ExactMatch,
                ["HistoricalSuccess"] = f => f.HistoricalSuccess,
                ["Popularity"] = f => f.Popularity
            };

        public RankingExplanation Generate(
            RankingFeatures normalizedFeatures,
            IReadOnlyDictionary<string, double> weights,
            double confidence,
            string languageCode)
        {
            var text = ExplanationVocabulary.For(languageCode);

            var contributions = FeatureSelectors
                .Select(kv =>
                {
                    var value = kv.Value(normalizedFeatures);
                    var weight = weights.GetValueOrDefault(kv.Key, 0);
                    return new FeatureContribution(kv.Key, Math.Round(value, 3), weight, Math.Round(value * weight, 3));
                })
                .OrderByDescending(c => c.WeightedContribution)
                .ToList();

            var strongestSignals = contributions
                .Where(c => c.NormalizedValue > 0 && c.Weight > 0)
                .Take(3)
                .Select(c => $"{text.FeatureName(c.FeatureName)} ({c.NormalizedValue:P0})")
                .ToList();

            var summary = strongestSignals.Count > 0
                ? text.MatchedOn(strongestSignals, confidence)
                : text.NoStrongSignals(confidence);

            var hasSemanticEvidence = normalizedFeatures.EmbeddingSimilarity > 0;
            var hasGraphEvidence = normalizedFeatures.KnowledgeGraphSimilarity > 0 || normalizedFeatures.AliasMatch > 0;

            return new RankingExplanation(summary, strongestSignals, contributions, hasSemanticEvidence, hasGraphEvidence);
        }
    }
}
