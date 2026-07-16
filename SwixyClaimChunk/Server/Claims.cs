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

/// <summary>Часть <see cref="SwixyClaimChunkServerMod"/> — сервер: клейм и слияние.</summary>
public sealed partial class SwixyClaimChunkServerMod
{
    /// <summary>Переключает клейм чанка: добавить или снять с собственного привата.</summary>
    private ClaimActionResult ToggleChunkClaim(IServerPlayer player, int chunkX, int chunkZ)
    {
        var envError = ValidateClaimEnvironment(player);
        if (envError != null)
        {
            return envError.Value;
        }

        if (!TryBuildChunkArea(chunkX, chunkZ, out var area))
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-out-of-world");
        }

        var existing = FindIntersectingClaim(area);
        if (existing != null)
        {
            var adminUnclaim = CanAdminUnclaimOthers(player);
            if (!CanUnclaimFromClaim(existing, player.PlayerUID, adminUnclaim))
            {
                return ClaimActionResult.Error("swixyclaimchunk:error-owned-by-other", ResolveClaimOwnerName(existing));
            }

            if (adminUnclaim && !CanManageClaim(existing, player.PlayerUID))
            {
                serverApi!.Logger.Notification(
                    "[SwixyClaimChunk] Admin {0} unclaimed chunk {1},{2} from claim '{3}'",
                    player.PlayerName,
                    chunkX,
                    chunkZ,
                    existing.Description);
            }

            return TryRemoveChunkFromClaim(existing, area);
        }

        return TryAddChunkClaim(player, area);
    }

    /// <summary>
    /// Добавляет область: расширение соседнего, новая area в соседнем привате или новый LandClaim.
    /// Проверяет квоты volume/areas; после — MergeTouchingOwnClaims.
    /// </summary>
    private ClaimActionResult TryAddChunkClaim(IServerPlayer player, Cuboidi area)
    {
        var ownClaims = GetOwnClaims(player.PlayerUID).ToList();
        var allowanceError = ValidateLandClaimAllowance(player, area.SizeXYZ);
        if (allowanceError != null)
        {
            return allowanceError.Value;
        }

        if (TryExpandExistingArea(ownClaims, area, out var expandedClaim, player.PlayerUID))
        {
            MergeTouchingOwnClaims(player, expandedClaim);
            TouchClaim(expandedClaim);
            return ClaimActionResult.Success("swixyclaimchunk:message-claimed");
        }

        var usedAreas = ownClaims.Sum(static claim => claim.Areas?.Count ?? 0);
        var maxAreas = GetLandClaimMaxAreas(player);
        var adjacentClaim = FindAdjacentOwnClaim(ownClaims, area);
        if (adjacentClaim != null)
        {
            if (TryExpandTouchingArea(adjacentClaim, area))
            {
                ConsolidateClaimAreas(adjacentClaim);
                MergeTouchingOwnClaims(player, adjacentClaim);
                TouchClaim(adjacentClaim);
                return ClaimActionResult.Success("swixyclaimchunk:message-claimed");
            }

            if (maxAreas > 0 && usedAreas + 1 > maxAreas)
            {
                return ClaimActionResult.Error("swixyclaimchunk:error-areas");
            }

            var addAdjacentError = adjacentClaim.AddArea(area);
            if (addAdjacentError != EnumClaimError.NoError)
            {
                return ClaimActionResult.Error(ClaimErrorKey(addAdjacentError));
            }

            ConsolidateClaimAreas(adjacentClaim);
            MergeTouchingOwnClaims(player, adjacentClaim);
            TouchClaim(adjacentClaim);
            return ClaimActionResult.Success("swixyclaimchunk:message-claimed");
        }

        if (maxAreas > 0 && usedAreas + 1 > maxAreas)
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-areas");
        }

        // Новый отдельный приват с именем «Claim {ник} {индекс}»
        var claimIndex = GetNextClaimIndex(player, ownClaims);
        var newClaim = LandClaim.CreateClaim(player, ClaimConstants.ProtectionLevel);
        newClaim.Description = BuildClaimName(player, claimIndex);
        var addError = newClaim.AddArea(area);
        if (addError != EnumClaimError.NoError)
        {
            return ClaimActionResult.Error(ClaimErrorKey(addError));
        }

        serverApi!.World.Claims.Add(newClaim);
        MergeTouchingOwnClaims(player, newClaim);
        serverApi.Logger.Notification(
            "[SwixyClaimChunk] Added land claim '{0}' for {1} area={2},{3},{4} to {5},{6},{7}",
            newClaim.Description,
            player.PlayerName,
            area.X1, area.Y1, area.Z1,
            area.X2, area.Y2, area.Z2);
        return ClaimActionResult.Success("swixyclaimchunk:message-claimed");
    }

    /// <summary>Ищет свой приват с областью, смежной с chunkArea.</summary>
    private static LandClaim? FindAdjacentOwnClaim(IEnumerable<LandClaim> ownClaims, Cuboidi area)
    {
        foreach (var claim in ownClaims)
        {
            if (claim.Areas == null)
            {
                continue;
            }

            foreach (var existing in claim.Areas)
            {
                if (AreAdjacent(existing, area))
                {
                    return claim;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Пока есть соприкасающиеся приваты того же игрока — поглощает их в anchorClaim.
    /// </summary>
    private void MergeTouchingOwnClaims(IServerPlayer player, LandClaim anchorClaim)
    {
        if (anchorClaim.Areas == null || anchorClaim.Areas.Count == 0)
        {
            return;
        }

        var mergedAny = true;
        while (mergedAny)
        {
            mergedAny = false;
            foreach (var otherClaim in GetOwnClaims(player.PlayerUID).ToList())
            {
                if (ReferenceEquals(otherClaim, anchorClaim) || !ClaimsTouch(anchorClaim, otherClaim))
                {
                    continue;
                }

                AbsorbClaimInto(player, anchorClaim, otherClaim);
                mergedAny = true;
            }
        }

        ConsolidateClaimAreas(anchorClaim);
    }

    /// <summary>
    /// Переносит области other в primary, удаляет other; сохраняет меньший индекс в имени.
    /// </summary>
    private void AbsorbClaimInto(IServerPlayer player, LandClaim primary, LandClaim other)
    {
        if (other.Areas == null || primary.Areas == null)
        {
            return;
        }

        // При слиянии оставляем имя с меньшим номером (Claim Player 1 поглощает Claim Player 3)
        var primaryIndex = TryParseClaimIndex(primary.Description, player.PlayerName);
        var otherIndex = TryParseClaimIndex(other.Description, player.PlayerName);
        if (otherIndex > 0 && (primaryIndex == 0 || otherIndex < primaryIndex))
        {
            primary.Description = BuildClaimName(player, otherIndex);
        }

        foreach (var otherArea in other.Areas.ToList())
        {
            if (TryExpandExistingArea(new[] { primary }, otherArea, out _, player.PlayerUID)
                || TryExpandTouchingArea(primary, otherArea))
            {
                continue;
            }

            if (primary.Areas.Any(existing => existing.Equals(otherArea) || existing.Intersects(otherArea)))
            {
                continue;
            }

            primary.AddArea(otherArea);
        }

        serverApi!.World.Claims.Remove(other);
        MergeCoOwners(primary, other);
        MergeUseFilters(primary, other);
        serverApi.Logger.Notification(
            "[SwixyClaimChunk] Merged claim '{0}' into '{1}' for {2}",
            other.Description,
            primary.Description,
            player.PlayerName);
    }

    /// <summary>Объединяет смежные Areas внутри одного LandClaim в один Cuboidi.</summary>
    private void ConsolidateClaimAreas(LandClaim claim)
    {
        if (claim.Areas == null || claim.Areas.Count <= 1)
        {
            return;
        }

        var mergedAny = true;
        while (mergedAny)
        {
            mergedAny = false;
            for (var i = 0; i < claim.Areas.Count; i++)
            {
                for (var j = i + 1; j < claim.Areas.Count; j++)
                {
                    var first = claim.Areas[i];
                    var second = claim.Areas[j];
                    if (!TryCreateExpandedArea(first, second, out var expandedArea)
                        && !TryCreateExpandedArea(second, first, out expandedArea))
                    {
                        continue;
                    }

                    first.Set(expandedArea.X1, expandedArea.Y1, expandedArea.Z1, expandedArea.X2, expandedArea.Y2, expandedArea.Z2);
                    claim.Areas.RemoveAt(j);
                    mergedAny = true;
                    break;
                }

                if (mergedAny)
                {
                    break;
                }
            }
        }
    }

    /// <summary>True, если у двух приватов есть пересекающиеся или смежные области.</summary>
    private static bool ClaimsTouch(LandClaim first, LandClaim second)
    {
        if (first.Areas == null || second.Areas == null)
        {
            return false;
        }

        foreach (var firstArea in first.Areas)
        {
            foreach (var secondArea in second.Areas)
            {
                if (firstArea.Intersects(secondArea) || AreAdjacent(firstArea, secondArea))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Шаблон имени по умолчанию: «Claim {ник} {индекс}».</summary>
    private static string BuildClaimName(IServerPlayer player, int index)
    {
        return $"Claim {player.PlayerName} {index}";
    }

    /// <summary>Следующий свободный индекс по максимуму среди существующих имён игрока.</summary>
    private static int GetNextClaimIndex(IServerPlayer player, IEnumerable<LandClaim> ownClaims)
    {
        var maxIndex = 0;
        foreach (var claim in ownClaims)
        {
            maxIndex = Math.Max(maxIndex, TryParseClaimIndex(claim.Description, player.PlayerName));
        }

        return maxIndex + 1;
    }

    /// <summary>
    /// Извлекает числовой индекс из Description; поддерживает старый формат «{ник} N».
    /// </summary>
    private static int TryParseClaimIndex(string? description, string playerName)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return 0;
        }

        // Новый префикс «Claim {ник} » и legacy «{ник} »
        foreach (var prefix in new[] { $"Claim {playerName} ", $"{playerName} " })
        {
            if (!description.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (int.TryParse(description.AsSpan(prefix.Length), out var index))
            {
                return index;
            }
        }

        return 0;
    }

}
