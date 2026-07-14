using System;
using Cairo;
using SwixySkyBlock.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock.Content;

public sealed class StoryDungeonCellEntry
{
    public StoryDungeonSiteStatePacket Site = null!;
    public Action<string>? OnTeleport;
}

internal sealed class StoryDungeonListCell : GuiElementTextBase, IGuiElementCell
{
    private const int UnscaledRowHeight = 52;
    private const int UnscaledOrderWidth = 40;
    private const int UnscaledStatusWidth = 108;

    private readonly StoryDungeonCellEntry cellEntry;
    private LoadedTexture rowTexture = null!;

    public double? FixedHeight => UnscaledRowHeight;

    ElementBounds IGuiElementCell.Bounds => Bounds;

    public StoryDungeonListCell(ICoreClientAPI api, StoryDungeonCellEntry cell, ElementBounds bounds)
        : base(api, "", null, bounds)
    {
        cellEntry = cell;
        rowTexture = new LoadedTexture(api);
    }

    public void Compose()
    {
        Bounds.CalcWorldBounds();

        using var surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt);
        using var ctx = genContext(surface);

        var site = cellEntry.Site;
        IslandHubTheme.DrawListRowInset(ctx, Bounds.OuterWidthInt, Bounds.OuterHeightInt, accent: site.Ready);

        var orderWidth = scaled(UnscaledOrderWidth);
        var statusWidth = scaled(UnscaledStatusWidth);
        var padding = Bounds.absPaddingX;
        var textY = (Bounds.OuterHeight - scaled(14)) / 2;

        var orderFont = CairoFont.WhiteSmallText();
        orderFont.Color = [(float)IslandHubTheme.AccentGoldR, (float)IslandHubTheme.AccentGoldG, (float)IslandHubTheme.AccentGoldB, 1f];
        Font = orderFont;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            site.Order.ToString(),
            padding,
            textY,
            orderWidth - padding);

        var nameFont = site.Ready
            ? CairoFont.WhiteSmallText()
            : IslandHubTheme.CreateMutedTitleFont();
        Font = nameFont;
        var nameX = padding + orderWidth;
        var nameWidth = Bounds.InnerWidth - orderWidth - statusWidth - padding * 2;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            site.Name,
            nameX,
            textY,
            nameWidth);

        var statusKey = site.Ready
            ? "swixyskyblock:story-site-ready"
            : site.Generating
                ? "swixyskyblock:story-site-generating"
                : "swixyskyblock:story-site-pending";
        var statusFont = CairoFont.WhiteDetailText().WithFontSize(11);
        statusFont.Color[3] *= site.Ready ? 1f : 0.75f;
        Font = statusFont;
        var statusX = Bounds.OuterWidth - statusWidth - padding;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            Lang.Get(statusKey),
            statusX,
            textY,
            statusWidth);

        generateTexture(surface, ref rowTexture);
    }

    public void UpdateCellHeight()
    {
        Bounds.fixedHeight = UnscaledRowHeight;
    }

    public void OnRenderInteractiveElements(ICoreClientAPI capi, float deltaTime)
    {
        if (rowTexture.TextureId == 0)
        {
            Compose();
        }

        capi.Render.Render2DTexturePremultipliedAlpha(
            rowTexture.TextureId,
            Bounds.absX,
            Bounds.absY,
            Bounds.OuterWidth,
            Bounds.OuterHeight);
    }

    public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
    {
        if (!Bounds.PointInside(api.Input.MouseX, api.Input.MouseY))
        {
            return;
        }

        if (cellEntry.Site.Ready || !cellEntry.Site.Generating)
        {
            cellEntry.OnTeleport?.Invoke(cellEntry.Site.Code);
        }
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
        rowTexture.Dispose();
    }
}