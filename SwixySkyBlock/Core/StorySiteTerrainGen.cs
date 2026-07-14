using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace SwixySkyBlock;

/// <summary>Процедурный остров с плавными краями прямо в chunk column.</summary>
internal static class StorySiteTerrainGen
{
    public static void GenerateColumn(
        ICoreServerAPI api,
        StorySiteContext site,
        int chunkX,
        int chunkZ,
        IServerChunk[] chunks)
    {
        const int chunkSize = GlobalConstants.ChunkSize;
        if (api.World.GetBlockAccessorMapChunkLoading(true, true) is not IBulkBlockAccessor bulk)
        {
            return;
        }

        bulk.SetChunks(new Vec2i(chunkX, chunkZ), chunks);
        var seed = site.HashSiteSeed();
        var isDeep = site.Structure.Placement is EnumStructurePlacement.Underground
            or EnumStructurePlacement.SurfaceRuin;
        var blocks = ResolveBlockPalette(api, site);
        var mapChunk = chunks[0]?.MapChunk;
        var maxTerrainY = site.SurfaceY;

        for (var lz = 0; lz < chunkSize; lz++)
        {
            for (var lx = 0; lx < chunkSize; lx++)
            {
                var worldX = chunkX * chunkSize + lx;
                var worldZ = chunkZ * chunkSize + lz;
                var dx = worldX - site.Center.X;
                var dz = worldZ - site.Center.Z;
                if (!IsInsideIsland(dx, dz, site.IslandRadius, seed))
                {
                    continue;
                }

                var dist = Math.Sqrt(dx * dx + dz * dz);
                var edgeBlend = ComputeEdgeBlend(dist, site.IslandRadius);
                var heightNoise = SampleHeightNoise(seed, worldX, worldZ);
                var columnHeight = isDeep
                    ? site.IslandDepth
                    : Math.Clamp((int)Math.Round(3 + heightNoise * edgeBlend), 2, site.IslandDepth);
                var topY = site.SurfaceY;
                var bottomY = topY - columnHeight + 1;
                var shore = dist > site.IslandRadius * 0.78;
                var localTop = topY;

                if (mapChunk?.WorldGenTerrainHeightMap != null)
                {
                    var idx = lz * chunkSize + lx;
                    if (idx < mapChunk.WorldGenTerrainHeightMap.Length)
                    {
                        mapChunk.WorldGenTerrainHeightMap[idx] = (ushort)localTop;
                        if (localTop > maxTerrainY)
                        {
                            maxTerrainY = localTop;
                        }
                    }
                }

                for (var y = bottomY; y <= topY; y++)
                {
                    var block = PickBlock(
                        api,
                        blocks,
                        y,
                        topY,
                        bottomY,
                        shore,
                        isDeep,
                        SampleSoilNoise(seed, worldX, worldZ, y));
                    if (block == null)
                    {
                        continue;
                    }

                    bulk.SetBlock(block.BlockId, new BlockPos(worldX, y, worldZ));
                }
            }
        }

        bulk.Commit();

        if (mapChunk != null && maxTerrainY > mapChunk.YMax)
        {
            mapChunk.YMax = (ushort)maxTerrainY;
        }
    }

    public static bool IsInsideIsland(int dx, int dz, int radius, int seed)
    {
        var distSq = dx * dx + dz * dz;
        var edge = SampleSmoothEdgeFactor(seed, dx, dz);
        var effectiveRadius = radius * edge;
        return distSq <= effectiveRadius * effectiveRadius;
    }

    private static float SampleSmoothEdgeFactor(int seed, int dx, int dz)
    {
        var n1 = HashToUnit(seed, dx, dz);
        var n2 = HashToUnit(seed + 31, dx / 2, dz / 2);
        var n3 = HashToUnit(seed + 67, dx / 3, dz / 3);
        return Math.Clamp(0.74f + n1 * 0.14f + n2 * 0.08f + n3 * 0.04f, 0.68f, 0.98f);
    }

    private static float ComputeEdgeBlend(double dist, int radius)
    {
        var t = dist / Math.Max(1, radius);
        if (t <= 0.68)
        {
            return 1f;
        }

        var fade = (t - 0.68f) / 0.32f;
        fade = Math.Clamp(fade, 0f, 1f);
        var s = 1f - fade;
        return (float)(s * s * (3f - 2f * s));
    }

    private static int SampleHeightNoise(int seed, int x, int z)
    {
        var value = HashToUnit(seed + 11, x, z) + HashToUnit(seed + 23, z, x) * 0.5f;
        return (int)Math.Round(Math.Clamp(value * 3.0, 0.0, 3.0));
    }

    private static int SampleSoilNoise(int seed, int x, int z, int y) =>
        Math.Abs(HashCode.Combine(seed, x, z, y)) % 11;

    private static float HashToUnit(int seed, int x, int z)
    {
        var hash = HashCode.Combine(seed, x * 374761393, z * 668265263);
        return (hash & 0xFFFF) / 65535f;
    }

    private sealed class TerrainBlockPalette
    {
        public required Block Cobble { get; init; }
        public required Block PackedDirt { get; init; }
        public required Block Soil { get; init; }
        public required Block SparseSoil { get; init; }
        public required Block Gravel { get; init; }
        public required Block Rock { get; init; }
        public required Block StoneBrick { get; init; }
    }

    private static TerrainBlockPalette ResolveBlockPalette(ICoreServerAPI api, StorySiteContext site)
    {
        var rock = site.RockBlock;
        var rockPath = rock.Code.Path;
        var rockType = rockPath.StartsWith("rock-", StringComparison.Ordinal)
            ? rockPath["rock-".Length..]
            : "granite";

        Block Resolve(string path, string fallback) =>
            api.World.GetBlock(new AssetLocation(path))
            ?? api.World.GetBlock(new AssetLocation(fallback))
            ?? rock;

        return new TerrainBlockPalette
        {
            Cobble = Resolve($"game:cobblestone-{rockType}", "game:cobblestone-granite"),
            PackedDirt = Resolve("game:packeddirt", "game:packeddirt"),
            Soil = Resolve("game:soil-medium-normal", "game:soil-medium-normal"),
            SparseSoil = Resolve("game:soil-medium-sparse", "game:soil-medium-sparse"),
            Gravel = Resolve($"game:gravel-{rockType}", "game:gravel-granite"),
            Rock = rock,
            StoneBrick = Resolve($"game:stonebricks-{rockType}", "game:stonebricks-granite")
        };
    }

    private static Block? PickBlock(
        ICoreServerAPI api,
        TerrainBlockPalette palette,
        int y,
        int topY,
        int bottomY,
        bool shore,
        bool isDeep,
        int soilNoise)
    {
        if (isDeep)
        {
            if (y >= topY - 4)
            {
                if (y == topY)
                {
                    return soilNoise > 6 ? palette.SparseSoil : palette.Soil;
                }

                return shore ? palette.Cobble : palette.PackedDirt;
            }

            if (y >= bottomY + Math.Max(4, (topY - bottomY) / 3))
            {
                return palette.Cobble;
            }

            return y % 5 == 0 ? palette.StoneBrick : palette.Rock;
        }

        if (y == bottomY)
        {
            return shore ? palette.Gravel : palette.Rock;
        }

        if (y < topY - 1)
        {
            return shore ? palette.Cobble : palette.PackedDirt;
        }

        if (y == topY)
        {
            return soilNoise > 6 ? palette.SparseSoil : palette.Soil;
        }

        return palette.PackedDirt;
    }

}