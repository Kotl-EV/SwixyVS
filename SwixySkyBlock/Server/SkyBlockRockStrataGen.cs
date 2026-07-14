using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace SwixySkyBlock;

/// <summary>TopRockIdMap и прочие map-chunk данные для rockTypeRemap в сюжетных структурах.</summary>
internal static class SkyBlockRockStrataGen
{
    private static MethodInfo? genChunkColumn;

    public static void OnTerrainPass(ICoreServerAPI api, IChunkColumnGenerateRequest request)
    {
        SkyBlockWorldGenBootstrap.EnsureMapGeneratorInits(api);
        InvokeGenChunkColumn(api, request);
    }

    public static void RefreshLoadedColumn(ICoreServerAPI api, int chunkX, int chunkZ, IServerChunk[] chunks)
    {
        if (chunks.Length == 0)
        {
            return;
        }

        SkyBlockWorldGenBootstrap.EnsureMapGeneratorInits(api);
        InvokeGenChunkColumn(api, new SkyBlockChunkColumnRequest(chunkX, chunkZ, chunks));
    }

    /// <summary>Без GenRockStrata на уже загруженных колонках (NRE вне worldgen).</summary>
    public static void SeedTopRockForColumn(ICoreServerAPI api, IServerChunk[] chunks)
    {
        var mapChunk = chunks[0]?.MapChunk;
        if (mapChunk?.TopRockIdMap == null)
        {
            return;
        }

        const int mapSize = GlobalConstants.ChunkSize * GlobalConstants.ChunkSize;
        if (mapChunk.TopRockIdMap.Length < mapSize)
        {
            return;
        }

        var rockId = api.World.GetBlock(new AssetLocation("game:rock-granite"))?.Id ?? 1;
        for (var i = 0; i < mapSize; i++)
        {
            if (mapChunk.TopRockIdMap[i] == 0)
            {
                mapChunk.TopRockIdMap[i] = rockId;
            }
        }
    }

    private static void InvokeGenChunkColumn(ICoreServerAPI api, IChunkColumnGenerateRequest request)
    {
        var rockStrata = api.ModLoader.GetModSystem<GenRockStrataNew>();
        if (rockStrata == null)
        {
            return;
        }

        genChunkColumn ??= typeof(GenRockStrataNew).GetMethod(
            "GenChunkColumn",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (genChunkColumn == null)
        {
            api.Logger.Warning("[SwixySkyBlock] GenRockStrataNew.GenChunkColumn not found; rock remaps may fail.");
            return;
        }

        try
        {
            genChunkColumn.Invoke(rockStrata, [request]);
        }
        catch (Exception ex)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] GenRockStrata.GenChunkColumn failed at ({0}, {1}): {2}",
                request.ChunkX,
                request.ChunkZ,
                ex.InnerException?.Message ?? ex.Message);
        }
    }

    private sealed class SkyBlockChunkColumnRequest : IChunkColumnGenerateRequest
    {
        public SkyBlockChunkColumnRequest(int chunkX, int chunkZ, IServerChunk[] chunks)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Chunks = chunks;
        }

        public int ChunkX { get; }
        public int ChunkZ { get; }
        public IServerChunk[] Chunks { get; }
        public ITreeAttribute ChunkGenParams { get; } = new TreeAttribute();
        public ushort[][] NeighbourTerrainHeight { get; } = [];
        public bool RequiresChunkBorderSmoothing => false;
    }
}