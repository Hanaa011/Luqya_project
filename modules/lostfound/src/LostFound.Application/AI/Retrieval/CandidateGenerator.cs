using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LostFound.AI.Retrieval
{
    // Runs every planned strategy in parallel (Task.WhenAll), isolating
    // each one's failure so it never aborts the others or the whole search
    // - "No single retriever failure should fail the search" (spec).
    internal sealed class CandidateGenerator(ILogger<CandidateGenerator> logger) : ICandidateGenerator
    {
        public async Task<IReadOnlyList<StrategyResult>> GenerateAsync(
            RetrievalContext context, IReadOnlyList<IRetrievalStrategy> strategies, CancellationToken cancellationToken = default)
        {
            var results = await Task.WhenAll(strategies.Select(strategy => RunStrategyAsync(strategy, context, cancellationToken)));
            return results;
        }

        private async Task<StrategyResult> RunStrategyAsync(IRetrievalStrategy strategy, RetrievalContext context, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var candidates = await strategy.RetrieveAsync(context, cancellationToken);
                return new StrategyResult(strategy.StrategyName, candidates, stopwatch.ElapsedMilliseconds, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Retrieval strategy '{Strategy}' failed; contributing zero candidates.", strategy.StrategyName);
                return new StrategyResult(strategy.StrategyName, Array.Empty<StrategyCandidate>(), stopwatch.ElapsedMilliseconds, ex.Message);
            }
        }
    }
}
