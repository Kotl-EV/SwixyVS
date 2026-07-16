using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace SwixySkyBlock;

/// <summary>Runs selected vanilla worldgen passes only for story-site pocket columns.</summary>
internal static class SkyBlockVanillaStoryPocketGen
{
    private const string GeneratedKey = "swixyskyblock:vanilla-story-pocket-v1";

    private static readonly object Sync = new();
    private static readonly List<GeneratorStep> Steps =
    [
        Step<GenTerra>("initWorldGen", "OnChunkColumnGen", EnumWorldGenPass.Terrain),
        Step<GenRockStrataNew>("initWorldGen", "GenChunkColumn", EnumWorldGenPass.Terrain),
        Step<GenBlockLayers>("InitWorldGen", "OnChunkColumnGeneration", EnumWorldGenPass.Terrain)
    ];

    private static bool initialized;

    public static void GenerateColumn(
        ICoreServerAPI api,
        int chunkX,
        int chunkZ,
        IServerChunk[] chunks)
    {
        if (chunks.Length == 0 || chunks[0].MapChunk == null)
        {
            return;
        }

        if (chunks[0].MapChunk.GetModdata(GeneratedKey) != null)
        {
            return;
        }

        SkyBlockClimateMaps.EnsureForBlockPos(
            api,
            chunkX * GlobalConstants.ChunkSize + GlobalConstants.ChunkSize / 2,
            chunkZ * GlobalConstants.ChunkSize + GlobalConstants.ChunkSize / 2);

        EnsureInitialized(api);

        var request = new PocketChunkColumnGenerateRequest(chunkX, chunkZ, chunks);
        foreach (var step in Steps)
        {
            var system = GetModSystem(api, step.SystemType);
            if (system == null || step.GenerateMethod == null)
            {
                continue;
            }

            try
            {
                chunks[0].MapChunk.CurrentPass = step.Pass;
                step.GenerateMethod.Invoke(system, [request]);
            }
            catch (Exception ex)
            {
                api.Logger.Warning(
                    "[SwixySkyBlock] Vanilla story pocket pass {0}.{1} failed at ({2}, {3}): {4}",
                    step.SystemType.Name,
                    step.GenerateMethod.Name,
                    chunkX,
                    chunkZ,
                    ex.InnerException?.Message ?? ex.Message);
            }
        }

        chunks[0].MapChunk.SetModdata(GeneratedKey, [1]);
        foreach (var chunk in chunks)
        {
            chunk.MarkModified();
        }
    }

    private static void EnsureInitialized(ICoreServerAPI api)
    {
        if (initialized)
        {
            return;
        }

        lock (Sync)
        {
            if (initialized)
            {
                return;
            }

            foreach (var step in Steps)
            {
                var system = GetModSystem(api, step.SystemType);
                if (system == null || step.InitMethod == null)
                {
                    continue;
                }

                try
                {
                    step.InitMethod.Invoke(system, []);
                }
                catch (Exception ex)
                {
                    api.Logger.Warning(
                        "[SwixySkyBlock] Vanilla story pocket init {0}.{1} failed: {2}",
                        step.SystemType.Name,
                        step.InitMethod.Name,
                        ex.InnerException?.Message ?? ex.Message);
                }
            }

            initialized = true;
        }
    }

    private static GeneratorStep Step<T>(string initMethodName, string generateMethodName, EnumWorldGenPass pass) =>
        new(
            typeof(T),
            typeof(T).GetMethod(initMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            typeof(T).GetMethod(generateMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            pass);

    private static object? GetModSystem(ICoreServerAPI api, Type systemType) =>
        api.ModLoader.Systems.FirstOrDefault(systemType.IsInstanceOfType);

    private sealed record GeneratorStep(
        Type SystemType,
        MethodInfo? InitMethod,
        MethodInfo? GenerateMethod,
        EnumWorldGenPass Pass);

    private sealed class PocketChunkColumnGenerateRequest(
        int chunkX,
        int chunkZ,
        IServerChunk[] chunks) : IChunkColumnGenerateRequest
    {
        public IServerChunk[] Chunks { get; } = chunks;
        public int ChunkX { get; } = chunkX;
        public int ChunkZ { get; } = chunkZ;
        public ITreeAttribute ChunkGenParams { get; } = new TreeAttribute();
        public ushort[][] NeighbourTerrainHeight { get; } = [];
        public bool RequiresChunkBorderSmoothing { get; } = false;
    }
}
