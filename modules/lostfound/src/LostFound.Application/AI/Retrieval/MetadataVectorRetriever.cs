using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LostFound.AI.Core;
using LostFound.AI.Query;
using LostFound.Matching;

namespace LostFound.AI.Retrieval
{
    // Dense retrieval over the SECOND, independent embedding - AI-classified
    // attributes only (ObjectType/Color/Brand/Category/Tags), never the raw
    // Description - see Report.MetadataEmbeddingJson's remarks and
    // Multi-Representation-Embedding-Architecture-Analysis.md for why this
    // exists as its own strategy rather than folding into VectorRetriever.
    // Structurally identical to VectorRetriever otherwise (same cosine
    // helper, same "no candidates on failure" degrade, same context.Limit).
    //
    // Query-side text is built here, inline, from the entities
    // IEntityRecognizer already extracted (the same entities the existing
    // Category/Brand/Color/Material structured retrievers already consume -
    // see AttributeMatchHelper.ExtractEntityValues) - a query never gets its
    // own "AI classification", so there is no equivalent of
    // Report.BuildMetadataEmbeddingText() to call on the query side; this is
    // the smallest way to give the query a comparable representation.
    internal sealed class MetadataVectorRetriever(IEmbeddingEngine embeddingEngine, ILogger<MetadataVectorRetriever> logger) : IRetrievalStrategy
    {
        private static readonly EntityType[] MetadataEntityTypes =
        {
            EntityType.Object, EntityType.Color, EntityType.Brand, EntityType.Category, EntityType.Material
        };

        public string StrategyName => "MetadataVector";

        public async Task<IReadOnlyList<StrategyCandidate>> RetrieveAsync(RetrievalContext context, CancellationToken cancellationToken = default)
        {
            var metadataText = BuildQueryMetadataText(context.Query);
            if (string.IsNullOrWhiteSpace(metadataText))
            {
                return Array.Empty<StrategyCandidate>();
            }

            float[] queryEmbedding;
            try
            {
                queryEmbedding = await embeddingEngine.GenerateEmbeddingAsync(metadataText, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Metadata vector retrieval failed to generate a query embedding; returning no candidates.");
                return Array.Empty<StrategyCandidate>();
            }

            return context.Candidates
                .Where(r => r.MetadataEmbeddingVector is { Length: > 0 })
                .Select(r => new StrategyCandidate(r.ReportId, CosineSimilarityCalculator.CalculatePercentage(queryEmbedding, r.MetadataEmbeddingVector)))
                .Where(c => c.Score > 0)
                .OrderByDescending(c => c.Score)
                .Take(context.Limit)
                .ToList();
        }

        private static string? BuildQueryMetadataText(SemanticQuery query)
        {
            var values = query.Entities
                .Where(e => MetadataEntityTypes.Contains(e.Type))
                .Select(e => e.Value.Trim())
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return values.Count > 0 ? string.Join(". ", values) : null;
        }
    }
}
