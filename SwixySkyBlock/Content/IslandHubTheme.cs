using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace SwixySkyBlock.Content;

/// <summary>Shared SkyBlock hub palette and Cairo panel helpers.</summary>
internal static class IslandHubTheme
{
    public const int DialogFramePadding = 8;
    /// <summary>Компенсация ширины кнопки закрытия в title bar (визуальный перекос вправо).</summary>
    public const int DialogTitleBarCloseInset = 8;
    /// <summary>Доп. левый отступ контента (тонкая подстройка).</summary>
    public const int ContentAreaLeftTune = 8;
    /// <summary>Горизонтальный отступ контента от левого края области диалога.</summary>
    public const int ContentAreaX = DialogFramePadding + DialogTitleBarCloseInset + ContentAreaLeftTune;
    /// <summary>Правый отступ контента (зазор под кнопку закрытия title bar).</summary>
    public const int ContentAreaRight = DialogFramePadding + DialogTitleBarCloseInset;
    public const int ContentPanelWidth = 728;
    public const int ContentPanelHeight = IslandColumnHeight;
    public const int ContentBottomPadding = 8;
    public const int TabBarY = 32;
    public const int TabBarHeight = 38;
    public const int TabBarPadding = 5;
    public const int TabGap = 6;
    public const int TabCount = 5;
    public const int ContentAreaGapBelowTabs = 6;
    public const int ContentAreaY = TabBarY + TabBarHeight + ContentAreaGapBelowTabs;
    public const int TabBarX = ContentAreaX;
    public const int TabBarWidth = ContentPanelWidth - DialogTitleBarCloseInset;
    public const int DialogContentWidth = ContentAreaX + ContentPanelWidth + ContentAreaRight;
    public const int DialogContentHeight = ContentAreaY + ContentPanelHeight + ContentBottomPadding;

    public const int AccessHeaderTopInset = 10;
    public const int AccessHeaderBottomInset = 16;
    public const int AccessHeaderSideInset = 8;
    public const int AccessHeaderCellHeight = 64;
    public const int AccessHeaderHeight =
        AccessHeaderTopInset + AccessHeaderCellHeight + AccessHeaderBottomInset;
    /// <summary>Зазор между треком списка и полосой прокрутки (ElementStdBounds.VerticalScrollbar).</summary>
    public const int AccessScrollbarGap = 3;
    /// <summary>Ширина полосы прокрутки (GuiElementScrollbar.DefaultScrollbarWidth).</summary>
    public const int AccessScrollbarWidth = 20;
    /// <summary>Горизонтальный padding полосы прокрутки (GuiElementScrollbar.DeafultScrollbarPadding × 2).</summary>
    public const int AccessScrollbarOuterPad = 4;
    /// <summary>Доп. сдвиг трека вправо для выравнивания скролла с кнопками.</summary>
    public const int AccessScrollbarAlignShift = 5;
    /// <summary>Трек списка с внутренним отступом (вкладка «Доступ»): скролл внутри блока деталей.</summary>
    public const int PanelListTrackWidth =
        AccessDetailsWidth - AccessScrollbarGap - AccessScrollbarWidth + AccessScrollbarAlignShift;
    public const int AccessMemberListTrackWidth = PanelListTrackWidth;
    public const int PanelListTrackX = AccessDetailsX;
    /// <summary>Трек списка на всю ширину панели (генератор, топ): края совпадают с шапкой/подвалом.</summary>
    public const int PanelFullListTrackX = ContentAreaX;
    public const int PanelFullListTrackWidth =
        ContentPanelWidth - AccessScrollbarGap - AccessScrollbarWidth - AccessScrollbarOuterPad;
    public const int AccessSectionGap = 10;
    public const int AccessDetailsPanelY = ContentAreaY + AccessHeaderHeight + AccessSectionGap;
    public const int AccessDetailsPanelHeight = ContentPanelHeight - AccessHeaderHeight - AccessSectionGap;
    public const int AccessDetailsInnerPad = 18;
    public const int AccessDetailsX = ContentAreaX + AccessDetailsInnerPad;
    public const int AccessDetailsWidth = ContentPanelWidth - AccessDetailsInnerPad * 2;

    public const int IslandLeftPanelWidth = 344;
    public const int IslandPanelGap = 14;
    public const int IslandRightPanelWidth = ContentPanelWidth - IslandLeftPanelWidth - IslandPanelGap;
    public const int IslandActionPanelGap = 14;
    public const int IslandActionPanelHeight = 287;
    public const int IslandTemplateHeaderOffset = 8;
    public const int IslandTemplateHeaderHeight = 30;
    public const int IslandTemplateListOffset = 44;
    public const int IslandTemplateListBottomInset = 14;
    public const int IslandTemplateListInsetX = 16;
    public const int IslandTemplateListTrackWidth =
        IslandRightPanelWidth - IslandTemplateListInsetX - AccessScrollbarGap - AccessScrollbarWidth - AccessScrollbarOuterPad;
    public const int IslandColumnHeight = IslandActionPanelHeight * 2 + IslandActionPanelGap;
    public const int IslandTemplateListHeight =
        IslandColumnHeight - IslandTemplateListOffset - IslandTemplateListBottomInset;

    public static ElementBounds TabBarBounds =>
        ElementBounds.Fixed(TabBarX, TabBarY, TabBarWidth, TabBarHeight);

    public static ElementBounds TabButtonBounds(int index)
    {
        var innerWidth = TabBarWidth - TabBarPadding * 2;
        var totalGap = TabGap * (TabCount - 1);
        var tabWidth = (innerWidth - totalGap) / TabCount;
        var x = TabBarX + TabBarPadding + index * (tabWidth + TabGap);
        var y = TabBarY + TabBarPadding;
        var height = TabBarHeight - TabBarPadding * 2;
        return ElementBounds.Fixed(x, y, tabWidth, height);
    }
    public const double DialogR = 0.028;
    public const double DialogG = 0.04;
    public const double DialogB = 0.068;

    public const double PanelR = 0.1;
    public const double PanelG = 0.13;
    public const double PanelB = 0.2;

    public const double ScrollR = 0.018;
    public const double ScrollG = 0.028;
    public const double ScrollB = 0.048;

    public const double AccentGoldR = 1.0;
    public const double AccentGoldG = 0.86;
    public const double AccentGoldB = 0.28;

    public const double AccentSkyR = 0.35;
    public const double AccentSkyG = 0.62;
    public const double AccentSkyB = 0.92;

    public static CairoFont CreateSectionTitleFont()
    {
        var font = CairoFont.WhiteSmallText();
        font.Color = [(float)AccentGoldR, (float)AccentGoldG, (float)AccentGoldB, 1f];
        return font;
    }

    public static CairoFont CreateMutedTitleFont()
    {
        var font = CairoFont.WhiteSmallText();
        font.Color = [0.72f, 0.78f, 0.88f, 1f];
        return font;
    }

    public static CairoFont CreateButtonFont(IslandHubButtonKind kind, IslandHubButtonVisual visual)
    {
        var font = kind == IslandHubButtonKind.Tab
            ? CairoFont.WhiteSmallText()
            : CairoFont.ButtonText().WithFontSize(12);

        switch (visual)
        {
            case IslandHubButtonVisual.Active:
                font.Color = [(float)AccentGoldR, (float)AccentGoldG, (float)AccentGoldB, 1f];
                break;
            case IslandHubButtonVisual.Disabled:
                font.Color = [0.55f, 0.6f, 0.68f, 0.85f];
                break;
            case IslandHubButtonVisual.Hover:
                font.Color = [0.95f, 0.97f, 1f, 1f];
                break;
            default:
                font.Color = [0.86f, 0.9f, 0.96f, 1f];
                break;
        }

        return font;
    }

    public static void DrawDialogBackground(Context ctx, double width, double height)
    {
        ctx.Rectangle(0, 0, width, height);
        ctx.SetSourceRGBA(DialogR, DialogG, DialogB, 0.99);
        ctx.Fill();

        var gradient = new LinearGradient(0, 0, 0, height);
        gradient.AddColorStop(0, new Color(AccentSkyR, AccentSkyG, AccentSkyB, 0.14));
        gradient.AddColorStop(0.4, new Color(PanelR, PanelG, PanelB, 0.03));
        gradient.AddColorStop(1, new Color(0.01, 0.012, 0.025, 0.28));
        ctx.SetSource(gradient);
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();

        ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, 0.45);
        ctx.Rectangle(0, 0, width, 2);
        ctx.Fill();

        ctx.SetSourceRGBA(AccentGoldR, AccentGoldG, AccentGoldB, 0.32);
        ctx.Rectangle(0, height - 1.5, width, 1.5);
        ctx.Fill();
    }

    public static void DrawTabBar(Context ctx, double width, double height)
    {
        ctx.Rectangle(0, 0, width, height);
        ctx.SetSourceRGBA(ScrollR, ScrollG, ScrollB, 0.95);
        ctx.Fill();

        ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, 0.22);
        ctx.Rectangle(0, height - 1.5, width, 1.5);
        ctx.Fill();
    }

    public static void DrawPanel(Context ctx, double width, double height, double radius = 6)
    {
        DrawRoundedRect(ctx, 0, 0, width, height, radius);
        ctx.SetSourceRGBA(PanelR, PanelG, PanelB, 0.98);
        ctx.FillPreserve();

        ctx.SetSourceRGBA(0.01, 0.02, 0.04, 0.9);
        ctx.LineWidth = 2.5;
        ctx.Stroke();

        DrawRoundedRect(ctx, 1.5, 1.5, width - 3, height - 3, Math.Max(1, radius - 1));
        ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, 0.42);
        ctx.LineWidth = 1.4;
        ctx.Stroke();

        DrawRoundedRect(ctx, 2.5, 2.5, width - 5, height - 5, Math.Max(1, radius - 2));
        ctx.SetSourceRGBA(AccentGoldR, AccentGoldG, AccentGoldB, 0.12);
        ctx.LineWidth = 1;
        ctx.Stroke();
    }

    public static void DrawSectionHeaderStrip(Context ctx, double width, double height)
    {
        ctx.Rectangle(0, 0, width, height);
        ctx.SetSourceRGBA(PanelR * 0.82, PanelG * 0.82, PanelB * 0.82, 0.96);
        ctx.Fill();

        ctx.SetSourceRGBA(AccentGoldR, AccentGoldG, AccentGoldB, 0.45);
        ctx.Rectangle(0, height - 1.5, width, 1.5);
        ctx.Fill();
    }

    public static void DrawScrollArea(Context ctx, double width, double height)
    {
        ctx.Rectangle(0, 0, width, height);
        ctx.SetSourceRGBA(ScrollR, ScrollG, ScrollB, 0.99);
        ctx.FillPreserve();

        ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, 0.35);
        ctx.LineWidth = 1.2;
        ctx.Stroke();

        ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, 0.12);
        ctx.Rectangle(0, 0, width, 2);
        ctx.Fill();
    }

    public static void DrawTextInput(Context ctx, double width, double height)
    {
        DrawRoundedRect(ctx, 0, 0, width, height, 4);
        ctx.SetSourceRGBA(ScrollR * 1.2, ScrollG * 1.2, ScrollB * 1.2, 0.99);
        ctx.FillPreserve();

        ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, 0.65);
        ctx.LineWidth = 1.5;
        ctx.Stroke();

        DrawRoundedRect(ctx, 1, 1, width - 2, height - 2, 3);
        ctx.SetSourceRGBA(AccentGoldR, AccentGoldG, AccentGoldB, 0.1);
        ctx.LineWidth = 1;
        ctx.Stroke();
    }

    public static void DrawButton(
        Context ctx,
        double width,
        double height,
        IslandHubButtonKind kind,
        IslandHubButtonVisual visual)
    {
        var radius = kind == IslandHubButtonKind.Tab ? 5 : 4;
        GetButtonFill(kind, visual, out var fillR, out var fillG, out var fillB, out var fillA);
        GetButtonBorder(visual, out var borderR, out var borderG, out var borderB, out var borderA, out var borderWidth);

        DrawRoundedRect(ctx, 0, 0, width, height, radius);
        ctx.SetSourceRGBA(fillR, fillG, fillB, fillA);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(borderR, borderG, borderB, borderA);
        ctx.LineWidth = borderWidth;
        ctx.Stroke();

        if (visual == IslandHubButtonVisual.Hover)
        {
            DrawRoundedRect(ctx, 2, 2, width - 4, height - 4, Math.Max(1, radius - 1));
            ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, 0.22);
            ctx.Fill();
        }

        if (visual == IslandHubButtonVisual.Active)
        {
            ctx.SetSourceRGBA(AccentGoldR, AccentGoldG, AccentGoldB, 0.75);
            ctx.Rectangle(1, 1, width - 2, 2.5);
            ctx.Fill();
        }

        if (visual == IslandHubButtonVisual.Pressed)
        {
            DrawRoundedRect(ctx, 0, 0, width, height, radius);
            ctx.SetSourceRGBA(0, 0, 0, 0.16);
            ctx.Fill();
        }
    }

    public const double ActionCardHoverInset = 5;

    public static void DrawActionCard(
        Context ctx,
        double width,
        double height,
        double radius,
        bool hover,
        bool pressed,
        double alpha = 1)
    {
        DrawRoundedRect(ctx, 0, 0, width, height, radius);
        ctx.SetSourceRGBA(PanelR * 0.92, PanelG * 0.92, PanelB * 0.92, 0.98 * alpha);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, 0.38 * alpha);
        ctx.LineWidth = 1.6;
        ctx.Stroke();

        DrawRoundedRect(ctx, 1.5, 1.5, width - 3, height - 3, Math.Max(1, radius - 1));
        ctx.SetSourceRGBA(AccentGoldR, AccentGoldG, AccentGoldB, (hover ? 0.16 : 0.08) * alpha);
        ctx.LineWidth = 1;
        ctx.Stroke();

        if (pressed)
        {
            DrawRoundedRect(ctx, 0, 0, width, height, radius);
            ctx.SetSourceRGBA(0, 0, 0, 0.12 * alpha);
            ctx.Fill();
        }
    }

    public static void DrawActionCardHoverOverlay(Context ctx, double width, double height, double radius, double alpha = 1)
    {
        var inset = ActionCardHoverInset;
        var innerW = width - inset * 2;
        var innerH = height - inset * 2;
        if (innerW <= 0 || innerH <= 0)
        {
            return;
        }

        var innerRadius = Math.Max(1, radius - 2);
        DrawRoundedRect(ctx, inset, inset, innerW, innerH, innerRadius);
        ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, 0.24 * alpha);
        ctx.Fill();

        DrawRoundedRect(ctx, inset, inset, innerW, innerH, innerRadius);
        ctx.SetSourceRGBA(AccentGoldR, AccentGoldG, AccentGoldB, 0.18 * alpha);
        ctx.LineWidth = 1;
        ctx.Stroke();
    }

    public static void DrawListRow(Context ctx, double width, double height, bool accent = true)
    {
        DrawRoundedRect(ctx, 0, 0, width, height, 4);
        ctx.SetSourceRGBA(PanelR * 0.88, PanelG * 0.88, PanelB * 0.88, 0.96);
        ctx.FillPreserve();

        ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, accent ? 0.28 : 0.18);
        ctx.LineWidth = 1.2;
        ctx.Stroke();

        if (!accent)
        {
            return;
        }

        DrawRoundedRect(ctx, 1, 1, width - 2, height - 2, 3);
        ctx.SetSourceRGBA(AccentGoldR, AccentGoldG, AccentGoldB, 0.1);
        ctx.LineWidth = 1;
        ctx.Stroke();
    }

    public static void DrawListRowInset(Context ctx, double width, double height, double inset = 1, bool accent = true)
    {
        if (width <= inset * 2 || height <= inset * 2)
        {
            DrawListRow(ctx, width, height, accent);
            return;
        }

        ctx.Save();
        ctx.Translate(inset, inset);
        DrawListRow(ctx, width - inset * 2, height - inset * 2, accent);
        ctx.Restore();
    }

    public static void DrawTemplateCard(Context ctx, double width, double height)
    {
        DrawRoundedRect(ctx, 0, 0, width, height, 8);
        ctx.SetSourceRGBA(PanelR * 0.9, PanelG * 0.9, PanelB * 0.9, 0.98);
        ctx.FillPreserve();

        ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, 0.42);
        ctx.LineWidth = 1.5;
        ctx.Stroke();

        DrawRoundedRect(ctx, 2, 2, width - 4, height - 4, 7);
        ctx.SetSourceRGBA(AccentGoldR, AccentGoldG, AccentGoldB, 0.1);
        ctx.LineWidth = 1;
        ctx.Stroke();
    }

    public static void ApplyListButtonFill(Context ctx, double width, double height, bool pressed, double alpha = 1)
    {
        RoundRectangle(ctx, 0, 0, width, height, 3);
        ctx.SetSourceRGBA(PanelR * 0.85, PanelG * 0.85, PanelB * 0.85, 0.98 * alpha);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(AccentSkyR, AccentSkyG, AccentSkyB, (pressed ? 0.55 : 0.38) * alpha);
        ctx.LineWidth = 1.5;
        ctx.Stroke();
    }

    public static void DrawRoundedRect(Context ctx, double x, double y, double w, double h, double r)
    {
        r = Math.Min(r, Math.Min(w, h) * 0.5);
        ctx.NewPath();
        ctx.Arc(x + w - r, y + r, r, -Math.PI / 2, 0);
        ctx.Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
        ctx.Arc(x + r, y + h - r, r, Math.PI / 2, Math.PI);
        ctx.Arc(x + r, y + r, r, Math.PI, Math.PI * 1.5);
        ctx.ClosePath();
    }

    private static void RoundRectangle(Context ctx, double x, double y, double w, double h, double r)
    {
        DrawRoundedRect(ctx, x, y, w, h, r);
    }

    private static void GetButtonFill(
        IslandHubButtonKind kind,
        IslandHubButtonVisual visual,
        out double r,
        out double g,
        out double b,
        out double a)
    {
        if (visual == IslandHubButtonVisual.Disabled)
        {
            r = PanelR * 0.55;
            g = PanelG * 0.55;
            b = PanelB * 0.55;
            a = 0.75;
            return;
        }

        if (visual == IslandHubButtonVisual.Active)
        {
            r = PanelR * 1.05;
            g = PanelG * 1.05;
            b = PanelB * 1.05;
            a = 0.98;
            return;
        }

        if (kind == IslandHubButtonKind.Tab)
        {
            r = ScrollR * 1.8;
            g = ScrollG * 1.8;
            b = ScrollB * 1.8;
            a = 0.96;
            return;
        }

        r = PanelR * 0.78;
        g = PanelG * 0.78;
        b = PanelB * 0.78;
        a = 0.98;
    }

    private static void GetButtonBorder(
        IslandHubButtonVisual visual,
        out double r,
        out double g,
        out double b,
        out double a,
        out double width)
    {
        if (visual == IslandHubButtonVisual.Disabled)
        {
            r = 0.25;
            g = 0.3;
            b = 0.38;
            a = 0.45;
            width = 1.2;
            return;
        }

        if (visual == IslandHubButtonVisual.Active)
        {
            r = AccentGoldR;
            g = AccentGoldG;
            b = AccentGoldB;
            a = 0.65;
            width = 1.6;
            return;
        }

        r = AccentSkyR;
        g = AccentSkyG;
        b = AccentSkyB;
        a = visual == IslandHubButtonVisual.Hover ? 0.75 : 0.5;
        width = 1.4;
    }
}