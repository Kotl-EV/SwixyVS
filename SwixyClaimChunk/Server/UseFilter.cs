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

        // Не трогаем TouchClaim: не двигаем claim в списке All и индекс ClaimId.
        var normalized = SanitizeUseFilterCodes(codes);
        if (mode == ClaimUseFilterMode.Whitelist && normalized.Count == 0)
        {
            return ClaimActionResult.Error("swixyclaimchunk:use-filter-error-empty");
        }

        var keys = ClaimStorageKeys.EnumerateClaimStorageKeys(claim).ToList();
        if (keys.Count == 0)
        {
            return ClaimActionResult.Error("swixyclaimchunk:claims-error-not-found");
        }

        if (mode == ClaimUseFilterMode.AllowAll)
        {
            ClearUseFilterKeysOnly(claim);
            PersistUseFiltersNow();
            BroadcastUseFiltersSync();
            return ClaimActionResult.Success("swixyclaimchunk:use-filter-message-saved");
        }

        WriteUseFilter(claim, ClaimUseFilterMode.Whitelist, normalized);
        PersistUseFiltersNow();
        BroadcastUseFiltersSync();

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
        if (!TryConsumePacketRate(fromPlayer, "usefilters", ClaimConstants.RateUseFiltersRequestMs))
        {
            return;
        }

        BroadcastUseFiltersSync(fromPlayer);
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

    // ── Use-filter scan: cache + chunk pass + time budget (без лага сервера) ──

    /// <summary>Бюджет одного тика скана (мс). Безопасно для dedicated.</summary>
    private const int UseFilterScanBudgetMs = 2;

    /// <summary>
    /// Запускает быстрый фоновый скан: кэш → BlockEntities → уникальные block id в чанках.
    /// Не блокирует тик (time-budget ~2 ms/шаг).
    /// </summary>
    private void OnUseFilterScanRequestPacket(IServerPlayer fromPlayer, ClaimUseFilterScanRequestPacket packet)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        if (!TryConsumePacketRate(fromPlayer, "usefilterscan", ClaimConstants.RateUseFilterScanMs))
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

        EnsureUseFilterScanLookups();

        var areasList = areas.ToList();
        var signature = ComputeAreasSignature(areasList);
        var cacheKey = BuildClaimStorageKey(claim);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            cacheKey = "claim:" + packet.ClaimId;
        }

        // Мгновенный ответ из кэша (повторное открытие UI / другой игрок).
        if (useFilterScanCache.TryGetValue(cacheKey, out var cached)
            && cached.AreasSignature == signature)
        {
            serverChannel.SendPacket(new ClaimUseFilterScanResultPacket
            {
                ClaimId = packet.ClaimId,
                CodesRaw = cached.CodesRaw,
                CodeCount = cached.CodeCount,
                ScannedBlocks = cached.ScannedBlocks,
                Message = cached.CodeCount == 0
                    ? Lang.GetL(fromPlayer.LanguageCode, "swixyclaimchunk:use-filter-scan-empty")
                    : Lang.GetL(fromPlayer.LanguageCode, "swixyclaimchunk:use-filter-scan-ok", cached.CodeCount)
            }, fromPlayer);
            return;
        }

        var jobKey = fromPlayer.PlayerUID + ":" + packet.ClaimId;
        if (activeUseFilterScans.ContainsKey(jobKey))
        {
            return;
        }

        var chunks = BuildIntersectingChunkCoords(areasList);
        activeUseFilterScans[jobKey] = new UseFilterScanJob
        {
            Player = fromPlayer,
            ClaimId = packet.ClaimId,
            Claim = claim,
            Areas = areasList,
            Chunks = chunks,
            CacheKey = cacheKey,
            AreasSignature = signature,
            Phase = 0,
            ChunkIndex = 0,
            LocalIndex = 0
        };

        serverApi.Event.EnqueueMainThreadTask(() => ProcessUseFilterScanStep(jobKey), "swixy-usefilter-scan");
    }

    private void InvalidateUseFilterScanCache(LandClaim claim)
    {
        var key = BuildClaimStorageKey(claim);
        if (!string.IsNullOrWhiteSpace(key))
        {
            useFilterScanCache.Remove(key);
        }
    }

    private static long ComputeAreasSignature(IReadOnlyList<Cuboidi> areas)
    {
        unchecked
        {
            long h = areas.Count * 397L;
            for (var i = 0; i < areas.Count; i++)
            {
                var a = areas[i];
                h = (h * 31) + a.X1;
                h = (h * 31) + a.Y1;
                h = (h * 31) + a.Z1;
                h = (h * 31) + a.X2;
                h = (h * 31) + a.Y2;
                h = (h * 31) + a.Z2;
            }

            return h;
        }
    }

    /// <summary>Все (cx,cy,cz) чанки, пересекающие cuboid areas.</summary>
    private List<(int Cx, int Cy, int Cz)> BuildIntersectingChunkCoords(IReadOnlyList<Cuboidi> areas)
    {
        var set = new HashSet<(int, int, int)>();
        if (serverApi == null)
        {
            return [];
        }

        var cs = serverApi.WorldManager.ChunkSize;
        if (cs <= 0)
        {
            cs = 32;
        }

        foreach (var area in areas)
        {
            var x1 = Math.Min(area.X1, area.X2);
            var x2 = Math.Max(area.X1, area.X2) - 1;
            var y1 = Math.Min(area.Y1, area.Y2);
            var y2 = Math.Max(area.Y1, area.Y2) - 1;
            var z1 = Math.Min(area.Z1, area.Z2);
            var z2 = Math.Max(area.Z1, area.Z2) - 1;
            if (x2 < x1 || y2 < y1 || z2 < z1)
            {
                continue;
            }

            var cx0 = FloorDiv(x1, cs);
            var cx1 = FloorDiv(x2, cs);
            var cy0 = FloorDiv(y1, cs);
            var cy1 = FloorDiv(y2, cs);
            var cz0 = FloorDiv(z1, cs);
            var cz1 = FloorDiv(z2, cs);

            for (var cx = cx0; cx <= cx1; cx++)
            {
                for (var cy = cy0; cy <= cy1; cy++)
                {
                    for (var cz = cz0; cz <= cz1; cz++)
                    {
                        set.Add((cx, cy, cz));
                    }
                }
            }
        }

        return set.OrderBy(t => t.Item1).ThenBy(t => t.Item3).ThenBy(t => t.Item2).ToList();
    }

    /// <summary>Таблицы skip-id и creative-кодов — один раз на мир.</summary>
    private void EnsureUseFilterScanLookups()
    {
        if (serverApi == null || useFilterSkipBlockIds != null)
        {
            return;
        }

        var skip = new HashSet<int>();
        var creative = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var b in serverApi.World.Blocks)
        {
            if (b == null)
            {
                continue;
            }

            if (b.Id == 0 || b.Code == null)
            {
                skip.Add(b.Id);
                continue;
            }

            // Террейн без EntityClass/Use-behavior — не трогаем (O(1) skip по id).
            var mat = b.BlockMaterial;
            var noEntity = string.IsNullOrWhiteSpace(b.EntityClass);
            if (noEntity
                && IsTerrainMaterial(mat)
                && !HasUseInteractiveBehavior(b)
                && !IsUseInteractivePath(b.Code.ToString())
                && !IsUseInteractiveName(b.GetType().Name))
            {
                skip.Add(b.Id);
            }

            if (!HasCreativeDisplay(b))
            {
                continue;
            }

            var full = ClaimCodeUtil.NormalizeCollectibleCode(b.Code.ToString());
            if (string.IsNullOrWhiteSpace(full) || ClaimCodeUtil.IsMultiblockStubCode(full))
            {
                continue;
            }

            var groupKey = ClaimCodeUtil.GetCatalogGroupKey(full);
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                groupKey = ClaimCodeUtil.StripVariantSuffixes(full);
            }

            if (string.IsNullOrWhiteSpace(groupKey))
            {
                groupKey = full;
            }

            if (!creative.TryGetValue(groupKey, out var existing)
                || ScoreDisplayBlockCode(full, b) > ScoreDisplayBlockCode(existing, null))
            {
                creative[groupKey] = full;
            }
        }

        useFilterSkipBlockIds = skip;
        useFilterCreativeCache = creative;
        serverApi.Logger.Notification(
            "[SwixyClaimChunk] Use-filter scan lookups: skipIds={0} creativeGroups={1}",
            skip.Count, creative.Count);
    }

    /// <summary>
    /// Один квант: phase0 = BlockEntities чанка, phase1 = block ids (Data).
    /// Лимит ~2 ms — сервер не подвисает.
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var accessor = serverApi.World.BlockAccessor;
            var cs = serverApi.WorldManager.ChunkSize;
            if (cs <= 0)
            {
                cs = 32;
            }

            var blocksPerChunk = cs * cs * cs;
            var done = false;

            while (sw.ElapsedMilliseconds < UseFilterScanBudgetMs && !done)
            {
                if (job.ChunkIndex >= job.Chunks.Count)
                {
                    if (job.Phase == 0)
                    {
                        // После BE — проход по block ids.
                        job.Phase = 1;
                        job.ChunkIndex = 0;
                        job.LocalIndex = 0;
                        if (job.Chunks.Count == 0)
                        {
                            done = true;
                        }

                        continue;
                    }

                    done = true;
                    break;
                }

                var (cx, cy, cz) = job.Chunks[job.ChunkIndex];
                var chunk = accessor.GetChunk(cx, cy, cz);
                if (chunk == null || chunk.Disposed)
                {
                    job.ChunkIndex++;
                    job.LocalIndex = 0;
                    continue;
                }

                if (job.Phase == 0)
                {
                    // Phase 0: только BlockEntities — почти free, ловит сундуки/машины/двери.
                    CollectUseFilterFromChunkEntities(job, chunk, accessor);
                    job.ChunkIndex++;
                    job.LocalIndex = 0;
                    continue;
                }

                // Phase 1: уникальные block id внутри пересечения claim ∩ chunk.
                try
                {
                    chunk.Unpack_ReadOnly();
                }
                catch
                {
                    job.ChunkIndex++;
                    job.LocalIndex = 0;
                    continue;
                }

                // Границы чанка в блоках.
                var baseX = cx * cs;
                var baseY = cy * cs;
                var baseZ = cz * cs;

                // Быстрый путь: полный чанк внутри claim → идём по всем id без AABB-check.
                var fullInside = IsChunkFullyInsideAnyArea(job.Areas, baseX, baseY, baseZ, cs);

                while (job.LocalIndex < blocksPerChunk
                       && sw.ElapsedMilliseconds < UseFilterScanBudgetMs)
                {
                    var i = job.LocalIndex++;
                    job.Scanned++;

                    int blockId;
                    try
                    {
                        blockId = chunk.UnpackAndReadBlock(i, 0);
                    }
                    catch
                    {
                        // fallback: Data indexer if available
                        try
                        {
                            blockId = chunk.Data?[i] ?? 0;
                        }
                        catch
                        {
                            blockId = 0;
                        }
                    }

                    if (blockId == 0
                        || (useFilterSkipBlockIds != null && useFilterSkipBlockIds.Contains(blockId)))
                    {
                        continue;
                    }

                    if (!fullInside)
                    {
                        // local: (y * cs + z) * cs + x
                        var lx = i % cs;
                        var t = i / cs;
                        var lz = t % cs;
                        var ly = t / cs;
                        if (!IsBlockInsideAnyArea(job.Areas, baseX + lx, baseY + ly, baseZ + lz))
                        {
                            continue;
                        }
                    }

                    if (!job.SeenBlockIds.Add(blockId))
                    {
                        continue;
                    }

                    // Новый id — один раз классифицируем (без GetBlockEntity на каждой клетке).
                    TryRegisterUseFilterBlockId(job, blockId);
                }

                if (job.LocalIndex >= blocksPerChunk)
                {
                    job.ChunkIndex++;
                    job.LocalIndex = 0;
                }
            }

            if (done)
            {
                FinishUseFilterScan(jobKey, job);
                return;
            }

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

    private void CollectUseFilterFromChunkEntities(
        UseFilterScanJob job,
        IWorldChunk chunk,
        IBlockAccessor accessor)
    {
        if (serverApi == null)
        {
            return;
        }

        try
        {
            var bes = chunk.BlockEntities;
            if (bes == null)
            {
                return;
            }

            // Dictionary<BlockPos, BlockEntity> или массив / IEnumerable
            if (bes is System.Collections.IDictionary dict)
            {
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    if (entry.Value is not BlockEntity be)
                    {
                        continue;
                    }

                    RegisterUseFilterFromBlockEntity(job, be, accessor);
                }

                return;
            }

            if (bes is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is BlockEntity be)
                    {
                        RegisterUseFilterFromBlockEntity(job, be, accessor);
                    }
                    else if (item is System.Collections.DictionaryEntry de
                             && de.Value is BlockEntity be2)
                    {
                        RegisterUseFilterFromBlockEntity(job, be2, accessor);
                    }
                }
            }
        }
        catch
        {
            // chunk BE shape may vary
        }
    }

    private void RegisterUseFilterFromBlockEntity(
        UseFilterScanJob job,
        BlockEntity be,
        IBlockAccessor accessor)
    {
        if (serverApi == null || be == null)
        {
            return;
        }

        BlockPos? pos = null;
        try
        {
            pos = be.Pos;
        }
        catch
        {
            // ignore
        }

        if (pos == null || !IsBlockInsideAnyArea(job.Areas, pos.X, pos.Y, pos.Z))
        {
            return;
        }

        Block? block = null;
        try
        {
            block = be.Block ?? accessor.GetBlock(pos);
        }
        catch
        {
            return;
        }

        if (block == null || block.Id == 0)
        {
            return;
        }

        job.Scanned++;
        if (!job.SeenBlockIds.Add(block.Id))
        {
            // Уже классифицировали id — но BE-тип всё равно Use-кандидат.
            // Если id был skip — не попадёт; если уже interesting — ок.
        }

        TryRegisterUseFilterBlock(job, block, pos);
    }

    private void TryRegisterUseFilterBlockId(UseFilterScanJob job, int blockId)
    {
        if (serverApi == null)
        {
            return;
        }

        Block? block;
        try
        {
            block = serverApi.World.GetBlock(blockId);
        }
        catch
        {
            return;
        }

        if (block == null)
        {
            return;
        }

        // Без позиции: дешёвая классификация по типу (BE уже собраны в phase 0).
        TryRegisterUseFilterBlock(job, block, pos: null);
    }

    private void TryRegisterUseFilterBlock(UseFilterScanJob job, Block block, BlockPos? pos)
    {
        if (serverApi == null || block == null)
        {
            return;
        }

        if (useFilterSkipBlockIds != null && useFilterSkipBlockIds.Contains(block.Id))
        {
            return;
        }

        var code = ClaimCodeUtil.NormalizeCollectibleCode(block.Code?.ToString());
        if (string.IsNullOrWhiteSpace(code) || ClaimCodeUtil.IsMultiblockStubCode(code))
        {
            // Multiblock stub: если есть pos — control block.
            if (pos != null)
            {
                try
                {
                    IMultiblockOffset? mb = block as IMultiblockOffset
                        ?? block.GetInterface<IMultiblockOffset>(serverApi.World, pos);
                    if (mb != null)
                    {
                        var controlPos = mb.GetControlBlockPos(pos);
                        if (controlPos != null)
                        {
                            var control = serverApi.World.BlockAccessor.GetBlock(controlPos);
                            if (control != null && control.Id != 0 && job.SeenBlockIds.Add(control.Id))
                            {
                                TryRegisterUseFilterBlock(job, control, controlPos);
                            }
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            return;
        }

        var groupKey = ClaimCodeUtil.GetCatalogGroupKey(code);
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            groupKey = ClaimCodeUtil.StripVariantSuffixes(code);
        }

        if (string.IsNullOrWhiteSpace(groupKey))
        {
            groupKey = code;
        }

        if (!IsUseInteractableForScan(serverApi.World, block, code, groupKey, pos))
        {
            return;
        }

        var displayCode = PreferCreativeInventoryCodeCached(useFilterCreativeCache, block, code);
        if (string.IsNullOrWhiteSpace(displayCode))
        {
            displayCode = code;
        }

        if (!job.InterestingPreferred.TryGetValue(groupKey, out var existing)
            || IsBetterDisplayBlockCode(serverApi.World, displayCode, existing, block))
        {
            job.InterestingPreferred[groupKey] = displayCode;
        }
    }

    private static bool IsChunkFullyInsideAnyArea(
        IReadOnlyList<Cuboidi> areas,
        int baseX,
        int baseY,
        int baseZ,
        int cs)
    {
        var x2 = baseX + cs;
        var y2 = baseY + cs;
        var z2 = baseZ + cs;
        for (var i = 0; i < areas.Count; i++)
        {
            var a = areas[i];
            var ax1 = Math.Min(a.X1, a.X2);
            var ax2 = Math.Max(a.X1, a.X2);
            var ay1 = Math.Min(a.Y1, a.Y2);
            var ay2 = Math.Max(a.Y1, a.Y2);
            var az1 = Math.Min(a.Z1, a.Z2);
            var az2 = Math.Max(a.Z1, a.Z2);
            if (ax1 <= baseX && ax2 >= x2
                && ay1 <= baseY && ay2 >= y2
                && az1 <= baseZ && az2 >= z2)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBlockInsideAnyArea(IReadOnlyList<Cuboidi> areas, int x, int y, int z)
    {
        for (var i = 0; i < areas.Count; i++)
        {
            var a = areas[i];
            var ax1 = Math.Min(a.X1, a.X2);
            var ax2 = Math.Max(a.X1, a.X2);
            var ay1 = Math.Min(a.Y1, a.Y2);
            var ay2 = Math.Max(a.Y1, a.Y2);
            var az1 = Math.Min(a.Z1, a.Z2);
            var az2 = Math.Max(a.Z1, a.Z2);
            // Cuboidi в VS обычно [min, max) по осям areas claim.
            if (x >= ax1 && x < ax2 && y >= ay1 && y < ay2 && z >= az1 && z < az2)
            {
                return true;
            }
        }

        return false;
    }

    private void FinishUseFilterScan(string jobKey, UseFilterScanJob job)
    {
        activeUseFilterScans.Remove(jobKey);
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        var codes = job.InterestingPreferred.Values
            .OrderBy(static c => c, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var codesRaw = ClaimUseFilterCodesCodec.Join(codes);
        useFilterScanCache[job.CacheKey] = new UseFilterScanCacheEntry
        {
            AreasSignature = job.AreasSignature,
            CodesRaw = codesRaw,
            CodeCount = codes.Count,
            ScannedBlocks = job.Scanned
        };

        serverChannel.SendPacket(new ClaimUseFilterScanResultPacket
        {
            ClaimId = job.ClaimId,
            CodesRaw = codesRaw,
            CodeCount = codes.Count,
            ScannedBlocks = job.Scanned,
            Message = codes.Count == 0
                ? Lang.GetL(job.Player.LanguageCode, "swixyclaimchunk:use-filter-scan-empty")
                : Lang.GetL(job.Player.LanguageCode, "swixyclaimchunk:use-filter-scan-ok", codes.Count)
        }, job.Player);

        serverApi.Logger.Notification(
            "[SwixyClaimChunk] Use filter scan done claimId={0} by {1}: unique={2} scanned={3} chunks={4}",
            job.ClaimId,
            job.Player.PlayerName,
            codes.Count,
            job.Scanned,
            job.Chunks.Count);
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

        // Семья каталога (first-part / fruit / coal) — как ключи creative-кэша.
        var groupKey = ClaimCodeUtil.GetCatalogGroupKey(worldCode);
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            groupKey = ClaimCodeUtil.StripVariantSuffixes(worldCode);
        }

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
    /// В каталог Use: только двери/калитки и блоки с инвентарём.
    /// </summary>
    private static bool IsUseInteractableForScan(
        IWorldAccessor world,
        Block block,
        string code,
        string groupKey,
        BlockPos? pos)
        => ClaimCodeUtil.IsUseFilterCatalogCandidate(world, block, pos)
           || ((ClaimCodeUtil.IsDoorOrGateCode(code)
                || ClaimCodeUtil.IsDoorOrGateCode(groupKey)
                || ClaimCodeUtil.IsInventoryPathCode(code)
                || ClaimCodeUtil.IsInventoryPathCode(groupKey))
               && !ClaimCodeUtil.IsUseFilterCatalogExcluded(code)
               && !ClaimCodeUtil.IsUseFilterCatalogExcluded(groupKey));

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
            // chute/жёлоб не в Use UI (см. IsTerrainLikeCode)
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