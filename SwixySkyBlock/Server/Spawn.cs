using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Спавн игрока на острове и спасение из пустоты.</summary>
public sealed partial class SwixySkyBlockServerMod
{
    private readonly Dictionary<string, long> voidRescueCooldownUntilMs = new();

    private void RegisterSpawnHandlers(ICoreServerAPI api)
    {
        api.Event.PlayerCreate += OnPlayerCreate;
        api.Event.PlayerRespawn += OnPlayerRespawn;
        api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
        api.Event.RegisterGameTickListener(OnVoidFallCheck, 250);
    }

    private void OnPlayerCreate(IServerPlayer player)
    {
        if (!IsSkyBlockWorld || serverApi == null)
        {
            return;
        }

        PrepareWorldForPlayer();
        TeleportToIslandSpawn(player);
    }

    private void OnPlayerRespawn(IServerPlayer player)
    {
        if (!IsSkyBlockWorld || serverApi == null)
        {
            return;
        }

        PrepareWorldForPlayer();
        TeleportToIslandSpawn(player);
    }

    private void OnPlayerNowPlaying(IServerPlayer player)
    {
        if (!IsSkyBlockWorld || serverApi == null)
        {
            return;
        }

        PrepareWorldForPlayer();
        TeleportToIslandSpawn(player);
    }

    private void OnVoidFallCheck(float _)
    {
        if (!IsSkyBlockWorld || serverApi == null)
        {
            return;
        }

        var thresholdY = SkyBlockWorld.GetVoidFallThresholdY();
        var now = serverApi.World.ElapsedMilliseconds;

        foreach (var onlinePlayer in serverApi.World.AllOnlinePlayers)
        {
            if (onlinePlayer is not IServerPlayer player)
            {
                continue;
            }

            var entity = player.Entity;
            if (entity == null || !entity.Alive)
            {
                continue;
            }

            if (entity.Pos.Dimension != 0)
            {
                continue;
            }

            if (entity.Pos.Y >= thresholdY)
            {
                continue;
            }

            if (voidRescueCooldownUntilMs.TryGetValue(player.PlayerUID, out var until) && now < until)
            {
                continue;
            }

            voidRescueCooldownUntilMs[player.PlayerUID] = now + 2000;
            RescuePlayerFromVoid(player);
        }
    }

    private void RescuePlayerFromVoid(IServerPlayer player)
    {
        if (serverApi == null)
        {
            return;
        }

        player.Entity.Pos.Motion.Set(0, 0, 0);

        if (TryTeleportPlayerHome(player))
        {
            var langKey = islandRegistry.Has(player.PlayerUID)
                ? "swixyskyblock:island-message-void-rescue-home"
                : "swixyskyblock:island-message-void-rescue-resident";
            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                Lang.GetL(player.LanguageCode, langKey),
                EnumChatType.Notification);
            return;
        }

        TeleportToHubSpawn(player, updateRespawnSpawn: false);
        player.SendMessage(
            GlobalConstants.GeneralChatGroup,
            Lang.GetL(player.LanguageCode, "swixyskyblock:island-message-void-rescue-spawn"),
            EnumChatType.Notification);
    }

    private void PrepareWorldForPlayer()
    {
        if (serverApi == null)
        {
            return;
        }

        if (!UsePerPlayerIslands)
        {
            TryApplyDefaultSpawn(serverApi);
            EnsureIslandPlaced(serverApi);
        }
    }

    private void TeleportToIslandSpawn(IServerPlayer player)
    {
        if (serverApi == null)
        {
            return;
        }

        var record = islandRegistry.Get(player.PlayerUID);
        if (record != null)
        {
            TeleportToPlayerIsland(player, record);
            return;
        }

        if (TryTeleportPlayerHome(player))
        {
            return;
        }

        TeleportToHubSpawn(player, updateRespawnSpawn: true);
    }

    private void TeleportToHubSpawn(IServerPlayer player, bool updateRespawnSpawn)
    {
        if (serverApi == null)
        {
            return;
        }

        if (UsePerPlayerIslands)
        {
            var center = SkyBlockWorld.ComputeIslandOrigin(serverApi);
            var voidSpawn = new BlockPos(center.X, center.Y + 1, center.Z);

            if (updateRespawnSpawn)
            {
                player.SetSpawnPosition(new PlayerSpawnPos(voidSpawn.X, voidSpawn.Y, voidSpawn.Z));
            }

            player.Entity.TeleportTo(voidSpawn.ToVec3d().Add(0.5, 0, 0.5));
            return;
        }

        var spawn = islandSpawn;

        if (updateRespawnSpawn)
        {
            player.SetSpawnPosition(new PlayerSpawnPos(spawn.X, spawn.Y, spawn.Z));
        }

        player.Entity.TeleportTo(spawn.ToVec3d().Add(0.5, 0, 0.5));
    }
}
