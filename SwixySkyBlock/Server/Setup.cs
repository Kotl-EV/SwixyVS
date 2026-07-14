using SwixySkyBlock.Net;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Серверная инициализация.</summary>
public sealed partial class SwixySkyBlockMod
{
    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        serverApi = api;
        LegacySaveFixup.MigrateAllSaves(api.Logger);
        if (SkyBlockWorld.IsSkyBlockWorld(api))
        {
            SkyBlockWorld.ApplyWorldConfig(api);
        }

        serverChannel = RegisterIslandPacketTypes(api.Network.RegisterChannel(ChannelName))
            .SetMessageHandler<IslandHubRequestPacket>(OnHubRequest)
            .SetMessageHandler<IslandActionPacket>(OnIslandAction)
            .SetMessageHandler<IslandGeneratorLabelsRequestPacket>(OnGeneratorLabelsRequest)
            .SetMessageHandler<IslandGeneratorStateRequestPacket>(OnGeneratorStateRequest)
            .SetMessageHandler<IslandGeneratorUpgradeRequestPacket>(OnGeneratorUpgradeRequest)
            .SetMessageHandler<IslandTopRequestPacket>(OnTopRequest)
            .SetMessageHandler<StoryDungeonStateRequestPacket>(OnStoryDungeonStateRequest)
            .SetMessageHandler<StoryDungeonTeleportRequestPacket>(OnStoryDungeonTeleportRequest)
            .SetMessageHandler<IslandClaimListRequestPacket>(OnClaimListRequest)
            .SetMessageHandler<IslandClaimAccessActionPacket>(OnClaimAccessAction)
            .SetMessageHandler<IslandClaimShowRequestPacket>(OnClaimShowRequest);

        api.Event.SaveGameLoaded += OnIslandSaveGameLoaded;
        api.Event.GameWorldSave += OnCoOwnersSaveGameSaving;

        RegisterWorldGen(api);
        RegisterClimateHandlers(api);
        RegisterSpawnHandlers(api);
        RegisterIslandGenerator(api);
        api.Event.PlayerJoin += OnPlayerJoinStoryState;
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnRunGameStorySites);

        api.Logger.Notification(
            "[SwixySkyBlock] Server started (worldType={0}, playstyle={1}, dedicated={2}).",
            api.WorldManager.SaveGame.WorldType,
            api.World.Config.GetString("playstyle", api.WorldManager.SaveGame.PlayStyle),
            api.Server.IsDedicated);
        api.Logger.Notification("[SwixySkyBlock] Island network channel registered on server.");
    }

    private void OnIslandSaveGameLoaded()
    {
        if (serverApi == null || !IsSkyBlockWorld)
        {
            return;
        }

        islandRegistry.Load(serverApi);
        islandResidency.Load(serverApi);
        IslandBlueprint.LoadAll(serverApi);
        LoadStoryDungeons();
        OnCoOwnersSaveGameLoaded();
        RestoreAllPlayerIslands();
    }
}
