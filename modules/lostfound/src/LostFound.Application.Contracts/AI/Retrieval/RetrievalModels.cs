using System;
using System.Collections.Generic;
using LostFound.AI.Query;

namespace LostFound.AI.Retrieval
{
    // Raw output of ONE IRetrievalStrategy call - unmerged, unfused.
    public sealed record StrategyCandidate(Guid ReportId, double Score);

    // One strategy's contribution to a merged candidate - Rank is populated
    // by ICandidateMerger (1-based position within that strategy's own
    // result list) since Reciprocal Rank Fusion needs rank, not just score.
    public sealed record StrategyContribution(string StrategyName, double Score, int Rank);

    // A candidate after ICandidateMerger + IDuplicateResolver - carries
    // provenance from every strategy that found it ("source attribution",
    // per the spec), ready for IFusionEngine.
    public sealed record RetrievedCandidate(Guid ReportId, IReadOnlyList<StrategyContribution> Contributions);

    // The final "Candidate Set" this whole engine produces - explicitly NOT
    // final ranking (spec: "This phase MUST NOT perform final ranking"),
    // just a fused relevance score plus full provenance for Phase 2B Part 3
    // to rank.
    public sealed record FusedCandidate(Guid ReportId, double FusedScore, IReadOnlyList<StrategyContribution> Contributions);

    public enum FusionMethod
    {
        WeightedLinear,
        ReciprocalRankFusion
    }

    // Shared input every IRetrievalStrategy receives - reports are fetched
    // ONCE by IHybridSearchEngine and distributed to every strategy running
    // in parallel, rather than each strategy independently querying
    // IReportRepository (the spec's "Ready for 100k+ concepts" performance
    // requirement rules out N independent DB round-trips per search).
    public sealed record RetrievalContext(SemanticQuery Query, IReadOnlyList<SearchableReport> Candidates, int Limit);

    public sealed record RetrievalPlan(IReadOnlyList<string> EnabledStrategyNames, int PerStrategyLimit);

    public sealed record RetrievalDiagnostics(
        IReadOnlyDictionary<string, long> StrategyExecutionTimesMs,
        IReadOnlyDictionary<string, int> StrategyCandidateCounts,
        IReadOnlyDictionary<string, string> StrategyFailures,
        int DuplicatesRemoved,
        int FinalCandidateCount,
        long TotalElapsedMs);
}
