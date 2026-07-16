using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SwixyQuestBook.Util.Items
{
    public enum QuestbookItemIconContext
    {
        QuestNode,
        Modal,
        Sidebar,
    }

    public sealed class QuestbookItemIconHelper
    {
        public const double LargeItemShrinkStart = 1.12;

        private static readonly IconScaleProfile QuestNodeProfile = new(0.78, 0.48, 0.88, 0.10);
        private static readonly IconScaleProfile ModalProfile = new(0.56, 0.32, 0.68, 0.08);
        private static readonly IconScaleProfile SidebarProfile = new(0.72, 0.44, 0.84, 0.10);

        private static readonly string[] ReferenceItemCodes =
        [
            "game:ingot-copper",
            "game:stone",
            "game:stick",
        ];

        private readonly ICoreClientAPI api;
        private readonly Dictionary<string, double> fitRatioCache = new(StringComparer.OrdinalIgnoreCase);
        private double? referenceGuiExtent;

        public QuestbookItemIconHelper(ICoreClientAPI api)
        {
            this.api = api;
        }

        public void ClearCache()
        {
            fitRatioCache.Clear();
            referenceGuiExtent = null;
        }

        public double GetFitRatio(ItemSlot? slot, QuestbookItemIconContext context, string? collectibleCode = null)
        {
            IconScaleProfile profile = GetProfile(context);
            string cacheKey = BuildCacheKey(context, slot, collectibleCode);
            if (cacheKey.Length == 0)
            {
                return profile.DefaultFitRatio;
            }

            if (fitRatioCache.TryGetValue(cacheKey, out double cachedRatio))
            {
                return cachedRatio;
            }

            double fitRatio = slot?.Itemstack != null
                ? ComputeFitRatio(slot, profile)
                : profile.DefaultFitRatio;

            fitRatioCache[cacheKey] = fitRatio;
            return fitRatio;
        }

        private static IconScaleProfile GetProfile(QuestbookItemIconContext context)
        {
            return context switch
            {
                QuestbookItemIconContext.QuestNode => QuestNodeProfile,
                QuestbookItemIconContext.Sidebar => SidebarProfile,
                _ => ModalProfile
            };
        }

        private double ComputeFitRatio(ItemSlot slot, IconScaleProfile profile)
        {
            ItemStack? stack = slot.Itemstack;
            if (stack?.Collectible == null)
            {
                return profile.DefaultFitRatio;
            }

            if (!TryMeasureGuiExtent(stack, slot, out double itemExtent))
            {
                return profile.DefaultFitRatio;
            }

            double referenceExtent = GetReferenceGuiExtent();
            if (referenceExtent <= 0.001)
            {
                return profile.DefaultFitRatio;
            }

            double relativeExtent = itemExtent / referenceExtent;
            double fitRatio = profile.DefaultFitRatio;

            if (relativeExtent > LargeItemShrinkStart)
            {
                fitRatio = profile.DefaultFitRatio / relativeExtent;
            }
            else if (relativeExtent < 0.92)
            {
                double boost = (0.92 - relativeExtent) * profile.SmallItemBoost;
                fitRatio = profile.DefaultFitRatio + boost;
            }

            return GameMath.Clamp(fitRatio, profile.MinFitRatio, profile.MaxFitRatio);
        }

        private double GetReferenceGuiExtent()
        {
            if (referenceGuiExtent.HasValue)
            {
                return referenceGuiExtent.Value;
            }

            foreach (string code in ReferenceItemCodes)
            {
                CollectibleObject? collectible = api.World.GetItem(new AssetLocation(code));
                collectible ??= api.World.GetBlock(new AssetLocation(code));
                if (collectible == null)
                {
                    continue;
                }

                ItemStack stack = new(collectible, 1);
                DummySlot referenceSlot = new(stack);
                if (TryMeasureGuiExtent(stack, referenceSlot, out double extent) && extent > 0.001)
                {
                    referenceGuiExtent = extent;
                    return extent;
                }
            }

            referenceGuiExtent = 1.0;
            return 1.0;
        }

        private bool TryMeasureGuiExtent(ItemStack stack, ItemSlot slot, out double extent)
        {
            extent = 0;

            if (!TryTesselate(stack, out MeshData? sourceMesh) || sourceMesh == null)
            {
                return false;
            }

            try
            {
                ItemRenderInfo renderInfo = api.Render.GetItemStackRenderInfo(slot, EnumItemRenderTarget.Gui, 0);
                ModelTransform transform = renderInfo.Transform ?? stack.Collectible.GuiTransform ?? ModelTransform.ItemDefaultGui();

                MeshData mesh = sourceMesh.Clone();
                mesh.ModelTransform(transform);
                extent = ComputeGuiViewExtent(mesh);
                return extent > 0.001;
            }
            catch
            {
                return false;
            }
        }

        private bool TryTesselate(ItemStack stack, out MeshData? mesh)
        {
            mesh = new MeshData();
            if (stack.Item != null)
            {
                api.Tesselator.TesselateItem(stack.Item, out mesh);
                return mesh.VerticesCount > 0;
            }

            if (stack.Block != null)
            {
                api.Tesselator.TesselateBlock(stack.Block, out mesh);
                return mesh.VerticesCount > 0;
            }

            mesh = null;
            return false;
        }

        private static double ComputeGuiViewExtent(MeshData mesh)
        {
            float[]? xyz = mesh.xyz;
            int vertexCount = mesh.VerticesCount;
            if (xyz == null || vertexCount <= 0)
            {
                return 1.0;
            }

            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;

            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                int offset = vertexIndex * 3;
                float x = xyz[offset];
                float y = xyz[offset + 1];

                minX = System.Math.Min(minX, x);
                maxX = System.Math.Max(maxX, x);
                minY = System.Math.Min(minY, y);
                maxY = System.Math.Max(maxY, y);
            }

            double extentX = maxX - minX;
            double extentY = maxY - minY;
            return System.Math.Max(0.01, System.Math.Max(extentX, extentY));
        }

        private static string BuildCacheKey(QuestbookItemIconContext context, ItemSlot? slot, string? collectibleCode)
        {
            string itemKey = ResolveItemKey(slot, collectibleCode);
            if (itemKey.Length == 0)
            {
                return string.Empty;
            }

            return $"{context}:{itemKey}";
        }

        private static string ResolveItemKey(ItemSlot? slot, string? collectibleCode)
        {
            if (!string.IsNullOrWhiteSpace(collectibleCode))
            {
                return collectibleCode.Trim();
            }

            return slot?.Itemstack?.Collectible?.Code?.ToString() ?? string.Empty;
        }

        private readonly record struct IconScaleProfile(
            double DefaultFitRatio,
            double MinFitRatio,
            double MaxFitRatio,
            double SmallItemBoost);
    }
}