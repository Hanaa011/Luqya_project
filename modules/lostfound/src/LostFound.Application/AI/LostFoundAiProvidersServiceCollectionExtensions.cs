using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LostFound.AI.Core;
using LostFound.AI.Embeddings;
using LostFound.AI.Providers;

namespace LostFound.AI
{
    // Replaces the old separate "LostFoundAIModule" (previously in its own
    // LostFound.AI project). AI providers are now merged directly into
    // LostFound.Application, so this is a plain IServiceCollection
    // extension called once from LostFoundApplicationModule.ConfigureServices.
    //
    // Usage (in LostFoundApplicationModule.cs):
    //
    //   public override void ConfigureServices(ServiceConfigurationContext context)
    //   {
    //       context.Services.AddLostFoundAiProviders(context.Services.GetConfiguration());
    //       // ... rest of module configuration
    //   }
    //
    // ----------------------------------------------------------------------
    // WHAT CHANGED vs the previous version of this file (Phase 2A Part 1 -
    // Enterprise AI Foundation)
    // ----------------------------------------------------------------------
    // 1. The two per-capability switch statements that used to pick a
    //    concrete provider type by string are gone - provider registration
    //    is now a lookup into AiProviderRegistry, a single table of
    //    (name -> DI registration delegate) shared by both capabilities.
    //    Adding/removing a provider is now a one-line change in exactly one
    //    place instead of touching two switch statements.
    // 2. IEmbeddingProvider/IItemClassificationProvider are no longer the
    //    types callers depend on. AddLostFoundSemanticAiFoundation() layers
    //    the capability-based IEmbeddingEngine/IClassificationEngine on top
    //    of whichever provider was just registered - see
    //    LostFound.AI.Core.ClassificationEngine and
    //    LostFound.AI.Embeddings.ProviderFallbackEmbeddingEngine.
    //    AiMatchingService and ReportMatchingBackgroundJob now depend on
    //    these two interfaces exclusively.
    // 3. Providers that never had their own retry/backoff logic (everything
    //    except Gemini - see AiProviderRegistry.SelfResilientProviders) are
    //    now transparently wrapped in ResilientEmbeddingProvider /
    //    ResilientClassificationProvider before being exposed through the
    //    capability engines.
    // 4. IMatchExplanationProvider and every *MatchExplanationProvider
    //    implementation (Gemini/OpenAI/Ollama/DeepSeek/HuggingFace) remain
    //    REMOVED, unchanged from before - match explanations are still built
    //    locally/deterministically by LostFound.AI.MatchExplanationGenerator.
    // 5. "deepseek" is still classification-only; embeddings still resolve
    //    from the separate "LostFound:AI:EmbeddingProvider" setting when
    //    Provider is "DeepSeek" (defaults to "Gemini").
    // 6. Classification is no longer a single configured remote provider
    //    with one local fallback - IClassificationEngine now tries an
    //    ORDERED CHAIN of remote IItemClassificationProvider instances
    //    (configured primary, then Gemini as a dedicated second remote
    //    tier) before falling through to the local providers. Only a
    //    thrown exception (timeout, HTTP failure, auth failure, quota)
    //    advances the chain - a successful-but-sparse result is returned
    //    as-is, exactly as the single-provider design already did. See
    //    LostFound.AI.Core.ClassificationEngine's own remarks for the full
    //    chain: configured primary -> Gemini -> Local LLM -> rule-based.
    // ----------------------------------------------------------------------
    public static class LostFoundAiProvidersServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundAiProviders(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<AIProviderOptions>(configuration.GetSection("LostFound:AI"));

            var classificationProviderName = Normalize(configuration["LostFound:AI:Provider"] ?? "Gemini");
            var embeddingProviderName = ResolveEmbeddingProvider(configuration, classificationProviderName);

            services.AddHttpClient();

            AiProviderRegistry.RegisterClassificationProvider(services, classificationProviderName);
            AiProviderRegistry.RegisterEmbeddingProvider(services, embeddingProviderName);

            services.AddLostFoundSemanticAiFoundation(classificationProviderName, embeddingProviderName);

            return services;
        }

        /// <summary>
        /// Wires the capability-based foundation
        /// (<see cref="IClassificationEngine"/>, <see cref="IEmbeddingEngine"/>)
        /// on top of whichever concrete provider was just registered by
        /// <see cref="AiProviderRegistry"/>. This is the seam Phase 2A Part 1
        /// (Enterprise AI Foundation) introduces:
        /// <see cref="LostFound.AiMatchingService"/> and
        /// <see cref="LostFound.BackgroundJobs.ReportMatchingBackgroundJob"/>
        /// depend on these two interfaces only, never on a specific
        /// provider, so a future local embedding/classification runtime
        /// (Phase 2A Part 2 onward) can be introduced without touching
        /// either caller. Providers without their own retry logic are
        /// transparently resilience-wrapped here - see
        /// <see cref="AiProviderRegistry.SelfResilientProviders"/>.
        /// </summary>
        public static IServiceCollection AddLostFoundSemanticAiFoundation(
            this IServiceCollection services,
            string classificationProviderName,
            string embeddingProviderName)
        {
            // PHASE-VALIDATION-06 introduced the Local-First classification
            // fallback, registered under its own interface (never as
            // IItemClassificationProvider) so it can never collide with, or
            // be picked up in place of, the single configured remote
            // provider registered above by AiProviderRegistry.
            //
            // PHASE-VALIDATION-08 replaces WHICH implementation answers that
            // interface: LocalLlmClassificationProvider (a local Ollama-
            // served LLM, selected by real, measured benchmark - see
            // PHASE-VALIDATION-08-Local-LLM-Classification-Engine-
            // Evaluation-Replacement-Report.md) is now the registered
            // ILocalClassificationProvider. The rule-based
            // LocalClassificationProvider is NOT removed - it is still
            // registered (as itself, a concrete singleton) and composed
            // INSIDE LocalLlmClassificationProvider as its own inner
            // fallback, so the local tier keeps working even if Ollama
            // isn't installed/running or LocalLlm:Enabled is turned off.
            services.AddSingleton<Providers.LocalClassificationProvider>();

            // Typed-client registration (matching AiProviderRegistry's
            // pattern for every other HTTP-backed provider) so
            // LocalLlmClassificationProvider's HttpClient constructor
            // parameter is satisfied by IHttpClientFactory rather than
            // requiring a raw HttpClient registration. AddHttpClient<T>
            // registers T as Transient, so the interface forwarding below
            // is Transient too (matching every remote provider's effective
            // lifetime) rather than Singleton, avoiding a captive-dependency
            // mismatch.
            services.AddHttpClient<Providers.LocalLlmClassificationProvider>();
            services.AddTransient<ILocalClassificationProvider>(
                sp => sp.GetRequiredService<Providers.LocalLlmClassificationProvider>());

            // Gemini is always available as a dedicated second remote tier,
            // ahead of the local providers, regardless of which provider
            // "LostFound:AI:Provider" configures as primary - see the
            // ClassificationEngine remote-chain construction below. Skipped
            // only when Gemini IS the configured primary (AiProviderRegistry
            // already registered it as IItemClassificationProvider in that
            // case; registering the concrete type a second time here would
            // just create a redundant duplicate HttpClient registration for
            // the exact same provider).
            var primaryIsGemini = string.Equals(classificationProviderName, "gemini", StringComparison.OrdinalIgnoreCase);
            if (!primaryIsGemini)
            {
                services.AddHttpClient<Providers.GeminiClassificationProvider>();
            }

            services.AddTransient<IClassificationEngine>(sp =>
            {
                IItemClassificationProvider primaryProvider = sp.GetRequiredService<IItemClassificationProvider>();

                if (!AiProviderRegistry.SelfResilientProviders.Contains(classificationProviderName))
                {
                    primaryProvider = new ResilientClassificationProvider(
                        primaryProvider,
                        sp.GetRequiredService<ILogger<ResilientClassificationProvider>>());
                }

                // Ordered remote-provider chain: primary first, then Gemini
                // as the dedicated fallback tier (unless Gemini already IS
                // the primary, in which case there is nothing to add - see
                // the guard above). Gemini is never resilience-wrapped here,
                // matching AiProviderRegistry.SelfResilientProviders: it
                // already retries internally (GeminiVisionHelper), so
                // wrapping it again would compound retries for no benefit,
                // exactly the same reasoning already applied to the primary
                // branch above when the primary itself is Gemini.
                var remoteProviders = new List<IItemClassificationProvider> { primaryProvider };
                if (!primaryIsGemini)
                {
                    remoteProviders.Add(sp.GetRequiredService<Providers.GeminiClassificationProvider>());
                }

                var localProvider = sp.GetRequiredService<ILocalClassificationProvider>();

                return new ClassificationEngine(
                    remoteProviders, localProvider, sp.GetRequiredService<ILogger<ClassificationEngine>>());
            });

            services.AddTransient<IEmbeddingEngine>(sp => CreateProviderFallbackEmbeddingEngine(sp, embeddingProviderName));

            return services;
        }

        /// <summary>
        /// Builds the Part 1 provider-based <see cref="IEmbeddingEngine"/> -
        /// the single configured <see cref="IEmbeddingProvider"/>, resilience
        /// -wrapped where needed. Exposed (not private) so Phase 2A Part 2's
        /// <c>AddLostFoundLocalAiRuntime</c> can reuse the exact same
        /// construction logic as the fallback branch of
        /// <c>LocalFirstEmbeddingEngine</c>, instead of duplicating it or
        /// resolving <see cref="IEmbeddingEngine"/> from the container
        /// recursively (which would self-reference once Part 2 re-registers
        /// that interface).
        /// </summary>
        internal static IEmbeddingEngine CreateProviderFallbackEmbeddingEngine(IServiceProvider sp, string embeddingProviderName)
        {
            var provider = sp.GetRequiredService<IEmbeddingProvider>();

            if (!AiProviderRegistry.SelfResilientProviders.Contains(embeddingProviderName))
            {
                provider = new ResilientEmbeddingProvider(
                    provider,
                    sp.GetRequiredService<ILogger<ResilientEmbeddingProvider>>());
            }

            return new ProviderFallbackEmbeddingEngine(provider);
        }

        /// <summary>
        /// Resolves the same embedding-provider name <see cref="AddLostFoundAiProviders"/>
        /// computed, for callers (Phase 2A Part 2's local runtime wiring)
        /// that need it without re-deriving classification-provider logic
        /// themselves.
        /// </summary>
        internal static string ResolveConfiguredEmbeddingProviderName(IConfiguration configuration)
        {
            var classificationProviderName = Normalize(configuration["LostFound:AI:Provider"] ?? "Gemini");
            return ResolveEmbeddingProvider(configuration, classificationProviderName);
        }

        /// <summary>
        /// Decides which provider actually generates embeddings. For every
        /// provider except DeepSeek, this is just <paramref name="provider"/>
        /// itself. DeepSeek has no embeddings API, so it requires an explicit
        /// "LostFound:AI:EmbeddingProvider" override - defaulting to "Gemini"
        /// if one isn't configured, rather than failing to start.
        /// </summary>
        private static string ResolveEmbeddingProvider(IConfiguration configuration, string provider)
        {
            var overrideValue = configuration["LostFound:AI:EmbeddingProvider"];

            if (!string.IsNullOrWhiteSpace(overrideValue))
            {
                return Normalize(overrideValue);
            }

            return provider == "deepseek" ? "gemini" : provider;
        }

        private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    }
}
