using Microsoft.Extensions.DependencyInjection;

namespace LostFound.AI
{
    // Phase 2A Part 4 (Dataset Importers) DI wiring. Depends on
    // AddLostFoundKnowledgeGraph (Part 3) having been called first -
    // IImportCoordinator writes through IKnowledgeGraph/IAliasResolver.
    public static class LostFoundImportersServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundDatasetImporters(this IServiceCollection services)
        {
            services.AddSingleton<Importers.IDataValidator, Importers.DataValidator>();
            services.AddSingleton<Importers.IDataNormalizer, Importers.DataNormalizer>();
            services.AddSingleton<Importers.IDeduplicationService, Importers.DeduplicationService>();
            services.AddSingleton<Importers.ICanonicalizer, Importers.Canonicalizer>();
            services.AddSingleton<Importers.IConceptBuilder, Importers.ConceptBuilder>();
            services.AddSingleton<Importers.IRelationshipBuilder, Importers.RelationshipBuilder>();
            services.AddSingleton<Importers.IDatasetImportHistoryRepository, Importers.SqliteDatasetImportHistoryRepository>();
            services.AddSingleton<Importers.IImportCoordinator, Importers.ImportCoordinator>();

            // Registering as IEnumerable<IDatasetImporter> (via repeated
            // AddSingleton<IDatasetImporter, TImpl>() calls) is deliberate -
            // adding ConceptNet/Wikidata/etc. later is one more line here,
            // never a change to IImportCoordinator or any pipeline stage.
            services.AddSingleton<Importers.IDatasetImporter, Importers.LostFoundSeedDataImporter>();

            // PHASE-VALIDATION-08: the ontology's real scaling path -
            // JsonLexiconDatasetImporter reads data-driven vocabulary from
            // embedded JSON (no hardcoded Entry() calls); WikidataDatasetImporter
            // is a real, live external source. Both are ordinary
            // IDatasetImporter registrations, run independently and in
            // parallel with the seed importer by IImportCoordinator.ImportAllAsync -
            // one failing (e.g. Wikidata unreachable) never affects the others.
            services.AddSingleton<Importers.IDatasetImporter, Importers.JsonLexiconDatasetImporter>();
            services.AddHttpClient(nameof(Importers.WikidataDatasetImporter));
            services.AddSingleton<Importers.IDatasetImporter, Importers.WikidataDatasetImporter>();

            return services;
        }
    }
}
