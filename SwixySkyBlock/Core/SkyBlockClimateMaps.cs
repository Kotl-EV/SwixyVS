using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace SwixySkyBlock;

/// <summary>
/// Подключает ванильный GenMaps к пустому SkyBlock-миру:
/// MapRegion.ClimateMap и остальные карты региона, как в standard.
/// </summary>
internal static class SkyBlockClimateMaps
{
    private static readonly MethodInfo InitWorldGenMethod = typeof(GenMaps).GetMethod(
        "initWorldGen",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static readonly MethodInfo OnMapRegionGenMethod = typeof(GenMaps).GetMethod(
        "OnMapRegionGen",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static bool genMapsInitialized;

    public static void Register(ICoreServerAPI api)
    {
        foreach (var worldType in SkyBlockWorld.LegacyWorldTypes)
        {
            api.Event.InitWorldGenerator(() => OnInitWorldGenerator(api), worldType);
            api.Event.MapRegionGeneration(
                (mapRegion, regionX, regionZ, chunkGenParams) =>
                    OnMapRegionGeneration(api, mapRegion, regionX, regionZ, chunkGenParams),
                worldType);
        }

        api.Event.SaveGameLoaded += () => BackfillLoadedRegions(api);
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () => BackfillLoadedRegions(api));
    }

    public static void EnsureForBlockPos(ICoreServerAPI api, int blockX, int blockZ)
    {
        if (!SkyBlockWorld.IsSkyBlockWorld(api))
        {
            return;
        }

        if (SkyBlockWorldGenBootstrap.UsesSkyBlockWorldType(api))
        {
            SkyBlockWorldGenBootstrap.EnsureMapGeneratorInits(api);
        }
        else if (!UsesLegacyClimateBackfill(api))
        {
            return;
        }
        else
        {
            EnsureInitialized(api);
        }

        var index = api.WorldManager.MapRegionIndex2DByBlockPos(blockX, blockZ);
        var region = api.WorldManager.GetMapRegion(index);
        if (region == null)
        {
            var chunkX = blockX / GlobalConstants.ChunkSize;
            var chunkZ = blockZ / GlobalConstants.ChunkSize;
            api.WorldManager.LoadChunkColumn(chunkX, chunkZ, keepLoaded: false);
            region = api.WorldManager.GetMapRegion(index);
        }

        if (region == null || !NeedsClimateMap(region))
        {
            return;
        }

        var pos = api.WorldManager.MapRegionPosFromIndex2D(index);
        if (SkyBlockWorldGenBootstrap.UsesSkyBlockWorldType(api))
        {
            SkyBlockWorldGenBootstrap.GenerateMapRegion(api, region, pos.X, pos.Z);
        }
        else
        {
            GenerateMapRegion(api, region, pos.X, pos.Z);
        }

        ReapplyUniformLatitude(api);
    }

    private static void OnInitWorldGenerator(ICoreServerAPI api)
    {
        if (!SkyBlockWorld.IsSkyBlockWorld(api))
        {
            return;
        }

        EnsureInitialized(api);
        ReapplyUniformLatitude(api);
    }

    private static void OnMapRegionGeneration(
        ICoreServerAPI api,
        IMapRegion mapRegion,
        int regionX,
        int regionZ,
        ITreeAttribute chunkGenParams)
    {
        if (!SkyBlockWorld.IsSkyBlockWorld(api))
        {
            return;
        }

        EnsureInitialized(api);
        GenerateMapRegion(api, mapRegion, regionX, regionZ, chunkGenParams);
        ReapplyUniformLatitude(api);
    }

    private static void BackfillLoadedRegions(ICoreServerAPI api)
    {
        if (!UsesLegacyClimateBackfill(api))
        {
            return;
        }

        EnsureInitialized(api);

        var filled = 0;
        foreach (var (index, region) in api.WorldManager.AllLoadedMapRegions)
        {
            if (!NeedsClimateMap(region))
            {
                continue;
            }

            var pos = api.WorldManager.MapRegionPosFromIndex2D(index);
            GenerateMapRegion(api, region, pos.X, pos.Z);
            filled++;
        }

        if (filled > 0)
        {
            api.Logger.Notification(
                "[SwixySkyBlock] Backfilled vanilla climate maps for {0} map region(s).",
                filled);
        }

        ReapplyUniformLatitude(api);
    }

    private static void EnsureInitialized(ICoreServerAPI api)
    {
        if (genMapsInitialized)
        {
            return;
        }

        var genMaps = api.ModLoader.GetModSystem<GenMaps>();
        if (genMaps == null)
        {
            api.Logger.Warning("[SwixySkyBlock] GenMaps is unavailable; climate maps cannot be generated.");
            return;
        }

        try
        {
            InitWorldGenMethod.Invoke(genMaps, []);
            genMapsInitialized = true;
            api.Logger.Notification("[SwixySkyBlock] Vanilla GenMaps initialized for SkyBlock climate maps.");
        }
        catch (Exception ex)
        {
            api.Logger.Error("[SwixySkyBlock] Failed to initialize GenMaps for SkyBlock: {0}", ex);
        }
    }

    private static void GenerateMapRegion(
        ICoreServerAPI api,
        IMapRegion mapRegion,
        int regionX,
        int regionZ,
        ITreeAttribute? chunkGenParams = null)
    {
        var genMaps = api.ModLoader.GetModSystem<GenMaps>();
        if (genMaps == null)
        {
            return;
        }

        try
        {
            OnMapRegionGenMethod.Invoke(
                genMaps,
                [mapRegion, regionX, regionZ, chunkGenParams ?? new TreeAttribute()]);
        }
        catch (Exception ex)
        {
            api.Logger.Error(
                "[SwixySkyBlock] Map region generation failed at ({0}, {1}): {2}",
                regionX,
                regionZ,
                ex.InnerException?.Message ?? ex.Message);
        }
    }

    private static void ReapplyUniformLatitude(ICoreServerAPI api)
    {
        api.World.Calendar.OnGetLatitude = _ => SkyBlockClimate.SeasonLatitude;
    }

    private static bool NeedsClimateMap(IMapRegion region)
    {
        var climate = region.ClimateMap;
        return climate?.Data == null || climate.Data.Length == 0;
    }

    private static bool UsesLegacyClimateBackfill(ICoreServerAPI api) =>
        SkyBlockWorld.IsSkyBlockWorld(api)
        && SkyBlockWorld.LegacyWorldTypes.Any(worldType =>
            string.Equals(
                api.World.Config.GetString("worldType", api.WorldManager.SaveGame.WorldType),
                worldType,
                StringComparison.OrdinalIgnoreCase));
}