// =============================================================================
// Client Use-filter picker catalog: scan a small cube around the player.
// Not authoritative — only UI convenience. Save still goes to server.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SwixyClaimChunk.Core;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Лёгкий клиентский скан: куб вокруг игрока (по умолчанию ±10 блоков).
/// </summary>
public sealed class ClaimUseFilterClientScanner
{
    public const int DefaultRadius = 10;
    private const int BudgetMs = 3;

    private readonly ICoreClientAPI api;

    private int jobClaimId;
    private int radius;
    private int x0, y0, z0, x1, y1, z1;
    private int curX, curY, curZ;
    private int scanned;
    private readonly HashSet<int> seenIds = [];
    /// <summary>groupKey → standard code (для whitelist).</summary>
    private readonly Dictionary<string, string> foundCodes = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>groupKey → стек с attributes (фонарь material/lining/glass).</summary>
    private readonly Dictionary<string, ItemStack> foundStacks = new(StringComparer.OrdinalIgnoreCase);
    private bool running;
    private Action<int, IReadOnlyList<string>, int, IReadOnlyDictionary<string, ItemStack>>? onComplete;

    public bool IsRunning => running;

    public ClaimUseFilterClientScanner(ICoreClientAPI api)
    {
        this.api = api;
    }

    /// <summary>
    /// onComplete(claimId, codes, scanned, stacksByCode) — stacks для корректных иконок.
    /// </summary>
    public void Start(
        int claimId,
        Action<int, IReadOnlyList<string>, int, IReadOnlyDictionary<string, ItemStack>> onComplete,
        int scanRadius = DefaultRadius)
    {
        Cancel();
        this.onComplete = onComplete;
        jobClaimId = claimId;
        radius = Math.Clamp(scanRadius, 4, 48);

        var player = api.World.Player?.Entity;
        if (player == null)
        {
            onComplete(claimId, Array.Empty<string>(), 0, new Dictionary<string, ItemStack>());
            return;
        }

        var pos = player.Pos.AsBlockPos;
        x0 = pos.X - radius;
        y0 = Math.Max(0, pos.Y - radius);
        z0 = pos.Z - radius;
        x1 = pos.X + radius;
        y1 = Math.Min(api.World.BlockAccessor.MapSizeY - 1, pos.Y + radius);
        z1 = pos.Z + radius;

        curX = x0;
        curY = y0;
        curZ = z0;
        scanned = 0;
        seenIds.Clear();
        foundCodes.Clear();
        foundStacks.Clear();
        running = true;

        api.Event.EnqueueMainThreadTask(Step, "swixy-usefilter-near-scan");
    }

    public void Cancel()
    {
        running = false;
        onComplete = null;
    }

    private void Step()
    {
        if (!running)
        {
            return;
        }

        try
        {
            var sw = Stopwatch.StartNew();
            var accessor = api.World.BlockAccessor;
            var done = false;
            var pos = new BlockPos(0, 0, 0);

            while (sw.ElapsedMilliseconds < BudgetMs && !done)
            {
                if (curX > x1)
                {
                    done = true;
                    break;
                }

                pos.X = curX;
                pos.Z = curZ;
                for (var y = y0; y <= y1; y++)
                {
                    scanned++;
                    pos.Y = y;
                    var block = accessor.GetBlock(pos);
                    if (block == null || block.Id == 0 || !seenIds.Add(block.Id))
                    {
                        continue;
                    }

                    TryAddBlock(block, pos, accessor);
                }

                curZ++;
                if (curZ > z1)
                {
                    curZ = z0;
                    curX++;
                }
            }

            if (!done)
            {
                api.Event.RegisterCallback(_ => Step(), 1);
                return;
            }

            // Стойки для брони / scarecrow — entity, не блок.
            CollectNearbyArmorStands();
            Finish();
        }
        catch (Exception ex)
        {
            api.Logger.Warning("[SwixyClaimChunk] Near use-filter scan failed: {0}", ex.Message);
            running = false;
            var cb = onComplete;
            onComplete = null;
            cb?.Invoke(jobClaimId, Array.Empty<string>(), scanned, new Dictionary<string, ItemStack>());
        }
    }

    private void Finish()
    {
        running = false;
        var codes = foundCodes.Values
            .OrderBy(static c => c, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Map code → stack for catalog build
        var byCode = new Dictionary<string, ItemStack>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in foundStacks)
        {
            if (foundCodes.TryGetValue(kv.Key, out var code) && !string.IsNullOrWhiteSpace(code))
            {
                byCode[code] = kv.Value;
            }
        }

        var cb = onComplete;
        onComplete = null;
        cb?.Invoke(jobClaimId, codes, scanned, byCode);
    }

    private void TryAddBlock(Block block, BlockPos pos, IBlockAccessor accessor)
    {
        if (block?.Code == null || block.Id == 0)
        {
            return;
        }

        var worldCode = ClaimCodeUtil.NormalizeCollectibleCode(block.Code.ToString());
        if (string.IsNullOrWhiteSpace(worldCode) || ClaimCodeUtil.IsMultiblockStubCode(worldCode))
        {
            return;
        }

        // Универсально: InteractionHelp + behaviors + EntityClass (не path-whitelist).
        if (!ClaimUseInteractability.ShouldShowInUseFilterCatalog(api, block, pos))
        {
            return;
        }

        var standard = ClaimCodeUtil.ResolveStandardDisplayCode(api.World, block);
        if (string.IsNullOrWhiteSpace(standard))
        {
            standard = ClaimCodeUtil.StripVariantSuffixes(worldCode);
        }

        // Семья: fence-oak/birch → game:fence; кусты → game:fruitingbush; двери up/down → один ключ.
        var groupKey = ClaimCodeUtil.GetCatalogGroupKey(worldCode);
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            groupKey = ClaimCodeUtil.GetCatalogGroupKey(standard);
        }

        if (string.IsNullOrWhiteSpace(groupKey))
        {
            groupKey = standard;
        }

        // Display code for whitelist/icon (preferred standard variant of this family).
        var displayCode = standard;

        // groundstorage (inventory pile): Cairo icon in GUI.
        if (ClaimCodeUtil.NeedsCairoIcon(standard) || ClaimCodeUtil.NeedsCairoIcon(worldCode))
        {
            displayCode = ClaimCodeUtil.NormalizeCollectibleCode("game:groundstorage");
        }

        // Уже есть вариант семьи (дверь up/down…).
        if (foundCodes.ContainsKey(groupKey))
        {
            if (IsBetterDoorDisplay(worldCode, foundCodes[groupKey])
                || IsPreferredFamilyDisplay(standard, foundCodes[groupKey]))
            {
                foundCodes[groupKey] = displayCode;
                var betterStack = BuildDisplayStack(block, standard, pos, accessor);
                if (betterStack != null)
                {
                    foundStacks[groupKey] = betterStack;
                }
            }

            return;
        }

        foundCodes[groupKey] = displayCode;

        if (ClaimCodeUtil.NeedsCairoIcon(displayCode))
        {
            return; // no 3D stack
        }

        var stack = BuildDisplayStack(block, standard, pos, accessor);
        if (stack != null)
        {
            foundStacks[groupKey] = stack;
        }
    }

    /// <summary>
    /// Предпочитаем более «стандартный» display-код семьи:
    /// короче (меньше вариантов), без open, creative-facing (south/up) уже в ResolveStandard.
    /// </summary>
    private static bool IsPreferredFamilyDisplay(string candidate, string existing)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(existing))
        {
            return false;
        }

        if (string.Equals(candidate, existing, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Shorter path = fewer material/state parts, usually better icon representative.
        var cPath = candidate.Contains(':') ? candidate[(candidate.IndexOf(':') + 1)..] : candidate;
        var ePath = existing.Contains(':') ? existing[(existing.IndexOf(':') + 1)..] : existing;
        if (cPath.Length < ePath.Length)
        {
            return true;
        }

        // Prefer oak as neutral wood default when same length-ish family variants.
        if (cPath.Contains("-oak", StringComparison.OrdinalIgnoreCase)
            && !ePath.Contains("-oak", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Armor stand / straw dummy стоят как entity — блоки рядом их не ловят.
    /// Добавляем item-код для whitelist/GUI (как в инвентаре).
    /// </summary>
    private void CollectNearbyArmorStands()
    {
        try
        {
            var player = api.World.Player?.Entity;
            if (player == null)
            {
                return;
            }

            var center = player.Pos.XYZ;
            var range = radius + 1f;
            api.World.GetEntitiesAround(
                center,
                range,
                range,
                entity =>
                {
                    if (entity?.Code == null || entity == player)
                    {
                        return true;
                    }

                    var path = entity.Code.Path ?? "";
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return true;
                    }

                    var isStand = path.Contains("armorstand", StringComparison.OrdinalIgnoreCase)
                                  || path.Contains("strawdummy", StringComparison.OrdinalIgnoreCase)
                                  || path.Contains("mannequin", StringComparison.OrdinalIgnoreCase);
                    if (!isStand)
                    {
                        return true;
                    }

                    // Entity code → item code (game:armorstand / game:armorstand-aged).
                    var itemCode = ClaimCodeUtil.NormalizeCollectibleCode(entity.Code.ToString());
                    if (string.IsNullOrWhiteSpace(itemCode))
                    {
                        return true;
                    }

                    var groupKey = ClaimCodeUtil.GetCatalogGroupKey(itemCode);
                    if (string.IsNullOrWhiteSpace(groupKey))
                    {
                        groupKey = itemCode;
                    }

                    if (!foundCodes.ContainsKey(groupKey))
                    {
                        foundCodes[groupKey] = itemCode;
                        var stack = TryGetItemStack(itemCode)
                                    ?? TryGetItemStack("game:armorstand")
                                    ?? TryGetItemStack("armorstand");
                        if (stack != null)
                        {
                            foundStacks[groupKey] = stack;
                        }

                        scanned++;
                    }

                    return true;
                });
        }
        catch (Exception ex)
        {
            api.Logger.Warning("[SwixyClaimChunk] Armor stand entity scan: {0}", ex.Message);
        }
    }

    private ItemStack? TryGetItemStack(string code)
    {
        try
        {
            var item = api.World.GetItem(new AssetLocation(code));
            if (item != null && item.Id != 0)
            {
                return new ItemStack(item, 1);
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    /// <summary>Иконка дерева/куста: fruit-* item по type из кода или BE.</summary>
    private ItemStack? BuildFruitTreeDisplayStack(string worldCode, BlockPos pos, IBlockAccessor accessor)
    {
        try
        {
            string? fruitType = null;

            // berrybush: fruitingbush-grown-blueberry-free / smallberrybush-blueberry-ripe
            var path = worldCode;
            var colon = worldCode.IndexOf(':');
            if (colon >= 0)
            {
                path = worldCode[(colon + 1)..];
            }

            var lower = path.ToLowerInvariant();
            string[] berryTypes =
            [
                "beautyberry", "blueberry", "cloudberry", "cranberry", "blackberry",
                "blackcurrant", "raspberry", "redcurrant", "whitecurrant", "strawberry",
                "pinkapple", "redapple", "yellowapple", "cherry", "mango", "olive",
                "orange", "peach", "pear", "breadfruit", "lychee", "pomegranate"
            ];
            foreach (var t in berryTypes)
            {
                if (lower.Contains(t, StringComparison.Ordinal))
                {
                    fruitType = t;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(fruitType))
            {
                var be = accessor.GetBlockEntity(pos);
                if (be != null)
                {
                    var tree = new TreeAttribute();
                    be.ToTreeAttributes(tree);
                    fruitType = tree.GetString("type");
                    if (string.IsNullOrWhiteSpace(fruitType))
                    {
                        fruitType = tree.GetString("fruitType");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(fruitType))
            {
                string[] tryItems =
                [
                    $"game:fruit-{fruitType}",
                    $"fruit-{fruitType}",
                    $"game:vegetable-{fruitType}",
                    $"vegetable-{fruitType}"
                ];
                foreach (var ic in tryItems)
                {
                    var item = api.World.GetItem(new AssetLocation(ic));
                    if (item != null && item.Id != 0)
                    {
                        return new ItemStack(item, 1);
                    }
                }
            }

            // Generic berry / apple
            foreach (var ic in new[]
                     {
                         "game:fruit-blueberry", "fruit-blueberry",
                         "game:fruit-redapple", "fruit-redapple", "game:fruit-cherry"
                     })
            {
                var item = api.World.GetItem(new AssetLocation(ic));
                if (item != null && item.Id != 0)
                {
                    return new ItemStack(item, 1);
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private ItemStack? BuildCoalPileItemStack(string worldCode)
    {
        string[] itemCodes = worldCode.Contains("charcoal", StringComparison.OrdinalIgnoreCase)
            ? ["game:charcoal", "charcoal"]
            : ["game:ore-bituminouscoal", "game:ore-lignite", "game:ore-anthracite", "game:charcoal"];

        foreach (var ic in itemCodes)
        {
            try
            {
                var item = api.World.GetItem(new AssetLocation(ic));
                if (item != null && item.Id != 0)
                {
                    return new ItemStack(item, 1);
                }
            }
            catch
            {
                // next
            }
        }

        return null;
    }

    /// <summary>
    /// Предпочтительный display: низ двери, закрытая калитка, изголовье кровати (как в креативе).
    /// </summary>
    private static bool IsBetterDoorDisplay(string candidateWorldCode, string existingStandard)
    {
        var c = candidateWorldCode.ToLowerInvariant();
        var e = existingStandard.ToLowerInvariant();
        var isFurniture = c.Contains("door") || c.Contains("gate") || c.Contains("bed")
                          || e.Contains("door") || e.Contains("gate") || e.Contains("bed");
        if (!isFurniture)
        {
            return false;
        }

        var score = 0;
        // Door halves
        if (c.Contains("-down") || c.Contains("-lower") || c.Contains("-bottom"))
        {
            score += 20;
        }

        if (c.Contains("-up") || c.Contains("-upper") || c.Contains("-top"))
        {
            score -= 20;
        }

        // Wattlegate / fence gate: closed for icon
        if (c.Contains("closed"))
        {
            score += 8;
        }

        if (c.Contains("opened") || (c.Contains("-open") && !c.Contains("opened")))
        {
            score -= 8;
        }

        // Bed: head + north (creativeinventory: *-head-north)
        if (c.Contains("-head"))
        {
            score += 15;
        }

        if (c.Contains("-feet") || c.Contains("-foot"))
        {
            score -= 15;
        }

        if (c.Contains("-north") && c.Contains("bed"))
        {
            score += 5;
        }

        if (e.Contains("-up") || e.Contains("-upper") || e.Contains("-top")
            || e.Contains("opened") || e.Contains("-feet") || e.Contains("-foot"))
        {
            score += 10;
        }

        return score > 0;
    }

    private ItemStack? BuildDisplayStack(Block worldBlock, string standardCode, BlockPos pos, IBlockAccessor accessor)
    {
        try
        {
            // 1) Creative stack семьи (lantern-*-up + material attributes).
            var creative = ClaimCodeUtil.TryGetFamilyCreativeStack(api.World, worldBlock)
                           ?? ClaimCodeUtil.TryGetFamilyCreativeStack(api.World, standardCode);
            if (creative != null)
            {
                // Если в мире другой material — подтянуть с BE/блока.
                TryCopyLanternishAttributesFromWorld(creative, worldBlock, pos, accessor);
                return creative;
            }

            // 2) Standard block + attributes from world
            Block? stdBlock = null;
            try
            {
                stdBlock = api.World.GetBlock(new AssetLocation(standardCode));
            }
            catch
            {
                // ignore
            }

            var block = stdBlock is { Id: > 0 } ? stdBlock : worldBlock;
            var stack = new ItemStack(block, 1);
            TryCopyLanternishAttributesFromWorld(stack, worldBlock, pos, accessor);
            // Defaults for lantern so mesh isn't empty
            EnsureLanternDefaultAttributes(stack);
            return stack;
        }
        catch
        {
            return null;
        }
    }

    private void TryCopyLanternishAttributesFromWorld(
        ItemStack stack,
        Block worldBlock,
        BlockPos pos,
        IBlockAccessor accessor)
    {
        try
        {
            // BE tree (lantern material / lining / glass).
            var be = accessor.GetBlockEntity(pos);
            if (be != null)
            {
                var tree = new TreeAttribute();
                be.ToTreeAttributes(tree);
                CopyTreeString(tree, stack, "material");
                CopyTreeString(tree, stack, "lining");
                CopyTreeString(tree, stack, "glass");
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void CopyTreeString(ITreeAttribute src, ItemStack stack, string key)
    {
        if (!src.HasAttribute(key))
        {
            return;
        }

        var v = src.GetString(key);
        if (string.IsNullOrWhiteSpace(v))
        {
            return;
        }

        stack.Attributes ??= new TreeAttribute();
        stack.Attributes.SetString(key, v);
    }

    private static void EnsureLanternDefaultAttributes(ItemStack stack)
    {
        var path = stack.Collectible?.Code?.Path ?? "";
        if (!path.Contains("lantern", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        stack.Attributes ??= new TreeAttribute();
        if (!stack.Attributes.HasAttribute("material"))
        {
            stack.Attributes.SetString("material", "copper");
        }

        if (!stack.Attributes.HasAttribute("lining"))
        {
            stack.Attributes.SetString("lining", "plain");
        }

        if (!stack.Attributes.HasAttribute("glass"))
        {
            stack.Attributes.SetString("glass", "quartz");
        }
    }
}
