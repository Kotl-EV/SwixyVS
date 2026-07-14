using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock;

/// <summary>Остров: схематика и точка спавна игрока.</summary>
internal sealed class IslandTemplate
{
    public required string Name { get; init; }
    public required BlockSchematic Schematic { get; init; }

    public BlockPos GetSpawnPosition(BlockPos origin)
    {
        var localSpawn = FindSurfaceSpawn();
        return origin.AddCopy(localSpawn.X, localSpawn.Y, localSpawn.Z);
    }

    /// <summary>Находит позицию на поверхности центра острова по данным схематики.</summary>
    private (int X, int Y, int Z) FindSurfaceSpawn()
    {
        var centerX = Schematic.SizeX / 2;
        var centerZ = Schematic.SizeZ / 2;

        if (TryFindSurfaceAt(centerX, centerZ, out var surfaceY))
        {
            return (centerX, surfaceY + 1, centerZ);
        }

        for (var radius = 1; radius <= Math.Max(Schematic.SizeX, Schematic.SizeZ); radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dz) != radius)
                    {
                        continue;
                    }

                    var x = centerX + dx;
                    var z = centerZ + dz;
                    if (x < 0 || x >= Schematic.SizeX || z < 0 || z >= Schematic.SizeZ)
                    {
                        continue;
                    }

                    if (TryFindSurfaceAt(x, z, out surfaceY))
                    {
                        return (x, surfaceY + 1, z);
                    }
                }
            }
        }

        return (centerX, Schematic.SizeY + 1, centerZ);
    }

    private bool TryFindSurfaceAt(int x, int z, out int surfaceY)
    {
        for (var y = Schematic.SizeY - 1; y >= 0; y--)
        {
            if (SchematicHasBlock(x, y, z))
            {
                surfaceY = y;
                return true;
            }
        }

        surfaceY = -1;
        return false;
    }

    private bool SchematicHasBlock(int x, int y, int z)
    {
        var count = Math.Min(Schematic.Indices.Count, Schematic.BlockIds.Count);
        for (var i = 0; i < count; i++)
        {
            if (Schematic.BlockIds[i] == 0)
            {
                continue;
            }

            var index = Schematic.Indices[i];
            if ((int)(index & 0x3ff) == x
                && (int)((index >> 10) & 0x3ff) == y
                && (int)((index >> 20) & 0x3ff) == z)
            {
                return true;
            }
        }

        return false;
    }

    public Cuboidi GetBounds(BlockPos origin) =>
        new(
            origin.X,
            origin.Y,
            origin.Z,
            origin.X + Schematic.SizeX - 1,
            origin.Y + Schematic.SizeY - 1,
            origin.Z + Schematic.SizeZ - 1);
}