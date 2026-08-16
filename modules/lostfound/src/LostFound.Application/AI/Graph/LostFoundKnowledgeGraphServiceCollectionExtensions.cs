using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using LostFound.AI.Caching;
using LostFound.AI.Concepts;
using LostFound.AI.Configuration;
using LostFound.AI.Graph;
using LostFound.AI.Languages;
using LostFound.AI.Storage;

namespace LostFound.AI
{
    // Phase 2A Part 3 (Semantic Knowledge Platform) DI wiring. Called from
    // LostFoundApplicationModule.ConfigureServices. ConceptResolver (below)
    // now also depends on IEmbeddingEngine, registered by
    // AddLostFoundAiProviders/AddLostFoundLocalAiRuntime - but .NET's DI
    // container resolves constructor dependencies lazily against the fully
    // built container, not registration order, so which of these extension
    // methods runs first still doesn't matter, as long as both run before
    // the service provider is built (they do - both are called from the
    // same ConfigureServices).
    public static class LostFoundKnowledgeGraphServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundKnowledgeGraph(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<KnowledgeGraphOptions>(configuration.GetSection("LostFound:AI:KnowledgeGraph"));
            services.AddSingleton<IValidateOptions<KnowledgeGraphOptions>, KnowledgeGraphOptionsValidator>();

            services.AddSingleton<KnowledgeSqliteConnectionFactory>();

            // MS.DI aggregates every ILanguageNormalizer registration below
            // into the IEnumerable<ILanguageNormalizer> LanguageNormalizerRegistry
            // asks for - adding a language is one more line here, nothing else.
            services.AddSingleton<ILanguageNormalizer, ArabicLanguageNormalizer>();
            services.AddSingleton<ILanguageNormalizer, EnglishLanguageNormalizer>();
            services.AddSingleton<ILanguageNormalizer, UrduLanguageNormalizer>();
            services.AddSingleton<LanguageNormalizerRegistry>();
            services.AddSingleton<IConceptNormalizer, ConceptNormalizer>();

            // IConceptRepository resolves to the cached decorator wrapping
            // the real SQLite repository - see CachedConceptRepository and
            // IConceptCache (Phase 2A Part 5's "Concept Cache" layer).
            services.AddSingleton<SqliteConceptRepository>();
            services.AddSingleton<IConceptCache, MemoryConceptCache>();
            services.AddSingleton<IConceptRepository>(sp =>
                new CachedConceptRepository(sp.GetRequiredService<SqliteConceptRepository>(), sp.GetRequiredService<IConceptCache>()));

            services.AddSingleton<IRelationshipRepository, SqliteRelationshipRepository>();

            // Singleton: owns the lazily-built in-memory alias index - see
            // InMemoryAliasResolver's doc comment.
            services.AddSingleton<IAliasResolver, InMemoryAliasResolver>();
            services.AddSingleton<IConceptResolver, ConceptResolver>();
            services.AddSingleton<IKnowledgeGraph, KnowledgeGraph>();

            return services;
        }
    }
}
