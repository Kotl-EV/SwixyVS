using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock;

[ProtoContract]
internal sealed class PlayerIslandRecord
{
    [ProtoMember(1)] public string PlayerUid { get; set; } = "";
    [ProtoMember(2)] public string TemplateName { get; set; } = "starter";
    [ProtoMember(3)] public int OriginX { get; set; }
    [ProtoMember(4)] public int OriginY { get; set; }
    [ProtoMember(5)] public int OriginZ { get; set; }
    [ProtoMember(6)] public int SlotIndex { get; set; }

    public BlockPos Origin => new(OriginX, OriginY, OriginZ);

    public void SetOrigin(BlockPos pos)
    {
        OriginX = pos.X;
        OriginY = pos.Y;
        OriginZ = pos.Z;
    }
}

[ProtoContract]
internal sealed class PlayerIslandSaveData
{
    [ProtoMember(1)] public List<PlayerIslandRecord> Islands { get; set; } = [];
    [ProtoMember(2)] public List<int> FreeSlots { get; set; } = [];
}
