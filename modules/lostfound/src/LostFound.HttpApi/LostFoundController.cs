using LostFound.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace LostFound;

public abstract class LostFoundController : AbpControllerBase
{
    protected LostFoundController()
    {
        LocalizationResource = typeof(LostFoundResource);
    }
}
