using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LostFound.AI.AiService
{
    // Mirrors LostFoundAiProvidersServiceCollectionExtensions's pattern:
    // one AddLostFound... extension called once from
    // LostFoundApplicationModule.ConfigureServices.
    public static class AiServiceClientServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundAiServiceClient(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<AiServiceOptions>(configuration.GetSection("LostFound:AiService"));

            services.AddHttpClient<IAiServiceClient, AiServiceClient>();

            return services;
        }
    }
}
