using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Точка привязки сюжетного острова на карте.</summary>
internal enum StoryDungeonAnchor
{
    CornerNorthEast = 0,
    CornerNorthWest = 1,
    CornerSouthEast = 2,
    CornerSouthWest = 3,
    EdgeNorthCenter = 4
}

/// <summary>Способ встраивания сюжетной схематики в остров.</summary>
internal enum StoryDungeonPlacement
{
    /// <summary>Над/в поверхности острова (ванильный Y-offset).</summary>
    Surface = 0,
    /// <summary>Утоплен в остров, виден только вход.</summary>
    BuriedRuin = 1,
    /// <summary>Глубоко под поверхностью острова.</summary>
    Underground = 2
}

/// <summary>Описание одной сюжетной локации SkyBlock.</summary>
internal sealed class StoryDungeonDefinition
{
    public required string Code { get; init; }
    public required string LangKey { get; init; }
    /// <summary>Код структуры из game:worldgen/storystructures.json.</summary>
    public required string StructureCode { get; init; }
    public required StoryDungeonAnchor Anchor { get; init; }
    public required StoryDungeonPlacement Placement { get; init; }
    public int CornerInset { get; init; }
    public int SchematicYOffset { get; init; }
    public int EntranceRevealBlocks { get; init; }
    public int IslandRadius { get; init; }
    public int IslandDepth { get; init; }
    public int StoryOrder { get; init; }
    public string? WorldgenHook { get; init; }
}

/// <summary>Статические определения сюжетных данжей в углах карты.</summary>
internal static class StoryDungeonDefinitions
{
    public const int PlacementVersion = 18;

    public static IReadOnlyList<StoryDungeonDefinition> All { get; } =
    [
        new()
        {
            Code = "lazaret",
            LangKey = "swixyskyblock:story-site-lazaret",
            StructureCode = "lazaret",
            Anchor = StoryDungeonAnchor.CornerNorthEast,
            Placement = StoryDungeonPlacement.BuriedRuin,
            CornerInset = 280,
            EntranceRevealBlocks = 10,
            IslandRadius = 80,
            IslandDepth = 58,
            StoryOrder = 1
        },
        new()
        {
            Code = "village",
            LangKey = "swixyskyblock:story-site-village",
            StructureCode = "village",
            Anchor = StoryDungeonAnchor.CornerNorthWest,
            Placement = StoryDungeonPlacement.Surface,
            CornerInset = 420,
            SchematicYOffset = -4,
            IslandRadius = 140,
            IslandDepth = 8,
            StoryOrder = 2
        },
        new()
        {
            Code = "devastation",
            LangKey = "swixyskyblock:story-site-devastation",
            StructureCode = "devastationarea",
            Anchor = StoryDungeonAnchor.CornerSouthEast,
            Placement = StoryDungeonPlacement.Surface,
            CornerInset = 360,
            SchematicYOffset = -23,
            IslandRadius = 96,
            IslandDepth = 10,
            StoryOrder = 3
        },
        new()
        {
            Code = "tobiascave",
            LangKey = "swixyskyblock:story-site-tobiascave",
            StructureCode = "tobiascave",
            Anchor = StoryDungeonAnchor.CornerSouthWest,
            Placement = StoryDungeonPlacement.BuriedRuin,
            CornerInset = 520,
            EntranceRevealBlocks = 14,
            IslandRadius = 136,
            IslandDepth = 72,
            StoryOrder = 4
        },
        new()
        {
            Code = "resoarchive",
            LangKey = "swixyskyblock:story-site-resoarchive",
            StructureCode = "resonancearchive",
            Anchor = StoryDungeonAnchor.EdgeNorthCenter,
            Placement = StoryDungeonPlacement.Underground,
            CornerInset = 380,
            EntranceRevealBlocks = 8,
            IslandRadius = 168,
            IslandDepth = 108,
            StoryOrder = 5,
            WorldgenHook = "resonancearchive"
        }
    ];

    public static StoryDungeonDefinition? TryGet(string code)
    {
        foreach (var definition in All)
        {
            if (string.Equals(definition.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return definition;
            }
        }

        return null;
    }

    public static BlockPos ComputeSiteCenter(ICoreServerAPI api, StoryDungeonDefinition definition)
    {
        var mapX = api.WorldManager.MapSizeX;
        var mapZ = api.WorldManager.MapSizeZ;
        var surfaceY = Math.Clamp(
            SkyBlockRuntime.Config.IslandSurfaceY,
            1,
            Math.Max(1, api.WorldManager.MapSizeY - 4));
        var inset = Math.Max(64, definition.CornerInset);

        var x = definition.Anchor switch
        {
            StoryDungeonAnchor.CornerNorthEast or StoryDungeonAnchor.CornerSouthEast => mapX - inset,
            StoryDungeonAnchor.CornerNorthWest or StoryDungeonAnchor.CornerSouthWest => inset,
            StoryDungeonAnchor.EdgeNorthCenter => mapX / 2,
            _ => mapX / 2
        };
        var z = definition.Anchor switch
        {
            StoryDungeonAnchor.CornerNorthEast or StoryDungeonAnchor.CornerNorthWest or StoryDungeonAnchor.EdgeNorthCenter => inset,
            StoryDungeonAnchor.CornerSouthEast or StoryDungeonAnchor.CornerSouthWest => mapZ - inset,
            _ => inset
        };

        return new BlockPos(x, surfaceY, z);
    }
}
