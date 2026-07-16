using System;
using SwixySkyBlock.Content;
using SwixySkyBlock.Core;
using SwixySkyBlock.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock;

/// <summary>Часть <see cref="SwixySkyBlockClientMod"/> — клиент: GUI и входящие пакеты.</summary>
public sealed partial class SwixySkyBlockClientMod
{
    private const int ClientClimateApplyRetryMs = 250;
    private const int ClientClimateApplyMaxAttempts = 40;
    private const int ClientGeneratorLabelsInitialRequestDelayMs = 500;
    private const int ClientGeneratorLabelsRetryRequestDelayMs = 3000;
    private const int ClientGeneratorLabelsLateRetryRequestDelayMs = 10000;

    private int clientClimateApplyAttempts;

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        clientApi = api;
        clientClimateApplyAttempts = 0;
        clientChannel = IslandPacketChannel.Register(api.Network.RegisterChannel(SkyBlockConstants.ChannelName))
            .SetMessageHandler<IslandHubStatePacket>(OnHubStatePacket)
            .SetMessageHandler<IslandGeneratorLabelsPacket>(OnGeneratorLabelsPacket)
            .SetMessageHandler<IslandGeneratorStatePacket>(OnGeneratorStatePacket)
            .SetMessageHandler<IslandTopStatePacket>(OnTopStatePacket)
            .SetMessageHandler<StoryDungeonStatePacket>(OnStoryDungeonStatePacket)
            .SetMessageHandler<IslandClaimListStatePacket>(OnClaimListStatePacket)
            .SetMessageHandler<IslandClaimShowStatePacket>(OnClaimShowStatePacket)
            .SetMessageHandler<IslandClaimListDeltaPacket>(OnClaimListDeltaPacket);

        api.Event.OnGetClimate += OnClientUniformClimate;
        api.Event.BlockTexturesLoaded += OnClientClimateReady;
        generatorLabelRenderer = new IslandGeneratorLabelRenderer(api);
        api.Event.RegisterRenderer(generatorLabelRenderer, EnumRenderStage.Ortho, "swixyskyblock-generator-labels");
        api.Event.RegisterCallback(_ => RequestGeneratorLabels(), ClientGeneratorLabelsInitialRequestDelayMs);
        api.Event.RegisterCallback(_ => RequestGeneratorLabels(), ClientGeneratorLabelsRetryRequestDelayMs);
        api.Event.RegisterCallback(_ => RequestGeneratorLabels(), ClientGeneratorLabelsLateRetryRequestDelayMs);

        api.Input.RegisterHotKey(
            SkyBlockConstants.OpenIslandHubHotkeyCode,
            Lang.Get("swixyskyblock:open-island-hub-hotkey"),
            GlKeys.I,
            HotkeyType.GUIOrOtherControls,
            false,
            false,
            false);
        api.Input.SetHotKeyHandler(SkyBlockConstants.OpenIslandHubHotkeyCode, _ =>
        {
            api.Logger.Notification("[SwixySkyBlock][Hub] Hotkey I pressed.");
            ToggleHubDialog();
            return true;
        });

        api.Logger.Notification("[SwixySkyBlock] Client started (island hub hotkey: I).");
    }

    private void ToggleHubDialog()
    {
        if (clientApi == null)
        {
            return;
        }

        LogWorldPlayStyleContext("ToggleHubDialog");

        if (!SkyBlockWorld.IsSkyBlockWorld(clientApi.World))
        {
            clientApi.Logger.Notification(
                "[SwixySkyBlock][Hub] Blocked: world is not SkyBlock (see values above).");
            return;
        }

        if (hubDialog?.IsOpened() == true)
        {
            clientApi.Logger.Notification("[SwixySkyBlock][Hub] Closing hub dialog.");
            hubDialog.TryClose();
            return;
        }

        clientApi.Logger.Notification("[SwixySkyBlock][Hub] Opening hub dialog...");
        OpenHubDialog();
    }

    private void LogWorldPlayStyleContext(string source)
    {
        if (clientApi == null)
        {
            return;
        }

        var world = clientApi.World;
        var playstyle = world.Config.GetString("playstyle", "");
        var playStyleAlt = world.Config.GetString("playStyle", "");
        var worldType = world.Config.GetString("worldType", "");
        var isSkyBlock = SkyBlockWorld.IsSkyBlockWorld(world);

        clientApi.Logger.Notification(
            "[SwixySkyBlock][Hub] {0}: playstyle='{1}', playStyle='{2}', worldType='{3}', isSkyBlock={4}, channel={5}, dialogOpen={6}",
            source,
            playstyle,
            playStyleAlt,
            worldType,
            isSkyBlock,
            clientChannel != null,
            hubDialog?.IsOpened() == true);
    }

    private void OpenHubDialog()
    {
        if (clientApi == null)
        {
            return;
        }

        if (clientChannel == null)
        {
            clientApi.Logger.Error("[SwixySkyBlock][Hub] clientChannel is null - network not registered.");
            return;
        }

        if (!SkyBlockWorld.IsSkyBlockWorld(clientApi.World))
        {
            clientApi.Logger.Notification("[SwixySkyBlock][Hub] OpenHubDialog blocked: not SkyBlock.");
            return;
        }

        try
        {
            hubDialog ??= new IslandHubDialog(clientApi, clientChannel);
            clientApi.Logger.Notification("[SwixySkyBlock][Hub] IslandHubDialog instance ready.");

            if (!hubDialog.IsOpened())
            {
                var opened = hubDialog.TryOpen();
                clientApi.Logger.Notification(
                    "[SwixySkyBlock][Hub] TryOpen() -> {0}, IsOpened={1}",
                    opened,
                    hubDialog.IsOpened());
            }
            else
            {
                clientApi.Logger.Notification("[SwixySkyBlock][Hub] Dialog already open, refreshing.");
                hubDialog.RequestRefresh();
                hubDialog.RequestClaimList();
                hubDialog.RequestGeneratorState();
            }
        }
        catch (Exception exception)
        {
            clientApi.Logger.Error("[SwixySkyBlock][Hub] Open failed: {0}", exception);
            clientApi.ShowChatMessage(Lang.Get("swixyskyblock:error-open-hub-failed"));
        }
    }

    private void OnHubStatePacket(IslandHubStatePacket packet)
    {
        clientApi?.Logger.Notification(
            "[SwixySkyBlock][Hub] HubState received: hasIsland={0}, templates={1}, message='{2}'",
            packet.HasIsland,
            packet.AvailableTemplates?.Count ?? 0,
            packet.Message ?? "");

        if (hubDialog == null || !hubDialog.IsOpened())
        {
            clientApi?.Logger.Warning("[SwixySkyBlock][Hub] HubState ignored - dialog not open.");
            return;
        }

        hubDialog.ApplyHubState(packet);
    }

    private void OnGeneratorLabelsPacket(IslandGeneratorLabelsPacket packet)
    {
        generatorLabelRenderer?.Apply(packet);
    }

    private void OnGeneratorStatePacket(IslandGeneratorStatePacket packet)
    {
        clientApi?.Logger.Notification(
            "[SwixySkyBlock][Generator] State received: level={0}/{1}, hasIsland={2}, levels={3}",
            packet.CurrentLevel,
            packet.MaxLevel,
            packet.HasIsland,
            packet.Levels?.Count ?? 0);

        if (hubDialog == null)
        {
            return;
        }

        hubDialog.ApplyGeneratorState(packet);
    }

    private void OnTopStatePacket(IslandTopStatePacket packet)
    {
        clientApi?.Logger.Notification(
            "[SwixySkyBlock][Top] State received: entries={0}",
            packet.Entries?.Count ?? 0);

        if (hubDialog == null)
        {
            return;
        }

        hubDialog.ApplyTopState(packet);
    }

    private void OnStoryDungeonStatePacket(StoryDungeonStatePacket packet)
    {
        clientApi?.Logger.Notification(
            "[SwixySkyBlock][Story] State received: sites={0}",
            packet.Sites?.Count ?? 0);

        if (hubDialog == null)
        {
            return;
        }

        hubDialog.ApplyStoryState(packet);
    }

    private void RequestGeneratorLabels()
    {
        if (clientApi == null || clientChannel == null || !SkyBlockWorld.IsSkyBlockWorld(clientApi.World))
        {
            return;
        }

        clientChannel.SendPacket(new IslandGeneratorLabelsRequestPacket());
    }

    private void OnClaimListStatePacket(IslandClaimListStatePacket packet)
    {
        clientApi?.Logger.Notification(
            "[SwixySkyBlock][Hub] ClaimList received: claims={0}",
            packet.Claims?.Count ?? 0);

        if (hubDialog == null || !hubDialog.IsOpened())
        {
            return;
        }

        hubDialog.ApplyClaimList(packet);
    }

    private void OnClaimListDeltaPacket(IslandClaimListDeltaPacket delta)
    {
        clientApi?.Logger.Notification(
            "[SwixySkyBlock][Hub] Delta received: type='{0}', claimKey='{1}'",
            delta.MessageType,
            delta.ClaimKey);

        if (hubDialog == null || !hubDialog.IsOpened())
        {
            return;
        }

        hubDialog.ApplyClaimListDelta(delta);
    }

    private void OnClaimShowStatePacket(IslandClaimShowStatePacket packet)
    {
        if (hubDialog == null || !hubDialog.IsOpened())
        {
            return;
        }

        hubDialog.ApplyClaimShow(packet);
    }

    private void OnClientClimateReady()
    {
        if (clientApi == null || !SkyBlockWorld.IsSkyBlockWorld(clientApi.World))
        {
            clientClimateApplyAttempts = 0;
            return;
        }

        var calendar = clientApi.World.Calendar;
        if (calendar == null)
        {
            if (clientClimateApplyAttempts++ >= ClientClimateApplyMaxAttempts)
            {
                clientApi.Logger.Warning("[SwixySkyBlock] Client climate latitude skipped: calendar is not ready.");
                return;
            }

            clientApi.Event.RegisterCallback(_ => OnClientClimateReady(), ClientClimateApplyRetryMs);
            return;
        }

        clientClimateApplyAttempts = 0;
        calendar.OnGetLatitude = _ => SkyBlockClimate.SeasonLatitude;
    }

    private void OnClientUniformClimate(
        ref ClimateCondition climate,
        BlockPos pos,
        EnumGetClimateMode mode,
        double totalDays)
    {
        if (clientApi == null || !SkyBlockWorld.IsSkyBlockWorld(clientApi.World))
        {
            return;
        }

        climate.WorldGenTemperature = SkyBlockClimate.AnnualMeanTemperatureC;
        climate.WorldgenRainfall = SkyBlockClimate.Rainfall;

        if (mode == EnumGetClimateMode.WorldGenValues)
        {
            climate.Temperature = SkyBlockClimate.AnnualMeanTemperatureC;
            climate.Rainfall = SkyBlockClimate.Rainfall;
        }
    }
}
