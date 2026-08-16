using System.Linq;
using System.Threading.Tasks;
using LostFound.AI;
using LostFound.AI.Importers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace LostFound;

[DependsOn(
    typeof(LostFoundDomainModule),
    typeof(LostFoundApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
    )]
public class LostFoundApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<LostFoundApplicationModule>();

        context.Services.AddLostFoundAiProviders(
            context.Services.GetConfiguration());
        context.Services.AddLostFoundLocalAiRuntime(
            context.Services.GetConfiguration());
        context.Services.AddLostFoundKnowledgeGraph(
            context.Services.GetConfiguration());
        context.Services.AddLostFoundDatasetImporters();
        context.Services.AddLostFoundQueryPipeline(
            context.Services.GetConfiguration());
        context.Services.AddLostFoundHybridRetrieval(
            context.Services.GetConfiguration());
        context.Services.AddLostFoundRankingEngine(
            context.Services.GetConfiguration());
        context.Services.AddLostFoundProductionIntegration(
            context.Services.GetConfiguration());
        context.Services.AddLostFoundAiDiagnostics();
    }

    // Phase 2A Part 4 built a complete, real, fully-offline dataset importer
    // (LostFoundSeedDataImporter) and import pipeline (IImportCoordinator),
    // but nothing ever called it in the running application - the knowledge
    // graph was silently empty in every environment (verified during
    // PHASE-VALIDATION-04: 0 concepts, 0 relationships), which meant
    // ontology/alias/graph-expansion signals could never contribute to
    // ranking regardless of query. ImportMode.Incremental makes this safe to
    // run on every startup: IImportCoordinator already skips re-importing an
    // unchanged dataset version (see ImportCoordinator.ImportAsync), so this
    // is a fast no-op after the first successful run, not a rebuild.
    public override async Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var coordinator = context.ServiceProvider.GetRequiredService<IImportCoordinator>();
        var importers = context.ServiceProvider.GetServices<IDatasetImporter>().ToList();
        var logger = context.ServiceProvider.GetRequiredService<ILogger<LostFoundApplicationModule>>();

        var report = await coordinator.ImportAllAsync(importers, ImportMode.Incremental);

        logger.LogInformation(
            "Knowledge graph dataset import: {Concepts} concept(s), {Relationships} relationship(s) across {Count} source(s).",
            report.TotalConceptsImported, report.TotalRelationshipsImported, importers.Count);
    }
}
