using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixySkyBlock;

/// <summary>
/// Renders generator block icons using the same slot/scissor approach as EP handbook slideshows.
/// </summary>
internal sealed class IslandGeneratorSlideshowIconRenderer
{
    private const float RenderSizeRatio = 0.68f;
    private const int UnscaledIconSize = 32;
    private const long VariantSwitchIntervalMs = 1000;

    private readonly ICoreClientAPI api;
    private readonly DummyInventory dummyInventory;
    private readonly DummySlot renderSlot;

    public IslandGeneratorSlideshowIconRenderer(ICoreClientAPI api)
    {
        this.api = api;
        dummyInventory = new DummyInventory(api);
        dummyInventory.OnAcquireTransitionSpeed += static (_, _, _) => 0;
        renderSlot = new DummySlot(null, dummyInventory);
    }

    public void Render(
        ItemStack?[] variants,
        ref int currentIndex,
        ref long lastSwitchMs,
        double iconX,
        double iconY,
        float iconSize,
        float renderZ,
        bool mouseOver,
        int color = -1)
    {
        if (variants.Length == 0)
        {
            return;
        }

        if (!mouseOver
            && variants.Length > 1
            && api.World.ElapsedMilliseconds - lastSwitchMs > VariantSwitchIntervalMs)
        {
            currentIndex = (currentIndex + 1) % variants.Length;
            lastSwitchMs = api.World.ElapsedMilliseconds;
        }

        var stack = variants[Math.Clamp(currentIndex, 0, variants.Length - 1)];
        if (stack == null || stack.Id == 0 || stack.Collectible == null)
        {
            return;
        }

        renderSlot.Itemstack = stack.Clone();

        var renderSize = iconSize * RenderSizeRatio;
        var scissor = ElementBounds.FixedSize(UnscaledIconSize, UnscaledIconSize);
        scissor.ParentBounds = api.Gui.WindowBounds;
        scissor.CalcWorldBounds();
        scissor.absFixedX = iconX;
        scissor.absFixedY = iconY;
        scissor.absInnerWidth *= renderSize / 0.58f;
        scissor.absInnerHeight *= renderSize / 0.58f;

        api.Render.PushScissor(scissor, true);
#pragma warning disable CS0618
        api.Render.RenderItemstackToGui(
            renderSlot,
            iconX + iconSize / 2,
            iconY + iconSize / 2,
            renderZ,
            renderSize,
            color,
            shading: true,
            rotate: false,
            showStackSize: false);
#pragma warning restore CS0618
        api.Render.PopScissor();
    }
}