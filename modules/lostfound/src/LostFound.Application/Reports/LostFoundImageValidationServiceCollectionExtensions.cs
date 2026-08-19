using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LostFound.Reports
{
    public static class LostFoundImageValidationServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundImageValidation(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<ImageValidationOptions>(configuration.GetSection("LostFound:ImageValidation"));
            services.AddSingleton<IImageValidator, ImageValidator>();

            return services;
        }
    }
}
