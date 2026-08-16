using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using LostFound.AI.Concepts;
using LostFound.AI.Configuration;
using LostFound.AI.Core;
using LostFound.AI.Importers;
using LostFound.AI.Integration;
using LostFound.AI.Ontology;
using LostFound.Categories;
using LostFound.Reports;

namespace LostFound.AI;

// Focused regression coverage for the tiers 1-4 generalized object-type-
// equivalence cascade (Category-equality gate -> existing ObjectTypeRelationship
// clusters -> ontology fallback -> Unknown) added to AiMatchingService, per
// Generalized-Concept-Equivalence-Cascade-Architecture-and-Validation-2026-08-07.md.
// Uses the REAL seeded ontology (IConceptResolver/IObjectTypeCompatibilityService,
// resolved from this test module's DI container) so tier 4 is exercised for
// real, not mocked - everything else (classification result, candidate
// repository, category repository, embeddings) is substituted so each test
// isolates exactly the object-type/category signal under test. Every
// candidate in a given test shares the identical embedding vector as the
// query, so TextScore is a fixed, identical baseline across candidates in
// that test - any score difference observed is caused entirely by the
// object-type penalty tier, nothing else.
public class AiMatchingServiceObjectTypeCascadeTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    private static readonly float[] SharedEmbedding = { 1f, 0f };

    private async Task SeedRealOntologyAsync()
    {
        var coordinator = GetRequiredService<IImportCoordinator>();
        var importers = GetRequiredService<IEnumerable<IDatasetImporter>>();
        await coordinator.ImportAllAsync(importers, ImportMode.Full);
    }

    private AiMatchingService BuildService(
        string? queryObjectType,
        string? queryCategoryName,
        IReadOnlyDictionary<string, Guid> categoryIdsByName,
        List<Report> candidates)
    {
        var embeddingEngine = Substitute.For<IEmbeddingEngine>();
        embeddingEngine.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SharedEmbedding);

        var classificationEngine = Substitute.For<IClassificationEngine>();
        classificationEngine.ClassifyAsync(Arg.Any<string?>(), Arg.Any<byte[]?>(), Arg.Any<CancellationToken>())
            .Returns(new ItemClassificationResult
            {
                ObjectType = queryObjectType,
                CategoryName = queryCategoryName
            });

        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.GetSearchableReportsAsync(Arg.Any<ReportType?>()).Returns(candidates);

        var categoryRepository = Substitute.For<ICategoryRepository>();
        foreach (var pair in categoryIdsByName)
        {
            categoryRepository.FindByNameAsync(pair.Key).Returns(new Category(pair.Value, pair.Key));
        }

        var options = Options.Create(new HybridPipelineOptions { Enabled = false });

        return new AiMatchingService(
            embeddingEngine,
            classificationEngine,
            reportRepository,
            categoryRepository,
            GetRequiredService<IConceptResolver>(),
            GetRequiredService<IObjectTypeCompatibilityService>(),
            Substitute.For<ISemanticSearchOrchestrator>(),
            options,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<AiMatchingService>>());
    }

    private static Report BuildCandidate(string description, string objectType, Guid? categoryId)
    {
        var report = new Report(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReportType.Found, description);
        report.ApplyAiClassification(categoryId, objectType, null, null, null);
        report.SetEmbedding(SharedEmbedding);
        return report;
    }

    [Fact]
    public async Task Watch_and_Wristwatch_get_the_moderate_related_penalty_when_their_categories_match()
    {
        await SeedRealOntologyAsync();

        var watchesCategoryId = Guid.NewGuid();
        var otherCategoryId = Guid.NewGuid();

        var sameCategory = BuildCandidate("Found a watch on the bench", "Watch", watchesCategoryId);
        var differentCategory = BuildCandidate("Found a watch on the bench", "Watch", otherCategoryId);

        var service = BuildService(
            queryObjectType: "Wristwatch",
            queryCategoryName: "Watches",
            categoryIdsByName: new Dictionary<string, Guid> { ["Watches"] = watchesCategoryId },
            candidates: new List<Report> { sameCategory, differentCategory });

        var results = await service.FindSimilarReportsAsync("wristwatch", null, null, 10);

        var sameCategoryResult = results.SingleOrDefault(r => r.ReportId == sameCategory.Id);
        var differentCategoryResult = results.SingleOrDefault(r => r.ReportId == differentCategory.Id);

        sameCategoryResult.ShouldNotBeNull("the same-category candidate should survive scoring (Category-equality gate -> moderate penalty, not the harsher default).");
        sameCategoryResult!.ScorePercentage.ShouldBeGreaterThan(differentCategoryResult?.ScorePercentage ?? 0);
    }

    [Fact]
    public async Task Duffel_Bag_and_Gym_Bag_get_the_moderate_related_penalty_when_their_categories_match()
    {
        await SeedRealOntologyAsync();

        var bagsCategoryId = Guid.NewGuid();
        var otherCategoryId = Guid.NewGuid();

        var sameCategory = BuildCandidate("Found a gym bag at the gym", "Gym Bag", bagsCategoryId);
        var differentCategory = BuildCandidate("Found a gym bag at the gym", "Gym Bag", otherCategoryId);

        var service = BuildService(
            queryObjectType: "Duffel Bag",
            queryCategoryName: "Bags",
            categoryIdsByName: new Dictionary<string, Guid> { ["Bags"] = bagsCategoryId },
            candidates: new List<Report> { sameCategory, differentCategory });

        var results = await service.FindSimilarReportsAsync("duffel bag", null, null, 10);

        var sameCategoryResult = results.SingleOrDefault(r => r.ReportId == sameCategory.Id);
        var differentCategoryResult = results.SingleOrDefault(r => r.ReportId == differentCategory.Id);

        sameCategoryResult.ShouldNotBeNull();
        sameCategoryResult!.ScorePercentage.ShouldBeGreaterThan(differentCategoryResult?.ScorePercentage ?? 0);
    }

    [Fact]
    public async Task Passport_and_Travel_Document_get_the_moderate_related_penalty_when_their_categories_match()
    {
        await SeedRealOntologyAsync();

        var documentsCategoryId = Guid.NewGuid();
        var otherCategoryId = Guid.NewGuid();

        var sameCategory = BuildCandidate("Found a travel document at the airport", "Travel Document", documentsCategoryId);
        var differentCategory = BuildCandidate("Found a travel document at the airport", "Travel Document", otherCategoryId);

        var service = BuildService(
            queryObjectType: "Passport",
            queryCategoryName: "Documents",
            categoryIdsByName: new Dictionary<string, Guid> { ["Documents"] = documentsCategoryId },
            candidates: new List<Report> { sameCategory, differentCategory });

        var results = await service.FindSimilarReportsAsync("passport", null, null, 10);

        var sameCategoryResult = results.SingleOrDefault(r => r.ReportId == sameCategory.Id);
        var differentCategoryResult = results.SingleOrDefault(r => r.ReportId == differentCategory.Id);

        sameCategoryResult.ShouldNotBeNull();
        sameCategoryResult!.ScorePercentage.ShouldBeGreaterThan(differentCategoryResult?.ScorePercentage ?? 0);
    }

    // Deliberately does NOT set a matching category for either side, so tier 2
    // never fires and tier 3 (the existing, untouched ObjectTypeRelationship
    // "mobile" cluster, which already groups Phone/Smartphone/Mobile) is the
    // tier that actually resolves this pair - isolating exactly the
    // regression risk called out before implementation: this cascade must
    // NOT promote a parent/child pair to a full (Same-tier) match, only ever
    // reuse the existing moderate RelatedCluster treatment.
    [Fact]
    public async Task Phone_and_Smartphone_stay_at_the_existing_moderate_related_tier_never_promoted_to_an_exact_match()
    {
        await SeedRealOntologyAsync();

        var exactMatch = BuildCandidate("Found a phone near the entrance", "Phone", null);
        var parentChild = BuildCandidate("Found a smartphone near the entrance", "Smartphone", null);
        var unrelated = BuildCandidate("Found a bicycle near the entrance", "Bicycle", null);

        var service = BuildService(
            queryObjectType: "Phone",
            queryCategoryName: null,
            categoryIdsByName: new Dictionary<string, Guid>(),
            candidates: new List<Report> { exactMatch, parentChild, unrelated });

        var results = await service.FindSimilarReportsAsync("phone", null, null, 10);

        var exactMatchResult = results.SingleOrDefault(r => r.ReportId == exactMatch.Id);
        var parentChildResult = results.SingleOrDefault(r => r.ReportId == parentChild.Id);
        var unrelatedResult = results.SingleOrDefault(r => r.ReportId == unrelated.Id);

        exactMatchResult.ShouldNotBeNull();
        parentChildResult.ShouldNotBeNull("Phone vs Smartphone must still resolve to the moderate RelatedCluster tier, not be dropped as unrelated.");

        // Never promoted to Same: an exact "Phone" match must still outrank "Smartphone".
        exactMatchResult!.ScorePercentage.ShouldBeGreaterThan(parentChildResult!.ScorePercentage);

        // Never demoted to Unrelated either: Smartphone must still comfortably
        // outrank a genuinely unrelated candidate, which this cascade (like
        // the pre-existing behavior) filters out of the results entirely.
        unrelatedResult.ShouldBeNull("a genuinely unrelated candidate (different static cluster, no category/ontology signal) should be filtered out, exactly as before this change.");
    }

    [Fact]
    public async Task Genuinely_unrelated_pairs_are_still_filtered_out_not_rescued_by_the_new_cascade_tiers()
    {
        await SeedRealOntologyAsync();

        var walletsCategoryId = Guid.NewGuid();
        var vehiclesCategoryId = Guid.NewGuid();

        var unrelated = BuildCandidate("Found a bicycle near the mall", "Bicycle", vehiclesCategoryId);

        var service = BuildService(
            queryObjectType: "Wallet",
            queryCategoryName: "Wallets",
            categoryIdsByName: new Dictionary<string, Guid> { ["Wallets"] = walletsCategoryId },
            candidates: new List<Report> { unrelated });

        var results = await service.FindSimilarReportsAsync("wallet", null, null, 10);

        results.ShouldNotContain(r => r.ReportId == unrelated.Id);
    }
}
