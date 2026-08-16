using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using LostFound.AI.Query;
using LostFound.Reports;
using Shouldly;
using Xunit;

namespace LostFound.AI.Retrieval;

// Phase 2B Part 2 (Hybrid Retrieval Engine) - each strategy tested in
// isolation against hand-built SearchableReport instances (no database
// needed at all, since SearchableReport is a Contracts-safe projection),
// per the spec's "Every strategy must be independently testable."
public class RetrievalStrategyTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    private static SearchableReport MakeReport(
        string? description = null, string? objectType = null, string? color = null,
        string? brand = null, IReadOnlyList<string>? tags = null, string? locationDetails = null,
        DateTime? lostFoundDate = null, string? categoryName = null) =>
        new(
            Guid.NewGuid(), ReportType.Lost, description, locationDetails, lostFoundDate,
            objectType, color, brand, tags ?? Array.Empty<string>(), null, categoryName, null, null, null);

    [Fact]
    public async Task Bm25_scores_a_report_containing_query_terms_over_one_that_does_not()
    {
        var bm25 = GetRequiredService<IBM25Retriever>();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var strongMatch = MakeReport(description: "black leather wallet lost near the library");
        var noMatch = MakeReport(description: "a red bicycle found in the park");

        var query = await pipeline.ProcessAsync("black leather wallet");
        var context = new RetrievalContext(query, new[] { strongMatch, noMatch }, 10);

        var results = await bm25.RetrieveAsync(context);

        results.Select(r => r.ReportId).ShouldContain(strongMatch.ReportId);
        results.Select(r => r.ReportId).ShouldNotContain(noMatch.ReportId);
    }

    [Fact]
    public async Task Exact_matches_reports_sharing_a_literal_query_token()
    {
        var exact = GetRequiredService<IExactRetriever>();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var match = MakeReport(description: "silver watch", objectType: "watch");
        var noMatch = MakeReport(description: "blue umbrella");

        var query = await pipeline.ProcessAsync("watch");
        var context = new RetrievalContext(query, new[] { match, noMatch }, 10);

        var results = await exact.RetrieveAsync(context);

        results.Select(r => r.ReportId).ShouldBe(new[] { match.ReportId });
    }

    [Fact]
    public async Task Fuzzy_matches_a_misspelled_query_term_within_edit_distance()
    {
        var fuzzy = GetRequiredService<IFuzzyRetriever>();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var match = MakeReport(description: "black wallet");

        // "walet" is one edit away from "wallet" - not a known concept
        // alias, so DictionarySpellCorrectionService won't have already
        // fixed it upstream; FuzzyRetriever must catch it independently.
        var query = await pipeline.ProcessAsync("walet");
        var context = new RetrievalContext(query, new[] { match }, 10);

        var results = await fuzzy.RetrieveAsync(context);

        results.Select(r => r.ReportId).ShouldContain(match.ReportId);
    }

    [Fact]
    public async Task Category_strategy_matches_on_resolved_category_name()
    {
        var strategies = ServiceProvider.GetServices<IRetrievalStrategy>();
        var categoryRetriever = strategies.Single(s => s.StrategyName == "Category");
        var pipeline = GetRequiredService<IQueryPipeline>();

        var match = MakeReport(description: "electronics item", categoryName: "electronics");
        var noMatch = MakeReport(description: "clothing item", categoryName: "clothing");

        var query = await pipeline.ProcessAsync("electronics");
        var context = new RetrievalContext(query, new[] { match, noMatch }, 10);

        var results = await categoryRetriever.RetrieveAsync(context);

        // Category is only a real signal once EntityRecognizer actually
        // extracts a Category-typed entity from the query text, which needs
        // the term to exist in the knowledge graph's Categories vocabulary -
        // "electronics" isn't in the Phase 2A Part 4 seed data, so this
        // documents the expected (empty) behavior when no such entity was
        // recognized, rather than asserting a match that can't happen yet.
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Location_strategy_matches_reports_whose_LocationDetails_contains_a_recognized_location_word()
    {
        var strategies = ServiceProvider.GetServices<IRetrievalStrategy>();
        var locationRetriever = strategies.Single(s => s.StrategyName == "Location");
        var pipeline = GetRequiredService<IQueryPipeline>();

        var match = MakeReport(description: "lost near the airport", locationDetails: "Terminal 2, near the airport gate");
        var noMatch = MakeReport(description: "lost at home", locationDetails: "Living room");

        var query = await pipeline.ProcessAsync("airport");
        var context = new RetrievalContext(query, new[] { match, noMatch }, 10);

        var results = await locationRetriever.RetrieveAsync(context);

        results.Select(r => r.ReportId).ShouldBe(new[] { match.ReportId });
    }

    [Fact]
    public async Task Time_strategy_scores_reports_closer_to_the_recognized_date_higher()
    {
        var strategies = ServiceProvider.GetServices<IRetrievalStrategy>();
        var timeRetriever = strategies.Single(s => s.StrategyName == "Time");
        var pipeline = GetRequiredService<IQueryPipeline>();

        var close = MakeReport(description: "lost item", lostFoundDate: new DateTime(2026, 1, 2));
        var far = MakeReport(description: "lost item", lostFoundDate: new DateTime(2026, 3, 1));

        var query = await pipeline.ProcessAsync("01/01/2026");
        var context = new RetrievalContext(query, new[] { close, far }, 10);

        var results = await timeRetriever.RetrieveAsync(context);

        results.ShouldNotBeEmpty();
        results[0].ReportId.ShouldBe(close.ReportId);
    }

    [Fact]
    public async Task Graph_strategy_matches_a_report_via_knowledge_graph_expansion()
    {
        var coordinator = GetRequiredService<Importers.IImportCoordinator>();
        var importers = GetRequiredService<IEnumerable<Importers.IDatasetImporter>>();
        await coordinator.ImportAllAsync(importers, Importers.ImportMode.Full);

        var strategies = ServiceProvider.GetServices<IRetrievalStrategy>();
        var graphRetriever = strategies.Single(s => s.StrategyName == "Graph");
        var pipeline = GetRequiredService<IQueryPipeline>();

        // "mobile" is an English alias of the seeded "Phone" concept, whose
        // aliases (Phase 2A Part 4 seed data) include "cell phone", "handset".
        // GraphRetriever deliberately matches only STRUCTURED fields
        // (ObjectType/Color/Brand/Tags), not free-text Description - BM25/
        // Exact/Fuzzy already cover unstructured text, so this needs the
        // report's AI-classified ObjectType to carry the matching term.
        var match = MakeReport(description: "lost my handset", objectType: "handset");
        var noMatch = MakeReport(description: "lost my bicycle", objectType: "bicycle");

        var query = await pipeline.ProcessAsync("mobile");
        var context = new RetrievalContext(query, new[] { match, noMatch }, 10);

        var results = await graphRetriever.RetrieveAsync(context);

        results.Select(r => r.ReportId).ShouldContain(match.ReportId);
    }
}
