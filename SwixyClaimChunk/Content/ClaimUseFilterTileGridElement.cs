// =============================================================================
// ClaimUseFilterTileGridElement.cs
// -----------------------------------------------------------------------------
// Virtualized creative-style tile grid for Use-filter.
// Icons rendered like vanilla inventory slots (GuiElementPassiveItemSlot).
// =============================================================================

using System;
using System.Collections.Generic;
using Cairo;
using SwixyClaimChunk.Core;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Scrollable tile grid of item stacks. Click toggles selection via <see cref="OnTileClick"/>.
/// </summary>
public sealed class ClaimUseFilterTileGridElement : GuiElement
{
    public const double UnscaledTile = 48; // same as GuiElementPassiveItemSlot.unscaledSlotSize
    public const double UnscaledGap = 4;

    private static readonly double UnscaledSlotSize = GuiElementPassiveItemSlot.unscaledSlotSize;
    private static readonly double UnscaledItemSize = GuiElementPassiveItemSlot.unscaledItemSize;
    private static readonly double ItemToSlotRatio = UnscaledItemSize / UnscaledSlotSize;

    public Action<string>? OnTileClick;
    public System.Func<string, bool>? IsSelected;
    public Action? OnScrollChanged;

    /// <summary>Shown centered when the grid has no entries.</summary>
    public string EmptyHint { get; set; } = "";

    private IReadOnlyList<(string Code, string Label, DummySlot Slot)> entries =
        Array.Empty<(string, string, DummySlot)>();

    private float scrollOffset;
    private LoadedTexture emptyTileTex;
    private LoadedTexture selectedTileTex;
    private LoadedTexture hoverTileTex;
    private LoadedTexture emptyHintTex;
    private LoadedTexture tooltipTex;
    private LoadedTexture groundStorageIconTex;
    private string emptyHintCached = "";
    private int emptyHintW;
    private int emptyHintH;
    private string tooltipCachedText = "";
    private string hoverTooltipLabel = "";
    private double hoverTileX;
    private double hoverTileY;
    private double hoverTileSize;

    /// <summary>
    /// Slot from DummyInventory — some blocks (EP machines etc.) call MarkDirty
    /// during GUI render and need a real inventory slot, not a free DummySlot.
    /// </summary>
    private readonly DummyInventory renderInventory;
    private readonly ItemSlot renderSlot;

    public override bool Focusable => true;

    public float ScrollOffset
    {
        get => scrollOffset;
        set
        {
            var max = GetMaxScroll();
            var next = Math.Clamp(value, 0f, max);
            if (Math.Abs(next - scrollOffset) < 0.01f)
            {
                return;
            }

            scrollOffset = next;
            OnScrollChanged?.Invoke();
        }
    }

    public float MaxScroll => GetMaxScroll();

    public int EntryCount => entries.Count;

    public ClaimUseFilterTileGridElement(ICoreClientAPI capi, ElementBounds bounds)
        : base(capi, bounds)
    {
        emptyTileTex = new LoadedTexture(capi);
        selectedTileTex = new LoadedTexture(capi);
        hoverTileTex = new LoadedTexture(capi);
        emptyHintTex = new LoadedTexture(capi);
        tooltipTex = new LoadedTexture(capi);
        groundStorageIconTex = new LoadedTexture(capi);

        renderInventory = new DummyInventory(capi, 1);
        renderInventory.OnAcquireTransitionSpeed += static (_, _, _) => 0f;
        renderSlot = renderInventory[0];
    }

    public void SetEntries(IReadOnlyList<(string Code, string Label, DummySlot Slot)>? list)
    {
        entries = list ?? Array.Empty<(string, string, DummySlot)>();
        scrollOffset = Math.Clamp(scrollOffset, 0f, GetMaxScroll());
    }

    public void ScrollBy(float delta)
    {
        ScrollOffset = scrollOffset + delta;
    }

    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        EnsureTileTextures();
    }

    private void EnsureTileTextures()
    {
        if (emptyTileTex.TextureId != 0)
        {
            return;
        }

        var size = Math.Max(8, (int)Math.Ceiling(scaled(UnscaledTile)));
        ComposeTileTexture(size, selected: false, ref emptyTileTex);
        ComposeTileTexture(size, selected: true, ref selectedTileTex);
        ComposeHoverTexture(size);
    }

    private void ComposeTileTexture(int size, bool selected, ref LoadedTexture texture)
    {
        using var surface = new ImageSurface(Format.Argb32, size, size);
        using var ctx = genContext(surface);
        RoundRectangle(ctx, 0, 0, size, size, 3);
        if (selected)
        {
            ctx.SetSourceRGBA(0.22, 0.48, 0.26, 0.98);
        }
        else
        {
            ctx.SetSourceRGBA(0.20, 0.15, 0.11, 0.96);
        }

        ctx.Fill();
        EmbossRoundRectangleElement(ctx, 0, 0, size, size, inverse: selected, depth: 2);

        ctx.SetSourceRGBA(
            selected ? 0.45 : 0x83 / 255.0,
            selected ? 0.85 : 0x66 / 255.0,
            selected ? 0.50 : 0x50 / 255.0,
            selected ? 0.9 : 0.55);
        RoundRectangle(ctx, 0.5, 0.5, size - 1, size - 1, 3);
        ctx.LineWidth = 1.25;
        ctx.Stroke();

        generateTexture(surface, ref texture);
    }

    private void ComposeHoverTexture(int size)
    {
        using var surface = new ImageSurface(Format.Argb32, size, size);
        using var ctx = genContext(surface);
        RoundRectangle(ctx, 0, 0, size, size, 3);
        ctx.SetSourceRGBA(1, 1, 1, 0.22);
        ctx.Fill();
        generateTexture(surface, ref hoverTileTex);
    }

    private void EnsureEmptyHintTexture()
    {
        var hint = EmptyHint ?? "";
        if (emptyHintTex.TextureId != 0 && string.Equals(hint, emptyHintCached, StringComparison.Ordinal))
        {
            return;
        }

        emptyHintCached = hint;
        if (string.IsNullOrWhiteSpace(hint))
        {
            emptyHintTex.Dispose();
            emptyHintTex = new LoadedTexture(api);
            emptyHintW = 0;
            emptyHintH = 0;
            return;
        }

        var font = ClaimFontHelper.Create(12, ClaimFontHelper.ColorCream, bold: true);
        var textW = Math.Max(40, (int)Math.Ceiling(Bounds.InnerWidth) - 16);
        if (textW < 40)
        {
            textW = 200;
        }

        var textUtil = new TextDrawUtil();
        var linesH = textUtil.GetMultilineTextHeight(font, hint, textW);
        emptyHintW = textW + 8;
        emptyHintH = Math.Max(18, (int)Math.Ceiling(linesH) + 8);

        using var surface = new ImageSurface(Format.Argb32, emptyHintW, emptyHintH);
        using var ctx = genContext(surface);
        font.SetupContext(ctx);
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            font,
            hint,
            4,
            4,
            textW,
            EnumTextOrientation.Center);
        generateTexture(surface, ref emptyHintTex);
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        EnsureTileTextures();
        Bounds.CalcWorldBounds();

        var rx = (float)Bounds.renderX;
        var ry = (float)Bounds.renderY;
        var rw = (float)Bounds.OuterWidth;
        var rh = (float)Bounds.OuterHeight;
        if (rw < 2 || rh < 2)
        {
            return;
        }

        if (entries.Count == 0)
        {
            EnsureEmptyHintTexture();
            if (emptyHintTex.TextureId != 0 && emptyHintW > 0)
            {
                var hx = rx + (rw - emptyHintW) * 0.5f;
                var hy = ry + (rh - emptyHintH) * 0.5f;
                api.Render.Render2DTexturePremultipliedAlpha(
                    emptyHintTex.TextureId,
                    hx,
                    hy,
                    emptyHintW,
                    emptyHintH);
            }

            return;
        }

        var tile = scaled(UnscaledTile);
        var gap = scaled(UnscaledGap);
        var step = tile + gap;
        var cols = Math.Max(1, (int)Math.Floor((Bounds.InnerWidth + gap) / step));
        var rows = Math.Max(1, (int)Math.Ceiling(entries.Count / (double)cols));
        var contentH = rows * tile + Math.Max(0, rows - 1) * gap;
        var maxScroll = Math.Max(0f, (float)(contentH - Bounds.InnerHeight));
        scrollOffset = Math.Clamp(scrollOffset, 0f, maxScroll);

        var firstRow = Math.Max(0, (int)Math.Floor(scrollOffset / step) - 1);
        var visibleRows = (int)Math.Ceiling(Bounds.InnerHeight / step) + 2;
        var firstIndex = firstRow * cols;
        var lastIndex = Math.Min(entries.Count, (firstRow + visibleRows) * cols);

        var mouseX = api.Input.MouseX;
        var mouseY = api.Input.MouseY;
        var insideViewport = IsPositionInside(mouseX, mouseY);
        hoverTooltipLabel = "";

        api.Render.PushScissor(Bounds, true);

        for (var i = firstIndex; i < lastIndex; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var cellX = Bounds.renderX + col * step + gap * 0.5;
            var cellY = Bounds.renderY + row * step - scrollOffset + gap * 0.5;

            if (cellY + tile < Bounds.renderY || cellY > Bounds.renderY + Bounds.InnerHeight)
            {
                continue;
            }

            var code = entries[i].Code;
            var selected = IsSelected?.Invoke(code) == true;
            var tex = selected ? selectedTileTex : emptyTileTex;

            api.Render.Render2DTexturePremultipliedAlpha(
                tex.TextureId,
                (float)cellX,
                (float)cellY,
                (float)tile,
                (float)tile);

            // groundstorage — невидимый shape; 3D-иконка пустая → Cairo.
            if (ClaimCodeUtil.NeedsCairoIcon(code)
                || entries[i].Slot?.Itemstack?.Collectible == null
                   && code.Contains("groundstorage", StringComparison.OrdinalIgnoreCase))
            {
                RenderGroundStorageIcon(cellX, cellY, tile);
            }
            else
            {
                RenderStackIcon(entries[i].Slot, cellX, cellY, tile, deltaTime);
            }

            if (insideViewport
                && mouseX >= cellX
                && mouseX < cellX + tile
                && mouseY >= cellY
                && mouseY < cellY + tile)
            {
                var visibleTop = Math.Max(cellY, Bounds.renderY);
                var visibleBottom = Math.Min(cellY + tile, Bounds.renderY + Bounds.InnerHeight);
                if (visibleBottom - visibleTop >= tile * 0.45)
                {
                    api.Render.Render2DTexturePremultipliedAlpha(
                        hoverTileTex.TextureId,
                        (float)cellX,
                        (float)cellY,
                        (float)tile,
                        (float)tile);

                    // Подсказка: имя + code (как QuestBook).
                    var label = entries[i].Label;
                    if (ClaimCodeUtil.IsUnknownLabel(label) || string.IsNullOrWhiteSpace(label))
                    {
                        label = ClaimCodeUtil.GetFriendlyBlockLabel(code, entries[i].Slot?.Itemstack);
                    }

                    if (!string.IsNullOrWhiteSpace(code)
                        && !string.Equals(label, code, StringComparison.OrdinalIgnoreCase)
                        && !label.Contains(code, StringComparison.OrdinalIgnoreCase))
                    {
                        label = label + "\n" + code;
                    }

                    hoverTooltipLabel = label;
                    hoverTileX = cellX;
                    hoverTileY = cellY;
                    hoverTileSize = tile;
                }
            }
        }

        api.Render.PopScissor();

        // Tooltip AFTER all tiles; Z above Itemstack (450).
        if (!string.IsNullOrWhiteSpace(hoverTooltipLabel))
        {
            RenderHoverTooltip(hoverTooltipLabel, hoverTileX, hoverTileY, hoverTileSize);
        }
    }

    private void RenderGroundStorageIcon(double cellX, double cellY, double tile)
    {
        EnsureGroundStorageIcon((int)Math.Ceiling(tile));
        if (groundStorageIconTex.TextureId <= 0)
        {
            return;
        }

        var pad = tile * 0.08;
        api.Render.Render2DTexturePremultipliedAlpha(
            groundStorageIconTex.TextureId,
            (float)(cellX + pad),
            (float)(cellY + pad),
            (float)(tile - pad * 2),
            (float)(tile - pad * 2),
            100f);
    }

    private void EnsureGroundStorageIcon(int size)
    {
        if (groundStorageIconTex.TextureId != 0)
        {
            return;
        }

        size = Math.Max(24, size);
        using var surface = new ImageSurface(Format.Argb32, size, size);
        using var ctx = genContext(surface);
        ClaimCairoIcons.DrawGroundStorage(ctx, 0, 0, size);
        generateTexture(surface, ref groundStorageIconTex);
    }

    /// <summary>Hover name like QuestBook admin picker (TextBackground fill).</summary>
    private void RenderHoverTooltip(string text, double tileX, double tileY, double tileSize)
    {
        try
        {
            if (!string.Equals(text, tooltipCachedText, StringComparison.Ordinal))
            {
                var font = CairoFont.WhiteSmallText();
                var background = new TextBackground
                {
                    HorPadding = 10,
                    VerPadding = 4,
                    Radius = 4,
                    BorderWidth = 1.5,
                    FillColor = [0.07, 0.08, 0.10, 0.96],
                    BorderColor = [0.55, 0.48, 0.35, 1.0],
                    Shade = true
                };
                var util = new TextTextureUtil(api);
                util.GenOrUpdateTextTexture(text, font, ref tooltipTex, background);
                tooltipCachedText = text;
            }

            if (tooltipTex.TextureId <= 0)
            {
                return;
            }

            var boxW = tooltipTex.Width;
            var boxH = tooltipTex.Height;
            var boxX = tileX + (tileSize - boxW) * 0.5;
            var boxY = tileY - boxH - 6;
            if (boxY < 4)
            {
                boxY = tileY + tileSize + 6;
            }

            if (boxX < 4)
            {
                boxX = 4;
            }

            if (boxX + boxW > api.Render.FrameWidth - 4)
            {
                boxX = api.Render.FrameWidth - boxW - 4;
            }

            if (boxY + boxH > api.Render.FrameHeight - 4)
            {
                boxY = api.Render.FrameHeight - boxH - 4;
            }

            // Itemstack uses posZ≈450 — tooltip must be higher or it sits under icons.
            api.Render.Render2DTexturePremultipliedAlpha(
                tooltipTex.TextureId,
                (float)boxX,
                (float)boxY,
                boxW,
                boxH,
                800f);
        }
        catch
        {
            // tooltip is non-critical
        }
    }

    /// <summary>
    /// Same path as <see cref="GuiElementPassiveItemSlot"/>:
    /// center of slot + scaled(unscaledItemSize) size, scissor = full slot.
    /// </summary>
    private void RenderStackIcon(DummySlot sourceSlot, double cellX, double cellY, double tile, float deltaTime)
    {
        var source = sourceSlot?.Itemstack;
        if (source?.Collectible == null)
        {
            return;
        }

        try
        {
            if (renderSlot.Itemstack == null
                || renderSlot.Itemstack.Collectible != source.Collectible
                || !ItemStackAttributesEqual(renderSlot.Itemstack, source))
            {
                renderSlot.Itemstack = source.Clone();
                if (renderSlot.Itemstack != null)
                {
                    renderSlot.Itemstack.StackSize = 1;
                }
            }
        }
        catch
        {
            return;
        }

        if (renderSlot.Itemstack?.Collectible == null)
        {
            return;
        }

        // Vanilla slot: center = slot origin + scaled(slotSize)/2, size = scaled(itemSize).
        // For a non-48 tile, scale item size proportionally to keep inventory-like centering.
        var scale = tile / scaled(UnscaledSlotSize);
        var centerX = cellX + tile / 2.0;
        var centerY = cellY + tile / 2.0;
        var renderSize = (float)(scaled(UnscaledItemSize) * scale);

        // Scissor = full tile (like inventory slot Bounds), not a tighter box —
        // tight scissor clips EP multi-block meshes asymmetrically → "shifted" look.
        var scissor = ElementBounds.Fixed(0, 0, UnscaledTile, UnscaledTile);
        scissor.ParentBounds = api.Gui.WindowBounds;
        scissor.CalcWorldBounds();
        scissor.absFixedX = cellX;
        scissor.absFixedY = cellY;
        scissor.absInnerWidth = tile;
        scissor.absInnerHeight = tile;

        api.Render.PushScissor(scissor, true);
        try
        {
            // color -1 like PassiveItemSlot; dt for proper transform.
            api.Render.RenderItemstackToGui(
                renderSlot,
                centerX,
                centerY,
                450,
                renderSize,
                -1,
                deltaTime);
        }
        catch
        {
            // bad collectible mesh — skip frame
        }
        finally
        {
            api.Render.PopScissor();
        }
    }

    /// <summary>Creative stacks often differ by attributes (EP / lanterns).</summary>
    private static bool ItemStackAttributesEqual(ItemStack a, ItemStack b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a.Collectible != b.Collectible)
        {
            return false;
        }

        try
        {
            var aa = a.Attributes;
            var ba = b.Attributes;
            if (aa == null && ba == null)
            {
                return true;
            }

            if (aa == null || ba == null)
            {
                return false;
            }

            return aa.Equals(ba);
        }
        catch
        {
            return false;
        }
    }

    public override void OnMouseDownOnElement(ICoreClientAPI capi, MouseEvent args)
    {
        if (args.Button != EnumMouseButton.Left || entries.Count == 0)
        {
            return;
        }

        if (!IsPositionInside(capi.Input.MouseX, capi.Input.MouseY))
        {
            return;
        }

        var code = HitTestCode(capi.Input.MouseX, capi.Input.MouseY);
        if (code == null)
        {
            return;
        }

        capi.Gui.PlaySound("toggleswitch");
        OnTileClick?.Invoke(code);
        args.Handled = true;
    }

    public override void OnMouseWheel(ICoreClientAPI capi, MouseWheelEventArgs args)
    {
        if (!IsPositionInside(capi.Input.MouseX, capi.Input.MouseY))
        {
            return;
        }

        if (GetMaxScroll() <= 0f)
        {
            return;
        }

        ScrollBy(-args.delta * 36f);
        args.SetHandled(true);
    }

    private string? HitTestCode(int mouseX, int mouseY)
    {
        var tile = scaled(UnscaledTile);
        var gap = scaled(UnscaledGap);
        var step = tile + gap;
        var cols = Math.Max(1, (int)Math.Floor((Bounds.InnerWidth + gap) / step));
        var localX = mouseX - Bounds.absX - gap * 0.5;
        var localY = mouseY - Bounds.absY + scrollOffset - gap * 0.5;

        if (localX < 0 || localY < 0)
        {
            return null;
        }

        var col = (int)(localX / step);
        var row = (int)(localY / step);
        if (col < 0 || col >= cols)
        {
            return null;
        }

        var inTileX = localX - col * step;
        var inTileY = localY - row * step;
        if (inTileX > tile || inTileY > tile)
        {
            return null;
        }

        var index = row * cols + col;
        if (index < 0 || index >= entries.Count)
        {
            return null;
        }

        return entries[index].Code;
    }

    private float GetMaxScroll()
    {
        if (entries.Count == 0)
        {
            return 0f;
        }

        Bounds.CalcWorldBounds();
        var tile = scaled(UnscaledTile);
        var gap = scaled(UnscaledGap);
        var step = tile + gap;
        var cols = Math.Max(1, (int)Math.Floor((Bounds.InnerWidth + gap) / step));
        var rows = Math.Max(1, (int)Math.Ceiling(entries.Count / (double)cols));
        var contentH = rows * tile + Math.Max(0, rows - 1) * gap;
        return Math.Max(0f, (float)(contentH - Bounds.InnerHeight));
    }

    public void GetScrollThumbDesign(double trackDesignY, double trackDesignH, out double thumbY, out double thumbH)
    {
        var max = GetMaxScroll();
        if (max <= 0.01f)
        {
            thumbY = trackDesignY;
            thumbH = trackDesignH;
            return;
        }

        var ratio = Bounds.InnerHeight / (Bounds.InnerHeight + max);
        thumbH = Math.Max(24, trackDesignH * ratio);
        thumbY = trackDesignY + (scrollOffset / max) * (trackDesignH - thumbH);
    }

    public override void Dispose()
    {
        base.Dispose();
        emptyTileTex.Dispose();
        selectedTileTex.Dispose();
        hoverTileTex.Dispose();
        emptyHintTex.Dispose();
        tooltipTex.Dispose();
        groundStorageIconTex.Dispose();
        try
        {
            renderSlot.Itemstack = null;
        }
        catch
        {
            // ignore
        }
    }
}
