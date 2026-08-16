using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LostFound.AI.Core;
using LostFound.Matching;

namespace LostFound.AI.Retrieval
{
    // Dense retrieval: cosine similarity between the query embedding
    // (generated via Phase 2A Part 1/2's IEmbeddingEngine - local-first,
    // falls back to the external provider) and each candidate's stored
    // text embedding. Reuses LostFound.Matching.CosineSimilarityCalculator
    // (the same 0-100 percentage calculation AiMatchingService's existing
    // scoring already uses) rather than a second implementation.
    internal sealed class VectorRetriever(IEmbeddingEngine embeddingEngine, ILogger<VectorRetriever> logger) : IVectorRetriever
    {
        public string StrategyName => "Vector";

        public async Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(context.Query.FinalSemanticText))
            {
                return Array.Empty<StrategyCandidate>();
            }

            float[] queryEmbedding;
            try
            {
                queryEmbedding = await embeddingEngine.GenerateEmbeddingAsync(context.Query.FinalSemanticText, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Embedding generation is an enhancement, not a hard
                // requirement for the whole search - "No single retriever
                // failure should fail the search" (spec).
                logger.LogWarning(ex, "Vector retrieval failed to generate a query embedding; returning no candidates.");
                return Array.Empty<StrategyCandidate>();
            }

            return context.Candidates
                .Where(r => r.EmbeddingVector is { Length: > 0 })
                .Select(r => new StrategyCandidate(r.ReportId, CosineSimilarityCalculator.CalculatePercentage(queryEmbedding, r.EmbeddingVector)))
                .Where(c => c.Score > 0)
                .OrderByDescending(c => c.Score)
                .Take(context.Limit)
                .ToList();
        }
    }
}
