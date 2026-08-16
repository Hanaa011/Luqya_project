using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// Central registry of every known AI provider name, replacing the
    /// per-capability switch statements that previously lived in
    /// <see cref="LostFoundAiProvidersServiceCollectionExtensions"/>. Each
    /// entry is a strongly-typed DI registration delegate (not a
    /// <see cref="Type"/> lookup + reflection) so adding/removing a provider
    /// stays a one-line, compile-time-checked change in exactly one place,
    /// while the actual <c>AddHttpClient&lt;TClient, TImplementation&gt;()</c>
    /// call keeps full type safety.
    /// </summary>
    internal static class AiProviderRegistry
    {
        public static readonly IReadOnlyDictionary<string, Action<IServiceCollection>> ClassificationProviders =
            new Dictionary<string, Action<IServiceCollection>>(StringComparer.OrdinalIgnoreCase)
            {
                ["gemini"] = services => services.AddHttpClient<IItemClassificationProvider, GeminiClassificationProvider>(),
                ["openai"] = services => services.AddHttpClient<IItemClassificationProvider, OpenAIClassificationProvider>(),
                ["ollama"] = services => services.AddHttpClient<IItemClassificationProvider, OllamaClassificationProvider>(),
                ["huggingface"] = services => services.AddHttpClient<IItemClassificationProvider, HuggingFaceClassificationProvider>(),
                ["deepseek"] = services => services.AddHttpClient<IItemClassificationProvider, DeepSeekClassificationProvider>(),
            };

        public static readonly IReadOnlyDictionary<string, Action<IServiceCollection>> EmbeddingProviders =
            new Dictionary<string, Action<IServiceCollection>>(StringComparer.OrdinalIgnoreCase)
            {
                ["gemini"] = services => services.AddHttpClient<IEmbeddingProvider, GeminiEmbeddingProvider>(),
                ["openai"] = services => services.AddHttpClient<IEmbeddingProvider, OpenAIEmbeddingProvider>(),
                ["ollama"] = services => services.AddHttpClient<IEmbeddingProvider, OllamaEmbeddingProvider>(),
                ["huggingface"] = services => services.AddHttpClient<IEmbeddingProvider, HuggingFaceEmbeddingProvider>(),
            };

        /// <summary>
        /// Providers that already implement their own retry/backoff
        /// internally (see <see cref="GeminiVisionHelper"/>) and therefore
        /// must NOT be wrapped again by <see cref="ResilientEmbeddingProvider"/>
        /// / <see cref="ResilientClassificationProvider"/>.
        /// </summary>
        public static readonly IReadOnlySet<string> SelfResilientProviders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gemini" };

        public static void RegisterClassificationProvider(IServiceCollection services, string providerName)
        {
            if (!ClassificationProviders.TryGetValue(providerName, out var register))
            {
                throw new InvalidOperationException(
                    $"Unknown classification provider '{providerName}'. Supported providers: " +
                    $"{string.Join(", ", ClassificationProviders.Keys)}.");
            }

            register(services);
        }

        public static void RegisterEmbeddingProvider(IServiceCollection services, string providerName)
        {
            if (!EmbeddingProviders.TryGetValue(providerName, out var register))
            {
                var message = providerName.Equals("deepseek", StringComparison.OrdinalIgnoreCase)
                    ? "DeepSeek has no embeddings API. Set \"LostFound:AI:EmbeddingProvider\" to " +
                      "\"Gemini\", \"OpenAI\", \"Ollama\", or \"HuggingFace\" when \"LostFound:AI:Provider\" is \"DeepSeek\"."
                    : $"Unknown embedding provider '{providerName}'. Supported providers: " +
                      $"{string.Join(", ", EmbeddingProviders.Keys)}.";

                throw new InvalidOperationException(message);
            }

            register(services);
        }
    }
}
