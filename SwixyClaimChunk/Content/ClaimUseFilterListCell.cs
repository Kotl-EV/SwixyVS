// =============================================================================
// ClaimUseFilterListCell.cs
// -----------------------------------------------------------------------------
// Строка whitelist Use: иконка предмета слева, название справа.
// Клик по строке — вкл/выкл в draft. Без инвентарных слотов (нет network-пакетов).
// =============================================================================

using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Ячейка списка блоков use-filter: [иконка] название.
/// </summary>
public sealed class ClaimUseFilterListCell : GuiElementTextBase, IGuiElementCell
{
    private const int UnscaledRowHeight = 44;
    private const int UnscaledIconSize = 36;
    private const int UnscaledIconPad = 6;
    private static readonly int UnscaledDepth = 3;

    public SavegameCellEntry cellEntry;
    public string BlockCode = "";
    public ItemStack? Stack;
    public bool IsWhitelisted;
    public Action<string>? OnToggle;

    private readonly DummySlot renderSlot;
    private LoadedTexture releasedTexture;
    private LoadedTexture selectedTexture;
    private LoadedTexture hoverTexture;
    private double titleTextHeight;

    public double? FixedHeight { get; set; } = UnscaledRowHeight;

    ElementBounds IGuiElementCell.Bounds => Bounds;

    public ClaimUseFilterListCell(
        ICoreClientAPI capi,
        SavegameCellEntry cell,
        ElementBounds bounds,
        string blockCode,
        ItemStack? stack,
        bool isWhitelisted)
        : base(capi, "", null, bounds)
    {
        cellEntry = cell;
        BlockCode = blockCode ?? "";
        Stack = stack;
        IsWhitelisted = isWhitelisted;

        renderSlot = new DummySlot(stack);
        releasedTexture = new LoadedTexture(capi);
        selectedTexture = new LoadedTexture(capi);
        hoverTexture = new LoadedTexture(capi);

        cell.TitleFont ??= CairoFont.WhiteSmallText();
        cell.DetailTextFont ??= CairoFont.WhiteDetailText();
        cell.DetailTextFont.Color[3] *= 0.75;
    }

    public void Compose()
    {
        Bounds.CalcWorldBounds();

        using (var surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt))
        using (var ctx = genContext(surface))
        {
            ComposeRow(ctx, selected: false);
            generateTexture(surface, ref releasedTexture);

            ctx.Operator = Operator.Clear;
            ctx.Paint();
            ctx.Operator = Operator.Over;

            ComposeRow(ctx, selected: true);
            generateTexture(surface, ref selectedTexture);
        }

        ComposeHover();
    }

    private void ComposeRow(Context ctx, bool selected)
    {
        RoundRectangle(ctx, 0, 0, Bounds.OuterWidthInt, Bounds.OuterHeightInt, 1);
        if (selected)
        {
            // Зелёный оттенок — блок в whitelist.
            ctx.SetSourceRGBA(0.18, 0.42, 0.22, 0.95);
        }
        else
        {
            ctx.SetSourceRGB(
                GuiStyle.DialogDefaultBgColor[0],
                GuiStyle.DialogDefaultBgColor[1],
                GuiStyle.DialogDefaultBgColor[2]);
        }

        ctx.Fill();
        EmbossRoundRectangleElement(
            ctx,
            0,
            0,
            Bounds.OuterWidthInt,
            Bounds.OuterHeightInt,
            inverse: selected,
            (int)scaled(UnscaledDepth));

        var iconArea = scaled(UnscaledIconPad * 2 + UnscaledIconSize);
        var textX = iconArea + Bounds.absPaddingX;
        var textWidth = Math.Max(20, Bounds.OuterWidth - textX - Bounds.absPaddingX);

        Font = cellEntry.TitleFont;
        titleTextHeight = textUtil.GetMultilineTextHeight(Font, cellEntry.Title, textWidth);
        var titleY = (Bounds.OuterHeight - titleTextHeight) / 2;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            cellEntry.Title,
            textX,
            titleY,
            textWidth);

        if (selected)
        {
            RoundRectangle(ctx, 0, 0, Bounds.OuterWidthInt, Bounds.OuterHeightInt, 1);
            ctx.SetSourceRGBA(0.2, 0.7, 0.3, 0.12);
            ctx.Fill();
        }
    }

    private void ComposeHover()
    {
        using var surface = new ImageSurface(Format.Argb32, (int)Bounds.OuterWidth, (int)Bounds.OuterHeight);
        using var ctx = genContext(surface);
        RoundRectangle(ctx, 0, 0, Bounds.OuterWidthInt, Bounds.OuterHeightInt, 1);
        ctx.SetSourceRGBA(0, 0, 0, 0.14);
        ctx.Fill();
        generateTexture(surface, ref hoverTexture);
    }

    public void UpdateCellHeight()
    {
        Bounds.CalcWorldBounds();
        Bounds.fixedHeight = FixedHeight ?? UnscaledRowHeight;
    }

    public void OnRenderInteractiveElements(ICoreClientAPI capi, float deltaTime)
    {
        if (releasedTexture.TextureId == 0)
        {
            Compose();
        }

        var tex = IsWhitelisted ? selectedTexture : releasedTexture;
        capi.Render.Render2DTexturePremultipliedAlpha(
            tex.TextureId,
            (int)Bounds.absX,
            (int)Bounds.absY,
            Bounds.OuterWidthInt,
            Bounds.OuterHeightInt);

        RenderItemIcon(capi, deltaTime);

        if (!IsPositionInside(capi.Input.MouseX, capi.Input.MouseY))
        {
            return;
        }

        capi.Render.Render2DTexturePremultipliedAlpha(
            hoverTexture.TextureId,
            Bounds.absX,
            Bounds.absY,
            Bounds.OuterWidth,
            Bounds.OuterHeight);
    }

    private void RenderItemIcon(ICoreClientAPI capi, float deltaTime)
    {
        if (Stack?.Collectible == null)
        {
            return;
        }

        if (renderSlot.Itemstack == null
            || renderSlot.Itemstack.Collectible != Stack.Collectible)
        {
            renderSlot.Itemstack = Stack.Clone();
            renderSlot.Itemstack.StackSize = 1;
        }

        var iconSize = scaled(UnscaledIconSize);
        var pad = scaled(UnscaledIconPad);
        var iconX = Bounds.renderX + pad;
        var iconY = Bounds.renderY + (Bounds.OuterHeight - iconSize) / 2;
        var centerX = iconX + iconSize / 2;
        var centerY = iconY + iconSize / 2;
        var renderSize = (float)(iconSize * (GuiElementPassiveItemSlot.unscaledItemSize
            / GuiElementPassiveItemSlot.unscaledSlotSize));

        // Локальный scissor вокруг иконки, чтобы 3D-модель не вылезала из строки.
        var scissor = ElementBounds.Fixed(0, 0, UnscaledIconSize, UnscaledIconSize);
        scissor.ParentBounds = capi.Gui.WindowBounds;
        scissor.CalcWorldBounds();
        scissor.absFixedX = iconX;
        scissor.absFixedY = iconY;
        scissor.absInnerWidth = iconSize;
        scissor.absInnerHeight = iconSize;

        capi.Render.PushScissor(scissor, true);
        capi.Render.RenderItemstackToGui(
            renderSlot,
            centerX,
            centerY,
            200,
            renderSize,
            ColorUtil.WhiteArgb);
        capi.Render.PopScissor();
    }

    public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
    {
        if (!IsPositionInside(api.Input.MouseX, api.Input.MouseY))
        {
            return;
        }

        args.Handled = true;
    }

    public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
    {
        if (!IsPositionInside(api.Input.MouseX, api.Input.MouseY))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(BlockCode))
        {
            return;
        }

        api.Gui.PlaySound("toggleswitch");
        OnToggle?.Invoke(BlockCode);
        args.Handled = true;
    }

    public void OnMouseMoveOnElement(MouseEvent args, int elementIndex)
    {
    }

    /// <summary>Обновляет визуал выбора без полной перекомпозиции списка.</summary>
    public void SetWhitelisted(bool value)
    {
        if (IsWhitelisted == value)
        {
            return;
        }

        IsWhitelisted = value;
        cellEntry.Selected = value;
    }

    public override void Dispose()
    {
        base.Dispose();
        releasedTexture.Dispose();
        selectedTexture.Dispose();
        hoverTexture.Dispose();
    }
}
