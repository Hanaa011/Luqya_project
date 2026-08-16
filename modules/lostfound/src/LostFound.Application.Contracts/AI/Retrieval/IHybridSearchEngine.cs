using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Query;
using LostFound.Reports;

namespace LostFound.AI.Retrieval
{
    // ReportsById is the same SearchableReport projection HybridSearchEngine
    // already builds internally to run retrieval - exposed here (Phase 2B
    // Part 4) so a ranking layer can reuse it directly instead of
    // re-fetching and re-mapping every report a second time.
    public sealed record HybridSearchResult(
        IReadOnlyList<FusedCandidate> Candidates,
        RetrievalDiagnostics Diagnostics,
        IReadOnlyDictionary<System.Guid, SearchableReport> ReportsById);

    // The single entry point for the whole engine: Retrieval Planner ->
    // Parallel Retrieval -> Candidate Merge -> Duplicate Removal -> Score
    // Fusion -> Candidate Set, exactly the spec's pipeline diagram. Does
    // NOT rank the candidate set (spec: "This phase MUST NOT perform final
    // ranking") - FusedCandidate.FusedScore is a relevance signal for
    // Phase 2B Part 3's ranking engine to consume, not a final order
    // (callers should not assume the returned list is sorted).
    public interface IHybridSearchEngine
    {
        Task<HybridSearchResult> SearchAsync(
            SemanticQuery query, ReportType? type, int maxResults, CancellationToken cancellationToken = default);
    }
}
