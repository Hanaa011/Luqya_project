using Volo.Abp.Modularity;

namespace Forge;

public abstract class ForgeApplicationTestBase<TStartupModule> : ForgeTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
