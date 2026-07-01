using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock.Content;

internal sealed class IslandTemplateListCell : GuiElementTextBase, IGuiElementCell
{
    private const int UnscaledMinRowHeight = 58;
    private const int UnscaledIconSize = 34;

    private readonly ICoreClientAPI clientApi;
    private readonly SavegameCellEntry cellEntry;
    private LoadedTexture releasedButtonTexture;
    private LoadedTexture pressedButtonTexture;
    private LoadedTexture hoverTexture;

    public System.Action<int>? OnSelect;

    ElementBounds IGuiElementCell.Bounds => Bounds;

    public IslandTemplateListCell(ICoreClientAPI api, SavegameCellEntry cell, ElementBounds bounds)
        : base(api, "", null, bounds)
    {
        clientApi = api;
        cellEntry = cell;
        releasedButtonTexture = new LoadedTexture(api);
        pressedButtonTexture = new LoadedTexture(api);
        hoverTexture = new LoadedTexture(api);

        cellEntry.TitleFont ??= CairoFont.WhiteSmallText();
        cellEntry.DetailTextFont ??= CairoFont.WhiteDetailText().WithFontSize(12);
    }

    public void Compose()
    {
        Bounds.CalcWorldBounds();

        using (var surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt))
        using (var ctx = genContext(surface))
        {
            ComposeButton(ctx, pressed: false);
            generateTexture(surface, ref releasedButtonTexture);

            ctx.Operator = Operator.Clear;
            ctx.Paint();
            ctx.Operator = Operator.Over;

            ComposeButton(ctx, pressed: true);
            generateTexture(surface, ref pressedButtonTexture);

            ctx.Operator = Operator.Clear;
            ctx.Paint();
            ctx.Operator = Operator.Over;

            ctx.Rectangle(0, 0, Bounds.OuterWidthInt, Bounds.OuterHeightInt);
            ctx.SetSourceRGBA(1, 1, 1, 0.08);
            ctx.Fill();
            generateTexture(surface, ref hoverTexture);
        }
    }

    private void ComposeButton(Context ctx, bool pressed)
    {
        var alpha = cellEntry.Enabled ? 1.0 : 0.45;
        var yOffset = pressed ? scaled(1) : 0;

        IslandHubTheme.ApplyListButtonFill(ctx, Bounds.OuterWidthInt, Bounds.OuterHeightInt, pressed, alpha);

        var iconSize = scaled(UnscaledIconSize);
        var iconX = Bounds.absPaddingX;
        var iconY = (Bounds.OuterHeight - iconSize) / 2 + yOffset;
        IslandHubIcons.DrawTemplateIcon(ctx, cellEntry.Title, iconX, iconY, iconSize);

        var textX = Bounds.absPaddingX + iconSize + scaled(12);
        var textWidth = Bounds.InnerWidth - iconSize - scaled(18);
        var titleY = scaled(7) + yOffset;

        Font = cellEntry.TitleFont;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            cellEntry.Title,
            textX,
            titleY,
            textWidth);

        Font = cellEntry.DetailTextFont;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            cellEntry.DetailText,
            textX,
            titleY + scaled(23),
            textWidth);
    }

    public void UpdateCellHeight()
    {
        Bounds.fixedHeight = UnscaledMinRowHeight;
    }

    public void OnRenderInteractiveElements(ICoreClientAPI capi, float deltaTime)
    {
        if (releasedButtonTexture.TextureId == 0)
        {
            Compose();
        }

        var texture = cellEntry.Selected ? pressedButtonTexture : releasedButtonTexture;
        capi.Render.Render2DTexturePremultipliedAlpha(
            texture.TextureId,
            Bounds.absX,
            Bounds.absY,
            Bounds.OuterWidth,
            Bounds.OuterHeight);

        if (IsPositionInside(capi.Input.MouseX, capi.Input.MouseY))
        {
            capi.Render.Render2DTexturePremultipliedAlpha(
                hoverTexture.TextureId,
                Bounds.absX,
                Bounds.absY,
                Bounds.OuterWidth,
                Bounds.OuterHeight);
        }
    }

    public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
    {
        clientApi.Gui.PlaySound("toggleswitch");
        OnSelect?.Invoke(elementIndex);
        args.Handled = true;
    }

    public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
    {
    }

    public void OnMouseMoveOnElement(MouseEvent args, int elementIndex)
    {
    }

    public override void Dispose()
    {
        base.Dispose();
        releasedButtonTexture.Dispose();
        pressedButtonTexture.Dispose();
        hoverTexture.Dispose();
    }
}
