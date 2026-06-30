using System;
using System.Linq;
using System.Text;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixySkyBlock;

/// <summary>Генерация пустого мира и размещение острова из схематики.</summary>
public sealed partial class SwixySkyBlockMod
{
    private IslandTemplate? activeIsland;
    private BlockPos islandOrigin;
    private BlockPos islandSpawn;
    private int islandPlaced;
    private int islandPlacementVerified;
    private bool defaultSpawnApplied;
    private readonly object islandPlaceLock = new();

    private bool IsSkyBlockWorld =>
        serverApi != null && SkyBlockWorld.IsSkyBlockWorld(serverApi);

    private string GetWorldType() =>
        serverApi?.World.Config.GetString("worldType", serverApi.WorldManager.SaveGame.WorldType ?? "standard")
        ?? "standard";

    private void RegisterWorldGen(ICoreServerAPI api)
    {
        foreach (var worldType in SkyBlockWorld.SupportedWorldTypes)
        {
            api.Event.InitWorldGenerator(OnInitWorldGenerator, worldType);
            api.Event.ChunkColumnGeneration(OnSunlightChunkColumn, EnumWorldGenPass.Vegetation, worldType);
            api.Event.ChunkColumnGeneration(OnSunlightNeighbourChunkColumn, EnumWorldGenPass.NeighbourSunLightFlood, worldType);
        }

        api.Event.SaveGameCreated += OnSaveGameCreated;
        api.Event.SaveGameLoaded += OnSaveGameLoaded;
        api.Event.ChunkColumnLoaded += OnChunkColumnLoaded;
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnRunGame);
        api.Event.WorldgenStartup += OnWorldgenStartup;
    }

    private void OnSaveGameCreated()
    {
        if (serverApi == null || !SkyBlockWorld.IsSkyBlockWorld(serverApi))
        {
            return;
        }

        SkyBlockWorld.ApplyWorldConfig(serverApi);

        if (UsePerPlayerIslands)
        {
            serverApi.Logger.Notification("[SwixySkyBlock] Dedicated server: per-player islands via hub (I).");
        }
        else
        {
            SelectIslandForWorld(serverApi);
            TryApplyDefaultSpawn(serverApi);
        }

        serverApi.Logger.Notification(
            "[SwixySkyBlock] New world created (worldType={0}, playstyle={1}).",
            serverApi.WorldManager.SaveGame.WorldType,
            serverApi.World.Config.GetString("playstyle", serverApi.WorldManager.SaveGame.PlayStyle));
    }

    private void OnSaveGameLoaded()
    {
        if (!IsSkyBlockWorld || serverApi == null)
        {
            activeIsland = null;
            return;
        }

        if (UsePerPlayerIslands)
        {
            activeIsland = null;
            return;
        }

        RestoreIslandFromSave(serverApi);
    }

    private void OnInitWorldGenerator()
    {
        if (!IsSkyBlockWorld || serverApi == null)
        {
            return;
        }

        if (activeIsland == null)
        {
            SelectIslandForWorld(serverApi);
        }

        Interlocked.Exchange(ref islandPlaced, 0);
        Volatile.Write(ref islandPlacementVerified, 0);
        serverApi.Logger.Notification("[SwixySkyBlock] World generator ready, island '{0}'.", activeIsland?.Name ?? "?");
    }

    private void OnWorldgenStartup()
    {
        if (serverApi != null && SkyBlockWorld.IsSkyBlockWorld(serverApi))
        {
            SkyBlockWorld.ApplyWorldConfig(serverApi);
        }

        TryApplyDefaultSpawn(serverApi);
    }

    private void OnRunGame()
    {
        TryApplyDefaultSpawn(serverApi);
        EnsureIslandPlaced(serverApi);
    }

    private void TryApplyDefaultSpawn(ICoreServerAPI? api)
    {
        if (defaultSpawnApplied || api == null || !IsSkyBlockWorld || activeIsland == null)
        {
            return;
        }

        if (api.WorldManager.MapSizeX <= 0 || api.WorldManager.MapSizeZ <= 0)
        {
            return;
        }

        try
        {
            api.WorldManager.SetDefaultSpawnPosition(islandSpawn.X, islandSpawn.Y, islandSpawn.Z);
            api.WorldManager.SaveGame.DefaultSpawn = new PlayerSpawnPos(islandSpawn.X, islandSpawn.Y, islandSpawn.Z);
            defaultSpawnApplied = true;
            api.Logger.Notification("[SwixySkyBlock] Default spawn -> {0}", islandSpawn);
        }
        catch (Exception ex)
        {
            api.Logger.Debug("[SwixySkyBlock] Default spawn not ready yet: {0}", ex.Message);
        }
    }

    private void SelectIslandForWorld(ICoreServerAPI api)
    {
        ResolveIslandOrigin(api, forNewWorld: true);

        var templates = IslandBlueprint.LoadAll(api);
        activeIsland = IslandBlueprint.PickForWorld(templates);
        islandSpawn = activeIsland.GetSpawnPosition(islandOrigin);

        api.WorldManager.SaveGame.StoreData(
            SkyBlockWorld.SaveKeyIslandTemplate,
            Encoding.UTF8.GetBytes(activeIsland.Name));
        StoreIslandOrigin(api);

        api.Logger.Notification("[SwixySkyBlock] Island origin {0}, spawn {1}.", islandOrigin, islandSpawn);
    }

    private void RestoreIslandFromSave(ICoreServerAPI api)
    {
        ResolveIslandOrigin(api, forNewWorld: false);

        var data = api.WorldManager.SaveGame.GetData(SkyBlockWorld.SaveKeyIslandTemplate);
        var savedName = data != null ? Encoding.UTF8.GetString(data) : null;

        var templates = IslandBlueprint.LoadAll(api);
        activeIsland = string.IsNullOrEmpty(savedName)
            ? IslandBlueprint.PickForWorld(templates)
            : templates.FirstOrDefault(t => t.Name == savedName) ?? IslandBlueprint.PickForWorld(templates);

        islandSpawn = activeIsland.GetSpawnPosition(islandOrigin);
    }

    private void ResolveIslandOrigin(ICoreServerAPI api, bool forNewWorld)
    {
        if (TryLoadIslandOrigin(api, out islandOrigin))
        {
            return;
        }

        if (!forNewWorld && api.WorldManager.SaveGame.GetData(SkyBlockWorld.SaveKeyIslandTemplate) != null)
        {
            islandOrigin = SkyBlockWorld.ComputeIslandOrigin(api);
            return;
        }

        islandOrigin = SkyBlockWorld.ComputeIslandOrigin(api);
    }

    private void StoreIslandOrigin(ICoreServerAPI api)
    {
        var bytes = new byte[12];
        Buffer.BlockCopy(BitConverter.GetBytes(islandOrigin.X), 0, bytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(islandOrigin.Y), 0, bytes, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(islandOrigin.Z), 0, bytes, 8, 4);
        api.WorldManager.SaveGame.StoreData(SkyBlockWorld.SaveKeyIslandOrigin, bytes);
    }

    private static bool TryLoadIslandOrigin(ICoreServerAPI api, out BlockPos origin)
    {
        var bytes = api.WorldManager.SaveGame.GetData(SkyBlockWorld.SaveKeyIslandOrigin);
        if (bytes == null || bytes.Length < 12)
        {
            origin = new BlockPos();
            return false;
        }

        origin = new BlockPos(
            BitConverter.ToInt32(bytes, 0),
            BitConverter.ToInt32(bytes, 4),
            BitConverter.ToInt32(bytes, 8));
        return true;
    }

    internal void OnPlaceIslandPreDone(IChunkColumnGenerateRequest request)
    {
        if (!IsSkyBlockWorld || serverApi == null || activeIsland == null)
        {
            return;
        }

        var bounds = activeIsland.GetBounds(islandOrigin);
        if (!ColumnIntersects(request.ChunkX, request.ChunkZ, bounds, GlobalConstants.ChunkSize))
        {
            return;
        }

        try
        {
            if (!IslandPlacer.PlaceIntoColumn(serverApi, request, activeIsland, islandOrigin))
            {
                return;
            }

            UpdateColumnTerrainHeight(request, bounds);
            RelightIslandColumn(serverApi, request);

            if (Volatile.Read(ref islandPlacementVerified) != 0)
            {
                return;
            }

            if (!IslandPlacer.IsSurfacePresent(serverApi.World.BlockAccessor, islandOrigin, activeIsland))
            {
                return;
            }

            lock (islandPlaceLock)
            {
                if (Volatile.Read(ref islandPlacementVerified) != 0)
                {
                    return;
                }

                if (!IslandPlacer.IsSurfacePresent(serverApi.World.BlockAccessor, islandOrigin, activeIsland))
                {
                    return;
                }

                MarkIslandPlaced(serverApi);
                serverApi.Logger.Notification(
                    "[SwixySkyBlock] Placed island '{0}' at {1}, spawn {2}.",
                    activeIsland.Name,
                    islandOrigin,
                    islandSpawn);
            }
        }
        catch (Exception ex)
        {
            serverApi.Logger.Error("[SwixySkyBlock] Failed to place island during worldgen: {0}", ex);
        }
    }

    private void RelightIslandColumn(ICoreServerAPI api, IChunkColumnGenerateRequest request)
    {
        if (activeIsland == null)
        {
            return;
        }

        var bounds = activeIsland.GetBounds(islandOrigin);
        if (!ColumnIntersects(request.ChunkX, request.ChunkZ, bounds, GlobalConstants.ChunkSize))
        {
            return;
        }

        UpdateColumnTerrainHeight(request, bounds);
        api.WorldManager.SunFloodChunkColumnForWorldGen(request.Chunks, request.ChunkX, request.ChunkZ);
        api.WorldManager.SunFloodChunkColumnNeighboursForWorldGen(request.Chunks, request.ChunkX, request.ChunkZ);
    }

    private void OnSunlightChunkColumn(IChunkColumnGenerateRequest request)
    {
        if (!IsSkyBlockWorld || serverApi == null)
        {
            return;
        }

        serverApi.WorldManager.SunFloodChunkColumnForWorldGen(request.Chunks, request.ChunkX, request.ChunkZ);
    }

    private void OnSunlightNeighbourChunkColumn(IChunkColumnGenerateRequest request)
    {
        if (!IsSkyBlockWorld || serverApi == null)
        {
            return;
        }

        serverApi.WorldManager.SunFloodChunkColumnNeighboursForWorldGen(request.Chunks, request.ChunkX, request.ChunkZ);
    }

    private void UpdateColumnTerrainHeight(IChunkColumnGenerateRequest request, Cuboidi bounds)
    {
        if (activeIsland == null || !ColumnIntersects(request.ChunkX, request.ChunkZ, bounds, GlobalConstants.ChunkSize))
        {
            return;
        }

        var topY = islandOrigin.Y + activeIsland.Schematic.SizeY - 1;
        var mapChunk = request.Chunks[0].MapChunk;
        if (topY > mapChunk.YMax)
        {
            mapChunk.YMax = (ushort)topY;
        }
    }

    private void OnChunkColumnLoaded(Vec2i chunkCoord, IWorldChunk[] chunks)
    {
        if (!IsSkyBlockWorld || serverApi == null)
        {
            return;
        }

        TryPlaceRegisteredIslandsOnChunkLoad(chunkCoord, chunks);

        if (activeIsland == null)
        {
            return;
        }

        if (Volatile.Read(ref islandPlacementVerified) != 0)
        {
            return;
        }

        var bounds = activeIsland.GetBounds(islandOrigin);
        const int chunkSize = GlobalConstants.ChunkSize;
        if (!ColumnIntersects(chunkCoord.X, chunkCoord.Y, bounds, chunkSize))
        {
            return;
        }

        if (IslandPlacer.IsSurfacePresent(serverApi.World.BlockAccessor, islandOrigin, activeIsland))
        {
            MarkIslandPlaced(serverApi);
            return;
        }

        lock (islandPlaceLock)
        {
            if (IslandPlacer.IsSurfacePresent(serverApi.World.BlockAccessor, islandOrigin, activeIsland))
            {
                MarkIslandPlaced(serverApi);
                return;
            }

            if (!IslandPlacer.PlaceIntoLoadedColumn(serverApi, chunkCoord, chunks, activeIsland, islandOrigin))
            {
                return;
            }

            RelightIsland(serverApi);
            MarkIslandPlaced(serverApi);
            serverApi.Logger.Notification("[SwixySkyBlock] Island placed on chunk load at {0}.", islandOrigin);
        }
    }

    private void MarkIslandPlaced(ICoreServerAPI api)
    {
        Volatile.Write(ref islandPlacementVerified, 1);
        Interlocked.Exchange(ref islandPlaced, 1);
        TryApplyDefaultSpawn(api);
    }

    private void RelightIsland(ICoreServerAPI api)
    {
        var bounds = activeIsland!.GetBounds(islandOrigin);
        api.WorldManager.FullRelight(
            new BlockPos(bounds.X1, bounds.Y1, bounds.Z1),
            new BlockPos(bounds.X2, bounds.Y2, bounds.Z2),
            sendToClients: true);
    }

    private void EnsureIslandPlaced(ICoreServerAPI? api)
    {
        if (api == null || !IsSkyBlockWorld || activeIsland == null || UsePerPlayerIslands)
        {
            return;
        }

        if (Volatile.Read(ref islandPlacementVerified) != 0)
        {
            return;
        }

        if (IslandPlacer.IsSurfacePresent(api.World.BlockAccessor, islandOrigin, activeIsland))
        {
            MarkIslandPlaced(api);
            return;
        }

        var bounds = activeIsland.GetBounds(islandOrigin);
        const int chunkSize = GlobalConstants.ChunkSize;
        var cx1 = bounds.X1 / chunkSize;
        var cx2 = bounds.X2 / chunkSize;
        var cz1 = bounds.Z1 / chunkSize;
        var cz2 = bounds.Z2 / chunkSize;

        lock (islandPlaceLock)
        {
            if (IslandPlacer.IsSurfacePresent(api.World.BlockAccessor, islandOrigin, activeIsland))
            {
                MarkIslandPlaced(api);
                return;
            }

            try
            {
                for (var cx = cx1; cx <= cx2; cx++)
                {
                    for (var cz = cz1; cz <= cz2; cz++)
                    {
                        api.WorldManager.LoadChunkColumn(cx, cz, keepLoaded: true);
                    }
                }

                if (!IslandPlacer.PlaceIntoWorld(api, activeIsland, islandOrigin))
                {
                    api.Logger.Warning("[SwixySkyBlock] Fallback island placement failed at {0}.", islandOrigin);
                    return;
                }

                RelightIsland(api);
                MarkIslandPlaced(api);
                api.Logger.Notification("[SwixySkyBlock] Island placed via fallback at {0}.", islandOrigin);
            }
            catch (Exception ex)
            {
                api.Logger.Error("[SwixySkyBlock] Failed to place island via fallback: {0}", ex);
            }
        }
    }

    private static bool ColumnIntersects(int chunkX, int chunkZ, Cuboidi bounds, int chunkSize)
    {
        var minX = chunkX * chunkSize;
        var maxX = minX + chunkSize - 1;
        var minZ = chunkZ * chunkSize;
        var maxZ = minZ + chunkSize - 1;

        return maxX >= bounds.X1 && minX <= bounds.X2 && maxZ >= bounds.Z1 && minZ <= bounds.Z2;
    }
}
