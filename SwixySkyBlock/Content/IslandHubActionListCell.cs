using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace SwixySkyBlock.Content;

internal sealed class IslandHubActionListCell : GuiElementTextBase, IGuiElementCell
{
    private const string ActionSpawn = "spawn";
    private const string ActionHome = "home";
    private const int UnscaledRowHeight = 251;

    private readonly ICoreClientAPI clientApi;
    private readonly SavegameCellEntry cellEntry;
    private LoadedTexture normalTexture;
    private LoadedTexture pressedTexture;
    private LoadedTexture hoverTexture;

    public Action<int>? OnSelect;

    ElementBounds IGuiElementCell.Bounds => Bounds;

    public IslandHubActionListCell(ICoreClientAPI api, SavegameCellEntry cell, ElementBounds bounds)
        : base(api, "", null, bounds)
    {
        clientApi = api;
        cellEntry = cell;
        normalTexture = new LoadedTexture(api);
        pressedTexture = new LoadedTexture(api);
        hoverTexture = new LoadedTexture(api);
    }

    public void Compose()
    {
        Bounds.CalcWorldBounds();

        using var surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt);
        using var ctx = genContext(surface);

        ComposeCell(ctx, pressed: false);
        generateTexture(surface, ref normalTexture);

        ctx.Operator = Operator.Clear;
        ctx.Paint();
        ctx.Operator = Operator.Over;

        ComposeCell(ctx, pressed: true);
        generateTexture(surface, ref pressedTexture);

        ctx.Operator = Operator.Clear;
        ctx.Paint();
        ctx.Operator = Operator.Over;

        DrawBackground(ctx, hover: true, pressed: false);
        generateTexture(surface, ref hoverTexture);
    }

    private void ComposeCell(Context ctx, bool pressed)
    {
        DrawBackground(ctx, hover: false, pressed);

        var iconSize = Math.Min(Bounds.OuterWidth, Bounds.OuterHeight) * 0.82;
        var iconX = (Bounds.OuterWidth - iconSize) / 2;
        var iconY = (Bounds.OuterHeight - iconSize) / 2 + (pressed ? scaled(1) : 0);

        if (cellEntry.Title == ActionHome && !cellEntry.Enabled)
        {
            IslandHubIcons.DrawGoHomeOutline(ctx, iconX, iconY, iconSize);
            return;
        }

        if (cellEntry.Title == ActionHome)
        {
            IslandHubIcons.DrawGoHome(ctx, iconX, iconY, iconSize);
            return;
        }

        IslandHubIcons.DrawGoSpawn(ctx, iconX, iconY, iconSize);
    }

    private void DrawBackground(Context ctx, bool hover, bool pressed)
    {
        var alpha = cellEntry.Enabled ? 1.0 : 0.52;
        var yOffset = pressed ? scaled(1) : 0;

        RoundRectangle(ctx, 0, yOffset, Bounds.OuterWidthInt, Bounds.OuterHeightInt - yOffset, 6);
        ctx.SetSourceRGBA(0.01, 0.012, 0.014, 0.92);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0, 0, 0, 0.78);
        ctx.LineWidth = scaled(1.5);
        ctx.Stroke();

        RoundRectangle(ctx, scaled(1.5), scaled(1.5) + yOffset, Bounds.OuterWidth - scaled(3), Bounds.OuterHeight - scaled(3) - yOffset, 5);
        ctx.SetSourceRGBA(1, 1, 1, hover && cellEntry.Enabled ? 0.13 : 0.055 * alpha);
        ctx.LineWidth = scaled(1.2);
        ctx.Stroke();

        if (!hover || !cellEntry.Enabled)
        {
            return;
        }

        RoundRectangle(ctx, scaled(3), scaled(3) + yOffset, Bounds.OuterWidth - scaled(6), Bounds.OuterHeight - scaled(6) - yOffset, 4);
        ctx.SetSourceRGBA(0.55, 0.74, 0.95, 0.12);
        ctx.Fill();
    }

    public void UpdateCellHeight()
    {
        Bounds.fixedHeight = UnscaledRowHeight;
    }

    public void OnRenderInteractiveElements(ICoreClientAPI capi, float deltaTime)
    {
        if (normalTexture.TextureId == 0)
        {
            Compose();
        }

        var texture = cellEntry.Selected ? pressedTexture : normalTexture;
        capi.Render.Render2DTexturePremultipliedAlpha(
            texture.TextureId,
            Bounds.absX,
            Bounds.absY,
            Bounds.OuterWidth,
            Bounds.OuterHeight);

        if (cellEntry.Enabled && IsPositionInside(capi.Input.MouseX, capi.Input.MouseY))
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
        if (!cellEntry.Enabled)
        {
            args.Handled = true;
            return;
        }

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
        normalTexture.Dispose();
        pressedTexture.Dispose();
        hoverTexture.Dispose();
    }
}
