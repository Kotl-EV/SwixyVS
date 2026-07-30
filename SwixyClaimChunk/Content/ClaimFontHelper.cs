// =============================================================================
// ClaimFontHelper.cs — Minecraft fonts for ClaimChunk UI.
// Body: minecraft.ttf → family "SwixyClaimBody" (patched capital Й).
// Titles/tabs: MinecraftTitle.ttf → family "SwixyClaimTitle".
// Unique family names avoid Windows/Cairo picking a stale "Minecraft Rus" face.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using FontWeight = Cairo.FontWeight;
using IOPath = System.IO.Path;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Registers shipped Minecraft TTFs so Cairo can resolve claim GUI text.
/// Family names must match the OpenType name table in the shipped TTFs.
/// </summary>
public static class ClaimFontHelper
{
    /// <summary>Body family from minecraft.ttf (OpenType name, not file name).</summary>
    public const string BodyFamilyName = "SwixyClaimBody";

    /// <summary>Title / tab family from MinecraftTitle.ttf.</summary>
    public const string TitleFamilyName = "SwixyClaimTitle";

    /// <summary>Legacy alias — body family.</summary>
    public const string FamilyName = BodyFamilyName;

    /// <summary>Design accent text #FEE4CF.</summary>
    public static readonly double[] ColorCream = [0.996, 0.894, 0.812, 1.0];

    /// <summary>Secondary accent #D29F78.</summary>
    public static readonly double[] ColorAccent = [0.824, 0.624, 0.471, 1.0];

    private static readonly string[] BodyFontFileNames = ["minecraft.ttf", "Minecraft.ttf"];
    private static readonly string[] TitleFontFileNames = ["MinecraftTitle.ttf", "minecrafttitle.ttf"];

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
                    "[SwixyClaimChunk] Registered {0} font file(s) (body '{1}', title '{2}'; e.g. {3})",
                    registeredFiles.Count,
                    BodyFamilyName,
                    TitleFamilyName,
                    registeredFiles[0]);
            }
            else
            {
                api.Logger.Warning(
                    "[SwixyClaimChunk] Minecraft .ttf not found — UI falls back to system fonts. " +
                    "Expected assets/swixyclaimchunk/fonts/minecraft.ttf and MinecraftTitle.ttf");
            }
        }
        catch (Exception ex)
        {
            api.Logger.Warning("[SwixyClaimChunk] Font registration failed: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Body font at SVG design size (px at GUIScale=1).
    /// <see cref="CairoFont.SetupContext"/> multiplies UnscaledFontsize by GUIScale.
    /// Minecraft faces are single-weight — never request Bold (Cairo misses the face).
    /// </summary>
    public static CairoFont Create(double designSize, double[]? color = null, bool bold = false)
    {
        return CreateWithFamily(BodyFamilyName, designSize, color, bold: false);
    }

    /// <summary>Title / tab font (Minecraft Five), optionally double-stroked like quest top menu.</summary>
    public static CairoFont CreateTitle(double designSize, double[]? color = null, bool renderTwice = true)
    {
        var font = CreateWithFamily(TitleFamilyName, designSize, color, bold: false);
        return renderTwice ? font.WithRenderTwice() : font;
    }

    /// <summary>
    /// Font for DynamicCustomDraw when positions are already multiplied by GUIScale.
    /// Same as <see cref="Create"/> — UnscaledFontsize is design size.
    /// </summary>
    public static CairoFont CreateForSurface(double designSize, double[]? color = null, bool bold = false)
    {
        return Create(designSize, color, bold: false);
    }

    /// <summary>
    /// Apply body font (SwixyClaimBody) to a Cairo context for chrome drawing.
    /// </summary>
    public static void SetupMontserrat(
        Context ctx,
        double designSize,
        double[]? color = null,
        bool bold = true)
    {
        SetupBody(ctx, designSize, color);
    }

    /// <summary>Body face on a surface context.</summary>
    public static void SetupBody(Context ctx, double designSize, double[]? color = null)
    {
        ApplyFont(ctx, BodyFamilyName, designSize, color);
    }

    /// <summary>Title face on a surface context (tabs / section headers).</summary>
    public static void SetupTitle(Context ctx, double designSize, double[]? color = null)
    {
        ApplyFont(ctx, TitleFamilyName, designSize, color);
    }

    private static void ApplyFont(Context ctx, string family, double designSize, double[]? color)
    {
        var font = CreateWithFamily(family, designSize, color, bold: false);
        font.SetupContext(ctx);
        ctx.Operator = Operator.Over;
        if (color != null && color.Length >= 3)
        {
            var a = color.Length > 3 ? color[3] : 1.0;
            ctx.SetSourceRGBA(color[0], color[1], color[2], a);
        }
    }

    private static CairoFont CreateWithFamily(string family, double designSize, double[]? color, bool bold)
    {
        return new CairoFont
        {
            Fontname = family,
            UnscaledFontsize = (float)designSize,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            Color = color ?? ColorCream
        };
    }

    public static CairoFont Tab() => CreateTitle(20, ColorCream);

    public static CairoFont Title() => CreateTitle(16, ColorCream, renderTwice: false);

    public static CairoFont Body() => Create(14, ColorAccent);

    public static CairoFont Hint() => Create(12, ColorAccent);

    public static CairoFont Center() => Create(16, ColorCream);

    public static CairoFont TabSurface() => CreateTitle(20, ColorCream);

    public static CairoFont TitleSurface() => CreateTitle(16, ColorCream, renderTwice: false);

    public static CairoFont BodySurface() => CreateForSurface(14, ColorAccent);

    public static CairoFont HintSurface() => CreateForSurface(12, ColorAccent);

    public static CairoFont CenterSurface() => CreateForSurface(16, ColorCream);

    public static CairoFont LegendSurface() => CreateForSurface(14, ColorAccent);

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
            var assetsRoot = IOPath.Combine(source, "assets");
            if (Directory.Exists(assetsRoot))
            {
                foreach (var pattern in new[] { "minecraft.ttf", "MinecraftTitle.ttf", "Minecraft*.ttf" })
                {
                    foreach (var file in Directory.EnumerateFiles(
                                 assetsRoot,
                                 pattern,
                                 SearchOption.AllDirectories))
                    {
                        foreach (var p in YieldIfExists(file))
                        {
                            yield return p;
                        }
                    }
                }
            }
        }

        foreach (var fileName in BodyFontFileNames)
        {
            foreach (var p in YieldIfExists(TryExtractDomainFont(api, "swixyclaimchunk", $"fonts/{fileName}")))
            {
                yield return p;
            }
        }

        foreach (var fileName in TitleFontFileNames)
        {
            foreach (var p in YieldIfExists(TryExtractDomainFont(api, "swixyclaimchunk", $"fonts/{fileName}")))
            {
                yield return p;
            }
        }

        // Reuse Questbook pack if present (same typefaces).
        foreach (var fileName in BodyFontFileNames.Concat(TitleFontFileNames))
        {
            foreach (var p in YieldIfExists(TryExtractDomainFont(api, "swixyquestbook", $"fonts/{fileName}")))
            {
                yield return p;
            }
        }

        var userFonts = IOPath.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Fonts");
        if (Directory.Exists(userFonts))
        {
            foreach (var fileName in BodyFontFileNames.Concat(TitleFontFileNames))
            {
                foreach (var p in YieldIfExists(IOPath.Combine(userFonts, fileName)))
                {
                    yield return p;
                }
            }
        }
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
            var needsWrite = !File.Exists(outPath);
            if (!needsWrite)
            {
                var existing = File.ReadAllBytes(outPath);
                needsWrite = existing.Length != asset.Data.Length
                    || !existing.AsSpan().SequenceEqual(asset.Data);
            }

            if (needsWrite)
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
            // Versioned unique file name so we always reinstall the patched glyph file
            // even when an older minecraft.ttf is locked by Windows.
            var dest = IOPath.Combine(userFonts, "swixyclaimchunk-v9-" + IOPath.GetFileName(sourcePath));
            var srcInfo = new FileInfo(sourcePath);
            var needsCopy = !File.Exists(dest)
                || new FileInfo(dest).Length != srcInfo.Length
                || !FilesEqual(sourcePath, dest);
            if (needsCopy)
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

    private static bool FilesEqual(string a, string b)
    {
        try
        {
            var ba = File.ReadAllBytes(a);
            var bb = File.ReadAllBytes(b);
            if (ba.Length != bb.Length)
            {
                return false;
            }

            for (var i = 0; i < ba.Length; i++)
            {
                if (ba[i] != bb[i])
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
