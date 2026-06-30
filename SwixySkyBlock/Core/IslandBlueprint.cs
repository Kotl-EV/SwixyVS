using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixySkyBlock;

/// <summary>Загрузка и сборка схематик островов.</summary>
internal static class IslandBlueprint
{
    private static readonly string[] DefaultAssetNames = ["starter", "classic"];
    private static IReadOnlyList<IslandTemplate>? cachedTemplates;

    public static IReadOnlyList<IslandTemplate> LoadAll(ICoreServerAPI api)
    {
        if (cachedTemplates != null)
        {
            return cachedTemplates;
        }

        var templates = new List<IslandTemplate>();
        var loadedFromAssets = 0;

        foreach (var name in DefaultAssetNames)
        {
            if (TryAddFromAsset(api, templates, name))
            {
                loadedFromAssets++;
            }
        }

        foreach (var asset in api.Assets.GetMany("schematics/"))
        {
            if (!string.Equals(asset.Location.Domain, "swixyskyblock", StringComparison.Ordinal))
            {
                continue;
            }

            var name = asset.Location.Path.Replace("schematics/", "", StringComparison.Ordinal)
                .Replace(".json", "", StringComparison.Ordinal);
            if (templates.Exists(t => t.Name == name))
            {
                continue;
            }

            if (TryAddFromText(api, templates, name, asset.ToText()))
            {
                loadedFromAssets++;
            }
        }

        if (templates.Count == 0)
        {
            templates.Add(CreateBuiltInCircular("starter", 10, "game:cobblestone-granite", "game:soil-medium-normal", "game:soil-medium-normal"));
            templates.Add(CreateBuiltInCircular("classic", 10, "game:rock-andesite", "game:cobblestone-andesite", "game:soil-medium-normal"));
            api.Logger.Warning(
                "[SwixySkyBlock] No schematic assets loaded (check Mods folder contains assets/swixyskyblock/schematics/). Using built-in islands.");
        }
        else if (loadedFromAssets == 0)
        {
            api.Logger.Warning("[SwixySkyBlock] Schematic files found but none passed validation; using partial/built-in list.");
        }

        cachedTemplates = templates;
        return templates;
    }

    public static void ClearCache() => cachedTemplates = null;

    public static IslandTemplate PickForWorld(IReadOnlyList<IslandTemplate> templates) =>
        templates.FirstOrDefault(t => t.Name == "starter") ?? templates[0];

    private static bool TryAddFromAsset(ICoreServerAPI api, List<IslandTemplate> templates, string name)
    {
        var locations = new[]
        {
            new AssetLocation("swixyskyblock", $"schematics/{name}.json"),
            new AssetLocation($"swixyskyblock:schematics/{name}.json")
        };

        foreach (var location in locations)
        {
            var asset = api.Assets.TryGet(location);
            if (asset == null)
            {
                continue;
            }

            api.Logger.Notification("[SwixySkyBlock] Found schematic asset {0}.", location);
            return TryAddFromText(api, templates, name, asset.ToText());
        }

        api.Logger.Debug("[SwixySkyBlock] Schematic asset not found for '{0}'.", name);
        return false;
    }

    private static bool TryAddFromText(ICoreServerAPI api, List<IslandTemplate> templates, string name, string json)
    {
        string? error = null;
        var schematic = BlockSchematic.LoadFromString(json, ref error);
        if (schematic == null || !string.IsNullOrEmpty(error))
        {
            api.Logger.Warning("[SwixySkyBlock] Failed to load schematic '{0}': {1}", name, error ?? "unknown");
            return false;
        }

        if (schematic.SizeX <= 0 || schematic.SizeY <= 0 || schematic.SizeZ <= 0)
        {
            api.Logger.Warning("[SwixySkyBlock] Schematic '{0}' has invalid size.", name);
            return false;
        }

        WarnUnknownSchematicBlocks(api, schematic, name);

        templates.Add(new IslandTemplate { Name = name, Schematic = schematic });
        api.Logger.Notification(
            "[SwixySkyBlock] Loaded island schematic '{0}' ({1}x{2}x{3}).",
            name,
            schematic.SizeX,
            schematic.SizeY,
            schematic.SizeZ);
        return true;
    }

    private static void WarnUnknownSchematicBlocks(ICoreServerAPI api, BlockSchematic schematic, string name)
    {
        var missing = new List<string>();

        foreach (var code in schematic.BlockCodes.Values)
        {
            if (code == null || code.Path == "air")
            {
                continue;
            }

            if (api.World.GetBlock(code).Id == 0)
            {
                missing.Add(code.ToString());
            }
        }

        if (missing.Count > 0)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] Schematic '{0}' references unresolved blocks (may still place): {1}",
                name,
                string.Join(", ", missing.Distinct()));
        }
    }

    private static IslandTemplate CreateBuiltInCircular(
        string name,
        int radius,
        string bottom,
        string middle,
        string top)
    {
        const int height = 3;
        var size = radius * 2 + 1;
        var center = radius;

        var schematic = new BlockSchematic
        {
            GameVersion = "1.22.0",
            SizeX = size,
            SizeY = height,
            SizeZ = size,
            ReplaceMode = EnumReplaceMode.ReplaceAllNoAir
        };

        schematic.BlockCodes[1] = new AssetLocation(bottom);
        schematic.BlockCodes[2] = new AssetLocation(middle);
        schematic.BlockCodes[3] = new AssetLocation(top);

        var radiusSq = radius * radius;
        for (var y = 0; y < height; y++)
        {
            var blockId = y + 1;
            for (var x = 0; x < size; x++)
            {
                for (var z = 0; z < size; z++)
                {
                    var dx = x - center;
                    var dz = z - center;
                    if (dx * dx + dz * dz > radiusSq)
                    {
                        continue;
                    }

                    schematic.Indices.Add(Pack(x, y, z));
                    schematic.BlockIds.Add(blockId);
                }
            }
        }

        return new IslandTemplate { Name = name, Schematic = schematic };
    }

    private static uint Pack(int x, int y, int z) => (uint)(x | (z << 10) | (y << 20));
}
