using System;
using System.Collections.Generic;
using System.Linq;
using SwixySkyBlock.Net;

namespace SwixySkyBlock;

internal static class IslandGeneratorStateBuilder
{
    public const string DefaultCostItemCode = "game:gear-rusty";
    public const int DefaultCostQuantity = 1;

    public static int GetMaxLevel(SkyBlockGeneratorConfig? config)
    {
        var levels = config?.Levels;
        if (levels == null || levels.Count == 0)
        {
            return 1;
        }

        return Math.Max(1, levels.Max(static level => Math.Max(1, level.Level)));
    }

    public static IReadOnlyList<SkyBlockGeneratorEntryConfig> GetEntriesForLevel(SkyBlockGeneratorConfig? config, int level)
    {
        var levels = config?.Levels;
        if (levels == null || levels.Count == 0)
        {
            levels = SkyBlockGeneratorLevelConfig.DefaultLevels();
        }

        var normalizedLevel = Math.Clamp(level, 1, GetMaxLevel(config));
        var entries = levels
            .Where(entry => entry.Level <= normalizedLevel && entry.Entries is { Count: > 0 })
            .OrderBy(static entry => entry.Level)
            .SelectMany(static entry => entry.Entries!)
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.BlockCode) && entry.Chance > 0)
            .GroupBy(static entry => entry.BlockCode, StringComparer.Ordinal)
            .Select(static group => new SkyBlockGeneratorEntryConfig
            {
                BlockCode = group.Key,
                Chance = group.Sum(static entry => entry.Chance)
            })
            .ToList();

        if (entries.Count > 0)
        {
            return entries;
        }

        return SkyBlockGeneratorLevelConfig.DefaultLevels()[0].Entries;
    }

    public static IslandGeneratorStatePacket Build(
        SkyBlockGeneratorConfig? config,
        bool hasIsland,
        int currentLevel,
        int playerCostCount = 0,
        string message = "",
        Func<string, int>? variantCountResolver = null,
        Func<string, string>? displayBlockResolver = null)
    {
        var maxLevel = GetMaxLevel(config);
        currentLevel = Math.Clamp(currentLevel, 1, maxLevel);
        variantCountResolver ??= static _ => 0;
        displayBlockResolver ??= static code => code;

        var packet = new IslandGeneratorStatePacket
        {
            CurrentLevel = currentLevel,
            MaxLevel = maxLevel,
            HasIsland = hasIsland,
            CanUpgrade = hasIsland && currentLevel < maxLevel,
            CostItemCode = DefaultCostItemCode,
            CostQuantity = DefaultCostQuantity,
            PlayerCostItemCount = Math.Max(0, playerCostCount),
            Message = message ?? ""
        };

        var levels = config?.Levels;
        if (levels == null || levels.Count == 0)
        {
            levels = SkyBlockGeneratorLevelConfig.DefaultLevels();
        }

        foreach (var level in levels
            .Select(static level => Math.Max(1, level.Level))
            .Distinct()
            .OrderBy(static level => level))
        {
            var entries = GetEntriesForLevel(config, level);
            var totalChance = entries.Sum(static entry => Math.Max(0, entry.Chance));
            var levelPacket = new IslandGeneratorLevelStatePacket
            {
                Level = level,
                Unlocked = hasIsland && level <= currentLevel
            };

            foreach (var entry in entries)
            {
                var chance = Math.Max(0, entry.Chance);
                if (chance <= 0)
                {
                    continue;
                }

                levelPacket.Entries.Add(new IslandGeneratorEntryStatePacket
                {
                    BlockCode = entry.BlockCode,
                    Chance = chance,
                    Percent = totalChance <= 0 ? 0 : chance / totalChance * 100,
                    VariantCount = Math.Max(0, variantCountResolver(entry.BlockCode)),
                    DisplayBlockCode = displayBlockResolver(entry.BlockCode) ?? entry.BlockCode
                });
            }

            packet.Levels.Add(levelPacket);
        }

        return packet;
    }
}