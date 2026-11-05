using System;
using System.Threading;
using System.Threading.Tasks;

using AllaganMarket.GameInterop;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using AllaganMarket.Services.Interfaces;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;

using FFXIVClientStructs.FFXIV.Client.Game;

using Microsoft.Extensions.Hosting;

namespace AllaganMarket.Services;

/// <summary>
/// Decodes the market price packets and caches them.
/// </summary>
public class MarketPriceUpdaterService(IGameInteropProvider gameInteropProvider, IPluginLog pluginLog, IAddonLifecycle addonLifecycle, IRetainerService retainerService)
    : IHostedService, IDisposable
{
[Signature(
        "48 89 5C 24 ?? 57 48 83 EC ?? 48 8B 0D ?? ?? ?? ?? 48 8B DA E8 ?? ?? ?? ?? 48 8B F8 48 85 C0 74 ?? ?? ?? ?? ?? ?? ?? ??",
        DetourName = nameof(ItemRequestStartPacketDetour))]
    private readonly Hook<MarketBoardItemRequestStartPacketHandler>? itemRequestStartPacketDetourHook = null;
    private uint currentSequenceId;

    public delegate void MarketBoardItemRequestReceivedDelegate(MarketBoardItemRequest request);

private delegate nint MarketBoardItemRequestStartPacketHandler(nint a1, nint packetRef);

    public event MarketBoardItemRequestReceivedDelegate? MarketBoardItemRequestReceived;

    public RetainerMarketItemPrice[] CachedPrices { get; private set; } = new RetainerMarketItemPrice[20];

    public IGameInteropProvider GameInteropProvider { get; } = gameInteropProvider;

    public IPluginLog PluginLog { get; } = pluginLog;

    public bool HasCachedPrices { get; } = false;

    public void Dispose()
    {
        this.itemRequestStartPacketDetourHook?.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.GameInteropProvider.InitializeFromAttributes(this);
        this.itemRequestStartPacketDetourHook?.Enable();
        addonLifecycle.RegisterListener(AddonEvent.PostShow, "SelectString", PostOpen);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.itemRequestStartPacketDetourHook?.Disable();
        addonLifecycle.UnregisterListener(AddonEvent.PostShow, "SelectString", PostOpen);
        return Task.CompletedTask;
    }

    private unsafe nint ItemRequestStartPacketDetour(nint a1, nint packetRef)
    {
        try
        {
            this.MarketBoardItemRequestReceived?.Invoke(MarketBoardItemRequest.Read(packetRef));
        }
        catch (Exception ex)
        {
            this.PluginLog.Error(ex, "ItemRequestStartPacketDetour threw an exception");
        }

        return this.itemRequestStartPacketDetourHook!.OriginalDisposeSafe(a1, packetRef);
    }

    // API14 obtains prices from the inventory cache instead of an obsolete packet ABI.
    private unsafe void PostOpen(AddonEvent type, AddonArgs args)
    {
        if (retainerService.RetainerId == 0) return;
        var manager = InventoryManager.Instance();
        if (manager == null) return;
        for (short slot = 0; slot < CachedPrices.Length; slot++)
            CachedPrices[slot] = new RetainerMarketItemPrice((uint)slot, checked((uint)manager->GetRetainerMarketPrice(slot)));
    }
}
