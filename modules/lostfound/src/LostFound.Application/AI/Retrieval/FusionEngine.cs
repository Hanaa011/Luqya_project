using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;

namespace LostFound.AI.Retrieval
{
    internal sealed class FusionEngine(IOptions<RetrievalOptions> options) : IFusionEngine
    {
        public IReadOnlyList<FusedCandidate> Fuse(IReadOnlyList<RetrievedCandidate> candidates, FusionMethod method) => method switch
        {
            FusionMethod.ReciprocalRankFusion => FuseByReciprocalRank(candidates),
            FusionMethod.WeightedLinear => FuseByWeightedLinear(candidates),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported fusion method.")
        };

        private IReadOnlyList<FusedCandidate> FuseByReciprocalRank(IReadOnlyList<RetrievedCandidate> candidates)
        {
            var k = options.Value.RrfK;

            return candidates
                .Select(c => new FusedCandidate(
                    c.ReportId,
                    c.Contributions.Sum(contribution => 1.0 / (k + contribution.Rank)),
                    c.Contributions))
                .OrderByDescending(c => c.FusedScore)
                .ToList();
        }

        private IReadOnlyList<FusedCandidate> FuseByWeightedLinear(IReadOnlyList<RetrievedCandidate> candidates)
        {
            var weights = options.Value.StrategyWeights;

            return candidates
                .Select(c => new FusedCandidate(
                    c.ReportId,
                    c.Contributions.Sum(contribution => contribution.Score * weights.GetValueOrDefault(contribution.StrategyName, 1.0)),
                    c.Contributions))
                .OrderByDescending(c => c.FusedScore)
                .ToList();
        }
    }
}
