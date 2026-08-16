using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using LostFound.AI.Query;
using LostFound.Categories;
using LostFound.Reports;
using Shouldly;
using Xunit;

namespace LostFound.AI.Retrieval;

// Phase 2B Part 2 end-to-end: real Report domain entities (constructed
// directly - Report's public constructor needs no database, per Report.cs),
// IReportRepository/ICategoryRepository mocked with NSubstitute (this
// environment has no LocalDB - see the Phase 2A Part 1 report), everything
// else (query pipeline, knowledge graph, retrieval orchestration) real.
public class HybridSearchEngineTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    private static Report MakeReport(string description, string? objectType = null, string? color = null, string? brand = null, IReadOnlyList<string>? tags = null)
    {
        var report = new Report(
            id: Guid.NewGuid(),
            reporterId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            type: ReportType.Lost,
            description: description);

        report.ApplyAiClassification(null, objectType, color, brand, tags);
        report.SetEmbedding(new float[] { 0.1f, 0.2f, 0.3f });

        return report;
    }

    [Fact]
    public async Task Returns_a_relevant_candidate_with_full_provenance_and_diagnostics()
    {
        var wallet = MakeReport("Black leather wallet lost near the library", objectType: "wallet", color: "black", tags: new[] { "leather" });
        var bicycle = MakeReport("Red bicycle found in the park", objectType: "bicycle", color: "red");

        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.GetSearchableReportsAsync(Arg.Any<ReportType?>())
            .Returns(new List<Report> { wallet, bicycle });

        var categoryRepository = Substitute.For<ICategoryRepository>();
        categoryRepository.GetListAsync(includeDetails: Arg.Any<bool>(), cancellationToken: Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<Category>());

        var pipeline = GetRequiredService<IQueryPipeline>();
        var query = await pipeline.ProcessAsync("black wallet");

        var engine = new HybridSearchEngine(
            reportRepository,
            categoryRepository,
            ServiceProvider.GetServices<IRetrievalStrategy>(),
            GetRequiredService<IRetrievalPlanner>(),
            GetRequiredService<ICandidateGenerator>(),
            GetRequiredService<ICandidateMerger>(),
            GetRequiredService<IDuplicateResolver>(),
            GetRequiredService<IFusionEngine>(),
            GetRequiredService<Microsoft.Extensions.Options.IOptions<Configuration.RetrievalOptions>>(),
            GetRequiredService<Microsoft.Extensions.Logging.ILogger<HybridSearchEngine>>());

        var result = await engine.SearchAsync(query, null, maxResults: 10);

        result.Candidates.ShouldNotBeEmpty();
        result.Candidates[0].ReportId.ShouldBe(wallet.Id);
        result.Candidates[0].Contributions.ShouldNotBeEmpty();
        result.Candidates[0].Contributions.ShouldContain(c => c.StrategyName == "BM25" || c.StrategyName == "Exact" || c.StrategyName == "Color");

        result.Diagnostics.FinalCandidateCount.ShouldBe(result.Candidates.Count);
        result.Diagnostics.StrategyExecutionTimesMs.ShouldNotBeEmpty();
        // No strategy should hard-fail against well-formed input.
        result.Diagnostics.StrategyFailures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_never_throws_when_the_repository_returns_no_candidates()
    {
        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.GetSearchableReportsAsync(Arg.Any<ReportType?>()).Returns(new List<Report>());

        var categoryRepository = Substitute.For<ICategoryRepository>();

        var pipeline = GetRequiredService<IQueryPipeline>();
        var query = await pipeline.ProcessAsync("anything");

        var engine = new HybridSearchEngine(
            reportRepository,
            categoryRepository,
            ServiceProvider.GetServices<IRetrievalStrategy>(),
            GetRequiredService<IRetrievalPlanner>(),
            GetRequiredService<ICandidateGenerator>(),
            GetRequiredService<ICandidateMerger>(),
            GetRequiredService<IDuplicateResolver>(),
            GetRequiredService<IFusionEngine>(),
            GetRequiredService<Microsoft.Extensions.Options.IOptions<Configuration.RetrievalOptions>>(),
            GetRequiredService<Microsoft.Extensions.Logging.ILogger<HybridSearchEngine>>());

        var result = await engine.SearchAsync(query, null, maxResults: 10);

        result.Candidates.ShouldBeEmpty();
    }
}
