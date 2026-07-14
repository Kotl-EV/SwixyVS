using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace SwixySkyBlock;

/// <summary>Доступ к internal-полям WorldGenStoryStructure из VSEssentials.</summary>
internal static class StoryStructureRuntime
{
    private static readonly FieldInfo SchematicDataField =
        typeof(WorldGenStoryStructure).GetField("schematicData", InstanceFlags)!;

    private static readonly FieldInfo RockRemapsField =
        typeof(WorldGenStoryStructure).GetField("resolvedRockTypeRemaps", InstanceFlags)!;

    private static readonly FieldInfo ReplaceLayerIdsField =
        typeof(WorldGenStoryStructure).GetField("replacewithblocklayersBlockids", InstanceFlags)!;

    private static readonly FieldInfo GenStoryAccessorField =
        typeof(GenStoryStructures).GetField("worldgenBlockAccessor", InstanceFlags)!;

    private static readonly FieldInfo WorldApiServerField =
        typeof(ICoreServerAPI).Assembly.GetType("Vintagestory.Server.WorldAPI")?
            .GetField("server", InstanceFlags)!;

    private static IWorldGenBlockAccessor? cachedWorldgenAccessor;

    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static BlockSchematicPartial GetSchematic(WorldGenStoryStructure structure) =>
        (BlockSchematicPartial)SchematicDataField.GetValue(structure)!;

    public static Dictionary<int, Dictionary<int, int>>? GetRockRemaps(WorldGenStoryStructure structure) =>
        RockRemapsField.GetValue(structure) as Dictionary<int, Dictionary<int, int>>;

    public static int[]? GetReplaceLayerBlockIds(WorldGenStoryStructure structure) =>
        ReplaceLayerIdsField.GetValue(structure) as int[];

    public static IWorldGenBlockAccessor? TryGetWorldgenAccessor(ICoreServerAPI api)
    {
        if (cachedWorldgenAccessor != null)
        {
            return cachedWorldgenAccessor;
        }

        var storyGen = api.ModLoader.GetModSystem<GenStoryStructures>();
        if (storyGen != null
            && GenStoryAccessorField.GetValue(storyGen) is IWorldGenBlockAccessor storyAccessor)
        {
            cachedWorldgenAccessor = storyAccessor;
            return cachedWorldgenAccessor;
        }

        cachedWorldgenAccessor = CreateWorldgenAccessor(api);
        return cachedWorldgenAccessor;
    }

    private static IWorldGenBlockAccessor? CreateWorldgenAccessor(ICoreServerAPI api)
    {
        if (WorldApiServerField?.GetValue(api.WorldManager) is not object serverMain)
        {
            return null;
        }

        var serverType = serverMain.GetType();
        var worldMap = serverType.GetField("WorldMap", InstanceFlags)?.GetValue(serverMain);
        var chunkThread = serverType.GetField("chunkThread", InstanceFlags)?.GetValue(serverMain);
        if (worldMap == null || chunkThread == null)
        {
            return null;
        }

        var libAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(asm => string.Equals(asm.GetName().Name, "VintagestoryLib", StringComparison.OrdinalIgnoreCase));
        var accessorType = libAsm?.GetType("Vintagestory.Server.BlockAccessorWorldGen");
        var ctor = accessorType?.GetConstructor(InstanceFlags, null, [serverType, worldMap.GetType(), chunkThread.GetType()], null);
        return ctor?.Invoke([serverMain, worldMap, chunkThread]) as IWorldGenBlockAccessor;
    }
}