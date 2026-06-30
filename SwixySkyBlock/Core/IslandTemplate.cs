using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock;

/// <summary>Остров: схематика и точка спавна игрока.</summary>
internal sealed class IslandTemplate
{
    public required string Name { get; init; }
    public required BlockSchematic Schematic { get; init; }

    public BlockPos GetSpawnPosition(BlockPos origin) =>
        origin.AddCopy(Schematic.SizeX / 2, Schematic.SizeY + 1, Schematic.SizeZ / 2);

    public Cuboidi GetBounds(BlockPos origin) =>
        new(
            origin.X,
            origin.Y,
            origin.Z,
            origin.X + Schematic.SizeX - 1,
            origin.Y + Schematic.SizeY - 1,
            origin.Z + Schematic.SizeZ - 1);
}
