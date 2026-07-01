using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace SwixySkyBlock.Content;

internal sealed class IslandHubActionListCell : GuiElementTextBase, IGuiElementCell
{
    private const string ActionSpawn = "spawn";
    private const string ActionHome = "home";
    private const int UnscaledRowHeight = 277;

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

        DrawHoverOverlay(ctx);
        generateTexture(surface, ref hoverTexture);
    }

    private void ComposeCell(Context ctx, bool pressed)
    {
        DrawBackground(ctx, pressed);

        var iconSize = Math.Min(Bounds.OuterWidth, Bounds.OuterHeight) * 0.72;
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

    private void DrawBackground(Context ctx, bool pressed)
    {
        var alpha = cellEntry.Enabled ? 1.0 : 0.52;
        IslandHubTheme.DrawActionCard(
            ctx,
            Bounds.OuterWidth,
            Bounds.OuterHeight,
            6,
            hover: false,
            pressed,
            alpha);
    }

    private void DrawHoverOverlay(Context ctx)
    {
        if (!cellEntry.Enabled)
        {
            return;
        }

        IslandHubTheme.DrawActionCardHoverOverlay(
            ctx,
            Bounds.OuterWidth,
            Bounds.OuterHeight,
            6);
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
