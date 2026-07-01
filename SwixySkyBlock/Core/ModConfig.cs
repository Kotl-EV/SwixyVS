using System;
using Vintagestory.API.Common;

namespace SwixySkyBlock;

public sealed partial class SwixySkyBlockMod
{
    private const string ConfigFileName = "skyblock.json";
    private const string LegacyConfigFileName = "config.json";
    private const string IslandGeneratorConfigFileName = "island_generator.json";

    public static SkyBlockConfig Config { get; private set; } = new();
    public static SkyBlockGeneratorConfig GeneratorConfig { get; private set; } = SkyBlockGeneratorConfig.CreateDefault();

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        LoadConfig(api);
        LoadGeneratorConfig(api);

        if (api.Side == EnumAppSide.Client)
        {
            LegacySaveFixup.MigrateAllSaves(api.Logger);
        }
    }

    private static void LoadConfig(ICoreAPI api)
    {
        try
        {
            var loaded = api.LoadModConfig<SkyBlockConfig>(ConfigFileName)
                ?? api.LoadModConfig<SkyBlockConfig>(LegacyConfigFileName);
            if (loaded != null)
            {
                Config = loaded;
                api.StoreModConfig(Config, ConfigFileName);
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

    private static void LoadGeneratorConfig(ICoreAPI api)
    {
        try
        {
            var loaded = api.LoadModConfig<SkyBlockGeneratorConfig>(IslandGeneratorConfigFileName);
            if (loaded != null)
            {
                GeneratorConfig = loaded;
            }
            else
            {
                GeneratorConfig = Config.GeneratorLevels is { Count: > 0 }
                    ? SkyBlockGeneratorConfig.CreateFromLegacy(Config.GeneratorLevels)
                    : SkyBlockGeneratorConfig.CreateDefault();
                NormalizeGeneratorConfig();
                api.StoreModConfig(GeneratorConfig, IslandGeneratorConfigFileName);
            }
        }
        catch (Exception ex)
        {
            api.Logger.Error("[SwixySkyBlock] Failed to load island generator config, using defaults: {0}", ex.Message);
            GeneratorConfig = SkyBlockGeneratorConfig.CreateDefault();
        }

        NormalizeGeneratorConfig();
    }

    private static void NormalizeGeneratorConfig()
    {
        if (GeneratorConfig.GeneratorLevels is { Count: > 0 } && (GeneratorConfig.Levels == null || GeneratorConfig.Levels.Count == 0))
        {
            GeneratorConfig.Levels = GeneratorConfig.GeneratorLevels;
        }

        if (GeneratorConfig.Levels == null || GeneratorConfig.Levels.Count == 0)
        {
            GeneratorConfig.Levels = SkyBlockGeneratorLevelConfig.DefaultLevels();
            return;
        }

        foreach (var level in GeneratorConfig.Levels)
        {
            level.Level = Math.Max(1, level.Level);
            if (level.Entries == null || level.Entries.Count == 0)
            {
                level.Entries =
                [
                    new() { BlockCode = "game:soil-medium-normal", Chance = 1 }
                ];
                continue;
            }

            foreach (var entry in level.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.BlockCode))
                {
                    entry.BlockCode = "game:soil-medium-normal";
                }

                entry.BlockCode = entry.BlockCode.Trim();
                entry.Chance = Math.Max(0, entry.Chance);
            }
        }
    }
}
