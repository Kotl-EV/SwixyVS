// =============================================================================
// ClaimUseFilterTileGridElement.cs
// -----------------------------------------------------------------------------
// Virtualized creative-style tile grid for Use-filter (selected + catalog).
// Only visible tiles are laid out / rendered — full creative catalogs stay smooth.
// =============================================================================

using System;
using System.Collections.Generic;
using Cairo;
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
    public const double UnscaledTile = 42;
    public const double UnscaledGap = 4;

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
    private string emptyHintCached = "";
    private int emptyHintW;
    private int emptyHintH;

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
        // Panel chrome is drawn every frame in RenderInteractiveElements so it stays
        // visible even if the static composer layer is covered/reordered.
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
            // Bright green so selected tiles read clearly on the brown panel.
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

        // No full-area plate under tiles — only individual tile cells + empty hint.

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

            var slot = entries[i].Slot;
            if (slot.Itemstack?.Collectible != null)
            {
                var centerX = cellX + tile / 2;
                var centerY = cellY + tile / 2;
                var renderSize = (float)(tile * (GuiElementPassiveItemSlot.unscaledItemSize
                    / GuiElementPassiveItemSlot.unscaledSlotSize) * 0.9);

                var tileBounds = ElementBounds.Fixed(0, 0, UnscaledTile, UnscaledTile);
                tileBounds.ParentBounds = api.Gui.WindowBounds;
                tileBounds.CalcWorldBounds();
                tileBounds.absFixedX = cellX;
                tileBounds.absFixedY = cellY;
                tileBounds.absInnerWidth = tile;
                tileBounds.absInnerHeight = tile;

                api.Render.PushScissor(tileBounds, true);
                api.Render.RenderItemstackToGui(
                    slot,
                    centerX,
                    centerY,
                    100,
                    renderSize,
                    ColorUtil.WhiteArgb);
                api.Render.PopScissor();
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
                }
            }
        }

        api.Render.PopScissor();
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

    /// <summary>Thumb geometry in unscaled design coords relative to this element's fixedY.</summary>
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
    }
}
