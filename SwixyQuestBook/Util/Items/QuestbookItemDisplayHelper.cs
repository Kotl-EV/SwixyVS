using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixyQuestBook.Util.Items
{
    /// <summary>
    /// Resolves collectible codes to stacks for questbook icons.
    /// </summary>
    public static class QuestbookItemDisplayHelper
    {
        public static string ResolveIconCode(string collectibleCode)
        {
            if (string.IsNullOrWhiteSpace(collectibleCode))
            {
                return string.Empty;
            }

            return collectibleCode;
        }

        public static ItemStack? CreateDisplayStack(ICoreClientAPI api, string collectibleCode)
        {
            if (string.IsNullOrWhiteSpace(collectibleCode) || collectibleCode.Contains('*'))
            {
                return null;
            }

            return CreateStack(api, collectibleCode);
        }

        public static bool ShouldAttemptGuiRender(ICoreClientAPI api, ItemStack stack)
        {
            if (stack?.Collectible == null)
            {
                return false;
            }

            if (IsRenderable(api, stack))
            {
                return true;
            }

            try
            {
                ItemRenderInfo renderInfo = api.Render.GetItemStackRenderInfo(
                    new DummySlot(stack),
                    EnumItemRenderTarget.Gui,
                    0);
                return renderInfo.ModelRef != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsRenderable(ICoreClientAPI api, ItemStack stack)
        {
            if (stack?.Collectible == null)
            {
                return false;
            }

            try
            {
                if (stack.Item != null)
                {
                    api.Tesselator.TesselateItem(stack.Item, out MeshData? mesh);
                    return mesh != null && mesh.VerticesCount > 0;
                }

                if (stack.Block != null)
                {
                    api.Tesselator.TesselateBlock(stack.Block, out MeshData? mesh);
                    return mesh != null && mesh.VerticesCount > 0;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static CollectibleObject? ResolveCollectible(ICoreClientAPI api, string collectibleCode)
        {
            if (string.IsNullOrWhiteSpace(collectibleCode) || collectibleCode.Contains('*'))
            {
                return null;
            }

            CollectibleObject? collectible = api.World.GetItem(new AssetLocation(collectibleCode));
            collectible ??= api.World.GetBlock(new AssetLocation(collectibleCode));
            if (collectible != null)
            {
                return collectible;
            }

            string searchCode = GetPath(collectibleCode);
            collectible = api.World.Items
                .FirstOrDefault(item => item?.Code != null && item.Code.Path.Equals(searchCode, StringComparison.OrdinalIgnoreCase));
            collectible ??= api.World.Blocks
                .FirstOrDefault(block => block?.Code != null && block.Code.Path.Equals(searchCode, StringComparison.OrdinalIgnoreCase));

            // Kill goals store entity type codes (game:drifter-normal). Icons use ItemCreature
            // stacks (game:creature-drifter-normal) — same path as creative creatures tab.
            if (collectible == null)
            {
                collectible = ResolveCreatureItemForEntityCode(api, collectibleCode);
            }

            return collectible;
        }

        /// <summary>
        /// Maps an entity type code to its ItemCreature collectible for GUI rendering.
        /// e.g. game:drifter-normal → game:creature-drifter-normal
        /// </summary>
        public static CollectibleObject? ResolveCreatureItemForEntityCode(ICoreClientAPI api, string entityCode)
        {
            if (string.IsNullOrWhiteSpace(entityCode) || entityCode.Contains('*'))
                return null;

            AssetLocation loc = new(entityCode);
            string path = loc.Path ?? string.Empty;
            if (string.IsNullOrEmpty(path))
                return null;

            // Already a creature item path.
            if (path.StartsWith("creature-", StringComparison.OrdinalIgnoreCase))
            {
                return api.World.GetItem(loc);
            }

            string domain = string.IsNullOrEmpty(loc.Domain) ? "game" : loc.Domain;
            string creatureCode = $"{domain}:creature-{path}";
            Item? creatureItem = api.World.GetItem(new AssetLocation(creatureCode));
            if (creatureItem != null)
                return creatureItem;

            // Fallback: scan ItemCreature paths that strip to this entity path.
            foreach (Item item in api.World.Items)
            {
                if (item?.Code == null || item.Id == 0)
                    continue;

                string itemPath = item.Code.Path ?? string.Empty;
                if (!itemPath.StartsWith("creature-", StringComparison.OrdinalIgnoreCase))
                    continue;

                string stripped = itemPath["creature-".Length..];
                if (!stripped.Equals(path, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(loc.Domain)
                    && !string.Equals(item.Code.Domain, loc.Domain, StringComparison.OrdinalIgnoreCase))
                    continue;

                return item;
            }

            return null;
        }

        /// <summary>
        /// Icon lookup code for goals: entity codes become creature-* item codes when needed.
        /// </summary>
        public static string ResolveDisplayIconCode(ICoreClientAPI api, string code, bool isKillEntityCode)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            if (!isKillEntityCode)
                return code;

            // Preserve wildcards: game:drifter-* → game:creature-drifter-*
            if (code.Contains('*'))
            {
                AssetLocation loc = new(code);
                string path = loc.Path ?? code;
                if (path.StartsWith("creature-", StringComparison.OrdinalIgnoreCase))
                    return code;
                string domain = string.IsNullOrEmpty(loc.Domain) ? "game" : loc.Domain;
                return $"{domain}:creature-{path}";
            }

            CollectibleObject? creature = ResolveCreatureItemForEntityCode(api, code);
            return creature?.Code?.ToString() ?? code;
        }

        private static ItemStack? CreateStack(ICoreClientAPI api, string collectibleCode)
        {
            CollectibleObject? collectible = ResolveCollectible(api, collectibleCode);
            return collectible != null ? new ItemStack(collectible, 1) : null;
        }

        private static string GetPath(string collectibleCode)
        {
            int colonIndex = collectibleCode.IndexOf(':');
            return colonIndex >= 0 ? collectibleCode[(colonIndex + 1)..] : collectibleCode;
        }
    }
}