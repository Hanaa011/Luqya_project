using System.Collections.Generic;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Importers;
using Xunit;
using Xunit.Abstractions;

namespace LostFound.AI.Ontology;

// TEMPORARY INVESTIGATIVE ARTIFACT - not a permanent regression suite.
// Written to answer a specific design question (Generalized-Object-Concept-
// Equivalence-Design, follow-up): "how does the EXISTING, already-built
// IConceptResolver/IObjectTypeCompatibilityService ontology layer actually
// behave against the real seeded production ontology (LostFoundSeedDataImporter
// + JsonLexiconDatasetImporter) for the exact synonym/alias/cross-lingual
// pairs the generalized-mechanism benchmark cares about?" No production code
// was changed to write or run this file. Results are transcribed into the
// design report; this file can be deleted or promoted to a permanent
// regression suite depending on which architecture option is chosen.
//
// Scope limitation (see LostFoundApplicationTestModule's own remarks): this
// test environment has no local embedding model installed and no route to an
// external provider, so IEmbeddingEngine cannot produce real vectors here.
// Every assertion below therefore only exercises the EXACT-alias-lookup and
// knowledge-graph-structural tiers of ConceptResolver/ObjectTypeCompatibilityService
// - never the semantic-similarity fallback tier. That is intentional and
// consistent with those tiers' own design (deterministic, embedding-independent);
// results for compound-phrase pairs that would only ever be caught by the
// semantic-similarity tier (e.g. "Duffel Bag" vs "Bag") are reported here as
// "does not resolve in this environment" and flagged in the design report as
// an open item requiring the real production embedding engine to validate.
public class GeneralizedConceptEquivalenceValidationTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    private readonly ITestOutputHelper _output;

    public GeneralizedConceptEquivalenceValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private async Task SeedRealOntologyAsync()
    {
        var coordinator = GetRequiredService<IImportCoordinator>();
        var importers = GetRequiredService<IEnumerable<IDatasetImporter>>();
        await coordinator.ImportAllAsync(importers, ImportMode.Full);
    }

    [Fact]
    public async Task Resolution_and_compatibility_matrix_against_the_real_seeded_ontology()
    {
        await SeedRealOntologyAsync();

        var resolver = GetRequiredService<IConceptResolver>();
        var compatibility = GetRequiredService<IObjectTypeCompatibilityService>();

        _output.WriteLine("=== RESOLUTION (single term -> concept or null) ===");
        await Resolve("Wallet", "en");
        await Resolve("Purse", "en");
        await Resolve("Wristwatch", "en");
        await Resolve("Watch", "en");
        await Resolve("ساعة", "ar");
        await Resolve("جوال", "ar");
        await Resolve("Smartphone", "en");
        await Resolve("Mobile Phone", "en");
        await Resolve("Duffel Bag", "en");
        await Resolve("Gym Bag", "en");
        await Resolve("Bag", "en");
        await Resolve("Passport", "en");
        await Resolve("Travel Document", "en");
        await Resolve("Vacuum Cleaner", "en");
        await Resolve("Sunglasses", "en");
        await Resolve("Rucksack", "en");
        await Resolve("Notebook Computer", "en");

        _output.WriteLine("");
        _output.WriteLine("=== COMPATIBILITY (query concept vs raw candidate text) ===");

        var wallet = await resolver.ResolveAsync("Wallet", "en");
        var watchFromArabic = await resolver.ResolveAsync("ساعة", "ar");
        var phone = await resolver.ResolveAsync("Phone", "en");
        var smartphone = await resolver.ResolveAsync("Smartphone", "en");

        await Classify("Wallet (query) vs 'Wallet'", wallet?.Id, "Wallet", "en", compatibility);
        await Classify("Wallet (query) vs 'Purse' (known EN alias)", wallet?.Id, "Purse", "en", compatibility);
        await Classify("Watch-from-Arabic (query) vs 'Watch' (cross-lingual, both known)", watchFromArabic?.Id, "Watch", "en", compatibility);
        await Classify("Watch-from-Arabic (query) vs 'Wristwatch' (flagship pair)", watchFromArabic?.Id, "Wristwatch", "en", compatibility);
        await Classify("Wallet (query) vs 'Bicycle' (genuinely unrelated, candidate NOT in ontology)", wallet?.Id, "Bicycle", "en", compatibility);
        await Classify("Watch (query) vs 'Passport' (genuinely unrelated, BOTH resolve in real ontology)", watchFromArabic?.Id, "Passport", "en", compatibility);
        await Classify("Phone (query, PARENT) vs 'Smartphone' (candidate, CHILD/IsA)", phone?.Id, "Smartphone", "en", compatibility);
        await Classify("Smartphone (query, CHILD) vs 'Phone' (candidate, PARENT/IsA)", smartphone?.Id, "Phone", "en", compatibility);
        await Classify("Wallet (query) vs 'Duffel Bag' (candidate unresolvable)", wallet?.Id, "Duffel Bag", "en", compatibility);
        await Classify("Watch-from-Arabic (query) vs 'Vacuum Cleaner' (no ontology concept at all)", watchFromArabic?.Id, "Vacuum Cleaner", "en", compatibility);

        return;

        async Task Resolve(string text, string lang)
        {
            var concept = await resolver.ResolveAsync(text, lang);
            _output.WriteLine(concept == null
                ? $"  '{text}' ({lang}) -> NULL (unresolved)"
                : $"  '{text}' ({lang}) -> '{concept.CanonicalName}' (id={concept.Id})");
        }
    }

    private async Task Classify(
        string label,
        System.Guid? queryConceptId,
        string candidateText,
        string lang,
        IObjectTypeCompatibilityService compatibility)
    {
        var result = await compatibility.ClassifyAsync(queryConceptId, candidateText, lang);
        _output.WriteLine($"  {label} => {result}");
    }
}
