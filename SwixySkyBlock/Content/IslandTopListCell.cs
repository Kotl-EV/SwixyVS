using Cairo;
using SwixySkyBlock.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock.Content;

public sealed class IslandTopCellEntry
{
    public IslandTopEntryPacket Entry = null!;
    public string TemplateLabel = "";
}

internal sealed class IslandTopListCell : GuiElementTextBase, IGuiElementCell
{
    private const int UnscaledRowHeight = 40;
    private const int UnscaledRankWidth = 44;
    private const int UnscaledLevelWidth = 72;
    private const int UnscaledTemplateWidth = 120;

    private readonly IslandTopCellEntry cellEntry;
    private LoadedTexture rowTexture = null!;

    public double? FixedHeight => UnscaledRowHeight;

    ElementBounds IGuiElementCell.Bounds => Bounds;

    public IslandTopListCell(ICoreClientAPI api, IslandTopCellEntry cell, ElementBounds bounds)
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

        var entry = cellEntry.Entry;
        var accent = entry.IsViewer || entry.Rank <= 3;
        IslandHubTheme.DrawListRowInset(ctx, Bounds.OuterWidthInt, Bounds.OuterHeightInt, accent: accent);

        var rankWidth = scaled(UnscaledRankWidth);
        var levelWidth = scaled(UnscaledLevelWidth);
        var templateWidth = scaled(UnscaledTemplateWidth);
        var padding = Bounds.absPaddingX;
        var textY = (Bounds.OuterHeight - scaled(14)) / 2;

        var rankFont = CreateRankFont(entry.Rank, entry.IsViewer);
        Font = rankFont;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            $"#{entry.Rank}",
            padding,
            textY,
            rankWidth - padding);

        var nameFont = entry.IsViewer
            ? IslandHubTheme.CreateSectionTitleFont()
            : CairoFont.WhiteSmallText();
        Font = nameFont;
        var nameX = padding + rankWidth;
        var nameWidth = Bounds.InnerWidth - rankWidth - levelWidth - templateWidth - padding * 2;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            entry.PlayerName,
            nameX,
            textY,
            nameWidth);

        var levelFont = CairoFont.WhiteDetailText().WithFontSize(12);
        Font = levelFont;
        var levelText = Lang.Get("swixyskyblock:island-top-level-short", entry.GeneratorLevel);
        var levelX = Bounds.OuterWidth - levelWidth - templateWidth - padding;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            levelText,
            levelX,
            textY,
            levelWidth);

        var templateFont = CairoFont.WhiteDetailText().WithFontSize(11);
        templateFont.Color[3] *= 0.82f;
        Font = templateFont;
        var templateX = Bounds.OuterWidth - templateWidth - padding;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            cellEntry.TemplateLabel,
            templateX,
            textY,
            templateWidth);

        generateTexture(surface, ref rowTexture);
    }

    private static CairoFont CreateRankFont(int rank, bool isViewer)
    {
        var font = CairoFont.WhiteSmallText();
        if (isViewer)
        {
            font.Color = [(float)IslandHubTheme.AccentGoldR, (float)IslandHubTheme.AccentGoldG, (float)IslandHubTheme.AccentGoldB, 1f];
            return font;
        }

        switch (rank)
        {
            case 1:
                font.Color = [1f, 0.86f, 0.28f, 1f];
                break;
            case 2:
                font.Color = [0.82f, 0.86f, 0.92f, 1f];
                break;
            case 3:
                font.Color = [0.86f, 0.62f, 0.36f, 1f];
                break;
            default:
                font.Color = [0.72f, 0.78f, 0.88f, 1f];
                break;
        }

        return font;
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