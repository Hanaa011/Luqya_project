using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using LostFound.AI.Concepts;
using LostFound.AI.Configuration;
using LostFound.AI.Core;
using LostFound.AI.Integration;
using LostFound.AI.Ontology;
using LostFound.Categories;
using LostFound.Reports;
using Shouldly;
using Xunit;

namespace LostFound.AI;

// Phase 2B Part 4 - verifies ONLY the dispatch branch added to
// AiMatchingService.FindSimilarReportsAsync: which path (new
// ISemanticSearchOrchestrator vs legacy deterministic scoring) is chosen
// for a given flag/imageBytes/searchText combination. Does not re-verify
// legacy scoring correctness (already covered by whatever exercised
// AiMatchingService before this Part) or the orchestrator's own behavior
// (SemanticSearchOrchestratorTests) - purely the routing decision.
public class AiMatchingServiceHybridDispatchTests
{
    private static AiMatchingService BuildService(
        bool hybridEnabled,
        out ISemanticSearchOrchestrator orchestrator,
        out IReportRepository reportRepository)
    {
        var embeddingEngine = Substitute.For<IEmbeddingEngine>();
        embeddingEngine.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new float[] { 0.1f, 0.2f });
        embeddingEngine.GenerateImageEmbeddingAsync(Arg.Any<byte[]>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new float[] { 0.1f, 0.2f });

        var classificationEngine = Substitute.For<IClassificationEngine>();
        classificationEngine.ClassifyAsync(Arg.Any<string?>(), Arg.Any<byte[]?>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new ItemClassificationResult());

        reportRepository = Substitute.For<IReportRepository>();
        reportRepository.GetSearchableReportsAsync(Arg.Any<ReportType?>()).Returns(new List<Report>());

        orchestrator = Substitute.For<ISemanticSearchOrchestrator>();
        orchestrator.SearchAsync(Arg.Any<string>(), Arg.Any<ReportType?>(), Arg.Any<int>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<RankedReportResult> { new() { ReportId = Guid.NewGuid(), ScorePercentage = 42 } });

        var options = Options.Create(new HybridPipelineOptions { Enabled = hybridEnabled });

        return new AiMatchingService(
            embeddingEngine,
            classificationEngine,
            reportRepository,
            Substitute.For<ICategoryRepository>(),
            Substitute.For<IConceptResolver>(),
            Substitute.For<IObjectTypeCompatibilityService>(),
            orchestrator,
            options,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<AiMatchingService>>());
    }

    [Fact]
    public async Task Routes_text_only_search_to_the_orchestrator_when_the_flag_is_enabled()
    {
        var service = BuildService(hybridEnabled: true, out var orchestrator, out var reportRepository);

        var results = await service.FindSimilarReportsAsync("black wallet", null, null, 5);

        results.Count.ShouldBe(1);
        results[0].ScorePercentage.ShouldBe(42);
        await orchestrator.Received(1).SearchAsync("black wallet", null, 5, Arg.Any<System.Threading.CancellationToken>());
        await reportRepository.DidNotReceive().GetSearchableReportsAsync(Arg.Any<ReportType?>());
    }

    [Fact]
    public async Task Uses_the_legacy_path_for_image_based_search_even_when_the_flag_is_enabled()
    {
        var service = BuildService(hybridEnabled: true, out var orchestrator, out var reportRepository);

        await service.FindSimilarReportsAsync(null, new byte[] { 1, 2, 3 }, null, 5);

        await orchestrator.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default, default);
        await reportRepository.Received(1).GetSearchableReportsAsync(Arg.Any<ReportType?>());
    }

    [Fact]
    public async Task Uses_the_legacy_path_when_the_flag_is_disabled()
    {
        var service = BuildService(hybridEnabled: false, out var orchestrator, out var reportRepository);

        await service.FindSimilarReportsAsync("black wallet", null, null, 5);

        await orchestrator.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default, default);
        await reportRepository.Received(1).GetSearchableReportsAsync(Arg.Any<ReportType?>());
    }
}
