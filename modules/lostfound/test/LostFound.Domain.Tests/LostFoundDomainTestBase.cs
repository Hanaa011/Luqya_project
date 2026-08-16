using Volo.Abp.Modularity;

namespace LostFound;

/* Inherit from this class for your domain layer tests.
 * See SampleManager_Tests for example.
 */
public abstract class LostFoundDomainTestBase<TStartupModule> : LostFoundTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
