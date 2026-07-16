using System;
using System.Collections.Generic;
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

/// <summary>Standard worldgen без блоков в чанках + размещение островов из схематик.</summary>
public sealed partial class SwixySkyBlockServerMod
{
    private IslandTemplate? activeIsland;
    private BlockPos islandOrigin;
    private BlockPos islandSpawn;
    private int islandPlaced;
    private int islandPlacementVerified;
    private bool defaultSpawnApplied;
    private readonly object islandPlaceLock = new();
    private const string SpawnTraderMarkerBlockCode = "game:plaster-plain";
    private static readonly SpawnTraderDefinition[] SpawnTraders =
    [
        new("game:trader-male-skyblock-temperate", SkyBlockWorld.SaveKeySpawnTrader, -11, 0),
        new("game:trader-male-skyblockfood-temperate", SkyBlockWorld.SaveKeySpawnFoodTrader, 11, 0),
        new("game:trader-male-skyblockfarming-temperate", SkyBlockWorld.SaveKeySpawnFarmingTrader, 0, -11),
        new("game:trader-male-skyblocksurvival-temperate", SkyBlockWorld.SaveKeySpawnSurvivalTrader, 0, 11),
        new("game:trader-male-skyblockdecor-temperate", SkyBlockWorld.SaveKeySpawnDecorTrader, 11, 11),
        new("game:trader-male-skyblockanimals-temperate", SkyBlockWorld.SaveKeySpawnAnimalsTrader, -11, 11)
    ];

    private bool IsSkyBlockWorld =>
        serverApi != null && SkyBlockWorld.IsSkyBlockWorld(serverApi);

    private string GetWorldType() =>
        serverApi?.World.Config.GetString("worldType", serverApi.WorldManager.SaveGame.WorldType ?? "standard")
        ?? "standard";

    private bool skyBlockWorldGenRegistered;

    private void RegisterWorldGen(ICoreServerAPI api)
    {
        api.Event.SaveGameCreated += OnSaveGameCreated;
        api.Event.SaveGameLoaded += OnSaveGameLoaded;
        api.Event.ChunkColumnLoaded += OnChunkColumnLoaded;
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnRunGame);
        api.Event.WorldgenStartup += OnWorldgenStartup;
    }

    internal void EnsureSkyBlockWorldGenRegistered(ICoreServerAPI api)
    {
        if (skyBlockWorldGenRegistered)
        {
            return;
        }

        var worldType = SkyBlockWorld.WorldType;
        api.Event.InitWorldGenerator(() => OnSkyBlockInitWorldGenerator(api), worldType);
        SkyBlockWorldGenBootstrap.Bootstrap(api);

        api.Event.ChunkColumnGeneration(
            request => SkyBlockEmptyChunkGen.OnTerrainPass(api, request),
            EnumWorldGenPass.Terrain,
            worldType);
        api.Event.ChunkColumnGeneration(OnSunlightChunkColumn, EnumWorldGenPass.Vegetation, worldType);
        api.Event.ChunkColumnGeneration(OnSunlightNeighbourChunkColumn, EnumWorldGenPass.NeighbourSunLightFlood, worldType);
        api.Event.ChunkColumnGeneration(
            request => OnPlaceIslandPreDone(request),
            EnumWorldGenPass.PreDone,
            worldType);

        foreach (var legacyWorldType in SkyBlockWorld.LegacyWorldTypes)
        {
            api.Event.InitWorldGenerator(OnInitWorldGenerator, legacyWorldType);
            api.Event.ChunkColumnGeneration(OnSunlightChunkColumn, EnumWorldGenPass.Vegetation, legacyWorldType);
            api.Event.ChunkColumnGeneration(OnSunlightNeighbourChunkColumn, EnumWorldGenPass.NeighbourSunLightFlood, legacyWorldType);
        }

        skyBlockWorldGenRegistered = true;
        api.Logger.Notification(
            "[SwixySkyBlock] Skyblock world generator registered (empty chunk columns, cloned standard maps).");
    }

    private static void OnSkyBlockInitWorldGenerator(ICoreServerAPI api)
    {
        if (!SkyBlockWorldGenBootstrap.UsesSkyBlockWorldType(api))
        {
            return;
        }

        api.World.Calendar.OnGetLatitude = _ => SkyBlockClimate.SeasonLatitude;
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
        if (serverApi == null || !IsSkyBlockWorld)
        {
            return;
        }

        SkyBlockWorld.ApplyWorldConfig(serverApi);
        EnsureSkyBlockWorldGenRegistered(serverApi);
        TryApplyDefaultSpawn(serverApi);
    }

    private void OnRunGame()
    {
        TryApplyDefaultSpawn(serverApi);
        EnsureIslandPlaced(serverApi);
        EnsureSpawnTraders(serverApi);
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

        activeIsland = IslandBlueprint.LoadSpawn(api);
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

        activeIsland = IslandBlueprint.LoadSpawn(api);

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
            UpdateColumnTerrainHeight(request, bounds);
            RelightIslandColumn(serverApi, request);

            // Placement is deferred to EnsureIslandPlaced(), which uses a world
            // accessor after all intersecting columns have been loaded.
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

        var bounds = activeIsland.GetBounds(islandOrigin);
        const int chunkSize = GlobalConstants.ChunkSize;
        if (!ColumnIntersects(chunkCoord.X, chunkCoord.Y, bounds, chunkSize))
        {
            return;
        }

        if (IslandPlacer.IsPlacementComplete(serverApi.World.BlockAccessor, islandOrigin, activeIsland))
        {
            if (Volatile.Read(ref islandPlacementVerified) == 0)
            {
                MarkIslandPlaced(serverApi);
            }

            return;
        }

        lock (islandPlaceLock)
        {
            if (IslandPlacer.IsPlacementComplete(serverApi.World.BlockAccessor, islandOrigin, activeIsland))
            {
                if (Volatile.Read(ref islandPlacementVerified) == 0)
                {
                    MarkIslandPlaced(serverApi);
                }

                return;
            }

            return;
        }
    }

    private void MarkIslandPlaced(ICoreServerAPI api)
    {
        Volatile.Write(ref islandPlacementVerified, 1);
        Interlocked.Exchange(ref islandPlaced, 1);
        TryApplyDefaultSpawn(api);
        EnsureSpawnTraders(api);
    }

    private void EnsureSpawnTraders(ICoreServerAPI? api)
    {
        if (api == null
            || !IsSkyBlockWorld
            || activeIsland == null
            || UsePerPlayerIslands
            || Volatile.Read(ref islandPlacementVerified) == 0)
        {
            return;
        }

        var occupied = new List<BlockPos>();
        foreach (var trader in SpawnTraders)
        {
            if (api.WorldManager.SaveGame.GetData(trader.SaveKey) != null)
            {
                continue;
            }

            var entityType = api.World.GetEntityType(new AssetLocation(trader.EntityCode));
            if (entityType == null)
            {
                api.Logger.Warning("[SwixySkyBlock] Cannot spawn skyblock trader, missing entity type: {0}", trader.EntityCode);
                continue;
            }

            EnsureSpawnTraderMarker(api, trader);

            var spawnPos = FindSpawnTraderPosition(api, trader.OffsetX, trader.OffsetZ, occupied);
            if (spawnPos == null)
            {
                api.Logger.Warning("[SwixySkyBlock] Cannot find a safe plaster marker for skyblock trader near {0}.", islandSpawn);
                continue;
            }

            var entity = api.World.ClassRegistry.CreateEntity(entityType);
            entity.Pos.SetPos(spawnPos.X + 0.5, spawnPos.Y, spawnPos.Z + 0.5);
            entity.Pos.Yaw = GameMath.PIHALF;

            api.World.SpawnEntity(entity);
            api.WorldManager.SaveGame.StoreData(trader.SaveKey, [1]);
            occupied.Add(spawnPos);
            api.Logger.Notification("[SwixySkyBlock] SkyBlock trader {0} spawned at {1}.", trader.EntityCode, spawnPos);
        }
    }

    private BlockPos? FindSpawnTraderPosition(ICoreServerAPI api, int offsetX, int offsetZ, IReadOnlyList<BlockPos> occupied)
    {
        var anchor = new BlockPos(islandSpawn.X + offsetX, islandSpawn.Y, islandSpawn.Z + offsetZ);
        for (var radius = 0; radius <= 4; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dz) != radius)
                    {
                        continue;
                    }

                    for (var dy = 1; dy >= -2; dy--)
                    {
                        var pos = new BlockPos(anchor.X + dx, anchor.Y + dy, anchor.Z + dz);
                        if (IsClearTraderPosition(api, pos, occupied))
                        {
                            return pos;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static bool IsClearTraderPosition(ICoreServerAPI api, BlockPos pos, IReadOnlyList<BlockPos> occupied)
    {
        if (occupied.Any(existing => existing.X == pos.X && existing.Y == pos.Y && existing.Z == pos.Z))
        {
            return false;
        }

        var accessor = api.World.BlockAccessor;
        var ground = accessor.GetBlock(new BlockPos(pos.X, pos.Y - 1, pos.Z));
        if (!IsSpawnTraderMarkerBlock(ground))
        {
            return false;
        }

        var feet = accessor.GetBlock(pos);
        if (feet.Id != 0)
        {
            return false;
        }

        var head = accessor.GetBlock(new BlockPos(pos.X, pos.Y + 1, pos.Z));
        return head.Id == 0;
    }

    private void EnsureSpawnTraderMarker(ICoreServerAPI api, SpawnTraderDefinition trader)
    {
        var markerBlock = api.World.GetBlock(new AssetLocation(SpawnTraderMarkerBlockCode));
        if (markerBlock == null || markerBlock.Id == 0)
        {
            api.Logger.Warning("[SwixySkyBlock] Cannot place skyblock trader markers, missing block: {0}", SpawnTraderMarkerBlockCode);
            return;
        }

        var accessor = api.World.BlockAccessor;
        var feetPos = new BlockPos(islandSpawn.X + trader.OffsetX, islandSpawn.Y, islandSpawn.Z + trader.OffsetZ);
        var groundPos = new BlockPos(feetPos.X, feetPos.Y - 1, feetPos.Z);
        var headPos = new BlockPos(feetPos.X, feetPos.Y + 1, feetPos.Z);

        accessor.SetBlock(markerBlock.Id, groundPos);
        accessor.SetBlock(0, feetPos);
        accessor.SetBlock(0, headPos);
    }

    private static bool IsSpawnTraderMarkerBlock(Block block) =>
        string.Equals(block.Code?.Path, "plaster-plain", StringComparison.Ordinal);

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

        if (IslandPlacer.IsPlacementComplete(api.World.BlockAccessor, islandOrigin, activeIsland))
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
            if (IslandPlacer.IsPlacementComplete(api.World.BlockAccessor, islandOrigin, activeIsland))
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

    private readonly record struct SpawnTraderDefinition(string EntityCode, string SaveKey, int OffsetX, int OffsetZ);
}
