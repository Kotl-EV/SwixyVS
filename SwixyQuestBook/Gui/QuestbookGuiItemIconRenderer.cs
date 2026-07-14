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

        public void Render(
            ItemSlot slot,
            double iconX,
            double iconY,
            float iconSize,
            float renderZ,
            float deltaTime,
            int displayCount = 1,
            bool showStackSize = false)
        {
            if (slot.Itemstack?.Collectible == null)
            {
                return;
            }

            renderSlot.Itemstack = slot.Itemstack.Clone();

            int originalCount = renderSlot.Itemstack.StackSize;
            renderSlot.Itemstack.StackSize = showStackSize ? displayCount : 1;

            double centerX = iconX + (iconSize / 2);
            double centerY = iconY + (iconSize / 2);
            float renderSize = (float)(iconSize * ItemToSlotRatio);

            ElementBounds scissor = BuildScissorBounds(api, iconX, iconY, iconSize);
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

            renderSlot.Itemstack.StackSize = originalCount;
        }

        private static ElementBounds BuildScissorBounds(ICoreClientAPI api, double iconX, double iconY, float iconSize)
        {
            double insetX = iconSize * ScissorInsetXRatio;
            double insetY = iconSize * ScissorInsetYRatio;
            double scissorSize = iconSize * ScissorSizeRatio;

            ElementBounds scissor = ElementBounds.FixedSize(UnscaledSlotSize - 4, UnscaledSlotSize - 4);
            scissor.ParentBounds = api.Gui.WindowBounds;
            scissor.CalcWorldBounds();
            scissor.absFixedX = iconX + insetX;
            scissor.absFixedY = iconY + insetY;
            scissor.absInnerWidth = scissorSize;
            scissor.absInnerHeight = scissorSize;
            return scissor;
        }
    }
}