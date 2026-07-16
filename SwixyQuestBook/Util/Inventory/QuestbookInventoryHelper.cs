using SwixyQuestBook.Domain.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixyQuestBook.Util.Inventory
{
    /// <summary>
    /// Inventory helpers for quest progress UI (client) and authoritative submit (server).
    /// Server path prefers all-or-nothing exchange with refund / overflow drop — never silent item loss.
    /// </summary>
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

        /// <summary>
        /// Allowed patterns: exact code, <c>prefix*</c>, <c>*suffix</c>, <c>*middle*</c> (middle ≥ 2 chars).
        /// Bare <c>*</c> and empty wildcards are rejected.
        /// </summary>
        public static bool IsSafeWildcardPattern(string? pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return false;

            string p = pattern.Trim();
            if (!p.Contains('*'))
                return true;

            int starCount = 0;
            foreach (char c in p)
            {
                if (c == '*')
                    starCount++;
            }

            // Only one * or the *middle* form (exactly two stars at ends).
            if (starCount == 1)
            {
                if (p == "*")
                    return false;
                if (p.StartsWith('*'))
                    return p.Length >= 3; // *x
                if (p.EndsWith('*'))
                    return p.Length >= 3; // x*
                return false; // star in the middle without pair — unsupported
            }

            if (starCount == 2 && p.StartsWith('*') && p.EndsWith('*'))
            {
                string middle = p.Trim('*');
                return middle.Length >= 2;
            }

            return false;
        }

        public static bool MatchesCollectibleCode(string itemCode, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || !IsSafeWildcardPattern(pattern))
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

        public static List<InventoryBase> GetPlayerQuestInventories(IPlayer? player, bool includeCreative = false)
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

            // Creative catalog (items + creature entries when the player is in creative).
            // Class name is "creative" in vanilla GlobalConstants.creativeInvClassName.
            if (includeCreative)
            {
                foreach (string creativeName in new[] { "creative", "creativecontents", "character" })
                {
                    IInventory? creative = player.InventoryManager.GetOwnInventory(creativeName);
                    if (creative is InventoryBase creativeBase && !inventories.Contains(creativeBase))
                        inventories.Add(creativeBase);
                }
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

        /// <summary>
        /// Authoritative quest completion exchange: validate → consume per-item flags →
        /// give rewards (overflow spawns at player so items are never destroyed).
        /// Only <see cref="QuestbookQuestItemRequirement.Consume"/> have-goals are removed.
        /// </summary>
        public static bool TryCompleteQuestExchange(
            IPlayer? player,
            QuestbookQuestItemRequirement[] requiredItems,
            QuestbookQuestItemRequirement[] rewardItems,
            out string? failureReason)
        {
            failureReason = null;
            requiredItems ??= [];
            rewardItems ??= [];

            if (player == null || player.Entity?.World == null)
            {
                failureReason = "no-player";
                return false;
            }

            if (requiredItems.Length > 0 && !HasAllRequiredCollectibles(player, requiredItems))
            {
                failureReason = "missing-requirements";
                return false;
            }

            // Resolve rewards before touching inventory.
            var resolvedRewards = new List<(CollectibleObject Collectible, int Count, string Code)>(rewardItems.Length);
            foreach (QuestbookQuestItemRequirement reward in rewardItems)
            {
                if (reward.Count <= 0 || string.IsNullOrWhiteSpace(reward.CollectibleCode))
                    continue;

                CollectibleObject? collectible = player.Entity.World.GetItem(new AssetLocation(reward.CollectibleCode));
                collectible ??= player.Entity.World.GetBlock(new AssetLocation(reward.CollectibleCode));
                if (collectible == null)
                {
                    failureReason = "invalid-reward:" + reward.CollectibleCode;
                    return false;
                }

                resolvedRewards.Add((collectible, reward.Count, reward.CollectibleCode));
            }

            // Per-item: only take items flagged Consume (detect-only goals stay in inventory).
            QuestbookQuestItemRequirement[] toConsume = requiredItems
                .Where(static i => i.Consume && !i.IsCraftObjective)
                .ToArray();

            var extracted = new List<ItemStack>(8);
            if (toConsume.Length > 0
                && !TryConsumeAllRecording(player, toConsume, extracted, out failureReason))
            {
                RefundStacks(player, extracted);
                return false;
            }

            foreach ((CollectibleObject collectible, int count, string code) in resolvedRewards)
            {
                if (!TryGiveOrDrop(player, collectible, count, code))
                {
                    // Should not happen (drop fallback). Refund goals if it did.
                    failureReason = "give-failed:" + code;
                    RefundStacks(player, extracted);
                    return false;
                }
            }

            return true;
        }

        /// <param name="consumeRequiredItems">
        /// Bulk override: when false, no goals are taken; when true, all non-craft goals are taken.
        /// Prefer the per-item overload when flags differ per goal.
        /// </param>
        public static bool TryCompleteQuestExchange(
            IPlayer? player,
            QuestbookQuestItemRequirement[] requiredItems,
            QuestbookQuestItemRequirement[] rewardItems,
            bool consumeRequiredItems,
            out string? failureReason)
        {
            requiredItems ??= [];
            var mapped = requiredItems.Select(i => new QuestbookQuestItemRequirement(
                i.CollectibleCode,
                i.Count,
                i.Objective,
                consume: consumeRequiredItems && !i.IsCraftObjective)).ToArray();
            return TryCompleteQuestExchange(player, mapped, rewardItems, out failureReason);
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

            var extracted = new List<ItemStack>(4);
            if (!TryConsumeOneRecording(player, collectibleCode, quantity, extracted))
            {
                RefundStacks(player, extracted);
                return false;
            }

            return true;
        }

        public static bool TryConsumeCollectibles(IPlayer? player, QuestbookQuestItemRequirement[] requiredItems)
        {
            return TryCompleteQuestExchange(player, requiredItems, [], out _);
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

            return TryGiveOrDrop(player, collectible, quantity, collectibleCode);
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

        private static bool TryConsumeAllRecording(
            IPlayer player,
            QuestbookQuestItemRequirement[] requiredItems,
            List<ItemStack> extracted,
            out string? failureReason)
        {
            failureReason = null;
            foreach (QuestbookQuestItemRequirement item in requiredItems)
            {
                if (item.Count <= 0 || string.IsNullOrWhiteSpace(item.CollectibleCode))
                    continue;

                if (!TryConsumeOneRecording(player, item.CollectibleCode, item.Count, extracted))
                {
                    failureReason = "consume-failed:" + item.CollectibleCode;
                    return false;
                }
            }

            return true;
        }

        private static bool TryConsumeOneRecording(
            IPlayer player,
            string collectibleCode,
            int quantity,
            List<ItemStack> extracted)
        {
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
                    if (extractedStack != null && extractedStack.StackSize > 0)
                    {
                        extracted.Add(extractedStack.Clone());
                    }

                    remaining -= extractCount;
                }
            }

            return remaining <= 0;
        }

        /// <summary>
        /// Place into hotbar/backpack; any remainder is dropped at the player's feet.
        /// Returns false only if the collectible could not be spawned at all.
        /// </summary>
        private static bool TryGiveOrDrop(
            IPlayer player,
            CollectibleObject collectible,
            int quantity,
            string collectibleCode)
        {
            if (quantity <= 0)
                return true;

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

                    if (!string.Equals(
                            slot.Itemstack.Collectible.Code.ToString(),
                            collectibleCode,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        // Empty placeholder from failed type match — clear zero stacks.
                        if (slot.Itemstack.StackSize <= 0)
                        {
                            slot.Itemstack = null;
                            inventory.DidModifyItemSlot(slot);
                        }

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

                // Fill truly empty slots with new stacks.
                for (int slotId = 0; slotId < inventory.Count && remaining > 0; slotId++)
                {
                    ItemSlot slot = inventory[slotId];
                    if (slot.Itemstack != null)
                        continue;

                    int addCount = System.Math.Min(remaining, collectible.MaxStackSize);
                    slot.Itemstack = new ItemStack(collectible, addCount);
                    remaining -= addCount;
                    inventory.DidModifyItemSlot(slot, slot.Itemstack);
                }
            }

            if (remaining <= 0)
                return true;

            // Overflow: drop at feet so the player never loses rewards after consume.
            return SpawnItemNearPlayer(player, new ItemStack(collectible, remaining));
        }

        private static void RefundStacks(IPlayer player, List<ItemStack> stacks)
        {
            if (stacks.Count == 0)
                return;

            foreach (ItemStack stack in stacks)
            {
                if (stack?.Collectible == null || stack.StackSize <= 0)
                    continue;

                string code = stack.Collectible.Code?.ToString() ?? string.Empty;
                if (!TryGiveOrDrop(player, stack.Collectible, stack.StackSize, code))
                {
                    SpawnItemNearPlayer(player, stack.Clone());
                }
            }

            stacks.Clear();
        }

        private static bool SpawnItemNearPlayer(IPlayer player, ItemStack stack)
        {
            try
            {
                if (player.Entity?.World == null || stack == null || stack.StackSize <= 0)
                    return false;

                Vec3d pos = player.Entity.Pos.XYZ.Add(0, 0.5, 0);
                player.Entity.World.SpawnItemEntity(stack, pos);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
