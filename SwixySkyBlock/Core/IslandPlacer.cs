using System;
using Vintagestory.API.Common;
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

    public static bool PlaceIntoWorld(ICoreServerAPI api, IslandTemplate island, BlockPos origin)
    {
        var bulk = api.World.GetBlockAccessorBulkUpdate(true, true);
        return PlaceWithAccessor(api, bulk, island, origin);
    }

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

        return IsSurfacePresent(api.World.BlockAccessor, origin, island);
    }
}
