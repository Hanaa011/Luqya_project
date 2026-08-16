using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using LostFound.AI.Configuration;
using LostFound.AI.Importers;

namespace LostFound;

[DependsOn(
    typeof(LostFoundApplicationModule),
    typeof(LostFoundDomainTestModule)
    )]
public class LostFoundApplicationTestModule : AbpModule
{
    private string? _testDataDirectory;

    // Phase 2A Part 5 ("Testing"): the AI subsystem's SQLite-backed stores
    // (Parts 2-4) default to relative paths under the host's content root.
    // Every xUnit test class gets a fresh AbpApplication (xUnit instantiates
    // the test class per [Fact]), so each one is repointed at its own
    // throwaway temp directory here - tests never share or leak state
    // through a real/relative database file.
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "lostfound-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDataDirectory);

        context.Services.PostConfigure<LocalAiRuntimeOptions>(options =>
        {
            options.ModelDirectory = Path.Combine(_testDataDirectory, "models");
            options.DatabasePath = Path.Combine(_testDataDirectory, "embeddings.db");
        });

        context.Services.PostConfigure<KnowledgeGraphOptions>(options =>
        {
            options.DatabasePath = Path.Combine(_testDataDirectory, "knowledge.db");
        });

        // No local embedding model is ever installed in this test
        // environment, and this sandbox has no route to the external
        // provider either (see the Phase 2A Part 1 report) - leaving Vector
        // retrieval enabled would make every test that exercises it spend
        // real wall-clock time on GeminiVisionHelper's retry/backoff before
        // failing. Retrieval-strategy tests that need Vector coverage
        // construct it directly instead of going through full DI.
        context.Services.PostConfigure<RetrievalOptions>(options =>
        {
            options.EnabledStrategies["Vector"] = true;
        });

        // PHASE-VALIDATION-08: same reasoning as the Vector-retrieval note
        // above, applied to dataset import - WikidataDatasetImporter makes a
        // real, live HTTP call to a public endpoint this sandbox may or may
        // not have a route to, and every xUnit test class boots a fresh
        // AbpApplication (see this class's own remarks), so leaving it
        // registered would make the ENTIRE suite's startup cost and
        // determinism depend on an external service's availability/latency
        // on every single test run. JsonLexiconDatasetImporter (fully
        // offline, deterministic) stays registered and exercised normally.
        var wikidataImporterDescriptor = context.Services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == typeof(IDatasetImporter) &&
            descriptor.ImplementationType == typeof(WikidataDatasetImporter));

        if (wikidataImporterDescriptor != null)
        {
            context.Services.Remove(wikidataImporterDescriptor);
        }
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        if (_testDataDirectory == null || !Directory.Exists(_testDataDirectory))
        {
            return;
        }

        // Microsoft.Data.Sqlite pools connections by default, keeping the
        // native SQLite file handle open past Dispose() - ClearAllPools()
        // forces every pooled connection closed so the temp directory can
        // actually be deleted. Best-effort: a stray handle from another
        // process should not fail the test itself, only leave the temp
        // directory behind for the OS to clean up later.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_testDataDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
