using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SwixyQuestBook.Gui
{
    /// <summary>
    /// Renders questbook item icons the same way as vanilla inventory slots
    /// (<see cref="GuiElementPassiveItemSlot"/> / <see cref="GuiElementItemSlotGridBase"/>).
    /// </summary>
    internal sealed class QuestbookGuiItemIconRenderer
    {
        private static readonly double UnscaledSlotSize = GuiElementPassiveItemSlot.unscaledSlotSize;
        private static readonly double UnscaledItemSize = GuiElementPassiveItemSlot.unscaledItemSize;
        private static readonly double ItemToSlotRatio = UnscaledItemSize / UnscaledSlotSize;
        private static readonly double ScissorInsetXRatio = 1.75 / UnscaledSlotSize;
        private static readonly double ScissorInsetYRatio = 1.5 / UnscaledSlotSize;
        private static readonly double ScissorSizeRatio = (UnscaledSlotSize - 4) / UnscaledSlotSize;

        private readonly ICoreClientAPI api;
        private readonly DummyInventory dummyInventory;
        private readonly DummySlot renderSlot;

        public QuestbookGuiItemIconRenderer(ICoreClientAPI api)
        {
            this.api = api;
            dummyInventory = new DummyInventory(api);
            dummyInventory.OnAcquireTransitionSpeed += static (_, _, _) => 0;
            renderSlot = new DummySlot(null, dummyInventory);
        }

        /// <param name="clipX">Optional screen-space clip rect (left). NaN = no external clip.</param>
        public void Render(
            ItemSlot slot,
            double iconX,
            double iconY,
            float iconSize,
            float renderZ,
            float deltaTime,
            int displayCount = 1,
            bool showStackSize = false,
            double clipX = double.NaN,
            double clipY = double.NaN,
            double clipWidth = double.NaN,
            double clipHeight = double.NaN)
        {
            if (slot.Itemstack?.Collectible == null)
            {
                return;
            }

            // Reuse stack reference when count already matches — avoids Clone() per icon per frame.
            ItemStack source = slot.Itemstack;
            int desiredCount = showStackSize ? System.Math.Max(1, displayCount) : 1;
            if (renderSlot.Itemstack == null
                || renderSlot.Itemstack.Collectible != source.Collectible
                || renderSlot.Itemstack.StackSize != desiredCount)
            {
                renderSlot.Itemstack = source.Clone();
                renderSlot.Itemstack.StackSize = desiredCount;
            }

            double centerX = iconX + (iconSize / 2);
            double centerY = iconY + (iconSize / 2);
            float renderSize = (float)(iconSize * ItemToSlotRatio);

            ElementBounds? scissor = BuildScissorBounds(api, iconX, iconY, iconSize, clipX, clipY, clipWidth, clipHeight);
            if (scissor == null)
            {
                return;
            }

            api.Render.PushScissor(scissor, true);
            api.Render.RenderItemstackToGui(
                renderSlot,
                centerX,
                centerY,
                renderZ,
                renderSize,
                ColorUtil.WhiteArgb,
                deltaTime);
            api.Render.PopScissor();
        }

        private static ElementBounds? BuildScissorBounds(
            ICoreClientAPI api,
            double iconX,
            double iconY,
            float iconSize,
            double clipX,
            double clipY,
            double clipWidth,
            double clipHeight)
        {
            double insetX = iconSize * ScissorInsetXRatio;
            double insetY = iconSize * ScissorInsetYRatio;
            double scissorSize = iconSize * ScissorSizeRatio;

            double left = iconX + insetX;
            double top = iconY + insetY;
            double right = left + scissorSize;
            double bottom = top + scissorSize;

            // Intersect with an external viewport (scroll lists / graph) so partially
            // visible rows do not let the 3D item mesh bleed outside the list.
            if (!double.IsNaN(clipX) && !double.IsNaN(clipY) && !double.IsNaN(clipWidth) && !double.IsNaN(clipHeight)
                && clipWidth > 0 && clipHeight > 0)
            {
                double clipRight = clipX + clipWidth;
                double clipBottom = clipY + clipHeight;
                left = System.Math.Max(left, clipX);
                top = System.Math.Max(top, clipY);
                right = System.Math.Min(right, clipRight);
                bottom = System.Math.Min(bottom, clipBottom);
            }

            double width = right - left;
            double height = bottom - top;
            if (width <= 0.5 || height <= 0.5)
            {
                return null;
            }

            ElementBounds scissor = ElementBounds.FixedSize(UnscaledSlotSize - 4, UnscaledSlotSize - 4);
            scissor.ParentBounds = api.Gui.WindowBounds;
            scissor.CalcWorldBounds();
            scissor.absFixedX = left;
            scissor.absFixedY = top;
            scissor.absInnerWidth = width;
            scissor.absInnerHeight = height;
            return scissor;
        }
    }
}
