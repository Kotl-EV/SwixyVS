using Cairo;
using SwixyQuestBook.Helpers;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixyQuestBook.Gui
{
    /// <summary>
    /// Performance helpers: soft redraw, graph caches, font cache, viewport culling.
    /// </summary>
    public sealed partial class QuestbookDialog
    {
        private const string ContentDrawKey = "swixyquestbookContent";
        private const double GraphCullMargin = 48;

        private readonly Dictionary<long, CairoFont> montserratFontCache = new();
        private readonly Dictionary<long, CairoFont> topMenuFontCache = new();
        private readonly Dictionary<string, double> textWidthCache = new(System.StringComparer.Ordinal);
        private readonly Dictionary<int, QuestbookQuestNodeDefinition> graphNodeById = new();
        private readonly Dictionary<int, bool> graphNodeUnlocked = new();
        private readonly Dictionary<int, bool> graphNodeReady = new();
        private readonly Dictionary<int, QuestNodeVisualState> graphNodeVisual = new();
        private readonly List<int> graphParentScratch = new(8);

        private int graphCacheCategoryIndex = int.MinValue;
        private int graphCacheInventoryHash;
        private bool graphCacheAdmin;
        private bool graphCacheValid;
        private int lastWildcardCycleIndex = -1;
        private bool contentSurfaceDirty;

        /// <summary>
        /// Lightweight UI update: mutates state only. Dynamic content surface re-renders without rebuilding the composer.
        /// </summary>
        private void RequestContentRefresh()
        {
            contentSurfaceDirty = true;
        }

        private void FlushContentRefresh()
        {
            if (!contentSurfaceDirty)
            {
                return;
            }

            contentSurfaceDirty = false;
            TryRedrawContentSurface();
        }

        private void TryRedrawContentSurface()
        {
            if (SingleComposer == null)
            {
                return;
            }

            try
            {
                // Present in VintagestoryAPI: GuiComposer.GetCustomDraw + GuiElementCustomDraw.Redraw
                GuiElementCustomDraw? draw = SingleComposer.GetCustomDraw(ContentDrawKey);
                draw?.Redraw();
            }
            catch
            {
                // Fall back: next full compose path will rebuild if API shape differs.
                RequestComposeDialog();
            }
        }

        private void InvalidateGraphCache()
        {
            graphCacheValid = false;
        }

        private void EnsureGraphCache(QuestbookCategoryDefinition category)
        {
            EnsureInventorySnapshot();
            int invHash = inventorySnapshot.ContentHash;
            bool admin = adminData.IsAdminPanelOpen;

            if (graphCacheValid
                && graphCacheCategoryIndex == selectedCategoryIndex
                && graphCacheInventoryHash == invHash
                && graphCacheAdmin == admin
                && graphNodeById.Count == category.Nodes.Length)
            {
                return;
            }

            RebuildGraphCache(category, invHash, admin);
        }

        private void RebuildGraphCache(QuestbookCategoryDefinition category, int invHash, bool admin)
        {
            graphNodeById.Clear();
            graphNodeUnlocked.Clear();
            graphNodeReady.Clear();
            graphNodeVisual.Clear();

            foreach (QuestbookQuestNodeDefinition node in category.Nodes)
            {
                graphNodeById[node.Id] = node;
            }

            // Parent completion map for unlock checks.
            foreach (QuestbookQuestNodeDefinition node in category.Nodes)
            {
                bool unlocked = true;
                foreach (QuestbookQuestConnectionDefinition connection in category.Connections)
                {
                    if (connection.EndNodeId != node.Id)
                    {
                        continue;
                    }

                    if (!graphNodeById.TryGetValue(connection.StartNodeId, out QuestbookQuestNodeDefinition? parent)
                        || parent.State != QuestbookQuestNodeState.Completed)
                    {
                        unlocked = false;
                        break;
                    }
                }

                graphNodeUnlocked[node.Id] = unlocked;

                bool completed = node.State == QuestbookQuestNodeState.Completed;
                bool ready = false;
                if (!admin && !completed && unlocked && node.NodeType == QuestbookQuestNodeType.Quest)
                {
                    ready = IsNodeReadyToSubmitCached(node);
                }

                graphNodeReady[node.Id] = ready;

                QuestNodeVisualState visual = completed
                    ? QuestNodeVisualState.Completed
                    : unlocked
                        ? QuestNodeVisualState.Active
                        : QuestNodeVisualState.Inactive;

                if (admin)
                {
                    visual = QuestNodeVisualState.Active;
                }

                graphNodeVisual[node.Id] = visual;
            }

            graphCacheCategoryIndex = selectedCategoryIndex;
            graphCacheInventoryHash = invHash;
            graphCacheAdmin = admin;
            graphCacheValid = true;
        }

        private bool IsNodeReadyToSubmitCached(QuestbookQuestNodeDefinition node)
        {
            if (node.RequiredItems.Length == 0)
            {
                return false;
            }

            foreach (QuestbookQuestItemRequirement item in node.RequiredItems)
            {
                if (string.IsNullOrWhiteSpace(item.CollectibleCode) || item.Count <= 0)
                {
                    continue;
                }

                if (inventorySnapshot.Count(item.CollectibleCode) < item.Count)
                {
                    return false;
                }
            }

            return true;
        }

        private QuestbookQuestNodeDefinition? GetCachedNodeById(int nodeId)
        {
            return graphNodeById.TryGetValue(nodeId, out QuestbookQuestNodeDefinition? node) ? node : null;
        }

        private bool IsNodeUnlockedCached(int nodeId)
        {
            return graphNodeUnlocked.TryGetValue(nodeId, out bool unlocked) && unlocked;
        }

        private bool IsNodeReadyCached(int nodeId)
        {
            return graphNodeReady.TryGetValue(nodeId, out bool ready) && ready;
        }

        private QuestNodeVisualState GetNodeVisualStateCached(int nodeId)
        {
            return graphNodeVisual.TryGetValue(nodeId, out QuestNodeVisualState state)
                ? state
                : QuestNodeVisualState.Inactive;
        }

        private static bool IntersectsViewport(LayoutRect rect, LayoutRect viewport, double margin)
        {
            return rect.X + rect.Width >= viewport.X - margin
                && rect.Y + rect.Height >= viewport.Y - margin
                && rect.X <= viewport.X + viewport.Width + margin
                && rect.Y <= viewport.Y + viewport.Height + margin;
        }

        private static bool SegmentMayHitViewport(
            double x1, double y1, double x2, double y2,
            LayoutRect viewport, double margin)
        {
            double minX = System.Math.Min(x1, x2);
            double maxX = System.Math.Max(x1, x2);
            double minY = System.Math.Min(y1, y2);
            double maxY = System.Math.Max(y1, y2);
            return maxX >= viewport.X - margin
                && maxY >= viewport.Y - margin
                && minX <= viewport.X + viewport.Width + margin
                && minY <= viewport.Y + viewport.Height + margin;
        }

        private CairoFont GetMontserratFont(double renderSize, double[] color)
        {
            long key = BuildFontCacheKey(renderSize, color, topMenu: false);
            if (montserratFontCache.TryGetValue(key, out CairoFont? font))
            {
                return font;
            }

            font = new CairoFont(new FontConfig
            {
                Fontname = "Montserrat",
                UnscaledFontsize = (float)(renderSize / RuntimeEnv.GUIScale),
                FontWeight = FontWeight.Bold,
                Color = color
            });
            montserratFontCache[key] = font;
            return font;
        }

        private CairoFont GetTopMenuFontCached(double fitScale, double[] color)
        {
            double renderSize = QuestbookGuiLayout.TopMenuFontSize * fitScale;
            long key = BuildFontCacheKey(renderSize, color, topMenu: true);
            if (topMenuFontCache.TryGetValue(key, out CairoFont? font))
            {
                return font;
            }

            font = new CairoFont(new FontConfig
            {
                Fontname = "Montserrat-Bold",
                UnscaledFontsize = (float)(renderSize / RuntimeEnv.GUIScale),
                FontWeight = FontWeight.Normal,
                Color = color
            }).WithRenderTwice();
            topMenuFontCache[key] = font;
            return font;
        }

        private static long BuildFontCacheKey(double renderSize, double[] color, bool topMenu)
        {
            int sizeKey = (int)System.Math.Round(renderSize * 100);
            int r = color.Length > 0 ? (int)(color[0] * 255) : 0;
            int g = color.Length > 1 ? (int)(color[1] * 255) : 0;
            int b = color.Length > 2 ? (int)(color[2] * 255) : 0;
            int a = color.Length > 3 ? (int)(color[3] * 255) : 255;
            long key = sizeKey;
            key = (key << 9) ^ r;
            key = (key << 9) ^ g;
            key = (key << 9) ^ b;
            key = (key << 9) ^ a;
            if (topMenu)
            {
                key ^= 1L << 60;
            }

            return key;
        }

        private double MeasureTextWidthCached(CairoFont font, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            // Font instances are cached; identity + text is stable enough for this dialog.
            string key = font.GetHashCode().ToString("X") + "\n" + text;
            if (textWidthCache.TryGetValue(key, out double width))
            {
                return width;
            }

            width = font.GetTextExtents(text).XAdvance;
            if (textWidthCache.Count > 2048)
            {
                textWidthCache.Clear();
            }

            textWidthCache[key] = width;
            return width;
        }

        private void DrawConnectionLine(
            Context ctx,
            double startX,
            double startY,
            double endX,
            double endY,
            QuestNodeVisualState startState,
            QuestNodeVisualState endState,
            double graphScale)
        {
            // Multi-pass Cairo stroke that mimics the old line_*.png look:
            // dark rim → colored body → bright center ridge.
            GetConnectionLinePalette(startState, endState, out double[] rim, out double[] body, out double[] core);

            // Match original texture strip height (~GraphLineThickness).
            double thickness = System.Math.Max(6.0, QuestbookGuiLayout.GraphLineThickness * graphScale);

            ctx.Save();
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;
            ctx.Operator = Operator.Over;

            void StrokePass(double[] rgba, double widthScale)
            {
                ctx.SetSourceRGBA(rgba[0], rgba[1], rgba[2], rgba[3]);
                ctx.LineWidth = System.Math.Max(1.0, thickness * widthScale);
                ctx.MoveTo(startX, startY);
                ctx.LineTo(endX, endY);
                ctx.Stroke();
            }

            // Soft outer aura
            StrokePass([rim[0], rim[1], rim[2], rim[3] * 0.30], 1.45);
            // Hard dark rim
            StrokePass(rim, 1.12);
            // Main body
            StrokePass(body, 0.78);
            // Bright ridge
            StrokePass(core, 0.36);
            // Specular
            StrokePass([1.0, 1.0, 1.0, core[3] * 0.28], 0.16);

            ctx.Restore();
        }

        private static void GetConnectionLinePalette(
            QuestNodeVisualState startState,
            QuestNodeVisualState endState,
            out double[] rim,
            out double[] body,
            out double[] core)
        {
            if (startState == QuestNodeVisualState.Completed && endState == QuestNodeVisualState.Completed)
            {
                // Matches green completed theme of the book UI
                rim = [0.08, 0.22, 0.10, 0.90];
                body = [0.28, 0.78, 0.30, 0.96];
                core = [0.55, 0.98, 0.52, 0.95];
                return;
            }

            if (startState == QuestNodeVisualState.Inactive || endState == QuestNodeVisualState.Inactive)
            {
                // Dim inactive links
                rim = [0.10, 0.11, 0.13, 0.70];
                body = [0.32, 0.34, 0.37, 0.72];
                core = [0.48, 0.50, 0.53, 0.55];
                return;
            }

            // Active / available — cool light metal like line.png
            rim = [0.12, 0.14, 0.16, 0.88];
            body = [0.72, 0.76, 0.80, 0.94];
            core = [0.94, 0.96, 0.98, 0.92];
        }

        private void DisposePerfCaches()
        {
            foreach (CairoFont font in montserratFontCache.Values)
            {
                // CairoFont does not require dispose in VS, but clear refs.
            }

            montserratFontCache.Clear();
            topMenuFontCache.Clear();
            textWidthCache.Clear();
            graphNodeById.Clear();
            graphNodeUnlocked.Clear();
            graphNodeReady.Clear();
            graphNodeVisual.Clear();
            graphCacheValid = false;
        }
    }
}
