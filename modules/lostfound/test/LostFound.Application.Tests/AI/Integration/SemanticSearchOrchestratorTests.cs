using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using LostFound.AI.Analytics;
using LostFound.AI.Configuration;
using LostFound.AI.Ontology;
using LostFound.AI.Query;
using LostFound.AI.Ranking;
using LostFound.AI.Retrieval;
using LostFound.Categories;
using LostFound.Reports;
using Shouldly;
using Xunit;

namespace LostFound.AI.Integration;

// Phase 2B Part 4 (Production Integration) - the final assembly of every
// subsystem built across Phase 2A/2B into AiMatchingService's public
// RankedReportResult shape. IReportRepository/ICategoryRepository mocked
// with NSubstitute (no LocalDB in this environment, same pattern as
// Phase 2B Part 2's HybridSearchEngineTests); everything else real.
public class SemanticSearchOrchestratorTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    private static Report MakeReport(string description, string? objectType = null, string? color = null, float[]? embedding = null)
    {
        var report = new Report(
            id: Guid.NewGuid(),
            reporterId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            type: ReportType.Lost,
            description: description);

        report.ApplyAiClassification(null, objectType, color, null, null);
        report.SetEmbedding(embedding ?? new float[] { 0.1f, 0.2f, 0.3f });

        return report;
    }

    private HybridSearchEngine BuildHybridSearchEngine(IReportRepository reportRepository, ICategoryRepository categoryRepository) =>
        new(
            reportRepository,
            categoryRepository,
            ServiceProvider.GetServices<IRetrievalStrategy>(),
            GetRequiredService<IRetrievalPlanner>(),
            GetRequiredService<ICandidateGenerator>(),
            GetRequiredService<ICandidateMerger>(),
            GetRequiredService<IDuplicateResolver>(),
            GetRequiredService<IFusionEngine>(),
            GetRequiredService<IOptions<Configuration.RetrievalOptions>>(),
            GetRequiredService<Microsoft.Extensions.Logging.ILogger<HybridSearchEngine>>());

    private SemanticSearchOrchestrator BuildOrchestrator(IReportRepository reportRepository, ICategoryRepository categoryRepository) =>
        new(
            GetRequiredService<IQueryPipeline>(),
            BuildHybridSearchEngine(reportRepository, categoryRepository),
            GetRequiredService<IRankingEngine>(),
            GetRequiredService<ISearchAnalyticsRecorder>(),
            Options.Create(new HybridPipelineOptions()),
            GetRequiredService<Microsoft.Extensions.Logging.ILogger<SemanticSearchOrchestrator>>());

    [Fact]
    public async Task Returns_ranked_results_shaped_like_the_public_RankedReportResult_contract()
    {
        var wallet = MakeReport("Black leather wallet lost near the library", objectType: "wallet", color: "black");

        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.GetSearchableReportsAsync(Arg.Any<ReportType?>()).Returns(new List<Report> { wallet });
        var categoryRepository = Substitute.For<ICategoryRepository>();

        var orchestrator = BuildOrchestrator(reportRepository, categoryRepository);

        var results = await orchestrator.SearchAsync("black wallet", null, maxResults: 5);

        results.ShouldNotBeEmpty();
        results[0].ReportId.ShouldBe(wallet.Id);
        results[0].ScorePercentage.ShouldBeInRange(0.0, 100.0);
        results[0].MatchExplanation.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Never_throws_and_returns_an_empty_list_when_the_repository_has_no_candidates()
    {
        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.GetSearchableReportsAsync(Arg.Any<ReportType?>()).Returns(new List<Report>());
        var categoryRepository = Substitute.For<ICategoryRepository>();

        var orchestrator = BuildOrchestrator(reportRepository, categoryRepository);

        var results = await orchestrator.SearchAsync("anything", null, maxResults: 5);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Truncates_to_maxResults_even_when_more_candidates_are_ranked()
    {
        // Distinct (non-near-duplicate) embeddings - three reports with
        // identical embeddings would be correctly collapsed into one by
        // IDuplicateResolver's semantic-duplicate merge (see
        // DuplicateResolver.cs), which would defeat this test's purpose of
        // checking maxResults truncation over genuinely distinct candidates.
        var reports = new List<Report>
        {
            MakeReport("Black wallet found downtown", objectType: "wallet", color: "black", embedding: new float[] { 0.9f, 0.1f, 0.0f }),
            MakeReport("Black wallet lost at the mall", objectType: "wallet", color: "black", embedding: new float[] { 0.1f, 0.9f, 0.0f }),
            MakeReport("Black wallet found in a taxi", objectType: "wallet", color: "black", embedding: new float[] { 0.0f, 0.1f, 0.9f })
        };

        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.GetSearchableReportsAsync(Arg.Any<ReportType?>()).Returns(reports);
        var categoryRepository = Substitute.For<ICategoryRepository>();

        var orchestrator = BuildOrchestrator(reportRepository, categoryRepository);

        var results = await orchestrator.SearchAsync("black wallet", null, maxResults: 2);

        results.Count.ShouldBe(2);
    }
}
