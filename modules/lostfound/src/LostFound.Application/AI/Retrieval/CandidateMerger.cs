using System;
using System.Collections.Generic;
using System.Linq;

namespace LostFound.AI.Retrieval
{
    // Combines every strategy's independent result list into one candidate
    // per distinct ReportId. Rank is computed here (1-based position within
    // each strategy's own descending-score order), since raw
    // StrategyCandidate output doesn't carry rank - IFusionEngine's
    // Reciprocal Rank Fusion mode needs it.
    internal sealed class CandidateMerger : ICandidateMerger
    {
        public IReadOnlyList<RetrievedCandidate> Merge(IReadOnlyList<StrategyResult> strategyResults)
        {
            var contributionsByReportId = new Dictionary<Guid, List<StrategyContribution>>();

            foreach (var result in strategyResults)
            {
                var rank = 1;

                foreach (var candidate in result.Candidates.OrderByDescending(c => c.Score))
                {
                    if (!contributionsByReportId.TryGetValue(candidate.ReportId, out var contributions))
                    {
                        contributions = new List<StrategyContribution>();
                        contributionsByReportId[candidate.ReportId] = contributions;
                    }

                    contributions.Add(new StrategyContribution(result.StrategyName, candidate.Score, rank));
                    rank++;
                }
            }

            return contributionsByReportId
                .Select(kv => new RetrievedCandidate(kv.Key, kv.Value))
                .ToList();
        }
    }
}
