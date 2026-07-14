using Vintagestory.API.MathTools;

namespace SwixySkyBlock;

/// <summary>Сохранённое состояние одной сюжетной локации.</summary>
internal sealed class StoryDungeonRecord
{
    public required string Code { get; init; }
    public BlockPos Center { get; set; } = new();
    public BlockPos Spawn { get; set; } = new();
    public bool Placed { get; set; }
}