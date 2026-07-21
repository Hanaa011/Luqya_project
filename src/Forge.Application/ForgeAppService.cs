using Forge.Localization;
using Volo.Abp.Application.Services;

namespace Forge;

/* Inherit your application services from this class.
 */
public abstract class ForgeAppService : ApplicationService
{
    protected ForgeAppService()
    {
        LocalizationResource = typeof(ForgeResource);
    }
}
