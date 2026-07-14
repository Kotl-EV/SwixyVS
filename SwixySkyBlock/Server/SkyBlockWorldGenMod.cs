using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Регистрирует worldgen skyblock после ванильных standard-генераторов.</summary>
public sealed class SkyBlockWorldGenMod : ModSystem
{
    public override double ExecuteOrder() => 1.0;

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        api.ModLoader.GetModSystem<SwixySkyBlockMod>().EnsureSkyBlockWorldGenRegistered(api);
    }
}