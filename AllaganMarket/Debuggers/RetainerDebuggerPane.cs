using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

using AllaganLib.Shared.Interfaces;

using AllaganMarket.Models;
using AllaganMarket.Services.Interfaces;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AllaganMarket.Debugging;

public unsafe class RetainerDebuggerPane : IDebugPane
{
    private readonly ICharacterMonitorService characterMonitor;

    private string searchStoredName = string.Empty;
    private string searchStoredWorld = string.Empty;
    private string searchActiveName = string.Empty;

    public RetainerDebuggerPane(ICharacterMonitorService characterMonitor)
    {
        this.characterMonitor = characterMonitor;
    }

    public string Name => Loc.T("Retainer Debugger");

    public void Draw()
    {
        using var child = ImRaii.Child("RetainerDebuggerPaneChild", new System.Numerics.Vector2(0, 0), true);

        if (!child.Success)
        {
            return;
        }

        DrawStoredRetainers();
        ImGui.Separator();
        DrawActiveRetainers();
    }

    private void DrawStoredRetainers()
    {
        ImGui.Text(Loc.T("Stored Retainers (from CharacterMonitorService)"));
        ImGui.InputText(Loc.T("Filter Name (Stored)"), ref this.searchStoredName, 32);
        ImGui.SameLine();
        ImGui.InputText(Loc.T("Filter WorldId (Stored)"), ref this.searchStoredWorld, 32);

        var stored = this.characterMonitor.Characters.Values
                         .Where(c => c.CharacterType == CharacterType.Retainer)
                         .Where(c =>
                                    (string.IsNullOrEmpty(this.searchStoredName) ||
                                     c.Name.Contains(this.searchStoredName, StringComparison.OrdinalIgnoreCase)) &&
                                    (string.IsNullOrEmpty(this.searchStoredWorld) ||
                                     c.WorldId.ToString(CultureInfo.InvariantCulture)
                                      .Contains(this.searchStoredWorld, StringComparison.OrdinalIgnoreCase)))
                         .ToList();

        using var table = ImRaii.Table(
            "StoredRetainersTable",
            8,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable |
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,
            new System.Numerics.Vector2(0, 200));

        if (!table.Success)
        {
            return;
        }

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn(Loc.T("Character Id"));
        ImGui.TableSetupColumn(Loc.T("Owner Id"));
        ImGui.TableSetupColumn(Loc.T("Name"));
        ImGui.TableSetupColumn(Loc.T("World Id"));
        ImGui.TableSetupColumn(Loc.T("ClassJob Id"));
        ImGui.TableSetupColumn(Loc.T("Level"));
        ImGui.TableSetupColumn(Loc.T("Display Order"));
        ImGui.TableSetupColumn(Loc.T("Town"));
        ImGui.TableHeadersRow();

        var clipper = new ImGuiListClipper();
        clipper.Begin(stored.Count);

        while (clipper.Step())
        {
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var ch = stored[i];

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.Text(ch.CharacterId.ToString(CultureInfo.InvariantCulture));

                ImGui.TableNextColumn();
                ImGui.Text(ch.OwnerId?.ToString(CultureInfo.InvariantCulture) ?? "-");

                ImGui.TableNextColumn();
                ImGui.Text(ch.Name);

                ImGui.TableNextColumn();
                ImGui.Text(ch.WorldId.ToString(CultureInfo.InvariantCulture));

                ImGui.TableNextColumn();
                ImGui.Text(ch.ClassJobId.ToString(CultureInfo.InvariantCulture));

                ImGui.TableNextColumn();
                ImGui.Text(ch.Level.ToString(CultureInfo.InvariantCulture));

                ImGui.TableNextColumn();
                ImGui.Text(ch.DisplayOrder.ToString(CultureInfo.InvariantCulture));

                ImGui.TableNextColumn();
                ImGui.Text(ch.RetainerTown?.ToString() ?? "-");
            }
        }

        clipper.End();
    }

    private void DrawActiveRetainers()
    {
        ImGui.Text(Loc.T("Active Retainers (from RetainerManager)"));
        ImGui.InputText(Loc.T("Filter Name (Active)"), ref this.searchActiveName, 32);

        var manager = RetainerManager.Instance();
        if (manager == null)
        {
            ImGui.Text(Loc.T("RetainerManager not available."));
            return;
        }

        var active = new List<RetainerManager.Retainer>();
        for (uint i = 0; i < 10; i++)
        {
            var retainer = RetainerManager.Instance()->GetRetainerBySortedIndex(i);
            if (retainer != null && retainer->RetainerId != 0)
            {
                active.Add(*retainer);
            }
        }

        using var table = ImRaii.Table("ActiveRetainersTable",
                                       11,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable |
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,
            new System.Numerics.Vector2(0, 200));

        if (!table.Success)
        {
            return;
        }

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn(Loc.T("Retainer Id"));
        ImGui.TableSetupColumn(Loc.T("Name"));
        ImGui.TableSetupColumn(Loc.T("Available"));
        ImGui.TableSetupColumn(Loc.T("Class Job"));
        ImGui.TableSetupColumn(Loc.T("Level"));
        ImGui.TableSetupColumn(Loc.T("Item Count"));
        ImGui.TableSetupColumn(Loc.T("Gil"));
        ImGui.TableSetupColumn(Loc.T("Town"));
        ImGui.TableSetupColumn(Loc.T("Market Item Count"));
        ImGui.TableSetupColumn(Loc.T("Venture Id"));
        ImGui.TableSetupColumn(Loc.T("Retainer Order"));
        ImGui.TableHeadersRow();

        var clipper = new ImGuiListClipper();
        clipper.Begin(active.Count);

        while (clipper.Step())
        {
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var r = active[i];
                var name = r.NameString.Trim();

                if (!string.IsNullOrEmpty(this.searchActiveName) &&
                    !name.Contains(this.searchActiveName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.Text(r.RetainerId.ToString(CultureInfo.InvariantCulture));

                ImGui.TableNextColumn();
                ImGui.Text(name);

                ImGui.TableNextColumn();
                ImGui.Text(r.Available ? Loc.T("Yes") : Loc.T("No"));

                ImGui.TableNextColumn();
                ImGui.Text(r.ClassJob.ToString(CultureInfo.InvariantCulture));

                ImGui.TableNextColumn();
                ImGui.Text(r.Level.ToString(CultureInfo.InvariantCulture));

                ImGui.TableNextColumn();
                ImGui.Text(r.ItemCount.ToString(CultureInfo.InvariantCulture));

                ImGui.TableNextColumn();
                ImGui.Text(r.Gil.ToString(CultureInfo.InvariantCulture));

                ImGui.TableNextColumn();
                ImGui.Text(r.Town.ToString());

                ImGui.TableNextColumn();
                ImGui.Text(r.MarketItemCount.ToString(CultureInfo.InvariantCulture));

                ImGui.TableNextColumn();
                ImGui.Text(r.VentureId.ToString(CultureInfo.InvariantCulture));

                var displayOrder = RetainerManager.Instance()->DisplayOrder.IndexOf((byte)i);
                displayOrder = displayOrder == -1 ? 0 : displayOrder;

                ImGui.TableNextColumn();
                ImGui.Text(displayOrder.ToString());
            }
        }

        clipper.End();
    }
}
