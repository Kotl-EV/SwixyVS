using System;
using System.Linq;
using SwixySkyBlock.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

public sealed partial class SwixySkyBlockMod
{
    private IslandHubStatePacket BuildHubStatePacket(IServerPlayer player, string message, int messageType)
    {
        var record = islandRegistry.Get(player.PlayerUID);
        var templates = IslandBlueprint.LoadAll(serverApi!);
        return new IslandHubStatePacket
        {
            HasIsland = record != null,
            IsIslandResident = islandResidency.Has(player.PlayerUID),
            TemplateName = record?.TemplateName ?? "",
            AvailableTemplates = templates.Select(static t => t.Name).ToList(),
            Message = message ?? "",
            MessageType = messageType
        };
    }

    private void SendHubState(IServerPlayer player, IslandActionResult result) =>
        SendHubState(player, result.Resolve(player), result.MessageType);

    private void SendHubState(IServerPlayer player, string message, int messageType)
    {
        if (serverChannel == null)
        {
            return;
        }

        serverChannel.SendPacket(BuildHubStatePacket(player, message, messageType), [player]);
    }

    private IslandActionResult ProcessIslandAction(IServerPlayer player, IslandActionPacket packet) =>
        packet.Action switch
        {
            IslandHubActionType.Refresh => IslandActionResult.Success(),
            IslandHubActionType.GoHome => TeleportPlayerHome(player),
            IslandHubActionType.GoSpawn => TeleportPlayerToHubSpawn(player),
            _ => IslandActionResult.Error("swixyskyblock:error-unknown")
        };

    private IslandActionResult TeleportPlayerHome(IServerPlayer player)
    {
        if (serverApi == null)
        {
            return IslandActionResult.Error("swixyskyblock:error-server-not-ready");
        }

        if (!TryTeleportPlayerHome(player))
        {
            return IslandActionResult.Error("swixyskyblock:island-error-no-island");
        }

        return IslandActionResult.Success("swixyskyblock:island-message-teleport-home");
    }

    private IslandActionResult TeleportPlayerToHubSpawn(IServerPlayer player)
    {
        if (serverApi == null)
        {
            return IslandActionResult.Error("swixyskyblock:error-server-not-ready");
        }

        TeleportToHubSpawn(player, updateRespawnSpawn: false);
        return IslandActionResult.Success("swixyskyblock:island-message-teleport-spawn");
    }

    private bool IsPlayerIslandClaim(LandClaim claim, string playerUid) =>
        claim.OwnedByPlayerUid == playerUid
        && (claim.Description?.StartsWith(SkyBlockWorld.IslandClaimDescriptionPrefix, StringComparison.Ordinal) ?? false);

    private void DestroyPlayerIslandData(IServerPlayer player, bool removeClaim)
    {
        if (serverApi == null)
        {
            return;
        }

        var record = islandRegistry.Get(player.PlayerUID);
        if (record == null)
        {
            return;
        }

        var template = ResolveIslandTemplate(record.TemplateName);
        if (template != null)
        {
            IslandPlacementService.ClearIslandVolume(serverApi, template, record.Origin);
        }

        if (removeClaim)
        {
            RemoveIslandClaim(player.PlayerUID);
        }

        islandRegistry.Remove(serverApi, player.PlayerUID);
        islandResidency.RemoveAllForHost(serverApi, player.PlayerUID);
        TeleportToIslandSpawn(player);
    }

    private bool TryTeleportPlayerHome(IServerPlayer player)
    {
        if (serverApi == null)
        {
            return false;
        }

        var record = islandRegistry.Get(player.PlayerUID);
        if (record != null)
        {
            TeleportToPlayerIsland(player, record);
            return true;
        }

        var hostUid = islandResidency.GetHost(player.PlayerUID);
        if (hostUid == null)
        {
            return false;
        }

        var hostRecord = islandRegistry.Get(hostUid);
        if (hostRecord == null)
        {
            return false;
        }

        TeleportToPlayerIsland(player, hostRecord);
        return true;
    }

    private void ApplyPlayerHomeSpawn(IServerPlayer player)
    {
        if (serverApi == null)
        {
            return;
        }

        var record = islandRegistry.Get(player.PlayerUID);
        if (record != null)
        {
            EnsurePlayerSpawnInsideIsland(player, record);
            return;
        }

        var hostUid = islandResidency.GetHost(player.PlayerUID);
        if (hostUid == null)
        {
            return;
        }

        var hostRecord = islandRegistry.Get(hostUid);
        if (hostRecord == null)
        {
            return;
        }

        EnsurePlayerSpawnInsideIsland(player, hostRecord);
    }

    private void BeginCreatePlayerIsland(IServerPlayer player, string templateName)
    {
        if (serverApi == null)
        {
            SendHubState(player, IslandActionResult.Error("swixyskyblock:error-server-not-ready"));
            return;
        }

        if (islandRegistry.Has(player.PlayerUID))
        {
            SendHubState(player, IslandActionResult.Error("swixyskyblock:island-error-already-exists"));
            return;
        }

        if (islandResidency.Has(player.PlayerUID))
        {
            SendHubState(player, IslandActionResult.Error("swixyskyblock:island-error-resident-cannot-create"));
            return;
        }

        var template = ResolveIslandTemplate(templateName);
        if (template == null)
        {
            SendHubState(player, IslandActionResult.Error("swixyskyblock:island-error-template-not-found"));
            return;
        }

        var record = islandRegistry.Create(serverApi, player.PlayerUID, template.Name);
        SendHubState(player, IslandActionResult.Success("swixyskyblock:island-message-creating"));

        PlacePlayerIslandAsync(player, record, template, player.PlayerUID, player.PlayerName, onSuccess: () =>
            IslandActionResult.Success("swixyskyblock:island-message-created", template.Name));
    }

    private void BeginRecreatePlayerIsland(IServerPlayer player, string templateName)
    {
        if (serverApi == null)
        {
            SendHubState(player, IslandActionResult.Error("swixyskyblock:error-server-not-ready"));
            return;
        }

        var record = islandRegistry.Get(player.PlayerUID);
        if (record == null)
        {
            if (!string.IsNullOrWhiteSpace(templateName))
            {
                BeginCreatePlayerIsland(player, templateName);
                return;
            }

            SendHubState(player, IslandActionResult.Error("swixyskyblock:island-error-no-island"));
            return;
        }

        BeginRecreatePlayerIsland(player, record, templateName);
    }

    private void BeginRecreatePlayerIsland(IServerPlayer actor, PlayerIslandRecord record, string templateName)
    {
        if (serverApi == null)
        {
            SendHubState(actor, IslandActionResult.Error("swixyskyblock:error-server-not-ready"));
            return;
        }

        var actorOwnsRecord = record.PlayerUid == actor.PlayerUID;
        if (actorOwnsRecord && islandResidency.Has(actor.PlayerUID))
        {
            SendHubState(actor, IslandActionResult.Error("swixyskyblock:island-error-resident-cannot-recreate"));
            return;
        }

        var oldTemplate = ResolveIslandTemplate(record.TemplateName);
        if (oldTemplate != null)
        {
            IslandPlacementService.ClearIslandVolume(serverApi, oldTemplate, record.Origin);
        }

        if (!string.IsNullOrWhiteSpace(templateName))
        {
            record.TemplateName = templateName.Trim();
            islandRegistry.Save(serverApi);
        }

        var newTemplate = ResolveIslandTemplate(record.TemplateName);
        if (newTemplate == null)
        {
            SendHubState(actor, IslandActionResult.Error("swixyskyblock:island-error-template-not-found"));
            return;
        }

        SendHubState(actor, IslandActionResult.Success("swixyskyblock:island-message-creating"));
        SendClaimList(actor, IslandActionResult.Success("swixyskyblock:island-message-creating"));

        PlacePlayerIslandAsync(
            actor,
            record,
            newTemplate,
            record.PlayerUid,
            ResolvePlayerName(record.PlayerUid),
            onSuccess: () =>
            IslandActionResult.Success("swixyskyblock:island-message-recreated", newTemplate.Name));
    }

    private void PlacePlayerIslandAsync(
        IServerPlayer actor,
        PlayerIslandRecord record,
        IslandTemplate template,
        string ownerUid,
        string ownerName,
        System.Func<IslandActionResult> onSuccess)
    {
        if (serverApi == null)
        {
            return;
        }

        IslandPlacementService.PlaceIslandAsync(serverApi, template, record.Origin, success =>
        {
            serverApi.Event.EnqueueMainThreadTask(() =>
            {
                if (!success)
                {
                    if (record.PlayerUid == actor.PlayerUID)
                    {
                        islandRegistry.Remove(serverApi, actor.PlayerUID);
                    }

                    SendHubState(actor, IslandActionResult.Error("swixyskyblock:island-error-place-failed"));
                    return;
                }

                var claimResult = CreateIslandClaim(actor, ownerUid, ownerName, template, record.Origin);
                if (!string.IsNullOrEmpty(claimResult.LangKey))
                {
                    serverApi.Logger.Warning(
                        "[SwixySkyBlock] Island claim failed for {0}: {1}",
                        actor.PlayerName,
                        claimResult.Resolve(actor));
                }

                TeleportToPlayerIsland(actor, record);
                var result = onSuccess();
                SendHubState(actor, result);
                SendClaimList(actor, result);
            }, "swixyskyblock-place-island");
        });
    }

    private IslandTemplate? ResolveIslandTemplate(string? templateName)
    {
        if (serverApi == null)
        {
            return null;
        }

        var templates = IslandBlueprint.LoadAll(serverApi);
        if (templates.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(templateName))
        {
            return templates.FirstOrDefault(t => t.Name == templateName.Trim())
                ?? IslandBlueprint.PickForWorld(templates);
        }

        return IslandBlueprint.PickForWorld(templates);
    }

    private void TeleportToPlayerIsland(IServerPlayer player, PlayerIslandRecord record)
    {
        if (serverApi == null)
        {
            return;
        }

        var spawn = GetPlayerHomeSpawn(player, record);
        EnsurePlayerSpawnInsideIsland(player, record);
        player.Entity.TeleportTo(spawn.ToVec3d().Add(0.5, 0, 0.5));
    }

    private BlockPos GetPlayerHomeSpawn(IServerPlayer player, PlayerIslandRecord record)
    {
        var savedSpawn = player.GetSpawnPosition(consumeSpawnUse: false);
        if (savedSpawn != null)
        {
            var savedPos = ToBlockPos(savedSpawn);
            if (IsPositionInsideIsland(record, savedPos))
            {
                return savedPos;
            }
        }

        return islandRegistry.GetSpawn(serverApi!, record);
    }

    private void EnsurePlayerSpawnInsideIsland(IServerPlayer player, PlayerIslandRecord record)
    {
        var savedSpawn = player.GetSpawnPosition(consumeSpawnUse: false);
        if (savedSpawn != null && IsPositionInsideIsland(record, ToBlockPos(savedSpawn)))
        {
            return;
        }

        var fallbackSpawn = islandRegistry.GetSpawn(serverApi!, record);
        player.SetSpawnPosition(new PlayerSpawnPos(fallbackSpawn.X, fallbackSpawn.Y, fallbackSpawn.Z));
    }

    private static BlockPos ToBlockPos(FuzzyEntityPos spawn) =>
        new((int)Math.Floor(spawn.X), (int)Math.Floor(spawn.Y), (int)Math.Floor(spawn.Z));

    private bool IsPositionInsideIsland(PlayerIslandRecord record, BlockPos pos)
    {
        var claim = FindIslandClaim(record.PlayerUid);
        if (claim?.Areas != null)
        {
            foreach (var area in claim.Areas)
            {
                if (pos.X >= area.X1 && pos.X <= area.X2
                    && pos.Y >= area.Y1 && pos.Y <= area.Y2
                    && pos.Z >= area.Z1 && pos.Z <= area.Z2)
                {
                    return true;
                }
            }
        }

        var template = ResolveIslandTemplate(record.TemplateName);
        if (template == null)
        {
            return false;
        }

        var bounds = template.GetBounds(record.Origin);
        return pos.X >= bounds.X1 && pos.X <= bounds.X2
            && pos.Y >= bounds.Y1 && pos.Y <= bounds.Y2 + 2
            && pos.Z >= bounds.Z1 && pos.Z <= bounds.Z2;
    }

    private void RestoreAllPlayerIslands()
    {
        if (!UsePerPlayerIslands || serverApi == null)
        {
            return;
        }

        var templates = IslandBlueprint.LoadAll(serverApi);
        if (templates.Count == 0)
        {
            serverApi.Logger.Warning("[SwixySkyBlock] No player island templates available; restore skipped.");
            return;
        }

        foreach (var record in islandRegistry.All.ToList())
        {
            var template = templates.FirstOrDefault(t => t.Name == record.TemplateName)
                ?? IslandBlueprint.PickForWorld(templates);
            if (IslandPlacer.IsSurfacePresent(serverApi.World.BlockAccessor, record.Origin, template))
            {
                continue;
            }

            IslandPlacementService.PlaceIslandAsync(serverApi, template, record.Origin, success =>
            {
                if (success)
                {
                    serverApi.Logger.Notification(
                        "[SwixySkyBlock] Restored island for {0} at {1}.",
                        record.PlayerUid,
                        record.Origin);
                }
            });
        }
    }

    private void TryPlaceRegisteredIslandsOnChunkLoad(Vec2i chunkCoord, IWorldChunk[] chunks)
    {
        if (serverApi == null)
        {
            return;
        }

        var templates = IslandBlueprint.LoadAll(serverApi);
        if (templates.Count == 0)
        {
            return;
        }

        foreach (var record in islandRegistry.All)
        {
            var template = templates.FirstOrDefault(t => t.Name == record.TemplateName)
                ?? IslandBlueprint.PickForWorld(templates);
            IslandPlacementService.TryPlaceRegisteredIslandAtChunk(
                serverApi,
                record,
                template,
                chunkCoord,
                chunks);
        }
    }
}
