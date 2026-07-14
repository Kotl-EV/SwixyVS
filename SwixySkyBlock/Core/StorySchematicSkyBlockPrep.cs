using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace SwixySkyBlock;

/// <summary>Подготовка ванильных сюжетных схематик для пустого SkyBlock-мира.</summary>
internal static class StorySchematicSkyBlockPrep
{
    private static readonly string[] DefaultLayerPaths =
    [
        "game:soil-medium-normal",
        "game:soil-medium-none",
        "game:soil-medium-sparse",
        "game:soil-low-normal",
        "game:soil-low-none",
        "game:packeddirt"
    ];

    public static BlockSchematicPartial Prepare(
        ICoreServerAPI api,
        WorldGenStoryStructure structure,
        Block? centerRock = null)
    {
        var source = StoryStructureRuntime.GetSchematic(structure);
        var schematic = (BlockSchematicPartial)source.ClonePacked();
        var layerBlockCodes = ResolveLayerBlockCodes(api, structure, schematic);
        var metaLayerCode = FindBlockCode(schematic, "meta-blocklayer");
        var stripMetaCodes = schematic.BlockCodes
            .Where(kv => IsMetaBlock(kv.Value)
                && !IsBlockCode(kv.Value, "meta-blocklayer"))
            .Select(kv => kv.Key)
            .ToHashSet();

        var count = Math.Min(schematic.Indices.Count, schematic.BlockIds.Count);
        var replacedLayers = 0;
        var strippedMeta = 0;
        for (var i = 0; i < count; i++)
        {
            var blockCode = schematic.BlockIds[i];
            if (blockCode == metaLayerCode && layerBlockCodes.Length > 0)
            {
                var y = (int)((schematic.Indices[i] >> 20) & 0x3ff);
                schematic.BlockIds[i] = layerBlockCodes[y % layerBlockCodes.Length];
                replacedLayers++;
                continue;
            }

            if (stripMetaCodes.Contains(blockCode))
            {
                schematic.BlockIds[i] = 0;
                strippedMeta++;
            }
        }

        var droppedUnresolved = DropUnresolvedBlockIds(api, schematic, structure.Code);
        var strippedBlockEntities = StripProblemBlockEntities(schematic);
        if (replacedLayers > 0 || strippedMeta > 0 || droppedUnresolved > 0 || strippedBlockEntities > 0)
        {
            api.Logger.Notification(
                "[SwixySkyBlock] Story schematic '{0}': replaced {1} layer marker(s), stripped {2} meta block(s), dropped {3} unresolved block(s), stripped {4} block entity payload(s).",
                structure.Code,
                replacedLayers,
                strippedMeta,
                droppedUnresolved,
                strippedBlockEntities);
        }

        return schematic;
    }

    private static int[] ResolveLayerBlockCodes(
        ICoreServerAPI api,
        WorldGenStoryStructure structure,
        BlockSchematic schematic)
    {
        var layerPaths = TryGetConfiguredLayerPaths(structure) ?? DefaultLayerPaths;
        var codes = new List<int>(layerPaths.Length);
        foreach (var path in layerPaths)
        {
            var block = api.World.GetBlock(new AssetLocation(path));
            if (block == null)
            {
                continue;
            }

            codes.Add(FindOrAddBlockCode(schematic, block.Code));
        }

        if (codes.Count == 0)
        {
            var fallback = api.World.GetBlock(new AssetLocation("game:soil-medium-normal"));
            if (fallback != null)
            {
                codes.Add(FindOrAddBlockCode(schematic, fallback.Code));
            }
        }

        return codes.ToArray();
    }

    private static string[]? TryGetConfiguredLayerPaths(WorldGenStoryStructure structure)
    {
        var field = structure.GetType().GetField("Replacewithblocklayers")
            ?? structure.GetType().GetField("replacewithblocklayers");
        if (field?.GetValue(structure) is not string[] configured || configured.Length == 0)
        {
            return null;
        }

        return configured
            .Select(path => path.Contains(':') ? path : $"game:{path}")
            .ToArray();
    }

    private static int FindBlockCode(BlockSchematic schematic, string path)
    {
        foreach (var (code, location) in schematic.BlockCodes)
        {
            if (IsBlockCode(location, path))
            {
                return code;
            }
        }

        return -1;
    }

    private static int FindOrAddBlockCode(BlockSchematic schematic, AssetLocation location)
    {
        var existing = FindBlockCode(schematic, location.ToShortString());
        if (existing >= 0)
        {
            return existing;
        }

        var nextCode = schematic.BlockCodes.Keys.DefaultIfEmpty(0).Max() + 1;
        schematic.BlockCodes[nextCode] = location;
        return nextCode;
    }

    private static int DropUnresolvedBlockIds(
        ICoreServerAPI api,
        BlockSchematic schematic,
        string structureCode)
    {
        var dropped = 0;
        var warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = Math.Min(schematic.Indices.Count, schematic.BlockIds.Count);

        for (var i = 0; i < count; i++)
        {
            var code = schematic.BlockIds[i];
            if (code == 0)
            {
                continue;
            }

            if (!schematic.BlockCodes.TryGetValue(code, out var location))
            {
                schematic.BlockIds[i] = 0;
                dropped++;
                continue;
            }

            var block = api.World.GetBlock(location);
            if (block != null && block.Id != 0)
            {
                continue;
            }

            var path = location.ToShortString();
            if (warned.Add(path))
            {
                api.Logger.Warning(
                    "[SwixySkyBlock] Story schematic '{0}': block '{1}' is not registered; removing from placement.",
                    structureCode,
                    path);
            }

            schematic.BlockIds[i] = 0;
            dropped++;
        }

        return dropped;
    }

    private static bool IsMetaBlock(AssetLocation location) =>
        location.Path.StartsWith("meta-", StringComparison.OrdinalIgnoreCase);

    private static int StripProblemBlockEntities(BlockSchematic schematic)
    {
        if (schematic.BlockEntities == null || schematic.BlockEntities.Count == 0)
        {
            return 0;
        }

        var blocksByPackedPos = new Dictionary<uint, int>();
        var count = Math.Min(schematic.Indices.Count, schematic.BlockIds.Count);
        for (var i = 0; i < count; i++)
        {
            blocksByPackedPos[schematic.Indices[i]] = schematic.BlockIds[i];
        }

        var remove = new List<uint>();
        foreach (var (packedPos, data) in schematic.BlockEntities)
        {
            var blockCode = blocksByPackedPos.TryGetValue(packedPos, out var code)
                && schematic.BlockCodes.TryGetValue(code, out var location)
                ? location
                : null;

            if (IsProblemBlockEntity(blockCode, data))
            {
                remove.Add(packedPos);
            }
        }

        foreach (var packedPos in remove)
        {
            schematic.BlockEntities.Remove(packedPos);
        }

        return remove.Count;
    }

    private static bool IsProblemBlockEntity(AssetLocation? blockCode, string data)
    {
        if (blockCode?.Path.StartsWith("fruitpress", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return data.Contains("BlockEntityFruitPress", StringComparison.OrdinalIgnoreCase)
            || data.Contains("fruitpress", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlockCode(AssetLocation location, string path)
    {
        if (string.Equals(location.Path, path, StringComparison.OrdinalIgnoreCase)
            || string.Equals(location.ToShortString(), path, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedPath = path.Contains(':', StringComparison.Ordinal)
            ? new AssetLocation(path).Path
            : path;
        return string.Equals(location.Path, normalizedPath, StringComparison.OrdinalIgnoreCase);
    }
}
