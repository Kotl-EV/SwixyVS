using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

internal static class IslandPlacementService
{
    public static void PlaceIslandAsync(
        ICoreServerAPI api,
        IslandTemplate template,
        BlockPos origin,
        Action<bool> onComplete)
    {
        var columns = GetIntersectingColumns(template, origin);
        if (columns.Count == 0)
        {
            api.Logger.Warning("[SwixySkyBlock] No chunk columns for island at {0}.", origin);
            onComplete(false);
            return;
        }

        api.Logger.Notification(
            "[SwixySkyBlock] Loading {0} chunk column(s) for island '{1}' at {2}",
            columns.Count,
            template.Name,
            origin);

        var remaining = columns.Count;

        void OnColumnReady()
        {
            if (Interlocked.Decrement(ref remaining) > 0)
            {
                return;
            }

            var placed = TryPlaceNow(api, template, origin);
            onComplete(placed);
        }

        foreach (var (cx, cz) in columns)
        {
            api.WorldManager.LoadChunkColumnFast(cx, cz, new ChunkLoadOptions
            {
                KeepLoaded = true,
                OnLoaded = OnColumnReady
            });
        }
    }

    public static bool TryPlaceNow(ICoreServerAPI api, IslandTemplate template, BlockPos origin)
    {
        var columns = GetIntersectingColumns(template, origin);

        api.Logger.Notification(
            "[SwixySkyBlock] Placing island '{0}' at {1} across {2} column(s)",
            template.Name,
            origin,
            columns.Count);

        if (IslandPlacer.IsSurfacePresent(api.World.BlockAccessor, origin, template))
        {
            RelightIsland(api, template, origin);
            return true;
        }

        foreach (var (cx, cz) in columns)
        {
            var chunks = CollectColumnChunks(api, cx, cz);
            if (chunks == null)
            {
                api.Logger.Warning(
                    "[SwixySkyBlock] Chunk column ({0},{1}) not available for placement.",
                    cx,
                    cz);
                continue;
            }
        }

        if (!IslandPlacer.PlaceIntoWorld(api, template, origin))
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Island placement failed at {0} (template={1}).",
                origin,
                template.Name);
            return false;
        }

        RelightIsland(api, template, origin);

        if (!IslandPlacer.IsSurfacePresent(api.World.BlockAccessor, origin, template))
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Island placed but surface not verified at {0} (template={1}).",
                origin,
                template.Name);
            return false;
        }

        api.Logger.Notification("[SwixySkyBlock] Island placed at {0}.", origin);
        return true;
    }

    public static void TryPlaceRegisteredIslandAtChunk(
        ICoreServerAPI api,
        PlayerIslandRecord record,
        IslandTemplate template,
        Vec2i chunkCoord,
        IWorldChunk[] chunks)
    {
        var bounds = template.GetBounds(record.Origin);
        if (!ColumnIntersects(chunkCoord.X, chunkCoord.Y, bounds, GlobalConstants.ChunkSize))
        {
            return;
        }

        if (IslandPlacer.IsSurfacePresent(api.World.BlockAccessor, record.Origin, template))
        {
            return;
        }

        if (TryPlaceNow(api, template, record.Origin))
        {
            api.Logger.Notification(
                "[SwixySkyBlock] Placed registered island for {0} at {1}.",
                record.PlayerUid,
                record.Origin);
        }
    }

    public static void ClearIslandVolume(ICoreServerAPI api, IslandTemplate template, BlockPos origin)
    {
        foreach (var (cx, cz) in GetIntersectingColumns(template, origin))
        {
            api.WorldManager.LoadChunkColumn(cx, cz, keepLoaded: true);
        }

        var bounds = template.GetBounds(origin);
        var bulk = api.World.GetBlockAccessorBulkUpdate(true, true);

        for (var x = bounds.X1; x <= bounds.X2; x++)
        {
            for (var y = bounds.Y1; y <= bounds.Y2; y++)
            {
                for (var z = bounds.Z1; z <= bounds.Z2; z++)
                {
                    var pos = new BlockPos(x, y, z);
                    if (bulk.GetBlock(pos).Id != 0)
                    {
                        bulk.SetBlock(0, pos);
                    }
                }
            }
        }

        bulk.Commit();
    }

    private static List<(int cx, int cz)> GetIntersectingColumns(IslandTemplate template, BlockPos origin)
    {
        var bounds = template.GetBounds(origin);
        const int chunkSize = GlobalConstants.ChunkSize;
        var cx1 = bounds.X1 / chunkSize;
        var cx2 = bounds.X2 / chunkSize;
        var cz1 = bounds.Z1 / chunkSize;
        var cz2 = bounds.Z2 / chunkSize;

        var columns = new List<(int, int)>();
        for (var cx = cx1; cx <= cx2; cx++)
        {
            for (var cz = cz1; cz <= cz2; cz++)
            {
                columns.Add((cx, cz));
            }
        }

        return columns;
    }

    private static IWorldChunk[]? CollectColumnChunks(ICoreServerAPI api, int chunkX, int chunkZ)
    {
        var chunkHeight = GlobalConstants.ChunkSize;
        var maxCy = Math.Max(1, (api.WorldManager.MapSizeY + chunkHeight - 1) / chunkHeight);
        var chunks = new List<IWorldChunk>(maxCy);

        for (var cy = 0; cy < maxCy; cy++)
        {
            var chunk = api.WorldManager.GetChunk(chunkX, cy, chunkZ);
            if (chunk == null)
            {
                break;
            }

            chunks.Add(chunk);
        }

        return chunks.Count > 0 ? chunks.ToArray() : null;
    }

    private static bool ColumnIntersects(int chunkX, int chunkZ, Cuboidi bounds, int chunkSize)
    {
        var minX = chunkX * chunkSize;
        var maxX = minX + chunkSize - 1;
        var minZ = chunkZ * chunkSize;
        var maxZ = minZ + chunkSize - 1;

        return maxX >= bounds.X1 && minX <= bounds.X2 && maxZ >= bounds.Z1 && minZ <= bounds.Z2;
    }

    private static void RelightIsland(ICoreServerAPI api, IslandTemplate template, BlockPos origin)
    {
        var bounds = template.GetBounds(origin);
        api.WorldManager.FullRelight(
            new BlockPos(bounds.X1, bounds.Y1, bounds.Z1),
            new BlockPos(bounds.X2, bounds.Y2, bounds.Z2),
            sendToClients: true);
    }
}
