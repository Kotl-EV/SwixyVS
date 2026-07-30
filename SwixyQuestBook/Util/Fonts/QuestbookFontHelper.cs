using System.Runtime.InteropServices;
using SwixyQuestBook.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using FontWeight = Cairo.FontWeight;
using IOPath = System.IO.Path;

namespace SwixyQuestBook.Util.Fonts
{
    /// <summary>
    /// Ensures shipped questbook TTFs under assets/…/fonts are visible to Cairo/fontconfig.
    /// Vintage Story only auto-installs <c>game:fonts</c>; mod fonts are otherwise ignored.
    /// Body UI: <see cref="BodyFamilyName"/> (minecraft.ttf). Titles: <see cref="TitleFamilyName"/> (MinecraftTitle.ttf).
    /// </summary>
    public static class QuestbookFontHelper
    {
        /// <summary>Body / general UI family from minecraft.ttf.</summary>
        public const string BodyFamilyName = "Minecraft Rus";

        /// <summary>Title / top-menu family from MinecraftTitle.ttf.</summary>
        public const string TitleFamilyName = "Minecraft Five";

        /// <summary>Legacy alias — body family.</summary>
        public const string FamilyName = BodyFamilyName;

        private static readonly string[] BodyFontFileNames =
        [
            "minecraft.ttf",
            "Minecraft.ttf"
        ];

        private static readonly string[] TitleFontFileNames =
        [
            "MinecraftTitle.ttf",
            "minecrafttitle.ttf"
        ];

        private const uint FrPrivate = 0x10;
        private static bool registered;
        private static string? activeSourcePath;

        [DllImport("gdi32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int AddFontResourceExW(string lpszFilename, uint fl, IntPtr pdv);

        [DllImport("gdi32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RemoveFontResourceExW(string lpszFilename, uint fl, IntPtr pdv);

        /// <summary>
        /// Register mod Minecraft TTFs so <see cref="CairoFont"/> SelectFontFace finds them.
        /// Safe to call multiple times.
        /// </summary>
        public static void EnsureRegistered(ICoreClientAPI api, Mod? mod = null)
        {
            if (registered)
                return;

            registered = true;
            var registeredFiles = new List<string>(4);

            try
            {
                foreach (string path in EnumerateCandidateFontFiles(api, mod))
                {
                    if (TryRegisterFontFile(path, api, out string? usedPath) && usedPath != null)
                        registeredFiles.Add(usedPath);
                }

                if (registeredFiles.Count > 0)
                {
                    activeSourcePath = registeredFiles[0];
                    api.Logger.Notification(
                        "[SwixyQuestBook] Registered {0} font file(s) (body '{1}', title '{2}'; e.g. {3})",
                        registeredFiles.Count,
                        BodyFamilyName,
                        TitleFamilyName,
                        activeSourcePath);
                }
                else
                {
                    api.Logger.Warning(
                        "[SwixyQuestBook] No Minecraft .ttf found to register. UI will fall back to system fonts. " +
                        "Expected assets/{0}/fonts/minecraft.ttf and MinecraftTitle.ttf.",
                        api.ModLoader.GetMod("swixyquestbook")?.Info?.ModID ?? "swixyquestbook");
                }
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[SwixyQuestBook] Font registration failed: {0}", ex.Message);
            }
        }

        /// <summary>Body UI font (minecraft.ttf / Minecraft Rus).</summary>
        public static CairoFont Create(double renderSize, double[] color, bool bold = false)
        {
            return CreateWithFamily(BodyFamilyName, renderSize, color, bold);
        }

        /// <summary>Title font (MinecraftTitle.ttf / Minecraft Five) for the top menu bar.</summary>
        public static CairoFont CreateTopMenu(double fitScale, double[] color)
        {
            double renderSize = QuestbookGuiLayout.TopMenuFontSize * fitScale;
            return CreateWithFamily(TitleFamilyName, renderSize, color, bold: false).WithRenderTwice();
        }

        /// <summary>Explicit title family at an arbitrary size (modals, section headers).</summary>
        public static CairoFont CreateTitle(double renderSize, double[] color)
        {
            return CreateWithFamily(TitleFamilyName, renderSize, color, bold: false);
        }

        private static CairoFont CreateWithFamily(string family, double renderSize, double[] color, bool bold)
        {
            // UnscaledFontsize is multiplied by RuntimeEnv.GUIScale inside CairoFont.SetupContext.
            float unscaled = (float)(renderSize / Math.Max(0.01, RuntimeEnv.GUIScale));
            // Minecraft faces are single-weight; requesting Bold often misses the face in Cairo.
            var font = new CairoFont
            {
                Fontname = family,
                UnscaledFontsize = unscaled,
                FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
                Color = color
            };
            return font;
        }

        private static IEnumerable<string> EnumerateCandidateFontFiles(ICoreClientAPI api, Mod? mod)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> YieldIfExists(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    yield break;
                string full = IOPath.GetFullPath(path);
                if (!seen.Add(full) || !File.Exists(full))
                    yield break;
                yield return full;
            }

            // 1) Mod folder / zip-extracted cache next to SourceIOPath.
            string? source = mod?.SourcePath
                ?? api.ModLoader.GetMod("swixyquestbook")?.SourcePath;
            if (!string.IsNullOrWhiteSpace(source) && Directory.Exists(source))
            {
                string assetsRoot = IOPath.Combine(source, "assets");
                if (Directory.Exists(assetsRoot))
                {
                    foreach (string pattern in new[] { "minecraft.ttf", "MinecraftTitle.ttf", "Minecraft*.ttf" })
                    {
                        foreach (string file in Directory.EnumerateFiles(
                                     assetsRoot,
                                     pattern,
                                     SearchOption.AllDirectories))
                        {
                            foreach (string p in YieldIfExists(file))
                                yield return p;
                        }
                    }
                }
            }

            // 2) Assets API (works when the pack is loaded as loose files or extracted).
            foreach (string fileName in BodyFontFileNames.Concat(TitleFontFileNames))
            {
                string? extracted = TryExtractAssetFont(api, $"fonts/{fileName}");
                foreach (string p in YieldIfExists(extracted))
                    yield return p;
            }

            // 3) Already-installed user fonts from a previous run of this mod.
            string userFonts = IOPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Fonts");
            if (Directory.Exists(userFonts))
            {
                foreach (string fileName in BodyFontFileNames.Concat(TitleFontFileNames))
                {
                    foreach (string p in YieldIfExists(IOPath.Combine(userFonts, fileName)))
                        yield return p;
                }
            }
        }

        private static string? TryExtractAssetFont(ICoreClientAPI api, string relativePath)
        {
            try
            {
                IAsset? asset = api.Assets.TryGet(new AssetLocation("swixyquestbook", relativePath))
                    ?? api.Assets.TryGet(new AssetLocation("game", relativePath));
                if (asset?.Data == null || asset.Data.Length == 0)
                    return null;

                string cacheDir = IOPath.Combine(GamePaths.Cache, "swixyquestbook", "fonts");
                Directory.CreateDirectory(cacheDir);
                string fileName = IOPath.GetFileName(relativePath);
                string outPath = IOPath.Combine(cacheDir, fileName);
                // Refresh when the packed asset is newer / different size.
                if (!File.Exists(outPath) || new FileInfo(outPath).Length != asset.Data.Length)
                    File.WriteAllBytes(outPath, asset.Data);
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
                return false;

            // Prefer a durable user-fonts copy so Cairo/fontconfig keeps finding the family
            // after restarts (same pattern as Vintage Story's own game fonts).
            string durable = TryInstallToUserFonts(path) ?? path;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    // FR_PRIVATE: available to this process without a global install prompt.
                    int added = AddFontResourceExW(durable, FrPrivate, IntPtr.Zero);
                    if (added == 0)
                    {
                        // Still may work if the file is already in the user font folder.
                        api.Logger.VerboseDebug(
                            "[SwixyQuestBook] AddFontResourceEx returned 0 for {0}", durable);
                    }
                }
                catch (Exception ex)
                {
                    api.Logger.Debug("[SwixyQuestBook] AddFontResourceEx failed for {0}: {1}", durable, ex.Message);
                }
            }

            usedPath = durable;
            return true;
        }

        private static string? TryInstallToUserFonts(string sourcePath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return null;

            try
            {
                string userFonts = IOPath.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Windows", "Fonts");
                Directory.CreateDirectory(userFonts);
                string dest = IOPath.Combine(userFonts, IOPath.GetFileName(sourcePath));
                if (!File.Exists(dest)
                    || new FileInfo(dest).Length != new FileInfo(sourcePath).Length)
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
}
