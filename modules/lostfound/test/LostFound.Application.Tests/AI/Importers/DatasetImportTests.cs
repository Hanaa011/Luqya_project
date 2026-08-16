using System.Linq;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Graph;
using LostFound.AI.Importers;
using Shouldly;
using Xunit;

namespace LostFound.AI.Importers;

// Phase 2A Part 4 (Dataset Importers), exercised against the one real
// importer (LostFoundSeedDataImporter) end-to-end through the actual
// production DI wiring - not a mock pipeline.
public class DatasetImportTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    [Fact]
    public async Task Importing_the_seed_dataset_merges_per_language_records_into_one_concept_each()
    {
        var coordinator = GetRequiredService<IImportCoordinator>();
        var importers = GetRequiredService<System.Collections.Generic.IEnumerable<IDatasetImporter>>().ToList();

        var report = await coordinator.ImportAllAsync(importers, ImportMode.Full);

        // PHASE-VALIDATION-08: two importers are active in tests now -
        // the hand-curated seed fixture and the data-driven JSON lexicon
        // (WikidataDatasetImporter is excluded from the test module, since
        // it makes a real live network call - see LostFoundApplicationTestModule).
        report.DatasetResults.Count.ShouldBe(2);
        var result = report.DatasetResults.Single(r => r.DatasetName == "lostfound-seed");

        result.Status.ShouldBe(DatasetImportStatus.Succeeded);
        result.ValidationFailureCount.ShouldBe(0);
        // PHASE-VALIDATION-07 added 38 Brand/Color/Material concepts (15 + 15 + 8)
        // to the 26 pre-existing object-type concepts. 64 seed entries x 3
        // languages (en/ar/ur) = 192 raw records merging down to 64 canonical
        // concepts.
        result.ConceptCount.ShouldBe(64);
        result.DuplicateGroupCount.ShouldBe(128); // 192 raw records - 64 concepts

        // JsonLexiconDatasetImporter now bundles two files:
        // lexicon-electronics-brands.json (11 original object-type concepts
        // + 4 Brand concepts + Remote/Mouse added by the
        // Arabic-Classification-Validation fixes = 17) and
        // lexicon-personal-items.json (Pen + Ring + Bank Card + Visa/
        // Mastercard/Mada added by the Arabic-E2E-Matching-Validation fix
        // = 6) - 23 total, each with en/ar/ur names, minus 3 IsA
        // relationships (DSLR/Mirrorless/Action Camera -> Camera; car-key/
        // remote-key/ring's parentKey references a concept defined in a
        // SEPARATE dataset file, so those relationships are not built - a
        // documented, acceptable cross-file limitation, not a defect).
        var lexiconResult = report.DatasetResults.Single(r => r.DatasetName == "lostfound-lexicon");
        lexiconResult.Status.ShouldBe(DatasetImportStatus.Succeeded);
        lexiconResult.ValidationFailureCount.ShouldBe(0);
        lexiconResult.ConceptCount.ShouldBe(23);
        lexiconResult.RelationshipCount.ShouldBe(3);

        var resolver = GetRequiredService<IConceptResolver>();
        var phone = await resolver.ResolveAsync("mobile", "en");
        phone.ShouldNotBeNull();
        phone!.CanonicalName.ShouldBe("Phone");
        phone.LocalizedNames["ar"].ShouldBe("هاتف");
        phone.LocalizedNames["ur"].ShouldBe("فون");

        // The whole point of PHASE-VALIDATION-08: "Canon" now resolves via
        // its Arabic name, generically, with no code naming that brand.
        var canon = await resolver.ResolveAsync("كانون", "ar");
        canon.ShouldNotBeNull();
        canon!.CanonicalName.ShouldBe("Canon");

        // Arabic-Classification-Validation.md fixes: the four ontology gaps
        // that (combined with a Local-fallback classification failure)
        // produced the reported ObjectType="لقيت"-style bug are now closed.
        var remote = await resolver.ResolveAsync("ريموت تلفزيون", "ar");
        remote.ShouldNotBeNull();
        remote!.CanonicalName.ShouldBe("Remote");

        var mouse = await resolver.ResolveAsync("ماوس", "ar");
        mouse.ShouldNotBeNull();
        mouse!.CanonicalName.ShouldBe("Mouse");

        var pen = await resolver.ResolveAsync("قلم", "ar");
        pen.ShouldNotBeNull();
        pen!.CanonicalName.ShouldBe("Pen");

        var ring = await resolver.ResolveAsync("خاتم", "ar");
        ring.ShouldNotBeNull();
        ring!.CanonicalName.ShouldBe("Ring");

        // The container-vs-contained-item fix: "سماعة" (singular) and
        // "ايربودز" (Arabic transliteration of AirPods) now resolve
        // directly to Earbuds, instead of leaving only a container mention
        // (e.g. "حقيبة") as the sole genuine Object entity.
        var earbudsBySingular = await resolver.ResolveAsync("سماعة", "ar");
        earbudsBySingular.ShouldNotBeNull();
        earbudsBySingular!.CanonicalName.ShouldBe("Earbuds");

        var earbudsByTransliteration = await resolver.ResolveAsync("ايربودز", "ar");
        earbudsByTransliteration.ShouldNotBeNull();
        earbudsByTransliteration!.CanonicalName.ShouldBe("Earbuds");

        // Arabic-E2E-Matching-Validation fix: "بطاقة بنكية" (bank card) had
        // no ontology concept at all, so entity recognition never found it
        // and the local classification fallback chain ended up picking an
        // unrelated preposition/fragment as ObjectType for a real bank-card
        // report. Closing the gap the same way earlier phases did (data,
        // not code).
        var bankCard = await resolver.ResolveAsync("بطاقة بنكية", "ar");
        bankCard.ShouldNotBeNull();
        bankCard!.CanonicalName.ShouldBe("Bank Card");

        var visa = await resolver.ResolveAsync("فيزا", "ar");
        visa.ShouldNotBeNull();
        visa!.CanonicalName.ShouldBe("Visa");
    }

    [Fact]
    public async Task Relationships_asserted_redundantly_across_languages_persist_exactly_once()
    {
        var coordinator = GetRequiredService<IImportCoordinator>();
        var importers = GetRequiredService<System.Collections.Generic.IEnumerable<IDatasetImporter>>().ToList();
        await coordinator.ImportAllAsync(importers, ImportMode.Full);

        var resolver = GetRequiredService<IConceptResolver>();
        var relationshipRepository = GetRequiredService<IRelationshipRepository>();

        var bag = (await resolver.ResolveAsync("Bag", "en"))!;
        var incoming = await relationshipRepository.GetByTargetAsync(bag.Id, RelationshipType.IsA);

        // Backpack/Handbag/Suitcase each assert "IsA Bag" once per language
        // (3x) plus once explicitly - RelationshipBuilder must collapse all
        // of that down to exactly one persisted edge per real relationship.
        incoming.Count.ShouldBe(3);
        incoming.Select(r => r.SourceConceptId).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task Incremental_reimport_of_an_unchanged_dataset_is_skipped_and_idempotent()
    {
        var coordinator = GetRequiredService<IImportCoordinator>();
        var importers = GetRequiredService<System.Collections.Generic.IEnumerable<IDatasetImporter>>().ToList();

        var firstResults = (await coordinator.ImportAllAsync(importers, ImportMode.Full)).DatasetResults;
        var secondResults = (await coordinator.ImportAllAsync(importers, ImportMode.Incremental)).DatasetResults;

        // PHASE-VALIDATION-08: two importers are active in tests now - both
        // must independently skip their own unchanged re-import.
        secondResults.Count.ShouldBe(2);
        secondResults.ShouldAllBe(r => r.Status == DatasetImportStatus.Skipped);

        var totalConceptsAfterFirstImport = firstResults.Sum(r => r.ConceptCount);
        var conceptRepository = GetRequiredService<IConceptRepository>();
        (await conceptRepository.GetAllAsync()).Count.ShouldBe(totalConceptsAfterFirstImport);
    }
}
