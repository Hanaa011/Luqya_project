using Forge.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Forge.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class ForgeController : AbpControllerBase
{
    protected ForgeController()
    {
        LocalizationResource = typeof(ForgeResource);
    }
}
