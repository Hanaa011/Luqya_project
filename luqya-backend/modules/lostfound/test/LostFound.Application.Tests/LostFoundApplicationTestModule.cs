using Volo.Abp.Modularity;

namespace LostFound;

[DependsOn(
    typeof(LostFoundApplicationModule),
    typeof(LostFoundDomainTestModule)
    )]
public class LostFoundApplicationTestModule : AbpModule
{

}
