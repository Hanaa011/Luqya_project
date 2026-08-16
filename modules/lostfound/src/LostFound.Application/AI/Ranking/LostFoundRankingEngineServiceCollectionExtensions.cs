using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LostFound.AI
{
    // Phase 2B Part 3 (Enterprise Ranking Engine) DI wiring. Must be
    // registered AFTER AddLostFoundLocalAiRuntime - RankingEngine depends
    // on IEmbeddingEngine to determine the fallback tier.
    public static class LostFoundRankingEngineServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundRankingEngine(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<Configuration.RankingOptions>(configuration.GetSection("LostFound:AI:Ranking"));

            services.AddSingleton<Ranking.IFeatureExtractor, Ranking.FeatureExtractor>();
            services.AddSingleton<Ranking.IScoreNormalizer, Ranking.ScoreNormalizer>();
            services.AddSingleton<Ranking.IWeightProvider, Ranking.WeightProvider>();
            services.AddSingleton<Ranking.ICrossEncoder, Ranking.NullCrossEncoder>();
            services.AddSingleton<Ranking.ILearningToRankEngine, Ranking.LinearLearningToRankEngine>();
            services.AddSingleton<Ranking.IConfidenceCalibrator, Ranking.ConfidenceCalibrator>();
            services.AddSingleton<Ranking.IExplanationGenerator, Ranking.ExplanationGenerator>();
            services.AddSingleton<Ranking.IAIFallbackOrchestrator, Ranking.AiFallbackOrchestrator>();
            services.AddSingleton<Ranking.IRankingEngine, Ranking.RankingEngine>();

            return services;
        }
    }
}
