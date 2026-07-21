using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;

namespace LostFound;

[DependsOn(
    typeof(LostFoundDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class LostFoundApplicationContractsModule : AbpModule
{

}
