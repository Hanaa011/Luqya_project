using LostFound.Localization;
using Volo.Abp.Application.Services;

namespace LostFound;

public abstract class LostFoundAppService : ApplicationService
{
    protected LostFoundAppService()
    {
        LocalizationResource = typeof(LostFoundResource);
        ObjectMapperContext = typeof(LostFoundApplicationModule);
    }
}
