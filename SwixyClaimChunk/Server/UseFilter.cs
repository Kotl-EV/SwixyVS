using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Core;
using SwixyClaimChunk.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixyClaimChunk;

/// <summary>����� <see cref="SwixyClaimChunkServerMod"/> � ������: ������ ������ ��� Use.</summary>
public sealed partial class SwixyClaimChunkServerMod
{
    private UseFilterRuleData? TryGetUseFilter(LandClaim claim)
        => ClaimUseFilterLogic.TryGetUseFilter(useFiltersByClaimKey, claim);

    private bool TryGetWhitelistRule(string key, out UseFilterRuleData? rule)
        => ClaimUseFilterLogic.TryGetWhitelistRule(useFiltersByClaimKey, key, out rule);

    private static List<string> NormalizeUseFilterCodes(IEnumerable<string>? codes)
        => ClaimUseFilterLogic.NormalizeUseFilterCodes(codes);

    /// <summary>
    /// �������������� ����� ������� ����������� (����� expand),
    /// ����� coord-���� �� �������������.
    /// </summary>
    private void RebindUseFilterKeys(LandClaim claim)
    {
        var rule = TryGetUseFilter(claim);
        if (rule == null)
        {
            return;
        }

        var codes = rule.Codes.ToList();
        var owner = claim.OwnedByPlayerUid ?? "";
        var name = (claim.Description ?? "").Trim();
        var freshKeys = new HashSet<string>(ClaimStorageKeys.EnumerateClaimStorageKeys(claim), StringComparer.Ordinal);

        // ������� orphan coord-����� ����� ��������� (������ minXYZ ����� expand).
        if (!string.IsNullOrWhiteSpace(owner))
        {
            foreach (var key in useFiltersByClaimKey.Keys.ToList())
            {
                if (!key.StartsWith(owner + ":", StringComparison.Ordinal))
                {
                    continue;
                }

                // name-���� ������ ������� ���� �� ��������� �� �������.
                if (key.StartsWith(owner + ":name:", StringComparison.Ordinal))
                {
                    var keyName = key[(owner.Length + ":name:".Length)..];
                    if (!string.Equals(keyName, name, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                if (!freshKeys.Contains(key))
                {
                    useFiltersByClaimKey.Remove(key);
                }
            }
        }

        WriteUseFilter(claim, ClaimUseFilterMode.Whitelist, codes);
    }

    /// <summary>������� whitelist ��� �������������� ������� (name-���� ��������).</summary>
    private void MigrateUseFilterAfterRename(LandClaim claim, string oldName)
    {
        var owner = claim.OwnedByPlayerUid ?? "";
        if (string.IsNullOrWhiteSpace(owner))
        {
            return;
        }

        UseFilterRuleData? rule = null;
        var oldNameTrimmed = (oldName ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(oldNameTrimmed))
        {
            TryGetWhitelistRule($"{owner}:name:{oldNameTrimmed}", out rule);
        }

        rule ??= TryGetUseFilter(claim);
        if (rule == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(oldNameTrimmed))
        {
            useFiltersByClaimKey.Remove($"{owner}:name:{oldNameTrimmed}");
        }

        WriteUseFilter(claim, ClaimUseFilterMode.Whitelist, rule.Codes.ToList());
        PersistUseFiltersNow();
        BroadcastUseFiltersSync();
    }

    private void ClearUseFilter(LandClaim claim)
    {
        foreach (var key in ClaimStorageKeys.EnumerateClaimStorageKeys(claim).ToList())
        {
            useFiltersByClaimKey.Remove(key);
        }

        PersistUseFiltersNow();
        BroadcastUseFiltersSync();
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

            WriteUseFilter(primary, ClaimUseFilterMode.Whitelist, merged.ToList());
        }

        ClearUseFilterKeysOnly(other);
        PersistUseFiltersNow();
        BroadcastUseFiltersSync();
    }

    private void ClearUseFilterKeysOnly(LandClaim claim)
    {
        foreach (var key in ClaimStorageKeys.EnumerateClaimStorageKeys(claim).ToList())
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

        // �� �������� TouchClaim: �� �������� claim � ����� All � ������ ClaimId.
        var normalized = NormalizeUseFilterCodes(codes);
        if (mode == ClaimUseFilterMode.Whitelist && normalized.Count == 0)
        {
            serverApi?.Logger.Warning("[SwixyClaimChunk] SetUseFilter: empty whitelist rejected");
            return ClaimActionResult.Error("swixyclaimchunk:use-filter-error-empty");
        }

        var keys = ClaimStorageKeys.EnumerateClaimStorageKeys(claim).ToList();
        if (keys.Count == 0)
        {
            serverApi?.Logger.Warning("[SwixyClaimChunk] SetUseFilter: no storage keys for claim");
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-not-found");
        }

        if (mode == ClaimUseFilterMode.AllowAll)
        {
            ClearUseFilterKeysOnly(claim);
            PersistUseFiltersNow();
            BroadcastUseFiltersSync();
            serverApi?.Logger.Notification(
                "[SwixyClaimChunk] Use filter cleared keys={0}",
                string.Join(", ", keys));
            return ClaimActionResult.Success("swixyclaimchunk:use-filter-message-saved");
        }

        WriteUseFilter(claim, ClaimUseFilterMode.Whitelist, normalized);
        PersistUseFiltersNow();
        BroadcastUseFiltersSync();

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

        foreach (var key in ClaimStorageKeys.EnumerateClaimStorageKeys(claim))
        {
            useFiltersByClaimKey[key] = new UseFilterRuleData
            {
                Mode = rule.Mode,
                Codes = rule.Codes.ToList()
            };
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

    private EnumWorldAccessResponse OnServerTestBlockAccess(
        IPlayer player,
        BlockSelection blockSel,
        EnumBlockAccessFlags accessType,
        ref string claimant,
        EnumWorldAccessResponse response)
        => ApplyUseBlockFilter(player, blockSel, accessType, ref claimant, null, response);

    private EnumWorldAccessResponse OnServerTestBlockAccessClaim(
        IPlayer player,
        BlockSelection blockSel,
        EnumBlockAccessFlags accessType,
        ref string claimant,
        LandClaim claim,
        EnumWorldAccessResponse response)
        => ApplyUseBlockFilter(player, blockSel, accessType, ref claimant, claim, response);

    /// <summary>
    /// Whitelist Use: участники с Use — все блоки; выбранные — публично для любого.
    /// </summary>
    private EnumWorldAccessResponse ApplyUseBlockFilter(
        IPlayer player,
        BlockSelection? blockSel,
        EnumBlockAccessFlags accessType,
        ref string claimant,
        LandClaim? claim,
        EnumWorldAccessResponse response)
    {
        return ClaimUseFilterLogic.ApplyUseBlockFilter(
            serverApi?.World,
            useFiltersByClaimKey,
            player,
            blockSel,
            accessType,
            ref claimant,
            claim,
            response,
            isPrivileged: (activeClaim, playerUid) =>
                IsClaimOwner(activeClaim, playerUid) || IsCoOwner(activeClaim, playerUid),
            logError: msg => serverApi?.Logger.Error(msg));
    }
    private void BroadcastUseFiltersSync(IServerPlayer? onlyPlayer = null)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        var packet = BuildUseFiltersSyncPacket();
        if (onlyPlayer != null)
        {
            serverChannel.SendPacket(packet, onlyPlayer);
            return;
        }

        foreach (var player in serverApi.World.AllOnlinePlayers.OfType<IServerPlayer>())
        {
            serverChannel.SendPacket(packet, player);
        }
    }

    private ClaimUseFiltersSyncPacket BuildUseFiltersSyncPacket()
    {
        var packet = new ClaimUseFiltersSyncPacket();
        foreach (var entry in useFiltersByClaimKey)
        {
            if (entry.Value.Mode != ClaimUseFilterMode.Whitelist || entry.Value.Codes.Count == 0)
            {
                continue;
            }

            packet.Entries.Add(new ClaimUseFilterSyncEntry
            {
                ClaimKey = entry.Key,
                Mode = ClaimUseFilterMode.Whitelist,
                CodesRaw = ClaimUseFilterCodesCodec.Join(entry.Value.Codes)
            });
        }

        return packet;
    }

    private void OnPlayerJoinSendUseFilters(IServerPlayer byPlayer)
    {
        // Небольшая задержка: канал клиента уже готов после NowPlaying.
        serverApi?.Event.RegisterCallback(_ => BroadcastUseFiltersSync(byPlayer), 250);
        serverApi?.Event.RegisterCallback(_ => BroadcastUseFiltersSync(byPlayer), 2000);
    }

    private void OnUseFiltersRequestPacket(IServerPlayer fromPlayer, ClaimUseFiltersRequestPacket packet)
    {
        BroadcastUseFiltersSync(fromPlayer);
        serverApi?.Logger.Notification(
            "[SwixyClaimChunk] Use filters requested by {0}, sent {1} keys",
            fromPlayer.PlayerName,
            useFiltersByClaimKey.Count);
    }

    private void OnUseFiltersSaveGameLoaded()
    {
        useFiltersByClaimKey.Clear();

        // 1) byte[] + SerializerUtil (как co-owners)
        var data = serverApi?.WorldManager.SaveGame.GetData(ClaimConstants.UseFiltersSaveKey);
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
                var generic = serverApi.WorldManager.SaveGame.GetData<UseFilterSaveData>(ClaimConstants.UseFiltersSaveKey + "_obj", null!);
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
            serverApi.WorldManager.SaveGame.StoreData(ClaimConstants.UseFiltersSaveKey, bytes);
            // Дублируем generic-путём — на случай если byte[] не попадёт в сейв.
            serverApi.WorldManager.SaveGame.StoreData(ClaimConstants.UseFiltersSaveKey + "_obj", payload);

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

    /// <summary>
    /// Запускает фоновый скан blocks привата (не блокирует тик).
    /// Уникальность по Block.Id — быстро; creative-маппинг один раз в конце.
    /// </summary>
    private void OnUseFilterScanRequestPacket(IServerPlayer fromPlayer, ClaimUseFilterScanRequestPacket packet)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        if (!TryGetClaimById(packet.ClaimId, out var claim) || !CanManageClaim(claim, fromPlayer.PlayerUID))
        {
            serverChannel.SendPacket(new ClaimUseFilterScanResultPacket
            {
                ClaimId = packet.ClaimId,
                Message = Lang.GetL(fromPlayer.LanguageCode, "swixyclaimchunk:claims-error-not-found")
            }, fromPlayer);
            return;
        }

        var areas = claim.Areas;
        if (areas == null || areas.Count == 0)
        {
            serverChannel.SendPacket(new ClaimUseFilterScanResultPacket
            {
                ClaimId = packet.ClaimId,
                Message = Lang.GetL(fromPlayer.LanguageCode, "swixyclaimchunk:use-filter-scan-empty")
            }, fromPlayer);
            return;
        }

        var jobKey = fromPlayer.PlayerUID + ":" + packet.ClaimId;
        // Не запускаем второй скан, пока первый не закончен (анти-spam kick).
        if (activeUseFilterScans.ContainsKey(jobKey))
        {
            return;
        }

        activeUseFilterScans[jobKey] = new UseFilterScanJob
        {
            Player = fromPlayer,
            ClaimId = packet.ClaimId,
            Claim = claim,
            Areas = areas.ToList(),
            CreativeByPrefix = BuildCreativeDisplayCodeCache()
        };

        // Первый шаг сразу, дальше — по тикам.
        serverApi.Event.EnqueueMainThreadTask(() => ProcessUseFilterScanStep(jobKey), "swixy-usefilter-scan");
    }

    /// <summary>
    /// Один «квант» скана: несколько XZ-колонок. Не подвешивает сервер.
    /// </summary>
    private void ProcessUseFilterScanStep(string jobKey)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        if (!activeUseFilterScans.TryGetValue(jobKey, out var job))
        {
            return;
        }

        try
        {
            // Больше колонок за тик — полный скан большого привата быстрее (список без обрезки).
            const int columnsPerStep = 128;
            var accessor = serverApi.World.BlockAccessor;
            var columnsDone = 0;
            var done = false;

            while (columnsDone < columnsPerStep && !done)
            {
                if (job.AreaIndex >= job.Areas.Count)
                {
                    done = true;
                    break;
                }

                var area = job.Areas[job.AreaIndex];
                var x1 = Math.Min(area.X1, area.X2);
                var x2 = Math.Max(area.X1, area.X2);
                var y1 = Math.Min(area.Y1, area.Y2);
                var y2 = Math.Max(area.Y1, area.Y2);
                var z1 = Math.Min(area.Z1, area.Z2);
                var z2 = Math.Max(area.Z1, area.Z2);

                if (!job.StartedArea)
                {
                    job.NextX = x1;
                    job.NextZ = z1;
                    job.StartedArea = true;
                }

                if (job.NextX >= x2)
                {
                    job.AreaIndex++;
                    job.StartedArea = false;
                    continue;
                }

                // Одна XZ-колонка: Y сверху вниз, step=1 (ничего не пропускаем).
                var x = job.NextX;
                var z = job.NextZ;
                for (var y = y2 - 1; y >= y1; y--)
                {
                    job.Scanned++;
                    var pos = new BlockPos(x, y, z);
                    var block = accessor.GetBlock(pos);
                    if (block == null || block.Id == 0)
                    {
                        continue;
                    }

                    // Multiblock-stub → control (двери / EP).
                    IMultiblockOffset? mb = block as IMultiblockOffset
                        ?? block.GetInterface<IMultiblockOffset>(serverApi.World, pos);
                    if (mb != null)
                    {
                        var controlPos = mb.GetControlBlockPos(pos);
                        if (controlPos != null)
                        {
                            block = accessor.GetBlock(controlPos);
                            if (block == null || block.Id == 0)
                            {
                                continue;
                            }
                        }
                    }

                    // Уже видели этот тип — O(1), без строк/creative.
                    if (!job.SeenBlockIds.Add(block.Id))
                    {
                        continue;
                    }

                    var code = ClaimCodeUtil.NormalizeCollectibleCode(block.Code?.ToString());
                    if (string.IsNullOrWhiteSpace(code) || ClaimCodeUtil.IsMultiblockStubCode(code))
                    {
                        continue;
                    }

                    var groupKey = ClaimCodeUtil.StripVariantSuffixes(code);
                    if (string.IsNullOrWhiteSpace(groupKey))
                    {
                        groupKey = code;
                    }

                    // Только блоки, с которыми можно взаимодействовать (Use),
                    // без земли/камня/руды и «просто декора».
                    if (!IsUseInteractableForScan(serverApi.World, block, code, groupKey, pos))
                    {
                        continue;
                    }

                    var displayCode = PreferCreativeInventoryCodeCached(job.CreativeByPrefix, block, code);
                    if (!job.InterestingPreferred.TryGetValue(groupKey, out var existing)
                        || IsBetterDisplayBlockCode(serverApi.World, displayCode, existing, block))
                    {
                        job.InterestingPreferred[groupKey] = displayCode;
                    }
                }

                columnsDone++;
                job.NextZ++;
                if (job.NextZ >= z2)
                {
                    job.NextZ = z1;
                    job.NextX++;
                }
            }

            if (done || job.AreaIndex >= job.Areas.Count)
            {
                FinishUseFilterScan(jobKey, job);
                return;
            }

            // Следующий квант на следующем тике.
            serverApi.Event.RegisterCallback(_ => ProcessUseFilterScanStep(jobKey), 1);
        }
        catch (Exception exception)
        {
            activeUseFilterScans.Remove(jobKey);
            serverApi.Logger.Error("[SwixyClaimChunk] Use filter scan failed: {0}", exception);
            try
            {
                serverChannel.SendPacket(new ClaimUseFilterScanResultPacket
                {
                    ClaimId = job.ClaimId,
                    Message = Lang.GetL(job.Player.LanguageCode, "swixyclaimchunk:error-unknown")
                }, job.Player);
            }
            catch
            {
                // ignore
            }
        }
    }

    private void FinishUseFilterScan(string jobKey, UseFilterScanJob job)
    {
        activeUseFilterScans.Remove(jobKey);
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        // Только «игровые» блоки — без террейна/руд.
        var codes = job.InterestingPreferred.Values
            .OrderBy(static c => c, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        serverChannel.SendPacket(new ClaimUseFilterScanResultPacket
        {
            ClaimId = job.ClaimId,
            CodesRaw = ClaimUseFilterCodesCodec.Join(codes),
            CodeCount = codes.Count,
            ScannedBlocks = job.Scanned,
            Message = codes.Count == 0
                ? Lang.GetL(job.Player.LanguageCode, "swixyclaimchunk:use-filter-scan-empty")
                : Lang.GetL(job.Player.LanguageCode, "swixyclaimchunk:use-filter-scan-ok", codes.Count)
        }, job.Player);

        serverApi.Logger.Notification(
            "[SwixyClaimChunk] Use filter scan done claimId={0} by {1}: unique={2} scanned={3}",
            job.ClaimId,
            job.Player.PlayerName,
            codes.Count,
            job.Scanned);
    }

    /// <summary>
    /// Один проход по world.Blocks: groupKey (без ориентации) → лучший creative-код.
    /// Важно: windmillrotor-wood и windmillrotor-metal — разные ключи,
    /// иначе обычный ротор подменяется металлическим.
    /// Lantern: material в attributes creative-стека, не в code — берём *-up со stacks.
    /// </summary>
    private Dictionary<string, string> BuildCreativeDisplayCodeCache()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (serverApi == null)
        {
            return map;
        }

        foreach (var b in serverApi.World.Blocks)
        {
            if (b?.Code == null || !HasCreativeDisplay(b))
            {
                continue;
            }

            var full = ClaimCodeUtil.NormalizeCollectibleCode(b.Code.ToString());
            if (string.IsNullOrWhiteSpace(full) || ClaimCodeUtil.IsMultiblockStubCode(full))
            {
                continue;
            }

            var groupKey = ClaimCodeUtil.StripVariantSuffixes(full);
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                groupKey = full;
            }

            if (!map.TryGetValue(groupKey, out var existing)
                || ScoreDisplayBlockCode(full, b) > ScoreDisplayBlockCode(existing, null))
            {
                map[groupKey] = full;
            }
        }

        return map;
    }

    /// <summary>
    /// Creative tabs ИЛИ CreativeInventoryStacks (фонари: material/lining/glass в stacks).
    /// </summary>
    private static bool HasCreativeDisplay(Block? block)
    {
        if (block == null)
        {
            return false;
        }

        if (block.CreativeInventoryTabs is { Length: > 0 })
        {
            return true;
        }

        return block.CreativeInventoryStacks is { Length: > 0 };
    }

    private static string PreferCreativeInventoryCodeCached(
        Dictionary<string, string>? cache,
        Block worldBlock,
        string worldCode)
    {
        // Стены фонаря (north) без stacks — не оставляем worldCode, ищем *-up.
        if (HasCreativeDisplay(worldBlock)
            && worldBlock.CreativeInventoryStacks is { Length: > 0 })
        {
            return worldCode;
        }

        if (cache == null || cache.Count == 0)
        {
            return worldCode;
        }

        // Только creative-вариант той же «семьи» (материал/тип), не всего firstPart.
        var groupKey = ClaimCodeUtil.StripVariantSuffixes(worldCode);
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            groupKey = worldCode;
        }

        if (cache.TryGetValue(groupKey, out var creative) && !string.IsNullOrWhiteSpace(creative))
        {
            return creative;
        }

        return worldCode;
    }

    /// <summary>
    /// Предпочитает вариант, который реально в creative menu
    /// (tabs или stacks), как InventoryPlayerCreative.GatherTabStacks.
    /// </summary>
    private static bool IsBetterDisplayBlockCode(
        IWorldAccessor world,
        string candidate,
        string existing,
        Block candidateBlock)
    {
        Block? existingBlock = null;
        try
        {
            existingBlock = world.GetBlock(new AssetLocation(existing));
        }
        catch
        {
            // ignore
        }

        return ScoreDisplayBlockCode(candidate, candidateBlock)
            > ScoreDisplayBlockCode(existing, existingBlock);
    }

    private static int ScoreDisplayBlockCode(string code, Block? block)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return int.MinValue;
        }

        var score = 0;

        // Stacks важнее tabs: lantern material/lining/glass только в stacks.
        if (block?.CreativeInventoryStacks is { Length: > 0 })
        {
            score += 1500;
        }
        else if (block?.CreativeInventoryTabs is { Length: > 0 })
        {
            score += 1000;
        }

        var lower = code.ToLowerInvariant();

        // Фонари: creative = *-up. EP/orientable: часто *-south / *-north.
        if (lower.EndsWith("-up") || lower.Contains("-up-"))
        {
            score += 220;
        }
        else if (lower.EndsWith("-south") || lower.Contains("-south-"))
        {
            score += 200;
        }
        else if (lower.EndsWith("-north") || lower.Contains("-north-"))
        {
            score += 40;
        }
        else if (lower.EndsWith("-east") || lower.Contains("-east-")
            || lower.EndsWith("-west") || lower.Contains("-west-"))
        {
            score += 20;
        }
        else if (lower.EndsWith("-down") || lower.Contains("-down-"))
        {
            score += 10;
        }

        if (lower.Contains("-normal"))
        {
            score += 80;
        }

        if (lower.Contains("-burned") || lower.Contains("-broken") || lower.Contains("-ruined"))
        {
            score -= 120;
        }

        score += Math.Min(40, code.Length);
        return score;
    }

    private static bool IsTerrainMaterial(EnumBlockMaterial material)
    {
        return material is EnumBlockMaterial.Air
            or EnumBlockMaterial.Soil
            or EnumBlockMaterial.Gravel
            or EnumBlockMaterial.Sand
            or EnumBlockMaterial.Stone
            or EnumBlockMaterial.Ore
            or EnumBlockMaterial.Water
            or EnumBlockMaterial.Snow
            or EnumBlockMaterial.Ice
            or EnumBlockMaterial.Mantle
            or EnumBlockMaterial.Plant
            or EnumBlockMaterial.Lava;
    }

    /// <summary>
    /// Блок, с которым use-only игрок реально может взаимодействовать
    /// (ПКМ: открыть, повесить факел, положить инструмент…), не террейн.
    /// </summary>
    private static bool IsUseInteractableForScan(
        IWorldAccessor world,
        Block block,
        string code,
        string groupKey,
        BlockPos pos)
    {
        // 0) Явные path (torchholder / toolrack / door…) — до terrain-фильтров.
        if (IsUseInteractivePath(code) || IsUseInteractivePath(groupKey))
        {
            return true;
        }

        // 1) Имя class / EntityClass (BlockTorchHolder, TorchHolder, …).
        if (IsUseInteractiveName(block.GetType().Name)
            || IsUseInteractiveName(block.EntityClass))
        {
            return true;
        }

        // 2) Террейн / руды — не в список (после явных interactable).
        if (IsTerrainLikeCode(groupKey) || IsTerrainLikeCode(code))
        {
            return false;
        }

        // 3) EntityClass (машины EP, сундуки…).
        if (!string.IsNullOrWhiteSpace(block.EntityClass)
            && !IsTerrainEntityClass(block.EntityClass))
        {
            return true;
        }

        // 4) Behaviors: Container, Door, HorizontalAttachable (holder/полка), Multiblock…
        if (HasUseInteractiveBehavior(block))
        {
            return true;
        }

        // 5) BE на позиции.
        try
        {
            var be = world.BlockAccessor.GetBlockEntity(pos);
            if (be == null)
            {
                return false;
            }

            if (be is IBlockEntityContainer)
            {
                return true;
            }

            if (IsUseInteractiveName(be.GetType().Name))
            {
                return true;
            }

            foreach (var bh in be.Behaviors)
            {
                if (bh != null && IsUseInteractiveName(bh.GetType().Name))
                {
                    return true;
                }
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static bool IsTerrainEntityClass(string entityClass)
    {
        return entityClass.Contains("Farmland", StringComparison.OrdinalIgnoreCase)
            || entityClass.Contains("Soil", StringComparison.OrdinalIgnoreCase)
            || entityClass.Contains("Rock", StringComparison.OrdinalIgnoreCase)
            || entityClass.Contains("Ore", StringComparison.OrdinalIgnoreCase)
            || entityClass.Contains("Plant", StringComparison.OrdinalIgnoreCase)
            || entityClass.Contains("Sapling", StringComparison.OrdinalIgnoreCase)
            || entityClass.Contains("BerryBush", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Behaviors для Use: контейнер, дверь, holder/полка (HorizontalAttachable), Multiblock-машины.
    /// </summary>
    private static bool HasUseInteractiveBehavior(Block block)
    {
        var behaviors = block.BlockBehaviors;
        if (behaviors == null || behaviors.Length == 0)
        {
            return false;
        }

        foreach (var behavior in behaviors)
        {
            if (behavior == null)
            {
                continue;
            }

            var name = behavior.GetType().Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            // Чистый Unstable / Harvestable / Decor — не Use.
            if (name.Contains("Unstable", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Harvestable", StringComparison.OrdinalIgnoreCase)
                || name.Contains("BreakIfFloating", StringComparison.OrdinalIgnoreCase)
                || name.Contains("FiniteSpreadingLiquid", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Snow", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.Contains("Lockable", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Door", StringComparison.OrdinalIgnoreCase))
            {
                // Lockable один — мало; смотрим дальше.
                continue;
            }

            if (name.Contains("Container", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Door", StringComparison.OrdinalIgnoreCase)
                || name.Contains("TrapDoor", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Trapdoor", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Openable", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Firepit", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Shelf", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Barrel", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Chest", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Crate", StringComparison.OrdinalIgnoreCase)
                || name.Contains("GroundStorage", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Inventory", StringComparison.OrdinalIgnoreCase)
                // holder / полка / toolrack
                || name.Contains("HorizontalAttachable", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Attachable", StringComparison.OrdinalIgnoreCase)
                // EP и др. multiblock-машины (control-block)
                || name.Contains("Multiblock", StringComparison.OrdinalIgnoreCase)
                || name.Contains("BlockEntityInteract", StringComparison.OrdinalIgnoreCase)
                || name.Contains("HorizontalOrientable", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(block.EntityClass))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUseInteractiveName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.Equals("Block", StringComparison.OrdinalIgnoreCase)
            || name.Equals("BlockGeneric", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Generic", StringComparison.OrdinalIgnoreCase)
            || name.Equals("BlockEntity", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return name.Contains("Door", StringComparison.OrdinalIgnoreCase)
            || name.Contains("TrapDoor", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Trapdoor", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Chest", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Container", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Openable", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Firepit", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Shelf", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Bookshelf", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Barrel", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Crate", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Oven", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Forge", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Anvil", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Quern", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Bloomery", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Trough", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Sign", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Bed", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Chair", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Seat", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Holder", StringComparison.OrdinalIgnoreCase)
            || name.Contains("TorchHolder", StringComparison.OrdinalIgnoreCase)
            || name.Contains("ToolRack", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Rack", StringComparison.OrdinalIgnoreCase)
            || name.Contains("GenericTypedContainer", StringComparison.OrdinalIgnoreCase)
            || name.Contains("GenericContainer", StringComparison.OrdinalIgnoreCase)
            || name.Contains("GroundStorage", StringComparison.OrdinalIgnoreCase)
            || name.Contains("LabeledChest", StringComparison.OrdinalIgnoreCase)
            || name.Contains("DisplayCase", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Crock", StringComparison.OrdinalIgnoreCase)
            || name.Contains("CookingPot", StringComparison.OrdinalIgnoreCase)
            || name.Contains("MealContainer", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Basket", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Hopper", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Chute", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Furnace", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Workbench", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Generator", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Motor", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Transformator", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Transformer", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Accumulator", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Charger", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Cable", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Switch", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Connector", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Electrical", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUseInteractivePath(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var path = code;
        var colon = code.IndexOf(':');
        if (colon >= 0 && colon + 1 < code.Length)
        {
            path = code[(colon + 1)..];
        }

        path = path.ToLowerInvariant();
        return path.Contains("torchholder")
            || path.Contains("toolrack")
            || path.Contains("shelf")
            || path.Contains("bookshelf")
            || path.Contains("chest")
            || path.Contains("crate")
            || path.Contains("barrel")
            || path.Contains("door")
            || path.Contains("trapdoor")
            || path.Contains("firepit")
            || path.Contains("oven")
            || path.Contains("forge")
            || path.Contains("anvil")
            || path.Contains("quern")
            || path.Contains("bloomery")
            || path.Contains("trough")
            || path.Contains("sign")
            || path.Contains("bed-")
            || path.StartsWith("bed")
            || path.Contains("chair")
            || path.Contains("displaycase")
            || path.Contains("crock")
            || path.Contains("hopper")
            || path.Contains("chute")
            || path.Contains("furnace")
            || path.Contains("workbench")
            || path.Contains("generator")
            || path.Contains("motor")
            || path.Contains("transformator")
            || path.Contains("transformer")
            || path.Contains("accumulator")
            || path.Contains("charger")
            || path.Contains("cable")
            || path.Contains("switch")
            || path.Contains("connector")
            || path.Contains("lantern")
            || path.Contains("clutter") && path.Contains("shelf");
    }

    /// <summary>
    /// Террейн по первому сегменту path (soil/rock/ore…), без ложных срабатываний на torchholder и т.п.
    /// </summary>
    private static bool IsTerrainLikeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return true;
        }

        var path = code;
        var colon = code.IndexOf(':');
        if (colon >= 0 && colon + 1 < code.Length)
        {
            path = code[(colon + 1)..];
        }

        path = path.ToLowerInvariant();
        var first = path;
        var dash = path.IndexOf('-');
        if (dash > 0)
        {
            first = path[..dash];
        }

        // Первый сегмент — типичный worldgen.
        if (first is "soil" or "dirt" or "mud" or "gravel" or "sand" or "rock" or "stone"
            or "cobblestone" or "cobble" or "ore" or "mineral" or "water" or "lava" or "ice"
            or "snow" or "air" or "mantle" or "rawclay" or "clay" or "peat" or "forestfloor"
            or "tallgrass" or "leaves" or "log" or "planks" or "caveart" or "fern" or "ferns"
            or "flower" or "crop" or "sapling" or "looseores" or "loosestones" or "looseboulders"
            or "looseflints" or "crushed" or "stalagmite" or "stalactite" or "geode"
            or "bonyremains" or "bones" or "farmland" or "layeredrock" or "rockpolished"
            or "basalt" or "granite" or "andesite" or "chalk" or "chert" or "conglomerate"
            or "limestone" or "sandstone" or "shale" or "phyllite" or "slate" or "kimberlite"
            or "suevite" or "bauxite" or "claystone" or "peridotite" or "halite" or "olivine"
            or "saltpeter" or "quartz" or "obsidian" or "scoria" or "tuff" or "phyllite"
            or "gneiss" or "marble" or "whitemarble" or "redmarble" or "greenmarble"
            or "clay" or "blueclay" or "fireclay" or "terracottaclay")
        {
            return true;
        }

        if (path.StartsWith("soil-", StringComparison.Ordinal)
            || path.StartsWith("rock-", StringComparison.Ordinal)
            || path.StartsWith("ore-", StringComparison.Ordinal)
            || path.StartsWith("stone-", StringComparison.Ordinal)
            || path.StartsWith("sand-", StringComparison.Ordinal)
            || path.StartsWith("gravel-", StringComparison.Ordinal)
            || path.StartsWith("log-", StringComparison.Ordinal)
            || path.StartsWith("planks-", StringComparison.Ordinal)
            || path.StartsWith("leaves-", StringComparison.Ordinal)
            || path.StartsWith("tallgrass-", StringComparison.Ordinal)
            || path.StartsWith("crop-", StringComparison.Ordinal)
            || path.StartsWith("sapling-", StringComparison.Ordinal)
            || path.StartsWith("flower-", StringComparison.Ordinal)
            || path.StartsWith("looseores-", StringComparison.Ordinal)
            || path.StartsWith("loosestones-", StringComparison.Ordinal)
            || path.StartsWith("farmland", StringComparison.Ordinal)
            || path.StartsWith("plant-", StringComparison.Ordinal)
            || path.StartsWith("vine-", StringComparison.Ordinal)
            || path.StartsWith("stalagmite", StringComparison.Ordinal)
            || path.StartsWith("stalactite", StringComparison.Ordinal)
            || path.StartsWith("crystal-", StringComparison.Ordinal)
            || path.StartsWith("geode", StringComparison.Ordinal)
            || path.StartsWith("bonyremains", StringComparison.Ordinal)
            || path.StartsWith("bones-", StringComparison.Ordinal)
            || path.StartsWith("soil", StringComparison.Ordinal) && path.Contains("farmland", StringComparison.Ordinal)
            || path.Contains("farmland", StringComparison.Ordinal)
            || path.Contains("/soil", StringComparison.Ordinal)
            || path.Contains("/rock", StringComparison.Ordinal)
            || path.Contains("/stone", StringComparison.Ordinal)
            || path.Contains("/ore", StringComparison.Ordinal)
            || path.Contains("rawrock", StringComparison.Ordinal)
            || path.Contains("terrain", StringComparison.Ordinal)
            || path.Contains("ground", StringComparison.Ordinal) && !path.Contains("groundstorage", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }
}