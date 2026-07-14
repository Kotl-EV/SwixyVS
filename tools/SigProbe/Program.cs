using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;
class P {
  void M(BlockSchematicPartial s, IServerChunk[] c, IWorldGenBlockAccessor a, IWorldAccessor w, BlockPos p, Block b) {
    s.PlacePartial(c, a, w, 0, 0, p, EnumReplaceMode.ReplaceAll, EnumStructurePlacement.SurfaceRuin, true, true, null, null, b, true);
  }
}
