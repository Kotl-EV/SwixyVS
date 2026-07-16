using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace SwixyClaimChunk.Core;

/// <summary>Ключи хранения метаданных привата (co-owners, use-filters).</summary>
public static class ClaimStorageKeys
{
    /// <summary>Стабильный ключ привата для хранения между сохранениями.</summary>
    public static string BuildClaimStorageKey(LandClaim claim)
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

    /// <summary>
    /// Несколько ключей на один приват: координаты (как co-owners) + имя.
    /// Lookup срабатывает, если совпал любой.
    /// </summary>
    public static IEnumerable<string> EnumerateClaimStorageKeys(LandClaim claim)
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
}
