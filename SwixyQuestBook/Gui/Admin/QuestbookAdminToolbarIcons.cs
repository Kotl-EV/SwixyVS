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
            double radius = QuestbookGuiLayout.AdminTileCornerRadius * fitScale;
            double borderWidth = (active ? 2.0 : 1.5) * fitScale;
            double[] accent = accentColor ?? QuestbookGuiLayout.AdminSaveButtonColor;

            double[] background = active
                ? QuestbookGuiLayout.AdminTileActiveBackgroundColor
                : hovered
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
                : QuestbookGuiLayout.AdminPanelTextColor;

            // Wide sidebar-style button: [icon]  Label
            if (hasLabel && labelOnRight)
            {
                double padX = Math.Max(8 * fitScale, area.Height * 0.16);
                double iconSize = Math.Min(area.Height * 0.55, area.Width * 0.28);
                double iconX = area.X + padX;
                double iconY = area.Y + ((area.Height - iconSize) / 2);
                DrawAdminToolbarIcon(ctx, icon, iconX, iconY, iconSize, iconColor);

                double fontSize = Math.Clamp(area.Height * 0.32, 11 * fitScale, 15 * fitScale);
                CairoFont font = CreateMontserratFont(fontSize, iconColor);
                string text = label!.Trim();
                double textX = iconX + iconSize + (10 * fitScale);
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

            double iconSizeStacked = hasLabel
                ? Math.Min(area.Width * 0.52, area.Height * 0.46)
                : Math.Min(area.Width, area.Height) * QuestbookGuiLayout.AdminTileIconScale;

            double contentTop = area.Y + (hasLabel ? area.Height * 0.10 : (area.Height - iconSizeStacked) / 2);
            double iconXStacked = area.X + ((area.Width - iconSizeStacked) / 2);
            double iconYStacked = contentTop;

            DrawAdminToolbarIcon(ctx, icon, iconXStacked, iconYStacked, iconSizeStacked, iconColor);

            if (hasLabel)
            {
                double fontSize = Math.Clamp(area.Height * 0.20, 8.5 * fitScale, 11.5 * fitScale);
                CairoFont font = CreateMontserratFont(fontSize, iconColor);
                string text = label!.Trim();
                // Keep labels short so they fit tile width.
                if (text.Length > 8)
                {
                    text = text[..8];
                }

                double textWidth = MeasureTextWidth(font, text);
                double textX = area.X + ((area.Width - textWidth) / 2);
                double textY = area.Y + area.Height * 0.78;
                DrawText(ctx, font, text, textX, textY);
            }
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
                    DrawPlusBadgeIcon(ctx, x, y, size, color, stroke, pad);
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

        // ── Cursor / select node ──────────────────────────────────────────
        private static void DrawSelectIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double tipX = x + pad + size * 0.06;
            double tipY = y + pad + size * 0.04;

            // Soft ground shadow under the pointer
            SetIconFill(ctx, color, 0.12);
            ctx.Arc(tipX + size * 0.22, tipY + size * 0.58, size * 0.18, 0, Math.PI * 2);
            ctx.Fill();

            // Pointer body
            SetIconFill(ctx, color, 0.92);
            ctx.MoveTo(tipX, tipY);
            ctx.LineTo(tipX, tipY + size * 0.66);
            ctx.LineTo(tipX + size * 0.17, tipY + size * 0.48);
            ctx.LineTo(tipX + size * 0.30, tipY + size * 0.78);
            ctx.LineTo(tipX + size * 0.44, tipY + size * 0.71);
            ctx.LineTo(tipX + size * 0.28, tipY + size * 0.42);
            ctx.LineTo(tipX + size * 0.52, tipY + size * 0.42);
            ctx.ClosePath();
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.85);
            ctx.MoveTo(tipX, tipY);
            ctx.LineTo(tipX, tipY + size * 0.66);
            ctx.LineTo(tipX + size * 0.17, tipY + size * 0.48);
            ctx.LineTo(tipX + size * 0.30, tipY + size * 0.78);
            ctx.LineTo(tipX + size * 0.44, tipY + size * 0.71);
            ctx.LineTo(tipX + size * 0.28, tipY + size * 0.42);
            ctx.LineTo(tipX + size * 0.52, tipY + size * 0.42);
            ctx.ClosePath();
            ctx.Stroke();

            // Inner highlight edge on pointer
            SetIconStroke(ctx, color, Math.Max(1.0, stroke * 0.45));
            ctx.MoveTo(tipX + size * 0.05, tipY + size * 0.12);
            ctx.LineTo(tipX + size * 0.05, tipY + size * 0.48);
            ctx.Stroke();

            // Target node with outer ring + diamond badge
            double nx = x + size * 0.68;
            double ny = y + size * 0.62;
            double nr = size * 0.15;
            SetIconStroke(ctx, color, stroke * 0.7);
            ctx.Arc(nx, ny, nr * 1.35, 0, Math.PI * 2);
            ctx.Stroke();
            FillNodeRing(ctx, nx, ny, nr, color, stroke * 0.9, 0.22);
            FillDiamond(ctx, nx, ny, nr * 0.42, color, 0.75, stroke * 0.55, false);
        }

        // ── New quest node (+ diamond) ────────────────────────────────────
        private static void DrawNewQuestIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double cx = x + size * 0.40;
            double cy = y + size * 0.54;
            double r = size * 0.30;

            // Outer glow diamond
            FillDiamond(ctx, cx, cy, r * 1.12, color, 0.10, stroke, false);
            // Main diamond with inner cut
            FillDiamond(ctx, cx, cy, r, color, 0.28, stroke, true);
            FillDiamond(ctx, cx, cy, r * 0.48, color, 0.18, stroke * 0.7, true);

            // Tiny connecting stub (graph edge)
            SetIconStroke(ctx, color, stroke * 0.8);
            ctx.MoveTo(cx + r * 0.72, cy - r * 0.2);
            ctx.LineTo(cx + r * 1.15, cy - r * 0.55);
            ctx.Stroke();
            FillNodeRing(ctx, cx + r * 1.22, cy - r * 0.62, size * 0.06, color, stroke * 0.7, 0.4);

            // Plus badge
            double bx = x + size - pad - size * 0.16;
            double by = y + pad + size * 0.16;
            double br = size * 0.17;
            SetIconFill(ctx, color, 0.95);
            ctx.Arc(bx, by, br, 0, Math.PI * 2);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.75);
            ctx.Arc(bx, by, br, 0, Math.PI * 2);
            ctx.Stroke();
            SetIconStroke(ctx, [0.08, 0.10, 0.12, 1.0], Math.Max(1.5, stroke * 0.95));
            double ph = br * 0.52;
            ctx.MoveTo(bx - ph, by);
            ctx.LineTo(bx + ph, by);
            ctx.MoveTo(bx, by - ph);
            ctx.LineTo(bx, by + ph);
            ctx.Stroke();
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

        // ── Link two nodes ────────────────────────────────────────────────
        private static void DrawLinkIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double r = size * 0.14;
            double leftCx = x + pad + r + size * 0.04;
            double rightCx = x + size - pad - r - size * 0.02;
            double topCy = y + size * 0.32;
            double botCy = y + size * 0.70;

            // Soft chain path fill
            SetIconStroke(ctx, color, stroke * 2.2);
            ctx.SetSourceRGBA(color[0], color[1], color[2], (color.Length > 3 ? color[3] : 1) * 0.12);
            double ax = leftCx + r * 0.65;
            double ay = topCy + r * 0.45;
            double bx = rightCx - r * 0.65;
            double by = botCy - r * 0.45;
            ctx.MoveTo(ax, ay);
            ctx.CurveTo(ax + size * 0.28, ay - size * 0.02, bx - size * 0.12, by + size * 0.02, bx, by);
            ctx.Stroke();

            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(ax, ay);
            ctx.CurveTo(ax + size * 0.28, ay - size * 0.02, bx - size * 0.12, by + size * 0.02, bx, by);
            ctx.Stroke();

            // Mid link knot
            double mx = (ax + bx) * 0.5 + size * 0.04;
            double my = (ay + by) * 0.5;
            SetIconFill(ctx, color, 0.45);
            ctx.Arc(mx, my, size * 0.05, 0, Math.PI * 2);
            ctx.Fill();

            FillNodeRing(ctx, leftCx, topCy, r, color, stroke, 0.32);
            FillDiamond(ctx, leftCx, topCy, r * 0.4, color, 0.55, stroke * 0.5, false);
            FillNodeRing(ctx, rightCx, botCy, r, color, stroke, 0.32);

            // Arrow head into target
            SetIconFill(ctx, color, 0.95);
            ctx.MoveTo(bx + size * 0.02, by);
            ctx.LineTo(bx - size * 0.14, by - size * 0.10);
            ctx.LineTo(bx - size * 0.06, by - size * 0.02);
            ctx.LineTo(bx - size * 0.12, by + size * 0.10);
            ctx.ClosePath();
            ctx.Fill();
        }

        // ── Trash can ─────────────────────────────────────────────────────
        private static void DrawTrashIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad + size * 0.12;
            double right = x + size - pad - size * 0.12;
            double top = y + pad + size * 0.30;
            double bottom = y + size - pad - size * 0.04;
            double bodyW = right - left;

            // Lid plate
            SetIconFill(ctx, color, 0.25);
            ctx.Rectangle(left - size * 0.05, top - size * 0.04, bodyW + size * 0.10, size * 0.08);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke);
            ctx.Rectangle(left - size * 0.05, top - size * 0.04, bodyW + size * 0.10, size * 0.08);
            ctx.Stroke();

            // Handle
            double hx1 = x + size * 0.36;
            double hx2 = x + size * 0.64;
            double hy = top - size * 0.16;
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(hx1, top - size * 0.02);
            ctx.LineTo(hx1, hy);
            ctx.CurveTo(hx1, hy - size * 0.06, hx2, hy - size * 0.06, hx2, hy);
            ctx.LineTo(hx2, top - size * 0.02);
            ctx.Stroke();

            // Body
            SetIconFill(ctx, color, 0.18);
            ctx.MoveTo(left, top + size * 0.08);
            ctx.LineTo(left + bodyW * 0.10, bottom);
            ctx.LineTo(right - bodyW * 0.10, bottom);
            ctx.LineTo(right, top + size * 0.08);
            ctx.ClosePath();
            ctx.Fill();
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(left, top + size * 0.08);
            ctx.LineTo(left + bodyW * 0.10, bottom);
            ctx.LineTo(right - bodyW * 0.10, bottom);
            ctx.LineTo(right, top + size * 0.08);
            ctx.ClosePath();
            ctx.Stroke();

            // Rib lines + bottom lip
            double mid = x + size / 2;
            SetIconStroke(ctx, color, stroke * 0.75);
            ctx.MoveTo(mid, top + size * 0.18);
            ctx.LineTo(mid, bottom - size * 0.10);
            ctx.MoveTo(mid - bodyW * 0.22, top + size * 0.20);
            ctx.LineTo(mid - bodyW * 0.16, bottom - size * 0.12);
            ctx.MoveTo(mid + bodyW * 0.22, top + size * 0.20);
            ctx.LineTo(mid + bodyW * 0.16, bottom - size * 0.12);
            ctx.Stroke();
            SetIconStroke(ctx, color, stroke * 0.9);
            ctx.MoveTo(left + bodyW * 0.12, bottom - size * 0.02);
            ctx.LineTo(right - bodyW * 0.12, bottom - size * 0.02);
            ctx.Stroke();
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

        // ── Graph grid ────────────────────────────────────────────────────
        private static void DrawGridIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad;
            double top = y + pad;
            double right = x + size - pad;
            double bottom = y + size - pad;

            SetIconFill(ctx, color, 0.08);
            ctx.Rectangle(left, top, right - left, bottom - top);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke);
            ctx.Rectangle(left, top, right - left, bottom - top);
            ctx.Stroke();

            double stepX = (right - left) / 4;
            double stepY = (bottom - top) / 4;
            SetIconStroke(ctx, color, stroke * 0.65);
            for (int i = 1; i <= 3; i++)
            {
                ctx.MoveTo(left + stepX * i, top);
                ctx.LineTo(left + stepX * i, bottom);
                ctx.MoveTo(left, top + stepY * i);
                ctx.LineTo(right, top + stepY * i);
            }
            ctx.Stroke();

            // Active cell + crosshair
            SetIconFill(ctx, color, 0.32);
            ctx.Rectangle(left + stepX, top + stepY, stepX, stepY);
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.85);
            ctx.Rectangle(left + stepX, top + stepY, stepX, stepY);
            ctx.Stroke();
            double hx = left + stepX * 1.5;
            double hy = top + stepY * 1.5;
            SetIconStroke(ctx, color, stroke * 0.7);
            ctx.MoveTo(hx - stepX * 0.28, hy);
            ctx.LineTo(hx + stepX * 0.28, hy);
            ctx.MoveTo(hx, hy - stepY * 0.28);
            ctx.LineTo(hx, hy + stepY * 0.28);
            ctx.Stroke();
            SetIconFill(ctx, color, 0.85);
            ctx.Arc(hx, hy, size * 0.03, 0, Math.PI * 2);
            ctx.Fill();
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

        // ── Branch tree ───────────────────────────────────────────────────
        private static void DrawBranchesIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double trunkX = x + pad + size * 0.20;
            double top = y + pad + size * 0.06;
            double bottom = y + size - pad;
            double r = size * 0.09;

            SetIconStroke(ctx, color, stroke * 1.1);
            ctx.MoveTo(trunkX, bottom);
            ctx.LineTo(trunkX, top + r);
            ctx.Stroke();

            double y1 = top + size * 0.26;
            double y2 = top + size * 0.52;
            double y3 = top + size * 0.76;
            double x1 = x + size - pad - r;
            double x2 = x + size - pad - r * 1.1;
            double x3 = x + size * 0.64;

            // Elbow connectors
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(trunkX, y1);
            ctx.LineTo(x1 - r * 0.2, y1);
            ctx.MoveTo(trunkX, y2);
            ctx.LineTo(x2 - r * 0.2, y2);
            ctx.MoveTo(trunkX, y3);
            ctx.LineTo(x3 - r * 0.2, y3);
            ctx.Stroke();

            // Elbow joints
            SetIconFill(ctx, color, 0.5);
            ctx.Arc(trunkX, y1, size * 0.03, 0, Math.PI * 2);
            ctx.Arc(trunkX, y2, size * 0.03, 0, Math.PI * 2);
            ctx.Arc(trunkX, y3, size * 0.03, 0, Math.PI * 2);
            ctx.Fill();

            FillNodeRing(ctx, trunkX, top + r * 0.15, r, color, stroke * 0.85, 0.35);
            FillNodeRing(ctx, x1, y1, r, color, stroke * 0.85, 0.3);
            FillNodeRing(ctx, x2, y2, r, color, stroke * 0.85, 0.3);
            FillNodeRing(ctx, x3, y3, r, color, stroke * 0.85, 0.3);
            // Leaf accents on right nodes
            FillDiamond(ctx, x1, y1, r * 0.35, color, 0.7, stroke * 0.4, false);
            FillDiamond(ctx, x2, y2, r * 0.35, color, 0.55, stroke * 0.4, false);
        }

        // ── Quest graph ───────────────────────────────────────────────────
        private static void DrawQuestsIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double r = size * 0.105;
            double n1x = x + pad + r + size * 0.02;
            double n1y = y + size * 0.52;
            double n2x = x + size * 0.50;
            double n2y = y + pad + r + size * 0.02;
            double n3x = x + size - pad - r;
            double n3y = y + size * 0.58;
            double n4x = x + size * 0.48;
            double n4y = y + size - pad - r;

            SetIconStroke(ctx, color, stroke * 0.95);
            ctx.MoveTo(n1x + r * 0.55, n1y - r * 0.55);
            ctx.LineTo(n2x - r * 0.45, n2y + r * 0.7);
            ctx.MoveTo(n2x + r * 0.55, n2y + r * 0.65);
            ctx.LineTo(n3x - r * 0.55, n3y - r * 0.45);
            ctx.MoveTo(n2x, n2y + r);
            ctx.LineTo(n4x, n4y - r);
            ctx.Stroke();

            // Direction ticks on edges
            SetIconFill(ctx, color, 0.8);
            ctx.Arc((n1x + n2x) * 0.5, (n1y + n2y) * 0.5, size * 0.025, 0, Math.PI * 2);
            ctx.Arc((n2x + n3x) * 0.5, (n2y + n3y) * 0.5, size * 0.025, 0, Math.PI * 2);
            ctx.Fill();

            FillNodeRing(ctx, n1x, n1y, r, color, stroke, 0.35);
            FillNodeRing(ctx, n2x, n2y, r, color, stroke, 0.4);
            FillDiamond(ctx, n2x, n2y, r * 0.4, color, 0.7, stroke * 0.45, false);
            FillNodeRing(ctx, n3x, n3y, r, color, stroke, 0.18);
            FillNodeRing(ctx, n4x, n4y, r, color, stroke, 0.18);
        }

        // ── Pencil rename ─────────────────────────────────────────────────
        private static void DrawPencilIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double tipX = x + size - pad - size * 0.04;
            double tipY = y + pad + size * 0.06;
            double baseX = x + pad + size * 0.08;
            double baseY = y + size - pad - size * 0.10;

            // Body
            SetIconFill(ctx, color, 0.28);
            ctx.MoveTo(baseX, baseY - size * 0.12);
            ctx.LineTo(baseX + size * 0.18, baseY);
            ctx.LineTo(tipX - size * 0.10, tipY + size * 0.20);
            ctx.LineTo(tipX - size * 0.24, tipY + size * 0.04);
            ctx.ClosePath();
            ctx.Fill();
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(baseX, baseY - size * 0.12);
            ctx.LineTo(baseX + size * 0.18, baseY);
            ctx.LineTo(tipX - size * 0.10, tipY + size * 0.20);
            ctx.LineTo(tipX - size * 0.24, tipY + size * 0.04);
            ctx.ClosePath();
            ctx.Stroke();

            // Ferrule band
            SetIconStroke(ctx, color, stroke * 0.8);
            double bandT = 0.28;
            double bx1 = baseX + (tipX - size * 0.24 - baseX) * bandT;
            double by1 = (baseY - size * 0.12) + (tipY + size * 0.04 - (baseY - size * 0.12)) * bandT;
            double bx2 = baseX + size * 0.18 + (tipX - size * 0.10 - (baseX + size * 0.18)) * bandT;
            double by2 = baseY + (tipY + size * 0.20 - baseY) * bandT;
            ctx.MoveTo(bx1, by1);
            ctx.LineTo(bx2, by2);
            double bandT2 = 0.38;
            double bx3 = baseX + (tipX - size * 0.24 - baseX) * bandT2;
            double by3 = (baseY - size * 0.12) + (tipY + size * 0.04 - (baseY - size * 0.12)) * bandT2;
            double bx4 = baseX + size * 0.18 + (tipX - size * 0.10 - (baseX + size * 0.18)) * bandT2;
            double by4 = baseY + (tipY + size * 0.20 - baseY) * bandT2;
            ctx.MoveTo(bx3, by3);
            ctx.LineTo(bx4, by4);
            ctx.Stroke();

            // Wood tip
            SetIconFill(ctx, color, 0.55);
            ctx.MoveTo(tipX - size * 0.10, tipY + size * 0.20);
            ctx.LineTo(tipX, tipY);
            ctx.LineTo(tipX - size * 0.24, tipY + size * 0.04);
            ctx.ClosePath();
            ctx.Fill();
            // Graphite point
            SetIconFill(ctx, color, 0.95);
            ctx.MoveTo(tipX - size * 0.06, tipY + size * 0.10);
            ctx.LineTo(tipX, tipY);
            ctx.LineTo(tipX - size * 0.12, tipY + size * 0.04);
            ctx.ClosePath();
            ctx.Fill();

            // Eraser end
            SetIconFill(ctx, color, 0.45);
            ctx.MoveTo(baseX - size * 0.02, baseY - size * 0.16);
            ctx.LineTo(baseX + size * 0.12, baseY + size * 0.02);
            ctx.LineTo(baseX + size * 0.18, baseY);
            ctx.LineTo(baseX, baseY - size * 0.12);
            ctx.ClosePath();
            ctx.Fill();

            // Write line
            SetIconStroke(ctx, color, stroke * 0.75);
            ctx.MoveTo(x + pad, y + size - pad);
            ctx.LineTo(x + size * 0.58, y + size - pad);
            ctx.Stroke();
            SetIconFill(ctx, color, 0.7);
            ctx.Arc(x + size * 0.62, y + size - pad, size * 0.025, 0, Math.PI * 2);
            ctx.Fill();
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
            /// <summary>Take / consume items on turn-in.</summary>
            Take,
            /// <summary>Craft objective (real craft detection).</summary>
            Craft,
            /// <summary>Kill entity objective.</summary>
            Kill,
            /// <summary>Match all variants of the item (wildcard types).</summary>
            AllVariants
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
            double radius = 4 * fitScale;
            double[] accent = icon switch
            {
                AdminFlagIcon.Craft => [0.40, 0.95, 1.0, 1.0],
                AdminFlagIcon.Kill => [1.0, 0.40, 0.40, 1.0],
                AdminFlagIcon.AllVariants => [1.0, 0.78, 0.30, 1.0],
                _ => QuestbookGuiLayout.AdminSaveButtonColor
            };

            double[] background = !enabled
                ? [0.12, 0.13, 0.15, 0.7]
                : isActive
                    ? QuestbookGuiLayout.AdminTileActiveBackgroundColor
                    : hovered
                        ? QuestbookGuiLayout.AdminTileHoverBackgroundColor
                        : QuestbookGuiLayout.AdminTileBackgroundColor;
            double[] border = !enabled
                ? QuestbookGuiLayout.AdminTileBorderColor
                : isActive || hovered
                    ? accent
                    : QuestbookGuiLayout.AdminTileBorderColor;

            FillRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius, background);
            StrokeRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius, 1.2 * fitScale, border);

            double[] iconColor = !enabled
                ? [0.40, 0.42, 0.45, 0.7]
                : isActive || hovered
                    ? accent
                    : QuestbookGuiLayout.AdminPanelTextColor;

            double pad = area.Width * 0.18;
            DrawAdminFlagGlyph(ctx, area.X + pad, area.Y + pad, area.Width - (pad * 2), icon, iconColor);
        }

        private static void DrawAdminFlagGlyph(
            Cairo.Context ctx,
            double x,
            double y,
            double size,
            AdminFlagIcon icon,
            double[] color)
        {
            double stroke = Math.Max(1.4, size * 0.12);
            SetIconStroke(ctx, color, stroke);
            SetIconFill(ctx, color, 0.85);

            switch (icon)
            {
                case AdminFlagIcon.Take:
                    DrawTakeFlagGlyph(ctx, x, y, size, color, stroke);
                    break;
                case AdminFlagIcon.Craft:
                    DrawCraftFlagGlyph(ctx, x, y, size, color, stroke);
                    break;
                case AdminFlagIcon.Kill:
                    DrawKillFlagGlyph(ctx, x, y, size, color, stroke);
                    break;
                case AdminFlagIcon.AllVariants:
                    DrawAllVariantsFlagGlyph(ctx, x, y, size, color, stroke);
                    break;
            }
        }

        /// <summary>Inbox / hand-in: tray with arrow down (take items).</summary>
        private static void DrawTakeFlagGlyph(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke)
        {
            double left = x + size * 0.12;
            double right = x + size * 0.88;
            double midX = x + size * 0.50;
            double trayTop = y + size * 0.52;
            double bottom = y + size * 0.88;

            // Arrow down
            ctx.NewPath();
            ctx.MoveTo(midX, y + size * 0.10);
            ctx.LineTo(midX, y + size * 0.48);
            ctx.Stroke();
            ctx.NewPath();
            ctx.MoveTo(midX - size * 0.18, y + size * 0.34);
            ctx.LineTo(midX, y + size * 0.50);
            ctx.LineTo(midX + size * 0.18, y + size * 0.34);
            ctx.Stroke();

            // Tray
            ctx.NewPath();
            ctx.MoveTo(left, trayTop);
            ctx.LineTo(left + size * 0.10, bottom);
            ctx.LineTo(right - size * 0.10, bottom);
            ctx.LineTo(right, trayTop);
            ctx.Stroke();
        }

        /// <summary>Hammer / anvil-ish craft glyph.</summary>
        private static void DrawCraftFlagGlyph(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke)
        {
            // Hammer head
            double headL = x + size * 0.12;
            double headR = x + size * 0.55;
            double headT = y + size * 0.18;
            double headB = y + size * 0.42;
            ctx.NewPath();
            ctx.Rectangle(headL, headT, headR - headL, headB - headT);
            ctx.Stroke();

            // Handle
            double hx = x + size * 0.48;
            ctx.NewPath();
            ctx.MoveTo(hx, headB);
            ctx.LineTo(x + size * 0.78, y + size * 0.86);
            ctx.Stroke();

            // Small spark / star near head
            double sx = x + size * 0.72;
            double sy = y + size * 0.22;
            double r = size * 0.10;
            ctx.NewPath();
            ctx.MoveTo(sx, sy - r);
            ctx.LineTo(sx, sy + r);
            ctx.MoveTo(sx - r, sy);
            ctx.LineTo(sx + r, sy);
            ctx.Stroke();
        }

        /// <summary>Three stacked squares / asterisk feel = all variants.</summary>
        private static void DrawAllVariantsFlagGlyph(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke)
        {
            double s = size * 0.34;
            // Back square
            ctx.NewPath();
            ctx.Rectangle(x + size * 0.28, y + size * 0.12, s, s);
            ctx.Stroke();
            // Mid
            ctx.NewPath();
            ctx.Rectangle(x + size * 0.38, y + size * 0.28, s, s);
            ctx.Stroke();
            // Front
            ctx.NewPath();
            ctx.Rectangle(x + size * 0.48, y + size * 0.44, s, s);
            ctx.Stroke();
        }

        /// <summary>Simple crossed-swords / X mark = kill creature.</summary>
        private static void DrawKillFlagGlyph(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke)
        {
            double pad = size * 0.18;
            // X
            ctx.NewPath();
            ctx.MoveTo(x + pad, y + pad);
            ctx.LineTo(x + size - pad, y + size - pad);
            ctx.Stroke();
            ctx.NewPath();
            ctx.MoveTo(x + size - pad, y + pad);
            ctx.LineTo(x + pad, y + size - pad);
            ctx.Stroke();
            // Small horizontal bar (blade guard vibe)
            ctx.NewPath();
            ctx.MoveTo(x + size * 0.22, y + size * 0.50);
            ctx.LineTo(x + size * 0.78, y + size * 0.50);
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
