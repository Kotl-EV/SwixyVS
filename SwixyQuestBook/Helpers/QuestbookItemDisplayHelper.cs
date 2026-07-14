using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixyQuestBook.Helpers
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

            return collectible;
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