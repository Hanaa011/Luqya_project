using Volo.Abp.Modularity;

namespace Forge;

[DependsOn(
    typeof(ForgeApplicationModule),
    typeof(ForgeDomainTestModule)
)]
public class ForgeApplicationTestModule : AbpModule
{

}
