using System;
using SwixySkyBlock.Core;
using System.Collections.Generic;
using System.Linq;
using SwixySkyBlock.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixySkyBlock;

public sealed partial class SwixySkyBlockServerMod
{
    private readonly struct IslandActionResult
    {
        public readonly string? LangKey;
        public readonly object[] Args;
        public readonly string? CompositeMessage;
        public readonly int MessageType;

        private IslandActionResult(string? langKey, object[] args, string? compositeMessage, int messageType)
        {
            LangKey = langKey;
            Args = args;
            CompositeMessage = compositeMessage;
            MessageType = messageType;
        }

        public string Resolve(IServerPlayer player) =>
            !string.IsNullOrEmpty(CompositeMessage)
                ? CompositeMessage
                : string.IsNullOrEmpty(LangKey)
                    ? ""
                    : Lang.GetL(player.LanguageCode, LangKey, Args);

        public static IslandActionResult Success() => new(null, [], null, 0);
        public static IslandActionResult Success(string langKey, params object[] args) => new(langKey, args, null, 0);
        public static IslandActionResult Error(string langKey, params object[] args) => new(langKey, args, null, 1);
    }

    [ProtoBuf.ProtoContract]
    private sealed class CoOwnerSaveData
    {
        [ProtoBuf.ProtoMember(1)]
        public Dictionary<string, List<string>> Entries { get; set; } = [];
    }

    private static string BuildClaimStorageKey(LandClaim claim)
    {
        if (string.IsNullOrWhiteSpace(claim.OwnedByPlayerUid))
        {
            return "";
        }

        var areas = claim.Areas;
        if (areas == null || areas.Count == 0)
        {
            return claim.OwnedByPlayerUid + ":noarea";
        }

        var minX = areas.Min(static a => a.X1);
        var minY = areas.Min(static a => a.Y1);
        var minZ = areas.Min(static a => a.Z1);
        return $"{claim.OwnedByPlayerUid}:{minX}:{minY}:{minZ}";
    }

    private HashSet<string> GetOrCreateCoOwners(LandClaim claim)
    {
        var key = BuildClaimStorageKey(claim);
        if (!coOwnerUidsByClaimKey.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            coOwnerUidsByClaimKey[key] = set;
        }

        return set;
    }

    private void AddCoOwner(LandClaim claim, string playerUid)
    {
        if (!string.IsNullOrWhiteSpace(playerUid))
        {
            GetOrCreateCoOwners(claim).Add(playerUid);
        }
    }

    private void RemoveCoOwner(LandClaim claim, string playerUid)
    {
        var key = BuildClaimStorageKey(claim);
        if (!coOwnerUidsByClaimKey.TryGetValue(key, out var set))
        {
            return;
        }

        set.Remove(playerUid);
        if (set.Count == 0)
        {
            coOwnerUidsByClaimKey.Remove(key);
        }
    }

    private void ClearCoOwners(LandClaim claim) =>
        coOwnerUidsByClaimKey.Remove(BuildClaimStorageKey(claim));

    private static bool IsClaimOwner(LandClaim claim, string playerUid) =>
        claim.OwnedByPlayerUid == playerUid;

    private bool IsCoOwner(LandClaim claim, string playerUid)
    {
        if (string.IsNullOrWhiteSpace(playerUid) || IsClaimOwner(claim, playerUid))
        {
            return false;
        }

        var key = BuildClaimStorageKey(claim);
        return coOwnerUidsByClaimKey.TryGetValue(key, out var set) && set.Contains(playerUid);
    }

    private bool CanManageClaim(LandClaim claim, string playerUid) =>
        IsClaimOwner(claim, playerUid) || IsCoOwner(claim, playerUid);

    private bool TryGetClaimById(int claimId, out LandClaim claim)
    {
        claim = null!;
        var allClaims = serverApi?.World.Claims?.All;
        if (allClaims == null || claimId <= 0 || claimId > allClaims.Count)
        {
            return false;
        }

        claim = allClaims[claimId - 1];
        return true;
    }

    private void TouchClaim(LandClaim claim)
    {
        var claims = serverApi!.World.Claims;
        if (claims.Remove(claim))
        {
            claims.Add(claim);
        }
    }

    private LandClaim? FindIslandClaim(string playerUid)
    {
        return serverApi?.World.Claims?.All
            .FirstOrDefault(c =>
                c.OwnedByPlayerUid == playerUid
                && (c.Description?.StartsWith(SkyBlockWorld.IslandClaimDescriptionPrefix, StringComparison.Ordinal) ?? false));
    }

    private Cuboidi BuildIslandClaimArea(ICoreServerAPI api, IslandTemplate template, BlockPos origin)
    {
        const int chunkSize = GlobalConstants.ChunkSize;
        var radius = SkyBlockWorld.IslandClaimChunkRadius;
        var centerX = origin.X + template.Schematic.SizeX / 2;
        var centerZ = origin.Z + template.Schematic.SizeZ / 2;
        var ccx = centerX / chunkSize;
        var ccz = centerZ / chunkSize;

        var x1 = (ccx - radius) * chunkSize;
        var z1 = (ccz - radius) * chunkSize;
        var x2 = (ccx + radius) * chunkSize + chunkSize - 1;
        var z2 = (ccz + radius) * chunkSize + chunkSize - 1;
        return new Cuboidi(x1, 0, z1, x2, api.WorldManager.MapSizeY - 1, z2);
    }

    private IslandActionResult CreateIslandClaim(IServerPlayer actor, string ownerUid, string ownerName, IslandTemplate template, BlockPos origin)
    {
        if (serverApi == null)
        {
            return IslandActionResult.Error("swixyskyblock:error-server-not-ready");
        }

        var existing = FindIslandClaim(ownerUid);
        if (existing != null)
        {
            existing.Areas ??= [];
            existing.Areas.Clear();
            existing.Description = SkyBlockWorld.IslandClaimDescriptionPrefix + " Island";

            var updatedError = existing.AddArea(BuildIslandClaimArea(serverApi, template, origin));
            if (updatedError != EnumClaimError.NoError)
            {
                return IslandActionResult.Error("swixyskyblock:island-error-claim-failed");
            }

            TouchClaim(existing);
            return IslandActionResult.Success();
        }

        var area = BuildIslandClaimArea(serverApi, template, origin);
        var claim = LandClaim.CreateClaim(actor, SkyBlockConstants.ProtectionLevel);
        claim.OwnedByPlayerUid = ownerUid;
        claim.LastKnownOwnerName = ownerName;
        claim.Description = SkyBlockWorld.IslandClaimDescriptionPrefix + " Island";
        var addError = claim.AddArea(area);
        if (addError != EnumClaimError.NoError)
        {
            return IslandActionResult.Error("swixyskyblock:island-error-claim-failed");
        }

        serverApi.World.Claims.Add(claim);
        return IslandActionResult.Success();
    }

    private void RemoveIslandClaim(string playerUid)
    {
        var claim = FindIslandClaim(playerUid);
        if (claim == null || serverApi == null)
        {
            return;
        }

        serverApi.World.Claims.Remove(claim);
        ClearCoOwners(claim);
    }

    private bool CanViewClaimInList(LandClaim claim, string playerUid) =>
        CanManageClaim(claim, playerUid) || IsIslandResidentOfClaim(claim, playerUid);

    private bool IsIslandResidentOfClaim(LandClaim claim, string playerUid) =>
        IsPlayerIslandClaim(claim, claim.OwnedByPlayerUid)
        && islandResidency.GetHost(playerUid) == claim.OwnedByPlayerUid;

    private bool CanRecreateIslandClaim(LandClaim claim, string playerUid) =>
        IsPlayerIslandClaim(claim, claim.OwnedByPlayerUid)
        && CanManageClaim(claim, playerUid)
        && islandRegistry.Has(claim.OwnedByPlayerUid);

    private IslandClaimListStatePacket BuildClaimListPacket(IServerPlayer player, string message, int messageType)
    {
        var packet = new IslandClaimListStatePacket
        {
            Message = message ?? "",
            MessageType = messageType
        };

        var allClaims = serverApi?.World.Claims?.All;
        if (allClaims == null)
        {
            return packet;
        }

        for (var i = 0; i < allClaims.Count; i++)
        {
            var claim = allClaims[i];
            if (!CanViewClaimInList(claim, player.PlayerUID))
            {
                continue;
            }

            packet.Claims.Add(BuildClaimInfo(i + 1, claim, player));
        }

        return packet;
    }

    private IslandClaimInfoPacket BuildClaimInfo(int claimId, LandClaim claim, IServerPlayer viewer)
    {
        var langCode = viewer.LanguageCode;
        var ownerName = ResolveClaimOwnerName(claim, langCode);
        var viewerCanManage = CanManageClaim(claim, viewer.PlayerUID);
        var viewerCanLeave = IsIslandResidentOfClaim(claim, viewer.PlayerUID) && !viewerCanManage;
        var info = new IslandClaimInfoPacket
        {
            ClaimId = claimId,
            Name = string.IsNullOrWhiteSpace(claim.Description) ? ownerName : claim.Description,
            OwnerName = ownerName,
            ViewerIsCoOwner = IsCoOwner(claim, viewer.PlayerUID),
            ViewerCanLeave = viewerCanLeave,
            IsIslandClaim = IsPlayerIslandClaim(claim, claim.OwnedByPlayerUid),
            AreaCount = claim.Areas?.Count ?? 0,
            Volume = claim.SizeXYZ,
            ChunkCount = BlocksToChunkCount(claim.SizeXYZ, serverApi!.WorldManager.ChunkSize, serverApi.WorldManager.MapSizeY)
        };

        info.Members.Add(new IslandClaimMemberPacket
        {
            PlayerUid = claim.OwnedByPlayerUid,
            PlayerName = info.OwnerName,
            AccessFlags = (int)(EnumBlockAccessFlags.Use | EnumBlockAccessFlags.BuildOrBreak),
            AccessName = Lang.GetL(langCode, "swixyskyblock:island-claims-owner-role"),
            IsOwner = true
        });

        if (claim.PermittedPlayerUids != null)
        {
            foreach (var entry in claim.PermittedPlayerUids.OrderBy(p => ResolvePlayerName(p.Key, langCode: langCode), StringComparer.OrdinalIgnoreCase))
            {
                if (entry.Key == claim.OwnedByPlayerUid)
                {
                    continue;
                }

                var flags = (EnumBlockAccessFlags)entry.Value;
                info.Members.Add(new IslandClaimMemberPacket
                {
                    PlayerUid = entry.Key,
                    PlayerName = ResolvePlayerName(entry.Key, langCode: langCode),
                    AccessFlags = (int)entry.Value,
                    AccessName = FormatMemberAccessName(langCode, flags, false, IsCoOwner(claim, entry.Key)),
                    IsCoOwner = IsCoOwner(claim, entry.Key)
                });
            }
        }

        return info;
    }

    private static long BlocksToChunkCount(long blocks, int chunkSize, int mapSizeY)
    {
        var chunkVolume = (long)chunkSize * chunkSize * mapSizeY;
        return chunkVolume <= 0 ? 0 : (blocks + chunkVolume - 1) / chunkVolume;
    }

    private static string FormatMemberAccessName(string langCode, EnumBlockAccessFlags flags, bool isOwner, bool isCoOwner)
    {
        if (isOwner)
        {
            return Lang.GetL(langCode, "swixyskyblock:island-claims-owner-role");
        }

        if (isCoOwner)
        {
            return Lang.GetL(langCode, "swixyskyblock:island-claims-coowner-role");
        }

        var parts = new List<string>();
        if (flags.HasFlag(EnumBlockAccessFlags.Use))
        {
            parts.Add(Lang.GetL(langCode, "swixyskyblock:island-claims-access-use"));
        }

        if (flags.HasFlag(EnumBlockAccessFlags.BuildOrBreak))
        {
            parts.Add(Lang.GetL(langCode, "swixyskyblock:island-claims-access-build"));
        }

        return parts.Count > 0
            ? string.Join(", ", parts)
            : Lang.GetL(langCode, "swixyskyblock:island-claims-access-none");
    }

    private string ResolvePlayerName(string? playerUid, string? fallbackName = null, string? langCode = null)
    {
        if (string.IsNullOrWhiteSpace(playerUid))
        {
            return string.IsNullOrWhiteSpace(fallbackName) ? "?" : fallbackName;
        }

        if (serverApi == null)
        {
            return !string.IsNullOrWhiteSpace(fallbackName) ? fallbackName : playerUid;
        }

        var online = serverApi.World.AllOnlinePlayers
            .FirstOrDefault(p => p.PlayerUID == playerUid);
        if (online != null)
        {
            return online.PlayerName;
        }

        var playerDataName = serverApi.PlayerData.GetPlayerDataByUid(playerUid)?.LastKnownPlayername;
        if (!string.IsNullOrWhiteSpace(playerDataName))
        {
            return playerDataName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(fallbackName) && !string.Equals(fallbackName, playerUid, StringComparison.Ordinal))
        {
            return fallbackName.Trim();
        }

        var code = string.IsNullOrWhiteSpace(langCode) ? Lang.CurrentLocale : langCode;
        return Lang.GetL(code, "swixyskyblock:island-claims-unknown-player");
    }

    private string ResolveClaimOwnerName(LandClaim claim, string langCode) =>
        ResolvePlayerName(claim.OwnedByPlayerUid, claim.LastKnownOwnerName, langCode);

    private void SendClaimList(IServerPlayer player, IslandActionResult result) =>
        SendClaimList(player, result.Resolve(player), result.MessageType);

    private void SendClaimList(IServerPlayer player, string message, int messageType)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        serverChannel.SendPacket(BuildClaimListPacket(player, message, messageType), [player]);
    }

    private IslandActionResult ProcessClaimAccessAction(IServerPlayer player, IslandClaimAccessActionPacket packet)
    {
        if (serverApi == null)
        {
            return IslandActionResult.Error("swixyskyblock:error-server-not-ready");
        }

        if (!TryGetClaimById(packet.ClaimId, out var claim) || !CanViewClaimInList(claim, player.PlayerUID))
        {
            return IslandActionResult.Error("swixyskyblock:island-claims-error-not-found");
        }

        return packet.Action switch
        {
            IslandAccessActionType.Refresh => IslandActionResult.Success(),
            IslandAccessActionType.AddPlayer => IsClaimOwner(claim, player.PlayerUID)
                ? TryAddClaimMember(player, claim, packet.PlayerName, packet.PlayerUid,
                    (EnumBlockAccessFlags)packet.AccessFlags)
                : IslandActionResult.Error("swixyskyblock:island-claims-error-owner-only-add"),
            IslandAccessActionType.RemovePlayer => CanManageClaim(claim, player.PlayerUID)
                ? TryRemoveClaimMember(claim, packet.PlayerName, packet.PlayerUid)
                : IslandActionResult.Error("swixyskyblock:island-claims-error-not-found"),
            IslandAccessActionType.RenameClaim => CanManageClaim(claim, player.PlayerUID)
                ? TryRenameClaim(claim, packet.ClaimName)
                : IslandActionResult.Error("swixyskyblock:island-claims-error-not-found"),
            IslandAccessActionType.DeleteClaim => IsClaimOwner(claim, player.PlayerUID)
                ? DeleteManagedClaim(player, claim)
                : IslandActionResult.Error("swixyskyblock:island-claims-error-coowner-cannot-delete"),
            IslandAccessActionType.UpdateMemberAccess => CanManageClaim(claim, player.PlayerUID)
                ? TryUpdateClaimMemberAccess(claim, packet.PlayerName, packet.PlayerUid,
                    (EnumBlockAccessFlags)packet.AccessFlags)
                : IslandActionResult.Error("swixyskyblock:island-claims-error-not-found"),
            IslandAccessActionType.GrantCoOwnership => IsClaimOwner(claim, player.PlayerUID)
                ? TryToggleCoOwnership(claim, packet.PlayerName, packet.PlayerUid)
                : IslandActionResult.Error("swixyskyblock:island-claims-error-owner-only-crown"),
            IslandAccessActionType.RecreateIsland => CanRecreateIslandClaim(claim, player.PlayerUID)
                ? RecreateIslandFromClaim(player, claim)
                : IslandActionResult.Error("swixyskyblock:island-error-no-island"),
            IslandAccessActionType.LeaveIsland => TryLeaveIsland(player, claim),
            _ => IslandActionResult.Error("swixyskyblock:error-unknown")
        };
    }

    private IslandActionResult TryLeaveIsland(IServerPlayer player, LandClaim claim)
    {
        if (!IsIslandResidentOfClaim(claim, player.PlayerUID))
        {
            return IslandActionResult.Error("swixyskyblock:island-claims-error-not-resident");
        }

        TryRemoveClaimMember(claim, "", player.PlayerUID);
        islandResidency.Remove(serverApi!, player.PlayerUID);
        TeleportToHubSpawn(player, updateRespawnSpawn: true);
        var result = IslandActionResult.Success("swixyskyblock:island-message-left-island");
        SendHubState(player, result);
        return result;
    }

    private IslandActionResult RecreateIslandFromClaim(IServerPlayer player, LandClaim claim)
    {
        var record = islandRegistry.Get(claim.OwnedByPlayerUid);
        if (record == null)
        {
            return IslandActionResult.Error("swixyskyblock:island-error-no-island");
        }

        BeginRecreatePlayerIsland(player, record, "");
        return IslandActionResult.Success("swixyskyblock:island-message-creating");
    }

    private IslandActionResult DeleteManagedClaim(IServerPlayer player, LandClaim claim)
    {
        if (IsPlayerIslandClaim(claim, claim.OwnedByPlayerUid) && islandRegistry.Has(player.PlayerUID))
        {
            DestroyPlayerIslandData(player, removeClaim: false);
            serverApi!.World.Claims.Remove(claim);
            ClearCoOwners(claim);
            var result = IslandActionResult.Success("swixyskyblock:island-message-deleted");
            SendHubState(player, result);
            return result;
        }

        serverApi!.World.Claims.Remove(claim);
        ClearCoOwners(claim);
        return IslandActionResult.Success("swixyskyblock:island-claims-message-deleted");
    }

    private IslandActionResult TryAddClaimMember(IServerPlayer owner, LandClaim claim, string playerName, string playerUid, EnumBlockAccessFlags accessFlags)
    {
        var playerData = ResolvePlayerData(playerName, playerUid);
        if (playerData == null)
        {
            return IslandActionResult.Error("swixyskyblock:island-claims-error-player-not-found");
        }

        if (playerData.PlayerUID == owner.PlayerUID)
        {
            return IslandActionResult.Error("swixyskyblock:island-claims-error-owner-member");
        }

        if (IsPlayerIslandClaim(claim, claim.OwnedByPlayerUid))
        {
            if (islandRegistry.Has(playerData.PlayerUID))
            {
                return IslandActionResult.Error("swixyskyblock:island-error-already-has-island");
            }

            if (islandResidency.Has(playerData.PlayerUID))
            {
                return IslandActionResult.Error("swixyskyblock:island-error-already-resident");
            }

            if (!islandRegistry.Has(claim.OwnedByPlayerUid))
            {
                return IslandActionResult.Error("swixyskyblock:island-error-host-no-island");
            }
        }

        claim.PermittedPlayerUids ??= [];
        claim.PermittedPlayerUids[playerData.PlayerUID] = accessFlags;
        TouchClaim(claim);

        if (IsPlayerIslandClaim(claim, claim.OwnedByPlayerUid))
        {
            islandResidency.Set(serverApi!, playerData.PlayerUID, claim.OwnedByPlayerUid);
            if (serverApi.World.PlayerByUid(playerData.PlayerUID) is IServerPlayer memberPlayer)
            {
                ApplyPlayerHomeSpawn(memberPlayer);
                TeleportToPlayerIsland(memberPlayer, islandRegistry.Get(claim.OwnedByPlayerUid)!);
                SendHubState(memberPlayer, IslandActionResult.Success("swixyskyblock:island-message-joined-island", ResolvePlayerName(claim.OwnedByPlayerUid)));
            }
        }

        return IslandActionResult.Success("swixyskyblock:island-claims-message-player-added", ResolvePlayerName(playerData.PlayerUID, playerName));
    }

    private IslandActionResult TryUpdateClaimMemberAccess(LandClaim claim, string playerName, string playerUid, EnumBlockAccessFlags accessFlags)
    {
        if (!TryResolveMemberUid(claim, playerName, playerUid, out var memberUid))
        {
            return IslandActionResult.Error("swixyskyblock:island-claims-error-member-not-found");
        }

        if (memberUid == claim.OwnedByPlayerUid)
        {
            return IslandActionResult.Error("swixyskyblock:island-claims-error-owner-member");
        }

        claim.PermittedPlayerUids![memberUid] = accessFlags;
        TouchClaim(claim);
        return IslandActionResult.Success();
    }

    private IslandActionResult TryRemoveClaimMember(LandClaim claim, string playerName, string playerUid)
    {
        if (!TryResolveMemberUid(claim, playerName, playerUid, out var memberUid))
        {
            return IslandActionResult.Error("swixyskyblock:island-claims-error-member-not-found");
        }

        if (memberUid == claim.OwnedByPlayerUid)
        {
            return IslandActionResult.Error("swixyskyblock:island-claims-error-cannot-remove-owner");
        }

        claim.PermittedPlayerUids?.Remove(memberUid);
        RemoveCoOwner(claim, memberUid);
        if (IsPlayerIslandClaim(claim, claim.OwnedByPlayerUid))
        {
            islandResidency.Remove(serverApi!, memberUid);
            if (serverApi.World.PlayerByUid(memberUid) is IServerPlayer removedPlayer)
            {
                TeleportToHubSpawn(removedPlayer, updateRespawnSpawn: true);
                SendHubState(removedPlayer, IslandActionResult.Success("swixyskyblock:island-message-removed-from-island"));
            }
        }

        TouchClaim(claim);
        return IslandActionResult.Success("swixyskyblock:island-claims-message-player-removed", ResolvePlayerName(memberUid, playerName));
    }

    private IslandActionResult TryToggleCoOwnership(LandClaim claim, string playerName, string playerUid)
    {
        if (!TryResolveMemberUid(claim, playerName, playerUid, out var memberUid))
        {
            return IslandActionResult.Error("swixyskyblock:island-claims-error-member-not-found");
        }

        if (memberUid == claim.OwnedByPlayerUid)
        {
            return IslandActionResult.Error("swixyskyblock:island-claims-error-owner-member");
        }

        if (IsCoOwner(claim, memberUid))
        {
            RemoveCoOwner(claim, memberUid);
            TouchClaim(claim);
            return IslandActionResult.Success("swixyskyblock:island-claims-message-coowner-revoked", ResolvePlayerName(memberUid, playerName));
        }

        AddCoOwner(claim, memberUid);
        TouchClaim(claim);
        return IslandActionResult.Success("swixyskyblock:island-claims-message-coowner-granted", ResolvePlayerName(memberUid, playerName));
    }

    private IslandActionResult TryRenameClaim(LandClaim claim, string claimName)
    {
        claimName = claimName.Trim();
        if (string.IsNullOrWhiteSpace(claimName))
        {
            return IslandActionResult.Error("swixyskyblock:island-claims-error-empty-name");
        }

        claim.Description = claimName;
        TouchClaim(claim);
        return IslandActionResult.Success("swixyskyblock:island-claims-message-renamed");
    }

    private IServerPlayerData? ResolvePlayerData(string playerName, string playerUid = "")
    {
        playerName = playerName.Trim();
        playerUid = playerUid.Trim();
        if (serverApi == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(playerUid))
        {
            var byUid = serverApi.PlayerData.GetPlayerDataByUid(playerUid);
            if (byUid != null)
            {
                return byUid;
            }
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return null;
        }

        var online = serverApi.World.AllOnlinePlayers
            .FirstOrDefault(p => string.Equals(p.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
        if (online != null)
        {
            return serverApi.PlayerData.GetPlayerDataByUid(online.PlayerUID);
        }

        return serverApi.PlayerData.GetPlayerDataByLastKnownName(playerName);
    }

    private bool TryResolveMemberUid(LandClaim claim, string playerName, string playerUid, out string memberUid)
    {
        memberUid = playerUid.Trim();
        if (!string.IsNullOrWhiteSpace(memberUid))
        {
            return claim.PermittedPlayerUids?.ContainsKey(memberUid) == true;
        }

        var playerData = ResolvePlayerData(playerName);
        if (playerData == null)
        {
            return false;
        }

        memberUid = playerData.PlayerUID;
        return claim.PermittedPlayerUids?.ContainsKey(memberUid) == true;
    }

    private void OnCoOwnersSaveGameLoaded()
    {
        coOwnerUidsByClaimKey.Clear();
        var bytes = serverApi?.WorldManager.SaveGame.GetData(SkyBlockWorld.CoOwnersSaveKey);
        if (bytes == null || bytes.Length == 0)
        {
            return;
        }

        var data = SerializerUtil.Deserialize<CoOwnerSaveData>(bytes);
        foreach (var entry in data.Entries)
        {
            coOwnerUidsByClaimKey[entry.Key] = new HashSet<string>(entry.Value, StringComparer.Ordinal);
        }
    }

    private void OnCoOwnersSaveGameSaving()
    {
        if (serverApi == null)
        {
            return;
        }

        var data = new CoOwnerSaveData();
        foreach (var entry in coOwnerUidsByClaimKey)
        {
            data.Entries[entry.Key] = entry.Value.ToList();
        }

        serverApi.WorldManager.SaveGame.StoreData(SkyBlockWorld.CoOwnersSaveKey, SerializerUtil.Serialize(data));
    }
}
