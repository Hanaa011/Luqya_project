using Microsoft.Extensions.Localization;
using Forge.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace Forge;

[Dependency(ReplaceServices = true)]
public class ForgeBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<ForgeResource> _localizer;

    public ForgeBrandingProvider(IStringLocalizer<ForgeResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
