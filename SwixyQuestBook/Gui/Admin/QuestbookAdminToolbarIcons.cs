using Cairo;
using System;
using Vintagestory.API.Client;

namespace SwixyQuestBook.Gui
{
    public sealed partial class QuestbookDialog
    {
        private enum AdminToolbarIcon
        {
            Select,
            NewQuest,
            Link,
            Delete,
            Save,
            Clear,
            Grid,
            Close,
            Branches,
            Quests,
            Add,
            Rename,
            EditBranch,
            Image,
            Editor,
            Start,
            Quest,
            Checkpoint,
            Kill
        }

        private static void AddRoundedRectanglePath(Cairo.Context ctx, double x, double y, double width, double height, double radius)
        {
            radius = Math.Min(radius, Math.Min(width, height) / 2);
            if (radius <= 0)
            {
                ctx.Rectangle(x, y, width, height);
                return;
            }

            double right = x + width;
            double bottom = y + height;
            ctx.NewPath();
            ctx.MoveTo(x + radius, y);
            ctx.LineTo(right - radius, y);
            ctx.Arc(right - radius, y + radius, radius, -Math.PI / 2, 0);
            ctx.LineTo(right, bottom - radius);
            ctx.Arc(right - radius, bottom - radius, radius, 0, Math.PI / 2);
            ctx.LineTo(x + radius, bottom);
            ctx.Arc(x + radius, bottom - radius, radius, Math.PI / 2, Math.PI);
            ctx.LineTo(x, y + radius);
            ctx.Arc(x + radius, y + radius, radius, Math.PI, 3 * Math.PI / 2);
            ctx.ClosePath();
        }

        private static void FillRoundedRectangle(Cairo.Context ctx, double x, double y, double width, double height, double radius, double[] color)
        {
            AddRoundedRectanglePath(ctx, x, y, width, height, radius);
            ctx.SetSourceRGBA(color[0], color[1], color[2], color[3]);
            ctx.Fill();
        }

        private static void StrokeRoundedRectangle(Cairo.Context ctx, double x, double y, double width, double height, double radius, double lineWidth, double[] color)
        {
            AddRoundedRectanglePath(ctx, x, y, width, height, radius);
            ctx.SetSourceRGBA(color[0], color[1], color[2], color[3]);
            ctx.LineWidth = lineWidth;
            ctx.Stroke();
        }

        private void DrawAdminTileButton(
            Cairo.Context ctx,
            double fitScale,
            LayoutRect area,
            AdminToolbarIcon icon,
            bool active,
            bool hovered,
            double[]? accentColor = null)
        {
            DrawAdminTileButton(ctx, fitScale, area, icon, active, hovered, accentColor, label: null);
        }

        private void DrawAdminTileButton(
            Cairo.Context ctx,
            double fitScale,
            LayoutRect area,
            AdminToolbarIcon icon,
            bool active,
            bool hovered,
            double[]? accentColor,
            string? label,
            bool labelOnRight = false)
        {
            // Group 1140.svg: 1px stroke on #171717 tiles; active/hover only recolor stroke + glyphs.
            double radius = QuestbookGuiLayout.AdminTileCornerRadius * fitScale;
            double borderWidth = Math.Max(1.0, 1.0 * fitScale);
            double[] accent = accentColor ?? QuestbookGuiLayout.AdminTileActiveContentColor;

            double[] background = hovered && !active
                ? QuestbookGuiLayout.AdminTileHoverBackgroundColor
                : QuestbookGuiLayout.AdminTileBackgroundColor;

            double[] border = active || hovered
                ? accent
                : QuestbookGuiLayout.AdminTileBorderColor;

            FillRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius, background);
            StrokeRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius, borderWidth, border);

            bool hasLabel = !string.IsNullOrWhiteSpace(label);
            double[] iconColor = active || hovered
                ? accent
                : QuestbookGuiLayout.AdminTileIdleContentColor;

            // Wide sidebar-style button: [icon]  Label  (CREATE / EDIT / DELETE BRANCH)
            if (hasLabel && labelOnRight)
            {
                // SVG action rows: icon ~x28–44 in 288-wide, label starts ~x62.
                double padX = Math.Max(10 * fitScale, area.Height * 0.28);
                double iconSize = Math.Min(area.Height * 0.58, 22 * fitScale);
                double iconX = area.X + padX;
                double iconY = area.Y + ((area.Height - iconSize) / 2);
                DrawAdminToolbarIcon(ctx, icon, iconX, iconY, iconSize, iconColor);

                double fontSize = Math.Clamp(area.Height * 0.36, 11 * fitScale, 14 * fitScale);
                CairoFont font = CreateMontserratFont(fontSize, iconColor);
                string text = label!.Trim();
                double textX = iconX + iconSize + (12 * fitScale);
                double maxTextWidth = area.X + area.Width - padX - textX;
                while (text.Length > 1 && MeasureTextWidth(font, text) > maxTextWidth)
                    text = text[..^1];

                DrawText(
                    ctx,
                    font,
                    text,
                    textX,
                    GetTextBaselineY(font, area.Y, area.Height, area.Height));
                return;
            }

            // Mode tiles (QUESTS / BRANCHES): icon in upper band, label along bottom — SVG 139×69.
            double iconSizeStacked = hasLabel
                ? Math.Min(area.Width * 0.42, area.Height * 0.42)
                : Math.Min(area.Width, area.Height) * QuestbookGuiLayout.AdminTileIconScale;

            double contentTop = area.Y + (hasLabel ? area.Height * 0.10 : (area.Height - iconSizeStacked) / 2);
            double iconXStacked = area.X + ((area.Width - iconSizeStacked) / 2);
            double iconYStacked = contentTop;

            DrawAdminToolbarIcon(ctx, icon, iconXStacked, iconYStacked, iconSizeStacked, iconColor);

            if (hasLabel)
            {
                double fontSize = Math.Clamp(area.Height * 0.18, 10 * fitScale, 13 * fitScale);
                CairoFont font = CreateMontserratFont(fontSize, iconColor);
                string text = label!.Trim();
                double textWidth = MeasureTextWidth(font, text);
                double maxW = area.Width - (10 * fitScale);
                while (text.Length > 1 && textWidth > maxW)
                {
                    text = text[..^1];
                    textWidth = MeasureTextWidth(font, text);
                }

                double textX = area.X + ((area.Width - textWidth) / 2);
                // Pixel-label band near bottom of the 69px tile (SVG paths ~y48–62).
                double textY = area.Y + area.Height * 0.82;
                DrawText(ctx, font, text, textX, textY);
            }
        }

        /// <summary>
        /// Group 1141.svg EXIT — full-width tile, centered red label, no icon.
        /// </summary>
        private void DrawAdminCenteredTextButton(
            Cairo.Context ctx,
            double fitScale,
            LayoutRect area,
            string label,
            bool hovered,
            double[] accentColor)
        {
            double radius = QuestbookGuiLayout.AdminTileCornerRadius * fitScale;
            double borderWidth = Math.Max(1.0, 1.0 * fitScale);
            double[] background = hovered
                ? QuestbookGuiLayout.AdminTileHoverBackgroundColor
                : QuestbookGuiLayout.AdminTileBackgroundColor;
            double[] border = hovered ? accentColor : QuestbookGuiLayout.AdminTileBorderColor;
            double[] textColor = hovered ? accentColor : accentColor;

            FillRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius, background);
            StrokeRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius, borderWidth, border);

            string text = (label ?? string.Empty).Trim();
            if (text.Length == 0)
                return;

            double fontSize = Math.Clamp(area.Height * 0.38, 12 * fitScale, 15 * fitScale);
            CairoFont font = CreateMontserratFont(fontSize, textColor);
            double textWidth = MeasureTextWidth(font, text);
            double maxW = area.Width - (16 * fitScale);
            while (text.Length > 1 && textWidth > maxW)
            {
                text = text[..^1];
                textWidth = MeasureTextWidth(font, text);
            }

            double textX = area.X + ((area.Width - textWidth) / 2);
            DrawText(
                ctx,
                font,
                text,
                textX,
                GetTextBaselineY(font, area.Y, area.Height, area.Height));
        }

        private static string GetAdminToolbarLabel(AdminToolbarIcon icon)
        {
            return icon switch
            {
                AdminToolbarIcon.Select => QuestbookLang.GetLocal("admin.icon.select"),
                AdminToolbarIcon.NewQuest => QuestbookLang.GetLocal("admin.icon.new"),
                AdminToolbarIcon.Link => QuestbookLang.GetLocal("admin.icon.link"),
                AdminToolbarIcon.Delete => QuestbookLang.GetLocal("admin.icon.delete"),
                AdminToolbarIcon.Save => QuestbookLang.GetLocal("admin.icon.save"),
                AdminToolbarIcon.Clear => QuestbookLang.GetLocal("admin.icon.clear"),
                AdminToolbarIcon.Grid => QuestbookLang.GetLocal("admin.icon.grid"),
                AdminToolbarIcon.Close => QuestbookLang.GetLocal("admin.icon.close"),
                AdminToolbarIcon.Branches => QuestbookLang.GetLocal("admin.icon.branches"),
                AdminToolbarIcon.Quests => QuestbookLang.GetLocal("admin.icon.quests"),
                AdminToolbarIcon.Add => QuestbookLang.GetLocal("admin.icon.add"),
                AdminToolbarIcon.Rename => QuestbookLang.GetLocal("admin.icon.rename"),
                AdminToolbarIcon.EditBranch => QuestbookLang.GetLocal("admin.icon.rename"),
                AdminToolbarIcon.Image => QuestbookLang.GetLocal("admin.icon.image"),
                AdminToolbarIcon.Editor => QuestbookLang.GetLocal("admin.icon.editor"),
                AdminToolbarIcon.Start => QuestbookLang.GetLocal("admin.icon.start"),
                AdminToolbarIcon.Quest => QuestbookLang.GetLocal("admin.icon.quest"),
                AdminToolbarIcon.Checkpoint => QuestbookLang.GetLocal("admin.icon.checkpoint"),
                AdminToolbarIcon.Kill => QuestbookLang.GetLocal("admin.icon.kill"),
                _ => string.Empty
            };
        }

        private static void SetIconStroke(Cairo.Context ctx, double[] color, double lineWidth)
        {
            ctx.SetSourceRGBA(color[0], color[1], color[2], color[3]);
            ctx.LineWidth = lineWidth;
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;
        }

        private static void SetIconFill(Cairo.Context ctx, double[] color, double alphaScale = 1.0)
        {
            double a = color.Length > 3 ? color[3] * alphaScale : alphaScale;
            ctx.SetSourceRGBA(color[0], color[1], color[2], a);
        }

        private static void DrawAdminToolbarIcon(Cairo.Context ctx, AdminToolbarIcon icon, double x, double y, double size, double[] color)
        {
            // Slightly thinner stroke + smaller pad leaves room for fine detail.
            double stroke = Math.Max(1.4, size * 0.078);
            double pad = size * 0.10;

            switch (icon)
            {
                case AdminToolbarIcon.Select:
                    DrawSelectIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.NewQuest:
                    DrawNewQuestIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Add:
                    DrawCreateDocumentIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Link:
                    DrawLinkIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Delete:
                    DrawTrashIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Save:
                    DrawSaveIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Clear:
                    DrawClearIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Grid:
                    DrawGridIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Close:
                    DrawCloseIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Branches:
                    DrawBranchesIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Quests:
                    DrawQuestsIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Rename:
                case AdminToolbarIcon.EditBranch:
                    DrawPencilIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Image:
                    DrawImageIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Editor:
                    DrawEditorIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Start:
                    DrawStartIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Quest:
                    DrawQuestIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Checkpoint:
                    DrawCheckpointIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.Kill:
                    DrawKillFlagGlyph(ctx, x + pad, y + pad, size - (pad * 2), color, stroke);
                    break;
            }
        }

        private static void FillDiamond(Cairo.Context ctx, double cx, double cy, double r, double[] color, double fillA, double stroke, bool strokeOutline)
        {
            SetIconFill(ctx, color, fillA);
            ctx.MoveTo(cx, cy - r);
            ctx.LineTo(cx + r, cy);
            ctx.LineTo(cx, cy + r);
            ctx.LineTo(cx - r, cy);
            ctx.ClosePath();
            ctx.Fill();
            if (strokeOutline)
            {
                SetIconStroke(ctx, color, stroke);
                ctx.MoveTo(cx, cy - r);
                ctx.LineTo(cx + r, cy);
                ctx.LineTo(cx, cy + r);
                ctx.LineTo(cx - r, cy);
                ctx.ClosePath();
                ctx.Stroke();
            }
        }

        private static void FillNodeRing(Cairo.Context ctx, double nx, double ny, double r, double[] color, double stroke, double fillA = 0.28)
        {
            SetIconFill(ctx, color, fillA);
            ctx.Arc(nx, ny, r, 0, Math.PI * 2);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke);
            ctx.Arc(nx, ny, r, 0, Math.PI * 2);
            ctx.Stroke();
            SetIconFill(ctx, color, 0.55);
            ctx.Arc(nx, ny, r * 0.38, 0, Math.PI * 2);
            ctx.Fill();
        }

        // ── SELECT (Group 1143.svg crosshair / 4-way move at 27–45, 93–111) ─
        private static void DrawSelectIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            BeginSvgIconMap(ctx, x, y, size, 27, 93, 18, 18, out double s);
            SetIconStroke(ctx, color, SvgStroke(s));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Vertical axis + arrow heads
            ctx.MoveTo(36, 111);
            ctx.LineTo(36, 102);
            ctx.MoveTo(33, 108);
            ctx.LineTo(36, 111);
            ctx.LineTo(39, 108);
            ctx.MoveTo(36, 102);
            ctx.LineTo(36, 93);
            ctx.MoveTo(39, 96);
            ctx.LineTo(36, 93);
            ctx.LineTo(33, 96);
            // Horizontal axis + arrow heads
            ctx.MoveTo(36, 102);
            ctx.LineTo(27, 102);
            ctx.MoveTo(36, 102);
            ctx.LineTo(45, 102);
            ctx.MoveTo(30, 99);
            ctx.LineTo(27, 102);
            ctx.LineTo(30, 105);
            ctx.MoveTo(42, 105);
            ctx.LineTo(45, 102);
            ctx.LineTo(42, 99);
            ctx.Stroke();
            ctx.Restore();
        }

        // ── NEW QUEST (Group 733.svg 28×28 — folder + plus, exact path) ───
        private static void DrawNewQuestIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            // Group 733.svg viewBox 0 0 28 28, stroke-width 2, round caps/joins.
            BeginSvgIconMap(ctx, x, y, size, 5, 6, 18, 16, out double s);
            SetIconStroke(ctx, color, SvgStroke(s));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Right top flap: M13 6 H17.8 C… V11 L13 11
            ctx.MoveTo(13, 6);
            ctx.LineTo(17.8002, 6);
            ctx.CurveTo(18.9203, 6, 19.4801, 6, 19.9079, 6.21799);
            ctx.CurveTo(20.2842, 6.40973, 20.5905, 6.71547, 20.7822, 7.0918);
            ctx.CurveTo(21, 7.5192, 21, 8.07899, 21, 9.19691);
            ctx.LineTo(21, 11.0002);
            ctx.LineTo(13.0001, 11.0001);
            ctx.Stroke();

            // Left top flap: M13 6 H8.2 C… V11
            ctx.MoveTo(13, 6);
            ctx.LineTo(8.2002, 6);
            ctx.CurveTo(7.08009, 6, 6.51962, 6, 6.0918, 6.21799);
            ctx.CurveTo(5.71547, 6.40973, 5.40973, 6.71547, 5.21799, 7.0918);
            ctx.CurveTo(5, 7.51962, 5, 8.08009, 5, 9.2002);
            ctx.LineTo(5, 11);
            ctx.Stroke();

            // Tab spine: M13 6 L13 11
            ctx.MoveTo(13, 6);
            ctx.LineTo(13.0001, 11.0001);
            ctx.Stroke();

            // Body left + mid shelf: M5 11 V17; M5 11 L13 11
            ctx.MoveTo(5, 11);
            ctx.LineTo(5, 17);
            ctx.MoveTo(5, 11);
            ctx.LineTo(13.0001, 11.0001);
            ctx.Stroke();

            // Bottom body: M5 17 V18.8 C… H13 L13 11
            ctx.MoveTo(5, 17);
            ctx.LineTo(5, 18.8002);
            ctx.CurveTo(5, 19.9203, 5, 20.4801, 5.21799, 20.9079);
            ctx.CurveTo(5.40973, 21.2842, 5.71547, 21.5905, 6.0918, 21.7822);
            ctx.CurveTo(6.5192, 22, 7.07899, 22, 8.19691, 22);
            ctx.LineTo(13.0002, 22);
            ctx.LineTo(13.0001, 11.0001);
            ctx.Stroke();

            // Shelf line: M5 17 H13
            ctx.MoveTo(5, 17);
            ctx.LineTo(13, 17);
            ctx.Stroke();

            // Plus: M17 18 H23 + vertical line (20,15)–(20,21)
            ctx.MoveTo(17, 18);
            ctx.LineTo(23, 18);
            ctx.MoveTo(20, 21);
            ctx.LineTo(20, 15);
            ctx.Stroke();
            ctx.Restore();
        }

        private static void DrawPlusBadgeIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double cx = x + size / 2;
            double cy = y + size / 2;
            double r = (size - pad * 2) * 0.48;
            SetIconFill(ctx, color, 0.14);
            ctx.Arc(cx, cy, r, 0, Math.PI * 2);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke);
            ctx.Arc(cx, cy, r, 0, Math.PI * 2);
            ctx.Stroke();
            SetIconStroke(ctx, color, stroke * 0.65);
            ctx.Arc(cx, cy, r * 0.78, 0, Math.PI * 2);
            ctx.Stroke();
            double half = r * 0.52;
            SetIconStroke(ctx, color, stroke * 1.15);
            ctx.MoveTo(cx - half, cy);
            ctx.LineTo(cx + half, cy);
            ctx.MoveTo(cx, cy - half);
            ctx.LineTo(cx, cy + half);
            ctx.Stroke();
        }

        /// <summary>
        /// Maps Group 1140.svg design units into an icon square.
        /// <paramref name="designMinX"/>/<paramref name="designMinY"/> are the top-left of the glyph bbox in SVG space.
        /// </summary>
        private static void BeginSvgIconMap(
            Cairo.Context ctx,
            double x,
            double y,
            double size,
            double designMinX,
            double designMinY,
            double designWidth,
            double designHeight,
            out double scale)
        {
            // Fit the SVG glyph bbox into the icon square with a little padding.
            double pad = size * 0.06;
            double avail = size - pad * 2;
            scale = Math.Min(avail / designWidth, avail / designHeight);
            double drawW = designWidth * scale;
            double drawH = designHeight * scale;
            double ox = x + (size - drawW) / 2 - designMinX * scale;
            double oy = y + (size - drawH) / 2 - designMinY * scale;
            ctx.Save();
            ctx.Translate(ox, oy);
            ctx.Scale(scale, scale);
        }

        private static double SvgStroke(double scale, double designStroke = 2.0)
        {
            // Keep visual stroke ~design 2px after scaling into icon size.
            return Math.Max(1.2, designStroke);
        }

        /// <summary>Group 1140.svg CREATE — document + plus at (28–44, 94–110).</summary>
        private static void DrawCreateDocumentIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            // SVG path is stroke-width 2 around rounded rect 28,94 → 44,110 with plus through (36,102).
            BeginSvgIconMap(ctx, x, y, size, 28, 94, 16, 16, out double s);
            SetIconStroke(ctx, color, SvgStroke(s));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Rounded document (from SVG cubic-rounded path approx).
            double left = 28, top = 94, w = 16, h = 16;
            double rx = 1.2;
            AddRoundedRectanglePath(ctx, left, top, w, h, rx);
            ctx.Stroke();

            // Plus: H 32–40 at y102, V 98–106 at x36
            ctx.MoveTo(32, 102);
            ctx.LineTo(40, 102);
            ctx.MoveTo(36, 98);
            ctx.LineTo(36, 106);
            ctx.Stroke();
            ctx.Restore();
        }

        // ── LINK (Group 732.svg 28×28 — two rings on a diagonal) ──────────
        private static void DrawLinkIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            // Group 732.svg: circles r=3 at (8,8) and (20,20), link along the diagonal.
            BeginSvgIconMap(ctx, x, y, size, 5, 5, 18, 18, out double s);
            SetIconStroke(ctx, color, SvgStroke(s));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Lower-right ring (center 20,20 r=3)
            ctx.NewPath();
            ctx.Arc(20, 20, 3, 0, Math.PI * 2);
            ctx.Stroke();

            // Upper-left ring (center 8,8 r=3)
            ctx.NewPath();
            ctx.Arc(8, 8, 3, 0, Math.PI * 2);
            ctx.Stroke();

            // Diagonal link between ring edges (SVG 10.12,10.12 → 17.88,17.88)
            ctx.MoveTo(10.1213, 10.1213);
            ctx.LineTo(17.8787, 17.8787);
            ctx.Stroke();
            ctx.Restore();
        }

        // ── Trash can (Group 1140.svg DELETE BRANCH path, y≈181–199) ──────
        private static void DrawTrashIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            // SVG: handle 30–42 @ y181–184, lid 28–44 @ y184, body to y199, ribs at x30/34/38.
            BeginSvgIconMap(ctx, x, y, size, 28, 181, 16, 18, out double s);
            SetIconStroke(ctx, color, SvgStroke(s));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Handle arch above lid
            ctx.MoveTo(32, 184);
            ctx.LineTo(32, 182);
            ctx.CurveTo(32, 181, 40, 181, 40, 182);
            ctx.LineTo(40, 184);
            ctx.Stroke();

            // Lid line with side flares
            ctx.MoveTo(28, 184);
            ctx.LineTo(44, 184);
            ctx.Stroke();

            // Body: rounded bottom rectangle 30,184 → 42,199
            ctx.MoveTo(30, 184);
            ctx.LineTo(30, 195.8);
            ctx.CurveTo(30, 197.48, 30, 199, 33.2, 199);
            ctx.LineTo(38.8, 199);
            ctx.CurveTo(42, 199, 42, 197.48, 42, 195.8);
            ctx.LineTo(42, 184);
            ctx.Stroke();

            // Ribs
            ctx.MoveTo(30, 188);
            ctx.LineTo(30, 195);
            ctx.MoveTo(34, 188);
            ctx.LineTo(34, 195);
            ctx.MoveTo(38, 188);
            ctx.LineTo(38, 195);
            ctx.Stroke();
            ctx.Restore();
        }

        // ── Floppy disk save ──────────────────────────────────────────────
        private static void DrawSaveIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad;
            double top = y + pad;
            double w = size - pad * 2;
            double h = size - pad * 2;
            double cut = w * 0.16;

            SetIconFill(ctx, color, 0.22);
            ctx.MoveTo(left, top + cut);
            ctx.LineTo(left + cut, top);
            ctx.LineTo(left + w, top);
            ctx.LineTo(left + w, top + h);
            ctx.LineTo(left, top + h);
            ctx.ClosePath();
            ctx.Fill();
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(left, top + cut);
            ctx.LineTo(left + cut, top);
            ctx.LineTo(left + w, top);
            ctx.LineTo(left + w, top + h);
            ctx.LineTo(left, top + h);
            ctx.ClosePath();
            ctx.Stroke();

            // Metal shutter on top
            SetIconFill(ctx, color, 0.18);
            ctx.Rectangle(left + w * 0.22, top, w * 0.42, h * 0.14);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.75);
            ctx.Rectangle(left + w * 0.22, top, w * 0.42, h * 0.14);
            ctx.Stroke();

            // Label sticker with write lines
            double ly1 = top + h * 0.18;
            double ly2 = top + h * 0.46;
            SetIconFill(ctx, color, 0.38);
            ctx.Rectangle(left + w * 0.16, ly1, w * 0.68, ly2 - ly1);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.75);
            ctx.Rectangle(left + w * 0.16, ly1, w * 0.68, ly2 - ly1);
            ctx.Stroke();
            SetIconStroke(ctx, color, stroke * 0.55);
            ctx.MoveTo(left + w * 0.24, ly1 + (ly2 - ly1) * 0.35);
            ctx.LineTo(left + w * 0.76, ly1 + (ly2 - ly1) * 0.35);
            ctx.MoveTo(left + w * 0.24, ly1 + (ly2 - ly1) * 0.65);
            ctx.LineTo(left + w * 0.62, ly1 + (ly2 - ly1) * 0.65);
            ctx.Stroke();

            // Bottom metal door + hole
            double sy = top + h * 0.56;
            SetIconFill(ctx, color, 0.16);
            ctx.Rectangle(left + w * 0.20, sy, w * 0.60, h * 0.30);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.85);
            ctx.Rectangle(left + w * 0.20, sy, w * 0.60, h * 0.30);
            ctx.Stroke();
            ctx.MoveTo(left + w * 0.54, sy + h * 0.05);
            ctx.LineTo(left + w * 0.54, sy + h * 0.25);
            ctx.Stroke();
            SetIconFill(ctx, color, 0.55);
            ctx.Arc(left + w * 0.34, sy + h * 0.15, size * 0.035, 0, Math.PI * 2);
            ctx.Fill();
        }

        // ── Eraser / reset ────────────────────────────────────────────────
        private static void DrawClearIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double cx = x + size / 2;
            double cy = y + size * 0.46;
            ctx.Save();
            ctx.Translate(cx, cy);
            ctx.Rotate(-0.48);

            double ew = size * 0.66;
            double eh = size * 0.36;
            SetIconFill(ctx, color, 0.24);
            AddRoundedRectanglePath(ctx, -ew / 2, -eh / 2, ew, eh, eh * 0.18);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke);
            AddRoundedRectanglePath(ctx, -ew / 2, -eh / 2, ew, eh, eh * 0.18);
            ctx.Stroke();

            // Ferrule band + metal tip section
            SetIconFill(ctx, color, 0.35);
            ctx.Rectangle(ew * 0.02, -eh / 2, ew * 0.16, eh);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.75);
            ctx.MoveTo(ew * 0.02, -eh / 2);
            ctx.LineTo(ew * 0.02, eh / 2);
            ctx.MoveTo(ew * 0.18, -eh / 2);
            ctx.LineTo(ew * 0.18, eh / 2);
            ctx.Stroke();

            // Rubber end ridges
            SetIconStroke(ctx, color, stroke * 0.55);
            for (int i = 0; i < 3; i++)
            {
                double rx = -ew * 0.42 + i * ew * 0.08;
                ctx.MoveTo(rx, -eh * 0.28);
                ctx.LineTo(rx, eh * 0.28);
            }
            ctx.Stroke();
            ctx.Restore();

            // Erase trails + dust
            SetIconStroke(ctx, color, stroke * 0.7);
            double baseY = y + size - pad - size * 0.06;
            ctx.MoveTo(x + pad, baseY);
            ctx.LineTo(x + size * 0.48, baseY);
            ctx.MoveTo(x + pad + size * 0.04, baseY - size * 0.09);
            ctx.LineTo(x + size * 0.40, baseY - size * 0.09);
            ctx.MoveTo(x + pad + size * 0.08, baseY - size * 0.17);
            ctx.LineTo(x + size * 0.30, baseY - size * 0.17);
            ctx.Stroke();
            SetIconFill(ctx, color, 0.55);
            ctx.Arc(x + size * 0.52, baseY - size * 0.02, size * 0.025, 0, Math.PI * 2);
            ctx.Arc(x + size * 0.60, baseY - size * 0.08, size * 0.02, 0, Math.PI * 2);
            ctx.Arc(x + size * 0.56, baseY - size * 0.14, size * 0.018, 0, Math.PI * 2);
            ctx.Fill();
        }

        // ── GRID (Group 1143.svg 3×3 cell grid at 28–44, 226–242) ────────
        private static void DrawGridIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            BeginSvgIconMap(ctx, x, y, size, 28, 226, 16, 16, out double s);
            SetIconStroke(ctx, color, Math.Max(1.0, 1.0));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Outer frame
            ctx.Rectangle(28, 226, 16, 16);
            ctx.Stroke();

            // Verticals at x=32, 36, 40
            ctx.MoveTo(32, 241.5);
            ctx.LineTo(32, 226.5);
            ctx.MoveTo(36, 226.5);
            ctx.LineTo(36, 241.5);
            ctx.MoveTo(40, 226.5);
            ctx.LineTo(40, 241.5);
            // Horizontals at y=230, 234, 238
            ctx.MoveTo(28.5, 230);
            ctx.LineTo(43.5, 230);
            ctx.MoveTo(28.5, 234);
            ctx.LineTo(43.5, 234);
            ctx.MoveTo(28.5, 238);
            ctx.LineTo(43.5, 238);
            ctx.Stroke();

            // Checker dots (SVG 2×2 fills)
            SetIconFill(ctx, color, 1.0);
            ctx.Rectangle(32, 230, 2, 2);
            ctx.Fill();
            ctx.Rectangle(34, 232, 2, 2);
            ctx.Fill();
            SetIconFill(ctx, color, 0.32);
            ctx.Rectangle(34, 230, 2, 2);
            ctx.Fill();
            ctx.Rectangle(32, 232, 2, 2);
            ctx.Fill();
            ctx.Restore();
        }

        private static void DrawCloseIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double cx = x + size / 2;
            double cy = y + size / 2;
            double r = (size - pad * 2) * 0.46;

            // Soft disc background
            SetIconFill(ctx, color, 0.12);
            ctx.NewPath();
            ctx.Arc(cx, cy, r, 0, Math.PI * 2);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke);
            ctx.NewPath();
            ctx.Arc(cx, cy, r, 0, Math.PI * 2);
            ctx.Stroke();

            // Clean X — two separate strokes with round caps (no multi-arc glue).
            double d = r * 0.40;
            SetIconStroke(ctx, color, Math.Max(2.0, stroke * 1.25));
            ctx.NewPath();
            ctx.MoveTo(cx - d, cy - d);
            ctx.LineTo(cx + d, cy + d);
            ctx.Stroke();
            ctx.NewPath();
            ctx.MoveTo(cx + d, cy - d);
            ctx.LineTo(cx - d, cy + d);
            ctx.Stroke();
        }

        // ── BRANCHES tree (Group 1140.svg LEFT tile, active green) ────────
        private static void DrawBranchesIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            // Exact SVG coords inside left 139×69 tile:
            // circles (61,12)(79,18)(75,25)(71,32) r=2.5
            // trunk 61:12→33, arms 62–78@18, 61–73@25, 61–71@32
            BeginSvgIconMap(ctx, x, y, size, 58.5, 9.5, 23, 25, out double s);
            SetIconStroke(ctx, color, SvgStroke(s));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            ctx.MoveTo(61, 12);
            ctx.LineTo(61, 33);
            ctx.MoveTo(62, 18);
            ctx.LineTo(78, 18);
            ctx.MoveTo(61, 25);
            ctx.LineTo(73, 25);
            ctx.MoveTo(61, 32);
            ctx.LineTo(71, 32);
            ctx.Stroke();

            SetIconFill(ctx, color, 1.0);
            ctx.Arc(61, 12, 2.5, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Arc(79, 18, 2.5, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Arc(75, 25, 2.5, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Arc(71, 32, 2.5, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Restore();
        }

        // ── QUESTS network (Group 1140.svg RIGHT tile) ────────────────────
        private static void DrawQuestsIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            // Exact SVG coords (global): hub (220,15)r4, leaves (209,22)(219,30)(229,24)r3
            // lines hub→each leaf. Tile origin x=149.5 — use absolute coords as drawn.
            BeginSvgIconMap(ctx, x, y, size, 206, 11, 27, 22, out double s);
            SetIconStroke(ctx, color, Math.Max(1.0, 1.0)); // SVG stroke default 1
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            ctx.MoveTo(220, 15);
            ctx.LineTo(229, 24);
            ctx.MoveTo(209, 22);
            ctx.LineTo(220, 15);
            ctx.MoveTo(219, 30);
            ctx.LineTo(220, 15);
            ctx.Stroke();

            SetIconFill(ctx, color, 1.0);
            ctx.Arc(220, 15, 4, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Arc(209, 22, 3, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Arc(219, 30, 3, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Arc(229, 24, 3, 0, Math.PI * 2);
            ctx.Fill();
            ctx.Restore();
        }

        // ── Pencil EDIT (Group 1140.svg path at y≈138–154) ────────────────
        private static void DrawPencilIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            // Exact lucide-style path from the SVG (stroke 2, round caps/joins).
            BeginSvgIconMap(ctx, x, y, size, 28, 138.4, 16, 15.6, out double s);
            SetIconStroke(ctx, color, SvgStroke(s));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Shaft: tip mid → lower-left corner → base left → base right
            ctx.MoveTo(36, 142);
            ctx.LineTo(28, 150);
            ctx.LineTo(28, 154);
            ctx.LineTo(44, 154);
            ctx.Stroke();

            // Base fold / page edge
            ctx.MoveTo(28, 154);
            ctx.LineTo(32, 154);
            ctx.LineTo(40, 146);
            ctx.Stroke();

            // Tip diamond with rounded corners (SVG cubic approximation)
            ctx.MoveTo(36, 142);
            ctx.LineTo(38.87, 139.13);
            ctx.CurveTo(39.46, 138.54, 40.11, 138.40, 40.31, 138.46);
            ctx.CurveTo(40.54, 138.54, 40.73, 138.74, 41.13, 139.13);
            ctx.LineTo(42.87, 140.87);
            ctx.CurveTo(43.46, 141.46, 43.54, 142.11, 43.54, 142.31);
            ctx.CurveTo(43.46, 142.54, 43.27, 142.74, 42.87, 143.13);
            ctx.LineTo(40, 146);
            ctx.Stroke();

            // Diagonal cut across tip body
            ctx.MoveTo(36, 142);
            ctx.LineTo(40, 146);
            ctx.Stroke();
            ctx.Restore();
        }

        // ── Editor / open book ────────────────────────────────────────────
        private static void DrawEditorIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad;
            double right = x + size - pad;
            double top = y + pad + size * 0.04;
            double bottom = y + size - pad;
            double mid = x + size / 2;

            // Page fills
            SetIconFill(ctx, color, 0.14);
            ctx.MoveTo(mid, top + size * 0.10);
            ctx.CurveTo(mid - size * 0.05, top, left + size * 0.06, top, left, top + size * 0.12);
            ctx.LineTo(left, bottom - size * 0.06);
            ctx.CurveTo(left + size * 0.08, bottom, mid - size * 0.04, bottom - size * 0.04, mid, bottom - size * 0.10);
            ctx.ClosePath();
            ctx.Fill();
            ctx.MoveTo(mid, top + size * 0.10);
            ctx.CurveTo(mid + size * 0.05, top, right - size * 0.06, top, right, top + size * 0.12);
            ctx.LineTo(right, bottom - size * 0.06);
            ctx.CurveTo(right - size * 0.08, bottom, mid + size * 0.04, bottom - size * 0.04, mid, bottom - size * 0.10);
            ctx.ClosePath();
            ctx.Fill();

            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(mid, top + size * 0.10);
            ctx.CurveTo(mid - size * 0.05, top, left + size * 0.06, top, left, top + size * 0.12);
            ctx.LineTo(left, bottom - size * 0.06);
            ctx.CurveTo(left + size * 0.08, bottom, mid - size * 0.04, bottom - size * 0.04, mid, bottom - size * 0.10);
            ctx.Stroke();
            ctx.MoveTo(mid, top + size * 0.10);
            ctx.CurveTo(mid + size * 0.05, top, right - size * 0.06, top, right, top + size * 0.12);
            ctx.LineTo(right, bottom - size * 0.06);
            ctx.CurveTo(right - size * 0.08, bottom, mid + size * 0.04, bottom - size * 0.04, mid, bottom - size * 0.10);
            ctx.Stroke();

            // Spine + stitch marks
            SetIconStroke(ctx, color, stroke * 1.05);
            ctx.MoveTo(mid, top + size * 0.12);
            ctx.LineTo(mid, bottom - size * 0.12);
            ctx.Stroke();
            SetIconStroke(ctx, color, stroke * 0.55);
            for (int i = 0; i < 4; i++)
            {
                double sy = top + size * 0.22 + i * size * 0.14;
                ctx.MoveTo(mid - size * 0.04, sy);
                ctx.LineTo(mid + size * 0.04, sy);
            }
            ctx.Stroke();

            // Text lines (different lengths)
            SetIconStroke(ctx, color, stroke * 0.65);
            double[] leftLens = { 0.78, 0.62, 0.70, 0.50 };
            double[] rightLens = { 0.72, 0.58, 0.66, 0.48 };
            for (int i = 0; i < 4; i++)
            {
                double ly = top + size * 0.28 + i * size * 0.13;
                ctx.MoveTo(left + size * 0.12, ly);
                ctx.LineTo(mid - size * 0.08 - size * (1 - leftLens[i]) * 0.15, ly);
                ctx.MoveTo(mid + size * 0.08, ly);
                ctx.LineTo(right - size * 0.12 + size * (1 - rightLens[i]) * 0.1, ly);
            }
            ctx.Stroke();
        }

        // ── Start: play triangle in circle ────────────────────────────────
        private static void DrawStartIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double cx = x + size / 2;
            double cy = y + size / 2;
            double r = (size - pad * 2) * 0.48;
            SetIconFill(ctx, color, 0.12);
            ctx.Arc(cx, cy, r, 0, Math.PI * 2);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke);
            ctx.Arc(cx, cy, r, 0, Math.PI * 2);
            ctx.Stroke();
            SetIconStroke(ctx, color, stroke * 0.6);
            ctx.Arc(cx, cy, r * 0.78, 0, Math.PI * 2);
            ctx.Stroke();

            // Play triangle with outline
            SetIconFill(ctx, color, 0.92);
            double left = cx - r * 0.22;
            ctx.MoveTo(left, cy - r * 0.40);
            ctx.LineTo(cx + r * 0.46, cy);
            ctx.LineTo(left, cy + r * 0.40);
            ctx.ClosePath();
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.7);
            ctx.MoveTo(left, cy - r * 0.40);
            ctx.LineTo(cx + r * 0.46, cy);
            ctx.LineTo(left, cy + r * 0.40);
            ctx.ClosePath();
            ctx.Stroke();
        }

        // ── Quest: scroll / list ──────────────────────────────────────────
        private static void DrawQuestIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad + size * 0.08;
            double right = x + size - pad - size * 0.06;
            double top = y + pad + size * 0.04;
            double bottom = y + size - pad - size * 0.02;
            double fold = size * 0.16;

            SetIconFill(ctx, color, 0.16);
            ctx.MoveTo(left, top);
            ctx.LineTo(right - fold, top);
            ctx.LineTo(right, top + fold);
            ctx.LineTo(right, bottom);
            ctx.LineTo(left, bottom);
            ctx.ClosePath();
            ctx.Fill();

            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(left, top);
            ctx.LineTo(right - fold, top);
            ctx.LineTo(right, top + fold);
            ctx.LineTo(right, bottom);
            ctx.LineTo(left, bottom);
            ctx.ClosePath();
            ctx.Stroke();

            // Fold triangle fill
            SetIconFill(ctx, color, 0.28);
            ctx.MoveTo(right - fold, top);
            ctx.LineTo(right - fold, top + fold);
            ctx.LineTo(right, top + fold);
            ctx.ClosePath();
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.85);
            ctx.MoveTo(right - fold, top);
            ctx.LineTo(right - fold, top + fold);
            ctx.LineTo(right, top + fold);
            ctx.Stroke();

            // Checklist: empty / checked / empty
            for (int i = 0; i < 3; i++)
            {
                double ly = top + size * 0.34 + i * size * 0.16;
                double box = size * 0.10;
                double bx = left + size * 0.08;
                SetIconStroke(ctx, color, stroke * 0.75);
                ctx.Rectangle(bx, ly - box * 0.5, box, box);
                ctx.Stroke();
                if (i == 1)
                {
                    SetIconStroke(ctx, color, stroke * 0.9);
                    ctx.MoveTo(bx + box * 0.2, ly);
                    ctx.LineTo(bx + box * 0.42, ly + box * 0.28);
                    ctx.LineTo(bx + box * 0.82, ly - box * 0.28);
                    ctx.Stroke();
                }
                SetIconStroke(ctx, color, stroke * 0.7);
                ctx.MoveTo(bx + box + size * 0.06, ly);
                ctx.LineTo(right - size * 0.14 - (i == 2 ? size * 0.08 : 0), ly);
                ctx.Stroke();
            }
        }

        // ── Checkpoint flag ───────────────────────────────────────────────
        private static void DrawCheckpointIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double poleX = x + pad + size * 0.24;
            double top = y + pad + size * 0.02;
            double bottom = y + size - pad;

            // Pole with ball tip
            SetIconStroke(ctx, color, stroke * 1.2);
            ctx.MoveTo(poleX, top + size * 0.06);
            ctx.LineTo(poleX, bottom - size * 0.04);
            ctx.Stroke();
            SetIconFill(ctx, color, 0.9);
            ctx.Arc(poleX, top + size * 0.05, size * 0.05, 0, Math.PI * 2);
            ctx.Fill();

            // Base stand
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(poleX - size * 0.16, bottom);
            ctx.LineTo(poleX + size * 0.20, bottom);
            ctx.Stroke();
            SetIconFill(ctx, color, 0.35);
            ctx.MoveTo(poleX - size * 0.10, bottom - size * 0.06);
            ctx.LineTo(poleX + size * 0.12, bottom - size * 0.06);
            ctx.LineTo(poleX + size * 0.16, bottom);
            ctx.LineTo(poleX - size * 0.14, bottom);
            ctx.ClosePath();
            ctx.Fill();

            // Flags with edge highlight
            SetIconFill(ctx, color, 0.88);
            ctx.MoveTo(poleX, top + size * 0.08);
            ctx.LineTo(x + size - pad, top + size * 0.20);
            ctx.LineTo(poleX + size * 0.08, top + size * 0.26);
            ctx.LineTo(poleX, top + size * 0.34);
            ctx.ClosePath();
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.7);
            ctx.MoveTo(poleX, top + size * 0.08);
            ctx.LineTo(x + size - pad, top + size * 0.20);
            ctx.LineTo(poleX + size * 0.08, top + size * 0.26);
            ctx.LineTo(poleX, top + size * 0.34);
            ctx.ClosePath();
            ctx.Stroke();

            SetIconFill(ctx, color, 0.55);
            ctx.MoveTo(poleX, top + size * 0.38);
            ctx.LineTo(x + size - pad - size * 0.08, top + size * 0.50);
            ctx.LineTo(poleX + size * 0.06, top + size * 0.56);
            ctx.LineTo(poleX, top + size * 0.66);
            ctx.ClosePath();
            ctx.Fill();
        }

        private static void DrawCheckmarkIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke)
        {
            double left = x + size * 0.18;
            double bottom = y + size * 0.55;
            double midX = x + size * 0.40;
            double midY = y + size * 0.72;
            double right = x + size * 0.82;
            double top = y + size * 0.28;
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(left, bottom);
            ctx.LineTo(midX, midY);
            ctx.LineTo(right, top);
            ctx.Stroke();
        }

        private enum AdminFlagIcon
        {
            /// <summary>
            /// Group 896 left chip — purple layers #AD00C8, legend «Create».
            /// Action: take / consume items on claim (ConsumeOnComplete).
            /// </summary>
            Take,
            /// <summary>
            /// Group 896 middle chip — cyan paperclip #5BD5DD, legend «Craft».
            /// Action: must craft the item (IsCraftObjective).
            /// </summary>
            Craft,
            /// <summary>Kill entity objective.</summary>
            Kill,
            /// <summary>
            /// Group 896 right chip — yellow tile #FFC74C, legend «All Types».
            /// Action: match all item variants (MatchAllVariants).
            /// </summary>
            AllVariants,
            /// <summary>Remove row (#FD5A53 trash). Group 896 x=390.5.</summary>
            Delete
        }

        private void DrawAdminCheckbox(
            Cairo.Context ctx,
            double fitScale,
            LayoutRect area,
            bool isChecked,
            bool enabled,
            bool hovered)
        {
            DrawAdminFlagToggle(ctx, fitScale, area, AdminFlagIcon.Take, isChecked, enabled, hovered);
        }

        /// <summary>
        /// Toggle tile with a small glyph (take / craft / all-variants) instead of a plain checkbox.
        /// </summary>
        private void DrawAdminFlagToggle(
            Cairo.Context ctx,
            double fitScale,
            LayoutRect area,
            AdminFlagIcon icon,
            bool isActive,
            bool enabled,
            bool hovered)
        {
            // Group 1169: idle = #171717/#353432; active = solid accent fill + matching glyph.
            double radius = 5.5 * fitScale;
            double[] accent = GetAdminFlagAccent(icon);

            double[] background;
            double[] border;
            double[] iconColor;

            if (!enabled)
            {
                background = [0.12, 0.13, 0.15, 0.7];
                border = QuestbookGuiLayout.AdminTileBorderColor;
                iconColor = [0.40, 0.42, 0.45, 0.7];
            }
            else if (isActive)
            {
                // Group 1169: all active chips use accent fill-opacity 0.16 + solid accent stroke/glyph.
                background = [accent[0], accent[1], accent[2], 0.16];
                border = accent;
                iconColor = accent;
            }
            else
            {
                background = hovered
                    ? QuestbookGuiLayout.AdminTileHoverBackgroundColor
                    : QuestbookGuiLayout.AdminTileBackgroundColor;
                border = hovered ? accent : QuestbookGuiLayout.AdminTileBorderColor;
                iconColor = QuestbookGuiLayout.AdminModalMutedIconColor;
            }

            FillRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius, background);
            StrokeRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius,
                Math.Max(1.0, fitScale), border);

            // Glyph inset — match SVG icons inside 31×31 chips.
            double pad = area.Width * 0.16;
            DrawAdminFlagGlyph(ctx, area.X + pad, area.Y + pad, area.Width - (pad * 2), icon, iconColor);
        }

        // Group 896: Create=purple layers · Craft=cyan paperclip · All Types=yellow tile.
        private static double[] GetAdminFlagAccent(AdminFlagIcon icon) => icon switch
        {
            AdminFlagIcon.Take => QuestbookGuiLayout.AdminFlagCraftColor,        // #AD00C8 purple Create
            AdminFlagIcon.Craft => QuestbookGuiLayout.AdminFlagVariantsColor,    // #5BD5DD cyan Craft
            AdminFlagIcon.AllVariants => QuestbookGuiLayout.AdminFlagTakeColor,  // #FFC74C yellow All Types
            AdminFlagIcon.Delete => QuestbookGuiLayout.AdminFlagDeleteColor,     // #FD5A53
            AdminFlagIcon.Kill => QuestbookGuiLayout.AdminFlagDeleteColor,
            _ => QuestbookGuiLayout.AdminTileActiveContentColor
        };

        private static void DrawAdminFlagGlyph(
            Cairo.Context ctx,
            double x,
            double y,
            double size,
            AdminFlagIcon icon,
            double[] color)
        {
            double stroke = Math.Max(1.2, size * 0.11);
            SetIconStroke(ctx, color, stroke);
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            switch (icon)
            {
                case AdminFlagIcon.Take:
                    // Create — layers chevron (purple).
                    DrawCraftFlagGlyph(ctx, x, y, size, color, stroke);
                    break;
                case AdminFlagIcon.Craft:
                    // Craft — paperclip (cyan).
                    DrawAllVariantsFlagGlyph(ctx, x, y, size, color, stroke);
                    break;
                case AdminFlagIcon.Kill:
                    DrawKillFlagGlyph(ctx, x, y, size, color, stroke);
                    break;
                case AdminFlagIcon.AllVariants:
                    // All Types — tile/component (yellow).
                    DrawTakeFlagGlyph(ctx, x, y, size, color, stroke);
                    break;
                case AdminFlagIcon.Delete:
                    DrawDeleteFlagGlyph(ctx, x, y, size, color, stroke);
                    break;
            }
        }

        /// <summary>
        /// Group 1171 take glyph — плитка / component (yellow chip x=356.5).
        /// Path from row y=74.5: M380 85 … component shape.
        /// </summary>
        private static void DrawTakeFlagGlyph(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke)
        {
            // Design bbox ≈ 363–381 × 81–99 inside 31×31 chip at (356.5, 74.5).
            BeginSvgIconMap(ctx, x, y, size, 363, 81, 18, 18, out _);
            SetIconStroke(ctx, color, SvgStroke(2));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            ctx.NewPath();
            ctx.MoveTo(380, 85);
            ctx.LineTo(377.849, 85);
            ctx.CurveTo(377.351, 85, 377, 84.4975, 377, 84);
            ctx.CurveTo(377, 82.3431, 375.657, 81, 374, 81);
            ctx.CurveTo(372.343, 81, 371, 82.3431, 371, 84);
            ctx.CurveTo(371, 84.4975, 370.649, 85, 370.151, 85);
            ctx.LineTo(368, 85);
            ctx.CurveTo(367.448, 85, 367, 85.4477, 367, 86);
            ctx.LineTo(367, 88.1513);
            ctx.CurveTo(367, 88.6488, 366.498, 89, 366, 89);
            ctx.CurveTo(364.343, 89, 363, 90.3431, 363, 92);
            ctx.CurveTo(363, 93.6569, 364.343, 95, 366, 95);
            ctx.CurveTo(366.498, 95, 367, 95.3511, 367, 95.8486);
            ctx.LineTo(367, 98);
            ctx.CurveTo(367, 98.5523, 367.448, 99, 368, 99);
            ctx.LineTo(380, 99);
            ctx.CurveTo(380.552, 99, 381, 98.5523, 381, 98);
            ctx.LineTo(381, 95.8486);
            ctx.CurveTo(381, 95.3511, 380.498, 95, 380, 95);
            ctx.CurveTo(378.343, 95, 377, 93.6569, 377, 92);
            ctx.CurveTo(377, 90.3431, 378.343, 89, 380, 89);
            ctx.CurveTo(380.498, 89, 381, 88.6488, 381, 88.1513);
            ctx.LineTo(381, 86);
            ctx.CurveTo(381, 85.4477, 380.552, 85, 380, 85);
            ctx.ClosePath();
            ctx.Stroke();
            ctx.Restore();
        }

        /// <summary>
        /// Group 1171 craft glyph — layers / double chevron (purple chip x=288.5).
        /// Path: M313 24 L304 30 L295 24 / diamond M313 20 L304 26 L295 20 L304 14 Z.
        /// </summary>
        private static void DrawCraftFlagGlyph(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke)
        {
            // Design bbox ≈ 295–313 × 14–30 inside 31×31 chip at (288.5, 6.5).
            BeginSvgIconMap(ctx, x, y, size, 295, 14, 18, 16, out _);
            SetIconStroke(ctx, color, SvgStroke(2));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Lower chevron (open V)
            ctx.NewPath();
            ctx.MoveTo(313, 24);
            ctx.LineTo(304, 30);
            ctx.LineTo(295, 24);
            ctx.Stroke();

            // Upper diamond / layers
            ctx.NewPath();
            ctx.MoveTo(313, 20);
            ctx.LineTo(304, 26);
            ctx.LineTo(295, 20);
            ctx.LineTo(304, 14);
            ctx.ClosePath();
            ctx.Stroke();
            ctx.Restore();
        }

        /// <summary>
        /// Group 1171 variants glyph — скрепка / paperclip (cyan chip x=322.5).
        /// Path: M330.535 21.465 … paperclip loop.
        /// </summary>
        private static void DrawAllVariantsFlagGlyph(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke)
        {
            // Design bbox ≈ 330.5–345 × 12.5–31.3 inside 31×31 chip at (322.5, 6.5).
            BeginSvgIconMap(ctx, x, y, size, 330.5, 12.5, 15, 19, out _);
            SetIconStroke(ctx, color, SvgStroke(2));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            ctx.NewPath();
            ctx.MoveTo(330.535, 21.4652);
            ctx.LineTo(337.429, 14.5709);
            ctx.CurveTo(339.48, 12.5206, 342.804, 12.5206, 344.854, 14.5709);
            ctx.CurveTo(346.904, 16.6211, 346.904, 19.9454, 344.854, 21.9957);
            ctx.LineTo(336.899, 29.9506);
            ctx.CurveTo(335.532, 31.3175, 333.316, 31.3173, 331.95, 29.9505);
            ctx.CurveTo(330.583, 28.5836, 330.582, 26.3678, 331.949, 25.0009);
            ctx.LineTo(339.904, 17.046);
            ctx.CurveTo(340.588, 16.3626, 341.696, 16.3626, 342.38, 17.046);
            ctx.CurveTo(343.063, 17.7294, 343.063, 18.8372, 342.379, 19.5206);
            ctx.LineTo(335.485, 26.4149);
            ctx.Stroke();
            ctx.Restore();
        }

        /// <summary>Group 1169 + add glyph — document with plus (cyan/orange).</summary>
        private static void DrawDocumentPlusGlyph(Cairo.Context ctx, double x, double y, double size, double[] color)
        {
            // Path like M482 165… document 482–498 × 165–181 with plus through center.
            BeginSvgIconMap(ctx, x, y, size, 482, 165, 16, 16, out _);
            SetIconStroke(ctx, color, SvgStroke(1));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Document rounded rect
            ctx.NewPath();
            ctx.MoveTo(482, 168.2);
            ctx.CurveTo(482, 167.08, 482, 166.52, 482.218, 166.092);
            ctx.CurveTo(482.41, 165.715, 482.715, 165.41, 483.092, 165.218);
            ctx.CurveTo(483.52, 165, 484.08, 165, 485.2, 165);
            ctx.LineTo(494.8, 165);
            ctx.CurveTo(495.92, 165, 496.48, 165, 496.908, 165.218);
            ctx.CurveTo(497.284, 165.41, 497.59, 165.715, 497.782, 166.092);
            ctx.CurveTo(498, 166.52, 498, 167.08, 498, 168.2);
            ctx.LineTo(498, 177.8);
            ctx.CurveTo(498, 178.92, 498, 179.48, 497.782, 179.908);
            ctx.CurveTo(497.59, 180.284, 497.284, 180.59, 496.908, 180.782);
            ctx.CurveTo(496.48, 181, 495.921, 181, 494.804, 181);
            ctx.LineTo(485.197, 181);
            ctx.CurveTo(484.079, 181, 483.519, 181, 483.092, 180.782);
            ctx.CurveTo(482.715, 180.59, 482.41, 180.284, 482.218, 179.908);
            ctx.CurveTo(482, 179.48, 482, 178.92, 482, 177.8);
            ctx.ClosePath();
            ctx.Stroke();

            // Plus
            ctx.MoveTo(486, 173);
            ctx.LineTo(494, 173);
            ctx.MoveTo(490, 169);
            ctx.LineTo(490, 177);
            ctx.Stroke();
            ctx.Restore();
        }

        /// <summary>Group 1171 delete glyph — trash can (#FD5A53), chip x=390.5.</summary>
        private static void DrawDeleteFlagGlyph(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke)
        {
            // Path from Group 1171 row y=74.5 (icon around 398–414, 81–99).
            BeginSvgIconMap(ctx, x, y, size, 398, 81, 16, 18, out _);
            SetIconStroke(ctx, color, SvgStroke(2));
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;

            // Vertical ribs
            ctx.NewPath();
            ctx.MoveTo(408, 88);
            ctx.LineTo(408, 95);
            ctx.MoveTo(404, 88);
            ctx.LineTo(404, 95);
            ctx.Stroke();

            // Can body
            ctx.NewPath();
            ctx.MoveTo(400, 84);
            ctx.LineTo(400, 95.8);
            ctx.CurveTo(400, 96.9201, 400, 97.4798, 400.218, 97.9076);
            ctx.CurveTo(400.41, 98.2839, 400.715, 98.5905, 401.092, 98.7822);
            ctx.CurveTo(401.519, 99, 402.079, 99, 403.197, 99);
            ctx.LineTo(408.803, 99);
            ctx.CurveTo(409.921, 99, 410.48, 99, 410.907, 98.7822);
            ctx.CurveTo(411.284, 98.5905, 411.59, 98.2839, 411.782, 97.9076);
            ctx.CurveTo(412, 97.4802, 412, 96.921, 412, 95.8031);
            ctx.LineTo(412, 84);
            ctx.Stroke();

            // Lid + handle
            ctx.NewPath();
            ctx.MoveTo(400, 84);
            ctx.LineTo(402, 84);
            ctx.MoveTo(400, 84);
            ctx.LineTo(398, 84);
            ctx.MoveTo(402, 84);
            ctx.LineTo(410, 84);
            ctx.MoveTo(402, 84);
            ctx.CurveTo(402, 83.0681, 402, 82.6024, 402.152, 82.2349);
            ctx.CurveTo(402.355, 81.7448, 402.744, 81.3552, 403.234, 81.1522);
            ctx.CurveTo(403.602, 81, 404.068, 81, 405, 81);
            ctx.LineTo(407, 81);
            ctx.CurveTo(407.932, 81, 408.398, 81, 408.765, 81.1522);
            ctx.CurveTo(409.255, 81.3552, 409.645, 81.7448, 409.848, 82.2349);
            ctx.CurveTo(410, 82.6024, 410, 83.0681, 410, 84);
            ctx.MoveTo(410, 84);
            ctx.LineTo(412, 84);
            ctx.MoveTo(412, 84);
            ctx.LineTo(414, 84);
            ctx.Stroke();
            ctx.Restore();
        }

        /// <summary>Kill flag reuses red X / blades for kill objectives.</summary>
        private static void DrawKillFlagGlyph(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke)
        {
            double pad = size * 0.20;
            ctx.NewPath();
            ctx.MoveTo(x + pad, y + pad);
            ctx.LineTo(x + size - pad, y + size - pad);
            ctx.Stroke();
            ctx.NewPath();
            ctx.MoveTo(x + size - pad, y + pad);
            ctx.LineTo(x + pad, y + size - pad);
            ctx.Stroke();
        }

        /// <summary>Legend chip: icon + short caption (used above goal/reward lists).</summary>
        private void DrawAdminFlagLegendChip(
            Cairo.Context ctx,
            double fitScale,
            double x,
            double y,
            double chipH,
            AdminFlagIcon icon,
            string caption,
            out double usedWidth)
        {
            double iconSize = chipH;
            LayoutRect iconRect = new(x, y, iconSize, iconSize);
            DrawAdminFlagToggle(ctx, fitScale, iconRect, icon, isActive: true, enabled: true, hovered: false);

            CairoFont font = CreateMontserratFont(10 * fitScale, QuestbookGuiLayout.AdminTitleColor);
            double textX = x + iconSize + (5 * fitScale);
            double textW = MeasureTextWidth(font, caption);
            DrawText(ctx, font, caption, textX, GetTextBaselineY(font, y, chipH, chipH));
            usedWidth = iconSize + (5 * fitScale) + textW;
        }

        private static void DrawImageIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad;
            double top = y + pad;
            double width = size - (pad * 2);
            double height = size - (pad * 2);
            double radius = width * 0.12;

            SetIconFill(ctx, color, 0.12);
            AddRoundedRectanglePath(ctx, left, top, width, height, radius);
            ctx.Fill();
            StrokeRoundedRectangle(ctx, left, top, width, height, radius, stroke, color);

            // Inner mat
            SetIconStroke(ctx, color, stroke * 0.55);
            AddRoundedRectanglePath(ctx, left + width * 0.08, top + height * 0.08, width * 0.84, height * 0.84, radius * 0.7);
            ctx.Stroke();

            double mountainBaseY = top + height * 0.80;
            SetIconFill(ctx, color, 0.55);
            ctx.MoveTo(left + width * 0.12, mountainBaseY);
            ctx.LineTo(left + width * 0.38, top + height * 0.46);
            ctx.LineTo(left + width * 0.54, mountainBaseY);
            ctx.ClosePath();
            ctx.Fill();

            SetIconFill(ctx, color, 0.85);
            ctx.MoveTo(left + width * 0.42, mountainBaseY);
            ctx.LineTo(left + width * 0.66, top + height * 0.32);
            ctx.LineTo(left + width * 0.90, mountainBaseY);
            ctx.ClosePath();
            ctx.Fill();

            // Sun
            SetIconFill(ctx, color, 0.75);
            ctx.Arc(left + width * 0.74, top + height * 0.28, width * 0.09, 0, Math.PI * 2);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.55);
            ctx.Arc(left + width * 0.74, top + height * 0.28, width * 0.09, 0, Math.PI * 2);
            ctx.Stroke();
        }
    }
}
