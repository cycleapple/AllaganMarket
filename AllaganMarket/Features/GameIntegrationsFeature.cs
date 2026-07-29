using System.Collections.Generic;

using AllaganLib.Interface.FormFields;
using AllaganLib.Interface.Wizard;

namespace AllaganMarket.Features;

public class GameIntegrationsFeature(IEnumerable<IFormField<Configuration>> settings) : Feature<Configuration>(
    [
    ],
    settings)
{
    public override string Name { get; } = Loc.T("Game Integrations");

    public override string Description { get; } = Loc.T("Activate features that integrate directly into the game.");
}
