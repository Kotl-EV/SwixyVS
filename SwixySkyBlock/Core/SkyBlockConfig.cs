using System.Collections.Generic;

namespace SwixySkyBlock;

/// <summary>Настройки мода (ModConfig/skyblock.json).</summary>
public sealed class SkyBlockConfig
{
    /// <summary>Абсолютная Y основания стартового острова (низ схематики).</summary>
    public int IslandSurfaceY = 100;

    /// <summary>Расстояние между островами игроков по X/Z (блоки).</summary>
    public int IslandSpacing = 256;

    public List<SkyBlockGeneratorLevelConfig>? GeneratorLevels = null;
}

public sealed class SkyBlockGeneratorConfig
{
    public List<SkyBlockGeneratorLevelConfig> Levels = [];
    public List<SkyBlockGeneratorLevelConfig>? GeneratorLevels = null;

    public static SkyBlockGeneratorConfig CreateDefault() => new()
    {
        Levels = SkyBlockGeneratorLevelConfig.DefaultLevels()
    };

    public static SkyBlockGeneratorConfig CreateFromLegacy(List<SkyBlockGeneratorLevelConfig> levels) => new()
    {
        Levels = levels
    };
}

public sealed class SkyBlockGeneratorLevelConfig
{
    public int Level = 1;
    public List<SkyBlockGeneratorEntryConfig> Entries = [];

    public static List<SkyBlockGeneratorLevelConfig> DefaultLevels() =>
    [
        new()
        {
            Level = 1,
            Entries =
            [
                new() { BlockCode = "game:soil-medium-normal", Chance = 8 },
                new() { BlockCode = "game:rawclay-*-none", Chance = 30 },
                new() { BlockCode = "game:loosestones-*-free", Chance = 42 },
                new() { BlockCode = "game:leavesbranchy-*-*", Chance = 4 },
                new() { BlockCode = "game:looseores-nativecopper-*-free", Chance = 16 }
            ]
        },
        new()
        {
            Level = 2,
            Entries =
            [
                new() { BlockCode = "game:log-placed-*-ud", Chance = 8 },
                new() { BlockCode = "game:peat-none", Chance = 6 },
                new() { BlockCode = "game:sand-*", Chance = 10 },
                new() { BlockCode = "game:gravel-*", Chance = 10 },
                new() { BlockCode = "game:looseores-nativecopper-*-free", Chance = 14 }
            ]
        },
        new()
        {
            Level = 3,
            Entries =
            [
                new() { BlockCode = "game:rawclay-fire-none", Chance = 6 },
                new() { BlockCode = "game:ore-flint-*", Chance = 5 },
                new() { BlockCode = "game:looseores-malachite-*", Chance = 2 },
                new() { BlockCode = "game:looseores-lignite-*", Chance = 3 },
                new() { BlockCode = "game:looseores-quartz-*", Chance = 2 }
            ]
        },
        new()
        {
            Level = 4,
            Entries =
            [
                new() { BlockCode = "game:rock-limestone", Chance = 3 },
                new() { BlockCode = "game:rock-chalk", Chance = 3 },
                new() { BlockCode = "game:rock-bauxite", Chance = 2 },
                new() { BlockCode = "game:ore-*-nativecopper-*", Chance = 4 },
                new() { BlockCode = "game:ore-*-malachite-*", Chance = 3 },
                new() { BlockCode = "game:ore-lignite-*", Chance = 3 }
            ]
        },
        new()
        {
            Level = 5,
            Entries =
            [
                new() { BlockCode = "game:looseores-cassiterite-*", Chance = 2 },
                new() { BlockCode = "game:looseores-sphalerite-*", Chance = 2 },
                new() { BlockCode = "game:looseores-bismuthinite-*", Chance = 2 },
                new() { BlockCode = "game:looseores-borax-*", Chance = 1 },
                new() { BlockCode = "game:looseores-galena-*", Chance = 1 }
            ]
        },
        new()
        {
            Level = 6,
            Entries =
            [
                new() { BlockCode = "game:ore-*-cassiterite-*", Chance = 3 },
                new() { BlockCode = "game:ore-*-sphalerite-*", Chance = 2 },
                new() { BlockCode = "game:ore-*-bismuthinite-*", Chance = 2 },
                new() { BlockCode = "game:ore-borax-*", Chance = 1 },
                new() { BlockCode = "game:ore-*-galena-*", Chance = 1 }
            ]
        },
        new()
        {
            Level = 7,
            Entries =
            [
                new() { BlockCode = "game:looseores-limonite-*", Chance = 3 },
                new() { BlockCode = "game:looseores-hematite-*", Chance = 3 },
                new() { BlockCode = "game:looseores-magnetite-*", Chance = 3 },
                new() { BlockCode = "game:ore-bituminouscoal-*", Chance = 4 },
                new() { BlockCode = "game:ore-sulfur-*", Chance = 1 }
            ]
        },
        new()
        {
            Level = 8,
            Entries =
            [
                new() { BlockCode = "game:ore-*-limonite-*", Chance = 4 },
                new() { BlockCode = "game:ore-*-hematite-*", Chance = 4 },
                new() { BlockCode = "game:ore-*-magnetite-*", Chance = 4 },
                new() { BlockCode = "game:ore-quartz-*", Chance = 3 },
                new() { BlockCode = "game:ore-halite-*", Chance = 1 }
            ]
        },
        new()
        {
            Level = 9,
            Entries =
            [
                new() { BlockCode = "game:ore-anthracite-*", Chance = 2 },
                new() { BlockCode = "game:ore-graphite-*", Chance = 1 },
                new() { BlockCode = "game:ore-*-quartz_nativesilver-*", Chance = 1 },
                new() { BlockCode = "game:ore-*-galena_nativesilver-*", Chance = 1 },
                new() { BlockCode = "game:ore-*-quartz_nativegold-*", Chance = 1 }
            ]
        },
        new()
        {
            Level = 10,
            Entries =
            [
                new() { BlockCode = "game:ore-*-chromite-*", Chance = 1 },
                new() { BlockCode = "game:ore-*-ilmenite-*", Chance = 1 },
                new() { BlockCode = "game:ore-*-wolframite-*", Chance = 1 },
                new() { BlockCode = "game:ore-low-diamond-*", Chance = 0.25 },
                new() { BlockCode = "game:ore-low-emerald-*", Chance = 0.25 }
            ]
        }
    ];
}

public sealed class SkyBlockGeneratorEntryConfig
{
    public string BlockCode = "game:soil-medium-normal";
    public double Chance = 1;
}
