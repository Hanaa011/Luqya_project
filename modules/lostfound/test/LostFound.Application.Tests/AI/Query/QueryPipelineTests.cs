using System.Linq;
using System.Threading.Tasks;
using LostFound.AI.Importers;
using Shouldly;
using Xunit;

namespace LostFound.AI.Query;

// Phase 2B Part 1 (Query Understanding & Semantic Pipeline), exercised
// against the real Phase 2A Part 4 seed dataset so concept resolution and
// knowledge-graph expansion have real data to work with.
public class QueryPipelineTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    private async Task SeedKnowledgeGraphAsync()
    {
        var coordinator = GetRequiredService<IImportCoordinator>();
        var importers = GetRequiredService<System.Collections.Generic.IEnumerable<IDatasetImporter>>();
        await coordinator.ImportAllAsync(importers, ImportMode.Full);
    }

    [Fact]
    public async Task Detects_lost_intent_and_resolves_the_object_concept_in_English()
    {
        await SeedKnowledgeGraphAsync();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var result = await pipeline.ProcessAsync("I lost my mobile phone");

        result.LanguageCode.ShouldBe("en");
        result.Intent.ShouldBe(QueryIntent.LostItem);
        result.ResolvedConceptId.ShouldNotBeNull();
        result.ExpandedTerms.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Detects_found_intent_and_resolves_the_object_concept_in_Arabic()
    {
        await SeedKnowledgeGraphAsync();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var result = await pipeline.ProcessAsync("لقيت محفظة");

        result.LanguageCode.ShouldBe("ar");
        result.Intent.ShouldBe(QueryIntent.FoundItem);
        result.ResolvedConceptId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Expands_a_resolved_concept_with_related_IsA_ancestors()
    {
        await SeedKnowledgeGraphAsync();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var result = await pipeline.ProcessAsync("Android Phone");

        result.ResolvedConceptId.ShouldNotBeNull();
        // Android Phone -[IsA]-> Smartphone -[IsA]-> Phone (Phase 2A Part 4
        // seed ontology) - expansion should surface at least one of these.
        result.ExpandedTerms.ShouldContain(t => t.Reason == "RelatedConcept");
    }

    [Fact]
    public async Task Repeated_identical_queries_are_served_from_the_query_cache()
    {
        await SeedKnowledgeGraphAsync();
        var pipeline = GetRequiredService<IQueryPipeline>();

        var first = await pipeline.ProcessAsync("lost wallet");
        var second = await pipeline.ProcessAsync("lost wallet");

        // Same SemanticQuery instance returned from cache, not recomputed.
        ReferenceEquals(first, second).ShouldBeTrue();
    }

    [Fact]
    public async Task Empty_input_returns_an_empty_semantic_query_without_throwing()
    {
        var pipeline = GetRequiredService<IQueryPipeline>();

        var result = await pipeline.ProcessAsync("   ");

        result.Intent.ShouldBe(QueryIntent.Unknown);
        result.Tokens.ShouldBeEmpty();
        result.FinalSemanticText.ShouldBe(string.Empty);
    }
}
