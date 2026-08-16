using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Query;
using LostFound.AI.Retrieval;

namespace LostFound.AI.Ranking
{
    // The single entry point implementing the spec's full pipeline: Feature
    // Extraction -> Score Normalization -> Weighted Ranking -> Cross
    // Encoder (optional) -> Learning-to-Rank -> Confidence Calibration ->
    // Explanation Generation -> Final Ranked Results. Consumes Phase 2B
    // Part 2's candidate set (FusedCandidate) - "Retrieval is complete.
    // This phase performs scoring, reranking, confidence estimation and
    // fallback orchestration" (spec).
    public interface IRankingEngine
    {
        Task<RankingResult> RankAsync(
            IReadOnlyList<FusedCandidate> candidates,
            IReadOnlyDictionary<System.Guid, SearchableReport> reportsById,
            SemanticQuery query,
            CancellationToken cancellationToken = default);
    }
}
