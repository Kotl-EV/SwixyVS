using System;
using System.Collections.Generic;
using System.Linq;
using SwixySkyBlock.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

public sealed partial class SwixySkyBlockMod
{
    private const int MinGeneratorLevel = 1;
    private const int GeneratorRestoreIntervalMs = 1000;
    private const string GeneratorUpgradeCostItemCode = "game:gear-rusty";
    private const int GeneratorUpgradeCostQuantity = 1;

    private IReadOnlyList<IslandTemplate>? islandGeneratorTemplatesCache;
    private readonly Dictionary<string, IReadOnlyList<Block>> generatorBlockMatchesCache = new(StringComparer.Ordinal);

    private void RegisterIslandGenerator(ICoreServerAPI api)
    {
        api.Event.BreakBlock += OnGeneratorBreakBlock;
        api.Event.RegisterGameTickListener(_ => RestoreMissingGeneratorBlocks(), GeneratorRestoreIntervalMs);
        api.Event.PlayerNowPlaying += SendGeneratorLabels;
        RegisterIslandGeneratorCommands(api);
        WarnUnknownGeneratorBlocks(api);
    }

    private void RegisterIslandGeneratorCommands(ICoreServerAPI api)
    {
        api.ChatCommands
            .Create("islandgen")
            .WithDescription("SkyBlock island generator")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(ShowGeneratorLevelCommand)
            .BeginSubCommand("level")
                .WithDescription("Show island generator level")
                .HandleWith(ShowGeneratorLevelCommand)
            .EndSubCommand()
            .BeginSubCommand("upgrade")
                .WithDescription("Upgrade island generator level")
                .HandleWith(UpgradeGeneratorCommand)
            .EndSubCommand()
            .Validate();
    }

    private TextCommandResult ShowGeneratorLevelCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player)
        {
            return TextCommandResult.Error("Only a player can use this command.");
        }

        var record = islandRegistry.Get(player.PlayerUID);
        if (record == null)
        {
            return TextCommandResult.Error("You do not have an island.");
        }

        NormalizeGeneratorLevel(record);
        return TextCommandResult.Success(
            $"Generator level: {record.GeneratorLevel}/{GetMaxGeneratorLevel()}.\n" +
            $"Active chances: {FormatGeneratorEntries(record.GeneratorLevel)}.");
    }

    private TextCommandResult UpgradeGeneratorCommand(TextCommandCallingArgs args)
    {
        if (serverApi == null || args.Caller.Player is not IServerPlayer player)
        {
            return TextCommandResult.Error("Only a player can use this command.");
        }

        var record = islandRegistry.Get(player.PlayerUID);
        if (record == null)
        {
            return TextCommandResult.Error("You do not have an island.");
        }

        NormalizeGeneratorLevel(record);
        var maxGeneratorLevel = GetMaxGeneratorLevel();
        if (record.GeneratorLevel >= maxGeneratorLevel)
        {
            return TextCommandResult.Success($"Generator is already at max level: {maxGeneratorLevel}.");
        }

        if (!TryTakeItems(player, GeneratorUpgradeCostItemCode, GeneratorUpgradeCostQuantity))
        {
            return TextCommandResult.Error("Need 1 rusty gear to upgrade the generator.");
        }

        record.GeneratorLevel++;
        islandRegistry.Save(serverApi);
        EnsureGeneratorBlock(record, ResolveIslandTemplate(record.TemplateName), replaceExistingGenerator: true);
        BroadcastGeneratorLabels();

        return TextCommandResult.Success($"Generator upgraded to level {record.GeneratorLevel}.");
    }

    private void OnGeneratorBreakBlock(
        IServerPlayer byPlayer,
        BlockSelection blockSel,
        ref float dropQuantityMultiplier,
        ref EnumHandling handling)
    {
        if (serverApi == null || !IsSkyBlockWorld || blockSel?.Position == null)
        {
            return;
        }

        if (!TryGetGeneratorAt(blockSel.Position, out var record, out var template))
        {
            return;
        }

        var accessor = serverApi.World.BlockAccessor;
        var currentBlock = accessor.GetBlock(blockSel.Position);
        if (currentBlock.Id == 0 || !IsGeneratorBlock(currentBlock))
        {
            return;
        }

        handling = EnumHandling.PreventDefault;

        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            SpawnGeneratorDrops(currentBlock, blockSel.Position, byPlayer, dropQuantityMultiplier);
        }

        var nextBlock = PickGeneratorBlock(record.GeneratorLevel);
        accessor.SetBlock(nextBlock.Id, blockSel.Position);
        accessor.MarkBlockModified(blockSel.Position);
        serverApi.World.SpawnCubeParticles(
            blockSel.Position,
            blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5),
            0.25f,
            12,
            1f,
            byPlayer);

        EnsureGeneratorBlock(record, template, replaceExistingGenerator: false);
    }

    private void SpawnGeneratorDrops(Block block, BlockPos pos, IServerPlayer player, float dropQuantityMultiplier)
    {
        if (serverApi == null)
        {
            return;
        }

        var drops = block.GetDrops(serverApi.World, pos, player, dropQuantityMultiplier);
        if (drops == null)
        {
            return;
        }

        foreach (var drop in drops)
        {
            if (drop == null || drop.StackSize <= 0)
            {
                continue;
            }

            if (block.SplitDropStacks)
            {
                for (var i = 0; i < drop.StackSize; i++)
                {
                    var itemStack = drop.Clone();
                    itemStack.StackSize = 1;
                    serverApi.World.SpawnItemEntity(itemStack, pos);
                }

                continue;
            }

            serverApi.World.SpawnItemEntity(drop.Clone(), pos);
        }
    }

    private void RestoreMissingGeneratorBlocks()
    {
        if (serverApi == null || !IsSkyBlockWorld)
        {
            return;
        }

        var templates = GetIslandGeneratorTemplates();
        foreach (var record in islandRegistry.All)
        {
            var template = templates.FirstOrDefault(t => t.Name == record.TemplateName)
                ?? (templates.Count > 0 ? IslandBlueprint.PickForWorld(templates) : null);
            EnsureGeneratorBlock(record, template, replaceExistingGenerator: false);
        }
    }

    private void EnsureGeneratorBlock(PlayerIslandRecord record, IslandTemplate? template, bool replaceExistingGenerator)
    {
        if (serverApi == null || template == null)
        {
            return;
        }

        NormalizeGeneratorLevel(record);
        var pos = GetGeneratorPosition(record, template);
        var accessor = serverApi.World.BlockAccessor;
        if (!accessor.IsValidPos(pos) || accessor.GetChunkAtBlockPos(pos) == null)
        {
            return;
        }

        var currentBlock = accessor.GetBlock(pos);
        if (currentBlock.Id != 0 && (!replaceExistingGenerator || !IsGeneratorBlock(currentBlock)))
        {
            return;
        }

        var block = PickGeneratorBlock(record.GeneratorLevel);
        accessor.SetBlock(block.Id, pos);
        accessor.MarkBlockModified(pos);
        BroadcastGeneratorLabels();
    }

    private bool TryGetGeneratorAt(BlockPos pos, out PlayerIslandRecord record, out IslandTemplate template)
    {
        record = null!;
        template = null!;

        if (serverApi == null)
        {
            return false;
        }

        var templates = GetIslandGeneratorTemplates();
        foreach (var islandRecord in islandRegistry.All)
        {
            var islandTemplate = templates.FirstOrDefault(t => t.Name == islandRecord.TemplateName)
                ?? (templates.Count > 0 ? IslandBlueprint.PickForWorld(templates) : null);
            if (islandTemplate == null)
            {
                continue;
            }

            if (!GetGeneratorPosition(islandRecord, islandTemplate).Equals(pos))
            {
                continue;
            }

            record = islandRecord;
            template = islandTemplate;
            return true;
        }

        return false;
    }

    private IReadOnlyList<IslandTemplate> GetIslandGeneratorTemplates()
    {
        if (serverApi == null)
        {
            return [];
        }

        islandGeneratorTemplatesCache ??= IslandBlueprint.LoadAll(serverApi);
        return islandGeneratorTemplatesCache;
    }

    private static BlockPos GetGeneratorPosition(PlayerIslandRecord record, IslandTemplate template) =>
        template.GetSpawnPosition(record.Origin);

    private void BroadcastGeneratorLabels()
    {
        if (serverApi == null)
        {
            return;
        }

        var packet = BuildGeneratorLabelsPacket();
        foreach (var player in serverApi.World.AllOnlinePlayers.OfType<IServerPlayer>())
        {
            serverChannel?.SendPacket(packet, player);
        }
    }

    private void SendGeneratorLabels(IServerPlayer player)
    {
        serverChannel?.SendPacket(BuildGeneratorLabelsPacket(), player);
    }

    private void OnGeneratorLabelsRequest(IServerPlayer player, IslandGeneratorLabelsRequestPacket _)
    {
        SendGeneratorLabels(player);
    }

    private void OnGeneratorStateRequest(IServerPlayer player, IslandGeneratorStateRequestPacket _)
    {
        if (serverChannel == null)
        {
            return;
        }

        try
        {
            var packet = BuildGeneratorStatePacket(player);
            serverChannel.SendPacket(packet, [player]);
            serverApi?.Logger.Notification(
                "[SwixySkyBlock][Generator] State sent to {0}: level={1}/{2}, levels={3}",
                player.PlayerName,
                packet.CurrentLevel,
                packet.MaxLevel,
                packet.Levels?.Count ?? 0);
        }
        catch (Exception ex)
        {
            serverApi?.Logger.Error(
                "[SwixySkyBlock][Generator] State request failed for {0}: {1}",
                player.PlayerName,
                ex);
            serverChannel.SendPacket(BuildGeneratorStatePacketSafe(player, Lang.Get("swixyskyblock:island-generator-load-error")), [player]);
        }
    }

    private void OnGeneratorUpgradeRequest(IServerPlayer player, IslandGeneratorUpgradeRequestPacket _)
    {
        if (serverApi == null)
        {
            return;
        }

        var record = islandRegistry.Get(player.PlayerUID);
        if (record == null)
        {
            serverChannel?.SendPacket(BuildGeneratorStatePacket(player, "You do not have an island."), player);
            return;
        }

        NormalizeGeneratorLevel(record);
        var maxGeneratorLevel = GetMaxGeneratorLevel();
        if (record.GeneratorLevel >= maxGeneratorLevel)
        {
            serverChannel?.SendPacket(BuildGeneratorStatePacket(player, $"Generator is already at max level: {maxGeneratorLevel}."), player);
            return;
        }

        if (!TryTakeItems(player, GeneratorUpgradeCostItemCode, GeneratorUpgradeCostQuantity))
        {
            serverChannel?.SendPacket(
                BuildGeneratorStatePacket(player, "Need 1 rusty gear to upgrade the generator."),
                player);
            return;
        }

        record.GeneratorLevel++;
        islandRegistry.Save(serverApi);
        EnsureGeneratorBlock(record, ResolveIslandTemplate(record.TemplateName), replaceExistingGenerator: true);
        BroadcastGeneratorLabels();

        serverChannel?.SendPacket(
            BuildGeneratorStatePacket(player, $"Generator upgraded to level {record.GeneratorLevel}."),
            player);
    }

    private IslandGeneratorStatePacket BuildGeneratorStatePacket(IServerPlayer player, string message = "") =>
        BuildGeneratorStatePacketSafe(player, message, countPlayerItems: true);

    private IslandGeneratorStatePacket BuildGeneratorStatePacketSafe(
        IServerPlayer player,
        string message = "",
        bool countPlayerItems = false)
    {
        var record = islandRegistry.Get(player.PlayerUID);
        if (record != null)
        {
            NormalizeGeneratorLevel(record);
        }

        return IslandGeneratorStateBuilder.Build(
            GeneratorConfig,
            record != null,
            record?.GeneratorLevel ?? MinGeneratorLevel,
            countPlayerItems ? CountItems(player, GeneratorUpgradeCostItemCode) : 0,
            message,
            GetCachedVariantCount,
            ResolveDisplayBlockCode);
    }

    private string ResolveDisplayBlockCode(string blockCode)
    {
        var blocks = ResolveGeneratorBlocks(blockCode);
        return blocks.Count > 0 ? blocks[0].Code.ToString() : blockCode;
    }

    private int GetCachedVariantCount(string blockCode)
    {
        if (generatorBlockMatchesCache.TryGetValue(blockCode, out var cachedBlocks))
        {
            return cachedBlocks.Count;
        }

        return blockCode.Contains('*', StringComparison.Ordinal) ? 0 : 1;
    }

    private static readonly string[] CountableInventoryIds =
    [
        "character",
        "backpack",
        "hotbar",
        "offhand",
        "craftinggrid"
    ];

    private static IEnumerable<IInventory> GetCountableInventories(IServerPlayer player)
    {
        var manager = player.InventoryManager;
        if (manager == null)
        {
            yield break;
        }

        foreach (var inventoryId in CountableInventoryIds)
        {
            var inventory = manager.GetOwnInventory(inventoryId);
            if (inventory != null)
            {
                yield return inventory;
            }
        }
    }

    private static int CountItems(IServerPlayer player, string itemCode)
    {
        var count = 0;
        foreach (var inventory in GetCountableInventories(player))
        {
            if (!TryCountItemsInInventory(inventory, itemCode, out var inventoryCount))
            {
                continue;
            }

            count += inventoryCount;
        }

        return count;
    }

    private static bool TryTakeItems(IServerPlayer player, string itemCode, int quantity)
    {
        if (CountItems(player, itemCode) < quantity)
        {
            return false;
        }

        var remaining = quantity;
        foreach (var inventory in GetCountableInventories(player))
        {
            if (!TryTakeItemsFromInventory(inventory, itemCode, ref remaining))
            {
                continue;
            }

            if (remaining <= 0)
            {
                return true;
            }
        }

        return remaining <= 0;
    }

    private static bool TryCountItemsInInventory(IInventory inventory, string itemCode, out int count)
    {
        count = 0;

        try
        {
            for (var slotId = 0; slotId < inventory.Count; slotId++)
            {
                var slot = inventory[slotId];
                if (slot?.Empty != false || slot.Itemstack?.Collectible?.Code == null)
                {
                    continue;
                }

                if (string.Equals(slot.Itemstack.Collectible.Code.ToString(), itemCode, StringComparison.Ordinal))
                {
                    count += slot.Itemstack.StackSize;
                }
            }

            return true;
        }
        catch (Exception)
        {
            count = 0;
            return false;
        }
    }

    private static bool TryTakeItemsFromInventory(IInventory inventory, string itemCode, ref int remaining)
    {
        try
        {
            for (var slotId = 0; slotId < inventory.Count && remaining > 0; slotId++)
            {
                var slot = inventory[slotId];
                if (slot?.Empty != false || slot.Itemstack?.Collectible?.Code == null)
                {
                    continue;
                }

                if (!string.Equals(slot.Itemstack.Collectible.Code.ToString(), itemCode, StringComparison.Ordinal))
                {
                    continue;
                }

                var taken = Math.Min(remaining, slot.Itemstack.StackSize);
                slot.TakeOut(taken);
                slot.MarkDirty();
                remaining -= taken;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private IslandGeneratorLabelsPacket BuildGeneratorLabelsPacket()
    {
        var packet = new IslandGeneratorLabelsPacket();
        if (serverApi == null)
        {
            return packet;
        }

        var templates = GetIslandGeneratorTemplates();
        foreach (var record in islandRegistry.All)
        {
            var template = templates.FirstOrDefault(t => t.Name == record.TemplateName)
                ?? (templates.Count > 0 ? IslandBlueprint.PickForWorld(templates) : null);
            if (template == null)
            {
                continue;
            }

            NormalizeGeneratorLevel(record);
            var pos = GetGeneratorPosition(record, template);
            packet.Labels.Add(new IslandGeneratorLabelPacket
            {
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
                Level = record.GeneratorLevel
            });
        }

        return packet;
    }

    private Block PickGeneratorBlock(int level)
    {
        var entries = ResolveGeneratorEntries(level);
        var totalChance = entries.Sum(static entry => entry.Chance);
        if (totalChance <= 0)
        {
            return serverApi!.World.GetBlock(new AssetLocation("game:soil-medium-normal"))
                ?? serverApi.World.BlockAccessor.GetBlock(0);
        }

        var roll = serverApi!.World.Rand.NextDouble() * totalChance;
        foreach (var entry in entries)
        {
            roll -= entry.Chance;
            if (roll >= 0)
            {
                continue;
            }

            return entry.PickBlock(serverApi.World.Rand);
        }

        return entries[^1].PickBlock(serverApi.World.Rand);
    }

    private List<ResolvedGeneratorEntry> ResolveGeneratorEntries(int level)
    {
        var entries = new List<ResolvedGeneratorEntry>();
        foreach (var entry in GetGeneratorEntries(level))
        {
            var chance = Math.Max(0, entry.Chance);
            if (chance <= 0)
            {
                continue;
            }

            var blocks = ResolveGeneratorBlocks(entry.BlockCode);
            if (blocks.Count == 0)
            {
                serverApi!.Logger.Warning(
                    "[SwixySkyBlock] Generator level {0} ignores unknown block code or wildcard: {1}",
                    level,
                    entry.BlockCode);
                continue;
            }

            entries.Add(new ResolvedGeneratorEntry(blocks, chance));
        }

        return entries;
    }

    private IReadOnlyList<Block> ResolveGeneratorBlocks(string blockCode)
    {
        if (serverApi == null)
        {
            return [];
        }

        if (generatorBlockMatchesCache.TryGetValue(blockCode, out var cachedBlocks))
        {
            return cachedBlocks;
        }

        IReadOnlyList<Block> blocks;
        if (blockCode.Contains('*', StringComparison.Ordinal))
        {
            blocks = serverApi.World.SearchBlocks(new AssetLocation(blockCode))
                .Where(static block => block.Id != 0)
                .OrderBy(static block => block.Code.ToString(), StringComparer.Ordinal)
                .ToArray();
        }
        else
        {
            var block = serverApi.World.GetBlock(new AssetLocation(blockCode));
            blocks = block == null || block.Id == 0 ? [] : [block];
        }

        generatorBlockMatchesCache[blockCode] = blocks;
        return blocks;
    }

    private string FormatGeneratorEntries(int level)
    {
        var entries = GetGeneratorEntries(level);
        var totalChance = entries.Sum(static entry => Math.Max(0, entry.Chance));
        if (totalChance <= 0)
        {
            return "none";
        }

        return string.Join(", ", entries.Select(entry =>
            $"{entry.BlockCode}={Math.Max(0, entry.Chance):0.###} ({Math.Max(0, entry.Chance) / totalChance:P1})"));
    }

    private static IReadOnlyList<SkyBlockGeneratorEntryConfig> GetGeneratorEntries(int level) =>
        IslandGeneratorStateBuilder.GetEntriesForLevel(GeneratorConfig, level);

    private static bool IsGeneratorBlock(Block block)
    {
        var code = block.Code?.ToString();
        return code != null
            && GeneratorConfig.Levels.Any(level =>
                level.Entries.Any(entry => MatchesGeneratorBlockCode(code, entry.BlockCode)));
    }

    private static bool MatchesGeneratorBlockCode(string blockCode, string configuredBlockCode)
    {
        if (!configuredBlockCode.Contains('*', StringComparison.Ordinal))
        {
            return string.Equals(configuredBlockCode, blockCode, StringComparison.Ordinal);
        }

        var parts = configuredBlockCode.Split('*');
        var index = 0;
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            var nextIndex = blockCode.IndexOf(part, index, StringComparison.Ordinal);
            if (nextIndex < 0)
            {
                return false;
            }

            index = nextIndex + part.Length;
        }

        return (parts[0].Length == 0 || blockCode.StartsWith(parts[0], StringComparison.Ordinal))
            && (parts[^1].Length == 0 || blockCode.EndsWith(parts[^1], StringComparison.Ordinal));
    }

    private void WarnUnknownGeneratorBlocks(ICoreServerAPI api)
    {
        foreach (var blockCode in GeneratorConfig.Levels
            .SelectMany(static level => level.Entries)
            .Select(static entry => entry.BlockCode)
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.Ordinal))
        {
            var blocks = ResolveGeneratorBlocks(blockCode);
            if (blocks.Count == 0)
            {
                api.Logger.Warning("[SwixySkyBlock] Generator config references unknown block code or wildcard: {0}", blockCode);
                continue;
            }

            if (blockCode.Contains('*', StringComparison.Ordinal))
            {
                api.Logger.Notification(
                    "[SwixySkyBlock] Generator wildcard {0} resolved to {1} block(s).",
                    blockCode,
                    blocks.Count);
            }
        }
    }

    private static void NormalizeGeneratorLevel(PlayerIslandRecord record) =>
        record.GeneratorLevel = Math.Clamp(record.GeneratorLevel, MinGeneratorLevel, GetMaxGeneratorLevel());

    private static int GetMaxGeneratorLevel() =>
        Math.Max(MinGeneratorLevel, IslandGeneratorStateBuilder.GetMaxLevel(GeneratorConfig));

    private readonly record struct ResolvedGeneratorEntry(IReadOnlyList<Block> Blocks, double Chance)
    {
        public Block PickBlock(Random rand) => Blocks[rand.Next(Blocks.Count)];
    }

}
