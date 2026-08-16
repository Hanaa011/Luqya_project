using System.Linq;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Graph;
using LostFound.AI.Importers;
using Shouldly;
using Xunit;

namespace LostFound.AI.Diagnostics;

// Phase 2A Part 5 (Infrastructure, Storage, Caching).
public class AiPlatformDiagnosticsTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    [Fact]
    public async Task Reports_embedding_runtime_status_and_empty_storage_before_anything_is_written()
    {
        var diagnostics = GetRequiredService<IAiPlatformDiagnostics>();

        var report = await diagnostics.GetReportAsync();

        report.EmbeddingRuntime.RuntimeStatus.Health.ShouldBe(LostFound.AI.Runtime.EmbeddingRuntimeHealth.NotInstalled);
        report.ConceptCacheEntryCount.ShouldBe(0);
        report.Storage.Count.ShouldBe(2); // embeddings.db + knowledge.db
        report.LatestSuccessfulImports.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reports_storage_health_and_import_history_after_real_activity()
    {
        var graph = GetRequiredService<IKnowledgeGraph>();
        await graph.AddConceptAsync(new Concept { Id = System.Guid.NewGuid(), CanonicalName = "Diagnostics Probe" });

        var coordinator = GetRequiredService<IImportCoordinator>();
        var importers = GetRequiredService<System.Collections.Generic.IEnumerable<IDatasetImporter>>();
        await coordinator.ImportAllAsync(importers, ImportMode.Full);

        var diagnostics = GetRequiredService<IAiPlatformDiagnostics>();
        var report = await diagnostics.GetReportAsync();

        var knowledgeStore = report.Storage.Single(s => s.StoreName == "knowledge");
        knowledgeStore.FileExists.ShouldBeTrue();
        knowledgeStore.FileSizeBytes.ShouldNotBeNull();
        knowledgeStore.FileSizeBytes!.Value.ShouldBeGreaterThan(0);

        // PHASE-VALIDATION-08: two importers are active in tests
        // (WikidataDatasetImporter is excluded from the test module - see
        // LostFoundApplicationTestModule) - the seed fixture and the
        // data-driven JSON lexicon.
        report.LatestSuccessfulImports.Count.ShouldBe(2);
        report.LatestSuccessfulImports.ShouldContain(r => r.DatasetName == "lostfound-seed");
        report.LatestSuccessfulImports.ShouldContain(r => r.DatasetName == "lostfound-lexicon");
    }
}
