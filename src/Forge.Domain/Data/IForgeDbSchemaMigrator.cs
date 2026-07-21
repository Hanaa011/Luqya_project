using System.Threading.Tasks;

namespace Forge.Data;

public interface IForgeDbSchemaMigrator
{
    Task MigrateAsync();
}
