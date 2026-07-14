using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Пустая chunk column: без terrain/руд/структур, только map chunk metadata для ванильных систем.</summary>
internal static class SkyBlockEmptyChunkGen
{
    private static int fallbackRockBlockId;

    public static void OnTerrainPass(ICoreServerAPI api, IChunkColumnGenerateRequest request)
    {
        if (request.Chunks.OfType<IServerChunk>().ToArray() is { Length: > 0 } serverChunks
            && StorySiteGenerationService.GetActiveContextForChunk(request.ChunkX, request.ChunkZ) is { } site)
        {
            StorySiteColumnGenerator.Generate(api, site, request.ChunkX, request.ChunkZ, serverChunks);
            return;
        }

        ClearChunkColumn(request);
        SeedMapChunkMetadata(api, request);
    }

    private static void ClearChunkColumn(IChunkColumnGenerateRequest request)
    {
        foreach (var chunk in request.Chunks)
        {
            chunk?.Data?.ClearBlocks();
        }
    }

    private static void SeedMapChunkMetadata(ICoreServerAPI api, IChunkColumnGenerateRequest request)
    {
        var mapChunk = request.Chunks[0]?.MapChunk;
        if (mapChunk == null)
        {
            return;
        }

        const int mapSize = GlobalConstants.ChunkSize * GlobalConstants.ChunkSize;

        if (mapChunk.TopRockIdMap != null && mapChunk.TopRockIdMap.Length >= mapSize
            && mapChunk.TopRockIdMap.All(id => id == 0))
        {
            var rockId = ResolveFallbackRockBlockId(api);
            Array.Fill(mapChunk.TopRockIdMap, rockId);
        }

        if (mapChunk.WorldGenTerrainHeightMap != null && mapChunk.WorldGenTerrainHeightMap.Length >= mapSize)
        {
            Array.Clear(mapChunk.WorldGenTerrainHeightMap, 0, mapSize);
        }

        if (mapChunk.RainHeightMap != null && mapChunk.RainHeightMap.Length >= mapSize)
        {
            Array.Clear(mapChunk.RainHeightMap, 0, mapSize);
        }
    }

    private static int ResolveFallbackRockBlockId(ICoreServerAPI api)
    {
        if (fallbackRockBlockId > 0)
        {
            return fallbackRockBlockId;
        }

        fallbackRockBlockId = api.World.GetBlock(new AssetLocation("game:rock-granite"))?.Id ?? 1;
        return fallbackRockBlockId;
    }
}
