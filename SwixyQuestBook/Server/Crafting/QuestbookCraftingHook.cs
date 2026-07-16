using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace SwixyQuestBook.Server.Crafting
{
    /// <summary>
    /// Craft detection without Harmony and without injecting collectible behaviors.
    /// Vanilla crafting output calls <c>Event.PushEvent("onitemcrafted", …)</c>;
    /// we listen via <see cref="IEventAPI.RegisterEventBusListener"/>.
    /// </summary>
    internal static class QuestbookCraftingHook
    {
        private static Action<IPlayer, string, int>? onCrafted;
        private static ICoreServerAPI? sapi;
        private static EventBusListenerDelegate? listener;
        private static bool registered;

        public static void Apply(ICoreServerAPI api, Action<IPlayer, string, int> craftedHandler)
        {
            sapi = api;
            onCrafted = craftedHandler;

            if (registered)
                return;

            listener = OnEventBus;
            // filterByEventName: only "onitemcrafted" (see ItemSlotCraftingOutput.triggerEvent)
            api.Event.RegisterEventBusListener(listener, filterByEventName: "onitemcrafted");
            registered = true;
            api.Logger.Notification(
                "[SwixyQuestBook] Craft detect listening on event bus 'onitemcrafted' (no Harmony, no behavior inject)");
        }

        public static void Unapply()
        {
            if (sapi != null && listener != null && registered)
            {
                try
                {
                    sapi.Event.UnregisterEventBusListener(listener);
                }
                catch
                {
                    // ignore
                }
            }

            registered = false;
            listener = null;
            onCrafted = null;
            sapi = null;
        }

        private static void OnEventBus(string eventName, ref EnumHandling handling, IAttribute data)
        {
            handling = EnumHandling.PassThrough;

            try
            {
                if (onCrafted == null || sapi == null)
                    return;

                if (!string.Equals(eventName, "onitemcrafted", StringComparison.OrdinalIgnoreCase))
                    return;

                if (data is not TreeAttribute tree)
                    return;

                ItemStack? stack = tree.GetItemstack("itemstack");
                // Ensure stack is resolved against world (network/event payloads sometimes need it).
                stack?.ResolveBlockOrItem(sapi.World);

                if (stack?.Collectible?.Code == null)
                    return;

                string code = stack.Collectible.Code.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code))
                    return;

                int qty = Math.Max(1, tree.GetInt("quantity", stack.StackSize));
                if (qty <= 0)
                    qty = Math.Max(1, stack.StackSize);

                IPlayer? player = null;
                long entityId = tree.GetLong("byentityid", 0);
                if (entityId != 0)
                {
                    Entity? entity = sapi.World.GetEntityById(entityId);
                    if (entity is EntityPlayer ep)
                        player = ep.Player;
                }

                // Fallback: attribute might store player differently on some versions.
                if (player == null)
                {
                    string? uid = tree.GetString("playeruid");
                    if (!string.IsNullOrEmpty(uid))
                        player = sapi.World.PlayerByUid(uid);
                }

                if (player == null)
                {
                    sapi.Logger.VerboseDebug(
                        "[SwixyQuestBook] onitemcrafted without player (entityId={0}): {1}",
                        entityId, code);
                    return;
                }

                sapi.Logger.VerboseDebug(
                    "[SwixyQuestBook] onitemcrafted {0} x{1} by {2}",
                    code, qty, player.PlayerName);
                onCrafted(player, code, qty);
            }
            catch (Exception ex)
            {
                sapi?.Logger.Warning("[SwixyQuestBook] onitemcrafted handler error: {0}", ex.Message);
            }
        }
    }
}
