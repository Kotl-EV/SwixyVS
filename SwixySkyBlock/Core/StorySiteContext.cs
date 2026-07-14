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

/// <summary>Полный контекст одного сюжетного сайта: остров + ванильная структура.</summary>
internal sealed class StorySiteContext
{
    public required StoryDungeonDefinition Definition { get; init; }
    public required StoryDungeonRecord Record { get; init; }
    public required WorldGenStoryStructure Structure { get; init; }
    public required BlockSchematicPartial Schematic { get; init; }
    public required BlockPos Center { get; init; }
    public required BlockPos StartPos { get; init; }
    public required Cuboidi Location { get; init; }
    public required Block RockBlock { get; init; }
    public required int IslandRadius { get; init; }
    public required int IslandDepth { get; init; }
    public required int SurfaceY { get; init; }
    public required int WorldSeed { get; init; }
    public required IWorldGenBlockAccessor WgenAccessor { get; init; }

    public List<(int Cx, int Cz)> IslandColumns { get; private set; } = [];
    public List<(int Cx, int Cz)> StructureColumns { get; private set; } = [];

    public static StorySiteContext? TryCreate(
        ICoreServerAPI api,
        StoryDungeonDefinition definition,
        StoryDungeonRecord record)
    {
        var storyGen = api.ModLoader.GetModSystem<GenStoryStructures>();
        if (storyGen == null || StoryStructureConfigLoader.EnsureLoaded(api, storyGen)?.Structures == null)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] GenStoryStructures unavailable; cannot build story site '{0}'.",
                definition.Code);
            return null;
        }

        var structure = storyGen.scfg.Structures.FirstOrDefault(s =>
            string.Equals(s.Code, definition.StructureCode, StringComparison.OrdinalIgnoreCase));
        if (structure == null)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Story structure '{0}' not found for site '{1}'.",
                definition.StructureCode,
                definition.Code);
            return null;
        }

        var wgenAccessor = StoryStructureRuntime.TryGetWorldgenAccessor(api);
        if (wgenAccessor == null)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Worldgen block accessor unavailable for story site '{0}'.",
                definition.Code);
            return null;
        }

        // Процедурный остров — компактный диск; landformRadius ванили не раздуваем.
        var islandRadius = definition.IslandRadius + 12;
        var surfaceY = record.Center.Y;
        var rockBlock = StoryStructurePlacer.ResolveRockBlock(api, structure, record.Center, surfaceY)
            ?? api.World.GetBlock(new AssetLocation("game:rock-granite"))!;
        var schematic = StorySchematicSkyBlockPrep.Prepare(api, structure, rockBlock);
        if (!StoryStructurePlacer.TryComputeStartPos(
                definition,
                structure,
                record.Center,
                surfaceY,
                out var startPos,
                out var location))
        {
            return null;
        }

        StoryStructurePlacer.RegisterStoryLocation(storyGen, structure, record.Center, location);

        var context = new StorySiteContext
        {
            Definition = definition,
            Record = record,
            Structure = structure,
            Schematic = schematic,
            Center = record.Center.Copy(),
            StartPos = startPos,
            Location = location,
            RockBlock = rockBlock,
            IslandRadius = islandRadius,
            IslandDepth = Math.Max(6, definition.IslandDepth),
            SurfaceY = surfaceY,
            WorldSeed = api.World.Seed,
            WgenAccessor = wgenAccessor
        };

        context.IslandColumns = StoryStructurePlacer.BuildDiskColumnList(record.Center, islandRadius);
        context.StructureColumns = StoryStructurePlacer.BuildColumnList(location);
        return context;
    }

    public bool ContainsIslandBlock(int worldX, int worldZ)
    {
        var dx = worldX - Center.X;
        var dz = worldZ - Center.Z;
        return StorySiteTerrainGen.IsInsideIsland(dx, dz, IslandRadius, HashSiteSeed());
    }

    public bool IntersectsStructureColumn(int chunkX, int chunkZ)
    {
        const int chunkSize = GlobalConstants.ChunkSize;
        var x1 = chunkX * chunkSize;
        var x2 = x1 + chunkSize - 1;
        var z1 = chunkZ * chunkSize;
        var z2 = z1 + chunkSize - 1;
        return Location.Intersects(new Cuboidi(x1, Location.Y1, z1, x2, Location.Y2, z2));
    }

    public int HashSiteSeed() => HashCode.Combine(WorldSeed, Definition.Code, Definition.Anchor);
}