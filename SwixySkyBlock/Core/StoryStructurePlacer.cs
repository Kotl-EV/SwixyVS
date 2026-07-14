using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace SwixySkyBlock;

internal enum StoryStructureAdvanceResult
{
    InProgress,
    WaitingForChunk,
    Succeeded,
    Failed
}

/// <summary>Сессия пошагового размещения одной ванильной сюжетной структуры.</summary>
internal sealed class StoryStructurePlacementSession
{
    private readonly ICoreServerAPI api;
    private readonly StoryDungeonDefinition definition;
    private readonly BlockPos center;
    private readonly BlockPos fallbackSpawn;
    private readonly WorldGenStoryStructure structure;
    private readonly BlockSchematicPartial schematic;
    private readonly BlockPos startPos;
    private readonly Cuboidi location;
    private readonly Block rockBlock;
    private readonly IWorldGenBlockAccessor? wgenAccessor;
    private readonly List<(int cx, int cz)> columns;
    private int columnIndex;
    private int totalPlaced;
    private bool finalized;

    public BlockPos? Spawn { get; private set; }

    private StoryStructurePlacementSession(
        ICoreServerAPI api,
        StoryDungeonDefinition definition,
        BlockPos center,
        WorldGenStoryStructure structure,
        BlockSchematicPartial schematic,
        BlockPos startPos,
        Cuboidi location,
        Block rockBlock,
        IWorldGenBlockAccessor? wgenAccessor)
    {
        this.api = api;
        this.definition = definition;
        this.center = center;
        this.fallbackSpawn = center.Copy();
        this.structure = structure;
        this.schematic = schematic;
        this.startPos = startPos;
        this.location = location;
        this.rockBlock = rockBlock;
        this.wgenAccessor = wgenAccessor;
        columns = StoryStructurePlacer.BuildColumnList(location);
    }

    public static StoryStructurePlacementSession? TryCreate(
        ICoreServerAPI api,
        StoryDungeonDefinition definition,
        BlockPos center)
    {
        var storyGen = api.ModLoader.GetModSystem<GenStoryStructures>();
        if (storyGen == null || StoryStructureConfigLoader.EnsureLoaded(api, storyGen)?.Structures == null)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] GenStoryStructures is unavailable; cannot place story site '{0}'.",
                definition.Code);
            return null;
        }

        var structure = storyGen.scfg.Structures.FirstOrDefault(s =>
            string.Equals(s.Code, definition.StructureCode, StringComparison.OrdinalIgnoreCase));
        if (structure == null)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Vanilla story structure '{0}' not found in storystructures.json.",
                definition.StructureCode);
            return null;
        }

        var surfaceY = StoryStructurePlacer.SampleIslandSurfaceY(api, center);
        var rockBlock = StoryStructurePlacer.ResolveRockBlock(api, structure, center, surfaceY)
            ?? api.World.GetBlock(new AssetLocation("game:rock-granite"))!;
        var schematic = StorySchematicSkyBlockPrep.Prepare(api, structure, rockBlock);
        if (!StoryStructurePlacer.TryComputeStartPos(definition, structure, center, surfaceY, out var startPos, out var location))
        {
            return null;
        }

        StoryStructurePlacer.RegisterStoryLocation(storyGen, structure, center, location);

        var wgenAccessor = StoryStructureRuntime.TryGetWorldgenAccessor(api);
        if (wgenAccessor == null)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Worldgen block accessor unavailable; cannot place story site '{0}'.",
                definition.Code);
            return null;
        }

        return new StoryStructurePlacementSession(
            api,
            definition,
            center,
            structure,
            schematic,
            startPos,
            location,
            rockBlock,
            wgenAccessor);
    }

    public StoryStructureAdvanceResult TryAdvanceColumn()
    {
        if (finalized)
        {
            return Spawn != null ? StoryStructureAdvanceResult.Succeeded : StoryStructureAdvanceResult.Failed;
        }

        if (columnIndex >= columns.Count)
        {
            return FinalizePlacement();
        }

        var (cx, cz) = columns[columnIndex++];
        var chunks = StoryStructurePlacer.CollectColumnChunks(api, cx, cz);
        if (chunks == null)
        {
            columnIndex--;
            api.WorldManager.LoadChunkColumnFast(cx, cz, new ChunkLoadOptions { KeepLoaded = false });
            return StoryStructureAdvanceResult.WaitingForChunk;
        }

        totalPlaced += StoryStructurePlacer.PlaceSingleColumn(
            api,
            structure,
            schematic,
            startPos,
            location,
            rockBlock,
            wgenAccessor!,
            cx,
            cz);
        return StoryStructureAdvanceResult.InProgress;
    }

    private StoryStructureAdvanceResult FinalizePlacement()
    {
        finalized = true;
        if (totalPlaced <= 0)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Column placement placed 0 blocks for '{0}' at {1}; retrying partial placement.",
                definition.Code,
                startPos);
            totalPlaced = StoryStructurePlacer.TryPartialPlaceStructure(
                api,
                structure,
                schematic,
                startPos,
                location,
                rockBlock,
                wgenAccessor!);
        }

        if (totalPlaced <= 0)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Vanilla story placement for '{0}' placed 0 blocks at {1}.",
                definition.Code,
                startPos);
            return StoryStructureAdvanceResult.Failed;
        }

        StoryStructurePlacer.PlaceEntities(api, schematic, startPos, rockBlock);
        StoryStructurePlacer.ApplyLandClaims(api, structure, location);
        Spawn = StoryStructurePlacer.ComputeSpawn(api, startPos, schematic, location);
        api.Logger.Notification(
            "[SwixySkyBlock] Vanilla story structure '{0}' ({1}) placed {2} blocks at {3}, spawn {4}.",
            definition.Code,
            definition.StructureCode,
            totalPlaced,
            startPos,
            Spawn);
        api.Event.RegisterCallback(
            _ => StoryStructurePlacer.RelightBounds(api, location),
            StoryStructurePlacer.RelightDelayMs);
        return StoryStructureAdvanceResult.Succeeded;
    }
}

/// <summary>Размещение ванильных сюжетных структур через PlacePartial (как GenStoryStructures).</summary>
internal static class StoryStructurePlacer
{
    private const int SliceDelayMs = 200;
    internal const int RelightDelayMs = 4000;

    public static void PlaceSpread(
        ICoreServerAPI api,
        StoryDungeonDefinition definition,
        BlockPos center,
        Action<bool, BlockPos> onComplete)
    {
        var fallbackSpawn = center.Copy();

        var storyGen = api.ModLoader.GetModSystem<GenStoryStructures>();
        if (storyGen == null || StoryStructureConfigLoader.EnsureLoaded(api, storyGen)?.Structures == null)
        {
            api.Logger.Warning("[SwixySkyBlock] GenStoryStructures is unavailable; cannot place story site '{0}'.", definition.Code);
            onComplete(false, fallbackSpawn);
            return;
        }

        var structure = storyGen.scfg.Structures.FirstOrDefault(s =>
            string.Equals(s.Code, definition.StructureCode, StringComparison.OrdinalIgnoreCase));
        if (structure == null)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Vanilla story structure '{0}' not found in storystructures.json.",
                definition.StructureCode);
            onComplete(false, fallbackSpawn);
            return;
        }

        var surfaceY = SampleIslandSurfaceY(api, center);
        var rockBlock = ResolveRockBlock(api, structure, center, surfaceY)
            ?? api.World.GetBlock(new AssetLocation("game:rock-granite"))!;
        var schematic = StorySchematicSkyBlockPrep.Prepare(api, structure, rockBlock);
        if (!TryComputeStartPos(definition, structure, center, surfaceY, out var startPos, out var location))
        {
            onComplete(false, fallbackSpawn);
            return;
        }

        RegisterStoryLocation(storyGen, structure, center, location);

        var wgenAccessor = StoryStructureRuntime.TryGetWorldgenAccessor(api);
        if (wgenAccessor == null)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Worldgen block accessor unavailable; cannot place story site '{0}'.",
                definition.Code);
            onComplete(false, fallbackSpawn);
            return;
        }

        var columns = BuildColumnList(location);
        var totalPlaced = 0;
        var columnIndex = 0;

        void PlaceNextColumn()
        {
            if (columnIndex >= columns.Count)
            {
                if (totalPlaced <= 0)
                {
                    api.Logger.Warning(
                        "[SwixySkyBlock] Vanilla story placement for '{0}' placed 0 blocks at {1}.",
                        definition.Code,
                        startPos);
                    onComplete(false, fallbackSpawn);
                    return;
                }

                Schedule(api, () =>
                {
                    PlaceEntities(api, schematic, startPos, rockBlock);
                    ApplyLandClaims(api, structure, location);
                    var spawn = ComputeSpawn(api, startPos, schematic, location);
                    api.Logger.Notification(
                        "[SwixySkyBlock] Vanilla story structure '{0}' ({1}) placed {2} blocks at {3}, spawn {4}.",
                        definition.Code,
                        definition.StructureCode,
                        totalPlaced,
                        startPos,
                        spawn);
                    onComplete(true, spawn);
                    Schedule(api, () => RelightBounds(api, location), RelightDelayMs);
                });
                return;
            }

            var (cx, cz) = columns[columnIndex++];
            totalPlaced += PlaceSingleColumn(
                api,
                structure,
                schematic,
                startPos,
                location,
                rockBlock,
                wgenAccessor,
                cx,
                cz);
            Schedule(api, PlaceNextColumn);
        }

        PlaceNextColumn();
    }

    public static BlockPos ComputeSpawn(
        ICoreServerAPI api,
        BlockPos startPos,
        BlockSchematic schematic,
        Cuboidi location)
    {
        // Игрок ставится в центре острова НАД блоками земли (на поверхности)
        var centerPos = location.Center.AsBlockPos;
        
        // Находим поверхность по центру острова - находим Y блока земли
        var accessor = api.World.BlockAccessor;
        int surfaceY = centerPos.Y;
        for (int y = centerPos.Y + 128; y >= centerPos.Y - 64; y--)
        {
            if (accessor.GetBlock(centerPos.X, y, centerPos.Z).Id != 0)
            {
                surfaceY = y;
                break;
            }
        }
        
        // Проверяем, является ли центр острова безопасной позицией для спавна на поверхности
        var spawnPos = new BlockPos(centerPos.X, surfaceY + 1, centerPos.Z);
        if (IsSafeStandingPosition(api, spawnPos))
        {
            return spawnPos;
        }

        // Если центр не безопасен, ищем безопасную позицию рядом с центром на поверхности
        var safeNearCenter = FindSafeStandingNearOnSurface(api, new BlockPos(centerPos.X, surfaceY + 1, centerPos.Z), maxRadius: 32);
        if (safeNearCenter != null)
        {
            return safeNearCenter;
        }

        // Если даже рядом нет безопасной позиции, возвращаем fallback позицию в центре на поверхности
        return new BlockPos(centerPos.X, surfaceY + 1, centerPos.Z);
    }

    /// <summary>Вычисляет позицию для генератора ресурсов (над блоками земли по центру острова).</summary>
    public static BlockPos ComputeResourceGeneratorSpawn(ICoreServerAPI api, Cuboidi location)
    {
        // Генератор ресурсов ставится в центре острова НАД блоками земли (на поверхности)
        var centerPos = location.Center.AsBlockPos;
        
        // Ищем поверхность по центру острова - находим Y блока земли
        var accessor = api.World.BlockAccessor;
        int surfaceY = centerPos.Y;
        for (int y = centerPos.Y + 128; y >= centerPos.Y - 64; y--)
        {
            if (accessor.GetBlock(centerPos.X, y, centerPos.Z).Id != 0)
            {
                surfaceY = y;
                break;
            }
        }
        
        // Возвращаем позицию в центре острова НАД блоком земли (на поверхности)
        return new BlockPos(centerPos.X, surfaceY + 1, centerPos.Z);
    }

    public static BlockPos ResolveSafeStorySpawn(ICoreServerAPI api, BlockPos preferred, BlockPos fallbackCenter)
    {
        // Предпочитаем позицию в центре острова на поверхности (над блоками земли)
        if (IsSafeStandingPosition(api, preferred))
        {
            return preferred;
        }

        // Ищем безопасную позицию рядом с центром на поверхности
        var safeNearPreferred = FindSafeStandingNear(api, preferred, maxRadius: 20);
        if (safeNearPreferred != null)
        {
            return safeNearPreferred;
        }

        // Пробуем найти безопасную позицию рядом с fallback центром
        var safeNearFallback = FindSafeStandingNear(api, fallbackCenter, maxRadius: 48);
        if (safeNearFallback != null)
        {
            return safeNearFallback;
        }

        // Fallback: возвращаем позицию в центре на поверхности
        return fallbackCenter.UpCopy(2);
    }

    private static BlockPos? FindSafeStandingInBounds(ICoreServerAPI api, Cuboidi location)
    {
        var centerX = location.Center.X;
        var centerZ = location.Center.Z;
        var maxRadius = Math.Max(location.X2 - location.X1, location.Z2 - location.Z1) / 2 + 6;

        for (var radius = 0; radius <= maxRadius; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    if (radius > 0 && Math.Abs(dx) != radius && Math.Abs(dz) != radius)
                    {
                        continue;
                    }

                    var x = centerX + dx;
                    var z = centerZ + dz;
                    for (var y = location.Y2 + 4; y >= location.Y1 - 1; y--)
                    {
                        var feet = new BlockPos(x, y, z);
                        if (IsSafeStandingPosition(api, feet))
                        {
                            return feet;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static BlockPos? FindSafeStandingNear(ICoreServerAPI api, BlockPos anchor, int maxRadius)
    {
        for (var radius = 0; radius <= maxRadius; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    if (radius > 0 && Math.Abs(dx) != radius && Math.Abs(dz) != radius)
                    {
                        continue;
                    }

                    for (var dy = 4; dy >= -8; dy--)
                    {
                        var feet = new BlockPos(anchor.X + dx, anchor.Y + dy, anchor.Z + dz);
                        if (IsSafeStandingPosition(api, feet))
                        {
                            return feet;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static BlockPos? FindSafeStandingNearOnSurface(ICoreServerAPI api, BlockPos anchor, int maxRadius)
    {
        var accessor = api.World.BlockAccessor;
        
        // Ищем поверхность рядом с якорем
        var surfaceY = anchor.Y;
        for (var y = anchor.Y + 128; y >= anchor.Y - 64; y--)
        {
            if (accessor.GetBlock(anchor.X, y, anchor.Z).Id != 0)
            {
                surfaceY = y;
                break;
            }
        }

        for (var radius = 0; radius <= maxRadius; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    if (radius > 0 && Math.Abs(dx) != radius && Math.Abs(dz) != radius)
                    {
                        continue;
                    }

                    var x = anchor.X + dx;
                    var z = anchor.Z + dz;
                    
                    // Проверяем, есть ли поверхность в этой точке
                    var surfaceFound = false;
                    for (var y = surfaceY + 128; y >= surfaceY - 64; y--)
                    {
                        if (accessor.GetBlock(x, y, z).Id != 0)
                        {
                            surfaceFound = true;
                            break;
                        }
                    }

                    if (!surfaceFound)
                    {
                        continue;
                    }

                    // Ищем безопасную позицию на поверхности
                    for (var dy = 4; dy >= -8; dy--)
                    {
                        var feet = new BlockPos(x, surfaceY + dy, z);
                        if (IsSafeStandingPosition(api, feet))
                        {
                            return feet;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static bool IsSafeStandingPosition(ICoreServerAPI api, BlockPos feet)
    {
        var accessor = api.World.BlockAccessor;
        if (accessor.GetBlock(feet.DownCopy()).Id == 0)
        {
            return false;
        }

        if (accessor.GetBlock(feet).Id != 0)
        {
            return false;
        }

        return accessor.GetBlock(feet.UpCopy()).Id == 0;
    }


    internal static List<(int cx, int cz)> BuildColumnList(Cuboidi location)
    {
        const int chunkSize = GlobalConstants.ChunkSize;
        var cx1 = location.X1 / chunkSize;
        var cx2 = location.X2 / chunkSize;
        var cz1 = location.Z1 / chunkSize;
        var cz2 = location.Z2 / chunkSize;
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

    internal static List<(int cx, int cz)> BuildDiskColumnList(BlockPos center, int radius)
    {
        const int chunkSize = GlobalConstants.ChunkSize;
        var cx1 = (center.X - radius) / chunkSize;
        var cx2 = (center.X + radius) / chunkSize;
        var cz1 = (center.Z - radius) / chunkSize;
        var cz2 = (center.Z + radius) / chunkSize;
        var columns = new List<(int, int)>();

        for (var cx = cx1; cx <= cx2; cx++)
        {
            for (var cz = cz1; cz <= cz2; cz++)
            {
                var chunkCenterX = cx * chunkSize + chunkSize / 2;
                var chunkCenterZ = cz * chunkSize + chunkSize / 2;
                var dx = chunkCenterX - center.X;
                var dz = chunkCenterZ - center.Z;
                if (dx * dx + dz * dz <= (radius + chunkSize) * (radius + chunkSize))
                {
                    columns.Add((cx, cz));
                }
            }
        }

        return columns;
    }

    internal static int PlaceSingleColumn(
        ICoreServerAPI api,
        WorldGenStoryStructure structure,
        BlockSchematicPartial schematic,
        BlockPos startPos,
        Cuboidi location,
        Block rockBlock,
        IWorldGenBlockAccessor wgenAccessor,
        int cx,
        int cz)
    {
        var chunks = CollectColumnChunks(api, cx, cz);
        if (chunks == null)
        {
            return 0;
        }

        if (chunks.OfType<IServerChunk>().ToArray() is { Length: > 0 } serverChunks)
        {
            SkyBlockRockStrataGen.RefreshLoadedColumn(api, cx, cz, serverChunks);
        }

        SeedTerrainHeightMap(cx, cz, chunks, location, structure.Placement);
        var placed = PlaceColumnPartial(
            api,
            structure,
            schematic,
            startPos,
            rockBlock,
            cx,
            cz,
            chunks,
            wgenAccessor);
        return placed > 0
            ? placed
            : PlaceColumnBulkFallback(api, structure.Code, schematic, startPos, cx, cz, chunks);
    }

    internal static int TryPartialPlaceStructure(
        ICoreServerAPI api,
        WorldGenStoryStructure structure,
        BlockSchematicPartial schematic,
        BlockPos startPos,
        Cuboidi location,
        Block rockBlock,
        IWorldGenBlockAccessor wgenAccessor)
    {
        var placedBefore = CountStructureBlocks(api, location);
        var columns = BuildColumnList(location);
        foreach (var (cx, cz) in columns)
        {
            var chunks = CollectColumnChunks(api, cx, cz);
            if (chunks == null)
            {
                api.WorldManager.LoadChunkColumnFast(cx, cz, new ChunkLoadOptions { KeepLoaded = false });
                continue;
            }

            if (chunks.OfType<IServerChunk>().ToArray() is { Length: > 0 } serverChunks)
            {
                SkyBlockRockStrataGen.RefreshLoadedColumn(api, cx, cz, serverChunks);
            }

            SeedTerrainHeightMap(cx, cz, chunks, location, structure.Placement);
            PlaceColumnPartial(
                api,
                structure,
                schematic,
                startPos,
                rockBlock,
                cx,
                cz,
                chunks,
                wgenAccessor);
        }

        var delta = Math.Max(0, CountStructureBlocks(api, location) - placedBefore);
        if (delta > 0)
        {
            api.Logger.Notification(
                "[SwixySkyBlock] Partial fallback placed ~{0} blocks for story structure at {1}.",
                delta,
                startPos);
        }

        return delta;
    }

    internal static int PlaceStructureColumn(
        ICoreServerAPI api,
        WorldGenStoryStructure structure,
        BlockSchematicPartial schematic,
        BlockPos startPos,
        Block rockBlock,
        IWorldGenBlockAccessor wgenAccessor,
        int cx,
        int cz,
        IServerChunk[] chunks)
    {
        var placed = PlaceColumnPartial(
            api,
            structure,
            schematic,
            startPos,
            rockBlock,
            cx,
            cz,
            chunks,
            wgenAccessor);
        if (placed <= 0)
        {
            placed = PlaceColumnBulkFallback(api, structure.Code, schematic, startPos, cx, cz, chunks);
        }

        foreach (var chunk in chunks)
        {
            chunk.MarkModified();
        }

        return placed;
    }

    private static int PlaceColumnBulkFallback(
        ICoreServerAPI api,
        string structureCode,
        BlockSchematicPartial schematic,
        BlockPos startPos,
        int cx,
        int cz,
        IServerChunk[] chunks)
    {
        var expectedBlocks = CountSchematicBlocksInColumn(schematic, startPos, cx, cz);
        if (expectedBlocks <= 0)
        {
            return 0;
        }

        if (api.World.GetBlockAccessorMapChunkLoading(true, true) is not IBulkBlockAccessor bulk)
        {
            return 0;
        }

        bulk.SetChunks(new Vec2i(cx, cz), chunks);
        try
        {
            schematic.LoadMetaInformationAndValidate(bulk, api.World, structureCode);
            schematic.Place(bulk, api.World, startPos, EnumReplaceMode.ReplaceAll, true);
            bulk.Commit();
        }
        catch (Exception ex)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Bulk fallback column ({0}, {1}) failed: {2}",
                cx,
                cz,
                ex.InnerException?.Message ?? ex.Message);
            return 0;
        }

        return expectedBlocks;
    }

    internal static int CountStructureBlocks(ICoreServerAPI api, Cuboidi bounds)
    {
        var count = 0;
        var accessor = api.World.BlockAccessor;
        for (var x = bounds.X1; x <= bounds.X2; x++)
        {
            for (var y = bounds.Y1; y <= bounds.Y2; y++)
            {
                for (var z = bounds.Z1; z <= bounds.Z2; z++)
                {
                    if (accessor.GetBlock(x, y, z).Id != 0)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private static int PlaceColumnPartial(
        ICoreServerAPI api,
        WorldGenStoryStructure structure,
        BlockSchematicPartial schematic,
        BlockPos startPos,
        Block rockBlock,
        int cx,
        int cz,
        IServerChunk[] chunks,
        IWorldGenBlockAccessor wgenAccessor)
    {
        var expectedBlocks = CountSchematicBlocksInColumn(schematic, startPos, cx, cz);
        if (expectedBlocks <= 0)
        {
            return 0;
        }

        try
        {
            schematic.PlacePartial(
                chunks,
                wgenAccessor,
                api.World,
                cx,
                cz,
                startPos,
                EnumReplaceMode.ReplaceAll,
                structure.Placement,
                true,
                true,
                StoryStructureRuntime.GetRockRemaps(structure),
                StoryStructureRuntime.GetReplaceLayerBlockIds(structure),
                rockBlock,
                true);
        }
        catch (Exception ex)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Partial story column ({0}, {1}) failed: {2}",
                cx,
                cz,
                ex.InnerException?.Message ?? ex.Message);
            return 0;
        }

        return expectedBlocks;
    }

    private static int CountSchematicBlocksInColumn(
        BlockSchematicPartial schematic,
        BlockPos startPos,
        int cx,
        int cz)
    {
        const int chunkSize = GlobalConstants.ChunkSize;
        var minX = cx * chunkSize;
        var maxX = minX + chunkSize - 1;
        var minZ = cz * chunkSize;
        var maxZ = minZ + chunkSize - 1;
        var count = Math.Min(schematic.Indices.Count, schematic.BlockIds.Count);

        var blocks = 0;
        for (var i = 0; i < count; i++)
        {
            if (schematic.BlockIds[i] == 0)
            {
                continue;
            }

            var packed = schematic.Indices[i];
            var worldX = startPos.X + (int)(packed & 0x3ff);
            var worldZ = startPos.Z + (int)((packed >> 10) & 0x3ff);
            if (worldX >= minX && worldX <= maxX && worldZ >= minZ && worldZ <= maxZ)
            {
                blocks++;
            }
        }

        return blocks;
    }

    private static void Schedule(ICoreServerAPI api, Action work, int delayMs = SliceDelayMs)
    {
        api.Event.RegisterCallback(_ => work(), delayMs);
    }

    internal static void PlaceEntities(
        ICoreServerAPI api,
        BlockSchematicPartial schematic,
        BlockPos startPos,
        Block? rockBlock)
    {
        var rockBlockId = rockBlock?.BlockId ?? api.World.GetBlock(new AssetLocation("game:rock-granite"))?.BlockId ?? 0;
        var entityAccessor = api.World.GetBlockAccessorBulkUpdate(true, true);
        schematic.PlaceEntitiesAndBlockEntities(
            entityAccessor,
            api.World,
            startPos,
            schematic.BlockCodes,
            schematic.ItemCodes,
            replaceBlockEntities: true,
            replaceBlocks: null,
            centerrockblockid: rockBlockId,
            layerBlockForBlockEntities: null,
            resolveImports: true);
        entityAccessor.Commit();
    }

    internal static bool TryComputeStartPos(
        StoryDungeonDefinition definition,
        WorldGenStoryStructure structure,
        BlockPos center,
        int surfaceY,
        out BlockPos startPos,
        out Cuboidi location)
    {
        var schematic = StoryStructureRuntime.GetSchematic(structure);
        var minX = center.X - schematic.SizeX / 2;
        var minZ = center.Z - schematic.SizeZ / 2;
        var originY = structure.Placement switch
        {
            EnumStructurePlacement.SurfaceRuin when schematic.PathwayStarts is { Length: > 0 } =>
                surfaceY - schematic.PathwayStarts[0].Y,
            EnumStructurePlacement.SurfaceRuin => surfaceY - schematic.SizeY + schematic.OffsetY,
            EnumStructurePlacement.Surface => surfaceY + schematic.OffsetY,
            EnumStructurePlacement.Underground => center.Y - schematic.SizeY + definition.EntranceRevealBlocks,
            _ => surfaceY + schematic.OffsetY
        };
        originY += definition.SchematicYOffset;

        startPos = new BlockPos(minX, originY, minZ);
        location = new Cuboidi(
            startPos.X,
            startPos.Y,
            startPos.Z,
            startPos.X + schematic.SizeX - 1,
            startPos.Y + schematic.SizeY - 1,
            startPos.Z + schematic.SizeZ - 1);
        return true;
    }

    internal static int SampleIslandSurfaceY(ICoreServerAPI api, BlockPos center)
    {
        var accessor = api.World.BlockAccessor;
        for (var y = center.Y + 8; y >= center.Y - 96; y--)
        {
            if (accessor.GetBlock(center.X, y, center.Z).Id != 0)
            {
                return y;
            }
        }

        return center.Y;
    }

    internal static Block ResolveRockBlock(
        ICoreServerAPI api,
        WorldGenStoryStructure structure,
        BlockPos center,
        int surfaceY)
    {
        if (StoryStructureRuntime.GetRockRemaps(structure) == null)
        {
            return api.World.GetBlock(new AssetLocation("game:rock-granite"));
        }

        var accessor = api.World.BlockAccessor;
        for (var y = surfaceY; y >= surfaceY - 24; y--)
        {
            var block = accessor.GetBlock(center.X, y, center.Z);
            if (block.BlockMaterial == EnumBlockMaterial.Stone)
            {
                return block;
            }
        }

        return api.World.GetBlock(new AssetLocation("game:rock-granite"))!;
    }

    internal static void RegisterStoryLocation(
        GenStoryStructures storyGen,
        WorldGenStoryStructure structure,
        BlockPos center,
        Cuboidi location)
    {
        var strucloc = new StoryStructureLocation
        {
            Code = structure.Code,
            CenterPos = center.Copy(),
            Location = location.Clone(),
            LandformRadius = structure.LandformRadius,
            GenerationRadius = structure.GenerationRadius,
            SkipGenerationFlags = structure.SkipGenerationFlags,
            DidGenerate = true
        };

        storyGen.Structures.Set(structure.Code, strucloc);
        storyGen.StoryStructureInstancesDirty = true;
    }

    internal static void ApplyLandClaims(ICoreServerAPI api, WorldGenStoryStructure structure, Cuboidi location)
    {
        if (!structure.BuildProtected)
        {
            return;
        }

        if (!structure.ExcludeSchematicSizeProtect)
        {
            TryAddClaim(api, structure, location);
        }

        if (structure.ExtraLandClaimX > 0 && structure.ExtraLandClaimZ > 0)
        {
            var expanded = new Cuboidi(
                location.Center.X - structure.ExtraLandClaimX,
                0,
                location.Center.Z - structure.ExtraLandClaimZ,
                location.Center.X + structure.ExtraLandClaimX,
                api.WorldManager.MapSizeY,
                location.Center.Z + structure.ExtraLandClaimZ);
            TryAddClaim(api, structure, expanded);
        }

        if (structure.CustomLandClaims == null)
        {
            return;
        }

        foreach (var custom in structure.CustomLandClaims)
        {
            var cuboid = custom.Clone();
            cuboid.X1 += location.X1;
            cuboid.X2 += location.X1;
            cuboid.Y1 += location.Y1;
            cuboid.Y2 += location.Y1;
            cuboid.Z1 += location.Z1;
            cuboid.Z2 += location.Z1;
            TryAddClaim(api, structure, cuboid);
        }
    }

    private static void TryAddClaim(ICoreServerAPI api, WorldGenStoryStructure structure, Cuboidi area)
    {
        var existing = api.World.Claims.Get(area.Center.AsBlockPos);
        if (existing != null && existing.Length > 0)
        {
            return;
        }

        api.World.Claims.Add(new LandClaim
        {
            Areas = [area],
            Description = structure.BuildProtectionDesc,
            ProtectionLevel = structure.ProtectionLevel,
            LastKnownOwnerName = structure.BuildProtectionName,
            AllowUseEveryone = structure.AllowUseEveryone,
            AllowTraverseEveryone = structure.AllowTraverseEveryone
        });
    }

    internal static void SeedTerrainHeightMap(
        int chunkX,
        int chunkZ,
        IServerChunk[] chunks,
        Cuboidi location,
        EnumStructurePlacement placement)
    {
        if (placement is not (EnumStructurePlacement.Surface or EnumStructurePlacement.SurfaceRuin))
        {
            return;
        }

        const int chunkSize = GlobalConstants.ChunkSize;
        var mapChunk = chunks[0].MapChunk;
        var topY = location.Y2;

        for (var lz = 0; lz < chunkSize; lz++)
        {
            for (var lx = 0; lx < chunkSize; lx++)
            {
                var worldX = chunkX * chunkSize + lx;
                var worldZ = chunkZ * chunkSize + lz;
                if (!location.Contains(worldX, topY, worldZ))
                {
                    continue;
                }

                mapChunk.WorldGenTerrainHeightMap[lz * chunkSize + lx] = (ushort)topY;
                if (topY > mapChunk.YMax)
                {
                    mapChunk.YMax = (ushort)topY;
                }
            }
        }
    }

    internal static void RelightBounds(ICoreServerAPI api, Cuboidi bounds)
    {
        api.WorldManager.FullRelight(
            new BlockPos(bounds.X1, bounds.Y1, bounds.Z1),
            new BlockPos(bounds.X2, bounds.Y2, bounds.Z2),
            sendToClients: true);
    }

    internal static IServerChunk[]? CollectColumnChunks(ICoreServerAPI api, int chunkX, int chunkZ)
    {
        var chunks = StoryDungeonChunkLoader.LoadColumnChunks(api, chunkX, chunkZ);
        return chunks?.OfType<IServerChunk>().ToArray() is { Length: > 0 } serverChunks
            ? serverChunks
            : null;
    }
}

/// <summary>Загрузка колонок чанков для крупных сюжетных структур.</summary>
internal static class StoryDungeonChunkLoader
{
    public static bool LoadBounds(ICoreServerAPI api, Cuboidi bounds)
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
                if (LoadColumnChunks(api, cx, cz) == null)
                {
                    api.Logger.Warning(
                        "[SwixySkyBlock] Failed to load story chunk column ({0}, {1}).",
                        cx,
                        cz);
                    return false;
                }
            }
        }

        return true;
    }

    public static IWorldChunk[]? LoadColumnChunks(ICoreServerAPI api, int chunkX, int chunkZ)
    {
        var chunkHeight = GlobalConstants.ChunkSize;
        var maxCy = Math.Max(1, (api.WorldManager.MapSizeY + chunkHeight - 1) / chunkHeight);
        var chunks = new IWorldChunk[maxCy];

        for (var cy = 0; cy < maxCy; cy++)
        {
            var chunk = api.WorldManager.GetChunk(chunkX, cy, chunkZ);
            if (chunk == null)
            {
                return null;
            }

            chunks[cy] = chunk;
        }

        return chunks;
    }
}