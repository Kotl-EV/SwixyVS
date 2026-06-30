using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixySkyBlock;

/// <summary>Загрузка и сборка схематик островов.</summary>
internal static class IslandBlueprint
{
    private const string SpawnTemplateName = "Spawn";
    private const string SpawnSchematicPath = "schematics/Spawn.json";
    private const string PlayerIslandSchematicPath = "schematics/islands/";
    private static IslandTemplate? cachedSpawnTemplate;

    public static IReadOnlyList<IslandTemplate> LoadAll(ICoreServerAPI api)
    {
        var templates = new List<IslandTemplate>();
        var loadedFromAssets = 0;

        foreach (var asset in api.Assets.GetMany(PlayerIslandSchematicPath)
            .Where(static asset => string.Equals(asset.Location.Domain, "swixyskyblock", StringComparison.Ordinal))
            .OrderBy(static asset => asset.Location.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (!asset.Location.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = GetTemplateName(asset.Location.Path);
            if (templates.Exists(t => t.Name == name))
            {
                continue;
            }

            if (TryAddFromText(api, templates, name, asset.ToText()))
            {
                loadedFromAssets++;
            }
        }

        loadedFromAssets += LoadFromLooseFiles(api, templates);

        if (templates.Count == 0)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] No player island schematics loaded (check assets/swixyskyblock/schematics/islands/). Island creation templates list will be empty.");
        }
        else if (loadedFromAssets == 0)
        {
            api.Logger.Warning("[SwixySkyBlock] Schematic files found but none passed validation; using partial/built-in list.");
        }

        return templates;
    }

    public static IslandTemplate LoadSpawn(ICoreServerAPI api)
    {
        if (cachedSpawnTemplate != null)
        {
            return cachedSpawnTemplate;
        }

        var asset = api.Assets.TryGet(new AssetLocation("swixyskyblock", SpawnSchematicPath))
            ?? api.Assets.TryGet(new AssetLocation($"swixyskyblock:{SpawnSchematicPath}"));
        if (asset != null)
        {
            var templates = new List<IslandTemplate>(1);
            if (TryAddFromText(api, templates, SpawnTemplateName, asset.ToText()))
            {
                cachedSpawnTemplate = templates[0];
                return cachedSpawnTemplate;
            }
        }

        api.Logger.Warning(
            "[SwixySkyBlock] Spawn schematic not loaded (check assets/swixyskyblock/schematics/Spawn.json). Using built-in spawn island.");
        cachedSpawnTemplate = CreateBuiltInCircular(SpawnTemplateName, 10, "game:cobblestone-granite", "game:soil-medium-normal", "game:soil-medium-normal");
        return cachedSpawnTemplate;
    }

    public static void ClearCache()
    {
        cachedSpawnTemplate = null;
    }

    public static IslandTemplate PickForWorld(IReadOnlyList<IslandTemplate> templates) =>
        templates.FirstOrDefault(t => string.Equals(t.Name, "starter", StringComparison.OrdinalIgnoreCase)) ?? templates[0];

    private static int LoadFromLooseFiles(ICoreServerAPI api, List<IslandTemplate> templates)
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(assemblyDir))
        {
            return 0;
        }

        var schematicsDir = Path.Combine(
            assemblyDir,
            "assets",
            "swixyskyblock",
            "schematics",
            "islands");
        if (!Directory.Exists(schematicsDir))
        {
            return 0;
        }

        var loaded = 0;
        foreach (var file in Directory.EnumerateFiles(schematicsDir, "*.json").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (templates.Exists(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (TryAddFromText(api, templates, name, File.ReadAllText(file)))
            {
                loaded++;
            }
        }

        return loaded;
    }

    private static string GetTemplateName(string assetPath)
    {
        var path = assetPath.Replace('\\', '/');
        var fileNameStart = path.LastIndexOf('/') + 1;
        var fileName = fileNameStart > 0 ? path[fileNameStart..] : path;
        return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^5]
            : fileName;
    }

    private static bool TryAddFromText(ICoreServerAPI api, List<IslandTemplate> templates, string name, string json)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

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
