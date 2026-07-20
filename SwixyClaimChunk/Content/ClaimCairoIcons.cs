// =============================================================================
// ClaimCairoIcons.cs
// -----------------------------------------------------------------------------
// Cairo icons from Group 470.svg (member row) — exact path geometry, 24×24 view.
// Colors: crown #FFEB61, gear #50B5E1, pickaxe #EAB137, trash #FD5A53.
// =============================================================================

using System;
using Cairo;

namespace SwixyClaimChunk.Content;

internal static class ClaimCairoIcons
{
    // Shared 24×24 icon mapping (matches white rect / Lucide-style wells in SVG).
    private static double Map(double v, double size) => v * (size / 24.0);

    /// <summary>
    /// Иконка «хранилище на земле» (groundstorage) — невидимый блок в мире,
    /// в GUI рисуем условный «настил с предметами».
    /// </summary>
    public static void DrawGroundStorage(Context ctx, double x, double y, double size)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);

        // Soft plate
        ctx.Save();
        ctx.NewPath();
        RoundRect(ctx, X(3), Y(4), Map(18, size), Map(16, size), Map(2.5, size));
        ctx.SetSourceRGBA(0.22, 0.17, 0.12, 0.95);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.55, 0.42, 0.28, 0.9);
        ctx.LineWidth = Math.Max(1, Map(1.2, size));
        ctx.Stroke();

        // Three “item” squares on the mat
        DrawMiniItem(ctx, X(5.5), Y(7), Map(5, size), 0.72, 0.55, 0.28);
        DrawMiniItem(ctx, X(11.5), Y(8.5), Map(4.5, size), 0.45, 0.55, 0.7);
        DrawMiniItem(ctx, X(9), Y(12.5), Map(5.5, size), 0.6, 0.45, 0.35);

        // Soft shadow under
        ctx.SetSourceRGBA(0, 0, 0, 0.2);
        ctx.NewPath();
        ctx.Save();
        ctx.Translate(X(12), Y(20.5));
        ctx.Scale(1.0, 0.28);
        ctx.Arc(0, 0, Map(7, size), 0, Math.PI * 2);
        ctx.Restore();
        ctx.Fill();
        ctx.Restore();
    }

    private static void DrawMiniItem(Context ctx, double x, double y, double s, double r, double g, double b)
    {
        ctx.NewPath();
        RoundRect(ctx, x, y, s, s, s * 0.15);
        ctx.SetSourceRGBA(r, g, b, 0.95);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(1, 1, 1, 0.22);
        ctx.LineWidth = Math.Max(0.8, s * 0.08);
        ctx.Stroke();
    }

    private static void RoundRect(Context ctx, double x, double y, double w, double h, double r)
    {
        r = Math.Min(r, Math.Min(w, h) * 0.5);
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

    public static void DrawHighlight(Context ctx, double x, double y, double size, bool active)
    {
        // Keep bulb for claim-list light (not in Group 470).
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

    /// <summary>Group 470 trash — stroke #FD5A53, width 2, 24×24 well.</summary>
    public static void DrawTrash(Context ctx, double x, double y, double size, bool destructive = true)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var alpha = destructive ? 1.0 : 0.38;
        // #FD5A53
        ctx.Save();
        ctx.SetSourceRGBA(0xFD / 255.0, 0x5A / 255.0, 0x53 / 255.0, alpha);
        ctx.LineWidth = Math.Max(1.25, Map(2, size));
        ctx.LineCap = LineCap.Round;
        ctx.LineJoin = LineJoin.Round;
        ctx.NewPath();
        ctx.MoveTo(X(13), Y(10));
        ctx.LineTo(X(13), Y(17));
        ctx.MoveTo(X(9), Y(10));
        ctx.LineTo(X(9), Y(17));
        ctx.MoveTo(X(5), Y(6));
        ctx.LineTo(X(5), Y(17.8));
        ctx.CurveTo(X(5), Y(18.9201), X(5), Y(19.4798), X(5.218), Y(19.9076));
        ctx.CurveTo(X(5.41), Y(20.2839), X(5.715), Y(20.5905), X(6.092), Y(20.7822));
        ctx.CurveTo(X(6.519), Y(21), X(7.079), Y(21), X(8.197), Y(21));
        ctx.LineTo(X(13.803), Y(21));
        ctx.CurveTo(X(14.921), Y(21), X(15.48), Y(21), X(15.907), Y(20.7822));
        ctx.CurveTo(X(16.284), Y(20.5905), X(16.59), Y(20.2839), X(16.782), Y(19.9076));
        ctx.CurveTo(X(17), Y(19.4802), X(17), Y(18.921), X(17), Y(17.8031));
        ctx.LineTo(X(17), Y(6));
        ctx.MoveTo(X(5), Y(6));
        ctx.LineTo(X(7), Y(6));
        ctx.MoveTo(X(5), Y(6));
        ctx.LineTo(X(3), Y(6));
        ctx.MoveTo(X(7), Y(6));
        ctx.LineTo(X(15), Y(6));
        ctx.MoveTo(X(7), Y(6));
        ctx.CurveTo(X(7), Y(5.0681), X(7), Y(4.6024), X(7.152), Y(4.2349));
        ctx.CurveTo(X(7.355), Y(3.7448), X(7.744), Y(3.3552), X(8.234), Y(3.1522));
        ctx.CurveTo(X(8.602), Y(3), X(9.068), Y(3), X(10), Y(3));
        ctx.LineTo(X(12), Y(3));
        ctx.CurveTo(X(12.932), Y(3), X(13.398), Y(3), X(13.765), Y(3.1522));
        ctx.CurveTo(X(14.255), Y(3.3552), X(14.645), Y(3.7448), X(14.848), Y(4.2349));
        ctx.CurveTo(X(15), Y(4.6024), X(15), Y(5.0681), X(15), Y(6));
        ctx.MoveTo(X(15), Y(6));
        ctx.LineTo(X(17), Y(6));
        ctx.MoveTo(X(17), Y(6));
        ctx.LineTo(X(19), Y(6));
        ctx.Stroke();
        ctx.Restore();
    }

    /// <summary>Group 470 crown/star — stroke #FFEB61, width 2.</summary>
    public static void DrawOwner(Context ctx, double x, double y, double size, CrownVisualState state)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var active = state != CrownVisualState.Member;
        var alpha = state switch
        {
            CrownVisualState.Owner => 1.0,
            CrownVisualState.CoOwner => 0.92,
            _ => 0.4
        };

        if (state == CrownVisualState.Owner)
        {
            DrawGlow(ctx, x + size * 0.5, y + size * 0.48, size * 0.5, 1, 0.92, 0.38);
        }

        ctx.Save();
        // #FFEB61
        ctx.SetSourceRGBA(0xFF / 255.0, 0xEB / 255.0, 0x61 / 255.0, alpha);
        ctx.LineWidth = Math.Max(1.25, Map(2, size));
        ctx.LineCap = LineCap.Round;
        ctx.LineJoin = LineJoin.Round;
        ctx.NewPath();
        ctx.MoveTo(X(2.335), Y(10.3363));
        ctx.CurveTo(X(2.022), Y(10.0466), X(2.192), Y(9.5229), X(2.616), Y(9.4727));
        ctx.LineTo(X(8.619), Y(8.7606));
        ctx.CurveTo(X(8.792), Y(8.7401), X(8.942), Y(8.6317), X(9.015), Y(8.4738));
        ctx.LineTo(X(11.547), Y(2.984));
        ctx.CurveTo(X(11.726), Y(2.5965), X(12.276), Y(2.5965), X(12.455), Y(2.9839));
        ctx.LineTo(X(14.987), Y(8.4736));
        ctx.CurveTo(X(15.06), Y(8.6315), X(15.209), Y(8.7403), X(15.382), Y(8.7608));
        ctx.LineTo(X(21.386), Y(9.4727));
        ctx.CurveTo(X(21.809), Y(9.5229), X(21.979), Y(10.0468), X(21.666), Y(10.3364));
        ctx.LineTo(X(17.228), Y(14.4414));
        ctx.CurveTo(X(17.1), Y(14.5595), X(17.043), Y(14.7352), X(17.077), Y(14.9058));
        ctx.LineTo(X(18.255), Y(20.8355));
        ctx.CurveTo(X(18.338), Y(21.2539), X(17.893), Y(21.5782), X(17.52), Y(21.3698));
        ctx.LineTo(X(12.245), Y(18.4161));
        ctx.CurveTo(X(12.093), Y(18.3312), X(11.909), Y(18.3316), X(11.757), Y(18.4165));
        ctx.LineTo(X(6.481), Y(21.369));
        ctx.CurveTo(X(6.109), Y(21.5774), X(5.663), Y(21.2539), X(5.746), Y(20.8354));
        ctx.LineTo(X(6.924), Y(14.9061));
        ctx.CurveTo(X(6.958), Y(14.7356), X(6.901), Y(14.5594), X(6.774), Y(14.4414));
        ctx.LineTo(X(2.335), Y(10.3363));
        ctx.ClosePath();
        ctx.Stroke();
        ctx.Restore();
    }

    /// <summary>Group 470 gear — stroke #50B5E1 + hub circle, width 2.</summary>
    public static void DrawGear(Context ctx, double x, double y, double size, bool active, bool locked)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var on = active || locked;
        var alpha = on ? 1.0 : 0.42;

        ctx.Save();
        // #50B5E1
        ctx.SetSourceRGBA(0x50 / 255.0, 0xB5 / 255.0, 0xE1 / 255.0, alpha);
        ctx.LineWidth = Math.Max(1.25, Map(2, size));
        ctx.LineCap = LineCap.Round;
        ctx.LineJoin = LineJoin.Round;
        ctx.NewPath();
        ctx.MoveTo(X(20.35), Y(8.9229));
        ctx.LineTo(X(19.984), Y(8.7192));
        ctx.CurveTo(X(19.927), Y(8.6876), X(19.899), Y(8.6717), X(19.871), Y(8.6552));
        ctx.CurveTo(X(19.598), Y(8.4917), X(19.368), Y(8.2656), X(19.2), Y(7.9952));
        ctx.CurveTo(X(19.183), Y(7.968), X(19.167), Y(7.9395), X(19.135), Y(7.8831));
        ctx.CurveTo(X(19.102), Y(7.8268), X(19.086), Y(7.7982), X(19.071), Y(7.77));
        ctx.CurveTo(X(18.92), Y(7.4887), X(18.838), Y(7.1752), X(18.834), Y(6.8561));
        ctx.CurveTo(X(18.833), Y(6.824), X(18.833), Y(6.7912), X(18.834), Y(6.726));
        ctx.LineTo(X(18.841), Y(6.3008));
        ctx.CurveTo(X(18.853), Y(5.6203), X(18.859), Y(5.2789), X(18.763), Y(4.9726));
        ctx.CurveTo(X(18.678), Y(4.7005), X(18.536), Y(4.4499), X(18.346), Y(4.2373));
        ctx.CurveTo(X(18.132), Y(3.9969), X(17.835), Y(3.8253), X(17.24), Y(3.4828));
        ctx.LineTo(X(16.746), Y(3.1982));
        ctx.CurveTo(X(16.154), Y(2.8566), X(15.857), Y(2.6857), X(15.542), Y(2.6206));
        ctx.CurveTo(X(15.264), Y(2.5629), X(14.977), Y(2.5656), X(14.699), Y(2.6279));
        ctx.CurveTo(X(14.386), Y(2.6982), X(14.093), Y(2.8735), X(13.508), Y(3.224));
        ctx.LineTo(X(13.505), Y(3.2255));
        ctx.LineTo(X(13.151), Y(3.4374));
        ctx.CurveTo(X(13.095), Y(3.4709), X(13.066), Y(3.4878), X(13.038), Y(3.5034));
        ctx.CurveTo(X(12.76), Y(3.6581), X(12.449), Y(3.7437), X(12.131), Y(3.7539));
        ctx.CurveTo(X(12.099), Y(3.7549), X(12.067), Y(3.7549), X(12.001), Y(3.7549));
        ctx.CurveTo(X(11.936), Y(3.7549), X(11.902), Y(3.7549), X(11.87), Y(3.7539));
        ctx.CurveTo(X(11.552), Y(3.7436), X(11.24), Y(3.6576), X(10.962), Y(3.5022));
        ctx.CurveTo(X(10.933), Y(3.4866), X(10.906), Y(3.4696), X(10.85), Y(3.4359));
        ctx.LineTo(X(10.493), Y(3.2221));
        ctx.CurveTo(X(9.904), Y(2.8684), X(9.609), Y(2.6912), X(9.294), Y(2.6206));
        ctx.CurveTo(X(9.016), Y(2.5581), X(8.727), Y(2.5563), X(8.448), Y(2.6147));
        ctx.CurveTo(X(8.132), Y(2.6806), X(7.836), Y(2.8528), X(7.243), Y(3.197));
        ctx.LineTo(X(7.24), Y(3.1982));
        ctx.LineTo(X(6.752), Y(3.4812));
        ctx.LineTo(X(6.747), Y(3.4845));
        ctx.CurveTo(X(6.159), Y(3.8257), X(5.864), Y(3.9967), X(5.652), Y(4.2361));
        ctx.CurveTo(X(5.463), Y(4.4486), X(5.322), Y(4.6988), X(5.237), Y(4.9702));
        ctx.CurveTo(X(5.142), Y(5.2769), X(5.147), Y(5.619), X(5.159), Y(6.3027));
        ctx.LineTo(X(5.166), Y(6.7274));
        ctx.CurveTo(X(5.167), Y(6.7917), X(5.169), Y(6.8236), X(5.168), Y(6.8552));
        ctx.CurveTo(X(5.163), Y(7.175), X(5.081), Y(7.4891), X(4.93), Y(7.771));
        ctx.CurveTo(X(4.915), Y(7.7988), X(4.899), Y(7.8267), X(4.867), Y(7.8824));
        ctx.CurveTo(X(4.834), Y(7.9381), X(4.819), Y(7.9658), X(4.802), Y(7.9927));
        ctx.CurveTo(X(4.633), Y(8.2645), X(4.402), Y(8.4919), X(4.127), Y(8.6557));
        ctx.CurveTo(X(4.1), Y(8.6719), X(4.072), Y(8.6875), X(4.015), Y(8.7187));
        ctx.LineTo(X(3.654), Y(8.9191));
        ctx.CurveTo(X(3.052), Y(9.2524), X(2.751), Y(9.4193), X(2.533), Y(9.6567));
        ctx.CurveTo(X(2.339), Y(9.8667), X(2.193), Y(10.1158), X(2.103), Y(10.3872));
        ctx.CurveTo(X(2.003), Y(10.6939), X(2.003), Y(11.0378), X(2.004), Y(11.7255));
        ctx.LineTo(X(2.006), Y(12.2877));
        ctx.CurveTo(X(2.007), Y(12.9708), X(2.009), Y(13.3122), X(2.11), Y(13.6168));
        ctx.CurveTo(X(2.2), Y(13.8863), X(2.345), Y(14.134), X(2.537), Y(14.3427));
        ctx.CurveTo(X(2.755), Y(14.5787), X(3.053), Y(14.7445), X(3.65), Y(15.0766));
        ctx.LineTo(X(4.008), Y(15.276));
        ctx.CurveTo(X(4.069), Y(15.3099), X(4.1), Y(15.3266), X(4.129), Y(15.3444));
        ctx.CurveTo(X(4.401), Y(15.5083), X(4.631), Y(15.735), X(4.798), Y(16.0053));
        ctx.CurveTo(X(4.816), Y(16.0345), X(4.834), Y(16.0648), X(4.868), Y(16.1255));
        ctx.CurveTo(X(4.903), Y(16.1853), X(4.92), Y(16.2152), X(4.936), Y(16.2452));
        ctx.CurveTo(X(5.083), Y(16.5229), X(5.161), Y(16.8315), X(5.166), Y(17.1455));
        ctx.CurveTo(X(5.167), Y(17.1794), X(5.167), Y(17.2137), X(5.165), Y(17.2827));
        ctx.LineTo(X(5.159), Y(17.6902));
        ctx.CurveTo(X(5.147), Y(18.3763), X(5.142), Y(18.7197), X(5.238), Y(19.0273));
        ctx.CurveTo(X(5.323), Y(19.2994), X(5.465), Y(19.55), X(5.655), Y(19.7627));
        ctx.CurveTo(X(5.869), Y(20.0031), X(6.167), Y(20.1745), X(6.761), Y(20.5171));
        ctx.LineTo(X(7.255), Y(20.8015));
        ctx.CurveTo(X(7.848), Y(21.1432), X(8.144), Y(21.3138), X(8.459), Y(21.379));
        ctx.CurveTo(X(8.737), Y(21.4366), X(9.025), Y(21.4344), X(9.302), Y(21.3721));
        ctx.CurveTo(X(9.616), Y(21.3017), X(9.909), Y(21.1258), X(10.496), Y(20.7743));
        ctx.LineTo(X(10.85), Y(20.5625));
        ctx.CurveTo(X(10.906), Y(20.5289), X(10.935), Y(20.5121), X(10.963), Y(20.4965));
        ctx.CurveTo(X(11.241), Y(20.3418), X(11.551), Y(20.2558), X(11.869), Y(20.2456));
        ctx.CurveTo(X(11.902), Y(20.2446), X(11.934), Y(20.2446), X(11.999), Y(20.2446));
        ctx.CurveTo(X(12.065), Y(20.2446), X(12.097), Y(20.2446), X(12.13), Y(20.2456));
        ctx.CurveTo(X(12.448), Y(20.2559), X(12.761), Y(20.3422), X(13.039), Y(20.4975));
        ctx.CurveTo(X(13.064), Y(20.5112), X(13.088), Y(20.526), X(13.132), Y(20.5519));
        ctx.LineTo(X(13.508), Y(20.7777));
        ctx.CurveTo(X(14.097), Y(21.1315), X(14.392), Y(21.3081), X(14.706), Y(21.3788));
        ctx.CurveTo(X(14.985), Y(21.4413), X(15.274), Y(21.4438), X(15.553), Y(21.3855));
        ctx.CurveTo(X(15.869), Y(21.3196), X(16.166), Y(21.1471), X(16.759), Y(20.803));
        ctx.LineTo(X(17.254), Y(20.5157));
        ctx.CurveTo(X(17.842), Y(20.1743), X(18.137), Y(20.0031), X(18.35), Y(19.7636));
        ctx.CurveTo(X(18.538), Y(19.5512), X(18.68), Y(19.3011), X(18.764), Y(19.0297));
        ctx.CurveTo(X(18.859), Y(18.7252), X(18.853), Y(18.3858), X(18.842), Y(17.7119));
        ctx.LineTo(X(18.834), Y(17.2724));
        ctx.CurveTo(X(18.833), Y(17.2081), X(18.833), Y(17.1761), X(18.834), Y(17.1445));
        ctx.CurveTo(X(18.838), Y(16.8247), X(18.92), Y(16.5104), X(19.071), Y(16.2286));
        ctx.CurveTo(X(19.086), Y(16.2007), X(19.102), Y(16.1726), X(19.134), Y(16.1171));
        ctx.CurveTo(X(19.166), Y(16.0615), X(19.183), Y(16.0337), X(19.199), Y(16.0068));
        ctx.CurveTo(X(19.368), Y(15.7349), X(19.6), Y(15.5074), X(19.874), Y(15.3435));
        ctx.CurveTo(X(19.901), Y(15.3275), X(19.929), Y(15.3122), X(19.984), Y(15.2818));
        ctx.LineTo(X(19.986), Y(15.2809));
        ctx.LineTo(X(20.347), Y(15.0805));
        ctx.CurveTo(X(20.949), Y(14.7472), X(21.25), Y(14.5801), X(21.469), Y(14.3427));
        ctx.CurveTo(X(21.662), Y(14.1327), X(21.809), Y(13.8839), X(21.898), Y(13.6126));
        ctx.CurveTo(X(21.998), Y(13.3077), X(21.997), Y(12.9658), X(21.996), Y(12.2861));
        ctx.LineTo(X(21.994), Y(11.7119));
        ctx.CurveTo(X(21.993), Y(11.0287), X(21.992), Y(10.6874), X(21.891), Y(10.3828));
        ctx.CurveTo(X(21.801), Y(10.1133), X(21.656), Y(9.8656), X(21.463), Y(9.6568));
        ctx.CurveTo(X(21.246), Y(9.4211), X(20.948), Y(9.2553), X(20.352), Y(8.9238));
        ctx.LineTo(X(20.35), Y(8.9229));
        ctx.ClosePath();
        ctx.ClosePath();
        ctx.Stroke();

        // Hub circle: SVG M292 29 r=4 → local (12,12) r=4 from origin 284,17
        ctx.NewPath();
        ctx.Arc(X(12), Y(12), Map(4, size), 0, Math.PI * 2);
        ctx.Stroke();
        ctx.Restore();
    }

    /// <summary>Group 470 pickaxe head — stroke #EAB137, width 2.</summary>
    public static void DrawPickaxe(Context ctx, double x, double y, double size, bool active, bool locked)
    {
        double X(double v) => x + Map(v, size);
        double Y(double v) => y + Map(v, size);
        var on = active || locked;
        var alpha = on ? 1.0 : 0.42;

        ctx.Save();
        // #EAB137
        ctx.SetSourceRGBA(0xEA / 255.0, 0xB1 / 255.0, 0x37 / 255.0, alpha);
        ctx.LineWidth = Math.Max(1.25, Map(2, size));
        ctx.LineCap = LineCap.Round;
        ctx.LineJoin = LineJoin.Round;
        ctx.NewPath();
        ctx.MoveTo(X(12.926), Y(20.6314));
        ctx.CurveTo(X(15.032), Y(19.6781), X(20), Y(16.7333), X(20), Y(10.165));
        ctx.LineTo(X(20), Y(6.1969));
        ctx.CurveTo(X(20), Y(5.079), X(20), Y(4.5192), X(19.782), Y(4.0918));
        ctx.CurveTo(X(19.59), Y(3.7155), X(19.284), Y(3.4097), X(18.907), Y(3.218));
        ctx.CurveTo(X(18.48), Y(3), X(17.92), Y(3), X(16.8), Y(3));
        ctx.LineTo(X(7.2), Y(3));
        ctx.CurveTo(X(6.08), Y(3), X(5.52), Y(3), X(5.092), Y(3.218));
        ctx.CurveTo(X(4.715), Y(3.4097), X(4.41), Y(3.7155), X(4.218), Y(4.0918));
        ctx.CurveTo(X(4), Y(4.5196), X(4), Y(5.0801), X(4), Y(6.2002));
        ctx.LineTo(X(4), Y(10.165));
        ctx.CurveTo(X(4), Y(16.7333), X(8.968), Y(19.6781), X(11.074), Y(20.6314));
        ctx.CurveTo(X(11.297), Y(20.7325), X(11.409), Y(20.7829), X(11.662), Y(20.8263));
        ctx.CurveTo(X(11.822), Y(20.8537), X(12.179), Y(20.8537), X(12.339), Y(20.8263));
        ctx.CurveTo(X(12.591), Y(20.7831), X(12.702), Y(20.7328), X(12.923), Y(20.6324));
        ctx.LineTo(X(12.926), Y(20.6314));
        ctx.ClosePath();
        ctx.ClosePath();
        ctx.Stroke();
        ctx.Restore();
    }

    private static void DrawGlow(Context ctx, double cx, double cy, double radius, double r, double g, double b)
    {
        foreach (var (scale, alpha) in new[] { (1.0, 0.1), (0.74, 0.16), (0.48, 0.22) })
        {
            ctx.SetSourceRGBA(r, g, b, alpha);
            ctx.Arc(cx, cy, radius * scale, 0, Math.PI * 2);
            ctx.Fill();
        }
    }

    private static void DrawLightRays(Context ctx, double cx, double cy, double size)
    {
        ctx.SetSourceRGBA(1, 0.9, 0.35, 0.35);
        ctx.LineWidth = Math.Max(1, size * 0.04);
        ctx.LineCap = LineCap.Round;
        for (var i = 0; i < 6; i++)
        {
            var a = i * Math.PI / 3 - Math.PI / 2;
            ctx.MoveTo(cx + Math.Cos(a) * size * 0.28, cy + Math.Sin(a) * size * 0.28);
            ctx.LineTo(cx + Math.Cos(a) * size * 0.42, cy + Math.Sin(a) * size * 0.42);
            ctx.Stroke();
        }
    }

    private static void DrawFilament(Context ctx, double cx, double cy, double bulbR, bool active)
    {
        ctx.SetSourceRGBA(1, 0.75, 0.2, active ? 0.7 : 0.25);
        ctx.LineWidth = Math.Max(1, bulbR * 0.12);
        ctx.MoveTo(cx - bulbR * 0.28, cy + bulbR * 0.1);
        ctx.LineTo(cx, cy - bulbR * 0.15);
        ctx.LineTo(cx + bulbR * 0.28, cy + bulbR * 0.1);
        ctx.Stroke();
    }

    private static void DrawLampBase(Context ctx, double cx, double cy, double size, bool active)
    {
        var w = size * 0.28;
        var h = size * 0.16;
        ctx.SetSourceRGBA(active ? 0.45 : 0.35, active ? 0.4 : 0.35, active ? 0.35 : 0.38, active ? 0.9 : 0.5);
        ctx.Rectangle(cx - w * 0.5, cy, w, h);
        ctx.Fill();
    }

    private static void DrawGem(Context ctx, double cx, double cy, double r)
    {
        ctx.Arc(cx, cy, r, 0, Math.PI * 2);
        ctx.Fill();
    }

    private static void DrawRoundedRect(Context ctx, double x, double y, double w, double h, double r)
    {
        r = Math.Min(r, Math.Min(w, h) * 0.5);
        ctx.NewPath();
        ctx.MoveTo(x + r, y);
        ctx.LineTo(x + w - r, y);
        ctx.Arc(x + w - r, y + r, r, -Math.PI / 2, 0);
        ctx.LineTo(x + w, y + h - r);
        ctx.Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
        ctx.LineTo(x + r, y + h);
        ctx.Arc(x + r, y + h - r, r, Math.PI / 2, Math.PI);
        ctx.LineTo(x, y + r);
        ctx.Arc(x + r, y + r, r, Math.PI, 3 * Math.PI / 2);
        ctx.ClosePath();
    }

    private static void AppendCogWheel(Context ctx, double cx, double cy, int teeth, double outerR, double rootR, double corner)
    {
        // Unused legacy helper kept for compatibility if referenced elsewhere.
        ctx.NewPath();
        ctx.Arc(cx, cy, outerR, 0, Math.PI * 2);
    }
}