using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LostFound.AI
{
    // Phase 2B Part 4 (Production Integration) DI wiring. Must be
    // registered AFTER AddLostFoundQueryPipeline, AddLostFoundHybridRetrieval,
    // AddLostFoundRankingEngine, and AddLostFoundKnowledgeGraph -
    // SemanticSearchOrchestrator and ObjectTypeCompatibilityService depend
    // on services each of those registers.
    public static class LostFoundProductionIntegrationServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundProductionIntegration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<Configuration.HybridPipelineOptions>(
                configuration.GetSection("LostFound:AI:HybridPipeline"));

            services.AddSingleton<Ontology.IObjectTypeCompatibilityService, Ontology.ObjectTypeCompatibilityService>();

            services.AddSingleton<Analytics.ISearchAnalyticsRecorder, Analytics.InMemorySearchAnalyticsRecorder>();
            services.AddSingleton<Analytics.ISearchQualityMetricsCalculator, Analytics.SearchQualityMetricsCalculator>();

            services.AddSingleton<Integration.ISemanticSearchOrchestrator, Integration.SemanticSearchOrchestrator>();

            // Domain-defined seam (see LostFound.Matches.IMatchRankingService)
            // implemented here, in Application, because it depends on
            // ISemanticSearchOrchestrator registered immediately above -
            // gives MatchManager (Domain) access to the exact same ranking
            // pipeline Search uses without Domain depending on Application.
            services.AddSingleton<Matches.IMatchRankingService, Matches.SearchBasedMatchRankingService>();

            return services;
        }
    }
}
