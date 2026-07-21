using Volo.Abp.Settings;

namespace Forge.Settings;

public class ForgeSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(ForgeSettings.MySetting1));
    }
}
