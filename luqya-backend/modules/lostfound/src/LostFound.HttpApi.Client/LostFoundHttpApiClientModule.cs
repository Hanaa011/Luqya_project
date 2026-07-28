using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace LostFound;

[DependsOn(
    typeof(LostFoundApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class LostFoundHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(LostFoundApplicationContractsModule).Assembly,
            LostFoundRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<LostFoundHttpApiClientModule>();
        });

    }
}
