using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LostFound.AI
{
    // Phase 2B Part 2 (Hybrid Retrieval Engine) DI wiring. Must be
    // registered AFTER AddLostFoundLocalAiRuntime (IEmbeddingEngine) and
    // AddLostFoundQueryPipeline (ITokenizer, SemanticQuery's EntityType) -
    // HybridSearchEngine and several retrievers depend on services each of
    // those registers.
    public static class LostFoundHybridRetrievalServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundHybridRetrieval(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<Configuration.RetrievalOptions>(configuration.GetSection("LostFound:AI:Retrieval"));

            // Every IRetrievalStrategy implementation is registered under
            // its OWN specific interface (so it stays individually
            // resolvable/testable, per the spec) AND aggregated as
            // IRetrievalStrategy (MS.DI's standard IEnumerable<T>
            // aggregation) for HybridSearchEngine to enumerate.
            services.AddSingleton<Retrieval.IBM25Retriever, Retrieval.Bm25Retriever>();
            services.AddSingleton<Retrieval.IRetrievalStrategy>(sp => sp.GetRequiredService<Retrieval.IBM25Retriever>());

            services.AddSingleton<Retrieval.IVectorRetriever, Retrieval.VectorRetriever>();
            services.AddSingleton<Retrieval.IRetrievalStrategy>(sp => sp.GetRequiredService<Retrieval.IVectorRetriever>());

            // Multi-Representation Embedding change: second dense-retrieval
            // strategy over the independent metadata embedding - no
            // dedicated interface, same reasoning as Category/Brand/Color/
            // Material below (not one of the spec's original five).
            services.AddSingleton<Retrieval.IRetrievalStrategy, Retrieval.MetadataVectorRetriever>();

            services.AddSingleton<Retrieval.IGraphRetriever, Retrieval.GraphRetriever>();
            services.AddSingleton<Retrieval.IRetrievalStrategy>(sp => sp.GetRequiredService<Retrieval.IGraphRetriever>());

            services.AddSingleton<Retrieval.IFuzzyRetriever, Retrieval.FuzzyRetriever>();
            services.AddSingleton<Retrieval.IRetrievalStrategy>(sp => sp.GetRequiredService<Retrieval.IFuzzyRetriever>());

            services.AddSingleton<Retrieval.IExactRetriever, Retrieval.ExactRetriever>();
            services.AddSingleton<Retrieval.IRetrievalStrategy>(sp => sp.GetRequiredService<Retrieval.IExactRetriever>());

            // The remaining strategies have no dedicated interface (the
            // spec only names the five above) - registered as
            // IRetrievalStrategy directly.
            services.AddSingleton<Retrieval.IRetrievalStrategy, Retrieval.CategoryRetriever>();
            services.AddSingleton<Retrieval.IRetrievalStrategy, Retrieval.BrandRetriever>();
            services.AddSingleton<Retrieval.IRetrievalStrategy, Retrieval.ColorRetriever>();
            services.AddSingleton<Retrieval.IRetrievalStrategy, Retrieval.MaterialRetriever>();
            services.AddSingleton<Retrieval.IRetrievalStrategy, Retrieval.LocationRetriever>();
            services.AddSingleton<Retrieval.IRetrievalStrategy, Retrieval.TimeRetriever>();

            services.AddSingleton<Retrieval.IRetrievalPlanner, Retrieval.RetrievalPlanner>();
            services.AddSingleton<Retrieval.ICandidateGenerator, Retrieval.CandidateGenerator>();
            services.AddSingleton<Retrieval.ICandidateMerger, Retrieval.CandidateMerger>();
            services.AddSingleton<Retrieval.IDuplicateResolver, Retrieval.DuplicateResolver>();
            services.AddSingleton<Retrieval.IFusionEngine, Retrieval.FusionEngine>();
            services.AddSingleton<Retrieval.IHybridSearchEngine, Retrieval.HybridSearchEngine>();

            return services;
        }
    }
}
