using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using SwixySkyBlock.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixySkyBlock.Content;

public sealed class GeneratorLevelCellEntry
{
    public IslandGeneratorLevelStatePacket Level = null!;
}

internal sealed class IslandGeneratorLevelListCell : GuiElementTextBase, IGuiElementCell
{
    private const int UnscaledIconSize = 32;
    private const int UnscaledIconGap = 6;
    private const int UnscaledTitleArea = 18;
    private const int UnscaledTitleToIconsGap = 10;
    private const int UnscaledIconRowHeight = 40;
    private const int UnscaledWrappedRowExtraOffset = 10;
    private const int UnscaledBottomPad = 4;

    private const float RenderZRowBackground = 50f;
    private const float RenderZTitle = 55f;
    private const float RenderZIcon = 100f;
    private const float RenderZPercentLabel = 105f;

    private readonly ICoreClientAPI clientApi;
    private readonly GeneratorLevelCellEntry cellEntry;
    private readonly CairoFont percentFont;
    private readonly IslandGeneratorSlideshowIconRenderer iconRenderer;
    private readonly DummyInventory tooltipInventory;
    private readonly DummySlot tooltipSlot;
    private LoadedTexture rowBackgroundTexture = null!;
    private LoadedTexture titleTexture = null!;
    private readonly List<GeneratorIconEntry> icons = [];
    private string titleText = "";
    private int composedWidth;
    private int composedHeight;
    private GeneratorIconEntry? tooltipIcon;
    private int tooltipVariantIndex = -1;

    public double? FixedHeight => CalcLayout().UnscaledCellHeight;

    ElementBounds IGuiElementCell.Bounds => Bounds;

    public IslandGeneratorLevelListCell(
        ICoreClientAPI api,
        GeneratorLevelCellEntry cell,
        ElementBounds bounds)
        : base(api, "", null, bounds)
    {
        clientApi = api;
        cellEntry = cell;
        percentFont = CairoFont.WhiteDetailText().WithFontSize(9);
        rowBackgroundTexture = new LoadedTexture(api);
        titleTexture = new LoadedTexture(api);
        iconRenderer = new IslandGeneratorSlideshowIconRenderer(api);
        tooltipInventory = new DummyInventory(api);
        tooltipSlot = new DummySlot(null, tooltipInventory);
        RebuildIconData();
    }

    public void RebuildIconData()
    {
        DisposeIconTextures();
        icons.Clear();

        var level = cellEntry.Level;
        titleText = level.Unlocked
            ? Lang.Get("swixyskyblock:island-generator-level-unlocked", level.Level)
            : Lang.Get("swixyskyblock:island-generator-level-locked", level.Level);

        foreach (var entry in (level.Entries ?? []).OrderByDescending(static candidate => candidate.Percent))
        {
            var percentLabel = $"{entry.Percent:0.#}%";

            icons.Add(new GeneratorIconEntry
            {
                BlockCode = entry.BlockCode,
                DisplayBlockCode = entry.DisplayBlockCode,
                PercentLabel = percentLabel
            });
        }

        composedWidth = 0;
        composedHeight = 0;
    }

    public void Compose()
    {
        Bounds.CalcWorldBounds();
        UpdateCellHeight();
        var width = Math.Max(1, Bounds.OuterWidthInt);
        var height = Math.Max(1, Bounds.OuterHeightInt);

        var titleFont = cellEntry.Level.Unlocked
            ? IslandHubTheme.CreateSectionTitleFont()
            : IslandHubTheme.CreateMutedTitleFont();

        titleTexture.Dispose();
        titleTexture = clientApi.Gui.TextTexture.GenTextTexture(titleText, titleFont);

        foreach (var icon in icons)
        {
            icon.PercentTexture?.Dispose();
            icon.PercentTexture = clientApi.Gui.TextTexture.GenTextTexture(icon.PercentLabel, percentFont);
            icon.CurrentVariantIndex = 0;
            icon.LastSwitchMs = 0;
            icon.VariantStacks = [];
        }

        if (width == composedWidth && height == composedHeight && rowBackgroundTexture.TextureId != 0)
        {
            return;
        }

        if (width > 2048 || height > 512)
        {
            return;
        }

        try
        {
            using var surface = new ImageSurface(Format.Argb32, width, height);
            using var ctx = genContext(surface);
            IslandHubTheme.DrawListRow(ctx, width, height, cellEntry.Level.Unlocked);
            generateTexture(surface, ref rowBackgroundTexture);
            composedWidth = width;
            composedHeight = height;
        }
        catch (Exception ex)
        {
            api.Logger.Warning("[SwixySkyBlock][Hub] Generator row background failed: {0}", ex.Message);
        }
    }

    public void UpdateCellHeight()
    {
        Bounds.fixedHeight = CalcLayout().UnscaledCellHeight;
    }

    public void OnRenderInteractiveElements(ICoreClientAPI capi, float deltaTime)
    {
        if (titleTexture.TextureId == 0 || rowBackgroundTexture.TextureId == 0)
        {
            Compose();
        }

        if (Bounds.OuterWidth < 1 || Bounds.OuterHeight < 1)
        {
            return;
        }

        Bounds.CalcWorldBounds();
        if (Bounds.OuterWidthInt != composedWidth || Bounds.OuterHeightInt != composedHeight)
        {
            Compose();
        }

        var layout = CalcLayout();
        var iconGap = scaled(UnscaledIconGap);
        var iconSize = (float)scaled(UnscaledIconSize);
        var iconRowHeight = scaled(UnscaledIconRowHeight);
        var iconColor = cellEntry.Level.Unlocked
            ? -1
            : ColorUtil.ColorMultiply3(ColorUtil.WhiteArgb, 0.55f);

        if (rowBackgroundTexture.TextureId != 0)
        {
            api.Render.Render2DTexturePremultipliedAlpha(
                rowBackgroundTexture.TextureId,
                (int)Bounds.absX,
                (int)Bounds.absY,
                Bounds.OuterWidthInt,
                Bounds.OuterHeightInt,
                RenderZRowBackground);
        }

        if (titleTexture.TextureId != 0)
        {
            api.Render.Render2DTexturePremultipliedAlpha(
                titleTexture.TextureId,
                Bounds.absX + Bounds.absPaddingX,
                Bounds.absY + scaled(4),
                titleTexture.Width,
                titleTexture.Height,
                RenderZTitle);
        }

        var startX = Bounds.absX + Bounds.absPaddingX;
        var baseIconY = Bounds.absY + scaled(UnscaledTitleArea + UnscaledTitleToIconsGap);
        var wrappedRowExtraOffset = scaled(UnscaledWrappedRowExtraOffset);
        var iconStep = iconSize + iconGap;
        var col = 0;
        var row = 0;

        foreach (var icon in icons)
        {
            var iconX = startX + col * iconStep;
            var iconY = baseIconY + row * iconRowHeight + row * wrappedRowExtraOffset;
            var mouseOver = IsMouseOverIcon(iconX, iconY, iconSize);
            icon.EnsureVariants(clientApi);
            iconRenderer.Render(
                icon.VariantStacks,
                ref icon.CurrentVariantIndex,
                ref icon.LastSwitchMs,
                iconX,
                iconY,
                iconSize,
                RenderZIcon,
                mouseOver,
                iconColor);

            if (icon.PercentTexture is { TextureId: not 0 } percentTexture)
            {
                var labelWidth = percentTexture.Width;
                var labelX = iconX + (iconSize - labelWidth) / 2.0;
                var labelY = iconY + iconSize + scaled(1);
                api.Render.Render2DTexturePremultipliedAlpha(
                    percentTexture.TextureId,
                    labelX,
                    labelY,
                    labelWidth,
                    percentTexture.Height,
                    RenderZPercentLabel);
            }

            col++;
            if (col >= layout.IconsPerRow)
            {
                col = 0;
                row++;
            }
        }

        UpdateIconTooltip(layout, startX, baseIconY, iconSize, iconGap, iconRowHeight, wrappedRowExtraOffset);
    }

    public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
    {
    }

    public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
    {
    }

    public void OnMouseMoveOnElement(MouseEvent args, int elementIndex)
    {
    }

    public override void Dispose()
    {
        ClearIconTooltip();
        base.Dispose();
        rowBackgroundTexture.Dispose();
        titleTexture.Dispose();
        DisposeIconTextures();
    }

    private bool IsMouseOverIcon(double iconX, double iconY, float iconSize)
    {
        var mouseX = clientApi.Input.MouseX;
        var mouseY = clientApi.Input.MouseY;
        return mouseX >= iconX
            && mouseX <= iconX + iconSize
            && mouseY >= iconY
            && mouseY <= iconY + iconSize;
    }

    private void UpdateIconTooltip(
        LayoutInfo layout,
        double startX,
        double baseIconY,
        float iconSize,
        double iconGap,
        double iconRowHeight,
        double wrappedRowExtraOffset)
    {
        GeneratorIconEntry? hovered = null;
        var iconStep = iconSize + iconGap;
        var col = 0;
        var row = 0;

        foreach (var icon in icons)
        {
            var iconX = startX + col * iconStep;
            var iconY = baseIconY + row * iconRowHeight + row * wrappedRowExtraOffset;
            if (IsMouseOverIcon(iconX, iconY, iconSize))
            {
                hovered = icon;
                break;
            }

            col++;
            if (col >= layout.IconsPerRow)
            {
                col = 0;
                row++;
            }
        }

        if (hovered == null)
        {
            ClearIconTooltip();
            return;
        }

        hovered.EnsureVariants(clientApi);
        if (hovered.VariantStacks.Length == 0)
        {
            ClearIconTooltip();
            return;
        }

        var variantIndex = Math.Clamp(hovered.CurrentVariantIndex, 0, hovered.VariantStacks.Length - 1);
        var stack = hovered.VariantStacks[variantIndex];
        if (stack is not { Id: not 0, Collectible: not null })
        {
            ClearIconTooltip();
            return;
        }

        if (hovered == tooltipIcon && variantIndex == tooltipVariantIndex)
        {
            return;
        }

        ClearIconTooltip();
        tooltipIcon = hovered;
        tooltipVariantIndex = variantIndex;
        tooltipSlot.Itemstack = stack.Clone();
        clientApi.Input.TriggerOnMouseEnterSlot(tooltipSlot);
    }

    private void ClearIconTooltip()
    {
        if (tooltipIcon == null)
        {
            return;
        }

        clientApi.Input.TriggerOnMouseLeaveSlot(tooltipSlot);
        tooltipIcon = null;
        tooltipVariantIndex = -1;
        tooltipSlot.Itemstack = null;
    }

    private LayoutInfo CalcLayout()
    {
        Bounds.CalcWorldBounds();
        var availableWidth = Math.Max(
            UnscaledIconSize + UnscaledIconGap,
            Bounds.fixedWidth - Bounds.fixedPaddingX * 2);

        var iconsPerRow = Math.Max(
            1,
            (int)((availableWidth + UnscaledIconGap) / (UnscaledIconSize + UnscaledIconGap)));

        var iconRows = icons.Count == 0
            ? 1
            : (int)Math.Ceiling(icons.Count / (double)iconsPerRow);

        var wrappedRowGaps = iconRows > 1 ? (iconRows - 1) * UnscaledWrappedRowExtraOffset : 0;
        var unscaledHeight = UnscaledTitleArea
            + UnscaledTitleToIconsGap
            + iconRows * UnscaledIconRowHeight
            + wrappedRowGaps
            + UnscaledBottomPad;

        return new LayoutInfo(iconsPerRow, iconRows, unscaledHeight);
    }

    private void DisposeIconTextures()
    {
        foreach (var icon in icons)
        {
            icon.PercentTexture?.Dispose();
            icon.PercentTexture = null;
        }
    }

    private readonly struct LayoutInfo(int iconsPerRow, int iconRows, double unscaledCellHeight)
    {
        public int IconsPerRow { get; } = iconsPerRow;
        public int IconRows { get; } = iconRows;
        public double UnscaledCellHeight { get; } = unscaledCellHeight;
    }

    private sealed class GeneratorIconEntry
    {
        public string BlockCode = "";
        public string DisplayBlockCode = "";
        public ItemStack?[] VariantStacks = [];
        public int CurrentVariantIndex;
        public long LastSwitchMs;
        public string PercentLabel = "";
        public LoadedTexture? PercentTexture;

        public void EnsureVariants(ICoreClientAPI api)
        {
            if (VariantStacks.Length > 0)
            {
                return;
            }

            VariantStacks = IslandGeneratorBlockResolver.ResolveVariantStacks(
                    api,
                    BlockCode,
                    DisplayBlockCode)
                .Where(static stack => stack is { Id: not 0, Collectible: not null })
                .ToArray();
        }
    }
}