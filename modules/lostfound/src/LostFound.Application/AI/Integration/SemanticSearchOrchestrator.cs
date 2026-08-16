using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LostFound.AI.Analytics;
using LostFound.AI.Configuration;
using LostFound.AI.Ontology;
using LostFound.AI.Query;
using LostFound.AI.Ranking;
using LostFound.AI.Retrieval;
using LostFound.Reports;

namespace LostFound.AI.Integration
{
    internal sealed class SemanticSearchOrchestrator(
        IQueryPipeline queryPipeline,
        IHybridSearchEngine hybridSearchEngine,
        IRankingEngine rankingEngine,
        ISearchAnalyticsRecorder analyticsRecorder,
        IOptions<HybridPipelineOptions> options,
        ILogger<SemanticSearchOrchestrator> logger) : ISemanticSearchOrchestrator
    {
        public async Task<List<RankedReportResult>> SearchAsync(
            string searchText, ReportType? type, int maxResults, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            var query = await queryPipeline.ProcessAsync(searchText, cancellationToken);

            var retrievalLimit = Math.Max(
                options.Value.MinRetrievalCandidates,
                (int)Math.Ceiling(maxResults * options.Value.RetrievalOverfetchMultiplier));

            var searchResult = await hybridSearchEngine.SearchAsync(query, type, retrievalLimit, cancellationToken);
            var rankingResult = await rankingEngine.RankAsync(
                searchResult.Candidates, searchResult.ReportsById, query, cancellationToken);

            var results = new List<RankedReportResult>(maxResults);

            foreach (var ranked in rankingResult.Results.Take(maxResults))
            {
                if (!searchResult.ReportsById.TryGetValue(ranked.ReportId, out var report))
                {
                    continue;
                }

                results.Add(BuildResult(ranked, report, query));
            }

            stopwatch.Stop();

            analyticsRecorder.Record(new SearchEvent(
                DateTime.UtcNow,
                Pipeline: "Hybrid",
                LanguageCode: query.LanguageCode,
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                ResultCount: results.Count,
                ZeroResults: results.Count == 0));

            logger.LogInformation(
                "Hybrid pipeline search: {Count} result(s) in {Elapsed}ms (fallback tier: {Tier}).",
                results.Count, stopwatch.ElapsedMilliseconds, rankingResult.Diagnostics.FallbackTier);

            return results;
        }

        // PHASE-VALIDATION-06: object-type compatibility is now computed
        // once, inside IRankingEngine.RankAsync, BEFORE candidates are
        // sorted/truncated - it already shaped ranked.Confidence (and thus
        // the actual result order) by the time execution reaches here. This
        // method used to re-classify compatibility a second time and apply
        // a display-only confidence adjustment AFTER the order was already
        // committed (so a penalized candidate could still occupy a top slot
        // with only its shown percentage reduced, and - a second, related
        // defect - that adjustment never reached ranked.Explanation.Summary,
        // so the displayed score and its explanation text could disagree).
        // Both are fixed by reusing ranked.Confidence/ranked.Compatibility
        // directly instead of recomputing and re-adjusting them here.
        private static RankedReportResult BuildResult(RankedResult ranked, SearchableReport report, SemanticQuery query)
        {
            var reasons = BuildMatchReasons(ranked, ranked.Compatibility, query.LanguageCode);

            return new RankedReportResult
            {
                ReportId = ranked.ReportId,
                Description = report.Description,
                Color = report.Color,
                AiObjectType = report.ObjectType,
                ScorePercentage = Math.Round(ranked.Confidence, 1),
                MatchReasons = reasons,
                MatchExplanation = ranked.Explanation.Summary
            };
        }

        private static List<string> BuildMatchReasons(RankedResult ranked, ObjectTypeCompatibility compatibility, string languageCode)
        {
            var reasons = new List<string>(ranked.Explanation.StrongestSignals);
            var text = ExplanationVocabulary.For(languageCode);

            if (compatibility == ObjectTypeCompatibility.UnrelatedCluster)
            {
                reasons.Add(text.UnrelatedCategoryReason);
            }
            else if (compatibility == ObjectTypeCompatibility.RelatedCluster)
            {
                reasons.Add(text.RelatedCategoryReason);
            }

            return reasons;
        }
    }
}
