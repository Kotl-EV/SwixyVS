using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace SwixyQuestBook.Server.Crafting
{
    /// <summary>
    /// Listens to <c>onitemcrafted</c>. Quantity = how many of that item just entered
    /// the player's bags (hotbar / backpack / cursor) — same number as audit
    /// <c>… to (32x into hotbar)</c>, not vanilla's often-wrong stack size of 1 on shift-bulk.
    /// </summary>
    internal static class QuestbookCraftingHook
    {
        private static Action<IPlayer, string, int>? onCrafted;
        private static ICoreServerAPI? sapi;
        private static EventBusListenerDelegate? listener;
        private static long bagTickId = -1;
        private static bool registered;

        /// <summary>
        /// Last bag totals per player. Needed only so that at craft time we can read
        /// intoBags = after − before (= the “32x into hotbar” number from audit).
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, int>> BagBefore =
            new(StringComparer.Ordinal);

        public static void Apply(ICoreServerAPI api, Action<IPlayer, string, int> craftedHandler)
        {
            sapi = api;
            onCrafted = craftedHandler;
            if (registered)
                return;

            listener = OnEventBus;
            api.Event.RegisterEventBusListener(listener, filterByEventName: "onitemcrafted");
            api.Event.PlayerJoin += OnPlayerJoin;
            // Keep “before” current (pickup/drop). Craft event runs mid-frame; tick won’t interleave.
            bagTickId = api.Event.RegisterGameTickListener(_ =>
            {
                if (sapi?.World?.AllOnlinePlayers == null) return;
                foreach (IPlayer p in sapi.World.AllOnlinePlayers)
                    StoreBags(p);
            }, 1);

            foreach (IPlayer p in api.World.AllOnlinePlayers)
                StoreBags(p);

            registered = true;
            api.Logger.Notification("[SwixyQuestBook] Craft: onitemcrafted → intoBags qty → progress");
        }

        private static void OnPlayerJoin(IServerPlayer player) => StoreBags(player);

        public static void Unapply()
        {
            if (sapi != null && registered)
            {
                try { if (listener != null) sapi.Event.UnregisterEventBusListener(listener); } catch { /* ignore */ }
                try { sapi.Event.PlayerJoin -= OnPlayerJoin; } catch { /* ignore */ }
                try { if (bagTickId >= 0) sapi.Event.UnregisterGameTickListener(bagTickId); } catch { /* ignore */ }
            }

            registered = false;
            listener = null;
            bagTickId = -1;
            onCrafted = null;
            BagBefore.Clear();
            sapi = null;
        }

        private static void OnEventBus(string eventName, ref EnumHandling handling, IAttribute data)
        {
            handling = EnumHandling.PassThrough;
            try
            {
                if (onCrafted == null || sapi == null || data is not TreeAttribute tree)
                    return;

                ItemStack? stack = tree.GetItemstack("itemstack");
                stack?.ResolveBlockOrItem(sapi.World);
                if (stack?.Collectible?.Code == null)
                    return;

                string code = stack.Collectible.Code.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(code))
                    return;

                IPlayer? player = null;
                long entityId = tree.GetLong("byentityid", 0);
                if (entityId != 0 && sapi.World.GetEntityById(entityId) is EntityPlayer ep)
                    player = ep.Player;
                if (player == null)
                {
                    string? uid = tree.GetString("playeruid");
                    if (!string.IsNullOrEmpty(uid))
                        player = sapi.World.PlayerByUid(uid);
                }
                if (player == null)
                    return;

                // Audit line: "Moved {stack.StackSize}xItem to ({intoBags}x into hotbar)"
                // Credit intoBags (items that appeared in bags during this craft put).
                int before = GetBefore(player.PlayerUID, code);
                int after = CountBags(player, code);
                int intoBags = System.Math.Max(0, after - before);
                int qty = System.Math.Max(System.Math.Max(1, stack.StackSize), intoBags);

                onCrafted(player, code, qty);
                StoreBags(player);

                sapi.Logger.Notification(
                    "[SwixyQuestBook] craft {0} +{1} (intoBags={2}) {3}",
                    code, qty, intoBags, player.PlayerName);
            }
            catch (Exception ex)
            {
                sapi?.Logger.Warning("[SwixyQuestBook] onitemcrafted error: {0}", ex.Message);
            }
        }

        private static int GetBefore(string uid, string code)
        {
            if (BagBefore.TryGetValue(uid, out var map) && map.TryGetValue(code, out int n))
                return n;
            return 0;
        }

        private static void StoreBags(IPlayer? player)
        {
            if (player == null || string.IsNullOrEmpty(player.PlayerUID))
                return;

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Add(map, player.InventoryManager?.GetOwnInventory(GlobalConstants.hotBarInvClassName));
            Add(map, player.InventoryManager?.GetOwnInventory(GlobalConstants.backpackInvClassName));
            Add(map, player.InventoryManager?.GetOwnInventory(GlobalConstants.mousecursorInvClassName));
            BagBefore[player.PlayerUID] = map;
        }

        private static void Add(Dictionary<string, int> map, IInventory? inv)
        {
            if (inv == null) return;
            foreach (ItemSlot slot in inv)
            {
                ItemStack? st = slot?.Itemstack;
                if (st?.Collectible?.Code == null || st.StackSize <= 0) continue;
                string c = st.Collectible.Code.ToString() ?? "";
                if (c.Length == 0) continue;
                map[c] = map.TryGetValue(c, out int n) ? n + st.StackSize : st.StackSize;
            }
        }

        private static int CountBags(IPlayer player, string code)
        {
            int t = 0;
            t += CountInv(player.InventoryManager?.GetOwnInventory(GlobalConstants.hotBarInvClassName), code);
            t += CountInv(player.InventoryManager?.GetOwnInventory(GlobalConstants.backpackInvClassName), code);
            t += CountInv(player.InventoryManager?.GetOwnInventory(GlobalConstants.mousecursorInvClassName), code);
            return t;
        }

        private static int CountInv(IInventory? inv, string code)
        {
            if (inv == null) return 0;
            int t = 0;
            foreach (ItemSlot slot in inv)
            {
                ItemStack? st = slot?.Itemstack;
                if (st?.Collectible?.Code == null || st.StackSize <= 0) continue;
                if (string.Equals(st.Collectible.Code.ToString(), code, StringComparison.OrdinalIgnoreCase))
                    t += st.StackSize;
            }
            return t;
        }
    }
}
