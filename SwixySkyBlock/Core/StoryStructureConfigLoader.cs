using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace SwixySkyBlock;

/// <summary>Загрузка storystructures.json для пустых SkyBlock-миров без standard worldgen.</summary>
internal static class StoryStructureConfigLoader
{
    public static WorldGenStoryStructuresConfig? EnsureLoaded(ICoreServerAPI api, GenStoryStructures storyGen)
    {
        if (storyGen.scfg?.Structures is { Length: > 0 })
        {
            return storyGen.scfg;
        }

        var assets = api.Assets.GetMany<WorldGenStoryStructuresConfig>(api.Logger, "worldgen/storystructures.json");
        if (!assets.Any())
        {
            api.Logger.Warning("[SwixySkyBlock] No storystructures.json assets found.");
            return null;
        }

        var scfg = new WorldGenStoryStructuresConfig
        {
            SchematicYOffsets = new Dictionary<string, int>(),
            RocktypeRemapGroups = new Dictionary<string, Dictionary<AssetLocation, AssetLocation>>()
        };
        var structures = new List<WorldGenStoryStructure>();

        foreach (var (_, conf) in assets)
        {
            foreach (var remap in conf.RocktypeRemapGroups)
            {
                if (scfg.RocktypeRemapGroups.TryGetValue(remap.Key, out var remapGroup))
                {
                    foreach (var (source, target) in remap.Value)
                    {
                        remapGroup.TryAdd(source, target);
                    }
                }
                else
                {
                    scfg.RocktypeRemapGroups.TryAdd(remap.Key, remap.Value);
                }
            }

            foreach (var remap in conf.SchematicYOffsets)
            {
                scfg.SchematicYOffsets.TryAdd(remap.Key, remap.Value);
            }

            structures.AddRange(conf.Structures);
        }

        scfg.Structures = structures.ToArray();
        var blockLayerConfig = BlockLayerConfig.GetInstance(api);
        scfg.Init(api, blockLayerConfig.RockStrata, blockLayerConfig);
        storyGen.scfg = scfg;

        api.Logger.Notification(
            "[SwixySkyBlock] Loaded {0} vanilla story structures for SkyBlock placement.",
            scfg.Structures.Length);
        return scfg;
    }
}