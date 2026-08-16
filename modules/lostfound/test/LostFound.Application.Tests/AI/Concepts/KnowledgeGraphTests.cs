using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Graph;
using Shouldly;
using Xunit;

namespace LostFound.AI.Concepts;

// Phase 2A Part 3 (Semantic Knowledge Platform).
public class KnowledgeGraphTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    [Fact]
    public async Task Resolves_a_concept_by_its_alias_and_by_diacritized_Arabic()
    {
        var graph = GetRequiredService<IKnowledgeGraph>();
        var resolver = GetRequiredService<IConceptResolver>();
        var aliasResolver = GetRequiredService<IAliasResolver>();

        var phoneId = Guid.NewGuid();
        await graph.AddConceptAsync(new Concept
        {
            Id = phoneId,
            CanonicalName = "Phone",
            LocalizedNames = new Dictionary<string, string> { ["en"] = "Phone", ["ar"] = "هاتف" },
            Aliases = new Dictionary<string, IReadOnlyList<string>>
            {
                ["en"] = new List<string> { "mobile" },
                ["ar"] = new List<string> { "موبايل" }
            },
            LanguageAvailability = new List<string> { "en", "ar" }
        });

        await aliasResolver.RebuildIndexAsync();

        (await resolver.ResolveAsync("mobile", "en"))!.Id.ShouldBe(phoneId);

        // Diacritized input ("هَاتِف" with tashkeel) must still match the
        // stored bare-form alias "هاتف" - proving ArabicLanguageNormalizer's
        // diacritic stripping is actually applied, not just present.
        (await resolver.ResolveAsync("هَاتِف", "ar"))!.Id.ShouldBe(phoneId);

        (await resolver.ResolveAsync("nonexistent-term-xyz", "en")).ShouldBeNull();
    }

    [Fact]
    public async Task Traverses_IsA_relationships_to_a_bounded_depth()
    {
        var graph = GetRequiredService<IKnowledgeGraph>();

        var phoneId = await graph.AddConceptAsync(new Concept { Id = Guid.NewGuid(), CanonicalName = "Phone" });
        var smartphoneId = await graph.AddConceptAsync(new Concept { Id = Guid.NewGuid(), CanonicalName = "Smartphone" });
        var androidId = await graph.AddConceptAsync(new Concept { Id = Guid.NewGuid(), CanonicalName = "Android Phone" });

        await graph.AddRelationshipAsync(smartphoneId, phoneId, RelationshipType.IsA);
        await graph.AddRelationshipAsync(androidId, smartphoneId, RelationshipType.IsA);

        var oneHop = await graph.GetRelatedConceptsAsync(androidId, new[] { RelationshipType.IsA }, maxDepth: 1);
        oneHop.Select(c => c.CanonicalName).ShouldBe(new[] { "Smartphone" });

        var twoHop = await graph.GetRelatedConceptsAsync(androidId, new[] { RelationshipType.IsA }, maxDepth: 2);
        twoHop.Select(c => c.CanonicalName).OrderBy(n => n).ShouldBe(new[] { "Phone", "Smartphone" });
    }

    [Fact]
    public async Task Rollback_restores_a_previous_version_and_records_history()
    {
        var repository = GetRequiredService<IConceptRepository>();
        var id = Guid.NewGuid();

        await repository.UpsertAsync(new Concept { Id = id, CanonicalName = "Wallet", Version = 1, PopularityScore = 0 });
        await repository.UpsertAsync(new Concept { Id = id, CanonicalName = "Wallet", Version = 2, PopularityScore = 42 });

        var history = await repository.GetHistoryAsync(id);
        history.Count.ShouldBe(1);
        history[0].Version.ShouldBe(1);

        (await repository.GetByIdAsync(id))!.PopularityScore.ShouldBe(42);

        await repository.RollbackAsync(id, 1);

        (await repository.GetByIdAsync(id))!.PopularityScore.ShouldBe(0);
    }
}
