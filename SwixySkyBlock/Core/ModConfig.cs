using System;
using Vintagestory.API.Common;

namespace SwixySkyBlock;

public sealed partial class SwixySkyBlockMod
{
    private const string ConfigFileName = "config.json";

    public static SkyBlockConfig Config { get; private set; } = new();

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        LoadConfig(api);

        if (api.Side == EnumAppSide.Client)
        {
            LegacySaveFixup.MigrateAllSaves(api.Logger);
        }
    }

    private static void LoadConfig(ICoreAPI api)
    {
        try
        {
            var loaded = api.LoadModConfig<SkyBlockConfig>(ConfigFileName);
            if (loaded != null)
            {
                Config = loaded;
            }
            else
            {
                Config = new SkyBlockConfig();
                api.StoreModConfig(Config, ConfigFileName);
            }
        }
        catch (Exception ex)
        {
            api.Logger.Error("[SwixySkyBlock] Failed to load config, using defaults: {0}", ex.Message);
            Config = new SkyBlockConfig();
        }

        Config.IslandSurfaceY = Math.Clamp(Config.IslandSurfaceY, 1, 2000);

        var minSpacing = SkyBlockWorld.MinIslandSpacingBlocks;
        var requestedSpacing = Config.IslandSpacing;
        Config.IslandSpacing = Math.Clamp(Config.IslandSpacing, minSpacing, 4096);
        if (requestedSpacing < minSpacing)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] IslandSpacing {0} is below claim size; raised to {1} (5×5 chunks).",
                requestedSpacing,
                Config.IslandSpacing);
        }

        api.Logger.Notification(
            "[SwixySkyBlock] Island surface Y = {0}, spacing = {1} (min {2})",
            Config.IslandSurfaceY,
            Config.IslandSpacing,
            minSpacing);
    }
}
