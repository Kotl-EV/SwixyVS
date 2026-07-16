using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace SwixySkyBlock;

/// <summary>
/// Подключает к worldType=skyblock только map-часть standard worldgen (GenMaps, rock strata),
/// без структур/данжей и без chunk column terrain.
/// </summary>
internal static class SkyBlockWorldGenBootstrap
{
    private static readonly MethodInfo GenMapsInit =
        typeof(GenMaps).GetMethod("initWorldGen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static readonly MethodInfo GenMapsOnMapRegionGen =
        typeof(GenMaps).GetMethod("OnMapRegionGen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static readonly MethodInfo GenRockStrataInit =
        typeof(GenRockStrataNew).GetMethod("initWorldGen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static readonly MethodInfo GenRockStrataOnMapRegionGen =
        typeof(GenRockStrataNew).GetMethod("OnMapRegionGen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static readonly MethodInfo GenBlockLayersInit =
        typeof(GenBlockLayers).GetMethod("InitWorldGen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static readonly MethodInfo GenBlockLayersOnMapRegionGen =
        typeof(GenBlockLayers).GetMethod("OnMapRegionGen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static bool bootstrapped;
    private static bool mapInitsApplied;

    public static void Bootstrap(ICoreServerAPI api)
    {
        if (bootstrapped)
        {
            return;
        }

        var skyblock = api.Event.GetRegisteredWorldGenHandlers(SkyBlockWorld.WorldType);
        if (skyblock == null)
        {
            api.Logger.Error(
                "[SwixySkyBlock] Skyblock worldgen handler slot is missing; register InitWorldGenerator first.");
            return;
        }

        skyblock.OnMapRegionGen.Clear();

        skyblock.OnMapChunkGen.Clear();

        if (skyblock.OnChunkColumnGen != null)
        {
            for (var pass = 0; pass < skyblock.OnChunkColumnGen.Length; pass++)
            {
                if (pass == (int)EnumWorldGenPass.Done)
                {
                    continue;
                }

                skyblock.OnChunkColumnGen[pass]?.Clear();
            }
        }

        bootstrapped = true;
        api.Logger.Notification(
            "[SwixySkyBlock] Skyblock worldgen wired ({0} region handler(s)); chunk columns empty.",
            skyblock.OnMapRegionGen.Count);
    }

    public static void EnsureMapGeneratorInits(ICoreServerAPI api)
    {
        if (mapInitsApplied || !UsesSkyBlockWorldType(api))
        {
            return;
        }

        try
        {
            var genMaps = api.ModLoader.GetModSystem<GenMaps>();
            var rockStrata = api.ModLoader.GetModSystem<GenRockStrataNew>();
            if (genMaps != null)
            {
                GenMapsInit.Invoke(genMaps, []);
            }

            if (rockStrata != null)
            {
                GenRockStrataInit.Invoke(rockStrata, []);
            }

            var blockLayers = api.ModLoader.GetModSystem<GenBlockLayers>();
            if (blockLayers != null)
            {
                GenBlockLayersInit.Invoke(blockLayers, []);
            }

            mapInitsApplied = true;
            api.Logger.Notification("[SwixySkyBlock] Vanilla map generator inits applied for skyblock world type.");
        }
        catch (Exception ex)
        {
            api.Logger.Error("[SwixySkyBlock] Failed to initialize vanilla map generators: {0}", ex);
        }
    }

    public static bool UsesSkyBlockWorldType(ICoreServerAPI api) =>
        string.Equals(
            api.World.Config.GetString("worldType", api.WorldManager.SaveGame.WorldType),
            SkyBlockWorld.WorldType,
            StringComparison.OrdinalIgnoreCase);

    public static void GenerateMapRegion(
        ICoreServerAPI api,
        IMapRegion mapRegion,
        int regionX,
        int regionZ,
        ITreeAttribute? chunkGenParams = null)
    {
        EnsureMapGeneratorInits(api);
        var tree = chunkGenParams ?? new TreeAttribute();
        var genMaps = api.ModLoader.GetModSystem<GenMaps>();
        var rockStrata = api.ModLoader.GetModSystem<GenRockStrataNew>();
        var blockLayers = api.ModLoader.GetModSystem<GenBlockLayers>();

        if (genMaps != null)
        {
            GenMapsOnMapRegionGen.Invoke(genMaps, [mapRegion, regionX, regionZ, tree]);
        }

        if (rockStrata != null)
        {
            GenRockStrataOnMapRegionGen.Invoke(rockStrata, [mapRegion, regionX, regionZ, tree]);
        }

        if (blockLayers != null)
        {
            GenBlockLayersOnMapRegionGen.Invoke(blockLayers, [mapRegion, regionX, regionZ, tree]);
        }
    }
}
