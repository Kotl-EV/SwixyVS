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

/// <summary>Часть <see cref="SwixyClaimChunkMod"/> — сервер: пакетный клейм.</summary>
public sealed partial class SwixyClaimChunkMod
{
    /// <summary>Обрабатывает выделение чанков: свободные — claim, свои — unclaim; чужие — unclaim для админа.</summary>
    private ClaimActionResult ProcessChunksBatch(IServerPlayer player, IReadOnlyList<ClaimChunkCoordPacket> chunks)
    {
        if (chunks.Count == 0)
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-unknown");
        }

        var envError = ValidateClaimEnvironment(player);
        if (envError != null)
        {
            return envError.Value;
        }

        // Разделяем чанки по состоянию карты
        var freeChunks = new List<(int ChunkX, int ChunkZ)>();
        var ownChunks = new List<(int ChunkX, int ChunkZ)>();
        var otherChunks = new List<(int ChunkX, int ChunkZ)>();
        var seen = new HashSet<long>();
        var adminUnclaim = CanAdminUnclaimOthers(player);

        foreach (var chunk in chunks)
        {
            var packed = PackChunkCoord(chunk.ChunkX, chunk.ChunkZ);
            if (!seen.Add(packed))
            {
                continue;
            }

            switch (BuildCell(player, chunk.ChunkX, chunk.ChunkZ).State)
            {
                case ClaimChunkCellState.Free:
                    freeChunks.Add((chunk.ChunkX, chunk.ChunkZ));
                    break;
                case ClaimChunkCellState.Own:
                    ownChunks.Add((chunk.ChunkX, chunk.ChunkZ));
                    break;
                case ClaimChunkCellState.Other:
                    if (adminUnclaim)
                    {
                        otherChunks.Add((chunk.ChunkX, chunk.ChunkZ));
                    }
                    else
                    {
                        var ownerName = BuildCell(player, chunk.ChunkX, chunk.ChunkZ).OwnerName;
                        return ClaimActionResult.Error("swixyclaimchunk:error-owned-by-other", ownerName ?? "?");
                    }
                    break;
                default:
                    return ClaimActionResult.Error("swixyclaimchunk:error-out-of-world");
            }
        }

        var claimed = 0;
        var unclaimed = 0;
        var adminUnclaimed = 0;
        ClaimActionResult? lastError = null;

        if (freeChunks.Count > 0)
        {
            var claimResult = TryClaimFreeChunksBatch(player, freeChunks);
            if (claimResult.MessageType != 0)
            {
                return claimResult;
            }

            claimed = freeChunks.Count;
        }

        if (ownChunks.Count > 0)
        {
            var unclaimResult = TryUnclaimChunksBatch(player, ownChunks, allowOtherPlayersClaims: false);
            if (unclaimResult.MessageType != 0 && claimed == 0 && otherChunks.Count == 0)
            {
                return unclaimResult;
            }

            if (unclaimResult.MessageType != 0)
            {
                lastError = unclaimResult;
            }
            else
            {
                unclaimed = ownChunks.Count;
            }
        }

        if (otherChunks.Count > 0)
        {
            var adminResult = TryUnclaimChunksBatch(player, otherChunks, allowOtherPlayersClaims: true);
            if (adminResult.MessageType != 0 && claimed == 0 && unclaimed == 0)
            {
                return adminResult;
            }

            if (adminResult.MessageType != 0)
            {
                lastError = adminResult;
            }
            else
            {
                adminUnclaimed = otherChunks.Count;
                serverApi!.Logger.Notification(
                    "[SwixyClaimChunk] Admin {0} unclaimed {1} chunks from other players' claims",
                    player.PlayerName,
                    adminUnclaimed);
            }
        }

        if (claimed == 0 && unclaimed == 0 && adminUnclaimed == 0)
        {
            return lastError ?? ClaimActionResult.Error("swixyclaimchunk:error-unknown");
        }

        var message = BuildBatchResultMessage(player, claimed, unclaimed, adminUnclaimed);
        if (lastError is { } failedResult && failedResult.HasMessage)
        {
            message = $"{message} {failedResult.Resolve(player)}";
        }

        return ClaimActionResult.SuccessComposite(message);
    }

    /// <summary>
    /// Клеймит свободные чанки: один чанк, сплошной прямоугольник или связная область.
    /// </summary>
    private ClaimActionResult TryClaimFreeChunksBatch(IServerPlayer player, IReadOnlyList<(int ChunkX, int ChunkZ)> chunks)
    {
        if (chunks.Count == 1)
        {
            if (!TryBuildChunkArea(chunks[0].ChunkX, chunks[0].ChunkZ, out var singleArea))
            {
                return ClaimActionResult.Error("swixyclaimchunk:error-out-of-world");
            }

            return TryAddChunkClaim(player, singleArea);
        }

        if (TryBuildSolidSelectionRectangle(chunks, out var rectangleArea))
        {
            serverApi?.Logger.Notification(
                "[SwixyClaimChunk] Claiming solid rectangle for {0}: {1},{2},{3} to {4},{5},{6}",
                player.PlayerName,
                rectangleArea.X1, rectangleArea.Y1, rectangleArea.Z1,
                rectangleArea.X2, rectangleArea.Y2, rectangleArea.Z2);
            return TryAddChunkClaim(player, rectangleArea);
        }

        return TryAddConnectedChunkAreas(player, chunks);
    }

    /// <summary>
    /// Снимает клейм с чанков; для прямоугольника — одна операция по bounding area.
    /// </summary>
    private ClaimActionResult TryUnclaimChunksBatch(
        IServerPlayer player,
        IReadOnlyList<(int ChunkX, int ChunkZ)> chunks,
        bool allowOtherPlayersClaims)
    {
        if (chunks.Count == 0)
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-unknown");
        }

        var successMessageKey = allowOtherPlayersClaims
            ? "swixyclaimchunk:message-batch-admin-unclaimed"
            : "swixyclaimchunk:message-batch-unclaimed";

        if (TryBuildSolidSelectionRectangle(chunks, out var rectangleArea))
        {
            var claim = FindIntersectingClaim(rectangleArea);
            if (claim != null && CanUnclaimFromClaim(claim, player.PlayerUID, allowOtherPlayersClaims))
            {
                var result = TryRemoveAreaFromClaim(claim, rectangleArea);
                if (result.MessageType == 0)
                {
                    return ClaimActionResult.Success(successMessageKey, chunks.Count);
                }
            }
        }

        foreach (var (chunkX, chunkZ) in chunks)
        {
            if (!TryBuildChunkArea(chunkX, chunkZ, out var chunkArea))
            {
                return ClaimActionResult.Error("swixyclaimchunk:error-out-of-world");
            }

            var claim = FindIntersectingClaim(chunkArea);
            if (claim == null || !CanUnclaimFromClaim(claim, player.PlayerUID, allowOtherPlayersClaims))
            {
                return ClaimActionResult.Error("swixyclaimchunk:error-cannot-remove");
            }

            var result = TryRemoveAreaFromClaim(claim, chunkArea);
            if (result.MessageType != 0)
            {
                return result;
            }
        }

        return ClaimActionResult.Success(successMessageKey, chunks.Count);
    }

    /// <summary>Снимает клейм с собственных чанков.</summary>
    private ClaimActionResult TryUnclaimOwnChunksBatch(IServerPlayer player, IReadOnlyList<(int ChunkX, int ChunkZ)> chunks)
    {
        return TryUnclaimChunksBatch(player, chunks, allowOtherPlayersClaims: false);
    }

    /// <summary>Проверяет, что выделение — сплошной прямоугольник чанков без дыр.</summary>
    private bool TryBuildSolidSelectionRectangle(IReadOnlyList<(int ChunkX, int ChunkZ)> chunks, out Cuboidi area)
    {
        area = null!;
        if (chunks.Count == 0)
        {
            return false;
        }

        var minChunkX = chunks[0].ChunkX;
        var maxChunkX = chunks[0].ChunkX;
        var minChunkZ = chunks[0].ChunkZ;
        var maxChunkZ = chunks[0].ChunkZ;
        var selected = new HashSet<long>(chunks.Count);

        foreach (var (chunkX, chunkZ) in chunks)
        {
            minChunkX = Math.Min(minChunkX, chunkX);
            maxChunkX = Math.Max(maxChunkX, chunkX);
            minChunkZ = Math.Min(minChunkZ, chunkZ);
            maxChunkZ = Math.Max(maxChunkZ, chunkZ);
            selected.Add(PackChunkCoord(chunkX, chunkZ));
        }

        for (var chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
        {
            for (var chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
            {
                if (!selected.Contains(PackChunkCoord(chunkX, chunkZ)))
                {
                    return false;
                }
            }
        }

        return TryBuildChunksBoundingArea(minChunkX, minChunkZ, maxChunkX, maxChunkZ, out area);
    }

    /// <summary>Строит Cuboidi по углам прямоугольника чанков (min/max chunk coords).</summary>
    private bool TryBuildChunksBoundingArea(int minChunkX, int minChunkZ, int maxChunkX, int maxChunkZ, out Cuboidi area)
    {
        area = null!;
        if (!TryBuildChunkArea(minChunkX, minChunkZ, out var minCorner)
            || !TryBuildChunkArea(maxChunkX, maxChunkZ, out var maxCorner))
        {
            return false;
        }

        area = new Cuboidi(
            Math.Min(minCorner.X1, maxCorner.X1),
            Math.Min(minCorner.Y1, maxCorner.Y1),
            Math.Min(minCorner.Z1, maxCorner.Z1),
            Math.Max(minCorner.X2, maxCorner.X2),
            Math.Max(minCorner.Y2, maxCorner.Y2),
            Math.Max(minCorner.Z2, maxCorner.Z2));
        return true;
    }

    /// <summary>
    /// Клеймит несвязный прямоугольник: итеративно добавляет чанки к соседнему привату
    /// или создаёт новый; после — MergeTouchingOwnClaims.
    /// </summary>
    private ClaimActionResult TryAddConnectedChunkAreas(IServerPlayer player, IReadOnlyList<(int ChunkX, int ChunkZ)> chunks)
    {
        var remaining = new HashSet<long>(chunks.Count);
        var areasByChunk = new Dictionary<long, Cuboidi>(chunks.Count);

        foreach (var (chunkX, chunkZ) in chunks)
        {
            if (!TryBuildChunkArea(chunkX, chunkZ, out var area))
            {
                return ClaimActionResult.Error("swixyclaimchunk:error-out-of-world");
            }

            var existing = FindIntersectingClaim(area);
            if (existing != null && existing.OwnedByPlayerUid != player.PlayerUID)
            {
                return ClaimActionResult.Error("swixyclaimchunk:error-owned-by-other", ResolveClaimOwnerName(existing));
            }

            var packed = PackChunkCoord(chunkX, chunkZ);
            remaining.Add(packed);
            areasByChunk[packed] = area;
        }

        var ownClaims = GetOwnClaims(player.PlayerUID).ToList();
        LandClaim? targetClaim = null;
        foreach (var packed in remaining)
        {
            targetClaim = FindAdjacentOwnClaim(ownClaims, areasByChunk[packed]);
            if (targetClaim != null)
            {
                break;
            }
        }

        var createdNewClaim = targetClaim == null;
        if (createdNewClaim)
        {
            var usedVolume = ownClaims.Sum(static claim => (long)claim.SizeXYZ);
            var totalVolume = remaining.Sum(packed => (long)areasByChunk[packed].SizeXYZ);
            var allowance = GetLandClaimAllowance(player);
            if (allowance > 0 && usedVolume + totalVolume > allowance)
            {
                return ClaimActionResult.Error("swixyclaimchunk:error-allowance");
            }

            var usedAreas = ownClaims.Sum(static claim => claim.Areas?.Count ?? 0);
            var maxAreas = GetLandClaimMaxAreas(player);
            if (maxAreas > 0 && usedAreas + 1 > maxAreas)
            {
                return ClaimActionResult.Error("swixyclaimchunk:error-areas");
            }

            var claimIndex = GetNextClaimIndex(player, ownClaims);
            targetClaim = LandClaim.CreateClaim(player, ProtectionLevel);
            targetClaim.Description = BuildClaimName(player, claimIndex);
        }

        // Пока есть чанки — расширяем приват или добавляем отдельные области
        while (remaining.Count > 0)
        {
            var madeProgress = false;
            foreach (var packed in remaining.ToList())
            {
                var area = areasByChunk[packed];
                if (createdNewClaim && targetClaim!.Areas!.Count == 0)
                {
                    var firstError = targetClaim.AddArea(area);
                    if (firstError != EnumClaimError.NoError)
                    {
                        return ClaimActionResult.Error(ClaimErrorKey(firstError));
                    }

                    remaining.Remove(packed);
                    madeProgress = true;
                    continue;
                }

                if (TryExpandTouchingArea(targetClaim!, area))
                {
                    remaining.Remove(packed);
                    madeProgress = true;
                    continue;
                }

                if (WouldOverlapAnotherClaim(targetClaim!, area, player.PlayerUID))
                {
                    continue;
                }

                var addError = targetClaim!.AddArea(area);
                if (addError == EnumClaimError.NoError)
                {
                    remaining.Remove(packed);
                    madeProgress = true;
                }
            }

            if (!madeProgress)
            {
                break;
            }

            ConsolidateClaimAreas(targetClaim!);
        }

        if (remaining.Count > 0)
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-batch-not-connected");
        }

        if (createdNewClaim)
        {
            serverApi!.World.Claims.Add(targetClaim!);
            serverApi.Logger.Notification(
                "[SwixyClaimChunk] Added connected land claim '{0}' for {1} with {2} chunk areas",
                targetClaim!.Description,
                player.PlayerName,
                chunks.Count);
        }
        else
        {
            TouchClaim(targetClaim!);
        }

        MergeTouchingOwnClaims(player, targetClaim!);
        return ClaimActionResult.Success("swixyclaimchunk:message-batch-claimed", chunks.Count);
    }


    /// <summary>Упаковывает пару chunkX/chunkZ в long для HashSet.</summary>
    private static long PackChunkCoord(int chunkX, int chunkZ)
    {
        return ((long)chunkX << 32) ^ (uint)chunkZ;
    }

    /// <summary>Локализованное сообщение по итогам пакетной операции.</summary>
    private static string BuildBatchResultMessage(IServerPlayer player, int claimed, int unclaimed, int adminUnclaimed = 0)
    {
        var langCode = player.LanguageCode;
        var parts = new List<string>();
        if (claimed > 0)
        {
            parts.Add(Lang.GetL(langCode, "swixyclaimchunk:message-batch-claimed", claimed));
        }

        if (unclaimed > 0)
        {
            parts.Add(Lang.GetL(langCode, "swixyclaimchunk:message-batch-unclaimed", unclaimed));
        }

        if (adminUnclaimed > 0)
        {
            parts.Add(Lang.GetL(langCode, "swixyclaimchunk:message-batch-admin-unclaimed", adminUnclaimed));
        }

        return parts.Count > 0
            ? string.Join(" ", parts)
            : Lang.GetL(langCode, "swixyclaimchunk:error-unknown");
    }

    private static string ClaimErrorKey(EnumClaimError error)
    {
        return error switch
        {
            EnumClaimError.NotAdjacent => "swixyclaimchunk:error-not-adjacent",
            EnumClaimError.Overlapping => "swixyclaimchunk:error-overlap",
            _ => "swixyclaimchunk:error-unknown"
        };
    }

}
