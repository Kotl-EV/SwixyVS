using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Мост к генерации острова из позднего ModSystem.</summary>
internal static class SkyBlockWorldGenHost
{
    private static SwixySkyBlockMod? host;

    public static void Bind(SwixySkyBlockMod mod) => host = mod;

    public static void RegisterLateIslandPlacement(ICoreServerAPI api)
    {
        foreach (var worldType in SkyBlockWorld.SupportedWorldTypes)
        {
            api.Event.ChunkColumnGeneration(
                request => host?.OnPlaceIslandPreDone(request),
                EnumWorldGenPass.PreDone,
                worldType);
        }
    }
}
