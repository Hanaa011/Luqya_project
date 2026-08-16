using System;
using System.Collections.Generic;

namespace LostFound.AI.Analytics
{
    // Standard IR quality metrics, implemented as pure functions of a
    // ranked result list and a set of relevance judgments the CALLER
    // supplies. Honest limitation, stated plainly: this environment has no
    // labeled relevance dataset (no click-through log, no human-graded
    // query/result pairs) to invoke these against in production today -
    // fabricating one would be worse than not having the metric. This is
    // real, tested, spec-correct math, ready the moment a relevance source
    // exists (e.g. logged user selections from search results), not a
    // placeholder that returns a fixed number.
    public interface ISearchQualityMetricsCalculator
    {
        double PrecisionAtK(IReadOnlyList<Guid> rankedResultIds, IReadOnlySet<Guid> relevantIds, int k);

        double RecallAtK(IReadOnlyList<Guid> rankedResultIds, IReadOnlySet<Guid> relevantIds, int k);

        double MeanAveragePrecision(
            IReadOnlyList<IReadOnlyList<Guid>> rankedResultsPerQuery,
            IReadOnlyList<IReadOnlySet<Guid>> relevantIdsPerQuery);

        double Ndcg(IReadOnlyList<Guid> rankedResultIds, IReadOnlyDictionary<Guid, double> relevanceGrades, int k);

        double MeanReciprocalRank(
            IReadOnlyList<IReadOnlyList<Guid>> rankedResultsPerQuery,
            IReadOnlyList<IReadOnlySet<Guid>> relevantIdsPerQuery);
    }
}
