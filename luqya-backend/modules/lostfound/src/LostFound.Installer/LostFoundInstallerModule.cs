using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace LostFound;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class LostFoundInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<LostFoundInstallerModule>();
        });
    }
}
