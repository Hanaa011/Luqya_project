using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Forge.Data;

/* This is used if database provider does't define
 * IForgeDbSchemaMigrator implementation.
 */
public class NullForgeDbSchemaMigrator : IForgeDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
