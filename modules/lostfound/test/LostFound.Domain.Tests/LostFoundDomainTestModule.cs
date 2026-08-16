using Volo.Abp.Modularity;

namespace LostFound;

[DependsOn(
    typeof(LostFoundDomainModule),
    typeof(LostFoundTestBaseModule)
)]
public class LostFoundDomainTestModule : AbpModule
{

}
