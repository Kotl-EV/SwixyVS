using System.Collections.Generic;

namespace SwixySkyBlock;

/// <summary>Настройки мода (ModConfig/skyblock.json).</summary>
public sealed class SkyBlockConfig
{
    /// <summary>Абсолютная Y основания стартового острова (низ схематики).</summary>
    public int IslandSurfaceY = 100;

    /// <summary>Расстояние между островами игроков по X/Z (блоки).</summary>
    public int IslandSpacing = 256;

    public bool AutoGenerateStorySites = false;

    public bool UseVanillaStoryTerrain = true;

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
                 new() { BlockCode = "game:loosestones-*-free", Chance = 35 },
                 new() { BlockCode = "game:soil-medium-normal", Chance = 12 },
                 new() { BlockCode = "game:looseflints-*-free", Chance = 12 },
                 new() { BlockCode = "game:tallgrass-*-free", Chance = 8 },
                 new() { BlockCode = "game:rawclay-blue-none", Chance = 5 },
                 new() { BlockCode = "game:rawclay-red-none", Chance = 4 },
                 new() { BlockCode = "game:tallplant-coopersreed-*-normal-*", Chance = 3 },
                 new() { BlockCode = "game:tallplant-papyrus-*-normal-*", Chance = 2 },
                 new() { BlockCode = "game:looseores-nativecopper-*-free", Chance = 4 },
                 new() { BlockCode = "game:sand-*", Chance = 3 },
                 new() { BlockCode = "game:gravel-*", Chance = 3 },
                 new() { BlockCode = "game:peat-none", Chance = 2 },
                 new() { BlockCode = "game:leavesbranchy-*-*", Chance = 6 },
                 new() { BlockCode = "game:lootvessel-food", Chance = 0.15 }
             ]
         },
         new()
         {
             Level = 2,
             Entries =
             [
                 new() { BlockCode = "game:looseores-nativecopper-*-free", Chance = 12 },
                 new() { BlockCode = "game:looseores-malachite-*-free", Chance = 4 },
                 new() { BlockCode = "game:log-placed-*-ud", Chance = 5 },
                 new() { BlockCode = "game:cobblestone-*", Chance = 8 },
                 new() { BlockCode = "game:peat-none", Chance = 4 },
                 new() { BlockCode = "game:looseores-lignite-*-free", Chance = 3 },
                 new() { BlockCode = "game:looseores-quartz-*-free", Chance = 2 },
                 new() { BlockCode = "game:lootvessel-arcticsupplies", Chance = 0.10 }
             ]
         },
         new()
         {
             Level = 3,
             Entries =
             [
                 new() { BlockCode = "game:looseores-nativecopper-*-free", Chance = 6 },
                 new() { BlockCode = "game:ore-*-nativecopper-*", Chance = 3 },
                 new() { BlockCode = "game:looseores-malachite-*-free", Chance = 3 },
                 new() { BlockCode = "game:looseores-lignite-*-free", Chance = 4 },
                 new() { BlockCode = "game:ore-lignite-*", Chance = 2 },
                 new() { BlockCode = "game:looseores-quartz-*-free", Chance = 3 },
                 new() { BlockCode = "game:rock-limestone", Chance = 3 },
                 new() { BlockCode = "game:rock-chalk", Chance = 3 },
                 new() { BlockCode = "game:looseores-borax-*-free", Chance = 1 },
                 new() { BlockCode = "game:rawclay-fire-none", Chance = 2 }
             ]
         },
         new()
         {
             Level = 4,
             Entries =
             [
                 new() { BlockCode = "game:looseores-cassiterite-*-free", Chance = 5 },
                 new() { BlockCode = "game:looseores-sphalerite-*-free", Chance = 3 },
                 new() { BlockCode = "game:looseores-bismuthinite-*-free", Chance = 2 },
                 new() { BlockCode = "game:looseores-galena-*-free", Chance = 2 },
                 new() { BlockCode = "game:looseores-quartz_nativesilver-*-free", Chance = 0.5 },
                 new() { BlockCode = "game:looseores-galena_nativesilver-*-free", Chance = 0.5 },
                 new() { BlockCode = "game:looseores-quartz_nativegold-*-free", Chance = 0.5 }
             ]
         },
         new()
         {
             Level = 5,
             Entries =
             [
                 new() { BlockCode = "game:ore-*-cassiterite-*", Chance = 4 },
                 new() { BlockCode = "game:ore-*-sphalerite-*", Chance = 2 },
                 new() { BlockCode = "game:ore-*-bismuthinite-*", Chance = 2 },
                 new() { BlockCode = "game:ore-*-galena-*", Chance = 1 },
                 new() { BlockCode = "game:rawclay-fire-none", Chance = 5 },
                 new() { BlockCode = "game:looseores-borax-*-free", Chance = 2 },
                 new() { BlockCode = "game:ore-borax-*", Chance = 1 },
                 new() { BlockCode = "game:ore-quartz-*", Chance = 3 },
                 new() { BlockCode = "game:rock-bauxite", Chance = 2 },
                 new() { BlockCode = "game:ore-bituminouscoal-*", Chance = 2 }
             ]
         },
         new()
         {
             Level = 6,
             Entries =
             [
                 new() { BlockCode = "game:looseores-limonite-*-free", Chance = 5 },
                 new() { BlockCode = "game:looseores-hematite-*-free", Chance = 5 },
                 new() { BlockCode = "game:looseores-magnetite-*-free", Chance = 4 },
                 new() { BlockCode = "game:ore-bituminouscoal-*", Chance = 4 },
                 new() { BlockCode = "game:looseores-anthracite-*-free", Chance = 1 },
                 new() { BlockCode = "game:ore-quartz-*", Chance = 2 }
             ]
         },
         new()
         {
             Level = 7,
             Entries =
             [
                 new() { BlockCode = "game:ore-*-limonite-*", Chance = 4 },
                 new() { BlockCode = "game:ore-*-hematite-*", Chance = 4 },
                 new() { BlockCode = "game:ore-*-magnetite-*", Chance = 4 },
                 new() { BlockCode = "game:rock-bauxite", Chance = 4 },
                 new() { BlockCode = "game:ore-quartz-*", Chance = 3 },
                 new() { BlockCode = "game:ore-anthracite-*", Chance = 2 },
                 new() { BlockCode = "game:ore-sulfur-*", Chance = 1 }
             ]
         },
         new()
         {
             Level = 8,
             Entries =
             [
                 new() { BlockCode = "game:meteorite-iron", Chance = 0.5 },
                 new() { BlockCode = "game:ore-olivine-*", Chance = 2 },
                 new() { BlockCode = "game:looseores-olivine-*-free", Chance = 1 },
                 new() { BlockCode = "game:ore-graphite-*", Chance = 1 },
                 new() { BlockCode = "game:rock-halite", Chance = 1 },
                 new() { BlockCode = "game:ore-sylvite-halite", Chance = 0.5 },
                 new() { BlockCode = "game:looseores-olivine_peridot-peridotite-free", Chance = 0.5 },
                 new() { BlockCode = "game:looseores-quartz_wolframite-granite-free", Chance = 0.5 }
             ]
         },
         new()
         {
             Level = 9,
             Entries =
             [
                 new() { BlockCode = "game:ore-*-ilmenite-*", Chance = 2 },
                 new() { BlockCode = "game:looseores-ilmenite-*-free", Chance = 1 },
                 new() { BlockCode = "game:ore-*-chromite-*", Chance = 1.5 },
                 new() { BlockCode = "game:looseores-chromite-*-free", Chance = 1 },
                 new() { BlockCode = "game:ore-graphite-*", Chance = 2 },
                 new() { BlockCode = "game:ore-sulfur-*", Chance = 1 },
                 new() { BlockCode = "game:ore-quartz_wolframite-granite", Chance = 1 }
             ]
         },
         new()
         {
             Level = 10,
             Entries =
             [
                 new() { BlockCode = "game:ore-low-diamond-*", Chance = 0.25 },
                 new() { BlockCode = "game:ore-low-emerald-*", Chance = 0.25 },
                 new() { BlockCode = "game:ore-low-olivine_peridot-*", Chance = 0.25 },
                 new() { BlockCode = "game:ore-*-quartz_nativegold-*", Chance = 0.5 },
                 new() { BlockCode = "game:ore-*-quartz_nativesilver-*", Chance = 0.5 },
                 new() { BlockCode = "game:ore-*-galena_nativesilver-*", Chance = 0.5 },
                 new() { BlockCode = "game:ore-*-wolframite-*", Chance = 0.5 }
             ]
         }
     ];
}

public sealed class SkyBlockGeneratorEntryConfig
{
    public string BlockCode = "game:soil-medium-normal";
    public double Chance = 1;
}
