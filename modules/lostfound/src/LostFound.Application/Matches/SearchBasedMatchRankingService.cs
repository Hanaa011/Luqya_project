using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LostFound.AI.Integration;
using LostFound.Reports;

namespace LostFound.Matches
{
    /// <summary>
    /// Implements the Domain-owned <see cref="IMatchRankingService"/> seam
    /// by wrapping the SAME <see cref="ISemanticSearchOrchestrator"/> real
    /// user searches already go through - no ranking logic is duplicated
    /// here, this class only translates between a "compare two reports"
    /// call shape (what <see cref="MatchManager"/> needs) and a "rank many
    /// candidates against a text query" call shape (what the orchestrator
    /// provides), by treating the source report's own stored
    /// classification/description text as the query.
    /// </summary>
    internal sealed class SearchBasedMatchRankingService(
        ISemanticSearchOrchestrator searchOrchestrator,
        ILogger<SearchBasedMatchRankingService> logger) : IMatchRankingService
    {
        // Generous on purpose: SemanticSearchOrchestrator computes its own
        // internal retrieval-candidate limit from this value (see
        // HybridPipelineOptions.RetrievalOverfetchMultiplier /
        // MinRetrievalCandidates), so asking for a large ranked result set
        // here gives MatchManager's own candidate list (already filtered to
        // "Open, opposite type, has an embedding" by IReportRepository.
        // GetMatchCandidatesAsync) the best real chance of appearing in the
        // ranking at all, in every corpus size this workspace has been
        // validated against. In a MUCH larger production corpus this value
        // may need to become configurable rather than a constant - flagged
        // here rather than silently assumed away.
        private const int MaxRankedCandidates = 200;

        public async Task<IReadOnlyDictionary<Guid, double>> RankCandidatesAsync(
            Report report, CancellationToken cancellationToken = default)
        {
            // Report.BuildEmbeddingText() was split (Multi-Representation
            // Embedding change) into BuildDescriptionEmbeddingText() +
            // BuildMetadataEmbeddingText() for STORED candidate embeddings -
            // that split is deliberately NOT extended to this call site,
            // which uses the combined text only as an ad-hoc QUERY STRING
            // fed through the full QueryPipeline (not stored as a vector),
            // so the embedding-pollution concern that motivated the split
            // doesn't apply here. Recombined to reproduce the exact same
            // query text this call site used before the split.
            var metadataText = report.BuildMetadataEmbeddingText();
            var queryText = string.IsNullOrWhiteSpace(metadataText)
                ? report.BuildDescriptionEmbeddingText()
                : $"{report.BuildDescriptionEmbeddingText()}. {metadataText}";

            var oppositeType = report.Type == ReportType.Lost ? ReportType.Found : ReportType.Lost;

            var ranked = await searchOrchestrator.SearchAsync(
                queryText, oppositeType, MaxRankedCandidates, cancellationToken);

            logger.LogDebug(
                "SearchBasedMatchRankingService: ranked {Count} candidate(s) for ReportId={ReportId} using its own text as the query.",
                ranked.Count, report.Id);

            var scoresByReportId = new Dictionary<Guid, double>(ranked.Count);
            foreach (var result in ranked)
            {
                // SearchAsync can never return duplicate ReportIds (each
                // candidate is ranked once), so a plain assignment is safe -
                // no TryAdd/merge logic needed.
                scoresByReportId[result.ReportId] = result.ScorePercentage;
            }

            return scoresByReportId;
        }
    }
}
