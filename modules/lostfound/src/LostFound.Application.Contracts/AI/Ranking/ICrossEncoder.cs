using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Query;
using LostFound.AI.Retrieval;

namespace LostFound.AI.Ranking
{
    // Optional local ONNX cross-encoder reranking of the top-N candidates.
    // "If unavailable, continue without failure" (spec) - no cross-encoder
    // model exists in this environment (the same "defer the model" posture
    // as Phase 2A Part 2's embedding runtime, for the same reason: no
    // internet route to a real model host to provision one from). Callers
    // must check GetStatusAsync() and treat NotAvailable as the expected,
    // normal state, not an error.
    public interface ICrossEncoder
    {
        Task<CrossEncoderStatus> GetStatusAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FusedCandidate>> RerankAsync(
            IReadOnlyList<FusedCandidate> topCandidates, SemanticQuery query, CancellationToken cancellationToken = default);
    }
}
