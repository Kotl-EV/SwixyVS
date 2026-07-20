using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

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
    /// Whitelist match: exact/prefix, strip, first-part, catalog family key
    /// (storagevessel-abyss ↔ storagevessel-copper по семье «vessel»).
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
        var blockFamily = GetCatalogGroupKey(blockCode);

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

            // Universal family: vessel / door / chest / shelf…
            var allowedFamily = GetCatalogGroupKey(allowed);
            if (!string.IsNullOrWhiteSpace(blockFamily)
                && string.Equals(blockFamily, allowedFamily, StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// Ролевые корни для универсальной группировки плиток Use.
    /// storagevessel-abyss / lootvessel-* → …:vessel; metaldoor-* / door-oak → …:door.
    /// Длинные корни проверяются первыми (bookshelf до shelf, trapdoor до door).
    /// </summary>
    private static readonly string[] CatalogFamilyRoots =
    [
        "bookshelf",
        "displaycase",
        "toolrack",
        "moldrack",
        "torchholder",
        "trapdoor",
        "fencegate",
        "wattlegate",
        "firepit",
        "bloomery",
        "storagevessel",
        "strongbox",
        "groundstorage",
        "vessel",
        "chest",
        "crate",
        "barrel",
        "basket",
        "crock",
        "trough",
        "shelf",
        "door",
        "gate",
        "hatch",
        "oven",
        "forge",
        "quern",
        "anvil",
        "hopper",
        "trunk",
        "cupboard",
        "drawer",
        "furnace",
        "stash",
        "locker",
    ];

    /// <summary>
    /// Ключ семьи для каталога Use — один пункт на все варианты «роли».
    /// Универсально: first-part, затем корень (vessel/door/chest…), если first-part на нём заканчивается
    /// или path содержит -root- / -root.
    /// </summary>
    public static string GetCatalogGroupKey(string? code)
    {
        code = NormalizeCollectibleCode(code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return "";
        }

        if (IsFruitTreeOrBush(code))
        {
            return GetFruitTreeWhitelistCode(code);
        }

        if (NeedsCairoIcon(code))
        {
            return NormalizeCollectibleCode("game:groundstorage");
        }

        if (IsCoalOrCharcoalPile(code))
        {
            var pilePath = GetPath(code);
            if (pilePath.Contains("charcoal", StringComparison.OrdinalIgnoreCase))
            {
                var first = GetFirstCodePart(code);
                return string.IsNullOrWhiteSpace(first)
                    ? NormalizeCollectibleCode("game:charcoalpile")
                    : first;
            }

            return NormalizeCollectibleCode("game:coalpile");
        }

        var pathLower = GetPath(code);
        if (pathLower.Contains("armorstand", StringComparison.OrdinalIgnoreCase)
            || pathLower.Contains("strawdummy", StringComparison.OrdinalIgnoreCase))
        {
            return StripVariantSuffixes(code);
        }

        var semantic = TryGetSemanticFamilyKey(code);
        if (!string.IsNullOrWhiteSpace(semantic))
        {
            return semantic!;
        }

        var family = GetFirstCodePart(code);
        return string.IsNullOrWhiteSpace(family) ? code : family;
    }

    /// <summary>
    /// domain:vessel для storagevessel-*, domain:door для metaldoor-*/door-*, и т.д.
    /// </summary>
    public static string? TryGetSemanticFamilyKey(string? code)
    {
        code = NormalizeCollectibleCode(code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var colon = code.IndexOf(':');
        var domain = colon >= 0 ? code[..colon] : "game";
        var path = colon >= 0 ? code[(colon + 1)..] : code;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var pathLower = path.ToLowerInvariant();
        var first = pathLower;
        var dash = pathLower.IndexOf('-');
        if (dash > 0)
        {
            first = pathLower[..dash];
        }

        foreach (var root in CatalogFamilyRoots)
        {
            // Exact first segment, or compound word ending with root (storagevessel, metaldoor).
            // Min length root+3 avoids "indoor"→door (6 chars), allows "metaldoor" (9).
            if (first.Equals(root, StringComparison.Ordinal)
                || (first.EndsWith(root, StringComparison.Ordinal)
                    && first.Length >= root.Length + 3))
            {
                return $"{domain}:{root}";
            }

            // Segment: vessel-*, *-vessel-*, *-vessel
            if (pathLower.Equals(root, StringComparison.Ordinal)
                || pathLower.StartsWith(root + "-", StringComparison.Ordinal)
                || pathLower.Contains("-" + root + "-", StringComparison.Ordinal)
                || pathLower.EndsWith("-" + root, StringComparison.Ordinal))
            {
                return $"{domain}:{root}";
            }
        }

        return null;
    }

    /// <summary>Два кода относятся к одной плитке каталога Use.</summary>
    public static bool SameCatalogGroup(string? a, string? b)
    {
        var ga = GetCatalogGroupKey(a);
        var gb = GetCatalogGroupKey(b);
        return !string.IsNullOrWhiteSpace(ga)
               && string.Equals(ga, gb, StringComparison.OrdinalIgnoreCase);
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
            // Door halves (legacy irondoor: …-up-closed-left / …-down-closed-left)
            || part.Equals("upper", StringComparison.OrdinalIgnoreCase)
            || part.Equals("lower", StringComparison.OrdinalIgnoreCase)
            || part.Equals("top", StringComparison.OrdinalIgnoreCase)
            || part.Equals("bottom", StringComparison.OrdinalIgnoreCase)
            // Bed parts (bed-wood-head-north / bed-wood-feet-south → bed-wood)
            || part.Equals("head", StringComparison.OrdinalIgnoreCase)
            || part.Equals("feet", StringComparison.OrdinalIgnoreCase)
            || part.Equals("foot", StringComparison.OrdinalIgnoreCase)
            // Door / wattlegate knob side
            || part.Equals("left", StringComparison.OrdinalIgnoreCase)
            || part.Equals("right", StringComparison.OrdinalIgnoreCase)
            // wattlegate-*-free (cover variant) — MUST strip or open/closed stay split
            || part.Equals("free", StringComparison.OrdinalIgnoreCase)
            || part.Equals("snow", StringComparison.OrdinalIgnoreCase)
            // berry bush growth / season states
            || part.Equals("wild", StringComparison.OrdinalIgnoreCase)
            || part.Equals("grown", StringComparison.OrdinalIgnoreCase)
            || part.Equals("empty", StringComparison.OrdinalIgnoreCase)
            || part.Equals("flowering", StringComparison.OrdinalIgnoreCase)
            || part.Equals("ripe", StringComparison.OrdinalIgnoreCase)
            || part.Equals("ripening", StringComparison.OrdinalIgnoreCase)
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

    /// <summary>
    /// Стандартный код для UI/whitelist (как в креативе/инвентаре).
    /// Для multiblock (EP termogen и т.п.) обязательно вариант из creative
    /// (часто *-south) — GuiTransform и origin меша заточены под него.
    /// </summary>
    public static string ResolveStandardDisplayCode(IWorldAccessor world, Block block)
    {
        if (block?.Code == null || world == null)
        {
            return "";
        }

        var worldCode = NormalizeCollectibleCode(block.Code.ToString());
        if (string.IsNullOrWhiteSpace(worldCode) || IsMultiblockStubCode(worldCode))
        {
            return "";
        }

        // 1) CreativeInventoryStacks (attributes + правильный collectible).
        var fromStacks = TryFirstCreativeStackCode(world, block);
        if (!string.IsNullOrWhiteSpace(fromStacks))
        {
            return fromStacks!;
        }

        // 2) Sibling, который реально в creative inventory tabs
        //    (EP: creativeinventory general: ["*-south"]).
        var fromCreativeTab = FindCreativeInventoryVariantCode(world, block);
        if (!string.IsNullOrWhiteSpace(fromCreativeTab))
        {
            return fromCreativeTab!;
        }

        var baseCode = StripVariantSuffixes(worldCode);
        if (string.IsNullOrWhiteSpace(baseCode))
        {
            baseCode = worldCode;
        }

        if (string.Equals(baseCode, worldCode, StringComparison.OrdinalIgnoreCase)
            && BlockExists(world, baseCode))
        {
            return baseCode;
        }

        // 3) VS/EP convention: HorizontalOrientable dropBlockFace = south.
        foreach (var suffix in StandardFacingSuffixes)
        {
            var candidate = baseCode + suffix;
            if (BlockExists(world, candidate))
            {
                return candidate;
            }
        }

        if (BlockExists(world, baseCode))
        {
            return baseCode;
        }

        return baseCode;
    }

    /// <summary>Нормализует уже сохранённый код к стандартному виду (если блок есть в мире).</summary>
    public static string ResolveStandardDisplayCode(IWorldAccessor world, string? rawCode)
    {
        var code = NormalizeCollectibleCode(rawCode);
        if (string.IsNullOrWhiteSpace(code) || world == null)
        {
            return code;
        }

        try
        {
            var block = world.GetBlock(new AssetLocation(code));
            if (block != null && block.Id != 0)
            {
                return ResolveStandardDisplayCode(world, block);
            }
        }
        catch
        {
            // ignore
        }

        var baseCode = StripVariantSuffixes(code);
        if (string.IsNullOrWhiteSpace(baseCode))
        {
            return code;
        }

        foreach (var suffix in StandardFacingSuffixes)
        {
            var candidate = baseCode + suffix;
            if (BlockExists(world, candidate))
            {
                return candidate;
            }
        }

        if (BlockExists(world, baseCode))
        {
            return baseCode;
        }

        return baseCode;
    }

    /// <summary>
    /// south — EP/HorizontalOrientable; up — lanterns (creative stacks only on *-up).
    /// </summary>
    private static readonly string[] StandardFacingSuffixes =
    [
        "-up",
        "-south",
        "-north",
        "-east",
        "-west",
        "-down",
        "-south-closed",
        "-north-closed",
        "-closed",
        "-open",
        "-south-open",
        "-north-open",
    ];

    private static readonly Dictionary<string, string> CreativeVariantCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static bool BlockExists(IWorldAccessor world, string code)
    {
        try
        {
            var block = world.GetBlock(new AssetLocation(code));
            return block != null && block.Id != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ищет вариант той же «семьи» блоков, который лежит в creative tabs
    /// (как у etermogenerator: только *-south).
    /// </summary>
    private static string? FindCreativeInventoryVariantCode(IWorldAccessor world, Block block)
    {
        if (block.Code == null)
        {
            return null;
        }

        var familyKey = GetBlockFamilyKey(block.Code.ToString());
        if (string.IsNullOrWhiteSpace(familyKey))
        {
            return null;
        }

        if (CreativeVariantCache.TryGetValue(familyKey, out var cached))
        {
            return cached;
        }

        // Сам блок уже в креативе — отлично (часто уже south).
        if (block.CreativeInventoryTabs is { Length: > 0 })
        {
            var self = NormalizeCollectibleCode(block.Code.ToString());
            CreativeVariantCache[familyKey] = self;
            return self;
        }

        string? best = null;
        var bestScore = int.MinValue;
        var domain = block.Code.Domain;
        var familyPath = familyKey.Contains(':') ? familyKey[(familyKey.IndexOf(':') + 1)..] : familyKey;

        foreach (var b in world.Blocks)
        {
            if (b?.Code == null || b.Id == 0)
            {
                continue;
            }

            if (!string.Equals(b.Code.Domain, domain, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = b.Code.Path ?? "";
            // etermogenerator-south / etermogenerator-north — общий префикс семьи
            if (!path.StartsWith(familyPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Следующий символ — конец или '-'
            if (path.Length > familyPath.Length
                && path[familyPath.Length] != '-')
            {
                continue;
            }

            // Lanterns: empty creativeinventory, but CreativeInventoryStacks on *-up.
            if (b.CreativeInventoryTabs is not { Length: > 0 }
                && b.CreativeInventoryStacks is not { Length: > 0 })
            {
                continue;
            }

            if (IsMultiblockStubCode(b.Code.ToString()))
            {
                continue;
            }

            var code = NormalizeCollectibleCode(b.Code.ToString());
            var score = ScoreCreativeFacing(code);
            if (score > bestScore)
            {
                bestScore = score;
                best = code;
            }
        }

        if (!string.IsNullOrWhiteSpace(best))
        {
            CreativeVariantCache[familyKey] = best!;
        }

        return best;
    }

    /// <summary>electricalprogressivebasics:etermogenerator-east → …:etermogenerator</summary>
    private static string GetBlockFamilyKey(string? code)
    {
        var stripped = StripVariantSuffixes(code ?? "");
        if (string.IsNullOrWhiteSpace(stripped))
        {
            return "";
        }

        // Ещё раз срезать возможные side-части, если strip оставил material+side — family = first part enough?
        // Для etermogenerator-east → Strip → etermogenerator. OK.
        return stripped;
    }

    private static int ScoreCreativeFacing(string code)
    {
        var lower = code.ToLowerInvariant();
        // EP creative: *-south; GuiTransform calibrated for that.
        if (lower.EndsWith("-south") || lower.Contains("-south-"))
        {
            return 100;
        }

        if (lower.EndsWith("-up") || lower.Contains("-up-"))
        {
            return 80;
        }

        if (lower.EndsWith("-north") || lower.Contains("-north-"))
        {
            return 40;
        }

        if (lower.EndsWith("-east") || lower.Contains("-east-")
            || lower.EndsWith("-west") || lower.Contains("-west-"))
        {
            return 20;
        }

        return 10;
    }

    private static string? TryFirstCreativeStackCode(IWorldAccessor world, Block block)
    {
        var stack = TryGetFamilyCreativeStack(world, block);
        return stack?.Collectible?.Code != null
            ? NormalizeCollectibleCode(stack.Collectible.Code.ToString())
            : null;
    }

    /// <summary>
    /// Creative stack with attributes (lantern material/lining/glass) —
    /// ищет по блоку и по всей «семье» (*-up для фонарей).
    /// </summary>
    public static ItemStack? TryGetFamilyCreativeStack(IWorldAccessor world, Block? block)
    {
        if (world == null || block?.Code == null)
        {
            return null;
        }

        var fromSelf = TryCreativeStacksOn(world, block);
        if (fromSelf != null)
        {
            return fromSelf;
        }

        return TryGetFamilyCreativeStack(world, block.Code.ToString());
    }

    public static ItemStack? TryGetFamilyCreativeStack(IWorldAccessor world, string? code)
    {
        if (world == null || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        try
        {
            var block = world.GetBlock(new AssetLocation(code));
            if (block != null)
            {
                var fromSelf = TryCreativeStacksOn(world, block);
                if (fromSelf != null)
                {
                    return fromSelf;
                }
            }
        }
        catch
        {
            // ignore
        }

        var family = GetBlockFamilyKey(code);
        if (string.IsNullOrWhiteSpace(family))
        {
            return null;
        }

        var domain = "game";
        var familyPath = family;
        var colon = family.IndexOf(':');
        if (colon >= 0)
        {
            domain = family[..colon];
            familyPath = family[(colon + 1)..];
        }

        // Prefer *-up (lanterns), then *-south (EP).
        Block? up = null;
        Block? south = null;
        Block? anyWithStacks = null;

        foreach (var b in world.Blocks)
        {
            if (b?.Code == null || b.Id == 0)
            {
                continue;
            }

            if (!string.Equals(b.Code.Domain, domain, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = b.Code.Path ?? "";
            if (!path.StartsWith(familyPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (path.Length > familyPath.Length && path[familyPath.Length] != '-')
            {
                continue;
            }

            if (b.CreativeInventoryStacks is not { Length: > 0 })
            {
                continue;
            }

            anyWithStacks ??= b;
            var lower = path.ToLowerInvariant();
            if (lower.EndsWith("-up") || lower.Contains("-up-"))
            {
                up = b;
            }
            else if (lower.EndsWith("-south") || lower.Contains("-south-"))
            {
                south = b;
            }
        }

        var pick = up ?? south ?? anyWithStacks;
        return pick != null ? TryCreativeStacksOn(world, pick) : null;
    }

    private static ItemStack? TryCreativeStacksOn(IWorldAccessor world, Block block)
    {
        if (block.CreativeInventoryStacks is not { Length: > 0 })
        {
            return null;
        }

        foreach (var tab in block.CreativeInventoryStacks)
        {
            if (tab?.Stacks == null)
            {
                continue;
            }

            foreach (var js in tab.Stacks)
            {
                if (js == null)
                {
                    continue;
                }

                try
                {
                    if (js.ResolvedItemstack == null)
                    {
                        js.Resolve(world, "swixyclaimchunk creative-stack", block.Code);
                    }

                    var stack = js.ResolvedItemstack;
                    if (stack?.Collectible == null)
                    {
                        continue;
                    }

                    var n = NormalizeCollectibleCode(stack.Collectible.Code?.ToString());
                    if (string.IsNullOrWhiteSpace(n) || IsMultiblockStubCode(n))
                    {
                        continue;
                    }

                    var clone = stack.Clone();
                    clone.StackSize = 1;
                    return clone;
                }
                catch
                {
                    // next
                }
            }
        }

        return null;
    }

    /// <summary>Террейн/растения по коду (для клиентского фильтра каталога).</summary>
    public static bool IsTerrainLikeCode(string? code)
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

        if (first is "soil" or "dirt" or "mud" or "gravel" or "sand" or "rock" or "stone"
            or "cobblestone" or "cobble" or "ore" or "mineral" or "water" or "lava" or "ice"
            or "snow" or "air" or "mantle" or "rawclay" or "clay" or "peat" or "forestfloor"
            or "tallgrass" or "leaves" or "leaf" or "log" or "planks" or "caveart" or "fern" or "ferns"
            or "flower" or "crop" or "sapling" or "looseores" or "loosestones" or "looseboulders"
            or "looseflints" or "crushed" or "stalagmite" or "stalactite" or "geode"
            or "bonyremains" or "bones" or "farmland" or "layeredrock" or "rockpolished"
            or "grass" or "drygrass" or "smallberrybush" or "bigberrybush" or "bamboo"
            or "reeds" or "papyrus" or "waterlily" or "seaweed" or "coral" or "mushroom"
            or "chiseledblock" or "chiseled" or "microblock" or "gabion" or "gabbion"
            or "cobblestone" or "cobble" or "packeddirt" or "path"
            or "chute") // item chute / жёлоб — нет смысла в Use whitelist
        {
            return true;
        }

        if (path.StartsWith("soil-", StringComparison.Ordinal)
            || path.StartsWith("rock-", StringComparison.Ordinal)
            || path.StartsWith("ore-", StringComparison.Ordinal)
            || path.StartsWith("tallgrass", StringComparison.Ordinal)
            || path.StartsWith("leaves", StringComparison.Ordinal)
            || path.StartsWith("leaf-", StringComparison.Ordinal)
            || path.StartsWith("log-", StringComparison.Ordinal)
            || path.StartsWith("crop-", StringComparison.Ordinal)
            || path.StartsWith("sapling-", StringComparison.Ordinal)
            || path.StartsWith("flower-", StringComparison.Ordinal)
            || path.StartsWith("plant-", StringComparison.Ordinal)
            || path.StartsWith("fern", StringComparison.Ordinal)
            || path.StartsWith("farmland", StringComparison.Ordinal)
            || path.StartsWith("grass", StringComparison.Ordinal)
            || path.StartsWith("chiseled", StringComparison.Ordinal)
            || path.StartsWith("microblock", StringComparison.Ordinal)
            || path.StartsWith("gabion", StringComparison.Ordinal)
            || path.StartsWith("gabbion", StringComparison.Ordinal)
            || path.StartsWith("cobble", StringComparison.Ordinal)
            || path.StartsWith("chute", StringComparison.Ordinal)
            || path.Contains("tallgrass", StringComparison.Ordinal)
            || path.Contains("farmland", StringComparison.Ordinal)
            || path.Contains("chiseled", StringComparison.Ordinal)
            || path.Contains("microblock", StringComparison.Ordinal)
            || path.Contains("gabion", StringComparison.Ordinal)
            || path.Contains("chute", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>Фруктовое дерево / ягодный куст (сбор = Use / Harvestable).</summary>
    public static bool IsFruitTreeOrBush(string? code)
    {
        var path = GetPath(code);
        return path.StartsWith("fruittree", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("fruitingbush", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("fruitingbushcutting", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("smallberrybush", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("bigberrybush", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Кандидат публичного Use-фильтра: двери/калитки, блоки с инвентарём,
    /// держатели факелов. Не переносные фонари, не террейн.
    /// </summary>
    public static bool IsUseFilterCatalogCandidate(IWorldAccessor? world, Block? block, BlockPos? pos)
    {
        if (block?.Code == null || block.Id == 0)
        {
            return false;
        }

        var code = NormalizeCollectibleCode(block.Code.ToString());
        if (string.IsNullOrWhiteSpace(code) || IsMultiblockStubCode(code) || IsTerrainLikeCode(code))
        {
            return false;
        }

        // Torch holders first — not in the light denylist.
        if (IsTorchHolderCode(code) || IsTorchHolderBlock(block))
        {
            return true;
        }

        // Explicit junk for public Use tiles.
        if (IsUseFilterCatalogExcluded(code))
        {
            return false;
        }

        if (IsDoorOrGateCode(code) || IsDoorOrGateBlock(block))
        {
            return true;
        }

        if (IsInventoryContainerBlock(world, block, pos))
        {
            return true;
        }

        return false;
    }

    /// <summary>Держатель факела (wall torch mount) — публичный Use: повесить/снять.</summary>
    public static bool IsTorchHolderCode(string? code)
    {
        var path = GetPath(code).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.Contains("torchholder", StringComparison.Ordinal)
               || path.Contains("torch-holder", StringComparison.Ordinal)
               || path.Contains("torch_holder", StringComparison.Ordinal)
               || path.Equals("torchrack", StringComparison.Ordinal)
               || path.StartsWith("torchrack-", StringComparison.Ordinal);
    }

    public static bool IsTorchHolderBlock(Block? block)
    {
        if (block == null)
        {
            return false;
        }

        if (IsTorchHolderCode(block.Code?.ToString()))
        {
            return true;
        }

        var typeName = block.GetType().Name ?? "";
        if (typeName.Contains("TorchHolder", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("TorchMount", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ec = block.EntityClass ?? "";
        if (ec.Contains("TorchHolder", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var behaviors = block.BlockBehaviors;
        if (behaviors != null)
        {
            foreach (var bh in behaviors)
            {
                var n = bh?.GetType().Name ?? "";
                if (n.Contains("TorchHolder", StringComparison.OrdinalIgnoreCase)
                    || n.Contains("HorizontalAttachable", StringComparison.OrdinalIgnoreCase)
                       && IsTorchHolderCode(block.Code?.ToString()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Дверь / калитка / люк / wattle gate.</summary>
    public static bool IsDoorOrGateCode(string? code)
    {
        var path = GetPath(code).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Avoid "outdoor" false positives: require segment boundaries.
        return path.Contains("door", StringComparison.Ordinal)
               || path.Contains("trapdoor", StringComparison.Ordinal)
               || path.Contains("trap-door", StringComparison.Ordinal)
               || path.Contains("fencegate", StringComparison.Ordinal)
               || path.Contains("fence-gate", StringComparison.Ordinal)
               || path.Contains("wattlegate", StringComparison.Ordinal)
               || path.Contains("wattle-gate", StringComparison.Ordinal)
               || path.Contains("gate-", StringComparison.Ordinal)
               || path.EndsWith("gate", StringComparison.Ordinal)
               || path.StartsWith("gate", StringComparison.Ordinal)
               || path.Contains("hatch", StringComparison.Ordinal);
    }

    public static bool IsDoorOrGateBlock(Block block)
    {
        if (block == null)
        {
            return false;
        }

        if (IsDoorOrGateCode(block.Code?.ToString()))
        {
            return true;
        }

        var typeName = block.GetType().Name;
        if (typeName.Contains("Door", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("Gate", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("TrapDoor", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("Trapdoor", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ec = block.EntityClass ?? "";
        if (ec.Contains("Door", StringComparison.OrdinalIgnoreCase)
            || ec.Contains("Gate", StringComparison.OrdinalIgnoreCase)
            || ec.Contains("TrapDoor", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var behaviors = block.BlockBehaviors;
        if (behaviors == null)
        {
            return false;
        }

        foreach (var bh in behaviors)
        {
            var n = bh?.GetType().Name ?? "";
            if (n.Contains("Door", StringComparison.OrdinalIgnoreCase)
                || n.Contains("TrapDoor", StringComparison.OrdinalIgnoreCase)
                || n.Contains("Trapdoor", StringComparison.OrdinalIgnoreCase)
                || n.Contains("Gate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Блок с инвентарём (IBlockEntityContainer / container BE / path+behavior).
    /// </summary>
    public static bool IsInventoryContainerBlock(IWorldAccessor? world, Block block, BlockPos? pos)
    {
        if (block == null)
        {
            return false;
        }

        // Live BE with inventory (chests, shelves, firepits, groundstorage…).
        if (world != null && pos != null)
        {
            try
            {
                var be = world.BlockAccessor.GetBlockEntity(pos);
                if (be is IBlockEntityContainer)
                {
                    return true;
                }

                var beName = be?.GetType().Name ?? "";
                if (IsInventoryTypeName(beName))
                {
                    return true;
                }
            }
            catch
            {
                // fall through
            }
        }

        var ec = block.EntityClass ?? "";
        if (IsInventoryTypeName(ec) || IsInventoryTypeName(block.GetType().Name))
        {
            return true;
        }

        var behaviors = block.BlockBehaviors;
        if (behaviors != null)
        {
            foreach (var bh in behaviors)
            {
                var n = bh?.GetType().Name ?? "";
                if (IsInventoryTypeName(n))
                {
                    return true;
                }
            }
        }

        return IsInventoryPathCode(block.Code?.ToString());
    }

    public static bool IsInventoryPathCode(string? code)
    {
        var path = GetPath(code).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Containers / shelves / work stations with slots.
        return path.Contains("chest", StringComparison.Ordinal)
               || path.Contains("crate", StringComparison.Ordinal)
               || path.Contains("barrel", StringComparison.Ordinal)
               || path.Contains("bookshelf", StringComparison.Ordinal)
               || path.Contains("shelf", StringComparison.Ordinal)
               || path.Contains("displaycase", StringComparison.Ordinal)
               || path.Contains("display-case", StringComparison.Ordinal)
               || path.Contains("toolrack", StringComparison.Ordinal)
               || path.Contains("tool-rack", StringComparison.Ordinal)
               || path.Contains("moldrack", StringComparison.Ordinal)
               || path.Contains("mold-rack", StringComparison.Ordinal)
               || path.Contains("firepit", StringComparison.Ordinal)
               || path.Contains("forge", StringComparison.Ordinal)
               || path.Contains("oven", StringComparison.Ordinal)
               || path.Contains("bloomery", StringComparison.Ordinal)
               || path.Contains("furnace", StringComparison.Ordinal)
               || path.Contains("basket", StringComparison.Ordinal)
               || path.Contains("vessel", StringComparison.Ordinal)
               || path.Contains("crock", StringComparison.Ordinal)
               || path.Contains("trunk", StringComparison.Ordinal)
               || path.Contains("cupboard", StringComparison.Ordinal)
               || path.Contains("drawer", StringComparison.Ordinal)
               || path.Contains("hopper", StringComparison.Ordinal)
               || path.Contains("trough", StringComparison.Ordinal)
               || path.Contains("quern", StringComparison.Ordinal)
               || path.Contains("anvil", StringComparison.Ordinal)
               || path.Contains("strongbox", StringComparison.Ordinal)
               || path.Contains("storagevessel", StringComparison.Ordinal)
               || path.Contains("groundstorage", StringComparison.Ordinal)
               || path.Contains("stash", StringComparison.Ordinal)
               || path.Contains("locker", StringComparison.Ordinal)
               || path.Contains("wardrobe", StringComparison.Ordinal)
               || path.Contains("fridge", StringComparison.Ordinal)
               || path.Contains("refrigerator", StringComparison.Ordinal)
               || path.Contains("inventory", StringComparison.Ordinal)
               || path.Contains("container", StringComparison.Ordinal);
    }

    private static bool IsInventoryTypeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Contains("Container", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Chest", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Crate", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Barrel", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Shelf", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Bookshelf", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Inventory", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Openable", StringComparison.OrdinalIgnoreCase)
               || name.Contains("GenericTyped", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Firepit", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Forge", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Oven", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Bloomery", StringComparison.OrdinalIgnoreCase)
               || name.Contains("DisplayCase", StringComparison.OrdinalIgnoreCase)
               || name.Contains("ToolRack", StringComparison.OrdinalIgnoreCase)
               || name.Contains("MoldRack", StringComparison.OrdinalIgnoreCase)
               || name.Contains("GroundStorage", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Trough", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Quern", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Anvil", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Hopper", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Crock", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Basket", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Vessel", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Cooking", StringComparison.OrdinalIgnoreCase)
               || name.Contains("MealContainer", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Не в плитки Use: фонари/факелы (не holder), стройматериалы.</summary>
    public static bool IsUseFilterCatalogExcluded(string? code)
    {
        var path = GetPath(code).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        // Torch holders are allowed (see IsTorchHolderCode) — exclude only free lights.
        if (IsTorchHolderCode(code))
        {
            return false;
        }

        // Lanterns / loose torches — not Use-furniture tiles.
        if (path.Contains("lantern", StringComparison.Ordinal)
            || path.Equals("torch", StringComparison.Ordinal)
            || path.StartsWith("torch-", StringComparison.Ordinal)
            || path.Contains("chandelier", StringComparison.Ordinal)
            || path.Contains("candle", StringComparison.Ordinal)
            || path.Contains("lamp", StringComparison.Ordinal))
        {
            return true;
        }

        // Building / harvest specials we no longer put in public Use tiles.
        if (IsFruitTreeOrBush(code)
            || IsCoalOrCharcoalPile(code)
            || path.Contains("fence", StringComparison.Ordinal) && !path.Contains("gate", StringComparison.Ordinal)
            || path.Contains("plank", StringComparison.Ordinal)
            || path.Contains("log-", StringComparison.Ordinal)
            || path.StartsWith("log", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>Код для whitelist: все части/стадии → один ключ семьи.</summary>
    public static string GetFruitTreeWhitelistCode(string? code)
    {
        var path = GetPath(code);
        if (path.StartsWith("fruitingbush", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("smallberrybush", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("bigberrybush", StringComparison.OrdinalIgnoreCase))
        {
            // Один пункт «ягодный куст» на все типы (blueberry/currant/…).
            return "game:fruitingbush";
        }

        if (path.StartsWith("fruittree", StringComparison.OrdinalIgnoreCase))
        {
            // Harvest hits foliage; first-part match still covers branch/stem.
            return "game:fruittree-foliage";
        }

        return NormalizeCollectibleCode(code);
    }

    /// <summary>Нужна Cairo-иконка вместо 3D (невидимый shape: groundstorage).</summary>
    public static bool NeedsCairoIcon(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var path = GetPath(code);
        return path.Equals("groundstorage", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("groundstorage-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>coalpile / charcoalpile — динамический mesh, в GUI часто «Unknown».</summary>
    public static bool IsCoalOrCharcoalPile(string? code)
    {
        var path = GetPath(code);
        return path.Equals("coalpile", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("coalpile-", StringComparison.OrdinalIgnoreCase)
               || path.Equals("charcoalpile", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("charcoalpile-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Читаемое имя: stack.GetName(), иначе lang block-*, с fallback.
    /// Для семьи (game:vessel) — общее имя, не Unknown.
    /// </summary>
    public static string GetFriendlyBlockLabel(string? code, ItemStack? stack = null)
    {
        // Prefer semantic family name in Use catalog (all vessels → «Сосуд»).
        var familyKey = GetCatalogGroupKey(code);
        var familyRootLabel = TryGetFamilyRootLabel(GetPath(familyKey));
        if (familyRootLabel != null)
        {
            return familyRootLabel;
        }

        if (stack?.Collectible != null)
        {
            try
            {
                var n = stack.GetName();
                if (!IsUnknownLabel(n))
                {
                    return n;
                }
            }
            catch
            {
                // fall through
            }
        }

        var path = GetPath(code);
        if (string.IsNullOrWhiteSpace(path))
        {
            return code ?? "?";
        }

        // coalpile / charcoalpile-*
        if (path.Equals("coalpile", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("coalpile-", StringComparison.OrdinalIgnoreCase))
        {
            var coal = Vintagestory.API.Config.Lang.Get("block-coalpile");
            if (!IsUnknownLabel(coal) && !string.Equals(coal, "block-coalpile", StringComparison.Ordinal))
            {
                return coal;
            }

            return Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-coalpile");
        }

        if (path.Equals("charcoalpile", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("charcoalpile-", StringComparison.OrdinalIgnoreCase))
        {
            // Prefer full-pile name if amount stripped, or specific layer.
            var key = "block-" + path.Replace('/', '-');
            var layer = Vintagestory.API.Config.Lang.Get(key);
            if (!IsUnknownLabel(layer) && !string.Equals(layer, key, StringComparison.Ordinal))
            {
                return layer;
            }

            var full = Vintagestory.API.Config.Lang.Get("block-charcoalpile-8");
            if (!IsUnknownLabel(full) && !string.Equals(full, "block-charcoalpile-8", StringComparison.Ordinal))
            {
                return full;
            }

            return Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-charcoalpile");
        }

        if (NeedsCairoIcon(code))
        {
            return Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-groundstorage");
        }

        if (IsFruitTreeOrBush(code))
        {
            if (path.StartsWith("fruitingbush", StringComparison.OrdinalIgnoreCase))
            {
                return Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-fruitingbush");
            }

            return Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-fruittree");
        }

        if (path.Contains("armorstand", StringComparison.OrdinalIgnoreCase)
            || path.Contains("strawdummy", StringComparison.OrdinalIgnoreCase))
        {
            // item-armorstand / item-armorstand-aged
            var itemKey = "item-" + path.Replace('/', '-');
            var itemLang = Vintagestory.API.Config.Lang.Get(itemKey);
            if (!IsUnknownLabel(itemLang) && !string.Equals(itemLang, itemKey, StringComparison.Ordinal))
            {
                return itemLang;
            }

            return Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-armorstand");
        }

        var langKey = "block-" + path.Replace('/', '-');
        var lang = Vintagestory.API.Config.Lang.Get(langKey);
        if (!IsUnknownLabel(lang) && !string.Equals(lang, langKey, StringComparison.Ordinal))
        {
            return lang;
        }

        // items (armor stand etc.)
        var itemKey2 = "item-" + path.Replace('/', '-');
        var itemLang2 = Vintagestory.API.Config.Lang.Get(itemKey2);
        if (!IsUnknownLabel(itemLang2) && !string.Equals(itemLang2, itemKey2, StringComparison.Ordinal))
        {
            return itemLang2;
        }

        return code ?? path;
    }

    /// <summary>Короткое имя для semantic family key (path = vessel / door / …).</summary>
    private static string? TryGetFamilyRootLabel(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('-'))
        {
            // Full block codes keep normal lang; only pure roots like "vessel".
            var pure = path;
            var dash = path.IndexOf('-');
            if (dash >= 0)
            {
                return null;
            }

            pure = path.ToLowerInvariant();
            foreach (var root in CatalogFamilyRoots)
            {
                if (!pure.Equals(root, StringComparison.Ordinal))
                {
                    continue;
                }

                // Prefer vanilla block lang for a common member, then generic.
                var tryKeys = root switch
                {
                    "vessel" => new[] { "block-storagevessel-earthen", "block-storagevessel", "storagevessel" },
                    "door" => new[] { "block-door-solid-north", "block-door-solid", "door" },
                    "chest" => new[] { "block-chest-east", "block-chest", "chest" },
                    "shelf" => new[] { "block-shelf-north", "block-shelf", "shelf" },
                    "bookshelf" => new[] { "block-bookshelf", "bookshelf" },
                    "barrel" => new[] { "block-barrel", "barrel" },
                    "crate" => new[] { "block-crate", "crate" },
                    "firepit" => new[] { "block-firepit-lit-cold", "block-firepit", "firepit" },
                    "gate" => new[] { "block-fencegate-closed-n", "block-fencegate", "gate" },
                    "torchholder" => new[] { "block-torchholder-empty-up", "block-torchholder", "torchholder" },
                    _ => new[] { "block-" + root, root },
                };

                foreach (var key in tryKeys)
                {
                    var lang = Vintagestory.API.Config.Lang.Get(key);
                    if (!IsUnknownLabel(lang) && !string.Equals(lang, key, StringComparison.Ordinal))
                    {
                        // Strip orientation noise from sample names if any.
                        return lang;
                    }
                }

                return root switch
                {
                    "vessel" => Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-family-vessel"),
                    "door" => Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-family-door"),
                    "chest" => Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-family-chest"),
                    "shelf" => Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-family-shelf"),
                    "gate" => Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-family-gate"),
                    "torchholder" => Vintagestory.API.Config.Lang.Get("swixyclaimchunk:use-filter-family-torchholder"),
                    _ => char.ToUpperInvariant(root[0]) + root[1..],
                };
            }
        }

        return null;
    }

    public static bool IsUnknownLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return true;
        }

        return label.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
               || label.Equals("Unknown Block", StringComparison.OrdinalIgnoreCase)
               || label.Equals("unknown block", StringComparison.OrdinalIgnoreCase)
               || label.Contains("Unknown Block", StringComparison.OrdinalIgnoreCase)
               || label.StartsWith("Unknown ", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPath(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "";
        }

        var colon = code.IndexOf(':');
        return colon >= 0 && colon + 1 < code.Length ? code[(colon + 1)..] : code;
    }
}
