// =============================================================================
// ClaimHighlightListCell.cs
// -----------------------------------------------------------------------------
// Ячейка списка приватов — layout Group 1349.svg:
//   outer 302×76, name face 256×70, icon wells 32×32 stacked (highlight / delete).
// =============================================================================

using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Строка списка приватов: name plate + две 32×32 кнопки (лампочка / корзина).
/// </summary>
public sealed class ClaimHighlightListCell : GuiElementTextBase, IGuiElementCell
{
    /// <summary>Group 1349 outer row size.</summary>
    private const int RowW = 302;
    private const int RowH = 76;

    /// <summary>Name face relative to outer (73−69, 217−215).</summary>
    private const int NameX = 4;
    private const int NameY = 2;
    private const int NameW = 256;
    private const int NameH = 70;

    /// <summary>Icon wells relative to outer (335−69).</summary>
    private const int IconX = 266;
    private const int IconTopY = 2;
    private const int IconBotY = 40;
    private const int IconSize = 32;

    private const int UnscaledIconDraw = 20;
    private const int UnscaledTrashDraw = 16;
    private static readonly int UnscaledDepth = 2;

    private static readonly double[] ColFace = [0x41 / 255.0, 0x2D / 255.0, 0x1D / 255.0];
    private static readonly double[] ColHi = [0x56 / 255.0, 0x3E / 255.0, 0x2B / 255.0];
    private static readonly double[] ColLo = [0x2A / 255.0, 0x1E / 255.0, 0x14 / 255.0];
    private static readonly double[] ColEdge = [0x12 / 255.0, 0x12 / 255.0, 0x12 / 255.0];
    private static readonly double[] ColSelected = [0.32, 0.22, 0.15];

    public SavegameCellEntry cellEntry;
    public bool HighlightActive;
    public bool AllowDelete = true;
    public Action<int>? OnMouseDownOnCellLeft;
    public Action<int>? OnMouseDownOnCellRight;
    public Action<int>? OnMouseDownOnCellDelete;
    public double? FixedHeight { get; set; }

    private LoadedTexture releasedButtonTexture;
    private LoadedTexture pressedButtonTexture;
    private LoadedTexture leftHighlightTexture;
    private LoadedTexture rightTopHighlightTexture;
    private LoadedTexture rightBottomHighlightTexture;
    private double titleTextHeight;
    private double pressedYOffset;

    ElementBounds IGuiElementCell.Bounds => Bounds;

    public ClaimHighlightListCell(ICoreClientAPI capi, SavegameCellEntry cell, ElementBounds bounds, bool highlightActive)
        : base(capi, "", null, bounds)
    {
        cellEntry = cell;
        HighlightActive = highlightActive;
        leftHighlightTexture = new LoadedTexture(capi);
        rightTopHighlightTexture = new LoadedTexture(capi);
        rightBottomHighlightTexture = new LoadedTexture(capi);
        releasedButtonTexture = new LoadedTexture(capi);
        pressedButtonTexture = new LoadedTexture(capi);

        // Same typefaces as Questbook / claim chrome (not vanilla default UI fonts).
        cell.TitleFont ??= ClaimFontHelper.Create(15, ClaimFontHelper.ColorCream);
        cell.DetailTextFont ??= ClaimFontHelper.Create(13, ClaimFontHelper.ColorAccent);
        cell.TitleFont.LineHeightMultiplier = 0.95;
        cell.DetailTextFont.LineHeightMultiplier = 0.9;
    }

    public void Compose()
    {
        Bounds.CalcWorldBounds();
        if (Bounds.OuterWidthInt <= 0 || Bounds.OuterHeightInt <= 0)
        {
            return;
        }

        using (var surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt))
        using (var ctx = genContext(surface))
        {
            ComposeButton(ctx, false);
            generateTexture(surface, ref releasedButtonTexture);

            ctx.Operator = Operator.Clear;
            ctx.Paint();
            ctx.Operator = Operator.Over;

            ComposeButton(ctx, true);
            generateTexture(surface, ref pressedButtonTexture);
        }

        ComposeHover(HoverRegion.Left, ref leftHighlightTexture);
        ComposeHover(HoverRegion.RightTop, ref rightTopHighlightTexture);
        ComposeHover(HoverRegion.RightBottom, ref rightBottomHighlightTexture);
    }

    private enum HoverRegion
    {
        Left,
        RightTop,
        RightBottom
    }

    private void ComposeButton(Context ctx, bool pressed)
    {
        var drawH = Bounds.OuterHeight;
        var drawW = Bounds.OuterWidth;
        var sx = drawW / RowW;
        var sy = drawH / RowH;
        pressedYOffset = 0;

        if (cellEntry.DrawAsButton)
        {
            // Outer #121212 chrome + name face + two icon wells (Group 1349).
            DrawOuterAndFaces(ctx, drawW, drawH, pressed || cellEntry.Selected);
            if (pressed)
            {
                pressedYOffset = scaled(UnscaledDepth) / 2;
            }
        }

        // Text in name face: pad X, vertically center title+detail block in the face.
        var textLeft = NameX * sx + scaled(10);
        var textW = NameW * sx - scaled(20);
        var nameFaceTop = NameY * sy + pressedYOffset;
        var nameFaceH = NameH * sy;
        var detailGap = scaled(System.Math.Max(2, cellEntry.DetailTextOffY));

        Font = cellEntry.TitleFont;
        titleTextHeight = textUtil.GetMultilineTextHeight(Font, cellEntry.Title ?? "", textW);
        Font = cellEntry.DetailTextFont;
        var detailH = string.IsNullOrEmpty(cellEntry.DetailText)
            ? 0
            : textUtil.GetMultilineTextHeight(Font, cellEntry.DetailText, textW);
        var blockH = titleTextHeight + (detailH > 0 ? detailGap + detailH : 0);
        var textTop = nameFaceTop + Math.Max(0, (nameFaceH - blockH) * 0.5);

        Font = cellEntry.TitleFont;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            cellEntry.Title,
            textLeft,
            textTop,
            textW);

        if (detailH > 0)
        {
            Font = cellEntry.DetailTextFont;
            textUtil.AutobreakAndDrawMultilineTextAt(
                ctx,
                Font,
                cellEntry.DetailText,
                textLeft,
                textTop + titleTextHeight + detailGap,
                textW);
        }

        DrawWellIcon(
            ctx,
            IconX * sx,
            IconTopY * sy + pressedYOffset,
            IconSize * sx,
            IconSize * sy,
            isHighlight: true);
        DrawWellIcon(
            ctx,
            IconX * sx,
            IconBotY * sy + pressedYOffset,
            IconSize * sx,
            IconSize * sy,
            isHighlight: false);

        if (cellEntry.DrawAsButton && (pressed || cellEntry.Selected))
        {
            ctx.SetSourceRGBA(0, 0, 0, 0.12);
            ctx.Rectangle(0, 0, drawW, drawH);
            ctx.Fill();
        }
    }

    private static void DrawOuterAndFaces(Context ctx, double drawW, double drawH, bool selected)
    {
        var sx = drawW / RowW;
        var sy = drawH / RowH;

        // Outer fill (black frame area).
        ctx.SetSourceRGB(ColEdge[0], ColEdge[1], ColEdge[2]);
        ctx.Rectangle(0, 0, drawW, drawH);
        ctx.Fill();

        DrawFace(ctx, NameX * sx, NameY * sy, NameW * sx, NameH * sy, selected);
        DrawFace(ctx, IconX * sx, IconTopY * sy, IconSize * sx, IconSize * sy, selected: false);
        DrawFace(ctx, IconX * sx, IconBotY * sy, IconSize * sx, IconSize * sy, selected: false);
    }

    private static void DrawFace(Context ctx, double x, double y, double w, double h, bool selected)
    {
        var face = selected ? ColSelected : ColFace;
        ctx.SetSourceRGB(face[0], face[1], face[2]);
        ctx.Rectangle(x, y, w, h);
        ctx.Fill();

        var bevel = Math.Max(1.5, Math.Min(w, h) * 0.04);
        ctx.SetSourceRGB(ColHi[0], ColHi[1], ColHi[2]);
        ctx.Rectangle(x, y, w, bevel);
        ctx.Fill();
        ctx.Rectangle(x, y, bevel, h);
        ctx.Fill();
        ctx.SetSourceRGB(ColLo[0], ColLo[1], ColLo[2]);
        ctx.Rectangle(x, y + h - bevel, w, bevel);
        ctx.Fill();
        ctx.Rectangle(x + w - bevel, y, bevel, h);
        ctx.Fill();
    }

    private void DrawWellIcon(Context ctx, double wellX, double wellY, double wellW, double wellH, bool isHighlight)
    {
        var iconSize = isHighlight
            ? Math.Min(scaled(UnscaledIconDraw), Math.Min(wellW, wellH) * 0.62)
            : Math.Min(scaled(UnscaledTrashDraw), Math.Min(wellW, wellH) * 0.55);
        var iconX = wellX + (wellW - iconSize) * 0.5;
        var iconY = wellY + (wellH - iconSize) * 0.5;

        if (isHighlight)
        {
            ClaimCairoIcons.DrawHighlight(ctx, iconX, iconY, iconSize, HighlightActive);
        }
        else
        {
            ClaimCairoIcons.DrawTrash(ctx, iconX, iconY, iconSize, destructive: AllowDelete);
        }
    }

    private void ComposeHover(HoverRegion region, ref LoadedTexture texture)
    {
        using var surface = new ImageSurface(Format.Argb32, (int)Bounds.OuterWidth, (int)Bounds.OuterHeight);
        using var ctx = genContext(surface);

        var sx = Bounds.OuterWidth / RowW;
        var sy = Bounds.OuterHeight / RowH;

        ctx.NewPath();
        switch (region)
        {
            case HoverRegion.Left:
                ctx.Rectangle(NameX * sx, NameY * sy, NameW * sx, NameH * sy);
                break;
            case HoverRegion.RightTop:
                ctx.Rectangle(IconX * sx, IconTopY * sy, IconSize * sx, IconSize * sy);
                break;
            default:
                ctx.Rectangle(IconX * sx, IconBotY * sy, IconSize * sx, IconSize * sy);
                break;
        }

        ctx.SetSourceRGBA(0, 0, 0, 0.15);
        ctx.Fill();
        generateTexture(surface, ref texture);
    }

    private bool TryGetHoverRegion(double posX, double posY, out HoverRegion region)
    {
        var sx = Bounds.OuterWidth / RowW;
        var sy = Bounds.OuterHeight / RowH;
        var iconLeft = IconX * sx;
        var iconTop = IconTopY * sy;
        var iconBot = IconBotY * sy;
        var iconW = IconSize * sx;
        var iconH = IconSize * sy;

        if (posX >= iconLeft && posX < iconLeft + iconW)
        {
            if (posY >= iconTop && posY < iconTop + iconH)
            {
                region = HoverRegion.RightTop;
                return true;
            }

            if (posY >= iconBot && posY < iconBot + iconH)
            {
                region = HoverRegion.RightBottom;
                return true;
            }
        }

        region = HoverRegion.Left;
        return true;
    }

    public void UpdateCellHeight()
    {
        Bounds.CalcWorldBounds();
        Bounds.fixedPaddingX = 0;
        Bounds.fixedPaddingY = 0;
        Bounds.fixedHeight = FixedHeight ?? RowH;
    }

    public void OnRenderInteractiveElements(ICoreClientAPI capi, float deltaTime)
    {
        Bounds.CalcWorldBounds();
        if (Bounds.OuterWidthInt <= 0 || Bounds.OuterHeightInt <= 0)
        {
            return;
        }

        if (CellTextureStale(releasedButtonTexture) || CellTextureStale(pressedButtonTexture))
        {
            Compose();
        }

        var texture = cellEntry.Selected ? pressedButtonTexture : releasedButtonTexture;
        capi.Render.Render2DTexturePremultipliedAlpha(
            texture.TextureId,
            (int)Bounds.absX,
            (int)Bounds.absY,
            Bounds.OuterWidthInt,
            Bounds.OuterHeightInt);

        if (!IsPositionInside(capi.Input.MouseX, capi.Input.MouseY))
        {
            return;
        }

        var pos = Bounds.PositionInside(capi.Input.MouseX, capi.Input.MouseY);
        if (pos == null || !TryGetHoverRegion(pos.X, pos.Y, out var region))
        {
            return;
        }

        if (region == HoverRegion.RightBottom && !AllowDelete)
        {
            return;
        }

        var hover = region switch
        {
            HoverRegion.Left => leftHighlightTexture,
            HoverRegion.RightTop => rightTopHighlightTexture,
            _ => rightBottomHighlightTexture
        };

        if (hover.TextureId != 0)
        {
            capi.Render.Render2DTexturePremultipliedAlpha(
                hover.TextureId,
                (int)Bounds.absX,
                (int)Bounds.absY,
                Bounds.OuterWidthInt,
                Bounds.OuterHeightInt);
        }
    }

    public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
    {
        if (!IsPositionInside(args.X, args.Y))
        {
            return;
        }

        var pos = Bounds.PositionInside(args.X, args.Y);
        if (pos == null || !TryGetHoverRegion(pos.X, pos.Y, out var region))
        {
            return;
        }

        args.Handled = true;
        switch (region)
        {
            case HoverRegion.Left:
                OnMouseDownOnCellLeft?.Invoke(elementIndex);
                break;
            case HoverRegion.RightTop:
                OnMouseDownOnCellRight?.Invoke(elementIndex);
                break;
            default:
                if (AllowDelete)
                {
                    OnMouseDownOnCellDelete?.Invoke(elementIndex);
                }

                break;
        }
    }

    public void OnMouseMoveOnElement(MouseEvent args, int elementIndex)
    {
    }

    public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
    {
    }

    private bool CellTextureStale(LoadedTexture texture)
    {
        return texture.TextureId == 0
            || texture.Width != Bounds.OuterWidthInt
            || texture.Height != Bounds.OuterHeightInt;
    }

    public override void Dispose()
    {
        base.Dispose();
        releasedButtonTexture?.Dispose();
        pressedButtonTexture?.Dispose();
        leftHighlightTexture?.Dispose();
        rightTopHighlightTexture?.Dispose();
        rightBottomHighlightTexture?.Dispose();
    }
}
