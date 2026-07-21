using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    // Wires BOTH IEmbeddingProvider AND IItemClassificationProvider to the
    // SAME configured provider, so you never end up mixing e.g. Gemini
    // embeddings with an Ollama classifier by accident.
    public static class LostFoundAiProvidersServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundAiProviders(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<AIProviderOptions>(configuration.GetSection("LostFound:AI"));

            var provider = configuration["LostFound:AI:Provider"] ?? "Gemini";

            services.AddHttpClient();

            switch (provider.Trim().ToLowerInvariant())
            {
                case "ollama":
                    services.AddHttpClient<IEmbeddingProvider, OllamaEmbeddingProvider>();
                    services.AddHttpClient<IItemClassificationProvider, OllamaClassificationProvider>();
                    break;
                case "huggingface":
                    services.AddHttpClient<IEmbeddingProvider, HuggingFaceEmbeddingProvider>();
                    services.AddHttpClient<IItemClassificationProvider, HuggingFaceClassificationProvider>();
                    break;
                case "openai":
                    services.AddHttpClient<IEmbeddingProvider, OpenAIEmbeddingProvider>();
                    services.AddHttpClient<IItemClassificationProvider, OpenAIClassificationProvider>();
                    break;
                case "gemini":
                default:
                    services.AddHttpClient<IEmbeddingProvider, GeminiEmbeddingProvider>();
                    services.AddHttpClient<IItemClassificationProvider, GeminiClassificationProvider>();
                    break;
            }

            return services;
        }
    }
}
