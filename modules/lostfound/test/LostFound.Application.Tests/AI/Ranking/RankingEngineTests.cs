using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LostFound.AI.Query;
using LostFound.AI.Retrieval;
using LostFound.Reports;
using Shouldly;
using Xunit;

namespace LostFound.AI.Ranking;

// Phase 2B Part 3 (Enterprise Ranking Engine).
public class RankingEngineTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    private static SearchableReport MakeReport(string? objectType = null, string? color = null, string? brand = null, IReadOnlyList<string>? tags = null) =>
        new(Guid.NewGuid(), ReportType.Lost, "test report", null, null, objectType, color, brand, tags ?? Array.Empty<string>(), null, null, null, null, null);

    private static FusedCandidate MakeCandidate(Guid reportId, params (string Strategy, double Score, int Rank)[] contributions) =>
        new(reportId, contributions.Sum(c => c.Score), contributions.Select(c => new StrategyContribution(c.Strategy, c.Score, c.Rank)).ToList());

    [Fact]
    public async Task Ranks_a_candidate_with_more_agreeing_signals_above_one_with_fewer()
    {
        var engine = GetRequiredService<IRankingEngine>();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var strongReport = MakeReport(objectType: "wallet", color: "black", brand: "fossil");
        var weakReport = MakeReport(objectType: "wallet");

        var strongCandidate = MakeCandidate(strongReport.ReportId,
            ("BM25", 8.0, 1), ("Exact", 2, 1), ("Color", 1.0, 1), ("Brand", 1.0, 1));
        var weakCandidate = MakeCandidate(weakReport.ReportId, ("BM25", 1.0, 5));

        var reportsById = new Dictionary<Guid, SearchableReport> { [strongReport.ReportId] = strongReport, [weakReport.ReportId] = weakReport };
        var query = await pipeline.ProcessAsync("black fossil wallet");

        var result = await engine.RankAsync(new[] { strongCandidate, weakCandidate }, reportsById, query);

        result.Results.Count.ShouldBe(2);
        result.Results[0].ReportId.ShouldBe(strongReport.ReportId);
        result.Results[0].Confidence.ShouldBeGreaterThan(result.Results[1].Confidence);
    }

    [Fact]
    public async Task Confidence_is_never_literally_equal_to_the_raw_weighted_score()
    {
        var engine = GetRequiredService<IRankingEngine>();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var report = MakeReport(objectType: "wallet");
        var candidate = MakeCandidate(report.ReportId, ("BM25", 12.0, 1), ("Exact", 3, 1));

        var reportsById = new Dictionary<Guid, SearchableReport> { [report.ReportId] = report };
        var query = await pipeline.ProcessAsync("wallet");

        var result = await engine.RankAsync(new[] { candidate }, reportsById, query);

        var ranked = result.Results.Single();
        // Structural check: confidence is a sigmoid-squashed, coverage-
        // adjusted transform, so it cannot equal any raw feature value or
        // the raw BM25/Exact contribution scores directly.
        ranked.Confidence.ShouldNotBe(candidate.FusedScore);
        ranked.Confidence.ShouldBeInRange(0.0, 100.0);
    }

    [Fact]
    public async Task Explanation_lists_the_strongest_matched_signals_and_evidence_flags()
    {
        var engine = GetRequiredService<IRankingEngine>();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var report = MakeReport(objectType: "wallet", color: "black");
        var candidate = MakeCandidate(report.ReportId, ("BM25", 10.0, 1), ("Color", 1.0, 1));

        var reportsById = new Dictionary<Guid, SearchableReport> { [report.ReportId] = report };
        var query = await pipeline.ProcessAsync("black wallet");

        var result = await engine.RankAsync(new[] { candidate }, reportsById, query);

        var explanation = result.Results.Single().Explanation;
        explanation.StrongestSignals.ShouldNotBeEmpty();
        explanation.FeatureContributions.ShouldNotBeEmpty();
        explanation.Summary.ShouldContain("Confidence");
    }

    [Fact]
    public async Task Reports_the_hybrid_ranking_fallback_tier_when_only_lexical_signals_are_present()
    {
        var engine = GetRequiredService<IRankingEngine>();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var report = MakeReport(objectType: "wallet", color: "black");
        var candidate = MakeCandidate(report.ReportId, ("BM25", 5.0, 1), ("Color", 1.0, 1));

        var reportsById = new Dictionary<Guid, SearchableReport> { [report.ReportId] = report };
        var query = await pipeline.ProcessAsync("black wallet");

        var result = await engine.RankAsync(new[] { candidate }, reportsById, query);

        // No Vector/Graph contributions were supplied, only BM25 + an
        // attribute strategy - the fallback ladder should report
        // HybridRanking, not ExternalAi/LocalEmbeddings/KnowledgeGraph.
        result.Diagnostics.FallbackTier.ShouldBe(FallbackTier.HybridRanking);
    }

    [Fact]
    public async Task Never_throws_when_the_candidate_set_is_empty()
    {
        var engine = GetRequiredService<IRankingEngine>();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var query = await pipeline.ProcessAsync("anything");

        var result = await engine.RankAsync(Array.Empty<FusedCandidate>(), new Dictionary<Guid, SearchableReport>(), query);

        result.Results.ShouldBeEmpty();
    }
}
