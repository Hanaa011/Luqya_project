using System.Collections.Generic;

namespace LostFound.AI.Retrieval
{
    // "Candidate Merge" stage - combines every strategy's independent
    // result list into one candidate per distinct ReportId, carrying every
    // strategy's contribution (score + that strategy's own rank, needed by
    // IFusionEngine's Reciprocal Rank Fusion mode).
    public interface ICandidateMerger
    {
        IReadOnlyList<RetrievedCandidate> Merge(IReadOnlyList<StrategyResult> strategyResults);
    }
}
