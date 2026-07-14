using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace SwixySkyBlock;

/// <summary>Запекает rockTypeRemapGroup в схематику до размещения (bulk Place не вызывает PlacePartial-ремап).</summary>
internal static class StorySchematicRockRemap
{
    public static void Bake(
        ICoreServerAPI api,
        BlockSchematic schematic,
        WorldGenStoryStructure structure,
        Block centerRock)
    {
        var remaps = StoryStructureRuntime.GetRockRemaps(structure);
        if (remaps == null || remaps.Count == 0)
        {
            return;
        }

        var activeRemap = ResolveActiveRemap(remaps, centerRock.BlockId);
        if (activeRemap == null || activeRemap.Count == 0)
        {
            api.Logger.Warning(
                "[SwixySkyBlock] No rock remap table for structure '{0}' (rock block id {1}).",
                structure.Code,
                centerRock.BlockId);
            return;
        }

        var count = Math.Min(schematic.Indices.Count, schematic.BlockIds.Count);
        var remapped = 0;
        for (var i = 0; i < count; i++)
        {
            var code = schematic.BlockIds[i];
            if (code == 0 || !activeRemap.TryGetValue(code, out var mapped) || mapped == 0)
            {
                continue;
            }

            schematic.BlockIds[i] = mapped;
            remapped++;
        }

        var dropped = DropUnresolvedBlockCodes(api, schematic, structure.Code);
        if (remapped > 0 || dropped > 0)
        {
            api.Logger.Notification(
                "[SwixySkyBlock] Story schematic '{0}': baked {1} rock remap(s), dropped {2} unresolved block(s).",
                structure.Code,
                remapped,
                dropped);
        }
    }

    private static Dictionary<int, int>? ResolveActiveRemap(
        Dictionary<int, Dictionary<int, int>> remaps,
        int centerRockBlockId)
    {
        if (remaps.TryGetValue(centerRockBlockId, out var exact))
        {
            return exact;
        }

        return remaps.Values.FirstOrDefault(group => group.Count > 0);
    }

    private static int DropUnresolvedBlockCodes(ICoreServerAPI api, BlockSchematic schematic, string structureCode)
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
}