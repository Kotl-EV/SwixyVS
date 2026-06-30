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
        SkyBlockWorldGenHost.Bind(this);
        LegacySaveFixup.MigrateAllSaves(api.Logger);
        if (SkyBlockWorld.IsSkyBlockWorld(api))
        {
            SkyBlockWorld.ApplyWorldConfig(api);
        }

        serverChannel = RegisterIslandPacketTypes(api.Network.RegisterChannel(ChannelName))
            .SetMessageHandler<IslandHubRequestPacket>(OnHubRequest)
            .SetMessageHandler<IslandActionPacket>(OnIslandAction)
            .SetMessageHandler<IslandClaimListRequestPacket>(OnClaimListRequest)
            .SetMessageHandler<IslandClaimAccessActionPacket>(OnClaimAccessAction)
            .SetMessageHandler<IslandClaimShowRequestPacket>(OnClaimShowRequest);

        api.Event.SaveGameLoaded += OnIslandSaveGameLoaded;
        api.Event.GameWorldSave += OnCoOwnersSaveGameSaving;

        RegisterWorldGen(api);
        RegisterClimateHandlers(api);
        RegisterSpawnHandlers(api);

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
        OnCoOwnersSaveGameLoaded();
        RestoreAllPlayerIslands();
    }
}
