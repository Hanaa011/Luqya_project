using LostFound.AI;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace LostFound;

[DependsOn(
    typeof(LostFoundDomainModule),
    typeof(LostFoundApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
    )]
public class LostFoundApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<LostFoundApplicationModule>();

        context.Services.AddLostFoundAiProviders(
            context.Services.GetConfiguration());
    }
}
