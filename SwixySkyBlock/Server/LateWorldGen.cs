using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Размещение острова в PreDone — после освещения и остальной генерации.</summary>
public sealed class SkyBlockLateWorldGenMod : ModSystem
{
    public override double ExecuteOrder() => 0.99;

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        SkyBlockWorldGenHost.RegisterLateIslandPlacement(api);
    }
}
