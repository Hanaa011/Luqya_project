using System;
using System.Collections.Generic;
using System.Linq;
using LostFound.AI.Analytics;

namespace LostFound.AI.Analytics
{
    internal sealed class SearchQualityMetricsCalculator : ISearchQualityMetricsCalculator
    {
        public double PrecisionAtK(IReadOnlyList<Guid> rankedResultIds, IReadOnlySet<Guid> relevantIds, int k)
        {
            if (k <= 0 || rankedResultIds.Count == 0)
            {
                return 0;
            }

            var top = rankedResultIds.Take(k).ToList();
            var relevantRetrieved = top.Count(id => relevantIds.Contains(id));

            return (double)relevantRetrieved / top.Count;
        }

        public double RecallAtK(IReadOnlyList<Guid> rankedResultIds, IReadOnlySet<Guid> relevantIds, int k)
        {
            if (k <= 0 || relevantIds.Count == 0)
            {
                return 0;
            }

            var relevantRetrieved = rankedResultIds.Take(k).Count(id => relevantIds.Contains(id));

            return (double)relevantRetrieved / relevantIds.Count;
        }

        public double MeanAveragePrecision(
            IReadOnlyList<IReadOnlyList<Guid>> rankedResultsPerQuery,
            IReadOnlyList<IReadOnlySet<Guid>> relevantIdsPerQuery)
        {
            if (rankedResultsPerQuery.Count == 0 || rankedResultsPerQuery.Count != relevantIdsPerQuery.Count)
            {
                return 0;
            }

            var averagePrecisions = new List<double>();

            for (var i = 0; i < rankedResultsPerQuery.Count; i++)
            {
                averagePrecisions.Add(AveragePrecision(rankedResultsPerQuery[i], relevantIdsPerQuery[i]));
            }

            return averagePrecisions.Average();
        }

        public double Ndcg(IReadOnlyList<Guid> rankedResultIds, IReadOnlyDictionary<Guid, double> relevanceGrades, int k)
        {
            if (k <= 0 || rankedResultIds.Count == 0)
            {
                return 0;
            }

            var top = rankedResultIds.Take(k).ToList();

            double Dcg(IReadOnlyList<Guid> ids)
            {
                var sum = 0.0;
                for (var i = 0; i < ids.Count; i++)
                {
                    var gain = relevanceGrades.GetValueOrDefault(ids[i], 0);
                    sum += gain / Math.Log2(i + 2); // rank is 1-based, i is 0-based -> i+2
                }
                return sum;
            }

            var idealOrder = relevanceGrades
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .Take(k)
                .ToList();

            var idealDcg = Dcg(idealOrder);
            if (idealDcg <= 0)
            {
                return 0;
            }

            return Dcg(top) / idealDcg;
        }

        public double MeanReciprocalRank(
            IReadOnlyList<IReadOnlyList<Guid>> rankedResultsPerQuery,
            IReadOnlyList<IReadOnlySet<Guid>> relevantIdsPerQuery)
        {
            if (rankedResultsPerQuery.Count == 0 || rankedResultsPerQuery.Count != relevantIdsPerQuery.Count)
            {
                return 0;
            }

            var reciprocalRanks = new List<double>();

            for (var i = 0; i < rankedResultsPerQuery.Count; i++)
            {
                var results = rankedResultsPerQuery[i];
                var relevant = relevantIdsPerQuery[i];
                var firstRelevantRank = -1;

                for (var rank = 0; rank < results.Count; rank++)
                {
                    if (relevant.Contains(results[rank]))
                    {
                        firstRelevantRank = rank + 1;
                        break;
                    }
                }

                reciprocalRanks.Add(firstRelevantRank > 0 ? 1.0 / firstRelevantRank : 0);
            }

            return reciprocalRanks.Average();
        }

        private static double AveragePrecision(IReadOnlyList<Guid> rankedResultIds, IReadOnlySet<Guid> relevantIds)
        {
            if (relevantIds.Count == 0)
            {
                return 0;
            }

            var relevantSeen = 0;
            var precisionSum = 0.0;

            for (var i = 0; i < rankedResultIds.Count; i++)
            {
                if (!relevantIds.Contains(rankedResultIds[i]))
                {
                    continue;
                }

                relevantSeen++;
                precisionSum += (double)relevantSeen / (i + 1);
            }

            return relevantSeen == 0 ? 0 : precisionSum / relevantIds.Count;
        }
    }
}
