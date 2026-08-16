using System.Collections.Generic;

namespace LostFound.AI.Retrieval
{
    // "Fusion implementation must be replaceable" (spec) - FusionMethod
    // selects between the two named techniques (WeightedLinear needs
    // per-strategy configured weights, ReciprocalRankFusion needs only each
    // strategy's rank - see the spec's own Phase 1 Part 7 rationale for RRF:
    // BM25 and cosine similarity aren't on a comparable scale, so fusing by
    // RANK rather than raw score avoids needing labeled data to tune
    // weights).
    public interface IFusionEngine
    {
        IReadOnlyList<FusedCandidate> Fuse(IReadOnlyList<RetrievedCandidate> candidates, FusionMethod method);
    }
}
