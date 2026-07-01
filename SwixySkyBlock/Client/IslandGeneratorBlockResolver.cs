using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixySkyBlock;

internal static class IslandGeneratorBlockResolver
{
    private static readonly Dictionary<string, ItemStack?[]> VariantCache = new(StringComparer.Ordinal);

    public static ItemStack?[] ResolveVariantStacks(
        ICoreClientAPI api,
        string blockCode,
        string displayBlockCode = "")
    {
        if (string.IsNullOrWhiteSpace(blockCode))
        {
            blockCode = displayBlockCode;
        }

        if (string.IsNullOrWhiteSpace(blockCode))
        {
            return [];
        }

        if (VariantCache.TryGetValue(blockCode, out var cached))
        {
            return CloneStacks(cached);
        }

        var stacks = ResolveStacksUncached(api, blockCode, displayBlockCode);
        if (stacks.Length > 0)
        {
            VariantCache[blockCode] = stacks;
        }

        return CloneStacks(stacks);
    }

    public static ItemStack? ResolveDisplayStack(ICoreClientAPI api, string displayBlockCode, string fallbackBlockCode = "")
    {
        var stacks = ResolveVariantStacks(api, fallbackBlockCode, displayBlockCode);
        return stacks.FirstOrDefault(static stack => stack != null)?.Clone();
    }

    private static ItemStack?[] ResolveStacksUncached(
        ICoreClientAPI api,
        string blockCode,
        string displayBlockCode)
    {
        ItemStack?[] stacks;
        try
        {
            if (blockCode.Contains('*', StringComparison.Ordinal))
            {
                stacks = ResolveWildcardStacks(api, blockCode);
                if (stacks.Length == 0 && !string.IsNullOrWhiteSpace(displayBlockCode))
                {
                    stacks = ResolveSingleStack(api, displayBlockCode);
                }
            }
            else
            {
                stacks = ResolveSingleStack(
                    api,
                    string.IsNullOrWhiteSpace(displayBlockCode) ? blockCode : displayBlockCode);
            }
        }
        catch
        {
            stacks = [];
        }

        if (stacks.Length == 0 && !string.IsNullOrWhiteSpace(displayBlockCode)
            && !string.Equals(displayBlockCode, blockCode, StringComparison.Ordinal))
        {
            stacks = ResolveSingleStack(api, displayBlockCode);
        }

        return stacks;
    }

    private static ItemStack?[] ResolveWildcardStacks(ICoreClientAPI api, string blockCode)
    {
        return api.World.SearchBlocks(new AssetLocation(blockCode))
            .Where(static block => block.Id != 0)
            .OrderBy(static block => block.Code.ToString(), StringComparer.Ordinal)
            .Select(static block => (ItemStack?)new ItemStack(block))
            .ToArray();
    }

    private static ItemStack?[] ResolveSingleStack(ICoreClientAPI api, string blockCode)
    {
        if (string.IsNullOrWhiteSpace(blockCode) || blockCode.Contains('*', StringComparison.Ordinal))
        {
            return [];
        }

        var block = api.World.GetBlock(new AssetLocation(blockCode));
        if (block == null || block.Id == 0)
        {
            return [];
        }

        return [new ItemStack(block)];
    }

    private static ItemStack?[] CloneStacks(IReadOnlyList<ItemStack?> stacks)
    {
        if (stacks.Count == 0)
        {
            return [];
        }

        var clones = new ItemStack?[stacks.Count];
        for (var index = 0; index < stacks.Count; index++)
        {
            clones[index] = stacks[index]?.Clone();
        }

        return clones;
    }
}