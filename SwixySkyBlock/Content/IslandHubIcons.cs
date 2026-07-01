// =============================================================================
// IslandHubIcons.cs — Cairo icons for the island hub tab and template picker.
// =============================================================================

using System;
using Cairo;
using Vintagestory.API.Client;

namespace SwixySkyBlock.Content;

internal static class IslandHubIcons
{
    public static void DrawActionCard(Context ctx, double width, double height, Action<Context, double, double, double> drawIcon, bool enabled = true)
    {
        var alpha = enabled ? 1.0 : 0.45;

        IslandHubTheme.DrawActionCard(ctx, width, height, 6, hover: false, pressed: false, alpha);

        var iconSize = Math.Min(width, height) * 0.46;
        var iconX = (width - iconSize) / 2;
        var iconY = (height - iconSize) / 2 - 10;
        drawIcon(ctx, iconX, iconY, iconSize * alpha);
    }

    public static void DrawTemplateCard(Context ctx, double width, double height, string templateName)
    {
        IslandHubTheme.DrawTemplateCard(ctx, width, height);

        var iconSize = Math.Min(width - 24, height - 36);
        var iconX = (width - iconSize) / 2;
        var iconY = 10;
        DrawTemplateIcon(ctx, templateName, iconX, iconY, iconSize);
    }

    public static void DrawCreateIsland(Context ctx, double x, double y, double size)
    {
        DrawFloatingShadow(ctx, x, y, size);

        var islandW = size * 0.78;
        var islandH = size * 0.28;
        var islandX = x + (size - islandW) / 2;
        var islandY = y + size * 0.46;

        DrawRoundedRect(ctx, islandX, islandY + islandH * 0.35, islandW, islandH * 0.65, size * 0.04);
        ctx.SetSourceRGBA(0.42, 0.28, 0.14, 0.95);
        ctx.Fill();

        DrawRoundedRect(ctx, islandX, islandY, islandW, islandH * 0.55, size * 0.05);
        ctx.SetSourceRGBA(0.28, 0.62, 0.24, 0.98);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.12, 0.28, 0.1, 0.75);
        ctx.LineWidth = Math.Max(1, size * 0.03);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.45, 0.78, 0.34, 0.55);
        ctx.Arc(islandX + islandW * 0.28, islandY + islandH * 0.18, size * 0.05, 0, Math.PI * 2);
        ctx.Arc(islandX + islandW * 0.62, islandY + islandH * 0.22, size * 0.04, 0, Math.PI * 2);
        ctx.Fill();

        var cx = x + size * 0.5;
        var cy = y + size * 0.22;
        DrawGlow(ctx, cx, cy, size * 0.22, 0.35, 0.82, 0.42);

        ctx.SetSourceRGBA(0.95, 0.98, 1, 0.95);
        ctx.LineWidth = Math.Max(2.2, size * 0.1);
        ctx.LineCap = LineCap.Round;
        var arm = size * 0.14;
        ctx.MoveTo(cx - arm, cy);
        ctx.LineTo(cx + arm, cy);
        ctx.Stroke();
        ctx.MoveTo(cx, cy - arm);
        ctx.LineTo(cx, cy + arm);
        ctx.Stroke();
    }

    public static void DrawGoHome(Context ctx, double x, double y, double size)
    {
        var cx = x + size * 0.5;
        var baseY = y + size * 0.78;
        var houseW = size * 0.58;
        var houseH = size * 0.36;
        var houseX = cx - houseW / 2;
        var houseY = y + size * 0.39;
        var roofPeakY = y + size * 0.18;

        DrawGlow(ctx, cx, houseY + houseH * 0.34, size * 0.42, 1, 0.68, 0.26);

        ctx.Save();
        ctx.Translate(cx, baseY);
        ctx.Scale(size * 0.36, size * 0.075);
        ctx.Arc(0, 0, 1, 0, Math.PI * 2);
        ctx.SetSourceRGBA(0, 0, 0, 0.26);
        ctx.Fill();
        ctx.Restore();

        DrawRoundedRect(ctx, x + size * 0.16, y + size * 0.72, size * 0.68, size * 0.11, size * 0.035);
        ctx.SetSourceRGBA(0.33, 0.62, 0.2, 0.98);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.1, 0.22, 0.08, 0.72);
        ctx.LineWidth = Math.Max(1, size * 0.018);
        ctx.Stroke();

        ctx.NewPath();
        ctx.MoveTo(cx - size * 0.09, y + size * 0.72);
        ctx.LineTo(cx + size * 0.09, y + size * 0.72);
        ctx.LineTo(cx + size * 0.05, y + size * 0.83);
        ctx.LineTo(cx - size * 0.05, y + size * 0.83);
        ctx.ClosePath();
        ctx.SetSourceRGBA(0.62, 0.5, 0.34, 0.72);
        ctx.Fill();

        DrawRoundedRect(ctx, houseX + houseW * 0.08, houseY + houseH * 0.18, houseW * 0.84, houseH * 0.82, size * 0.035);
        ctx.SetSourceRGBA(0.82, 0.63, 0.38, 0.98);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.28, 0.16, 0.08, 0.82);
        ctx.LineWidth = Math.Max(1, size * 0.022);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.98, 0.82, 0.52, 0.22);
        ctx.Rectangle(houseX + houseW * 0.16, houseY + houseH * 0.25, houseW * 0.68, houseH * 0.18);
        ctx.Fill();

        DrawRoundedRect(ctx, houseX + houseW * 0.68, houseY - houseH * 0.05, houseW * 0.1, houseH * 0.24, size * 0.018);
        ctx.SetSourceRGBA(0.45, 0.22, 0.15, 0.98);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.16, 0.08, 0.06, 0.72);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.86, 0.9, 0.88, 0.35);
        ctx.Arc(houseX + houseW * 0.78, houseY - houseH * 0.12, size * 0.025, 0, Math.PI * 2);
        ctx.Arc(houseX + houseW * 0.84, houseY - houseH * 0.2, size * 0.018, 0, Math.PI * 2);
        ctx.Fill();

        ctx.NewPath();
        ctx.MoveTo(houseX - houseW * 0.08, houseY + houseH * 0.28);
        ctx.LineTo(cx, roofPeakY);
        ctx.LineTo(houseX + houseW * 1.08, houseY + houseH * 0.28);
        ctx.LineTo(houseX + houseW * 0.95, houseY + houseH * 0.38);
        ctx.LineTo(cx, roofPeakY + houseH * 0.13);
        ctx.LineTo(houseX + houseW * 0.05, houseY + houseH * 0.38);
        ctx.ClosePath();
        ctx.SetSourceRGBA(0.66, 0.19, 0.15, 0.98);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.2, 0.05, 0.04, 0.85);
        ctx.LineWidth = Math.Max(1, size * 0.025);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.92, 0.38, 0.25, 0.8);
        ctx.LineWidth = Math.Max(1, size * 0.018);
        for (var i = 0; i < 4; i++)
        {
            var t = 0.22 + i * 0.16;
            ctx.MoveTo(houseX + houseW * t, houseY + houseH * (0.33 - i * 0.015));
            ctx.LineTo(houseX + houseW * (t + 0.16), houseY + houseH * (0.23 - i * 0.015));
            ctx.Stroke();
        }

        DrawRoundedRect(ctx, houseX + houseW * 0.39, houseY + houseH * 0.5, houseW * 0.22, houseH * 0.5, size * 0.025);
        ctx.SetSourceRGBA(0.36, 0.18, 0.09, 0.98);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.12, 0.06, 0.03, 0.82);
        ctx.Stroke();
        ctx.SetSourceRGBA(1, 0.76, 0.3, 0.95);
        ctx.Arc(houseX + houseW * 0.56, houseY + houseH * 0.73, size * 0.012, 0, Math.PI * 2);
        ctx.Fill();

        DrawHomeWindow(ctx, houseX + houseW * 0.17, houseY + houseH * 0.48, size * 0.13);
        DrawHomeWindow(ctx, houseX + houseW * 0.7, houseY + houseH * 0.48, size * 0.13);
    }

    public static void DrawGoHomeOutline(Context ctx, double x, double y, double size)
    {
        var cx = x + size * 0.5;
        var baseY = y + size * 0.78;
        var houseW = size * 0.58;
        var houseH = size * 0.36;
        var houseX = cx - houseW / 2;
        var houseY = y + size * 0.39;
        var roofPeakY = y + size * 0.18;

        ctx.Save();
        ctx.Translate(cx, baseY);
        ctx.Scale(size * 0.36, size * 0.075);
        ctx.Arc(0, 0, 1, 0, Math.PI * 2);
        ctx.SetSourceRGBA(0, 0, 0, 0.22);
        ctx.Fill();
        ctx.Restore();

        DrawRoundedRect(ctx, x + size * 0.16, y + size * 0.72, size * 0.68, size * 0.11, size * 0.035);
        ctx.SetSourceRGBA(0.34, 0.36, 0.37, 0.86);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.12, 0.13, 0.14, 0.62);
        ctx.LineWidth = Math.Max(1, size * 0.018);
        ctx.Stroke();

        ctx.NewPath();
        ctx.MoveTo(cx - size * 0.09, y + size * 0.72);
        ctx.LineTo(cx + size * 0.09, y + size * 0.72);
        ctx.LineTo(cx + size * 0.05, y + size * 0.83);
        ctx.LineTo(cx - size * 0.05, y + size * 0.83);
        ctx.ClosePath();
        ctx.SetSourceRGBA(0.48, 0.49, 0.5, 0.58);
        ctx.Fill();

        DrawRoundedRect(ctx, houseX + houseW * 0.08, houseY + houseH * 0.18, houseW * 0.84, houseH * 0.82, size * 0.035);
        ctx.SetSourceRGBA(0.57, 0.58, 0.59, 0.88);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.2, 0.21, 0.22, 0.72);
        ctx.LineWidth = Math.Max(1, size * 0.022);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.8, 0.82, 0.84, 0.16);
        ctx.Rectangle(houseX + houseW * 0.16, houseY + houseH * 0.25, houseW * 0.68, houseH * 0.18);
        ctx.Fill();

        DrawRoundedRect(ctx, houseX + houseW * 0.68, houseY - houseH * 0.05, houseW * 0.1, houseH * 0.24, size * 0.018);
        ctx.SetSourceRGBA(0.42, 0.43, 0.44, 0.84);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.16, 0.17, 0.18, 0.68);
        ctx.Stroke();

        ctx.NewPath();
        ctx.MoveTo(houseX - houseW * 0.08, houseY + houseH * 0.28);
        ctx.LineTo(cx, roofPeakY);
        ctx.LineTo(houseX + houseW * 1.08, houseY + houseH * 0.28);
        ctx.LineTo(houseX + houseW * 0.95, houseY + houseH * 0.38);
        ctx.LineTo(cx, roofPeakY + houseH * 0.13);
        ctx.LineTo(houseX + houseW * 0.05, houseY + houseH * 0.38);
        ctx.ClosePath();
        ctx.SetSourceRGBA(0.38, 0.39, 0.4, 0.92);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.12, 0.13, 0.14, 0.76);
        ctx.LineWidth = Math.Max(1, size * 0.025);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.7, 0.72, 0.74, 0.42);
        ctx.LineWidth = Math.Max(1, size * 0.018);
        for (var i = 0; i < 4; i++)
        {
            var t = 0.22 + i * 0.16;
            ctx.MoveTo(houseX + houseW * t, houseY + houseH * (0.33 - i * 0.015));
            ctx.LineTo(houseX + houseW * (t + 0.16), houseY + houseH * (0.23 - i * 0.015));
            ctx.Stroke();
        }

        DrawRoundedRect(ctx, houseX + houseW * 0.39, houseY + houseH * 0.5, houseW * 0.22, houseH * 0.5, size * 0.025);
        ctx.SetSourceRGBA(0.29, 0.3, 0.31, 0.9);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.12, 0.13, 0.14, 0.7);
        ctx.Stroke();

        DrawHomeWindowMuted(ctx, houseX + houseW * 0.17, houseY + houseH * 0.48, size * 0.13);
        DrawHomeWindowMuted(ctx, houseX + houseW * 0.7, houseY + houseH * 0.48, size * 0.13);
    }

    public static void DrawGoSpawn(Context ctx, double x, double y, double size)
    {
        var cx = x + size * 0.5;
        var baseY = y + size * 0.72;
        var pillarW = size * 0.16;
        var pillarH = size * 0.34;

        DrawGlow(ctx, cx, y + size * 0.28, size * 0.42, 0.2, 0.72, 0.95);

        for (var i = 0; i < 6; i++)
        {
            var angle = Math.PI * 2 * i / 6 - Math.PI / 2;
            ctx.SetSourceRGBA(0.35, 0.82, 0.95, 0.22);
            ctx.LineWidth = Math.Max(1, size * 0.03);
            ctx.MoveTo(cx, y + size * 0.3);
            ctx.LineTo(cx + Math.Cos(angle) * size * 0.28, y + size * 0.3 + Math.Sin(angle) * size * 0.16);
            ctx.Stroke();
        }

        DrawRoundedRect(ctx, cx - pillarW / 2, baseY - pillarH, pillarW, pillarH, size * 0.03);
        ctx.SetSourceRGBA(0.55, 0.58, 0.64, 0.96);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.12, 0.14, 0.16, 0.8);
        ctx.LineWidth = Math.Max(1, size * 0.03);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.45, 0.88, 1, 0.95);
        ctx.Arc(cx, baseY - pillarH - size * 0.05, size * 0.1, 0, Math.PI * 2);
        ctx.Fill();

        ctx.SetSourceRGBA(0.95, 0.98, 1, 0.9);
        ctx.Arc(cx, y + size * 0.24, size * 0.07, 0, Math.PI * 2);
        ctx.Fill();

        DrawCompassRing(ctx, cx, y + size * 0.52, size * 0.22);
    }

    public static void DrawTemplateIcon(Context ctx, string templateName, double x, double y, double size)
    {
        switch (templateName.ToLowerInvariant())
        {
            case "starter":
                DrawStarterTemplate(ctx, x, y, size);
                break;
            case "classic":
                DrawClassicTemplate(ctx, x, y, size);
                break;
            default:
                DrawGenericTemplate(ctx, x, y, size);
                break;
        }
    }

    private static void DrawStarterTemplate(Context ctx, double x, double y, double size)
    {
        DrawFloatingShadow(ctx, x, y, size);
        DrawGrassIsland(ctx, x + size * 0.14, y + size * 0.52, size * 0.72, size * 0.22, lush: true);

        var trunkX = x + size * 0.47;
        var trunkTop = y + size * 0.34;
        ctx.SetSourceRGBA(0.42, 0.26, 0.12, 0.95);
        DrawRoundedRect(ctx, trunkX, trunkTop, size * 0.06, size * 0.2, size * 0.015);
        ctx.Fill();

        ctx.SetSourceRGBA(0.18, 0.55, 0.2, 0.95);
        ctx.Arc(trunkX + size * 0.03, trunkTop - size * 0.02, size * 0.14, 0, Math.PI * 2);
        ctx.Fill();
        ctx.SetSourceRGBA(0.28, 0.68, 0.28, 0.9);
        ctx.Arc(trunkX + size * 0.03, trunkTop - size * 0.06, size * 0.1, 0, Math.PI * 2);
        ctx.Fill();
    }

    private static void DrawClassicTemplate(Context ctx, double x, double y, double size)
    {
        DrawFloatingShadow(ctx, x, y, size);

        var baseX = x + size * 0.12;
        var baseY = y + size * 0.58;
        var baseW = size * 0.76;

        ctx.NewPath();
        ctx.MoveTo(baseX, baseY + size * 0.12);
        ctx.LineTo(baseX + baseW * 0.22, baseY - size * 0.08);
        ctx.LineTo(baseX + baseW * 0.48, baseY + size * 0.04);
        ctx.LineTo(baseX + baseW * 0.72, baseY - size * 0.12);
        ctx.LineTo(baseX + baseW, baseY + size * 0.1);
        ctx.LineTo(baseX + baseW, baseY + size * 0.18);
        ctx.LineTo(baseX, baseY + size * 0.18);
        ctx.ClosePath();
        ctx.SetSourceRGBA(0.48, 0.5, 0.54, 0.96);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.14, 0.15, 0.17, 0.8);
        ctx.LineWidth = Math.Max(1, size * 0.025);
        ctx.Stroke();

        DrawGrassIsland(ctx, baseX + baseW * 0.18, baseY + size * 0.02, baseW * 0.42, size * 0.12, lush: false);

        ctx.SetSourceRGBA(0.62, 0.64, 0.68, 0.9);
        ctx.Arc(baseX + baseW * 0.78, baseY - size * 0.02, size * 0.05, 0, Math.PI * 2);
        ctx.Fill();
    }

    private static void DrawGenericTemplate(Context ctx, double x, double y, double size)
    {
        DrawFloatingShadow(ctx, x, y, size);
        DrawGrassIsland(ctx, x + size * 0.16, y + size * 0.5, size * 0.68, size * 0.24, lush: true);

        ctx.SetSourceRGBA(0.4, 0.72, 0.92, 0.75);
        ctx.Arc(x + size * 0.5, y + size * 0.3, size * 0.08, 0, Math.PI * 2);
        ctx.Fill();
    }

    private static void DrawHomeWindow(Context ctx, double x, double y, double size)
    {
        DrawRoundedRect(ctx, x, y, size, size, size * 0.16);
        ctx.SetSourceRGBA(0.42, 0.76, 0.92, 0.95);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.08, 0.16, 0.2, 0.78);
        ctx.LineWidth = Math.Max(1, size * 0.11);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.95, 0.98, 1, 0.65);
        ctx.Rectangle(x + size * 0.18, y + size * 0.15, size * 0.28, size * 0.22);
        ctx.Fill();

        ctx.SetSourceRGBA(0.1, 0.22, 0.28, 0.72);
        ctx.LineWidth = Math.Max(1, size * 0.08);
        ctx.MoveTo(x + size * 0.5, y + size * 0.08);
        ctx.LineTo(x + size * 0.5, y + size * 0.92);
        ctx.MoveTo(x + size * 0.08, y + size * 0.5);
        ctx.LineTo(x + size * 0.92, y + size * 0.5);
        ctx.Stroke();
    }

    private static void DrawHomeWindowMuted(Context ctx, double x, double y, double size)
    {
        DrawRoundedRect(ctx, x, y, size, size, size * 0.16);
        ctx.SetSourceRGBA(0.54, 0.56, 0.58, 0.82);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.16, 0.17, 0.18, 0.7);
        ctx.LineWidth = Math.Max(1, size * 0.11);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.82, 0.84, 0.86, 0.35);
        ctx.Rectangle(x + size * 0.18, y + size * 0.15, size * 0.28, size * 0.22);
        ctx.Fill();

        ctx.SetSourceRGBA(0.2, 0.21, 0.22, 0.62);
        ctx.LineWidth = Math.Max(1, size * 0.08);
        ctx.MoveTo(x + size * 0.5, y + size * 0.08);
        ctx.LineTo(x + size * 0.5, y + size * 0.92);
        ctx.MoveTo(x + size * 0.08, y + size * 0.5);
        ctx.LineTo(x + size * 0.92, y + size * 0.5);
        ctx.Stroke();
    }

    private static void DrawGrassIsland(Context ctx, double x, double y, double w, double h, bool lush)
    {
        DrawRoundedRect(ctx, x, y + h * 0.35, w, h * 0.65, h * 0.12);
        ctx.SetSourceRGBA(0.4, 0.27, 0.14, 0.95);
        ctx.Fill();

        var grassR = lush ? 0.26 : 0.34;
        var grassG = lush ? 0.64 : 0.52;
        var grassB = lush ? 0.22 : 0.28;

        DrawRoundedRect(ctx, x, y, w, h * 0.55, h * 0.14);
        ctx.SetSourceRGBA(grassR, grassG, grassB, 0.98);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.1, 0.22, 0.1, 0.65);
        ctx.LineWidth = Math.Max(1, h * 0.06);
        ctx.Stroke();
    }

    private static void DrawFloatingShadow(Context ctx, double x, double y, double size)
    {
        ctx.Save();
        ctx.Translate(x + size * 0.5, y + size * 0.88);
        ctx.Scale(size * 0.34, size * 0.08);
        ctx.Arc(0, 0, 1, 0, Math.PI * 2);
        ctx.SetSourceRGBA(0, 0, 0, 0.22);
        ctx.Fill();
        ctx.Restore();
    }

    private static void DrawCompassRing(Context ctx, double cx, double cy, double radius)
    {
        ctx.SetSourceRGBA(0.75, 0.82, 0.9, 0.55);
        ctx.LineWidth = Math.Max(1, radius * 0.18);
        ctx.Arc(cx, cy, radius, 0, Math.PI * 2);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.9, 0.35, 0.3, 0.9);
        ctx.MoveTo(cx, cy - radius * 0.75);
        ctx.LineTo(cx, cy + radius * 0.2);
        ctx.LineWidth = Math.Max(1.2, radius * 0.22);
        ctx.Stroke();
    }

    private static void DrawGlow(Context ctx, double cx, double cy, double radius, double r, double g, double b)
    {
        foreach (var (scale, alpha) in new[] { (1.0, 0.08), (0.7, 0.14), (0.45, 0.2) })
        {
            ctx.NewPath();
            ctx.Arc(cx, cy, radius * scale, 0, Math.PI * 2);
            ctx.SetSourceRGBA(r, g, b, alpha);
            ctx.Fill();
        }
    }

    private static void DrawRoundedRect(Context ctx, double x, double y, double w, double h, double r)
    {
        r = Math.Min(r, Math.Min(w, h) * 0.5);
        ctx.NewPath();
        ctx.Arc(x + w - r, y + r, r, -Math.PI / 2, 0);
        ctx.Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
        ctx.Arc(x + r, y + h - r, r, Math.PI / 2, Math.PI);
        ctx.Arc(x + r, y + r, r, Math.PI, Math.PI * 1.5);
        ctx.ClosePath();
    }
}
