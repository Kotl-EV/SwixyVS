using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Размещение схематики острова в генерируемых или загруженных колонках чанков.</summary>
internal static class IslandPlacer
{
    public static bool PlaceIntoColumn(
        ICoreServerAPI api,
        IChunkColumnGenerateRequest request,
        IslandTemplate island,
        BlockPos origin)
    {
        if (api.World.GetBlockAccessorMapChunkLoading(false) is not IBulkBlockAccessor bulk)
        {
            return false;
        }

        bulk.SetChunks(new Vec2i(request.ChunkX, request.ChunkZ), request.Chunks);
        return PlaceWithAccessor(api, bulk, island, origin);
    }

    public static bool PlaceIntoLoadedColumn(
        ICoreServerAPI api,
        Vec2i chunkCoord,
        IWorldChunk[] chunks,
        IslandTemplate island,
        BlockPos origin)
    {
        if (api.World.GetBlockAccessorMapChunkLoading(true, true) is not IBulkBlockAccessor bulk)
        {
            return false;
        }

        bulk.SetChunks(chunkCoord, chunks);
        return PlaceWithAccessor(api, bulk, island, origin);
    }

    public static bool PlacePrecomputedColumn(
        ICoreServerAPI api,
        Vec2i chunkCoord,
        IWorldChunk[] chunks,
        BlockPos origin,
        BlockSchematic schematic,
        IReadOnlyList<(int Lx, int Ly, int Lz, int BlockCode)> blocks)
    {
        if (blocks.Count == 0)
        {
            return true;
        }

        if (api.World.GetBlockAccessorMapChunkLoading(true, true) is not IBulkBlockAccessor bulk)
        {
            return false;
        }

        bulk.SetChunks(chunkCoord, chunks);
        foreach (var (lx, ly, lz, blockCode) in blocks)
        {
            if (!schematic.BlockCodes.TryGetValue(blockCode, out var location))
            {
                continue;
            }

            var block = api.World.GetBlock(location);
            if (block == null)
            {
                continue;
            }

            bulk.SetBlock(block.BlockId, new BlockPos(origin.X + lx, origin.Y + ly, origin.Z + lz));
        }

        bulk.Commit();
        return true;
    }

    public static bool PlaceIntoWorld(ICoreServerAPI api, IslandTemplate island, BlockPos origin)
    {
        var bulk = api.World.GetBlockAccessorBulkUpdate(true, true);
        return PlaceWithAccessor(api, bulk, island, origin);
    }

    public static bool PlaceIntoLoadedBounds(
        ICoreServerAPI api,
        IslandTemplate island,
        BlockPos origin,
        Cuboidi bounds)
    {
        const int chunkSize = GlobalConstants.ChunkSize;
        var cx1 = bounds.X1 / chunkSize;
        var cx2 = bounds.X2 / chunkSize;
        var cz1 = bounds.Z1 / chunkSize;
        var cz2 = bounds.Z2 / chunkSize;

        for (var cx = cx1; cx <= cx2; cx++)
        {
            for (var cz = cz1; cz <= cz2; cz++)
            {
                var chunks = CollectColumnChunks(api, cx, cz);
                if (chunks == null)
                {
                    continue;
                }

                PlaceIntoLoadedColumn(api, new Vec2i(cx, cz), chunks, island, origin);
            }
        }

        var accessor = api.World.BlockAccessor;
        return IsPlacementComplete(accessor, origin, island)
            || IsSurfacePresent(accessor, origin, island);
    }

    private static IWorldChunk[]? CollectColumnChunks(ICoreServerAPI api, int chunkX, int chunkZ) =>
        StoryDungeonChunkLoader.LoadColumnChunks(api, chunkX, chunkZ);

    public static bool IsSurfacePresent(IBlockAccessor accessor, BlockPos origin, IslandTemplate island)
    {
        var cx = island.Schematic.SizeX / 2;
        var cz = island.Schematic.SizeZ / 2;
        for (var y = island.Schematic.SizeY - 1; y >= 0; y--)
        {
            if (accessor.GetBlock(origin.X + cx, origin.Y + y, origin.Z + cz).Id != 0)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPlacementComplete(IBlockAccessor accessor, BlockPos origin, IslandTemplate island)
    {
        var schematic = island.Schematic;
        var cx = schematic.SizeX / 2;
        var cz = schematic.SizeZ / 2;
        var radius = Math.Min(cx, cz) - 2;
        if (radius < 4)
        {
            return IsSurfacePresent(accessor, origin, island);
        }

        Span<(int Dx, int Dz)> samples =
        [
            (0, 0),
            (-radius, 0), (radius, 0), (0, -radius), (0, radius),
            (-radius, -radius), (radius, -radius), (-radius, radius), (radius, radius),
            (-11, 0), (11, 0), (0, -11), (0, 11), (11, 11), (-11, 11)
        ];

        foreach (var (dx, dz) in samples)
        {
            var x = cx + dx;
            var z = cz + dz;
            if (x < 0 || x >= schematic.SizeX || z < 0 || z >= schematic.SizeZ)
            {
                continue;
            }

            if (!SchematicHasColumn(schematic, x, z))
            {
                continue;
            }

            if (!WorldHasColumn(accessor, origin, schematic, x, z))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SchematicHasColumn(BlockSchematic schematic, int x, int z)
    {
        var count = Math.Min(schematic.Indices.Count, schematic.BlockIds.Count);
        for (var i = 0; i < count; i++)
        {
            if (schematic.BlockIds[i] == 0)
            {
                continue;
            }

            var index = schematic.Indices[i];
            if ((int)(index & 0x3ff) == x && (int)((index >> 10) & 0x3ff) == z)
            {
                return true;
            }
        }

        return false;
    }

    private static bool WorldHasColumn(
        IBlockAccessor accessor,
        BlockPos origin,
        BlockSchematic schematic,
        int x,
        int z)
    {
        for (var y = schematic.SizeY - 1; y >= 0; y--)
        {
            if (accessor.GetBlock(origin.X + x, origin.Y + y, origin.Z + z).Id != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool PlaceWithAccessor(
        ICoreServerAPI api,
        IBlockAccessor accessor,
        IslandTemplate island,
        BlockPos origin)
    {
        var schematic = island.Schematic.ClonePacked();
        try
        {
            schematic.LoadMetaInformationAndValidate(accessor, api.World, island.Name);
            schematic.Place(accessor, api.World, origin, EnumReplaceMode.ReplaceAllNoAir, true);

            if (accessor is IBulkBlockAccessor bulk)
            {
                bulk.Commit();
            }
        }
        catch (Exception ex)
        {
            api.Logger.Error(
                "[SwixySkyBlock] Schematic '{0}' place error at {1}: {2}",
                island.Name,
                origin,
                ex);
            return false;
        }

        return IsPlacementComplete(api.World.BlockAccessor, origin, island);
    }
}
