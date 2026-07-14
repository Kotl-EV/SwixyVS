using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixyQuestBook.Helpers
{
    public sealed class QuestbookTextureHelper : IDisposable
    {
        private const string ModId = "swixyquestbook";
        private readonly ICoreClientAPI api;
        private readonly Dictionary<string, ImageSurface> surfaceCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ImageSurface> gameSurfaceCache = new(StringComparer.OrdinalIgnoreCase);

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
            return LoadSurface($"icon/{iconFileName}");
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
            if (surface != null)
            {
                gameSurfaceCache[gameTexturePath] = surface;
            }

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

            api.Logger.Warning($"[SwixyQuestBook] Asset not found: {ModId}:{assetPath}");
            return null;
        }

        private ImageSurface? TryLoadGameSurface(string gameTexturePath)
        {
            try
            {
                IAsset asset = api.Assets.Get(new AssetLocation("game", gameTexturePath));
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
                IAsset asset = api.Assets.Get(new AssetLocation(ModId, assetPath));
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
            foreach (ImageSurface surface in surfaceCache.Values)
            {
                surface.Dispose();
            }

            surfaceCache.Clear();

            foreach (ImageSurface surface in gameSurfaceCache.Values)
            {
                surface.Dispose();
            }

            gameSurfaceCache.Clear();
        }
    }
}