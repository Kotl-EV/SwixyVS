// =============================================================================
// Cairo icons for Permission Manager — stroke icons, 24×24 viewBox.
// =============================================================================

using System;
using Cairo;

namespace SwixyPermissionManager.Content;

internal static class PermissionCairoIcons
{
    private static double Map(double v, double size) => v * (size / 24.0);

    private static void StrokeSetup(Context ctx, double size, double r, double g, double b, double a = 1)
    {
        ctx.Save();
        ctx.SetSourceRGBA(r, g, b, a);
        ctx.LineWidth = Math.Max(1.2, Map(2, size));
        ctx.LineCap = LineCap.Round;
        ctx.LineJoin = LineJoin.Round;
    }

    /// <summary>Shield — roles / security.</summary>
    public static void DrawShield(Context ctx, double x, double y, double size, bool active = true)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var a = active ? 1.0 : 0.55;
        var c = PermissionTheme.ColAccent;
        StrokeSetup(ctx, size, c[0], c[1], c[2], a);
        ctx.NewPath();
        ctx.MoveTo(X(12), Y(3));
        ctx.LineTo(X(20), Y(7));
        ctx.LineTo(X(20), Y(13));
        ctx.CurveTo(X(20), Y(17.5), X(16.5), Y(20.5), X(12), Y(22));
        ctx.CurveTo(X(7.5), Y(20.5), X(4), Y(17.5), X(4), Y(13));
        ctx.LineTo(X(4), Y(7));
        ctx.ClosePath();
        ctx.SetSourceRGBA(c[0], c[1], c[2], a * 0.18);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(c[0], c[1], c[2], a);
        ctx.Stroke();
        ctx.Restore();
    }

    /// <summary>Key — privileges.</summary>
    public static void DrawKey(Context ctx, double x, double y, double size, bool active = true)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var a = active ? 1.0 : 0.55;
        var c = PermissionTheme.ColWarn;
        StrokeSetup(ctx, size, c[0], c[1], c[2], a);
        ctx.NewPath();
        ctx.Arc(X(8), Y(12), Map(4.2, size), 0, Math.PI * 2);
        ctx.Stroke();
        ctx.NewPath();
        ctx.MoveTo(X(12), Y(12));
        ctx.LineTo(X(21), Y(12));
        ctx.MoveTo(X(18), Y(12));
        ctx.LineTo(X(18), Y(16));
        ctx.MoveTo(X(15.5), Y(12));
        ctx.LineTo(X(15.5), Y(15));
        ctx.Stroke();
        ctx.Restore();
    }

    /// <summary>Checkbox well — granted / off.</summary>
    public static void DrawCheckWell(Context ctx, double x, double y, double size, bool granted, bool selected)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);

        // Well
        ctx.Save();
        if (granted)
        {
            ctx.SetSourceRGBA(0.12, 0.35, 0.24, 0.95);
        }
        else
        {
            ctx.SetSourceRGBA(0.08, 0.10, 0.16, 0.95);
        }

        RoundRect(ctx, X(3), Y(3), Map(18, size), Map(18, size), Map(3, size));
        ctx.FillPreserve();
        var border = selected ? PermissionTheme.ColAccent : PermissionTheme.ColBorder;
        ctx.SetSourceRGBA(border[0], border[1], border[2], selected ? 1 : 0.8);
        ctx.LineWidth = Math.Max(1.1, Map(1.6, size));
        ctx.Stroke();

        if (granted)
        {
            var ok = PermissionTheme.ColOk;
            ctx.SetSourceRGBA(ok[0], ok[1], ok[2], 1);
            ctx.LineWidth = Math.Max(1.5, Map(2.2, size));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;
            ctx.NewPath();
            ctx.MoveTo(X(7), Y(12.5));
            ctx.LineTo(X(10.5), Y(16));
            ctx.LineTo(X(17), Y(8.5));
            ctx.Stroke();
        }

        ctx.Restore();
    }

    public static void DrawPlus(Context ctx, double x, double y, double size)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var c = PermissionTheme.ColOk;
        StrokeSetup(ctx, size, c[0], c[1], c[2]);
        ctx.NewPath();
        ctx.MoveTo(X(12), Y(5));
        ctx.LineTo(X(12), Y(19));
        ctx.MoveTo(X(5), Y(12));
        ctx.LineTo(X(19), Y(12));
        ctx.Stroke();
        ctx.Restore();
    }

    public static void DrawMinus(Context ctx, double x, double y, double size)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var c = PermissionTheme.ColWarn;
        StrokeSetup(ctx, size, c[0], c[1], c[2]);
        ctx.NewPath();
        ctx.MoveTo(X(5), Y(12));
        ctx.LineTo(X(19), Y(12));
        ctx.Stroke();
        ctx.Restore();
    }

    public static void DrawClone(Context ctx, double x, double y, double size)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var c = PermissionTheme.ColAccent;
        StrokeSetup(ctx, size, c[0], c[1], c[2]);
        RoundRect(ctx, X(4), Y(7), Map(11, size), Map(13, size), Map(1.5, size));
        ctx.Stroke();
        RoundRect(ctx, X(9), Y(4), Map(11, size), Map(13, size), Map(1.5, size));
        ctx.Stroke();
        ctx.Restore();
    }

    public static void DrawTrash(Context ctx, double x, double y, double size)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var c = PermissionTheme.ColDanger;
        StrokeSetup(ctx, size, c[0], c[1], c[2]);
        ctx.NewPath();
        ctx.MoveTo(X(5), Y(7));
        ctx.LineTo(X(19), Y(7));
        ctx.MoveTo(X(9), Y(7));
        ctx.LineTo(X(9), Y(5));
        ctx.LineTo(X(15), Y(5));
        ctx.LineTo(X(15), Y(7));
        ctx.MoveTo(X(8), Y(7));
        ctx.LineTo(X(8.5), Y(19));
        ctx.LineTo(X(15.5), Y(19));
        ctx.LineTo(X(16), Y(7));
        ctx.Stroke();
        ctx.Restore();
    }

    public static void DrawDiff(Context ctx, double x, double y, double size)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var c = PermissionTheme.ColAccent;
        StrokeSetup(ctx, size, c[0], c[1], c[2]);
        // Two columns
        RoundRect(ctx, X(3), Y(4), Map(7, size), Map(16, size), Map(1, size));
        ctx.Stroke();
        RoundRect(ctx, X(14), Y(4), Map(7, size), Map(16, size), Map(1, size));
        ctx.Stroke();
        ctx.MoveTo(X(11), Y(12));
        ctx.LineTo(X(13), Y(12));
        ctx.Stroke();
        ctx.Restore();
    }

    public static void DrawRefresh(Context ctx, double x, double y, double size)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var c = PermissionTheme.ColAccent;
        StrokeSetup(ctx, size, c[0], c[1], c[2]);
        ctx.NewPath();
        ctx.Arc(X(12), Y(12), Map(7, size), Math.PI * 0.15, Math.PI * 1.55);
        ctx.Stroke();
        ctx.NewPath();
        ctx.MoveTo(X(17), Y(5));
        ctx.LineTo(X(19), Y(10));
        ctx.LineTo(X(14), Y(9));
        ctx.ClosePath();
        ctx.Fill();
        ctx.Restore();
    }

    public static void DrawClose(Context ctx, double x, double y, double size)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var c = PermissionTheme.ColTextMuted;
        StrokeSetup(ctx, size, c[0], c[1], c[2]);
        ctx.NewPath();
        ctx.MoveTo(X(7), Y(7));
        ctx.LineTo(X(17), Y(17));
        ctx.MoveTo(X(17), Y(7));
        ctx.LineTo(X(7), Y(17));
        ctx.Stroke();
        ctx.Restore();
    }

    public static void DrawUsers(Context ctx, double x, double y, double size)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var c = PermissionTheme.ColTextMuted;
        StrokeSetup(ctx, size, c[0], c[1], c[2]);
        ctx.NewPath();
        ctx.Arc(X(9), Y(9), Map(3.2, size), 0, Math.PI * 2);
        ctx.Stroke();
        ctx.NewPath();
        ctx.Arc(X(16), Y(10), Map(2.6, size), 0, Math.PI * 2);
        ctx.Stroke();
        ctx.NewPath();
        ctx.MoveTo(X(4), Y(19));
        ctx.CurveTo(X(4), Y(15), X(6.5), Y(13), X(9), Y(13));
        ctx.CurveTo(X(11.5), Y(13), X(14), Y(15), X(14), Y(19));
        ctx.Stroke();
        ctx.Restore();
    }

    public static void DrawApply(Context ctx, double x, double y, double size)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var c = PermissionTheme.ColOk;
        StrokeSetup(ctx, size, c[0], c[1], c[2]);
        ctx.NewPath();
        ctx.MoveTo(X(5), Y(12));
        ctx.LineTo(X(10), Y(17));
        ctx.LineTo(X(19), Y(6));
        ctx.Stroke();
        ctx.Restore();
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
}
