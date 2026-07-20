// =============================================================================
// Use-filter catalog: doors/gates + blocks with inventory only.
// =============================================================================

using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Core;

/// <summary>
/// Determines whether a placed block should appear in the Use-filter catalog.
/// Only doors/gates and inventory containers (chests, shelves, firepits…).
/// </summary>
public static class ClaimUseInteractability
{
    /// <summary>Cache: Block.Id → show? (ids stable per session).</summary>
    private static readonly Dictionary<int, bool> Cache = new();

    /// <summary>
    /// True if the block is a useful Use-whitelist candidate near the player.
    /// </summary>
    public static bool ShouldShowInUseFilterCatalog(
        ICoreClientAPI api,
        Block block,
        BlockPos pos)
    {
        if (api == null || block == null || block.Id == 0 || block.Code == null)
        {
            return false;
        }

        if (Cache.TryGetValue(block.Id, out var cached))
        {
            return cached;
        }

        var show = ClaimCodeUtil.IsUseFilterCatalogCandidate(api.World, block, pos);
        Cache[block.Id] = show;
        return show;
    }

    /// <summary>Server/path-only check without client help API.</summary>
    public static bool ShouldShowInUseFilterCatalog(
        IWorldAccessor world,
        Block block,
        BlockPos? pos)
        => ClaimCodeUtil.IsUseFilterCatalogCandidate(world, block, pos);

    /// <summary>Optional: clear cache if mods hot-reload (rarely needed).</summary>
    public static void ClearCache() => Cache.Clear();
}
