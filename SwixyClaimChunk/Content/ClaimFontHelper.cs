// =============================================================================
// ClaimFontHelper.cs — Montserrat for ClaimChunk UI (same approach as Questbook).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using FontWeight = Cairo.FontWeight;
using IOPath = System.IO.Path;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Registers Montserrat from assets so Cairo can resolve the family for claim GUI text.
/// </summary>
public static class ClaimFontHelper
{
    public const string FamilyName = "Montserrat";

    /// <summary>Design accent text #FEE4CF.</summary>
    public static readonly double[] ColorCream = [0.996, 0.894, 0.812, 1.0];

    /// <summary>Secondary accent #D29F78.</summary>
    public static readonly double[] ColorAccent = [0.824, 0.624, 0.471, 1.0];

    private const uint FrPrivate = 0x10;
    private static bool registered;

    [DllImport("gdi32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int AddFontResourceExW(string lpszFilename, uint fl, IntPtr pdv);

    public static void EnsureRegistered(ICoreClientAPI api, Mod? mod = null)
    {
        if (registered)
        {
            return;
        }

        registered = true;
        var registeredFiles = new List<string>(4);

        try
        {
            foreach (var path in EnumerateCandidateFontFiles(api, mod))
            {
                if (TryRegisterFontFile(path, api, out var usedPath) && usedPath != null)
                {
                    registeredFiles.Add(usedPath);
                }
            }

            if (registeredFiles.Count > 0)
            {
                api.Logger.Notification(
                    "[SwixyClaimChunk] Registered {0} Montserrat font file(s) (e.g. {1})",
                    registeredFiles.Count,
                    registeredFiles[0]);
            }
            else
            {
                api.Logger.Warning(
                    "[SwixyClaimChunk] Montserrat .ttf not found — UI falls back to system fonts. " +
                    "Expected assets/swixyclaimchunk/fonts/Montserrat-*.ttf");
            }
        }
        catch (Exception ex)
        {
            api.Logger.Warning("[SwixyClaimChunk] Font registration failed: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Montserrat at SVG design size (px at GUIScale=1).
    /// <see cref="CairoFont.SetupContext"/> multiplies UnscaledFontsize by GUIScale —
    /// same as ElementBounds.Fixed — so text and layout stay proportional.
    /// </summary>
    public static CairoFont Create(double designSize, double[]? color = null, bool bold = true)
    {
        return new CairoFont
        {
            Fontname = FamilyName,
            UnscaledFontsize = (float)designSize,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            Color = color ?? ColorCream
        };
    }

    /// <summary>
    /// Font for DynamicCustomDraw when positions are already multiplied by GUIScale
    /// (e.g. <c>x * s</c> with <c>s = RuntimeEnv.GUIScale</c>).
    /// <see cref="CairoFont.SetupContext"/> multiplies <see cref="CairoFont.UnscaledFontsize"/> by GUIScale,
    /// so we pass design size as UnscaledFontsize — result is designSize×GUIScale screen pixels,
    /// matching scaled layout coordinates.
    /// </summary>
    public static CairoFont CreateForSurface(double designSize, double[]? color = null, bool bold = true)
    {
        // Same as Create: do NOT divide by GUIScale (that was undersizing chrome text).
        return Create(designSize, color, bold);
    }

    /// <summary>
    /// Apply Montserrat (Bold) to a Cairo context for chrome drawing.
    /// Always forces family name — never "Montserrat-Bold" (Cairo misses PostScript name).
    /// </summary>
    public static void SetupMontserrat(
        Context ctx,
        double designSize,
        double[]? color = null,
        bool bold = true)
    {
        var font = Create(designSize, color, bold);
        font.Fontname = FamilyName;
        font.FontWeight = bold ? FontWeight.Bold : FontWeight.Normal;
        font.SetupContext(ctx);
        ctx.Operator = Operator.Over;
        if (color != null && color.Length >= 3)
        {
            var a = color.Length > 3 ? color[3] : 1.0;
            ctx.SetSourceRGBA(color[0], color[1], color[2], a);
        }
    }

    // SVG outlined-text height ~22px on tabs → design size 18–20.
    public static CairoFont Tab() => Create(20, ColorCream, bold: true);

    public static CairoFont Title() => Create(16, ColorCream, bold: true);

    public static CairoFont Body() => Create(14, ColorAccent, bold: true);

    public static CairoFont Hint() => Create(12, ColorAccent, bold: true);

    public static CairoFont Center() => Create(16, ColorCream, bold: true);

    public static CairoFont TabSurface() => CreateForSurface(20, ColorCream, bold: true);

    public static CairoFont TitleSurface() => CreateForSurface(16, ColorCream, bold: true);

    public static CairoFont BodySurface() => CreateForSurface(14, ColorAccent, bold: true);

    public static CairoFont HintSurface() => CreateForSurface(12, ColorAccent, bold: true);

    public static CairoFont CenterSurface() => CreateForSurface(16, ColorCream, bold: true);

    public static CairoFont LegendSurface() => CreateForSurface(14, ColorAccent, bold: true);

    private static IEnumerable<string> EnumerateCandidateFontFiles(ICoreClientAPI api, Mod? mod)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> YieldIfExists(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                yield break;
            }

            var full = IOPath.GetFullPath(path);
            if (!seen.Add(full) || !File.Exists(full))
            {
                yield break;
            }

            yield return full;
        }

        var source = mod?.SourcePath
            ?? api.ModLoader.GetMod("swixyclaimchunk")?.SourcePath;
        if (!string.IsNullOrWhiteSpace(source) && Directory.Exists(source))
        {
            foreach (var file in Directory.EnumerateFiles(
                         IOPath.Combine(source, "assets"),
                         "Montserrat*.ttf",
                         SearchOption.AllDirectories))
            {
                foreach (var p in YieldIfExists(file))
                {
                    yield return p;
                }
            }
        }

        foreach (var fileName in new[] { "Montserrat-Bold.ttf", "Montserrat-Regular.ttf" })
        {
            var extracted = TryExtractAssetFont(api, $"fonts/{fileName}");
            foreach (var p in YieldIfExists(extracted))
            {
                yield return p;
            }
        }

        // Reuse Questbook / game fonts if already present.
        foreach (var domain in new[] { "swixyquestbook", "game" })
        {
            var shared = TryExtractDomainFont(api, domain, "fonts/Montserrat-Bold.ttf");
            foreach (var p in YieldIfExists(shared))
            {
                yield return p;
            }
        }

        var gameFonts = IOPath.Combine(GamePaths.AssetsPath, "game", "fonts");
        if (Directory.Exists(gameFonts))
        {
            foreach (var file in Directory.EnumerateFiles(gameFonts, "Montserrat*.ttf"))
            {
                foreach (var p in YieldIfExists(file))
                {
                    yield return p;
                }
            }
        }

        var userFonts = IOPath.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Fonts");
        if (Directory.Exists(userFonts))
        {
            foreach (var file in Directory.EnumerateFiles(userFonts, "Montserrat*.ttf"))
            {
                foreach (var p in YieldIfExists(file))
                {
                    yield return p;
                }
            }
        }
    }

    private static string? TryExtractAssetFont(ICoreClientAPI api, string relativePath)
    {
        return TryExtractDomainFont(api, "swixyclaimchunk", relativePath);
    }

    private static string? TryExtractDomainFont(ICoreClientAPI api, string domain, string relativePath)
    {
        try
        {
            var asset = api.Assets.TryGet(new AssetLocation(domain, relativePath));
            if (asset?.Data == null || asset.Data.Length == 0)
            {
                return null;
            }

            var cacheDir = IOPath.Combine(GamePaths.Cache, "swixyclaimchunk", "fonts");
            Directory.CreateDirectory(cacheDir);
            var fileName = IOPath.GetFileName(relativePath);
            var outPath = IOPath.Combine(cacheDir, fileName);
            if (!File.Exists(outPath) || new FileInfo(outPath).Length != asset.Data.Length)
            {
                File.WriteAllBytes(outPath, asset.Data);
            }

            return outPath;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryRegisterFontFile(string path, ICoreClientAPI api, out string? usedPath)
    {
        usedPath = null;
        if (!File.Exists(path))
        {
            return false;
        }

        var durable = TryInstallToUserFonts(path) ?? path;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                AddFontResourceExW(durable, FrPrivate, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                api.Logger.Debug("[SwixyClaimChunk] AddFontResourceEx failed for {0}: {1}", durable, ex.Message);
            }
        }

        usedPath = durable;
        return true;
    }

    private static string? TryInstallToUserFonts(string sourcePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        try
        {
            var userFonts = IOPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Fonts");
            Directory.CreateDirectory(userFonts);
            var dest = IOPath.Combine(userFonts, IOPath.GetFileName(sourcePath));
            if (!File.Exists(dest) || new FileInfo(dest).Length != new FileInfo(sourcePath).Length)
            {
                File.Copy(sourcePath, dest, overwrite: true);
            }

            return dest;
        }
        catch
        {
            return null;
        }
    }
}
