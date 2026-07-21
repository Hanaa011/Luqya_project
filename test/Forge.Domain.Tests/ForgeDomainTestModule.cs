using Volo.Abp.Modularity;

namespace Forge;

[DependsOn(
    typeof(ForgeDomainModule),
    typeof(ForgeTestBaseModule)
)]
public class ForgeDomainTestModule : AbpModule
{

}
