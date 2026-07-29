using System.Collections.Generic;

using AllaganLib.Interface.FormFields;
using AllaganLib.Interface.Wizard;

using AllaganMarket.Settings;

namespace AllaganMarket.Features;

public class MiscFeature(IEnumerable<IFormField<Configuration>> settings) : Feature<Configuration>(
    [
        typeof(AddTitleMenuButtonSetting),
        typeof(AddDtrBarEntrySetting)
    ],
    settings)
{
    public override string Name { get; } = Loc.T("Misc Features");

    public override string Description { get; } = Loc.T("Miscellaneous features that improve your overall experience.");
}
