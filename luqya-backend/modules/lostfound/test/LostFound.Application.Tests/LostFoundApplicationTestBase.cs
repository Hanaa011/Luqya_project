using Volo.Abp.Modularity;

namespace LostFound;

/* Inherit from this class for your application layer tests.
 * See SampleAppService_Tests for example.
 */
public abstract class LostFoundApplicationTestBase<TStartupModule> : LostFoundTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
