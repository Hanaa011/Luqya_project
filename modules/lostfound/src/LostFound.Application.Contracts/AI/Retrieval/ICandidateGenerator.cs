using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Retrieval
{
    public sealed record StrategyResult(string StrategyName, IReadOnlyList<StrategyCandidate> Candidates, long ElapsedMilliseconds, string? Error);

    // Runs every enabled strategy in parallel and isolates failures per
    // strategy - "No single retriever failure should fail the search"
    // (spec). A failed strategy contributes an empty candidate list and its
    // error is captured in StrategyResult.Error for diagnostics, never
    // thrown out of GenerateAsync.
    public interface ICandidateGenerator
    {
        Task<IReadOnlyList<StrategyResult>> GenerateAsync(
            RetrievalContext context, IReadOnlyList<IRetrievalStrategy> strategies, CancellationToken cancellationToken = default);
    }
}
