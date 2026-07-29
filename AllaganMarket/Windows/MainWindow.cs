using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;

using AllaganLib.Data.Service;
using AllaganLib.Interface.Grid;
using AllaganLib.Interface.Widgets;
using AllaganLib.Shared.Extensions;

using AllaganMarket.Extensions;
using AllaganMarket.Filtering;
using AllaganMarket.Mediator;
using AllaganMarket.Models;
using AllaganMarket.Services;
using AllaganMarket.Services.Interfaces;
using AllaganMarket.Settings;
using AllaganMarket.Tables;
using AllaganMarket.Tables.Fields;

using DalaMock.Host.Mediator;
using DalaMock.Shared.Interfaces;

using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

using Dalamud.Bindings.ImGui;

using Lumina.Excel;
using Lumina.Excel.Sheets;

using Serilog.Events;

using TimeUnit = Humanizer.Localisation.TimeUnit;

namespace AllaganMarket.Windows;

public class MainWindow : ExtendedWindow
{
    private readonly ExcelSheet<Item> itemSheet;
    private readonly ExcelSheet<ClassJob> classJobSheet;
    private readonly IPluginLog pluginLog;
    private readonly SaleFilter saleFilter;
    private readonly NumberFormatInfo gilNumberFormat;
    private readonly ItemUpdatePeriodSetting itemUpdatePeriodSetting;
    private readonly ICommandManager commandManager;
    private readonly IFont font;
    private readonly CsvLoaderService csvLoaderService;
    private readonly IFileDialogManager fileDialogManager;
    private readonly SaleItemTable saleItemTable;
    private readonly SoldItemTable soldItemTable;
    private readonly SaleSummaryTable saleSummaryTable;
    private readonly SearchResultConfiguration saleItemSearchConfiguration;
    private readonly SearchResultConfiguration soldItemSearchConfiguration;
    private readonly SearchResultConfiguration saleSummarySearchConfiguration;
    private readonly ViewModeSetting viewModeSetting;
    private readonly SaleSummaryGroupFormField saleSummaryGroupFormField;
    private readonly SaleSummaryDateRangeFormField saleSummaryDateRangeFormField;
    private readonly SaleSummaryTimeSpanFormField saleSummaryTimeSpanFormField;
    private readonly UndercutService undercutService;
    private readonly TimeSpanHumanizerCache humanizerCache;
    private readonly ImGuiMenus imGuiMenus;
    private readonly ExcelSheet<World> worldSheet;
    private readonly GridQuickSearchWidget<SearchResultConfiguration, SearchResult, MessageBase> saleQuickSearchWidget;
    private readonly GridQuickSearchWidget<SearchResultConfiguration, SearchResult, MessageBase> soldQuickSearchWidget;

    private readonly GridQuickSearchWidget<SearchResultConfiguration, SearchResult, MessageBase>
        saleSummarySearchWidget;

    private bool filterMenuOpen;
    private SummaryDateMode summaryDateMode = SummaryDateMode.Range;

    public MainWindow(
        MediatorService mediatorService,
        ImGuiService imGuiService,
        Configuration configuration,
        ITextureProvider textureProvider,
        ConfigWindow configWindow,
        IPluginLog pluginLog,
        IRetainerMarketService retainerMarketService,
        SaleTrackerService saleTrackerService,
        ICharacterMonitorService characterMonitorService,
        IDataManager dataManager,
        SaleFilter saleFilter,
        NumberFormatInfo gilNumberFormat,
        ItemUpdatePeriodSetting itemUpdatePeriodSetting,
        ICommandManager commandManager,
        IFont font,
        CsvLoaderService csvLoaderService,
        IFileDialogManager fileDialogManager,
        SaleItemTable saleItemTable,
        SoldItemTable soldItemTable,
        SaleSummaryTable saleSummaryTable,
        ViewModeSetting viewModeSetting,
        SaleSummaryGroupFormField saleSummaryGroupFormField,
        SaleSummaryDateRangeFormField saleSummaryDateRangeFormField,
        SaleSummaryTimeSpanFormField saleSummaryTimeSpanFormField,
        UndercutService undercutService,
        TimeSpanHumanizerCache humanizerCache,
        ImGuiMenus imGuiMenus)
        : base(mediatorService, imGuiService, Loc.T("Allagan Market") + "##AllaganMarkets")
    {
        this.pluginLog = pluginLog;
        this.saleFilter = saleFilter;
        this.gilNumberFormat = gilNumberFormat;
        this.itemUpdatePeriodSetting = itemUpdatePeriodSetting;
        this.commandManager = commandManager;
        this.font = font;
        this.csvLoaderService = csvLoaderService;
        this.fileDialogManager = fileDialogManager;
        this.saleItemTable = saleItemTable;
        this.soldItemTable = soldItemTable;
        this.saleSummaryTable = saleSummaryTable;
        this.saleItemSearchConfiguration = saleItemTable.SearchFilter;
        this.soldItemSearchConfiguration = soldItemTable.SearchFilter;
        this.saleSummarySearchConfiguration = saleSummaryTable.SearchFilter;
        this.viewModeSetting = viewModeSetting;
        this.saleSummaryGroupFormField = saleSummaryGroupFormField;
        this.saleSummaryDateRangeFormField = saleSummaryDateRangeFormField;
        this.saleSummaryTimeSpanFormField = saleSummaryTimeSpanFormField;
        this.undercutService = undercutService;
        this.humanizerCache = humanizerCache;
        this.imGuiMenus = imGuiMenus;
        this.Configuration = configuration;
        this.TextureProvider = textureProvider;
        this.ConfigWindow = configWindow;
        this.RetainerMarketService = retainerMarketService;
        this.SaleTrackerService = saleTrackerService;
        this.CharacterMonitorService = characterMonitorService;
        this.DataManager = dataManager;
        this.WorldExpanded = [];
        this.CharacterExpanded = [];
        this.itemSheet = this.DataManager.GetExcelSheet<Item>()!;
        this.worldSheet = this.DataManager.GetExcelSheet<World>()!;
        this.classJobSheet = this.DataManager.GetExcelSheet<ClassJob>()!;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        this.Size = new Vector2(600, 600);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.MenuBar;
        this.SaleTrackerService.SnapshotCreated += this.SnapshotCreated;
        this.saleQuickSearchWidget =
            new GridQuickSearchWidget<SearchResultConfiguration, SearchResult, MessageBase>(this.saleItemTable);
        this.soldQuickSearchWidget =
            new GridQuickSearchWidget<SearchResultConfiguration, SearchResult, MessageBase>(this.soldItemTable);
        this.saleSummarySearchWidget =
            new GridQuickSearchWidget<SearchResultConfiguration, SearchResult, MessageBase>(this.saleSummaryTable);
        this.MediatorService.Subscribe<SaleFilterRefreshedMessage>(this, this.SaleFilterRefreshed);
    }

    private void SaleFilterRefreshed(SaleFilterRefreshedMessage obj)
    {
        this.saleItemTable.IsDirty = true;
        this.saleSummaryTable.IsDirty = true;
        this.soldItemTable.IsDirty = true;
    }

    public enum SummaryDateMode
    {
        Range,
        TimeSpan,
    }

    public enum MainWindowTab
    {
        CurrentlySelling,
        SalesHistory,
        SalesSummary,
    }

    public Configuration Configuration { get; }

    public ITextureProvider TextureProvider { get; }

    public ConfigWindow ConfigWindow { get; }

    public IRetainerMarketService RetainerMarketService { get; }

    public SaleTrackerService SaleTrackerService { get; }

    public ICharacterMonitorService CharacterMonitorService { get; }

    public IDataManager DataManager { get; }

    private MainWindowTab SelectedTab { get; set; }

    private Dictionary<uint, bool> WorldExpanded { get; set; }

    private Dictionary<ulong, bool> CharacterExpanded { get; set; }

    public override void Dispose()
    {
        this.SaleTrackerService.SnapshotCreated -= this.SnapshotCreated;
        base.Dispose();
    }

    public override void Draw()
    {
        if (ImGui.BeginMenuBar())
        {
            if (ImGui.BeginMenu(Loc.T("File")))
            {
                if (ImGui.MenuItem(Loc.T("Configuration")))
                {
                    this.MediatorService.Publish(new OpenWindowMessage(typeof(ConfigWindow)));
                }

                if (ImGui.MenuItem(Loc.T("Report a Issue")))
                {
                    "https://github.com/Critical-Impact/AllaganMarket".OpenBrowser();
                }

                if (ImGui.MenuItem(Loc.T("Enable Verbose Logging"), "", this.pluginLog.MinimumLogLevel == LogEventLevel.Verbose))
                {
                    if (this.pluginLog.MinimumLogLevel == LogEventLevel.Verbose)
                    {
                        this.pluginLog.MinimumLogLevel = LogEventLevel.Debug;
                    }
                    else
                    {
                        this.pluginLog.MinimumLogLevel = LogEventLevel.Verbose;
                    }
                }


                if (ImGui.MenuItem(Loc.T("Ko-Fi")))
                {
                    "https://ko-fi.com/critical_impact".OpenBrowser();
                }

                if (ImGui.MenuItem(Loc.T("Close")))
                {
                    this.IsOpen = false;
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu(Loc.T("Edit")))
            {
                if (ImGui.MenuItem(Loc.T("Mark all visible sale items as updated")))
                {
                    var saleItems = this.saleItemTable.GetFilteredItems(this.saleItemSearchConfiguration);
                    foreach (var saleItem in saleItems)
                    {
                        this.undercutService.InsertFakeMarketPriceCache(saleItem.SaleItem!);
                    }
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu(Loc.T("View")))
            {
                var currentViewMode = this.viewModeSetting.CurrentValue(this.Configuration);
                if (ImGui.MenuItem(Loc.T("Grid") + (currentViewMode == ViewMode.Grid ? Loc.T(" (Active)") : string.Empty)))
                {
                    this.viewModeSetting.UpdateFilterConfiguration(this.Configuration, ViewMode.Grid);
                }

                if (ImGui.MenuItem(Loc.T("List") + (currentViewMode == ViewMode.List ? Loc.T(" (Active)") : string.Empty)))
                {
                    this.viewModeSetting.UpdateFilterConfiguration(this.Configuration, ViewMode.List);
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu(Loc.T("Export")))
            {
                if (ImGui.MenuItem(Loc.T("Export Current Sales")))
                {
                    this.fileDialogManager.SaveFileDialog(
                        Loc.T("Select a save location"),
                        "*.csv",
                        "Current Sales.csv",
                        ".csv",
                        (success, fileName) =>
                        {
                            if (success)
                            {
                                this.saleItemTable.SaveToCsv(
                                    this.saleItemSearchConfiguration,
                                    new RenderTableCsvExportOptions()
                                    {
                                        ExportPath = fileName, IncludeHeaders = true,
                                    });
                            }
                        });
                }

                if (ImGui.MenuItem(Loc.T("Export Current Sales (Filtered)")))
                {
                    this.fileDialogManager.SaveFileDialog(
                        Loc.T("Select a save location"),
                        "*.csv",
                        "Current Sales(Filtered).csv",
                        ".csv",
                        (success, fileName) =>
                        {
                            if (success)
                            {
                                this.saleItemTable.SaveToCsv(
                                    this.saleItemSearchConfiguration,
                                    new RenderTableCsvExportOptions()
                                    {
                                        ExportPath = fileName, IncludeHeaders = true, UseFiltering = true,
                                    });
                            }
                        });
                }

                ImGui.Separator();

                if (ImGui.MenuItem(Loc.T("Export History (CSV)")))
                {
                    this.fileDialogManager.SaveFileDialog(
                        Loc.T("Select a save location"),
                        "*.csv",
                        "Sales History.csv",
                        ".csv",
                        (success, fileName) =>
                        {
                            if (success)
                            {
                                this.soldItemTable.SaveToCsv(
                                    this.soldItemSearchConfiguration,
                                    new RenderTableCsvExportOptions()
                                    {
                                        ExportPath = fileName, IncludeHeaders = true,
                                    });
                            }
                        });
                }

                if (ImGui.MenuItem(Loc.T("Export History (Filtered)")))
                {
                    this.fileDialogManager.SaveFileDialog(
                        Loc.T("Select a save location"),
                        "*.csv",
                        "Sales History(Filtered).csv",
                        ".csv",
                        (success, fileName) =>
                        {
                            if (success)
                            {
                                this.soldItemTable.SaveToCsv(
                                    this.soldItemSearchConfiguration,
                                    new RenderTableCsvExportOptions()
                                    {
                                        ExportPath = fileName, IncludeHeaders = true, UseFiltering = true,
                                    });
                            }
                        });
                }

                ImGui.Separator();

                if (ImGui.MenuItem(Loc.T("Export Sales Summary (CSV)")))
                {
                    this.fileDialogManager.SaveFileDialog(
                        Loc.T("Select a save location"),
                        "*.csv",
                        "Sales Summary.csv",
                        ".csv",
                        (success, fileName) =>
                        {
                            if (success)
                            {
                                this.saleSummaryTable.SaveToCsv(
                                    this.saleSummarySearchConfiguration,
                                    new RenderTableCsvExportOptions()
                                    {
                                        ExportPath = fileName, IncludeHeaders = true,
                                    });
                            }
                        });
                }

                if (ImGui.MenuItem(Loc.T("Export Sales Summary (Filtered)")))
                {
                    this.fileDialogManager.SaveFileDialog(
                        Loc.T("Select a save location"),
                        "*.csv",
                        "Sales Summary(Filtered).csv",
                        ".csv",
                        (success, fileName) =>
                        {
                            if (success)
                            {
                                this.saleSummaryTable.SaveToCsv(
                                    this.saleSummarySearchConfiguration,
                                    new RenderTableCsvExportOptions()
                                    {
                                        ExportPath = fileName, IncludeHeaders = true, UseFiltering = true,
                                    });
                            }
                        });
                }

                ImGui.EndMenu();
            }

            ImGui.EndMenuBar();
        }

        this.DrawCharacterSideBar();
        ImGui.SameLine();
        this.DrawSalesPane();
        ImGui.SameLine();
        this.DrawFilterSideBar();
    }

    private static void DrawBooleanTooltip(string fieldText)
    {
        ImGui.Text("?");
        if (ImGui.IsItemHovered())
        {
            using var tooltip = ImRaii.Tooltip();
            if (tooltip)
            {
                ImGui.Text(fieldText);
            }
        }
    }

    private static void DrawStringTooltip(string fieldText)
    {
        ImGui.Text("?");
        if (ImGui.IsItemHovered())
        {
            using var tooltip = ImRaii.Tooltip();
            if (tooltip)
            {
                ImGui.Text(fieldText);
                ImGui.Text(Loc.T("When searching the following operators can be used to compare: "));
                ImGui.Separator();
                ImGui.Text(Loc.T("=, for exact comparisons"));
                ImGui.Text(Loc.T("!, for inequality comparisons"));
                ImGui.Text(Loc.T("||, search multiple expressions using OR"));
                ImGui.Text(Loc.T("&&, search multiple expressions using AND"));
            }
        }
    }

    private static void DrawNumericTooltip(string fieldText)
    {
        ImGui.Text("?");
        if (ImGui.IsItemHovered())
        {
            using var tooltip = ImRaii.Tooltip();
            if (tooltip)
            {
                ImGui.Text(fieldText);
                ImGui.Text(Loc.T("When searching the following operators can be used to compare: "));
                ImGui.Separator();
                ImGui.Text(Loc.T(">, >=, <, <=, =, for numeric comparisons"));
                ImGui.Text(Loc.T("=, for exact comparisons"));
                ImGui.Text(Loc.T("!, for inequality comparisons"));
                ImGui.Text(Loc.T("||, search multiple expressions using OR"));
                ImGui.Text(Loc.T("&&, search multiple expressions using AND"));
            }
        }
    }

    private static void DrawDateTooltip(string fieldText)
    {
        ImGui.Text("?");
        if (ImGui.IsItemHovered())
        {
            using var tooltip = ImRaii.Tooltip();
            if (tooltip)
            {
                ImGui.Text(fieldText);
                ImGui.Text(Loc.T("When searching the following operators can be used to compare: "));
                ImGui.Separator();
                ImGui.Text(Loc.T("2 hours ago, finds all entries 2 hours and greater"));
                ImGui.Text(Loc.T(">, >=, <, <=, =, for date comparisons"));
                ImGui.Text(Loc.T("=, for exact comparisons"));
                ImGui.Text(Loc.T("!, for inequality comparisons"));
                ImGui.Text(Loc.T("||, search multiple expressions using OR"));
                ImGui.Text(Loc.T("&&, search multiple expressions using AND"));
            }
        }
    }

    private void SnapshotCreated()
    {
        this.saleFilter.RequestRefresh();
    }

    private bool IsWorldExpanded(uint worldId)
    {
        if (!this.WorldExpanded.TryGetValue(worldId, out var value))
        {
            return true;
        }

        return this.WorldExpanded.ContainsKey(worldId) && value;
    }

    private void ToggleWorldExpanded(uint worldId)
    {
        this.WorldExpanded[worldId] = !this.WorldExpanded.GetValueOrDefault(worldId, true);
    }

    private bool IsCharacterExpanded(ulong characterId)
    {
        if (!this.CharacterExpanded.TryGetValue(characterId, out var value))
        {
            return true;
        }

        return this.CharacterExpanded.ContainsKey(characterId) && value;
    }

    private void ToggleCharacterExpanded(ulong characterId)
    {
        this.CharacterExpanded[characterId] = !this.CharacterExpanded.GetValueOrDefault(characterId, true);
    }

    private string? DrawCharacterRightClickMenu(Character character)
    {
        using var popup = ImRaii.Popup("rc_" + character.CharacterId);
        if (!popup)
        {
            return null;
        }

        ImGui.Text(character.Name);
        ImGui.Separator();
        if (ImGui.Selectable(Loc.T("Delete Character")))
        {
            return Loc.T("Delete Character?") + "##dc_" + character.CharacterId;
        }

        return null;
    }

    private void DrawCharacterDeleteConfirmPopup(Character character)
    {
        var open = true;
        using var popupModal = ImRaii.PopupModal(
            Loc.T("Delete Character?") + "##dc_" + character.CharacterId,
            ref open,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
        if (!popupModal)
        {
            return;
        }

        ImGui.TextUnformatted(
            Loc.T("Are you sure you want to delete this character? Any retainers owned by this retainer will also be removed."));
        if (ImGui.Button(Loc.T("Confirm")))
        {
            this.CharacterMonitorService.RemoveCharacter(character.CharacterId);
            if (character.CharacterId == this.saleFilter.CharacterId)
            {
                this.saleFilter.CharacterId = null;
            }

            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.T("Cancel")))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    private void DrawCharacterSideBar()
    {
        using var characters = ImRaii.Child("characters", new Vector2(200, 0) * ImGuiHelpers.GlobalScale);
        if (characters.Success)
        {
            using (var mainSection = ImRaii.Child("mainSection", new Vector2(0, -30) * ImGuiHelpers.GlobalScale))
            {
                if (mainSection)
                {
                    if (this.CharacterMonitorService.Characters.Count == 0)
                    {
                        ImGui.TextWrapped(Loc.T("No characters found. Please login."));
                    }

                    var worlds = this.CharacterMonitorService.GetWorldIds(CharacterType.Character);
                    if (worlds.Count == 0)
                    {
                        ImGui.TextWrapped(Loc.T("No worlds found. Please login."));
                    }

                    foreach (var worldId in worlds)
                    {
                        var world = this.worldSheet.GetRow(worldId)!;
                        if (ImGui.Selectable(
                                world.Name.ExtractText(),
                                this.saleFilter.WorldId == worldId,
                                ImGuiSelectableFlags.AllowItemOverlap))
                        {
                            this.SwitchWorld(worldId);
                        }

                        if (ImGui.IsItemHovered())
                        {
                            using var tooltip = ImRaii.Tooltip();
                            if (tooltip)
                            {
                                ImGui.Text(Loc.T("Left Click: Select/Unselect"));
                                ImGui.Text(Loc.T("Arrow: Collapse/Uncollapse"));
                            }
                        }

                        ImGui.SameLine();
                        var style = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(1, 1));
                        if (ImGui.ArrowButton(
                                "world_" + worldId,
                                this.IsWorldExpanded(worldId) ? ImGuiDir.Down : ImGuiDir.Right))
                        {
                            this.ToggleWorldExpanded(worldId);
                        }

                        style.Pop();

                        if (!this.IsWorldExpanded(worldId))
                        {
                            continue;
                        }

                        using (ImRaii.PushIndent())
                        {
                            foreach (var character in this.CharacterMonitorService.GetCharactersByType(
                                         CharacterType.Character,
                                         worldId))
                            {
                                if (ImGui.Selectable(
                                        character.Name,
                                        this.saleFilter.CharacterId == character.CharacterId,
                                        ImGuiSelectableFlags.AllowItemOverlap))
                                {
                                    this.SwitchCharacter(character.CharacterId);
                                }

                                if (ImGui.IsItemHovered())
                                {
                                    using (var tooltip = ImRaii.Tooltip())
                                    {
                                        if (tooltip)
                                        {
                                            ImGui.Text(Loc.T("Left Click: Select/Unselect"));
                                            ImGui.Text(Loc.T("Right Click: Menu"));
                                        }
                                    }

                                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                                    {
                                        ImGui.OpenPopup("rc_" + character.CharacterId);
                                    }
                                }

                                var result = this.DrawCharacterRightClickMenu(character);
                                if (result != null)
                                {
                                    ImGui.OpenPopup(result);
                                }

                                this.DrawCharacterDeleteConfirmPopup(character);

                                ImGui.SameLine();
                                style.Push(ImGuiStyleVar.FramePadding, new Vector2(1, 1));
                                if (ImGui.ArrowButton(
                                        character.CharacterId.ToString(),
                                        this.IsCharacterExpanded(character.CharacterId) ? ImGuiDir.Down : ImGuiDir.Right))
                                {
                                    this.ToggleCharacterExpanded(character.CharacterId);
                                }

                                style.Pop();

                                if (!this.IsCharacterExpanded(character.CharacterId))
                                {
                                    continue;
                                }

                                ImGui.Separator();
                                using (ImRaii.PushIndent())
                                {
                                    foreach (var retainer in this.CharacterMonitorService.GetOwnedCharacters(
                                                 character.CharacterId,
                                                 CharacterType.Retainer).OrderBy(c => c.DisplayOrder))
                                    {
                                        if (ImGui.Selectable(
                                                retainer.Name,
                                                this.saleFilter.CharacterId == retainer.CharacterId))
                                        {
                                            this.SwitchCharacter(retainer.CharacterId);
                                        }

                                        var retainerHovered = ImGui.IsItemHovered();

                                        ImGui.SameLine();
                                        var icon = this.TextureProvider.GetFromGameIcon(
                                            new GameIconLookup(retainer.ClassJobId == 0 ? 62045 : retainer.ClassJobId + 62000));
                                        ImGui.Image(
                                            icon.GetWrapOrEmpty().Handle,
                                            new Vector2(16, 16) * ImGui.GetIO().FontGlobalScale);

                                        if (retainerHovered || ImGui.IsItemHovered())
                                        {
                                            using (var tooltip = ImRaii.Tooltip())
                                            {
                                                if (tooltip)
                                                {
                                                    ImGui.Text(retainer.Name);
                                                    ImGui.Separator();
                                                    ImGui.Text(
                                                        Loc.F(
                                                            "Class: {0}",
                                                            this.classJobSheet.GetRowOrDefault(retainer.ClassJobId)?.Abbreviation.ExtractText() ??
                                                            Loc.T("Unknown Class")));
                                                    ImGui.Text(Loc.F("Level: {0}", retainer.Level));
                                                    ImGui.Text(Loc.T("Left Click: Select/Unselect"));
                                                    ImGui.Text(Loc.T("Right Click: Menu"));
                                                }
                                            }

                                            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                                            {
                                                ImGui.OpenPopup("rc_" + retainer.CharacterId);
                                            }
                                        }

                                        var retainerResult = this.DrawCharacterRightClickMenu(retainer);
                                        if (retainerResult != null)
                                        {
                                            ImGui.OpenPopup(retainerResult);
                                        }

                                        this.DrawCharacterDeleteConfirmPopup(retainer);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            using var bottomBar = ImRaii.Child(
                "bottomBar",
                new Vector2(0, 0),
                false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            if (bottomBar)
            {
                ImGui.Separator();
            }
        }
    }

    private void DrawSalesPane()
    {
        using var saleItems = ImRaii.Child(
            "saleItems",
            new Vector2(this.filterMenuOpen ? -200 : 0, 0) * ImGuiHelpers.GlobalScale);
        if (saleItems)
        {
            using (var mainSection = ImRaii.Child("mainSection", new Vector2(0, -30) * ImGuiHelpers.GlobalScale))
            {
                if (mainSection)
                {
                    using var tabBar = ImRaii.TabBar("mainTabs");
                    if (tabBar)
                    {
                        using (var currentSales = ImRaii.TabItem(Loc.T("Currently Selling")))
                        {
                            if (currentSales)
                            {
                                ImGuiService.HoverTooltip(Loc.T("These are the items you currently have for sale."));
                                this.SelectedTab = MainWindowTab.CurrentlySelling;
                                this.DrawButtonBar();
                                this.DrawCurrentlySelling();
                            }
                        }

                        using (var recentSales = ImRaii.TabItem(Loc.T("Sale History")))
                        {
                            if (recentSales)
                            {
                                ImGuiService.HoverTooltip(Loc.T("These are the items that you have sold."));
                                this.SelectedTab = MainWindowTab.SalesHistory;
                                this.DrawButtonBar();
                                this.DrawSalesHistory();
                            }
                        }

                        using (var recentSales = ImRaii.TabItem(Loc.T("Sale Summary")))
                        {
                            if (recentSales)
                            {
                                ImGuiService.HoverTooltip(
                                    Loc.T("Provides a groupable list of items you have sold over a given period."));
                                this.SelectedTab = MainWindowTab.SalesSummary;
                                this.DrawButtonBar();
                                this.DrawSalesSummary();
                            }
                        }
                    }
                }
            }

            using var bottomBar = ImRaii.Child("bottomBar", new Vector2(0, 0), false);
            if (bottomBar)
            {
                ImGui.Separator();
                if (this.RetainerMarketService.InBadState)
                {
                    ImGui.PushTextWrapPos();
                    ImGui.Text(
                        Loc.T(
                            "It looks like you opened your retainer before this plugin loaded. Please go back to the retainer list and reopen the retainer. The only time this happens is when the plugin is updated or you have just installed the plugin."));
                    ImGui.PopTextWrapPos();
                }

                if (this.SelectedTab == MainWindowTab.CurrentlySelling)
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(
                        Loc.F(
                            "{0} items for sale on {1} worth {2}",
                            this.saleFilter.GetSaleItems().Count,
                            this.GetSelectedName(),
                            this.saleFilter.AggregateSalesTotalGil.ToString("C", this.gilNumberFormat)));
                }
                else if (this.SelectedTab == MainWindowTab.SalesHistory)
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(
                        Loc.F(
                            "{0} items sold on {1} worth {2}",
                            this.saleFilter.GetSoldItems().Count,
                            this.GetSelectedName(),
                            this.saleFilter.AggregateSoldTotalGil.ToString("C", this.gilNumberFormat)));
                }

                var selectedGil = this.GetSelectedGil();
                if (selectedGil != null)
                {
                    ImGui.SameLine();
                    ImGui.Text(
                        Loc.F(
                            "{0} has {1} stored.",
                            this.GetSelectedName(),
                            selectedGil.Value.ToString("C", this.gilNumberFormat)));
                }
            }
        }
    }

    private void DrawFilterSideBar()
    {
        using var filterMenu = ImRaii.Child("filterMenu", new Vector2(0, 0));
        if (filterMenu.Success)
        {
            using (var searchMain = ImRaii.Child("searchMain", new Vector2(0, -30) * ImGuiHelpers.GlobalScale))
            {
                if (searchMain)
                {
                    if (this.SelectedTab == MainWindowTab.CurrentlySelling)
                    {
                        this.saleQuickSearchWidget.Draw(this.saleItemSearchConfiguration, 150, 150);
                    }
                    else if (this.SelectedTab == MainWindowTab.SalesHistory)
                    {
                        this.soldQuickSearchWidget.Draw(this.soldItemSearchConfiguration, 150, 150);
                    }
                    else if (this.SelectedTab == MainWindowTab.SalesSummary)
                    {
                        this.saleSummarySearchWidget.Draw(this.saleSummarySearchConfiguration, 150, 150);
                    }
                }
            }

            using var bottomBar = ImRaii.Child(
                "bottomBar",
                new Vector2(0, 0),
                false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            if (bottomBar)
            {
                ImGui.Separator();
                var cursorPosX = ImGui.GetCursorPosX();
                if (ImGuiService.DrawIconButton(
                        this.font,
                        FontAwesomeIcon.Times,
                        ref cursorPosX,
                        Loc.T("Clear search")))
                {
                    this.saleFilter.Clear();
                }
            }
        }
    }

    private void DrawCurrentlySelling()
    {
        var currentViewMode = this.viewModeSetting.CurrentValue(this.Configuration);
        if (currentViewMode == ViewMode.Grid)
        {
            var widthAvailable = ImGui.GetContentRegionAvail().X;
            var totalItems = Math.Floor(widthAvailable / 300);
            using var currentlySelling = ImRaii.Child("currentlySelling", new Vector2(0, 0), false);
            if (currentlySelling)
            {
                var retainerItems = this.saleItemTable.GetFilteredItems(this.saleItemSearchConfiguration)
                                        .Select(c => c.SaleItem!);
                var iterateList = retainerItems.Select(c => c).SortByRetainerMarketOrder().ToList();
                var total = 0;
                for (var index = 0; index < iterateList.Count; index++)
                {
                    var saleItem = iterateList[index];
                    if (total >= totalItems)
                    {
                        total = 0;
                    }
                    else if (index != 0)
                    {
                        ImGui.SameLine();
                    }

                    total++;
                    this.DrawSaleItem(saleItem, index);
                }
            }
        }
        else
        {
            this.MediatorService.Publish(this.saleItemTable.Draw(this.saleItemSearchConfiguration, new Vector2(0, 0)));
        }
    }

    private void DrawSalesHistory()
    {
        var currentViewMode = this.viewModeSetting.CurrentValue(this.Configuration);
        if (currentViewMode == ViewMode.Grid)
        {
            var widthAvailable = ImGui.GetContentRegionAvail().X;
            var totalItems = Math.Floor(widthAvailable / 300);

            var retainerItems = this.soldItemTable.GetFilteredItems(this.soldItemSearchConfiguration)
                                    .Select(c => c.SoldItem!).ToList();
            var total = 0;
            if (retainerItems.Count == 0)
            {
                ImGui.Text(Loc.T("No sales tracked yet, please visit a retainer to perform an initial scan on their sales."));
            }

            for (var index = 0; index < retainerItems.Count; index++)
            {
                var saleItem = retainerItems[index];
                if (total >= totalItems)
                {
                    total = 0;
                }
                else if (index != 0 && index != retainerItems.Count)
                {
                    ImGui.SameLine();
                }

                total++;
                using var itemChild = ImRaii.Child(
                    $"item_{index}",
                    new Vector2(300, 80) * ImGuiHelpers.GlobalScale,
                    true,
                    ImGuiWindowFlags.NoScrollbar);
                if (itemChild)
                {
                    var item = this.itemSheet.GetRow(saleItem.ItemId)!;
                    using (var iconChild = ImRaii.Child(
                               $"icon_{index}",
                               new Vector2(64, 64) * ImGuiHelpers.GlobalScale,
                               false,
                               ImGuiWindowFlags.NoScrollbar))
                    {
                        if (iconChild)
                        {
                            var icon = this.TextureProvider.GetFromGameIcon(
                                new GameIconLookup(item.Icon, saleItem.IsHq));

                            ImGui.Image(icon.GetWrapOrEmpty().Handle, new Vector2(64, 64));
                        }
                    }

                    ImGui.SameLine();
                    using var infoChild = ImRaii.Child(
                        $"info_{index}",
                        new Vector2(0, 0) * ImGuiHelpers.GlobalScale,
                        false,
                        ImGuiWindowFlags.NoScrollbar);
                    if (infoChild)
                    {
                        ImGui.Text(item.Name.ExtractText());
                        ImGui.PushTextWrapPos();
                        ImGui.Text(
                            Loc.F(
                                "{0} at {1} ({2})",
                                saleItem.Quantity,
                                saleItem.UnitPrice.ToString("C", this.gilNumberFormat),
                                saleItem.TotalIncTax.ToString("C", this.gilNumberFormat)));
                        ImGui.Text(Loc.F("Sold on {0}", saleItem.SoldAt.ToString(CultureInfo.CurrentCulture)));
                        ImGui.PopTextWrapPos();
                    }
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenOverlapped) &&
                    ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    ImGui.OpenPopup("RightClick");
                }

                using var popup = ImRaii.Popup("RightClick");
                if (popup)
                {
                    var result = this.imGuiMenus.DrawSoldItemMenu(saleItem);
                    if (result != null)
                    {
                        this.MediatorService.Publish(result);
                    }
                }
            }
        }
        else
        {
            this.MediatorService.Publish(this.soldItemTable.Draw(this.soldItemSearchConfiguration, new Vector2(0, 0)));
        }
    }

    private void DrawSalesSummary()
    {
        this.MediatorService.Publish(
            this.saleSummaryTable.Draw(this.saleSummarySearchConfiguration, new Vector2(0, 0)));
    }

    private void DrawSaleItem(SaleItem saleItem, int index)
    {
        using (ImRaii.PushId(index))
        {
            using (var itemChild = ImRaii.Child(
                       $"item_{index}",
                       new Vector2(300, 90) * ImGuiHelpers.GlobalScale,
                       true,
                       ImGuiWindowFlags.NoScrollbar))
            {
                if (itemChild)
                {
                    // Empty slot
                    if (saleItem.ItemId == 0)
                    {
                        ImGui.Text(Loc.T("Empty Slot"));
                        return;
                    }

                    var item = this.itemSheet.GetRow(saleItem.ItemId)!;
                    var character = this.CharacterMonitorService.GetCharacterById(saleItem.RetainerId);
                    if (character == null)
                    {
                        ImGui.Text(Loc.T("Unknown Character"));
                        return;
                    }

                    var lastUpdated = DateTime.Now - (this.undercutService.GetLastUpdateTime(saleItem) ?? saleItem.UpdatedAt);
                    var lastUpdatedHumanized = this.humanizerCache.GetHumanizedTimeSpan(lastUpdated);

                    var listedAt = DateTime.Now - saleItem.ListedAt;
                    var listedAtHumanized = this.humanizerCache.GetHumanizedTimeSpan(listedAt);

                    var retainerWorld = this.worldSheet.GetRow(character.WorldId)!;
                    using (var iconChild = ImRaii.Child(
                               $"icon_{index}",
                               new Vector2(64, 64) * ImGuiHelpers.GlobalScale,
                               false,
                               ImGuiWindowFlags.NoScrollbar))
                    {
                        if (iconChild)
                        {
                            var icon = this.TextureProvider.GetFromGameIcon(
                                new GameIconLookup(item.Icon, saleItem.IsHq));
                            ImGui.Image(icon.GetWrapOrEmpty().Handle, new Vector2(64, 64));
                        }
                    }
                    var iconHovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);

                    var undercutHovered = false;

                    ImGui.SameLine();
                    using (var infoChild = ImRaii.Child(
                               $"info_{index}",
                               new Vector2(0, 0) * ImGuiHelpers.GlobalScale,
                               false,
                               ImGuiWindowFlags.NoScrollbar))
                    {
                        if (infoChild)
                        {
                            var undercutBy = this.undercutService.GetUndercutBy(saleItem);
                            if (undercutBy != null && undercutBy != 0)
                            {
                                var startPosition = ImGui.GetCursorPos();
                                ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - 16);
                                ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - 16);
                                ImGui.Image(
                                    this.TextureProvider.GetFromGameIcon(new GameIconLookup(61575)).GetWrapOrEmpty()
                                        .Handle,
                                    new Vector2(16, 16));
                                undercutHovered = ImGui.IsItemHovered();
                                ImGuiService.HoverTooltip(
                                    Loc.F(
                                        "You have been undercut on this item by {0} {1}",
                                        SeIconChar.Gil.ToIconString(),
                                        undercutBy));
                                ImGui.SetCursorPos(startPosition);
                            }

                            ImGui.Text(item.Name.ExtractText());
                            ImGui.PushTextWrapPos();
                            ImGui.Text(
                                Loc.F(
                                    "{0} at {1} ({2})",
                                    saleItem.Quantity,
                                    saleItem.UnitPrice.ToString("C", this.gilNumberFormat),
                                    saleItem.Total.ToString("C", this.gilNumberFormat)));
                            ImGui.Text(Loc.F("Listed: {0}", listedAtHumanized));

                            using (ImRaii.PushColor(
                                       ImGuiCol.Text,
                                       ImGuiColors.DalamudYellow,
                                       this.undercutService.NeedsUpdate(
                                           saleItem,
                                           this.itemUpdatePeriodSetting.CurrentValue(this.Configuration))))
                            {
                                ImGui.Text(Loc.F("Updated: {0}", lastUpdatedHumanized));
                            }

                            ImGui.PopTextWrapPos();
                        }
                    }

                    if (!undercutHovered && (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenOverlapped) || iconHovered))
                    {
                        using var tooltip = ImRaii.Tooltip();
                        if (tooltip)
                        {
                            ImGui.Text($"{item.Name.ExtractText()}");
                            ImGui.Text($"{character?.Name ?? Loc.T("Unknown")}");
                            ImGui.Text($"{retainerWorld.Name.ExtractText()}");
                            ImGui.Text(
                                Loc.F(
                                    "{0} at {1} ({2})",
                                    saleItem.Quantity,
                                    saleItem.UnitPrice.ToString("C", this.gilNumberFormat),
                                    saleItem.Total.ToString("C", this.gilNumberFormat)));
                            ImGui.Text(Loc.F("Listed: {0}", listedAtHumanized));
                            ImGui.Text(Loc.F("Updated: {0}", lastUpdatedHumanized));
                            var marketPriceNq = this.undercutService.GetMarketPriceCache(retainerWorld.RowId, saleItem.ItemId, false);
                            var marketPriceHq = this.undercutService.GetMarketPriceCache(retainerWorld.RowId, saleItem.ItemId, true);
                            if (marketPriceNq != null || marketPriceHq != null)
                            {
                                ImGui.Separator();
                                ImGui.Text(Loc.T("Latest Data:"));
                                ImGui.Text(
                                    Loc.F(
                                        "Source: {0}",
                                        marketPriceNq?.GetFormattedType() ?? marketPriceHq?.GetFormattedType()));
                                if (marketPriceNq != null)
                                {
                                    ImGui.Text(Loc.F("Unit Price NQ: {0}", marketPriceNq?.UnitCost));
                                }

                                if (marketPriceHq != null)
                                {
                                    ImGui.Text(Loc.F("Unit Price HQ: {0}", marketPriceHq?.UnitCost));
                                }
                            }
                        }
                    }
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenOverlapped) &&
                ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup("RightClick");
            }

            using var popup = ImRaii.Popup("RightClick");
            if (popup)
            {
                var result = this.imGuiMenus.DrawSaleItemMenu(saleItem);
                if (result != null)
                {
                    this.MediatorService.Publish(result);
                }
            }
        }
    }

    private void DrawButtonBar()
    {
        using var buttonBar = ImRaii.Child(
            "buttonBar",
            new Vector2(0, 22 + (ImGui.GetStyle().CellPadding.Y * 2)) * ImGuiHelpers.GlobalScale,
            false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (buttonBar)
        {
            var search = string.Empty;
            switch (this.SelectedTab)
            {
                case MainWindowTab.CurrentlySelling:
                    search = this.saleItemTable.SearchFilter.Get("Name") ?? string.Empty;
                    break;
                case MainWindowTab.SalesHistory:
                    search = this.soldItemTable.SearchFilter.Get("Name") ?? string.Empty;
                    break;
                case MainWindowTab.SalesSummary:
                    search = this.saleSummaryTable.SearchFilter.Get("Name") ?? string.Empty;
                    break;
            }

            var searchWidth = ImGui.CalcTextSize("Search").X + ImGui.GetStyle().ItemSpacing.X;
            ImGui.SetNextItemWidth(searchWidth);
            ImGui.LabelText(string.Empty, Loc.T("Search:"));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            if (ImGui.InputText("##searchBox", ref search, 200))
            {
                var newItemName = search == string.Empty ? null : search;
                switch (this.SelectedTab)
                {
                    case MainWindowTab.CurrentlySelling:
                        this.saleItemTable.SearchFilter.Set("Name", newItemName);
                        break;
                    case MainWindowTab.SalesHistory:
                        this.soldItemTable.SearchFilter.Set("Name", newItemName);
                        break;
                    case MainWindowTab.SalesSummary:
                        this.saleSummaryTable.SearchFilter.Set("Name", newItemName);
                        break;
                }
            }

            ImGui.SameLine();
            var cursorPosX = ImGui.GetCursorPosX();
            if (ImGuiService.DrawIconButton(
                    this.font,
                    FontAwesomeIcon.Times,
                    ref cursorPosX,
                    Loc.T("Clear search")))
            {
                this.saleFilter.Clear();
            }

            if (this.SelectedTab == MainWindowTab.CurrentlySelling)
            {
                ImGui.SameLine();
                var showEmptySlots = this.saleFilter.ShowEmpty ?? false;
                if (ImGui.Checkbox(Loc.T("Show empty slots:"), ref showEmptySlots))
                {
                    this.saleFilter.ShowEmpty = showEmptySlots;
                }
            }

            if (this.SelectedTab == MainWindowTab.SalesSummary)
            {
                ImGui.SameLine();
                if (this.saleSummaryGroupFormField.DrawInput(
                        this.saleSummaryTable.SaleSummary,
                        (int?)(150 * ImGuiHelpers.GlobalScale)))
                {
                    this.saleSummaryTable.IsDirty = true;
                }

                ImGui.SameLine();
                if (this.summaryDateMode == SummaryDateMode.Range)
                {
                    if(this.saleSummaryDateRangeFormField.DrawInput(
                        this.saleSummaryTable.SaleSummary,
                        (int?)(150 * ImGuiHelpers.GlobalScale)))
                    {
                        this.saleSummaryTable.IsDirty = true;
                    }
                }
                else
                {
                    if(this.saleSummaryTimeSpanFormField.DrawInput(
                        this.saleSummaryTable.SaleSummary,
                        (int?)(150 * ImGuiHelpers.GlobalScale)))
                    {
                        this.saleSummaryTable.IsDirty = true;
                    }
                }

                ImGui.SameLine();

                using (ImRaii.PushFont(this.font.IconFont))
                {
                    if (ImGui.Button($"{FontAwesomeIcon.Calendar.ToIconString()}"))
                    {
                        ImGui.OpenPopup("SDMPicker");
                    }
                }

                using (var popup = ImRaii.Popup("SDMPicker"))
                {
                    if (popup)
                    {
                        if (ImGui.Selectable(Loc.T("Date Range"), this.summaryDateMode == SummaryDateMode.Range))
                        {
                            this.summaryDateMode = SummaryDateMode.Range;
                        }

                        if (ImGui.Selectable(Loc.T("Since Today"), this.summaryDateMode == SummaryDateMode.TimeSpan))
                        {
                            this.summaryDateMode = SummaryDateMode.TimeSpan;
                        }
                    }
                }
            }

            ImGui.SameLine();

            cursorPosX = ImGui.GetWindowSize().X;
            if (ImGuiService.DrawIconButton(
                    this.font,
                    FontAwesomeIcon.Search,
                    ref cursorPosX,
                    Loc.T("Toggle search bar"),
                    true))
            {
                this.filterMenuOpen = !this.filterMenuOpen;
            }
            ImGui.Separator();
        }
    }

    private void SwitchCharacter(ulong newCharacterId)
    {
        if (this.saleFilter.CharacterId == newCharacterId)
        {
            this.saleFilter.CharacterId = null;
        }
        else
        {
            this.saleFilter.CharacterId = newCharacterId;
            this.saleFilter.WorldId = null;
        }
    }

    private void SwitchWorld(uint newWorldId)
    {
        if (this.saleFilter.WorldId == newWorldId)
        {
            this.saleFilter.WorldId = null;
        }
        else
        {
            this.saleFilter.WorldId = newWorldId;
            this.saleFilter.CharacterId = null;
        }
    }

    private string GetSelectedName()
    {
        if (this.saleFilter.CharacterId == null && this.saleFilter.WorldId == null)
        {
            return Loc.T("All Retainers/Worlds");
        }

        if (this.saleFilter.CharacterId != null)
        {
            return this.CharacterMonitorService.GetCharacterById(this.saleFilter.CharacterId.Value)?.Name ??
                   Loc.T("Unknown Character/Retainer");
        }

        if (this.saleFilter.WorldId != null)
        {
            return this.worldSheet.GetRowOrDefault((uint)this.saleFilter.WorldId)?.Name.ExtractText() ??
                   Loc.T("Unknown World");
        }

        return Loc.T("Unknown");
    }

    private uint? GetSelectedGil()
    {
        if (this.saleFilter.CharacterId != null)
        {
            return this.SaleTrackerService.GetRetainerGil(this.saleFilter.CharacterId.Value);
        }

        return null;
    }
}
