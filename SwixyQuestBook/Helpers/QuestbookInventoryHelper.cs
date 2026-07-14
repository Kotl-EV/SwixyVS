using SwixyQuestBook.Gui;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixyQuestBook.Helpers
{
    // Общий helper для чтения и списания предметов из инвентаря игрока.
    // Используется и клиентом для отображения прогресса, и сервером для авторитетной сдачи квеста.
    public static class QuestbookInventoryHelper
    {
        private static bool MatchesCollectibleCode(string itemCode, string pattern)
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

        public static int CountCollectibles(IPlayer? player, string collectibleCode)
        {
            if (string.IsNullOrWhiteSpace(collectibleCode))
            {
                return 0;
            }

            int totalCount = 0;
            foreach (InventoryBase inventory in GetPlayerQuestInventories(player))
            {
                foreach (ItemSlot slot in inventory)
                {
                    ItemStack? stack = slot.Itemstack;
                    if (stack?.Collectible?.Code == null)
                    {
                        continue;
                    }

                    if (!MatchesCollectibleCode(stack.Collectible.Code.ToString(), collectibleCode))
                    {
                        continue;
                    }

                    totalCount += stack.StackSize;
                }
            }

            return totalCount;
        }

        public static bool HasAllRequiredCollectibles(IPlayer? player, QuestbookQuestItemRequirement[] requiredItems)
        {
            if (requiredItems.Length == 0)
            {
                return true;
            }

            foreach (QuestbookQuestItemRequirement item in requiredItems)
            {
                if (CountCollectibles(player, item.CollectibleCode) < item.Count)
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
