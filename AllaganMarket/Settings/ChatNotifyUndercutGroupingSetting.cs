using System;
using System.Collections.Generic;

using AllaganLib.Interface.FormFields;
using AllaganLib.Interface.Services;

namespace AllaganMarket.Settings;

public class ChatNotifyUndercutGroupingSetting : EnumFormField<ChatNotifyUndercutGrouping, Configuration>, ISetting
{
    public ChatNotifyUndercutGroupingSetting(ImGuiService imGuiService)
        : base(imGuiService)
    {
    }

    public override Enum DefaultValue { get; set; } = ChatNotifyUndercutGrouping.GroupByItem;

    public override string Key { get; set; } = "UndercutGrouping";

    public override string Name { get; set; } = Loc.T("Group undercut messages by?");

    public override string HelpText { get; set; } =
        Loc.T("When multiple undercuts occur, how should these be grouped together?");

    public override string Version => "1.0.0.1";

    public override bool Equal(Enum item1, Enum item2)
    {
        return Equals(item1, item2);
    }

    public override Dictionary<Enum, string> Choices => new()
    {
        { ChatNotifyUndercutGrouping.Individual, Loc.T("Individually")},
        { ChatNotifyUndercutGrouping.Together, Loc.T("All Together")},
        { ChatNotifyUndercutGrouping.GroupByItem, Loc.T("By Item")},
        { ChatNotifyUndercutGrouping.GroupByRetainer, Loc.T("By Retainer")},
    };

    public SettingType Type { get; set; } = SettingType.Chat;

    public bool ShowInSettings => true;
}

public enum ChatNotifyUndercutGrouping
{
    Individual,
    Together,
    GroupByItem,
    GroupByRetainer,
}
