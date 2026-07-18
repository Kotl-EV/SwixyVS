using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Core;
using SwixyClaimChunk.Net;
using ProtoBuf;
using static SwixyClaimChunk.Core.ClaimVolumeUtil;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkServerMod"/> — сервер: доступ участников.</summary>
public sealed partial class SwixyClaimChunkServerMod
{
    /// <summary>Маршрутизирует ClaimAccessActionPacket по типу действия.</summary>
    private ClaimActionResult ProcessClaimAccessAction(IServerPlayer player, ClaimAccessActionPacket packet)
    {
        if (serverApi == null)
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-server-not-ready");
        }

        if (!TryGetClaimById(packet.ClaimId, out var claim) || !CanManageClaim(claim, player.PlayerUID))
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-not-found");
        }

        switch (packet.Action)
        {
            case ClaimAccessActionType.Refresh:
                return ClaimActionResult.Success();
            case ClaimAccessActionType.AddPlayer:
                return TryAddClaimMember(player, claim, packet.PlayerName, packet.PlayerUid, (EnumBlockAccessFlags)packet.AccessFlags);
            case ClaimAccessActionType.RemovePlayer:
                return TryRemoveClaimMember(claim, packet.PlayerName, packet.PlayerUid);
            case ClaimAccessActionType.RenameClaim:
                return TryRenameClaim(claim, packet.ClaimName);
            case ClaimAccessActionType.DeleteClaim:
                if (!IsClaimOwner(claim, player.PlayerUID))
                {
                    return ClaimActionResult.Error("swixyclaimchunk:claims-error-coowner-cannot-delete");
                }

                serverApi.World.Claims.Remove(claim);
                ClearCoOwners(claim);
                ClearUseFilter(claim);
                ClearClaimFlags(claim);
                return ClaimActionResult.Success("swixyclaimchunk:claims-message-deleted");
            case ClaimAccessActionType.UpdateMemberAccess:
                return TryUpdateClaimMemberAccess(claim, packet.PlayerName, packet.PlayerUid, (EnumBlockAccessFlags)packet.AccessFlags);
            case ClaimAccessActionType.GrantCoOwnership:
                if (!IsClaimOwner(claim, player.PlayerUID))
                {
                    return ClaimActionResult.Error("swixyclaimchunk:claims-error-owner-only-crown");
                }

                return TryToggleCoOwnership(claim, packet.PlayerName, packet.PlayerUid);
            case ClaimAccessActionType.SetUseFilter:
            {
                // Коды идут строкой UseFilterCodesRaw (protobuf List мог не доходить).
                var codes = ClaimUseFilterCodesCodec.Split(packet.UseFilterCodesRaw);
                serverApi.Logger.Notification(
                    "[SwixyClaimChunk] SetUseFilter request claimId={0} mode={1} codesRawLen={2} codes={3}",
                    packet.ClaimId,
                    packet.UseFilterMode,
                    packet.UseFilterCodesRaw?.Length ?? 0,
                    codes.Count);
                return TrySetUseFilter(claim, packet.UseFilterMode, codes);
            }
            case ClaimAccessActionType.SetClaimFlags:
                return TrySetClaimFlags(claim, packet.ClaimFlags);
            default:
                return ClaimActionResult.Error("swixyclaimchunk:error-unknown");
        }
    }

    /// <summary>Добавляет игрока в PermittedPlayerUids с заданными флагами доступа.</summary>
    private ClaimActionResult TryAddClaimMember(IServerPlayer owner, LandClaim claim, string playerName, string playerUid, EnumBlockAccessFlags accessFlags)
    {
        var playerData = ResolvePlayerData(playerName, playerUid);
        if (playerData == null)
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-player-not-found");
        }

        if (playerData.PlayerUID == owner.PlayerUID)
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-owner-member");
        }

        claim.PermittedPlayerUids ??= [];
        claim.PermittedPlayerUids[playerData.PlayerUID] = accessFlags;
        TouchClaim(claim);
        return ClaimActionResult.Success("swixyclaimchunk:claims-message-player-added", ResolvePlayerName(playerData.PlayerUID, playerName));
    }

    /// <summary>Обновляет флаги Use/Build у существующего участника.</summary>
    private ClaimActionResult TryUpdateClaimMemberAccess(LandClaim claim, string playerName, string playerUid, EnumBlockAccessFlags accessFlags)
    {
        if (!TryResolveMemberUid(claim, playerName, playerUid, out var memberUid))
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-member-not-found");
        }

        if (memberUid == claim.OwnedByPlayerUid)
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-owner-member");
        }

        if (claim.PermittedPlayerUids == null || !claim.PermittedPlayerUids.ContainsKey(memberUid))
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-member-not-found");
        }

        claim.PermittedPlayerUids[memberUid] = accessFlags;
        TouchClaim(claim);
        return ClaimActionResult.Success();
    }

    /// <summary>Удаляет участника из PermittedPlayerUids.</summary>
    private ClaimActionResult TryRemoveClaimMember(LandClaim claim, string playerName, string playerUid)
    {
        if (!TryResolveMemberUid(claim, playerName, playerUid, out var memberUid))
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-member-not-found");
        }

        if (memberUid == claim.OwnedByPlayerUid)
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-cannot-remove-owner");
        }

        if (claim.PermittedPlayerUids == null || !claim.PermittedPlayerUids.Remove(memberUid))
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-member-not-found");
        }

        RemoveCoOwner(claim, memberUid);
        TouchClaim(claim);
        return ClaimActionResult.Success("swixyclaimchunk:claims-message-player-removed", ResolvePlayerName(memberUid, playerName));
    }

    /// <summary>Переключает статус со-владельца (корона); Use/Build не меняются.</summary>
    private ClaimActionResult TryToggleCoOwnership(LandClaim claim, string playerName, string playerUid)
    {
        if (!TryResolveMemberUid(claim, playerName, playerUid, out var memberUid))
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-member-not-found");
        }

        if (memberUid == claim.OwnedByPlayerUid)
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-owner-member");
        }

        if (IsCoOwner(claim, memberUid))
        {
            RemoveCoOwner(claim, memberUid);
            TouchClaim(claim);
            return ClaimActionResult.Success("swixyclaimchunk:claims-message-coowner-revoked", ResolvePlayerName(memberUid, playerName));
        }

        AddCoOwner(claim, memberUid);
        TouchClaim(claim);
        return ClaimActionResult.Success("swixyclaimchunk:claims-message-coowner-granted", ResolvePlayerName(memberUid, playerName));
    }

    /// <summary>Меняет Description привата (отображаемое имя).</summary>
    private ClaimActionResult TryRenameClaim(LandClaim claim, string claimName)
    {
        claimName = claimName.Trim();
        if (string.IsNullOrWhiteSpace(claimName))
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-empty-name");
        }

        var oldName = (claim.Description ?? "").Trim();
        claim.Description = claimName;
        MigrateUseFilterAfterRename(claim, oldName);
        // Name-key for flags follows claim description.
        RebindClaimFlagsKeys(claim);
        TouchClaim(claim);
        return ClaimActionResult.Success("swixyclaimchunk:claims-message-renamed");
    }

    /// <summary>Ищет IServerPlayerData по UID или нику (онлайн / last known name).</summary>
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

        var onlinePlayer = serverApi.World.AllPlayers
            .FirstOrDefault(player => string.Equals(player.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
        if (onlinePlayer != null)
        {
            return serverApi.PlayerData.GetPlayerDataByUid(onlinePlayer.PlayerUID);
        }

        var byName = serverApi.PlayerData.GetPlayerDataByLastKnownName(playerName);
        if (byName != null)
        {
            return byName;
        }

        if (!string.IsNullOrWhiteSpace(playerUid))
        {
            return serverApi.PlayerData.GetPlayerDataByUid(playerUid);
        }

        return null;
    }

    /// <summary>Определяет UID участника привата по UID из пакета или нику.</summary>
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

}
