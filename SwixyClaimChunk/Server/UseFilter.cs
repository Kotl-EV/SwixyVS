using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkMod"/> — сервер: фильтр блоков для Use.</summary>
public sealed partial class SwixyClaimChunkMod
{
    private UseFilterRuleData? TryGetUseFilter(LandClaim claim)
    {
        foreach (var key in EnumerateClaimStorageKeys(claim))
        {
            if (useFiltersByClaimKey.TryGetValue(key, out var rule)
                && rule.Mode == ClaimUseFilterMode.Whitelist
                && rule.Codes.Count > 0)
            {
                return rule;
            }
        }

        return null;
    }

    private void ClearUseFilter(LandClaim claim)
    {
        foreach (var key in EnumerateClaimStorageKeys(claim).ToList())
        {
            useFiltersByClaimKey.Remove(key);
        }

        PersistUseFiltersNow();
    }

    private void MergeUseFilters(LandClaim primary, LandClaim other)
    {
        var otherRule = TryGetUseFilter(other);
        if (otherRule == null)
        {
            ClearUseFilterKeysOnly(other);
            return;
        }

        var primaryRule = TryGetUseFilter(primary);
        if (primaryRule == null)
        {
            WriteUseFilter(primary, ClaimUseFilterMode.Whitelist, otherRule.Codes);
        }
        else
        {
            var merged = new HashSet<string>(primaryRule.Codes, StringComparer.OrdinalIgnoreCase);
            foreach (var code in otherRule.Codes)
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    merged.Add(code.Trim());
                }
            }

            WriteUseFilter(primary, ClaimUseFilterMode.Whitelist, merged.Take(MaxUseFilterCodes).ToList());
        }

        ClearUseFilterKeysOnly(other);
        PersistUseFiltersNow();
    }

    private void ClearUseFilterKeysOnly(LandClaim claim)
    {
        foreach (var key in EnumerateClaimStorageKeys(claim).ToList())
        {
            useFiltersByClaimKey.Remove(key);
        }
    }

    private ClaimActionResult TrySetUseFilter(LandClaim claim, int mode, IEnumerable<string>? codes)
    {
        if (mode != ClaimUseFilterMode.AllowAll && mode != ClaimUseFilterMode.Whitelist)
        {
            serverApi?.Logger.Warning("[SwixyClaimChunk] SetUseFilter: bad mode={0}", mode);
            return ClaimActionResult.Error("swixyclaimchunk:error-unknown");
        }

        // Не вызываем TouchClaim: он сдвигает claim в конец All и ломает ClaimId.
        var normalized = NormalizeUseFilterCodes(codes);
        if (mode == ClaimUseFilterMode.Whitelist && normalized.Count == 0)
        {
            serverApi?.Logger.Warning("[SwixyClaimChunk] SetUseFilter: empty whitelist rejected");
            return ClaimActionResult.Error("swixyclaimchunk:use-filter-error-empty");
        }

        var keys = EnumerateClaimStorageKeys(claim).ToList();
        if (keys.Count == 0)
        {
            serverApi?.Logger.Warning("[SwixyClaimChunk] SetUseFilter: no storage keys for claim");
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-not-found");
        }

        if (mode == ClaimUseFilterMode.AllowAll)
        {
            ClearUseFilterKeysOnly(claim);
            PersistUseFiltersNow();
            serverApi?.Logger.Notification(
                "[SwixyClaimChunk] Use filter cleared keys={0}",
                string.Join(", ", keys));
            return ClaimActionResult.Success("swixyclaimchunk:use-filter-message-saved");
        }

        WriteUseFilter(claim, ClaimUseFilterMode.Whitelist, normalized);
        PersistUseFiltersNow();

        serverApi?.Logger.Notification(
            "[SwixyClaimChunk] Use filter saved keys=[{0}] codes={1} sample={2}",
            string.Join(" | ", keys),
            normalized.Count,
            normalized.Count > 0 ? normalized[0] : "");

        return ClaimActionResult.Success("swixyclaimchunk:use-filter-message-saved");
    }

    private void WriteUseFilter(LandClaim claim, int mode, List<string> codes)
    {
        var rule = new UseFilterRuleData
        {
            Mode = mode,
            Codes = codes.ToList()
        };

        foreach (var key in EnumerateClaimStorageKeys(claim))
        {
            useFiltersByClaimKey[key] = new UseFilterRuleData
            {
                Mode = rule.Mode,
                Codes = rule.Codes.ToList()
            };
        }
    }

    /// <summary>
    /// Несколько ключей на один приват: координаты (как co-owners) + имя.
    /// Lookup срабатывает, если совпал любой.
    /// </summary>
    private static IEnumerable<string> EnumerateClaimStorageKeys(LandClaim claim)
    {
        if (string.IsNullOrWhiteSpace(claim.OwnedByPlayerUid))
        {
            yield break;
        }

        var coordKey = BuildClaimStorageKey(claim);
        if (!string.IsNullOrWhiteSpace(coordKey))
        {
            yield return coordKey;
        }

        var name = (claim.Description ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            yield return $"{claim.OwnedByPlayerUid}:name:{name}";
        }
    }

    private static List<string> NormalizeUseFilterCodes(IEnumerable<string>? codes)
    {
        if (codes == null)
        {
            return [];
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in codes)
        {
            var code = NormalizeCollectibleCode(raw);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            set.Add(code);
            if (set.Count >= MaxUseFilterCodes)
            {
                break;
            }
        }

        return set.OrderBy(static code => code, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Единый вид кода: domain:path (через AssetLocation).</summary>
    internal static string NormalizeCollectibleCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        var trimmed = raw.Trim();
        try
        {
            var location = new AssetLocation(trimmed);
            if (string.IsNullOrWhiteSpace(location.Domain) || string.IsNullOrWhiteSpace(location.Path))
            {
                return trimmed;
            }

            return location.ToString();
        }
        catch
        {
            return trimmed;
        }
    }

    private void FillClaimUseFilterInfo(ClaimInfoPacket info, LandClaim claim)
    {
        var rule = TryGetUseFilter(claim);
        if (rule == null || rule.Mode != ClaimUseFilterMode.Whitelist || rule.Codes.Count == 0)
        {
            info.UseFilterMode = ClaimUseFilterMode.AllowAll;
            info.UseFilterCodesRaw = "";
            return;
        }

        info.UseFilterMode = ClaimUseFilterMode.Whitelist;
        info.UseFilterCodesRaw = ClaimUseFilterCodesCodec.Join(rule.Codes);
    }

    /// <summary>
    /// Сужает уже разрешённый ванилью Use: whitelist только для «Use без Build».
    /// Владелец / со-владелец / Use+Build / чистое строительство — без фильтра.
    /// </summary>
    private EnumWorldAccessResponse OnTestBlockAccessClaim(
        IPlayer player,
        BlockSelection blockSel,
        EnumBlockAccessFlags accessType,
        ref string claimant,
        LandClaim claim,
        EnumWorldAccessResponse response)
    {
        try
        {
            // VS иногда вызывает событие с claim == null — не падаем.
            if (claim == null || player == null)
            {
                return response;
            }

            if (response != EnumWorldAccessResponse.Granted)
            {
                return response;
            }

            // Фильтр только на взаимодействие Use, не на Build/Break.
            if (!accessType.HasFlag(EnumBlockAccessFlags.Use))
            {
                return response;
            }

            if (accessType.HasFlag(EnumBlockAccessFlags.BuildOrBreak))
            {
                return response;
            }

            var playerUid = player.PlayerUID;
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return response;
            }

            // Владелец / со-владелец — полный доступ.
            if (IsClaimOwner(claim, playerUid) || IsCoOwner(claim, playerUid))
            {
                return response;
            }

            // Use + Build (или просто Build) — фильтр блоков НЕ применяется.
            if (HasBuildAccess(claim, playerUid) || !HasUseOnlyAccess(claim, playerUid))
            {
                return response;
            }

            var rule = TryGetUseFilter(claim);
            if (rule == null || rule.Mode != ClaimUseFilterMode.Whitelist || rule.Codes.Count == 0)
            {
                return response;
            }

            var blockCode = NormalizeCollectibleCode(blockSel?.Block?.Code?.ToString());
            if (string.IsNullOrWhiteSpace(blockCode) || !IsBlockCodeAllowedByUseFilter(blockCode, rule.Codes))
            {
                claimant = Lang.Get("swixyclaimchunk:use-filter-denied");
                return EnumWorldAccessResponse.DeniedByMod;
            }

            return response;
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error(
                "[SwixyClaimChunk] OnTestBlockAccessClaim failed: {0}",
                exception);
            return response;
        }
    }

    /// <summary>
    /// Точное совпадение или вариант кода (door-oak ↔ door-oak-north).
    /// </summary>
    internal static bool IsBlockCodeAllowedByUseFilter(string blockCode, IReadOnlyList<string> allowedCodes)
    {
        blockCode = NormalizeCollectibleCode(blockCode);
        foreach (var allowedRaw in allowedCodes)
        {
            var allowed = NormalizeCollectibleCode(allowedRaw);
            if (string.IsNullOrWhiteSpace(allowed))
            {
                continue;
            }

            if (string.Equals(blockCode, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (blockCode.StartsWith(allowed + "-", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (allowed.StartsWith(blockCode + "-", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void OnUseFiltersSaveGameLoaded()
    {
        useFiltersByClaimKey.Clear();

        // 1) byte[] + SerializerUtil (как co-owners)
        var data = serverApi?.WorldManager.SaveGame.GetData(UseFiltersSaveKey);
        if (data != null && data.Length > 0)
        {
            try
            {
                var saved = SerializerUtil.Deserialize<UseFilterSaveData>(data);
                ImportUseFilterSaveData(saved);
            }
            catch (Exception exception)
            {
                serverApi?.Logger.Error("[SwixyClaimChunk] Failed to deserialize use filters (bytes): {0}", exception);
            }
        }

        // 2) generic StoreData fallback
        if (useFiltersByClaimKey.Count == 0 && serverApi != null)
        {
            try
            {
                var generic = serverApi.WorldManager.SaveGame.GetData<UseFilterSaveData>(UseFiltersSaveKey + "_obj", null!);
                if (generic?.Entries != null)
                {
                    ImportUseFilterSaveData(generic);
                }
            }
            catch (Exception exception)
            {
                serverApi.Logger.Warning("[SwixyClaimChunk] Use filter generic load skipped: {0}", exception.Message);
            }
        }

        serverApi?.Logger.Notification(
            "[SwixyClaimChunk] Loaded use filters: {0} entries",
            useFiltersByClaimKey.Count);
    }

    private void ImportUseFilterSaveData(UseFilterSaveData? saved)
    {
        if (saved?.Entries == null)
        {
            return;
        }

        foreach (var entry in saved.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null || entry.Value.Count == 0)
            {
                continue;
            }

            // Формат: [mode, code1, code2, ...]
            var modeToken = entry.Value[0]?.Trim() ?? "0";
            var mode = modeToken is "1" or "whitelist"
                ? ClaimUseFilterMode.Whitelist
                : ClaimUseFilterMode.AllowAll;
            var codes = NormalizeUseFilterCodes(entry.Value.Skip(1));
            if (mode != ClaimUseFilterMode.Whitelist || codes.Count == 0)
            {
                continue;
            }

            useFiltersByClaimKey[entry.Key] = new UseFilterRuleData
            {
                Mode = ClaimUseFilterMode.Whitelist,
                Codes = codes
            };
        }
    }

    private void OnUseFiltersSaveGameSaving()
    {
        PersistUseFiltersNow();
    }

    /// <summary>Пишет фильтры в SaveGame сразу (не только на автосейве мира).</summary>
    private void PersistUseFiltersNow()
    {
        if (serverApi == null)
        {
            return;
        }

        var payload = new UseFilterSaveData();
        foreach (var entry in useFiltersByClaimKey)
        {
            if (entry.Value.Mode != ClaimUseFilterMode.Whitelist || entry.Value.Codes.Count == 0)
            {
                continue;
            }

            var list = new List<string>(entry.Value.Codes.Count + 1) { "1" };
            list.AddRange(entry.Value.Codes);
            payload.Entries[entry.Key] = list;
        }

        try
        {
            var bytes = SerializerUtil.Serialize(payload);
            serverApi.WorldManager.SaveGame.StoreData(UseFiltersSaveKey, bytes);
            // Дублируем generic-путём — на случай если byte[] не попадёт в сейв.
            serverApi.WorldManager.SaveGame.StoreData(UseFiltersSaveKey + "_obj", payload);

            serverApi.Logger.Notification(
                "[SwixyClaimChunk] Use filters persisted entries={0} bytes={1}",
                payload.Entries.Count,
                bytes?.Length ?? 0);
        }
        catch (Exception exception)
        {
            serverApi.Logger.Error("[SwixyClaimChunk] Failed to persist use filters: {0}", exception);
        }
    }
}
