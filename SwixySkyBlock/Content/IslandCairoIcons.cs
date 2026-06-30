// =============================================================================
// ClaimCairoIcons.cs
// -----------------------------------------------------------------------------
// Shared Cairo vector icons for claim list and claim member cells.
// =============================================================================

using System;
using Cairo;

namespace SwixySkyBlock.Content;

internal static class IslandCairoIcons
{
    public static void DrawHighlight(Context ctx, double x, double y, double size, bool active)
    {
        var cx = x + size * 0.5;
        var cy = y + size * 0.42;
        var bulbR = size * 0.24;
        var glassAlpha = active ? 0.96 : 0.58;

        if (active)
        {
            DrawGlow(ctx, cx, cy, size * 0.5, 1, 0.82, 0.18);
            DrawLightRays(ctx, cx, cy, size);
        }

        ctx.NewPath();
        ctx.MoveTo(cx - bulbR * 0.72, cy + bulbR * 0.7);
        ctx.CurveTo(cx - bulbR * 1.28, cy + bulbR * 0.22, cx - bulbR * 1.12, cy - bulbR * 0.86, cx, cy - bulbR * 1.03);
        ctx.CurveTo(cx + bulbR * 1.12, cy - bulbR * 0.86, cx + bulbR * 1.28, cy + bulbR * 0.22, cx + bulbR * 0.72, cy + bulbR * 0.7);
        ctx.CurveTo(cx + bulbR * 0.42, cy + bulbR * 0.98, cx - bulbR * 0.42, cy + bulbR * 0.98, cx - bulbR * 0.72, cy + bulbR * 0.7);
        ctx.ClosePath();
        ctx.SetSourceRGBA(active ? 1 : 0.68, active ? 0.89 : 0.7, active ? 0.28 : 0.56, glassAlpha);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(active ? 0.48 : 0.36, active ? 0.34 : 0.36, active ? 0.03 : 0.42, active ? 0.9 : 0.52);
        ctx.LineWidth = Math.Max(1, size * 0.045);
        ctx.Stroke();

        ctx.SetSourceRGBA(1, 1, 0.78, active ? 0.58 : 0.22);
        ctx.Arc(cx - bulbR * 0.35, cy - bulbR * 0.36, bulbR * 0.22, 0, Math.PI * 2);
        ctx.Fill();

        DrawFilament(ctx, cx, cy, bulbR, active);
        DrawLampBase(ctx, cx, cy + bulbR * 0.82, size, active);
    }

    public static void DrawRecreate(Context ctx, double x, double y, double size, bool active = true)
    {
        var cx = x + size * 0.5;
        var cy = y + size * 0.5;
        var radius = size * 0.34;
        var alpha = active ? 0.95 : 0.5;

        ctx.SetSourceRGBA(0.35, 0.62, 0.92, alpha);
        ctx.LineWidth = Math.Max(2, size * 0.1);
        ctx.LineCap = LineCap.Round;

        ctx.Arc(cx, cy, radius, Math.PI * 0.15, Math.PI * 1.85);
        ctx.Stroke();

        var endAngle = Math.PI * 1.85;
        var arrowX = cx + Math.Cos(endAngle) * radius;
        var arrowY = cy + Math.Sin(endAngle) * radius;
        var arrowSize = size * 0.14;

        ctx.MoveTo(arrowX, arrowY);
        ctx.LineTo(arrowX - arrowSize, arrowY - arrowSize * 0.35);
        ctx.LineTo(arrowX + arrowSize * 0.35, arrowY - arrowSize);
        ctx.ClosePath();
        ctx.Fill();
    }

    public static void DrawLeave(Context ctx, double x, double y, double size, bool active = true)
    {
        var alpha = active ? 0.95 : 0.5;
        var frameX = x + size * 0.22;
        var frameY = y + size * 0.18;
        var frameW = size * 0.42;
        var frameH = size * 0.64;

        DrawRoundedRect(ctx, frameX, frameY, frameW, frameH, size * 0.04);
        ctx.SetSourceRGBA(0.42, 0.46, 0.52, alpha);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.1, 0.12, 0.14, active ? 0.8 : 0.35);
        ctx.LineWidth = Math.Max(1, size * 0.035);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.18, 0.2, 0.24, alpha);
        DrawRoundedRect(ctx, frameX + frameW * 0.34, frameY + frameH * 0.34, frameW * 0.32, frameH * 0.42, size * 0.02);
        ctx.Fill();

        ctx.SetSourceRGBA(0.92, 0.55, 0.22, alpha);
        ctx.LineWidth = Math.Max(2, size * 0.08);
        ctx.LineCap = LineCap.Round;
        ctx.MoveTo(x + size * 0.72, y + size * 0.5);
        ctx.LineTo(x + size * 0.44, y + size * 0.5);
        ctx.Stroke();

        ctx.MoveTo(x + size * 0.52, y + size * 0.38);
        ctx.LineTo(x + size * 0.44, y + size * 0.5);
        ctx.LineTo(x + size * 0.52, y + size * 0.62);
        ctx.Stroke();
    }

    public static void DrawTrash(Context ctx, double x, double y, double size, bool destructive = true)
    {
        var cx = x + size * 0.5;
        var top = y + size * 0.2;
        var bodyTop = y + size * 0.34;
        var bodyBottom = y + size * 0.82;
        var alpha = destructive ? 0.95 : 0.5;

        var r = destructive ? 0.86 : 0.5;
        var g = destructive ? 0.28 : 0.52;
        var b = destructive ? 0.27 : 0.56;

        DrawRoundedRect(ctx, x + size * 0.24, y + size * 0.1, size * 0.52, size * 0.08, size * 0.03);
        ctx.SetSourceRGBA(r + 0.12, g + 0.18, b + 0.18, alpha);
        ctx.Fill();

        DrawRoundedRect(ctx, x + size * 0.16, top, size * 0.68, size * 0.1, size * 0.03);
        ctx.SetSourceRGBA(r + 0.08, g + 0.1, b + 0.1, alpha);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.08, 0.05, 0.04, destructive ? 0.72 : 0.35);
        ctx.LineWidth = Math.Max(1, size * 0.035);
        ctx.Stroke();

        ctx.NewPath();
        ctx.MoveTo(x + size * 0.25, bodyTop);
        ctx.LineTo(x + size * 0.75, bodyTop);
        ctx.LineTo(x + size * 0.66, bodyBottom);
        ctx.LineTo(x + size * 0.34, bodyBottom);
        ctx.ClosePath();
        ctx.SetSourceRGBA(r, g, b, alpha);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.1, 0.06, 0.05, destructive ? 0.8 : 0.35);
        ctx.LineWidth = Math.Max(1, size * 0.04);
        ctx.Stroke();

        ctx.SetSourceRGBA(1, 0.74, 0.72, destructive ? 0.28 : 0.12);
        ctx.MoveTo(x + size * 0.31, bodyTop + size * 0.06);
        ctx.LineTo(x + size * 0.69, bodyTop + size * 0.06);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.18, 0.09, 0.08, destructive ? 0.7 : 0.32);
        ctx.LineWidth = Math.Max(1, size * 0.032);
        for (var i = -1; i <= 1; i++)
        {
            var offset = i * size * 0.11;
            ctx.MoveTo(cx + offset * 0.55, bodyTop + size * 0.08);
            ctx.LineTo(cx + offset, bodyBottom - size * 0.07);
        }
        ctx.Stroke();
    }

    public static void DrawOwner(Context ctx, double x, double y, double size, CrownVisualState state)
    {
        var cx = x + size * 0.5;
        var top = y + size * 0.18;
        var baseY = y + size * 0.7;
        var active = state != CrownVisualState.Member;

        if (state == CrownVisualState.Owner)
        {
            DrawGlow(ctx, cx, y + size * 0.48, size * 0.55, 1, 0.78, 0.12);
        }

        ctx.NewPath();
        ctx.MoveTo(x + size * 0.18, baseY);
        ctx.LineTo(x + size * 0.24, top + size * 0.22);
        ctx.LineTo(x + size * 0.38, top + size * 0.4);
        ctx.LineTo(cx, top);
        ctx.LineTo(x + size * 0.62, top + size * 0.4);
        ctx.LineTo(x + size * 0.76, top + size * 0.22);
        ctx.LineTo(x + size * 0.82, baseY);
        ctx.ClosePath();
        ctx.SetSourceRGBA(active ? 1 : 0.52, active ? 0.83 : 0.54, active ? 0.24 : 0.58, state == CrownVisualState.Member ? 0.5 : 0.96);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(active ? 0.35 : 0.35, active ? 0.22 : 0.36, active ? 0.02 : 0.4, active ? 0.95 : 0.45);
        ctx.LineWidth = Math.Max(1.1, size * 0.04);
        ctx.Stroke();

        ctx.SetSourceRGBA(1, 0.96, 0.55, active ? 0.95 : 0.28);
        DrawGem(ctx, x + size * 0.25, top + size * 0.2, size * 0.06);
        DrawGem(ctx, cx, top + size * 0.1, size * 0.075);
        DrawGem(ctx, x + size * 0.75, top + size * 0.2, size * 0.06);

        DrawRoundedRect(ctx, x + size * 0.2, baseY - size * 0.02, size * 0.6, size * 0.13, size * 0.04);
        ctx.SetSourceRGBA(active ? 0.92 : 0.44, active ? 0.64 : 0.46, active ? 0.12 : 0.5, active ? 0.98 : 0.5);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.12, 0.08, 0.03, active ? 0.75 : 0.28);
        ctx.LineWidth = Math.Max(1, size * 0.03);
        ctx.Stroke();
    }

    public static void DrawGear(Context ctx, double x, double y, double size, bool active, bool locked)
    {
        var activeVisual = active || locked;
        var cx = x + size * 0.5;
        var cy = y + size * 0.5;
        var outerR = size * 0.45;
        var toothRootR = size * 0.35;
        var hubR = size * 0.22;
        var holeR = size * 0.1;

        AppendCogWheel(ctx, cx, cy, 12, outerR, toothRootR, size * 0.015);
        ctx.SetSourceRGBA(activeVisual ? 0.27 : 0.5, activeVisual ? 0.68 : 0.52, activeVisual ? 0.86 : 0.56, activeVisual ? 0.96 : 0.5);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.025, 0.04, 0.055, activeVisual ? 0.88 : 0.34);
        ctx.LineWidth = Math.Max(1.2, size * 0.045);
        ctx.Stroke();

        ctx.SetSourceRGBA(activeVisual ? 0.11 : 0.31, activeVisual ? 0.33 : 0.36, activeVisual ? 0.43 : 0.4, activeVisual ? 0.96 : 0.45);
        ctx.Arc(cx, cy, hubR, 0, Math.PI * 2);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.02, 0.04, 0.05, activeVisual ? 0.72 : 0.25);
        ctx.LineWidth = Math.Max(1, size * 0.032);
        ctx.Stroke();

        ctx.SetSourceRGBA(activeVisual ? 0.03 : 0.18, activeVisual ? 0.11 : 0.2, activeVisual ? 0.16 : 0.23, activeVisual ? 0.92 : 0.42);
        ctx.Arc(cx, cy, holeR, 0, Math.PI * 2);
        ctx.Fill();
    }

    public static void DrawPickaxe(Context ctx, double x, double y, double size, bool active, bool locked)
    {
        var activeVisual = active || locked;
        var metalAlpha = activeVisual ? 0.97 : 0.5;
        var handleAlpha = activeVisual ? 0.95 : 0.5;

        ctx.Save();
        ctx.Translate(x, y);
        ctx.Scale(size / 100, size / 100);

        ctx.LineJoin = LineJoin.Round;
        ctx.LineCap = LineCap.Round;

        ctx.Save();
        ctx.Translate(50, 50);
        ctx.Rotate(-Math.PI / 4);
        ctx.Translate(-50, -50);

        DrawRoundedRect(ctx, 44, 30, 12, 62, 4);
        ctx.SetSourceRGBA(activeVisual ? 0.48 : 0.38, activeVisual ? 0.29 : 0.38, activeVisual ? 0.13 : 0.42, handleAlpha);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.07, 0.045, 0.028, activeVisual ? 0.78 : 0.3);
        ctx.LineWidth = 3.8;
        ctx.Stroke();

        ctx.NewPath();
        ctx.MoveTo(47, 35);
        ctx.LineTo(47, 86);
        ctx.LineWidth = 2.8;
        ctx.SetSourceRGBA(1, 0.72, 0.34, activeVisual ? 0.24 : 0.09);
        ctx.Stroke();

        DrawRoundedRect(ctx, 13, 14, 74, 25, 6);
        ctx.SetSourceRGBA(activeVisual ? 0.92 : 0.54, activeVisual ? 0.68 : 0.54, activeVisual ? 0.28 : 0.58, metalAlpha);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.08, 0.065, 0.045, activeVisual ? 0.84 : 0.32);
        ctx.LineWidth = 4.4;
        ctx.Stroke();

        DrawRoundedRect(ctx, 17, 18, 16, 17, 4);
        ctx.SetSourceRGBA(activeVisual ? 0.98 : 0.58, activeVisual ? 0.76 : 0.56, activeVisual ? 0.34 : 0.58, metalAlpha);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.08, 0.065, 0.045, activeVisual ? 0.86 : 0.32);
        ctx.LineWidth = 3.2;
        ctx.Stroke();

        DrawRoundedRect(ctx, 67, 18, 16, 17, 4);
        ctx.SetSourceRGBA(activeVisual ? 0.98 : 0.58, activeVisual ? 0.76 : 0.56, activeVisual ? 0.34 : 0.58, metalAlpha);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.08, 0.065, 0.045, activeVisual ? 0.86 : 0.32);
        ctx.LineWidth = 3.2;
        ctx.Stroke();

        ctx.SetSourceRGBA(1, 0.88, 0.48, activeVisual ? 0.28 : 0.1);
        ctx.LineWidth = 2.8;
        ctx.MoveTo(23, 20);
        ctx.LineTo(77, 20);
        ctx.Stroke();

        ctx.Restore();
        ctx.Restore();
    }

    private static void DrawGlow(Context ctx, double cx, double cy, double radius, double r, double g, double b)
    {
        foreach (var (scale, alpha) in new[] { (1.0, 0.1), (0.74, 0.16), (0.48, 0.22) })
        {
            ctx.NewPath();
            ctx.Arc(cx, cy, radius * scale, 0, Math.PI * 2);
            ctx.SetSourceRGBA(r, g, b, alpha);
            ctx.Fill();
        }
    }

    private static void DrawLightRays(Context ctx, double cx, double cy, double size)
    {
        ctx.SetSourceRGBA(1, 0.84, 0.2, 0.7);
        ctx.LineWidth = Math.Max(1, size * 0.035);
        for (var i = 0; i < 8; i++)
        {
            var angle = Math.PI * 2 * i / 8;
            var start = size * 0.3;
            var end = size * 0.42;
            ctx.MoveTo(cx + Math.Cos(angle) * start, cy + Math.Sin(angle) * start);
            ctx.LineTo(cx + Math.Cos(angle) * end, cy + Math.Sin(angle) * end);
        }

        ctx.Stroke();
    }

    private static void DrawFilament(Context ctx, double cx, double cy, double bulbR, bool active)
    {
        ctx.SetSourceRGBA(active ? 1 : 0.48, active ? 0.62 : 0.45, active ? 0.08 : 0.38, active ? 0.95 : 0.45);
        ctx.LineWidth = Math.Max(1, bulbR * 0.14);
        ctx.MoveTo(cx - bulbR * 0.42, cy + bulbR * 0.18);
        ctx.CurveTo(cx - bulbR * 0.2, cy - bulbR * 0.04, cx + bulbR * 0.2, cy + bulbR * 0.4, cx + bulbR * 0.42, cy + bulbR * 0.18);
        ctx.Stroke();
    }

    private static void DrawLampBase(Context ctx, double cx, double top, double size, bool active)
    {
        var w = size * 0.28;
        var h = size * 0.22;
        DrawRoundedRect(ctx, cx - w * 0.5, top, w, h, size * 0.03);
        ctx.SetSourceRGBA(active ? 0.5 : 0.4, active ? 0.46 : 0.42, active ? 0.34 : 0.46, active ? 0.95 : 0.58);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.14, 0.13, 0.13, active ? 0.8 : 0.38);
        ctx.LineWidth = Math.Max(1, size * 0.03);
        ctx.Stroke();

        ctx.SetSourceRGBA(0.14, 0.13, 0.12, active ? 0.8 : 0.42);
        ctx.LineWidth = Math.Max(1, size * 0.026);
        for (var i = 1; i <= 2; i++)
        {
            var yy = top + h * i / 3;
            ctx.MoveTo(cx - w * 0.42, yy);
            ctx.LineTo(cx + w * 0.42, yy);
        }

        ctx.Stroke();
    }

    private static void DrawGem(Context ctx, double cx, double cy, double r)
    {
        ctx.NewPath();
        ctx.MoveTo(cx, cy - r);
        ctx.LineTo(cx + r, cy);
        ctx.LineTo(cx, cy + r);
        ctx.LineTo(cx - r, cy);
        ctx.ClosePath();
        ctx.Fill();
    }

    private static void DrawSmallLock(Context ctx, double x, double y, double size)
    {
        var cx = x + size * 0.5;
        ctx.SetSourceRGBA(0.08, 0.09, 0.1, 0.68);
        ctx.LineWidth = Math.Max(1, size * 0.12);
        ctx.Arc(cx, y + size * 0.42, size * 0.26, Math.PI, Math.PI * 2);
        ctx.Stroke();

        DrawRoundedRect(ctx, x + size * 0.2, y + size * 0.38, size * 0.6, size * 0.42, size * 0.08);
        ctx.SetSourceRGBA(0.12, 0.14, 0.16, 0.72);
        ctx.Fill();
    }

    private static void AppendCogWheel(Context ctx, double cx, double cy, int teeth, double outerR, double rootR, double bevel)
    {
        var step = Math.PI * 2 / teeth;
        ctx.NewPath();
        var firstPoint = true;
        for (var i = 0; i < teeth; i++)
        {
            var angle = i * step - Math.PI / 2;
            var points = new[]
            {
                (angle - step * 0.42, rootR),
                (angle - step * 0.28, outerR - bevel),
                (angle - step * 0.1, outerR),
                (angle + step * 0.1, outerR),
                (angle + step * 0.28, outerR - bevel),
                (angle + step * 0.42, rootR)
            };

            foreach (var (pointAngle, radius) in points)
            {
                var px = cx + Math.Cos(pointAngle) * radius;
                var py = cy + Math.Sin(pointAngle) * radius;
                if (firstPoint)
                {
                    ctx.MoveTo(px, py);
                    firstPoint = false;
                }
                else
                {
                    ctx.LineTo(px, py);
                }
            }
        }

        ctx.ClosePath();
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
