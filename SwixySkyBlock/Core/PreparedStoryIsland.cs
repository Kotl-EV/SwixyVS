using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock;

/// <summary>Остров, разбитый по колонкам чанков на worker-потоке (без доступа к миру).</summary>
internal sealed class PreparedStoryIsland
{
    public required IslandTemplate Island { get; init; }
    public required BlockPos Origin { get; init; }
    public required Dictionary<(int Cx, int Cz), List<(int Lx, int Ly, int Lz, int BlockCode)>> ColumnBlocks { get; init; }
    public required List<(int Cx, int Cz)> ColumnOrder { get; init; }

    public static PreparedStoryIsland Build(
        StoryDungeonDefinition definition,
        int worldSeed,
        BlockPos center)
    {
        var island = StoryIslandBlueprint.Create(definition, worldSeed);
        var origin = ComputeOrigin(center, island, definition);
        var schematic = island.Schematic;
        var columnBlocks = new Dictionary<(int, int), List<(int, int, int, int)>>();
        var count = System.Math.Min(schematic.Indices.Count, schematic.BlockIds.Count);

        const int chunkSize = GlobalConstants.ChunkSize;
        for (var i = 0; i < count; i++)
        {
            var blockCode = schematic.BlockIds[i];
            if (blockCode == 0)
            {
                continue;
            }

            var index = schematic.Indices[i];
            var lx = (int)(index & 0x3ff);
            var lz = (int)((index >> 10) & 0x3ff);
            var ly = (int)((index >> 20) & 0x3ff);
            var worldX = origin.X + lx;
            var worldZ = origin.Z + lz;
            var key = (worldX / chunkSize, worldZ / chunkSize);

            if (!columnBlocks.TryGetValue(key, out var list))
            {
                list = [];
                columnBlocks[key] = list;
            }

            list.Add((lx, ly, lz, blockCode));
        }

        var columnOrder = new List<(int, int)>(columnBlocks.Keys);
        columnOrder.Sort(static (a, b) =>
        {
            var cmp = a.Item1.CompareTo(b.Item1);
            return cmp != 0 ? cmp : a.Item2.CompareTo(b.Item2);
        });

        return new PreparedStoryIsland
        {
            Island = island,
            Origin = origin,
            ColumnBlocks = columnBlocks,
            ColumnOrder = columnOrder
        };
    }

    private static BlockPos ComputeOrigin(
        BlockPos center,
        IslandTemplate island,
        StoryDungeonDefinition definition)
    {
        var radius = island.Schematic.SizeX / 2;
        var originY = center.Y;
        if (definition.Placement is StoryDungeonPlacement.BuriedRuin or StoryDungeonPlacement.Underground)
        {
            originY = center.Y - island.Schematic.SizeY + System.Math.Min(4, definition.EntranceRevealBlocks);
        }

        return new BlockPos(center.X - radius, originY, center.Z - radius);
    }
}