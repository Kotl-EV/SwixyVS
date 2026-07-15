using SwixyQuestBook.Gui;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixyQuestBook.Helpers
{
    // Общий helper для чтения и списания предметов из инвентаря игрока.
    // Используется и клиентом для отображения прогресса, и сервером для авторитетной сдачи квеста.
    public static class QuestbookInventoryHelper
    {
        /// <summary>
        /// Snapshot of player quest inventories for O(1) exact lookups and cheap wildcard scans.
        /// Built once per frame / refresh interval instead of rescanning per quest item.
        /// </summary>
        public sealed class InventorySnapshot
        {
            private readonly Dictionary<string, int> exactCounts;
            private readonly List<(string Code, int Count)> stacks;

            public InventorySnapshot(Dictionary<string, int> exactCounts, List<(string Code, int Count)> stacks)
            {
                this.exactCounts = exactCounts;
                this.stacks = stacks;
            }

            public static InventorySnapshot Empty { get; } = new(
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                []);

            public int Count(string collectibleCode)
            {
                if (string.IsNullOrWhiteSpace(collectibleCode))
                {
                    return 0;
                }

                if (!collectibleCode.Contains('*'))
                {
                    return exactCounts.TryGetValue(collectibleCode, out int count) ? count : 0;
                }

                int total = 0;
                foreach ((string code, int count) in stacks)
                {
                    if (MatchesCollectibleCode(code, collectibleCode))
                    {
                        total += count;
                    }
                }

                return total;
            }

            /// <summary>Fast content fingerprint for dirty-checking UI without full dialog rebuilds every frame.</summary>
            public int ContentHash
            {
                get
                {
                    int hash = stacks.Count;
                    foreach ((string code, int count) in stacks)
                    {
                        hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(code);
                        hash = (hash * 397) ^ count;
                    }

                    return hash;
                }
            }
        }

        public static bool MatchesCollectibleCode(string itemCode, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            if (!pattern.Contains('*'))
            {
                return string.Equals(itemCode, pattern, StringComparison.OrdinalIgnoreCase);
            }

            string prefix = pattern.TrimEnd('*');
            string suffix = pattern.TrimStart('*');
            string middle = pattern.Trim('*');

            if (pattern.StartsWith('*') && pattern.EndsWith('*'))
            {
                return itemCode.Contains(middle, StringComparison.OrdinalIgnoreCase);
            }
            if (pattern.StartsWith('*'))
            {
                return itemCode.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
            }
            if (pattern.EndsWith('*'))
            {
                return itemCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(itemCode, pattern, StringComparison.OrdinalIgnoreCase);
        }

        public static List<InventoryBase> GetPlayerQuestInventories(IPlayer? player)
        {
            List<InventoryBase> inventories = [];
            if (player?.InventoryManager == null)
            {
                return inventories;
            }

            IInventory? hotbarInventory = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
            if (hotbarInventory is InventoryBase hotbarInventoryBase)
            {
                inventories.Add(hotbarInventoryBase);
            }

            IInventory? backpackInventory = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
            if (backpackInventory is InventoryBase backpackInventoryBase)
            {
                inventories.Add(backpackInventoryBase);
            }

            return inventories;
        }

        public static InventorySnapshot BuildSnapshot(IPlayer? player)
        {
            if (player?.InventoryManager == null)
            {
                return InventorySnapshot.Empty;
            }

            var exactCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var stacks = new List<(string Code, int Count)>(32);

            foreach (InventoryBase inventory in GetPlayerQuestInventories(player))
            {
                foreach (ItemSlot slot in inventory)
                {
                    ItemStack? stack = slot.Itemstack;
                    if (stack?.Collectible?.Code == null || stack.StackSize <= 0)
                    {
                        continue;
                    }

                    string code = stack.Collectible.Code.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(code))
                    {
                        continue;
                    }

                    stacks.Add((code, stack.StackSize));
                    exactCounts[code] = exactCounts.TryGetValue(code, out int existing)
                        ? existing + stack.StackSize
                        : stack.StackSize;
                }
            }

            return new InventorySnapshot(exactCounts, stacks);
        }

        public static int CountCollectibles(IPlayer? player, string collectibleCode)
        {
            return BuildSnapshot(player).Count(collectibleCode);
        }

        public static int CountCollectibles(InventorySnapshot snapshot, string collectibleCode)
        {
            return snapshot.Count(collectibleCode);
        }

        public static bool HasAllRequiredCollectibles(IPlayer? player, QuestbookQuestItemRequirement[] requiredItems)
        {
            if (requiredItems.Length == 0)
            {
                return true;
            }

            InventorySnapshot snapshot = BuildSnapshot(player);
            foreach (QuestbookQuestItemRequirement item in requiredItems)
            {
                if (snapshot.Count(item.CollectibleCode) < item.Count)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryConsumeCollectibles(IPlayer? player, string collectibleCode, int quantity)
        {
            if (quantity <= 0)
            {
                return true;
            }

            if (player == null || CountCollectibles(player, collectibleCode) < quantity)
            {
                return false;
            }

            int remaining = quantity;
            foreach (InventoryBase inventory in GetPlayerQuestInventories(player))
            {
                for (int slotId = 0; slotId < inventory.Count && remaining > 0; slotId++)
                {
                    ItemSlot slot = inventory[slotId];
                    ItemStack? stack = slot.Itemstack;
                    if (stack?.Collectible?.Code == null)
                    {
                        continue;
                    }

                    if (!MatchesCollectibleCode(stack.Collectible.Code.ToString(), collectibleCode))
                    {
                        continue;
                    }

                    int extractCount = System.Math.Min(remaining, stack.StackSize);
                    ItemStack extractedStack = slot.TakeOut(extractCount);
                    inventory.DidModifyItemSlot(slot, extractedStack);
                    remaining -= extractCount;
                }
            }

            return remaining <= 0;
        }

        public static bool TryConsumeCollectibles(IPlayer? player, QuestbookQuestItemRequirement[] requiredItems)
        {
            if (requiredItems.Length == 0)
            {
                return true;
            }

            if (!HasAllRequiredCollectibles(player, requiredItems))
            {
                return false;
            }

            foreach (QuestbookQuestItemRequirement item in requiredItems)
            {
                if (!TryConsumeCollectibles(player, item.CollectibleCode, item.Count))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryGiveCollectibles(IPlayer? player, string collectibleCode, int quantity)
        {
            if (quantity <= 0 || string.IsNullOrWhiteSpace(collectibleCode))
            {
                return true;
            }

            if (player == null || player.Entity?.World == null)
            {
                return false;
            }

            CollectibleObject? collectible = player.Entity.World.GetItem(new AssetLocation(collectibleCode));
            collectible ??= player.Entity.World.GetBlock(new AssetLocation(collectibleCode));
            if (collectible == null)
            {
                return false;
            }

            int remaining = quantity;
            foreach (InventoryBase inventory in GetPlayerQuestInventories(player))
            {
                for (int slotId = 0; slotId < inventory.Count && remaining > 0; slotId++)
                {
                    ItemSlot slot = inventory[slotId];
                    if (slot.Itemstack == null || slot.Itemstack.Collectible == null)
                    {
                        slot.Itemstack = new ItemStack(collectible, 0);
                    }

                    if (!string.Equals(slot.Itemstack.Collectible.Code.ToString(), collectibleCode, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int space = slot.Itemstack.Collectible.MaxStackSize - slot.Itemstack.StackSize;
                    if (space <= 0)
                    {
                        continue;
                    }

                    int addCount = System.Math.Min(remaining, space);
                    slot.Itemstack.StackSize += addCount;
                    remaining -= addCount;
                    inventory.DidModifyItemSlot(slot, slot.Itemstack);
                }
            }

            return remaining <= 0;
        }

        public static bool TryGiveCollectibles(IPlayer? player, QuestbookQuestItemRequirement[] rewardItems)
        {
            if (rewardItems.Length == 0)
            {
                return true;
            }

            foreach (QuestbookQuestItemRequirement item in rewardItems)
            {
                if (!TryGiveCollectibles(player, item.CollectibleCode, item.Count))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
