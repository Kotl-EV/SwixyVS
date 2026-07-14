using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Content;
using SwixyClaimChunk.Net;
using ProtoBuf;
using static SwixyClaimChunk.Content.ClaimVolumeUtil;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkMod"/> — сервер: права и со-владельцы.</summary>
public sealed partial class SwixyClaimChunkMod
{
    /// <summary>Стабильный ключ привата для хранения со-владельцев между сохранениями.</summary>
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

        var minX = areas.Min(static area => area.X1);
        var minY = areas.Min(static area => area.Y1);
        var minZ = areas.Min(static area => area.Z1);
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
        if (string.IsNullOrWhiteSpace(playerUid))
        {
            return;
        }

        GetOrCreateCoOwners(claim).Add(playerUid);
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

    private void ClearCoOwners(LandClaim claim)
    {
        var key = BuildClaimStorageKey(claim);
        coOwnerUidsByClaimKey.Remove(key);
    }

    private void MergeCoOwners(LandClaim primary, LandClaim other)
    {
        var otherKey = BuildClaimStorageKey(other);
        if (!coOwnerUidsByClaimKey.TryGetValue(otherKey, out var otherSet) || otherSet.Count == 0)
        {
            return;
        }

        var primarySet = GetOrCreateCoOwners(primary);
        foreach (var uid in otherSet)
        {
            primarySet.Add(uid);
        }

        coOwnerUidsByClaimKey.Remove(otherKey);
    }

    /// <summary>Игрок — официальный владелец привата.</summary>
    private static bool IsClaimOwner(LandClaim claim, string playerUid)
    {
        return claim.OwnedByPlayerUid == playerUid;
    }

    /// <summary>Игрок — со-владелец (назначен короной, не зависит от Use/Build).</summary>
    private bool IsCoOwner(LandClaim claim, string playerUid)
    {
        if (string.IsNullOrWhiteSpace(playerUid) || IsClaimOwner(claim, playerUid))
        {
            return false;
        }

        var key = BuildClaimStorageKey(claim);
        return coOwnerUidsByClaimKey.TryGetValue(key, out var set) && set.Contains(playerUid);
    }

    /// <summary>Владелец или со-владелец может управлять приватом в GUI.</summary>
    private bool CanManageClaim(LandClaim claim, string playerUid)
    {
        return IsClaimOwner(claim, playerUid) || IsCoOwner(claim, playerUid);
    }

    /// <summary>Чанки привата отображаются как «свои» на карте.</summary>
    private bool PlayerTreatsClaimAsOwn(LandClaim claim, string playerUid)
    {
        return CanManageClaim(claim, playerUid);
    }

    /// <summary>Можно снять клейм с чанка: владелец, со-владелец или админ.</summary>
    private bool CanUnclaimFromClaim(LandClaim claim, string playerUid, bool adminBypass)
    {
        if (adminBypass)
        {
            return true;
        }

        return CanManageClaim(claim, playerUid);
    }

    /// <summary>claimland, controlserver или одиночная игра.</summary>
    private bool CanClaimLand(IServerPlayer player)
    {
        return player.HasPrivilege(Privilege.claimland)
            || player.HasPrivilege(Privilege.controlserver)
            || serverApi?.Server?.IsDedicated == false;
    }

    /// <summary>Проверяет готовность сервера и прав на клейм; null — всё в порядке.</summary>
    private ClaimActionResult? ValidateClaimEnvironment(IServerPlayer player)
    {
        if (serverApi == null)
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-server-not-ready");
        }

        if (!IsLandClaimingEnabled())
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-land-claiming-disabled");
        }

        if (!CanClaimLand(player))
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-no-privilege");
        }

        return null;
    }

    /// <summary>Снимать приват с чужих чанков на карте — только controlserver.</summary>
    private static bool CanAdminUnclaimOthers(IServerPlayer player)
    {
        return player.HasPrivilege(Privilege.controlserver);
    }

    /// <summary>Лимит объёма приватов (роль + extra).</summary>
    private static long GetLandClaimAllowance(IServerPlayer player)
    {
        var roleAllowance = player.Role?.LandClaimAllowance ?? 0;
        var extraAllowance = player.ServerData?.ExtraLandClaimAllowance ?? 0;
        return (long)roleAllowance + extraAllowance;
    }

    /// <summary>Проверяет, что добавление additionalVolume не превысит квоту игрока.</summary>
    private ClaimActionResult? ValidateLandClaimAllowance(IServerPlayer player, long additionalVolume)
    {
        if (additionalVolume <= 0)
        {
            return null;
        }

        var usedVolume = GetOwnClaims(player.PlayerUID).Sum(static claim => (long)claim.SizeXYZ);
        var allowance = GetLandClaimAllowance(player);
        if (allowance > 0 && usedVolume + additionalVolume > allowance)
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-allowance");
        }

        return null;
    }

    /// <summary>Максимум отдельных областей (роль + extra).</summary>
    private static int GetLandClaimMaxAreas(IServerPlayer player)
    {
        var roleAreas = player.Role?.LandClaimMaxAreas ?? 0;
        var extraAreas = player.ServerData?.ExtraLandClaimAreas ?? 0;
        return roleAreas + extraAreas;
    }

    /// <summary>Деление с округлением вниз для отрицательных координат чанков.</summary>
    private static int FloorDiv(int value, int divisor)
    {
        return (int)Math.Floor((double)value / divisor);
    }

}
