using System.Collections.Generic;

using AllaganLib.Interface.FormFields;
using AllaganLib.Interface.Wizard;

using AllaganMarket.Settings;

namespace AllaganMarket.Features;

public class UndercutsFeature(IEnumerable<IFormField<Configuration>> settings) : Feature<Configuration>(
    [
        typeof(UndercutBySetting),
        typeof(UndercutComparisonSetting),
    ],
    settings)
{
    public override string Name { get; } = Loc.T("Undercuts");

    public override string Description { get; } = Loc.T("How should undercuts be handled?");
}
