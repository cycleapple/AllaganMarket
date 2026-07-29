using System.Collections.Generic;

using AllaganLib.Interface.FormFields;
using AllaganLib.Interface.Wizard;

using AllaganMarket.Settings;

namespace AllaganMarket.Features;

public class OverlaysFeature(IEnumerable<IFormField<Configuration>> settings) : Feature<Configuration>(
    [
        typeof(ShowRetainerOverlaySetting)
    ],
    settings)
{
    public override string Name { get; } = Loc.T("Overlays");

    public override string Description { get; } =
        Loc.T("Activate overlays that will appear when certain parts of the game UI are present.");
}
