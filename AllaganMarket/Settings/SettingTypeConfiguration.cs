using System.Collections.Generic;

using static AllaganMarket.Settings.SettingType;

namespace AllaganMarket.Settings;

public class SettingTypeConfiguration
{
    private List<SettingType>? settingTypes;

    public static string GetFormattedName(SettingType settingType)
    {
        return settingType switch
        {
            Chat => Loc.T("Chat"),
            General => Loc.T("General"),
            Undercutting => Loc.T("Undercutting"),
            SettingType.Features => Loc.T("Features"),
            Overlays => Loc.T("Overlays"),
            Highlighting => Loc.T("Highlighting"),
            _ => settingType.ToString(),
        };
    }

    public List<SettingType> GetCategoryOrder()
    {
        return this.settingTypes ??= [General, Undercutting, SettingType.Features, Overlays, Highlighting, Chat];
    }
}
