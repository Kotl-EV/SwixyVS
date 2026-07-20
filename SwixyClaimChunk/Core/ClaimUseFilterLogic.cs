using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Core;

/// <summary>
/// Общая логика whitelist Use (lookup правил + client/server TestBlockAccess).
/// Не зависит от ModSystem — принимает store и world снаружи.
/// </summary>
public static class ClaimUseFilterLogic
{
    public static UseFilterRuleData? TryGetUseFilter(
        Dictionary<string, UseFilterRuleData> store,
        LandClaim claim)
    {
        if (claim == null)
        {
            return null;
        }

        foreach (var key in ClaimStorageKeys.EnumerateClaimStorageKeys(claim))
        {
            if (TryGetWhitelistRule(store, key, out var rule))
            {
                return rule;
            }
        }

        var owner = claim.OwnedByPlayerUid ?? "";
        var name = (claim.Description ?? "").Trim();
        if (string.IsNullOrWhiteSpace(owner))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var nameKey = $"{owner}:name:{name}";
            if (TryGetWhitelistRule(store, nameKey, out var byName))
            {
                return byName;
            }
        }

        UseFilterRuleData? sole = null;
        var soleCount = 0;
        foreach (var entry in store)
        {
            if (!entry.Key.StartsWith(owner + ":", StringComparison.Ordinal)
                || entry.Value.Mode != ClaimUseFilterMode.Whitelist
                || entry.Value.Codes.Count == 0)
            {
                continue;
            }

            sole = entry.Value;
            soleCount++;
            if (soleCount > 1)
            {
                return null;
            }
        }

        return soleCount == 1 ? sole : null;
    }

    public static bool TryGetWhitelistRule(
        Dictionary<string, UseFilterRuleData> store,
        string key,
        out UseFilterRuleData? rule)
    {
        rule = null;
        if (!store.TryGetValue(key, out var found)
            || found.Mode != ClaimUseFilterMode.Whitelist
            || found.Codes.Count == 0)
        {
            return false;
        }

        rule = found;
        return true;
    }

    public static List<string> NormalizeUseFilterCodes(IEnumerable<string>? codes)
    {
        if (codes == null)
        {
            return [];
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in codes)
        {
            var code = ClaimCodeUtil.NormalizeCollectibleCode(raw);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            set.Add(code);
        }

        return set.OrderBy(static code => code, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool IsClaimOwner(LandClaim? claim, string? playerUid)
    {
        return claim != null
            && !string.IsNullOrWhiteSpace(playerUid)
            && claim.OwnedByPlayerUid == playerUid;
    }

    public static bool HasBuildAccess(LandClaim? claim, string? playerUid)
    {
        if (claim?.PermittedPlayerUids == null || string.IsNullOrWhiteSpace(playerUid))
        {
            return false;
        }

        return claim.PermittedPlayerUids.TryGetValue(playerUid, out var flags)
            && flags.HasFlag(EnumBlockAccessFlags.BuildOrBreak);
    }

    /// <summary>Есть ли у игрока право Use в этом привате (участник с Use).</summary>
    public static bool HasUseAccess(LandClaim? claim, string? playerUid)
    {
        if (claim?.PermittedPlayerUids == null || string.IsNullOrWhiteSpace(playerUid))
        {
            return false;
        }

        return claim.PermittedPlayerUids.TryGetValue(playerUid, out var flags)
            && flags.HasFlag(EnumBlockAccessFlags.Use);
    }

    /// <summary>
    /// Whitelist Use — публичный доступ к дверям/инвентарям из списка.
    /// Только <see cref="EnumBlockAccessFlags.Use"/> (открыть дверь, сундук, полку…).
    /// Build/Break (поставить фонарь, кучу на земле) — ванильные права привата.
    /// <paramref name="isPrivileged"/> — owner/co-owner.
    /// </summary>
    public static EnumWorldAccessResponse ApplyUseBlockFilter(
        IWorldAccessor? world,
        Dictionary<string, UseFilterRuleData> store,
        IPlayer player,
        BlockSelection? blockSel,
        EnumBlockAccessFlags accessType,
        ref string claimant,
        LandClaim? claim,
        EnumWorldAccessResponse response,
        System.Func<LandClaim, string, bool>? isPrivileged = null,
        System.Action<string>? logError = null)
    {
        try
        {
            if (player == null || world == null)
            {
                return response;
            }

            // Только Use: двери, сундуки, полки. Не Build (стройка).
            if (accessType != EnumBlockAccessFlags.Use)
            {
                return response;
            }

            var playerUid = player.PlayerUID;
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return response;
            }

            var pos = blockSel?.Position;
            if (pos == null)
            {
                return response;
            }

            if (store.Count == 0)
            {
                return response;
            }

            var resolved = ResolveBlockForUseFilter(world, blockSel, pos);
            var lookupPos = resolved.ControlPos ?? pos;
            var claims = ResolveClaimsAt(world, claim, lookupPos, pos);
            if (claims.Length == 0)
            {
                return response;
            }

            var codesToTest = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(resolved.BlockCode))
            {
                codesToTest.Add(resolved.BlockCode);
            }

            if (!string.IsNullOrWhiteSpace(resolved.AltBlockCode)
                && !codesToTest.Contains(resolved.AltBlockCode, StringComparer.OrdinalIgnoreCase))
            {
                codesToTest.Add(resolved.AltBlockCode);
            }

            // Only upgrades Denied → Granted for public whitelist blocks.
            var result = response;

            foreach (var activeClaim in claims)
            {
                if (activeClaim == null)
                {
                    continue;
                }

                if (isPrivileged != null && isPrivileged(activeClaim, playerUid))
                {
                    continue;
                }

                if (IsClaimOwner(activeClaim, playerUid))
                {
                    continue;
                }

                if (HasBuildAccess(activeClaim, playerUid) || HasUseAccess(activeClaim, playerUid))
                {
                    continue;
                }

                var rule = TryGetUseFilter(store, activeClaim);
                if (rule == null || rule.Mode != ClaimUseFilterMode.Whitelist || rule.Codes.Count == 0)
                {
                    continue;
                }

                foreach (var code in codesToTest)
                {
                    if (!IsPublicUseFurnitureCode(world, code, lookupPos))
                    {
                        continue;
                    }

                    if (ClaimCodeUtil.IsBlockCodeAllowedByUseFilter(code, rule.Codes))
                    {
                        result = EnumWorldAccessResponse.Granted;
                        break;
                    }
                }

                if (result == EnumWorldAccessResponse.Granted)
                {
                    break;
                }
            }

            return result;
        }
        catch (Exception exception)
        {
            logError?.Invoke($"[SwixyClaimChunk] ApplyUseBlockFilter failed: {exception}");
            return response;
        }
    }

    /// <summary>Дверь/калитка, инвентарь или держатель факела.</summary>
    private static bool IsPublicUseFurnitureCode(IWorldAccessor world, string code, BlockPos pos)
    {
        if (ClaimCodeUtil.IsDoorOrGateCode(code)
            || ClaimCodeUtil.IsInventoryPathCode(code)
            || ClaimCodeUtil.IsTorchHolderCode(code))
        {
            return true;
        }

        try
        {
            var blk = world.BlockAccessor.GetBlock(pos);
            if (blk == null || blk.Id == 0)
            {
                return false;
            }

            return ClaimCodeUtil.IsDoorOrGateBlock(blk)
                   || ClaimCodeUtil.IsTorchHolderBlock(blk)
                   || ClaimCodeUtil.IsInventoryContainerBlock(world, blk, pos);
        }
        catch
        {
            return false;
        }
    }

    public static LandClaim[] ResolveClaimsAt(
        IWorldAccessor world,
        LandClaim? claim,
        BlockPos lookupPos,
        BlockPos originalPos)
    {
        if (claim != null)
        {
            return [claim];
        }

        var found = world.Claims.Get(lookupPos);
        if ((found == null || found.Length == 0) && lookupPos != originalPos)
        {
            found = world.Claims.Get(originalPos);
        }

        if (found != null && found.Length > 0)
        {
            return found;
        }

        var all = world.Claims.All;
        if (all == null || all.Count == 0)
        {
            return [];
        }

        var list = new List<LandClaim>();
        foreach (var c in all)
        {
            try
            {
                if (c != null && (c.PositionInside(lookupPos) || c.PositionInside(originalPos)))
                {
                    list.Add(c);
                }
            }
            catch
            {
                // ignore broken claim
            }
        }

        return list.ToArray();
    }

    public readonly struct ResolvedUseFilterBlock
    {
        public readonly string BlockCode;
        public readonly string AltBlockCode;
        public readonly BlockPos? ControlPos;

        public ResolvedUseFilterBlock(string blockCode, string altBlockCode, BlockPos? controlPos)
        {
            BlockCode = blockCode;
            AltBlockCode = altBlockCode;
            ControlPos = controlPos;
        }
    }

    /// <summary>
    /// Код «логического» блока для фильтра + control-pos multiblock (двери 1x2).
    /// </summary>
    public static ResolvedUseFilterBlock ResolveBlockForUseFilter(
        IWorldAccessor world,
        BlockSelection? blockSel,
        BlockPos pos)
    {
        try
        {
            var block = world.BlockAccessor.GetBlock(pos);
            BlockPos? controlPos = null;
            string alt = "";

            IMultiblockOffset? multiblock = block as IMultiblockOffset
                ?? block?.GetInterface<IMultiblockOffset>(world, pos);
            if (multiblock != null)
            {
                controlPos = multiblock.GetControlBlockPos(pos);
                if (controlPos != null)
                {
                    var controlBlock = world.BlockAccessor.GetBlock(controlPos);
                    var controlCode = ClaimCodeUtil.NormalizeCollectibleCode(controlBlock?.Code?.ToString());
                    if (!string.IsNullOrWhiteSpace(controlCode) && !ClaimCodeUtil.IsMultiblockStubCode(controlCode))
                    {
                        var be = world.BlockAccessor.GetBlockEntity(controlPos);
                        var beCode = ClaimCodeUtil.NormalizeCollectibleCode(be?.Block?.Code?.ToString());
                        if (!string.IsNullOrWhiteSpace(beCode) && !ClaimCodeUtil.IsMultiblockStubCode(beCode))
                        {
                            return new ResolvedUseFilterBlock(beCode, controlCode, controlPos);
                        }

                        return new ResolvedUseFilterBlock(controlCode, "", controlPos);
                    }
                }
            }

            var code = ClaimCodeUtil.NormalizeCollectibleCode(block?.Code?.ToString());

            if (ClaimCodeUtil.IsMultiblockStubCode(code) || string.IsNullOrWhiteSpace(code))
            {
                var belowPos = pos.DownCopy();
                var below = world.BlockAccessor.GetBlock(belowPos);
                var belowCode = ClaimCodeUtil.NormalizeCollectibleCode(below?.Code?.ToString());
                if (!string.IsNullOrWhiteSpace(belowCode) && !ClaimCodeUtil.IsMultiblockStubCode(belowCode))
                {
                    controlPos ??= belowPos;
                    alt = code;
                    code = belowCode;
                }
            }

            if (string.IsNullOrWhiteSpace(code) || ClaimCodeUtil.IsMultiblockStubCode(code))
            {
                var bePos = controlPos ?? pos;
                var be = world.BlockAccessor.GetBlockEntity(bePos);
                var beCode = ClaimCodeUtil.NormalizeCollectibleCode(be?.Block?.Code?.ToString());
                if (!string.IsNullOrWhiteSpace(beCode) && !ClaimCodeUtil.IsMultiblockStubCode(beCode))
                {
                    code = beCode;
                    controlPos ??= bePos;
                }
            }

            if (string.IsNullOrWhiteSpace(code) || ClaimCodeUtil.IsMultiblockStubCode(code))
            {
                var fromSelection = ClaimCodeUtil.NormalizeCollectibleCode(blockSel?.Block?.Code?.ToString());
                if (!ClaimCodeUtil.IsMultiblockStubCode(fromSelection) && !string.IsNullOrWhiteSpace(fromSelection))
                {
                    code = fromSelection;
                }
            }

            if (string.IsNullOrWhiteSpace(code) || ClaimCodeUtil.IsMultiblockStubCode(code))
            {
                var solid = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.MostSolid);
                var solidCode = ClaimCodeUtil.NormalizeCollectibleCode(solid?.Code?.ToString());
                if (!string.IsNullOrWhiteSpace(solidCode) && !ClaimCodeUtil.IsMultiblockStubCode(solidCode))
                {
                    code = solidCode;
                }
            }

            return new ResolvedUseFilterBlock(code, alt, controlPos);
        }
        catch
        {
            return new ResolvedUseFilterBlock(
                ClaimCodeUtil.NormalizeCollectibleCode(blockSel?.Block?.Code?.ToString()),
                "",
                null);
        }
    }
}
