// =============================================================================
// Каталог Use-фильтра: двери/калитки + только блоки с инвентарем.
// =============================================================================

using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Core;

/// <summary>
/// Определяет, должен ли установленный блок отображаться в каталоге Use-фильтра.
/// Только двери/калитки и контейнеры инвентаря (сундуки, полки, костры…).
/// </summary>
public static class ClaimUseInteractability
{
    /// <summary>Кэш: Block.Id → показывать? (id стабильны в течение сессии).</summary>
    private static readonly Dictionary<int, bool> Cache = new();

    /// <summary>Показывать ли блок как подходящий кандидат в whitelist Use рядом с игроком.</summary>
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

    /// <summary>Проверка только на сервере/в логике мира без участия client API.</summary>
    public static bool ShouldShowInUseFilterCatalog(
        IWorldAccessor world,
        Block block,
        BlockPos? pos)
        => ClaimCodeUtil.IsUseFilterCatalogCandidate(world, block, pos);

    /// <summary>Очистить кэш (нужно редко — при hot-reload модов).</summary>
    public static void ClearCache() => Cache.Clear();
}