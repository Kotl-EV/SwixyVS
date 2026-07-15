using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixyQuestBook.Helpers
{
    public sealed class QuestbookTextureHelper : IDisposable
    {
        private const string ModId = "swixyquestbook";
        private readonly ICoreClientAPI api;
        private readonly Dictionary<string, ImageSurface?> surfaceCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ImageSurface?> gameSurfaceCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> loggedMissingAssets = new(StringComparer.OrdinalIgnoreCase);
        private ImageSurface? generatedMissingIcon;

        public QuestbookTextureHelper(ICoreClientAPI api)
        {
            this.api = api;
        }

        public ImageSurface? GetTexture(string textureFileName)
        {
            return LoadSurface($"textures/{textureFileName}");
        }

        public ImageSurface? GetIcon(string iconFileName)
        {
            if (string.IsNullOrWhiteSpace(iconFileName))
            {
                return GetOrCreateMissingIconSurface();
            }

            // Prefer icon/ domain path, then textures/ fallbacks used by older packs.
            ImageSurface? surface =
                LoadSurface($"icon/{iconFileName}")
                ?? LoadSurface($"textures/{iconFileName}")
                ?? LoadSurface($"textures/icons/{iconFileName}")
                ?? LoadSurface($"textures/gui/{iconFileName}");

            if (surface != null)
            {
                return surface;
            }

            // Always return a drawable placeholder so callers never spam missing-asset lookups.
            if (iconFileName.Contains("missing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(iconFileName, "icon_missing.png", StringComparison.OrdinalIgnoreCase))
            {
                return GetOrCreateMissingIconSurface();
            }

            return GetOrCreateMissingIconSurface();
        }

        public ImageSurface? GetGameTexture(string gameTexturePath)
        {
            if (string.IsNullOrWhiteSpace(gameTexturePath))
            {
                return null;
            }

            if (gameSurfaceCache.TryGetValue(gameTexturePath, out ImageSurface? cachedSurface))
            {
                return cachedSurface;
            }

            ImageSurface? surface = TryLoadGameSurface(gameTexturePath);
            gameSurfaceCache[gameTexturePath] = surface;
            return surface;
        }

        private ImageSurface? LoadSurface(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            if (surfaceCache.TryGetValue(assetPath, out ImageSurface? cachedSurface))
            {
                return cachedSurface;
            }

            string[] candidates =
            [
                assetPath.ToLowerInvariant(),
                assetPath
            ];

            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ImageSurface? surface = TryLoadSurface(candidate);
                if (surface != null)
                {
                    surfaceCache[assetPath] = surface;
                    return surface;
                }
            }

            // Cache miss so we do not re-query / re-log every frame.
            surfaceCache[assetPath] = null;
            if (loggedMissingAssets.Add(assetPath))
            {
                api.Logger.Warning($"[SwixyQuestBook] Asset not found (once): {ModId}:{assetPath}");
            }

            return null;
        }

        private ImageSurface? TryLoadGameSurface(string gameTexturePath)
        {
            try
            {
                IAsset? asset = api.Assets.TryGet(new AssetLocation("game", gameTexturePath));
                if (asset == null || asset.Data == null || asset.Data.Length == 0)
                {
                    return null;
                }

                using BitmapExternal bitmap = api.Render.BitmapCreateFromPng(asset.Data);
                RepairAlphaFringe(bitmap);
                return GuiElement.getImageSurfaceFromAsset(bitmap);
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SwixyQuestBook] Game texture miss game:{gameTexturePath} ({ex.Message})");
                return null;
            }
        }

        private ImageSurface? TryLoadSurface(string assetPath)
        {
            try
            {
                // TryGet avoids exception spam for optional icons.
                IAsset? asset = api.Assets.TryGet(new AssetLocation(ModId, assetPath));
                if (asset == null || asset.Data == null || asset.Data.Length == 0)
                {
                    // Some installs register assets under a legacy domain.
                    asset = api.Assets.TryGet(new AssetLocation("questbook", assetPath));
                }

                if (asset == null || asset.Data == null || asset.Data.Length == 0)
                {
                    return null;
                }

                using BitmapExternal bitmap = api.Render.BitmapCreateFromPng(asset.Data);
                RepairAlphaFringe(bitmap);
                return GuiElement.getImageSurfaceFromAsset(bitmap);
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SwixyQuestBook] Asset miss {ModId}:{assetPath} ({ex.Message})");
                return null;
            }
        }

        private ImageSurface GetOrCreateMissingIconSurface()
        {
            if (generatedMissingIcon != null)
            {
                return generatedMissingIcon;
            }

            const int size = 32;
            generatedMissingIcon = new ImageSurface(Format.Argb32, size, size);
            using (var ctx = new Context(generatedMissingIcon))
            {
                ctx.SetSourceRGBA(0.12, 0.13, 0.15, 0.95);
                ctx.Rectangle(0, 0, size, size);
                ctx.Fill();

                ctx.SetSourceRGBA(0.55, 0.58, 0.62, 1.0);
                ctx.LineWidth = 2;
                ctx.Rectangle(3, 3, size - 6, size - 6);
                ctx.Stroke();

                ctx.SetSourceRGBA(0.75, 0.35, 0.32, 1.0);
                ctx.LineWidth = 2.5;
                ctx.MoveTo(9, 9);
                ctx.LineTo(size - 9, size - 9);
                ctx.MoveTo(size - 9, 9);
                ctx.LineTo(9, size - 9);
                ctx.Stroke();
            }

            return generatedMissingIcon;
        }

        /// <summary>
        /// Premultiplies RGB by alpha to remove white halos on soft edges.
        /// Bitmap and Cairo surfaces share the same BGRA pixel layout.
        /// </summary>
        private static unsafe void RepairAlphaFringe(BitmapExternal bitmap)
        {
            uint* pixels = (uint*)bitmap.PixelsPtrAndLock.ToPointer();
            int count = bitmap.Width * bitmap.Height;

            for (int i = 0; i < count; i++)
            {
                uint pixel = pixels[i];
                byte b = (byte)(pixel & 0xFF);
                byte g = (byte)((pixel >> 8) & 0xFF);
                byte r = (byte)((pixel >> 16) & 0xFF);
                byte a = (byte)((pixel >> 24) & 0xFF);

                if (a == 0)
                {
                    pixels[i] = 0;
                    continue;
                }

                if (a == 255)
                {
                    continue;
                }

                pixels[i] =
                    ((uint)a << 24)
                    | ((uint)(r * a / 255) << 16)
                    | ((uint)(g * a / 255) << 8)
                    | (uint)(b * a / 255);
            }
        }

        public void Dispose()
        {
            foreach (ImageSurface? surface in surfaceCache.Values)
            {
                surface?.Dispose();
            }

            surfaceCache.Clear();

            foreach (ImageSurface? surface in gameSurfaceCache.Values)
            {
                surface?.Dispose();
            }

            gameSurfaceCache.Clear();
            loggedMissingAssets.Clear();

            generatedMissingIcon?.Dispose();
            generatedMissingIcon = null;
        }
    }
}
