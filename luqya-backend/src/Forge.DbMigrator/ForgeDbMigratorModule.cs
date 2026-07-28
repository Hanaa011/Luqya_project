using Forge.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Forge.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(ForgeEntityFrameworkCoreModule),
    typeof(ForgeApplicationContractsModule)
)]
public class ForgeDbMigratorModule : AbpModule
{
}
