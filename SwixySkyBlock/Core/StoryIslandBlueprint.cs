using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock;

/// <summary>Процедурная генерация крупных островов под сюжетные локации.</summary>
internal static class StoryIslandBlueprint
{
    public static IslandTemplate Create(StoryDungeonDefinition definition, int worldSeed)
    {
        var radius = Math.Max(48, definition.IslandRadius);
        var depth = Math.Max(6, definition.IslandDepth);
        var size = radius * 2 + 1;
        var center = radius;
        var seed = HashCode.Combine(worldSeed, definition.Code, definition.Anchor);

        var schematic = new BlockSchematic
        {
            GameVersion = "1.22.0",
            SizeX = size,
            SizeY = depth,
            SizeZ = size,
            ReplaceMode = EnumReplaceMode.ReplaceAllNoAir
        };

        schematic.BlockCodes[1] = new AssetLocation("game:cobblestone-granite");
        schematic.BlockCodes[2] = new AssetLocation("game:packeddirt");
        schematic.BlockCodes[3] = new AssetLocation("game:soil-medium-normal");
        schematic.BlockCodes[4] = new AssetLocation("game:soil-medium-sparse");
        schematic.BlockCodes[5] = new AssetLocation("game:gravel-granite");
        schematic.BlockCodes[6] = new AssetLocation("game:rock-granite");
        schematic.BlockCodes[7] = new AssetLocation("game:stonebricks-granite");

        var radiusSq = radius * radius;
        var isDeep = definition.Placement is StoryDungeonPlacement.BuriedRuin or StoryDungeonPlacement.Underground;
        var surfaceCap = Math.Min(depth - 1, isDeep ? 4 : depth);

        for (var x = 0; x < size; x++)
        {
            for (var z = 0; z < size; z++)
            {
                var dx = x - center;
                var dz = z - center;
                var distSq = dx * dx + dz * dz;
                if (distSq > radiusSq)
                {
                    continue;
                }

                var edgeNoise = SampleEdgeNoise(seed, x, z);
                var effectiveRadiusSq = radiusSq * edgeNoise;
                if (distSq > effectiveRadiusSq)
                {
                    continue;
                }

                var heightNoise = SampleHeightNoise(seed, x, z);
                var columnHeight = isDeep
                    ? depth
                    : Math.Clamp(3 + heightNoise, 3, depth);
                var shore = distSq > radiusSq * 0.82;
                var topY = columnHeight - 1;

                for (var y = 0; y < columnHeight; y++)
                {
                    var blockId = PickBlockId(y, topY, columnHeight, shore, isDeep, surfaceCap, SampleSoilNoise(seed, x, z, y));
                    schematic.Indices.Add(Pack(x, y, z));
                    schematic.BlockIds.Add(blockId);
                }
            }
        }

        return new IslandTemplate
        {
            Name = $"story-{definition.Code}",
            Schematic = schematic
        };
    }

    private static int PickBlockId(
        int y,
        int topY,
        int columnHeight,
        bool shore,
        bool isDeep,
        int surfaceCap,
        int soilNoise)
    {
        if (isDeep)
        {
            if (y >= topY - surfaceCap)
            {
                if (y == topY)
                {
                    return soilNoise > 6 ? 4 : 3;
                }

                return shore ? 1 : 2;
            }

            if (y >= columnHeight - 8)
            {
                return 1;
            }

            return y % 5 == 0 ? 7 : 6;
        }

        if (y == 0)
        {
            return shore ? 5 : 6;
        }

        if (y < columnHeight - 2)
        {
            return shore ? 1 : 2;
        }

        if (y == columnHeight - 1)
        {
            return soilNoise > 6 ? 4 : 3;
        }

        return 2;
    }

    private static float SampleEdgeNoise(int seed, int x, int z)
    {
        var value = Math.Sin((x + seed % 97) * 0.17 + (z + seed % 53) * 0.23) * 0.5
            + Math.Cos((x - z + seed % 31) * 0.11) * 0.35;
        return Math.Clamp(0.78f + (float)value * 0.18f, 0.62f, 0.98f);
    }

    private static int SampleHeightNoise(int seed, int x, int z)
    {
        var value = Math.Sin((x + seed) * 0.09 + z * 0.13) + Math.Cos(z * 0.07 - x * 0.05 + seed);
        return (int)Math.Round(Math.Clamp((value + 2.0) * 0.75, 0.0, 3.0));
    }

    private static int SampleSoilNoise(int seed, int x, int z, int y) =>
        Math.Abs(HashCode.Combine(seed, x, z, y)) % 11;

    private static uint Pack(int x, int y, int z) => (uint)(x | (z << 10) | (y << 20));
}