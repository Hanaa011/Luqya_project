using Localization.Resources.AbpUi;
using LostFound.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace LostFound;

[DependsOn(
    typeof(LostFoundApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class LostFoundHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(LostFoundHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<LostFoundResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
