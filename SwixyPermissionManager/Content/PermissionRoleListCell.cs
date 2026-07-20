using System;
using Cairo;
using Vintagestory.API.Client;

namespace SwixyPermissionManager.Content;

/// <summary>Строка роли — navy-card + shield icon.</summary>
internal sealed class PermissionRoleListCell : GuiElementTextBase, IGuiElementCell
{
    private const int RowH = 42;

    private readonly ICoreClientAPI clientApi;
    private readonly SavegameCellEntry cellEntry;
    private LoadedTexture normalTex;
    private LoadedTexture selectedTex;
    private LoadedTexture hoverTex;

    public Action? OnSelect;

    ElementBounds IGuiElementCell.Bounds => Bounds;

    public PermissionRoleListCell(ICoreClientAPI api, SavegameCellEntry cell, ElementBounds bounds)
        : base(api, "", null, bounds)
    {
        clientApi = api;
        cellEntry = cell;
        normalTex = new LoadedTexture(api);
        selectedTex = new LoadedTexture(api);
        hoverTex = new LoadedTexture(api);
        cellEntry.TitleFont ??= CairoFont.WhiteSmallishText().WithColor(PermissionTheme.ColText);
        cellEntry.DetailTextFont ??= CairoFont.WhiteDetailText().WithColor(PermissionTheme.ColTextMuted);
    }

    public void Compose()
    {
        Bounds.CalcWorldBounds();
        var w = Bounds.OuterWidthInt;
        var h = Bounds.OuterHeightInt;
        using var surface = new ImageSurface(Format.Argb32, w, h);
        using var ctx = genContext(surface);

        Draw(ctx, w, h, selected: false);
        generateTexture(surface, ref normalTex);

        ctx.Operator = Operator.Clear;
        ctx.Paint();
        ctx.Operator = Operator.Over;
        Draw(ctx, w, h, selected: true);
        generateTexture(surface, ref selectedTex);

        ctx.Operator = Operator.Clear;
        ctx.Paint();
        ctx.Operator = Operator.Over;
        var hv = PermissionTheme.ColHover;
        ctx.SetSourceRGBA(hv[0], hv[1], hv[2], hv[3]);
        RoundRect(ctx, 1, 1, w - 2, h - 2, 5);
        ctx.Fill();
        generateTexture(surface, ref hoverTex);
    }

    private void Draw(Context ctx, int w, int h, bool selected)
    {
        var face = selected ? PermissionTheme.ColCardSelected : PermissionTheme.ColCard;
        ctx.SetSourceRGBA(face[0], face[1], face[2], face[3]);
        RoundRect(ctx, 1, 1, w - 2, h - 2, 5);
        ctx.Fill();

        var border = selected ? PermissionTheme.ColAccent : PermissionTheme.ColBorder;
        ctx.SetSourceRGBA(border[0], border[1], border[2], selected ? 0.95 : 0.7);
        ctx.LineWidth = selected ? 1.5 : 1;
        RoundRect(ctx, 1, 1, w - 2, h - 2, 5);
        ctx.Stroke();

        // Left accent bar when selected
        if (selected)
        {
            var a = PermissionTheme.ColAccent;
            ctx.SetSourceRGBA(a[0], a[1], a[2], 0.95);
            RoundRect(ctx, 1, 4, 3, h - 8, 1.5);
            ctx.Fill();
        }

        var icon = scaled(22);
        var iconX = scaled(8);
        var iconY = (h - icon) / 2;
        PermissionCairoIcons.DrawShield(ctx, iconX, iconY, icon, selected);

        var textX = iconX + icon + scaled(8);
        var textW = w - textX - scaled(8);

        Font = cellEntry.TitleFont;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx, Font, cellEntry.Title ?? "", textX, scaled(6), textW);

        if (!string.IsNullOrEmpty(cellEntry.DetailText))
        {
            Font = cellEntry.DetailTextFont;
            textUtil.AutobreakAndDrawMultilineTextAt(
                ctx, Font, cellEntry.DetailText, textX, scaled(23), textW);
        }
    }

    private static void RoundRect(Context ctx, double x, double y, double w, double h, double r)
    {
        ctx.NewPath();
        ctx.MoveTo(x + r, y);
        ctx.LineTo(x + w - r, y);
        ctx.CurveTo(x + w, y, x + w, y, x + w, y + r);
        ctx.LineTo(x + w, y + h - r);
        ctx.CurveTo(x + w, y + h, x + w, y + h, x + w - r, y + h);
        ctx.LineTo(x + r, y + h);
        ctx.CurveTo(x, y + h, x, y + h, x, y + h - r);
        ctx.LineTo(x, y + r);
        ctx.CurveTo(x, y, x, y, x + r, y);
        ctx.ClosePath();
    }

    public void UpdateCellHeight() => Bounds.fixedHeight = RowH;

    public void OnRenderInteractiveElements(ICoreClientAPI capi, float deltaTime)
    {
        if (normalTex.TextureId == 0)
        {
            Compose();
        }

        var tex = cellEntry.Selected ? selectedTex : normalTex;
        capi.Render.Render2DTexturePremultipliedAlpha(
            tex.TextureId, Bounds.absX, Bounds.absY, Bounds.OuterWidth, Bounds.OuterHeight);

        if (IsPositionInside(capi.Input.MouseX, capi.Input.MouseY))
        {
            capi.Render.Render2DTexturePremultipliedAlpha(
                hoverTex.TextureId, Bounds.absX, Bounds.absY, Bounds.OuterWidth, Bounds.OuterHeight);
        }
    }

    public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
    {
        clientApi.Gui.PlaySound("toggleswitch");
        OnSelect?.Invoke();
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
        normalTex.Dispose();
        selectedTex.Dispose();
        hoverTex.Dispose();
    }
}
