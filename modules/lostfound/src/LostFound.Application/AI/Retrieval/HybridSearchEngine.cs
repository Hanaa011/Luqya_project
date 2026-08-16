using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;
using LostFound.AI.Query;
using LostFound.Categories;
using LostFound.Reports;

namespace LostFound.AI.Retrieval
{
    /// <summary>
    /// The single entry point implementing the full pipeline: fetch
    /// searchable reports once (Report -&gt; SearchableReport projection,
    /// including a one-time Category name resolution) -&gt; Retrieval
    /// Planner -&gt; Parallel Retrieval -&gt; Candidate Merge -&gt; Duplicate
    /// Removal -&gt; Score Fusion. Never ranks the result (spec: "This
    /// phase MUST NOT perform final ranking") - Phase 2B Part 3 owns that.
    /// </summary>
    internal sealed class HybridSearchEngine(
        IReportRepository reportRepository,
        ICategoryRepository categoryRepository,
        IEnumerable<IRetrievalStrategy> strategies,
        IRetrievalPlanner planner,
        ICandidateGenerator candidateGenerator,
        ICandidateMerger candidateMerger,
        IDuplicateResolver duplicateResolver,
        IFusionEngine fusionEngine,
        IOptions<RetrievalOptions> options,
        ILogger<HybridSearchEngine> logger) : IHybridSearchEngine
    {
        public async Task<HybridSearchResult> SearchAsync(
            SemanticQuery query, ReportType? type, int maxResults, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            var reports = await reportRepository.GetSearchableReportsAsync(type);
            var searchableReports = await BuildSearchableReportsAsync(reports, cancellationToken);
            var reportsById = searchableReports.ToDictionary(r => r.ReportId);

            var strategyList = strategies.ToList();
            var plan = planner.Plan(query, strategyList);
            var enabledStrategies = strategyList.Where(s => plan.EnabledStrategyNames.Contains(s.StrategyName)).ToList();

            var context = new RetrievalContext(query, searchableReports, plan.PerStrategyLimit);
            var strategyResults = await candidateGenerator.GenerateAsync(context, enabledStrategies, cancellationToken);

            var merged = candidateMerger.Merge(strategyResults);
            var deduped = duplicateResolver.Resolve(merged, reportsById);
            var fused = fusionEngine.Fuse(deduped, options.Value.FusionMethod);

            var top = fused.OrderByDescending(c => c.FusedScore).Take(maxResults).ToList();

            var diagnostics = new RetrievalDiagnostics(
                strategyResults.ToDictionary(r => r.StrategyName, r => r.ElapsedMilliseconds),
                strategyResults.ToDictionary(r => r.StrategyName, r => r.Candidates.Count),
                strategyResults.Where(r => r.Error != null).ToDictionary(r => r.StrategyName, r => r.Error!),
                merged.Count - deduped.Count,
                top.Count,
                stopwatch.ElapsedMilliseconds);

            logger.LogInformation(
                "Hybrid search: {StrategyCount} strategies run, {Merged} merged, {Deduped} after dedup, {Final} returned in {Elapsed}ms.",
                enabledStrategies.Count, merged.Count, deduped.Count, top.Count, stopwatch.ElapsedMilliseconds);

            return new HybridSearchResult(top, diagnostics, reportsById);
        }

        private async Task<IReadOnlyList<SearchableReport>> BuildSearchableReportsAsync(
            List<Report> reports, CancellationToken cancellationToken)
        {
            var categoryIds = reports.Where(r => r.CategoryId.HasValue).Select(r => r.CategoryId!.Value).Distinct().ToList();
            var categoryNamesById = new Dictionary<System.Guid, string>();

            if (categoryIds.Count > 0)
            {
                var allCategories = await categoryRepository.GetListAsync(cancellationToken: cancellationToken);
                categoryNamesById = allCategories
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToDictionary(c => c.Id, c => c.Name);
            }

            return reports.Select(report => new SearchableReport(
                report.Id,
                report.Type,
                report.Description,
                report.LocationDetails,
                report.LostFoundDate,
                report.AiObjectType,
                report.Color,
                report.AiBrand,
                report.GetAiTags(),
                report.CategoryId,
                report.CategoryId.HasValue ? categoryNamesById.GetValueOrDefault(report.CategoryId.Value) : null,
                report.GetEmbeddingVector(),
                report.GetImageEmbeddingVector(),
                report.GetMetadataEmbeddingVector())).ToList();
        }
    }
}
