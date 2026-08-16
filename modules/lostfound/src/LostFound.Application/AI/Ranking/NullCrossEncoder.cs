using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LostFound.AI.Query;
using LostFound.AI.Retrieval;

namespace LostFound.AI.Ranking
{
    // No local ONNX cross-encoder model is installed in this environment
    // (same "defer the model" posture as Phase 2A Part 2's embedding
    // runtime). "If unavailable, continue without failure" (spec) -
    // RerankAsync is a real, safe pass-through, not a stub that throws.
    // Swapping in a real cross-encoder later means a new ICrossEncoder
    // implementation (following OnnxEmbeddingRuntime's install/load
    // pattern), registered in this class's place - zero change to
    // IRankingEngine.
    internal sealed class NullCrossEncoder(ILogger<NullCrossEncoder> logger) : ICrossEncoder
    {
        public Task<CrossEncoderStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CrossEncoderStatus.NotAvailable);

        public Task<IReadOnlyList<FusedCandidate>> RerankAsync(
            IReadOnlyList<FusedCandidate> topCandidates, SemanticQuery query, CancellationToken cancellationToken = default)
        {
            logger.LogDebug(
                "No local cross-encoder model is installed; passing {Count} candidate(s) through unchanged.", topCandidates.Count);

            return Task.FromResult(topCandidates);
        }
    }
}
