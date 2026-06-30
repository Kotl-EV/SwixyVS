using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

/// <summary>Параметры мира SkyBlock.</summary>
internal static class SkyBlockWorld
{
    public const string WorldType = "empty";
    public const string PlayStyle = "skyblock";
    public const string LegacyPlayStyle = "preset-skyblock";
    public const string SaveKeyIslandTemplate = "swixyskyblock_island_template";
    public const string SaveKeyIslandOrigin = "swixyskyblock_island_origin";
    public const string SaveKeyPlayerIslands = "swixyskyblock_player_islands";
    public const string SaveKeyIslandResidency = "swixyskyblock_island_residency";
    public const string CoOwnersSaveKey = "swixyskyblock_coowners";
    public const string IslandClaimDescriptionPrefix = "SkyBlock:";
    public const int IslandClaimChunkRadius = 2;

    /// <summary>Минимальный шаг сетки островов = диаметр привата в блоках (5×5 чанков).</summary>
    public static int MinIslandSpacingBlocks =>
        (2 * IslandClaimChunkRadius + 1) * GlobalConstants.ChunkSize;

    /// <summary>Старые сейвы до фикса координат (бывшая высота 130).</summary>
    public const int LegacyIslandSurfaceY = 130;

    public static readonly BlockPos LegacyIslandOrigin = new(0, LegacyIslandSurfaceY, 0);

    /// <summary>Типы мира, для которых активна генерация SkyBlock (skyblock — legacy-сейвы).</summary>
    public static readonly string[] SupportedWorldTypes = ["empty", "superflat", "skyblock"];

    /// <summary>Центр карты — HUD и миникарта считают спавн отсюда.</summary>
    public static BlockPos ComputeIslandOrigin(ICoreServerAPI api)
    {
        var y = Math.Clamp(
            SwixySkyBlockMod.Config.IslandSurfaceY,
            1,
            Math.Max(1, api.WorldManager.MapSizeY - 4));
        return new BlockPos(api.WorldManager.MapSizeX / 2, y, api.WorldManager.MapSizeZ / 2);
    }

    public static BlockPos ComputePlayerIslandOrigin(ICoreServerAPI api, int slotIndex)
    {
        var spacing = Math.Max(SkyBlockWorld.MinIslandSpacingBlocks, SwixySkyBlockMod.Config.IslandSpacing);
        var gridWidth = Math.Max(8, (int)Math.Ceiling(Math.Sqrt(slotIndex + 1)) + 4);
        var gx = slotIndex % gridWidth;
        var gz = slotIndex / gridWidth;
        var center = ComputeIslandOrigin(api);
        var ox = center.X + (gx - gridWidth / 2) * spacing;
        var oz = center.Z + (gz - gridWidth / 2) * spacing;
        return new BlockPos(ox, center.Y, oz);
    }

    public static bool IsSkyBlockPlayStyle(ICoreServerAPI api) =>
        IsSkyBlockWorld(api);

    public static bool IsSkyBlockWorld(ICoreServerAPI api) =>
        IsSkyBlockWorld(api.World, api.WorldManager.SaveGame?.PlayStyle, api.WorldManager.SaveGame.GetData);

    /// <summary>
    /// SkyBlock-мир: по playstyle, данным сейва (сервер) или типу мира empty/superflat/skyblock (клиент).
    /// </summary>
    public static bool IsSkyBlockWorld(IWorldAccessor world, string? savePlayStyle = null, System.Func<string, byte[]?>? getSaveData = null)
    {
        if (MatchesPlayStyle(world, savePlayStyle))
        {
            return true;
        }

        var worldType = GetWorldType(world);
        if (!IsSupportedWorldType(worldType))
        {
            return false;
        }

        if (getSaveData != null)
        {
            return getSaveData(SaveKeyIslandTemplate) != null
                || getSaveData(SaveKeyPlayerIslands) != null
                || getSaveData(SaveKeyIslandOrigin) != null;
        }

        // На клиенте playstyle часто не синхронизирован — worldType empty достаточен для нашего пресета.
        return true;
    }

    public static bool IsSkyBlockPlayStyle(IWorldAccessor world, string? savePlayStyle = null) =>
        IsSkyBlockWorld(world, savePlayStyle);

    private static bool MatchesPlayStyle(IWorldAccessor world, string? savePlayStyle)
    {
        var playStyle = world.Config.GetString("playstyle", "");
        if (string.IsNullOrEmpty(playStyle))
        {
            playStyle = world.Config.GetString("playStyle", "");
        }

        if (string.Equals(playStyle, PlayStyle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(playStyle, LegacyPlayStyle, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        savePlayStyle ??= "";
        return string.Equals(savePlayStyle, PlayStyle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(savePlayStyle, LegacyPlayStyle, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetWorldType(IWorldAccessor world) =>
        world.Config.GetString("worldType", "");

    private static bool IsSupportedWorldType(string worldType) =>
        SupportedWorldTypes.Any(t => string.Equals(worldType, t, StringComparison.OrdinalIgnoreCase));

    /// <summary>Y-уровень, ниже которого игрок считается упавшим в пустоту.</summary>
    public static int GetVoidFallThresholdY() => 0;

    public static void ApplyWorldConfig(ICoreServerAPI api)
    {
        if (!IsSkyBlockWorld(api))
        {
            return;
        }

        api.WorldManager.SaveGame.WorldType = WorldType;

        var cfg = api.World.Config;
        cfg.SetString("worldType", WorldType);
        cfg.SetString("playstyle", PlayStyle);
        cfg.SetString("playStyle", PlayStyle);
        cfg.SetString("gameMode", "survival");
        cfg.SetString("worldClimate", "realistic");
        cfg.SetString("seasons", "enabled");
        cfg.SetString("colorAccurateWorldmap", "true");
        cfg.SetString("spawnRadius", "0");
        cfg.SetString("temporalStability", "true");
        cfg.SetString("temporalStorms", "sometimes");
        cfg.SetString("temporalGearRespawnUses", "-1");
        cfg.SetString("temporalRifts", "off");
        cfg.SetString("loreContent", "false");
        cfg.SetString("allowLandClaiming", "true");
        cfg.SetString("allowMap", "true");
        cfg.SetString("allowCoordinateHud", "true");
        cfg.SetString("playerHealthPoints", "15");
        cfg.SetString("playerHungerSpeed", "1");
        cfg.SetString("deathPunishment", "drop");
        cfg.SetString("harshWinters", "false");
    }
}
