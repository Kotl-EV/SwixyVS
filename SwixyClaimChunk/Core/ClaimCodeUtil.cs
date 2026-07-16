using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace SwixyClaimChunk.Core;

/// <summary>Нормализация и сопоставление кодов collectible/block для use-filter.</summary>
public static class ClaimCodeUtil
{
    /// <summary>Единый вид кода: domain:path (через AssetLocation).</summary>
    public static string NormalizeCollectibleCode(string? raw)
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

    public static bool IsMultiblockStubCode(string? code)
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

        return path.StartsWith("multiblock", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whitelist match: exact/prefix, strip orientation/state, FirstCodePart
    /// (metaldoor-solid-iron ↔ metaldoor-barred-iron по first part «metaldoor»).
    /// </summary>
    public static bool IsBlockCodeAllowedByUseFilter(string blockCode, IReadOnlyList<string> allowedCodes)
    {
        blockCode = NormalizeCollectibleCode(blockCode);
        if (string.IsNullOrWhiteSpace(blockCode) || IsMultiblockStubCode(blockCode))
        {
            return false;
        }

        var blockStripped = StripVariantSuffixes(blockCode);
        var blockFirst = GetFirstCodePart(blockCode);

        foreach (var allowedRaw in allowedCodes)
        {
            var allowed = NormalizeCollectibleCode(allowedRaw);
            if (string.IsNullOrWhiteSpace(allowed))
            {
                continue;
            }

            if (CodesLooselyMatch(blockCode, allowed))
            {
                return true;
            }

            var allowedStripped = StripVariantSuffixes(allowed);
            if (CodesLooselyMatch(blockStripped, allowedStripped)
                || CodesLooselyMatch(blockCode, allowedStripped)
                || CodesLooselyMatch(blockStripped, allowed))
            {
                return true;
            }

            var allowedFirst = GetFirstCodePart(allowed);
            if (!string.IsNullOrWhiteSpace(blockFirst)
                && string.Equals(blockFirst, allowedFirst, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>game:metaldoor-solid-iron → game:metaldoor</summary>
    public static string GetFirstCodePart(string code)
    {
        code = NormalizeCollectibleCode(code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return "";
        }

        var colon = code.IndexOf(':');
        var domain = colon >= 0 ? code[..colon] : "game";
        var path = colon >= 0 ? code[(colon + 1)..] : code;
        var dash = path.IndexOf('-');
        var first = dash >= 0 ? path[..dash] : path;
        return string.IsNullOrWhiteSpace(first) ? "" : $"{domain}:{first}";
    }

    public static bool CodesLooselyMatch(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (left.StartsWith(right + "-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (right.StartsWith(left + "-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Срезает trailing-варианты ориентации/состояния: north/east, lit/cold/extinct и т.п.
    /// Не трогает смысловые части вроде normal/oak.
    /// </summary>
    public static string StripVariantSuffixes(string code)
    {
        code = NormalizeCollectibleCode(code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return "";
        }

        var colon = code.IndexOf(':');
        var domain = colon >= 0 ? code[..colon] : "game";
        var path = colon >= 0 ? code[(colon + 1)..] : code;
        if (string.IsNullOrWhiteSpace(path))
        {
            return code;
        }

        var parts = path.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 1)
        {
            return $"{domain}:{path}";
        }

        var end = parts.Length;
        while (end > 1 && IsStrippableVariantPart(parts[end - 1]))
        {
            end--;
        }

        return $"{domain}:{string.Join("-", parts.Take(end))}";
    }

    public static bool IsStrippableVariantPart(string part)
    {
        if (string.IsNullOrWhiteSpace(part))
        {
            return false;
        }

        return part.Equals("north", StringComparison.OrdinalIgnoreCase)
            || part.Equals("south", StringComparison.OrdinalIgnoreCase)
            || part.Equals("east", StringComparison.OrdinalIgnoreCase)
            || part.Equals("west", StringComparison.OrdinalIgnoreCase)
            || part.Equals("up", StringComparison.OrdinalIgnoreCase)
            || part.Equals("down", StringComparison.OrdinalIgnoreCase)
            || part.Equals("n", StringComparison.OrdinalIgnoreCase)
            || part.Equals("s", StringComparison.OrdinalIgnoreCase)
            || part.Equals("e", StringComparison.OrdinalIgnoreCase)
            || part.Equals("w", StringComparison.OrdinalIgnoreCase)
            || part.Equals("ns", StringComparison.OrdinalIgnoreCase)
            || part.Equals("sn", StringComparison.OrdinalIgnoreCase)
            || part.Equals("we", StringComparison.OrdinalIgnoreCase)
            || part.Equals("ew", StringComparison.OrdinalIgnoreCase)
            || part.Equals("ud", StringComparison.OrdinalIgnoreCase)
            || part.Equals("du", StringComparison.OrdinalIgnoreCase)
            || part.Equals("lit", StringComparison.OrdinalIgnoreCase)
            || part.Equals("cold", StringComparison.OrdinalIgnoreCase)
            || part.Equals("extinct", StringComparison.OrdinalIgnoreCase)
            || part.Equals("construct1", StringComparison.OrdinalIgnoreCase)
            || part.Equals("construct2", StringComparison.OrdinalIgnoreCase)
            || part.Equals("construct3", StringComparison.OrdinalIgnoreCase)
            || part.Equals("construct4", StringComparison.OrdinalIgnoreCase)
            || part.Equals("open", StringComparison.OrdinalIgnoreCase)
            || part.Equals("closed", StringComparison.OrdinalIgnoreCase)
            || part.Equals("opened", StringComparison.OrdinalIgnoreCase)
            || part.Equals("empty", StringComparison.OrdinalIgnoreCase)
            || part.Equals("full", StringComparison.OrdinalIgnoreCase)
            || part.Equals("filled", StringComparison.OrdinalIgnoreCase);
    }
}
