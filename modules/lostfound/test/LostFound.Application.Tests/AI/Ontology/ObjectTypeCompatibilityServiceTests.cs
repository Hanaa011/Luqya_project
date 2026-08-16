using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Graph;
using Shouldly;
using Xunit;

namespace LostFound.AI.Ontology;

// Phase 2B Part 4 (Production Integration) - ontology-driven replacement
// for the legacy static ObjectTypeRelationship table, used only by the new
// hybrid pipeline path.
public class ObjectTypeCompatibilityServiceTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    private static Concept MakeConcept(string name) => new()
    {
        Id = Guid.NewGuid(),
        CanonicalName = name,
        LocalizedNames = new Dictionary<string, string> { ["en"] = name },
        LanguageAvailability = new List<string> { "en" }
    };

    [Fact]
    public async Task Classifies_the_same_concept_as_Same()
    {
        var graph = GetRequiredService<IKnowledgeGraph>();
        var aliasResolver = GetRequiredService<IAliasResolver>();
        var service = GetRequiredService<IObjectTypeCompatibilityService>();

        var wallet = MakeConcept("Wallet");
        var walletId = await graph.AddConceptAsync(wallet);
        await aliasResolver.RebuildIndexAsync();

        (await service.ClassifyAsync(walletId, "Wallet", "en")).ShouldBe(ObjectTypeCompatibility.Same);
    }

    [Fact]
    public async Task Classifies_two_unconnected_concepts_as_UnrelatedCluster()
    {
        var graph = GetRequiredService<IKnowledgeGraph>();
        var aliasResolver = GetRequiredService<IAliasResolver>();
        var service = GetRequiredService<IObjectTypeCompatibilityService>();

        var walletId = await graph.AddConceptAsync(MakeConcept("Wallet"));
        await graph.AddConceptAsync(MakeConcept("Bicycle"));
        await aliasResolver.RebuildIndexAsync();

        (await service.ClassifyAsync(walletId, "Bicycle", "en")).ShouldBe(ObjectTypeCompatibility.UnrelatedCluster);
    }

    [Fact]
    public async Task Classifies_a_parent_query_against_a_child_candidate_as_RelatedCluster()
    {
        var graph = GetRequiredService<IKnowledgeGraph>();
        var aliasResolver = GetRequiredService<IAliasResolver>();
        var service = GetRequiredService<IObjectTypeCompatibilityService>();

        var phoneId = await graph.AddConceptAsync(MakeConcept("Phone"));
        var smartphoneId = await graph.AddConceptAsync(MakeConcept("Smartphone"));

        // Conventionally recorded child -> parent (Smartphone IsA Phone),
        // per Phase 2A Part 3's own KnowledgeGraphTests.
        await graph.AddRelationshipAsync(smartphoneId, phoneId, RelationshipType.IsA);
        await aliasResolver.RebuildIndexAsync();

        // Query is the PARENT ("Phone"), candidate is the CHILD
        // ("Smartphone") - only reachable by also checking outward from the
        // candidate, since GetRelatedConceptsAsync only walks source -> target.
        (await service.ClassifyAsync(phoneId, "Smartphone", "en")).ShouldBe(ObjectTypeCompatibility.RelatedCluster);
    }

    [Fact]
    public async Task Returns_Unknown_when_the_query_concept_is_absent_or_the_candidate_type_cannot_be_resolved()
    {
        var graph = GetRequiredService<IKnowledgeGraph>();
        var aliasResolver = GetRequiredService<IAliasResolver>();
        var service = GetRequiredService<IObjectTypeCompatibilityService>();

        var walletId = await graph.AddConceptAsync(MakeConcept("Wallet"));
        await aliasResolver.RebuildIndexAsync();

        (await service.ClassifyAsync(null, "Wallet", "en")).ShouldBe(ObjectTypeCompatibility.Unknown);
        (await service.ClassifyAsync(walletId, null, "en")).ShouldBe(ObjectTypeCompatibility.Unknown);
        (await service.ClassifyAsync(walletId, "totally-unresolvable-xyz", "en")).ShouldBe(ObjectTypeCompatibility.Unknown);
    }
}
