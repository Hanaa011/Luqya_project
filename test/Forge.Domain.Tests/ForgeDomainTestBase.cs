using Volo.Abp.Modularity;

namespace Forge;

/* Inherit from this class for your domain layer tests. */
public abstract class ForgeDomainTestBase<TStartupModule> : ForgeTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
