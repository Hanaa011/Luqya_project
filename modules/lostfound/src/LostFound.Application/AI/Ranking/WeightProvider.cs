using System.Collections.Generic;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;

namespace LostFound.AI.Ranking
{
    internal sealed class WeightProvider(IOptions<RankingOptions> options) : IWeightProvider
    {
        // 'variant' is accepted now so callers can already code against the
        // A/B-testing seam the spec asks to "prepare for" - only the
        // default (null) variant is actually backed by configuration today.
        public IReadOnlyDictionary<string, double> GetWeights(string? variant = null) => options.Value.FeatureWeights;
    }
}
