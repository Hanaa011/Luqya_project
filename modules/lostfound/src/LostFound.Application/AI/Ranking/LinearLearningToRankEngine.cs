using System.Collections.Generic;
using System.Linq;

namespace LostFound.AI.Ranking
{
    // The real, working default ILearningToRankEngine - a weighted-linear
    // combination of every normalized feature, renormalized back to a 0-100
    // scale by the sum of weights actually applied. "Future ML ranking
    // models" (spec) are a different class registered in this interface's
    // place, not something this Part needs to build - there is no labeled
    // training data in this environment to train one against yet.
    internal sealed class LinearLearningToRankEngine : ILearningToRankEngine
    {
        public double Score(RankingFeatures normalizedFeatures, IReadOnlyDictionary<string, double> weights)
        {
            double Weight(string name) => weights.GetValueOrDefault(name, 0);

            var weightedSum =
                normalizedFeatures.EmbeddingSimilarity * Weight("EmbeddingSimilarity") +
                normalizedFeatures.Bm25Score * Weight("Bm25Score") +
                normalizedFeatures.KnowledgeGraphSimilarity * Weight("KnowledgeGraphSimilarity") +
                normalizedFeatures.ObjectTypeSimilarity * Weight("ObjectTypeSimilarity") +
                normalizedFeatures.CategorySimilarity * Weight("CategorySimilarity") +
                normalizedFeatures.BrandSimilarity * Weight("BrandSimilarity") +
                normalizedFeatures.ColorSimilarity * Weight("ColorSimilarity") +
                normalizedFeatures.MaterialSimilarity * Weight("MaterialSimilarity") +
                normalizedFeatures.LocationSimilarity * Weight("LocationSimilarity") +
                normalizedFeatures.TimeProximity * Weight("TimeProximity") +
                normalizedFeatures.AliasMatch * Weight("AliasMatch") +
                normalizedFeatures.ExactMatch * Weight("ExactMatch") +
                normalizedFeatures.HistoricalSuccess * Weight("HistoricalSuccess") +
                normalizedFeatures.Popularity * Weight("Popularity");

            var totalWeight = weights.Values.Sum();
            return totalWeight > 0 ? weightedSum / totalWeight * 100.0 : 0.0;
        }
    }
}
