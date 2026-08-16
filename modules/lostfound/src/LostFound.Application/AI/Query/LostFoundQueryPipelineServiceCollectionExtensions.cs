using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LostFound.AI
{
    // Phase 2B Part 1 (Query Understanding & Semantic Pipeline) DI wiring.
    // Must be registered AFTER AddLostFoundLocalAiRuntime (IEmbeddingEngine),
    // AddLostFoundKnowledgeGraph (IConceptRepository/IKnowledgeGraph/
    // LanguageNormalizerRegistry), and AddLostFoundDatasetImporters
    // (IDatasetImportHistoryRepository/IDatasetImporter) - MemoryQueryCache
    // and QueryPipeline depend on services each of those registers.
    public static class LostFoundQueryPipelineServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundQueryPipeline(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<Configuration.QueryPipelineOptions>(configuration.GetSection("LostFound:AI:QueryPipeline"));

            services.AddSingleton<Query.ILanguageDetector, Query.HeuristicLanguageDetector>();
            services.AddSingleton<Query.ITextNormalizer, Query.TextNormalizer>();
            services.AddSingleton<Query.ITokenizer, Query.SimpleTokenizer>();
            services.AddSingleton<Query.IMorphologyService, Query.MorphologyService>();
            services.AddSingleton<Query.ITransliterationService, Query.TransliterationService>();
            services.AddSingleton<Query.IIntentDetector, Query.IntentDetector>();
            services.AddSingleton<Query.ISpellCorrectionService, Query.DictionarySpellCorrectionService>();
            services.AddSingleton<Query.IEntityRecognizer, Query.EntityRecognizer>();
            services.AddSingleton<Query.ISemanticExpander, Query.SemanticExpander>();
            services.AddSingleton<Query.ISemanticQueryBuilder, Query.SemanticQueryBuilder>();
            services.AddSingleton<Query.IQueryCache, Query.MemoryQueryCache>();
            services.AddSingleton<Query.IQueryPipeline, Query.QueryPipeline>();

            return services;
        }
    }
}
