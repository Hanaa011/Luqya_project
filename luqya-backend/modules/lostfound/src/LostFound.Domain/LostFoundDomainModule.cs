using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace LostFound;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(LostFoundDomainSharedModule)
)]
public class LostFoundDomainModule : AbpModule
{

}
