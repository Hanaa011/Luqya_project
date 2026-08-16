using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LostFound.AI.Caching;
using LostFound.AI.Configuration;
using LostFound.AI.Core;
using LostFound.AI.Diagnostics;
using LostFound.AI.Embeddings;
using LostFound.AI.Models;
using LostFound.AI.Runtime;
using LostFound.AI.Storage;

namespace LostFound.AI
{
    // Phase 2A Part 2 (Local AI Runtime) DI wiring. Called from
    // LostFoundApplicationModule.ConfigureServices AFTER AddLostFoundAiProviders
    // - registration order matters here: this method re-registers
    // IEmbeddingEngine with LocalFirstEmbeddingEngine, which (per standard
    // Microsoft.Extensions.DependencyInjection semantics) becomes the
    // instance GetRequiredService&lt;IEmbeddingEngine&gt;() resolves, shadowing
    // Part 1's plain provider-fallback registration for single-service
    // resolution without removing it - AiMatchingService/ReportMatchingBackgroundJob
    // (both already depending on the stable IEmbeddingEngine interface from
    // Part 1) automatically start getting local-first behavior with no
    // changes of their own.
    public static class LostFoundLocalAiRuntimeServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundLocalAiRuntime(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<LocalAiRuntimeOptions>(configuration.GetSection("LostFound:AI:LocalRuntime"));
            services.AddSingleton<IValidateOptions<LocalAiRuntimeOptions>, LocalAiRuntimeOptionsValidator>();

            services.AddSingleton<EmbeddingSqliteConnectionFactory>();
            services.AddSingleton<IEmbeddingDownloader, HttpEmbeddingDownloader>();

            services.AddSingleton<EmbeddingModelManager>();
            services.AddSingleton<IEmbeddingModelManager>(sp => sp.GetRequiredService<EmbeddingModelManager>());
            services.AddSingleton<IEmbeddingVersionManager>(sp => sp.GetRequiredService<EmbeddingModelManager>());

            services.AddSingleton<IEmbeddingStore, SqliteEmbeddingStore>();
            services.AddSingleton<IEmbeddingCache, MemoryEmbeddingCache>();
            services.AddSingleton<IEmbeddingRuntimeDiagnostics, EmbeddingRuntimeDiagnostics>();

            // Singleton: owns a warm, loaded ONNX InferenceSession once a
            // model is installed - reloading it per-request would defeat
            // the entire point of "warm startup" from the spec's Model
            // Manager responsibilities.
            services.AddSingleton<IEmbeddingRuntime, OnnxEmbeddingRuntime>();

            var embeddingProviderName = LostFoundAiProvidersServiceCollectionExtensions.ResolveConfiguredEmbeddingProviderName(configuration);

            services.AddTransient<IEmbeddingEngine>(sp =>
            {
                var fallback = LostFoundAiProvidersServiceCollectionExtensions.CreateProviderFallbackEmbeddingEngine(sp, embeddingProviderName);

                return new LocalFirstEmbeddingEngine(
                    sp.GetRequiredService<IEmbeddingRuntime>(),
                    fallback,
                    sp.GetRequiredService<IEmbeddingCache>(),
                    sp.GetRequiredService<IEmbeddingStore>(),
                    sp.GetRequiredService<IEmbeddingRuntimeDiagnostics>(),
                    sp.GetRequiredService<ILogger<LocalFirstEmbeddingEngine>>());
            });

            return services;
        }
    }
}
