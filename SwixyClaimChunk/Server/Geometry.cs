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

/// <summary>Часть <see cref="SwixyClaimChunkMod"/> — сервер: геометрия приватов.</summary>
public sealed partial class SwixyClaimChunkMod
{
    /// <summary>Проверяет world config allowLandClaiming.</summary>
    private bool IsLandClaimingEnabled()
    {
        return serverApi?.World.Config.GetAsBool("allowLandClaiming", true) != false;
    }

    /// <summary>
    /// Вычитает removeArea из привата: для каждой пересекающейся области снимает только пересечение
    /// (поддерживает ванильные приваты, не выровненные по чанкам).
    /// </summary>
    private ClaimActionResult TryRemoveAreaFromClaim(LandClaim claim, Cuboidi removeArea)
    {
        if (claim.Areas == null || claim.Areas.Count == 0)
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-cannot-remove");
        }

        var removedAny = false;
        for (var i = 0; i < claim.Areas.Count;)
        {
            var area = claim.Areas[i];
            if (!TryGetIntersection(area, removeArea, out var actualRemove))
            {
                i++;
                continue;
            }

            if (area.Equals(actualRemove))
            {
                claim.Areas.RemoveAt(i);
                removedAny = true;
                continue;
            }

            if (TryShrinkArea(area, actualRemove))
            {
                removedAny = true;
                i++;
                continue;
            }

            if (TrySubtractAreaFromArea(area, actualRemove, out var remainingPieces))
            {
                claim.Areas.RemoveAt(i);
                foreach (var piece in remainingPieces)
                {
                    claim.Areas.Add(piece);
                }

                removedAny = true;
                continue;
            }

            return ClaimActionResult.Error("swixyclaimchunk:error-cannot-remove");
        }

        if (!removedAny)
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-cannot-remove");
        }

        if (claim.Areas.Count == 0)
        {
            serverApi!.World.Claims.Remove(claim);
            ClearCoOwners(claim);
            ClearUseFilter(claim);
        }
        else
        {
            ConsolidateClaimAreas(claim);
            TouchClaim(claim);
        }

        return ClaimActionResult.Success("swixyclaimchunk:message-unclaimed");
    }

    /// <summary>Алиас TryRemoveAreaFromClaim для одного чанка.</summary>
    private ClaimActionResult TryRemoveChunkFromClaim(LandClaim claim, Cuboidi chunkArea)
    {
        return TryRemoveAreaFromClaim(claim, chunkArea);
    }

    /// <summary>
    /// Вырезает removeArea из area (одинаковая высота Y); до четырёх оставшихся прямоугольников.
    /// </summary>
    private static bool TrySubtractAreaFromArea(Cuboidi area, Cuboidi removeArea, out List<Cuboidi> remainingPieces)
    {
        remainingPieces = [];

        if (!area.Intersects(removeArea))
        {
            return false;
        }

        if (area.Y1 != removeArea.Y1 || area.Y2 != removeArea.Y2)
        {
            return false;
        }

        if (removeArea.X1 < area.X1 || removeArea.X2 > area.X2 || removeArea.Z1 < area.Z1 || removeArea.Z2 > area.Z2)
        {
            return false;
        }

        if (removeArea.X1 > area.X1)
        {
            remainingPieces.Add(new Cuboidi(area.X1, area.Y1, area.Z1, removeArea.X1, area.Y2, area.Z2));
        }

        if (removeArea.X2 < area.X2)
        {
            remainingPieces.Add(new Cuboidi(removeArea.X2, area.Y1, area.Z1, area.X2, area.Y2, area.Z2));
        }

        var overlapX1 = Math.Max(area.X1, removeArea.X1);
        var overlapX2 = Math.Min(area.X2, removeArea.X2);

        if (removeArea.Z1 > area.Z1)
        {
            remainingPieces.Add(new Cuboidi(overlapX1, area.Y1, area.Z1, overlapX2, area.Y2, removeArea.Z1));
        }

        if (removeArea.Z2 < area.Z2)
        {
            remainingPieces.Add(new Cuboidi(overlapX1, area.Y1, removeArea.Z2, overlapX2, area.Y2, area.Z2));
        }

        remainingPieces.RemoveAll(static piece => piece.X1 >= piece.X2 || piece.Z1 >= piece.Z2);
        return true;
    }

    /// <summary>Расширяет существующую смежную область вместо добавления новой.</summary>
    private bool TryExpandExistingArea(IEnumerable<LandClaim> ownClaims, Cuboidi chunkArea, out LandClaim expandedClaim, string? ownerPlayerUid = null)
    {
        foreach (var claim in ownClaims)
        {
            if (TryExpandTouchingArea(claim, chunkArea, out var expandedArea, out var expandedExisting)
                && !WouldOverlapAnotherAreaInSameClaim(claim, expandedExisting, expandedArea, chunkArea)
                && !WouldOverlapAnotherClaim(claim, expandedArea, ownerPlayerUid))
            {
                expandedExisting.Set(expandedArea.X1, expandedArea.Y1, expandedArea.Z1, expandedArea.X2, expandedArea.Y2, expandedArea.Z2);
                ConsolidateClaimAreas(claim);
                expandedClaim = claim;
                return true;
            }
        }

        expandedClaim = null!;
        return false;
    }

    /// <summary>Расширяет первую подходящую area в claim и применяет к ней.</summary>
    private static bool TryExpandTouchingArea(LandClaim claim, Cuboidi chunkArea)
    {
        if (!TryExpandTouchingArea(claim, chunkArea, out var expandedArea, out var existing))
        {
            return false;
        }

        existing.Set(expandedArea.X1, expandedArea.Y1, expandedArea.Z1, expandedArea.X2, expandedArea.Y2, expandedArea.Z2);
        return true;
    }

    /// <summary>Ищет area в claim, которую можно расширить до chunkArea по стороне.</summary>
    private static bool TryExpandTouchingArea(LandClaim claim, Cuboidi chunkArea, out Cuboidi expandedArea, out Cuboidi expandedExisting)
    {
        expandedArea = null!;
        expandedExisting = null!;

        if (claim.Areas == null)
        {
            return false;
        }

        foreach (var existing in claim.Areas)
        {
            if (!TryCreateExpandedArea(existing, chunkArea, out var candidate))
            {
                continue;
            }

            expandedArea = candidate;
            expandedExisting = existing;
            return true;
        }

        return false;
    }

    /// <summary>Проверяет смежность по X или Z (одинаковая Y) и строит объединённый Cuboidi.</summary>
    private static bool TryCreateExpandedArea(Cuboidi existing, Cuboidi chunkArea, out Cuboidi expanded)
    {
        expanded = null!;

        if (existing.Y1 != chunkArea.Y1 || existing.Y2 != chunkArea.Y2)
        {
            return false;
        }

        if (existing.Z1 == chunkArea.Z1 && existing.Z2 == chunkArea.Z2)
        {
            if (existing.X2 == chunkArea.X1)
            {
                expanded = new Cuboidi(existing.X1, existing.Y1, existing.Z1, chunkArea.X2, existing.Y2, existing.Z2);
                return true;
            }

            if (chunkArea.X2 == existing.X1)
            {
                expanded = new Cuboidi(chunkArea.X1, existing.Y1, existing.Z1, existing.X2, existing.Y2, existing.Z2);
                return true;
            }
        }

        if (existing.X1 == chunkArea.X1 && existing.X2 == chunkArea.X2)
        {
            if (existing.Z2 == chunkArea.Z1)
            {
                expanded = new Cuboidi(existing.X1, existing.Y1, existing.Z1, existing.X2, existing.Y2, chunkArea.Z2);
                return true;
            }

            if (chunkArea.Z2 == existing.Z1)
            {
                expanded = new Cuboidi(existing.X1, existing.Y1, chunkArea.Z1, existing.X2, existing.Y2, existing.Z2);
                return true;
            }
        }

        return false;
    }

    /// <summary>Уменьшает existing, отрезая removeArea с края (unclaim одного чанка или его части).</summary>
    private static bool TryShrinkArea(Cuboidi existing, Cuboidi removeArea)
    {
        if (existing.Y1 != removeArea.Y1 || existing.Y2 != removeArea.Y2)
        {
            return false;
        }

        if (removeArea.X1 == existing.X1 && removeArea.X2 < existing.X2
            && removeArea.Z1 <= existing.Z1 && removeArea.Z2 >= existing.Z2)
        {
            existing.X1 = removeArea.X2;
            return true;
        }

        if (removeArea.X2 == existing.X2 && removeArea.X1 > existing.X1
            && removeArea.Z1 <= existing.Z1 && removeArea.Z2 >= existing.Z2)
        {
            existing.X2 = removeArea.X1;
            return true;
        }

        if (removeArea.Z1 == existing.Z1 && removeArea.Z2 < existing.Z2
            && removeArea.X1 <= existing.X1 && removeArea.X2 >= existing.X2)
        {
            existing.Z1 = removeArea.Z2;
            return true;
        }

        if (removeArea.Z2 == existing.Z2 && removeArea.Z1 > existing.Z1
            && removeArea.X1 <= existing.X1 && removeArea.X2 >= existing.X2)
        {
            existing.Z2 = removeArea.Z1;
            return true;
        }

        return false;
    }

    /// <summary>Возвращает пересечение двух областей; false, если оно пустое.</summary>
    private static bool TryGetIntersection(Cuboidi first, Cuboidi second, out Cuboidi intersection)
    {
        intersection = null!;
        var x1 = Math.Max(first.X1, second.X1);
        var y1 = Math.Max(first.Y1, second.Y1);
        var z1 = Math.Max(first.Z1, second.Z1);
        var x2 = Math.Min(first.X2, second.X2);
        var y2 = Math.Min(first.Y2, second.Y2);
        var z2 = Math.Min(first.Z2, second.Z2);
        if (x1 >= x2 || y1 >= y2 || z1 >= z2)
        {
            return false;
        }

        intersection = new Cuboidi(x1, y1, z1, x2, y2, z2);
        return true;
    }

    /// <summary>Проверяет пересечение с чужими приватами (свои другого claim — пропуск при merge).</summary>
    private bool WouldOverlapAnotherClaim(LandClaim ownClaim, Cuboidi area, string? ownerPlayerUid = null)
    {
        foreach (var claim in serverApi!.World.Claims.All)
        {
            if (ReferenceEquals(claim, ownClaim))
            {
                continue;
            }

            if (ownerPlayerUid != null && claim.OwnedByPlayerUid == ownerPlayerUid)
            {
                continue;
            }

            if (claim.Intersects(area))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Расширение не должно наехать на несмежную area того же привата.</summary>
    private static bool WouldOverlapAnotherAreaInSameClaim(LandClaim claim, Cuboidi originalArea, Cuboidi expandedArea, Cuboidi chunkArea)
    {
        if (claim.Areas == null)
        {
            return false;
        }

        foreach (var area in claim.Areas)
        {
            if (ReferenceEquals(area, originalArea) || !area.Intersects(expandedArea))
            {
                continue;
            }

            if (AreAdjacent(area, chunkArea) || AreAdjacent(area, originalArea))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>Две области на одной высоте Y соприкасаются по грани X или Z.</summary>
    private static bool AreAdjacent(Cuboidi first, Cuboidi second)
    {
        if (first.Y1 != second.Y1 || first.Y2 != second.Y2)
        {
            return false;
        }

        var touchesX = first.X2 == second.X1 || second.X2 == first.X1;
        var overlapsZ = first.Z1 < second.Z2 && second.Z1 < first.Z2;
        if (touchesX && overlapsZ)
        {
            return true;
        }

        var touchesZ = first.Z2 == second.Z1 || second.Z2 == first.Z1;
        var overlapsX = first.X1 < second.X2 && second.X1 < first.X2;
        return touchesZ && overlapsX;
    }

    /// <summary>Перемещает claim в конец списка World.Claims (обновление порядка/сохранения).</summary>
    private void TouchClaim(LandClaim claim)
    {
        var claims = serverApi!.World.Claims;
        if (claims.Remove(claim))
        {
            claims.Add(claim);
        }

        // Coord-ключ фильтра зависит от minXYZ areas — перепривязываем после expand/merge.
        RebindUseFilterKeys(claim);
    }

    /// <summary>Все LandClaim, принадлежащие игроку по UID.</summary>
    private IEnumerable<LandClaim> GetOwnClaims(string playerUid)
    {
        var claims = serverApi?.World.Claims?.All;
        if (claims == null)
        {
            return [];
        }

        return claims.Where(claim => claim.OwnedByPlayerUid == playerUid);
    }

    /// <summary>Первый приват, пересекающий area; битые claims пропускаются с warning.</summary>
    private LandClaim? FindIntersectingClaim(Cuboidi area)
    {
        var claims = serverApi?.World.Claims?.All;
        if (claims == null)
        {
            return null;
        }

        foreach (var claim in claims)
        {
            try
            {
                if (claim.Intersects(area))
                {
                    return claim;
                }
            }
            catch (Exception exception)
            {
                serverApi?.Logger.Warning("Skipped invalid land claim while building map: {0}", exception.Message);
            }
        }

        return null;
    }

    /// <summary>Индекс в allClaims для BuildCell (быстрее, чем повторный перебор).</summary>
    private int FindIntersectingClaimIndex(Cuboidi area, IReadOnlyList<LandClaim> claims)
    {
        for (var i = 0; i < claims.Count; i++)
        {
            try
            {
                if (claims[i].Intersects(area))
                {
                    return i;
                }
            }
            catch (Exception exception)
            {
                serverApi?.Logger.Warning("Skipped invalid land claim while building map: {0}", exception.Message);
            }
        }

        return -1;
    }

    /// <summary>Cuboidi чанка от y=0 до mapSizeY с учётом границ мира.</summary>
    private bool TryBuildChunkArea(int chunkX, int chunkZ, out Cuboidi area)
    {
        var sapi = serverApi!;
        var chunkSize = sapi.WorldManager.ChunkSize;
        if (chunkSize <= 0)
        {
            area = null!;
            return false;
        }

        long x1 = (long)chunkX * chunkSize;
        long z1 = (long)chunkZ * chunkSize;
        if (x1 < 0 || z1 < 0 || x1 > int.MaxValue || z1 > int.MaxValue)
        {
            area = null!;
            return false;
        }

        var mapSizeX = sapi.WorldManager.MapSizeX;
        var mapSizeZ = sapi.WorldManager.MapSizeZ;
        var mapSizeY = sapi.WorldManager.MapSizeY;
        if (mapSizeX <= 0 || mapSizeZ <= 0 || mapSizeY <= 0)
        {
            area = null!;
            return false;
        }

        var ix1 = (int)x1;
        var iz1 = (int)z1;
        if (ix1 >= mapSizeX || iz1 >= mapSizeZ)
        {
            area = null!;
            return false;
        }

        var x2 = Math.Min(ix1 + chunkSize, mapSizeX);
        var z2 = Math.Min(iz1 + chunkSize, mapSizeZ);
        area = new Cuboidi(ix1, 0, iz1, x2, mapSizeY, z2);
        return true;
    }

}
