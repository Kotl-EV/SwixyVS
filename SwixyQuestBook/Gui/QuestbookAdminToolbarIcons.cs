using Cairo;
using System;

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
            Checkpoint
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

            double iconSize = Math.Min(area.Width, area.Height) * QuestbookGuiLayout.AdminTileIconScale;
            double iconX = area.X + ((area.Width - iconSize) / 2);
            double iconY = area.Y + ((area.Height - iconSize) / 2);
            double[] iconColor = active || hovered
                ? accent
                : QuestbookGuiLayout.AdminPanelTextColor;

            DrawAdminToolbarIcon(ctx, icon, iconX, iconY, iconSize, iconColor);
        }

        private static void SetIconStroke(Cairo.Context ctx, double[] color, double lineWidth)
        {
            ctx.SetSourceRGBA(color[0], color[1], color[2], color[3]);
            ctx.LineWidth = lineWidth;
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;
        }

        private static void SetIconFill(Cairo.Context ctx, double[] color)
        {
            ctx.SetSourceRGBA(color[0], color[1], color[2], color[3]);
        }

        private static void DrawAdminToolbarIcon(Cairo.Context ctx, AdminToolbarIcon icon, double x, double y, double size, double[] color)
        {
            double stroke = Math.Max(1.6, size * 0.09);
            double pad = size * 0.18;

            switch (icon)
            {
                case AdminToolbarIcon.Select:
                    DrawSelectIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.NewQuest:
                case AdminToolbarIcon.Add:
                    DrawPlusIcon(ctx, x, y, size, color, stroke, pad);
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
                    DrawPencilIcon(ctx, x, y, size, color, stroke, pad);
                    break;
                case AdminToolbarIcon.EditBranch:
                    DrawEditBranchIcon(ctx, x, y, size, color, stroke, pad);
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
            }
        }

        private static void DrawPlusIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double cx = x + size / 2;
            double cy = y + size / 2;
            double half = (size - pad * 2) / 2;
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(cx - half, cy);
            ctx.LineTo(cx + half, cy);
            ctx.MoveTo(cx, cy - half);
            ctx.LineTo(cx, cy + half);
            ctx.Stroke();
        }

        private static void DrawCloseIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad;
            double top = y + pad;
            double right = x + size - pad;
            double bottom = y + size - pad;
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(left, top);
            ctx.LineTo(right, bottom);
            ctx.MoveTo(right, top);
            ctx.LineTo(left, bottom);
            ctx.Stroke();
        }

        private static void DrawSelectIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double tipX = x + pad;
            double tipY = y + size - pad;
            double right = x + size - pad;
            double top = y + pad;
            SetIconFill(ctx, color);
            ctx.MoveTo(tipX, tipY);
            ctx.LineTo(tipX + size * 0.22, tipY - size * 0.22);
            ctx.LineTo(tipX + size * 0.34, tipY - size * 0.10);
            ctx.LineTo(right - size * 0.08, top + size * 0.12);
            ctx.LineTo(right, top);
            ctx.LineTo(right - size * 0.12, top + size * 0.08);
            ctx.LineTo(tipX + size * 0.10, tipY - size * 0.34);
            ctx.ClosePath();
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.7);
            ctx.Stroke();
        }

        private static void DrawLinkIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double r = size * 0.14;
            double leftCx = x + pad + r;
            double rightCx = x + size - pad - r;
            double cy = y + size / 2;
            SetIconStroke(ctx, color, stroke);
            ctx.Arc(leftCx, cy, r, 0, Math.PI * 2);
            ctx.Stroke();
            ctx.Arc(rightCx, cy, r, 0, Math.PI * 2);
            ctx.Stroke();
            ctx.MoveTo(leftCx + r, cy);
            ctx.LineTo(rightCx - r, cy);
            ctx.Stroke();
        }

        private static void DrawTrashIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad + size * 0.08;
            double right = x + size - pad - size * 0.08;
            double top = y + pad + size * 0.18;
            double bottom = y + size - pad;
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(left + size * 0.08, top);
            ctx.LineTo(right - size * 0.08, top);
            ctx.Stroke();
            ctx.MoveTo(left, top + size * 0.08);
            ctx.LineTo(left + size * 0.04, bottom);
            ctx.LineTo(right - size * 0.04, bottom);
            ctx.LineTo(right, top + size * 0.08);
            ctx.Stroke();
            double lidY = top - size * 0.10;
            ctx.MoveTo(left - size * 0.02, lidY);
            ctx.LineTo(right + size * 0.02, lidY);
            ctx.Stroke();
            ctx.MoveTo(x + size / 2, lidY);
            ctx.LineTo(x + size / 2, lidY - size * 0.10);
            ctx.Stroke();
        }

        private static void DrawSaveIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad;
            double right = x + size - pad;
            double top = y + pad;
            double bottom = y + size - pad;
            SetIconStroke(ctx, color, stroke);
            ctx.Rectangle(left, top, right - left, bottom - top);
            ctx.Stroke();
            double slotTop = top + size * 0.08;
            double slotBottom = top + size * 0.30;
            ctx.MoveTo(left + size * 0.16, slotTop);
            ctx.LineTo(right - size * 0.16, slotTop);
            ctx.LineTo(right - size * 0.16, slotBottom);
            ctx.LineTo(left + size * 0.16, slotBottom);
            ctx.ClosePath();
            ctx.Stroke();
            double checkY = bottom - size * 0.18;
            ctx.MoveTo(left + size * 0.18, checkY);
            ctx.LineTo(left + size * 0.34, checkY + size * 0.16);
            ctx.LineTo(right - size * 0.14, top + size * 0.40);
            ctx.Stroke();
        }

        private static void DrawClearIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double cx = x + size / 2;
            double cy = y + size / 2;
            double r = (size - pad * 2) / 2;
            SetIconStroke(ctx, color, stroke);
            ctx.Arc(cx, cy, r, 0, Math.PI * 2);
            ctx.Stroke();
            double inner = r * 0.45;
            ctx.MoveTo(cx - inner, cy - inner);
            ctx.LineTo(cx + inner, cy + inner);
            ctx.MoveTo(cx + inner, cy - inner);
            ctx.LineTo(cx - inner, cy + inner);
            ctx.Stroke();
        }

        private static void DrawGridIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad;
            double top = y + pad;
            double right = x + size - pad;
            double bottom = y + size - pad;
            double thirdW = (right - left) / 3;
            double thirdH = (bottom - top) / 3;
            SetIconStroke(ctx, color, stroke);
            for (int i = 1; i <= 2; i++)
            {
                double vx = left + thirdW * i;
                ctx.MoveTo(vx, top);
                ctx.LineTo(vx, bottom);
                double hy = top + thirdH * i;
                ctx.MoveTo(left, hy);
                ctx.LineTo(right, hy);
            }
            ctx.Stroke();
        }

        private static void DrawBranchesIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double trunkX = x + pad + size * 0.12;
            double top = y + pad;
            double bottom = y + size - pad;
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(trunkX, bottom);
            ctx.LineTo(trunkX, top + size * 0.28);
            ctx.Stroke();
            double midY = top + size * 0.34;
            ctx.MoveTo(trunkX, midY);
            ctx.LineTo(x + size - pad, midY);
            ctx.Stroke();
            double lowY = top + size * 0.58;
            ctx.MoveTo(trunkX, lowY);
            ctx.LineTo(x + size - pad - size * 0.08, lowY);
            ctx.Stroke();
            double r = size * 0.09;
            ctx.Arc(x + size - pad, midY, r, 0, Math.PI * 2);
            ctx.Stroke();
            ctx.Arc(x + size - pad - size * 0.08, lowY, r, 0, Math.PI * 2);
            ctx.Stroke();
            ctx.Arc(trunkX, top + size * 0.20, r, 0, Math.PI * 2);
            ctx.Stroke();
        }

        private static void DrawQuestsIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double r = size * 0.12;
            double n1x = x + pad + r;
            double n1y = y + size / 2;
            double n2x = x + size / 2;
            double n2y = y + pad + r;
            double n3x = x + size - pad - r;
            double n3y = y + size - pad - r;
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(n1x + r * 0.8, n1y - r * 0.4);
            ctx.LineTo(n2x - r * 0.5, n2y + r * 0.6);
            ctx.Stroke();
            ctx.MoveTo(n2x + r * 0.6, n2y + r * 0.8);
            ctx.LineTo(n3x - r * 0.7, n3y - r * 0.2);
            ctx.Stroke();
            ctx.Arc(n1x, n1y, r, 0, Math.PI * 2);
            ctx.Stroke();
            ctx.Arc(n2x, n2y, r, 0, Math.PI * 2);
            ctx.Stroke();
            ctx.Arc(n3x, n3y, r, 0, Math.PI * 2);
            ctx.Stroke();
        }

        private static void DrawEditBranchIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double slotSize = size * 0.42;
            double slotX = x + pad;
            double slotY = y + pad + size * 0.08;
            double radius = slotSize * 0.18;
            StrokeRoundedRectangle(ctx, slotX, slotY, slotSize, slotSize, radius, stroke, color);

            double inner = slotSize * 0.22;
            SetIconFill(ctx, color);
            ctx.Rectangle(slotX + inner, slotY + inner, slotSize - (inner * 2), slotSize - (inner * 2));
            ctx.Fill();

            double penBaseX = slotX + slotSize * 0.55;
            double penBaseY = slotY + slotSize * 0.95;
            double penTipX = x + size - pad;
            double penTipY = y + pad;
            SetIconFill(ctx, color);
            ctx.MoveTo(penBaseX, penBaseY);
            ctx.LineTo(penBaseX + size * 0.18, penBaseY - size * 0.18);
            ctx.LineTo(penTipX, penTipY);
            ctx.LineTo(penTipX - size * 0.12, penTipY + size * 0.12);
            ctx.ClosePath();
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.7);
            ctx.Stroke();
        }

        private static void DrawPencilIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double bodyLeft = x + pad + size * 0.10;
            double bodyTop = y + size - pad - size * 0.10;
            double tipX = x + size - pad;
            double tipY = y + pad;
            SetIconFill(ctx, color);
            ctx.MoveTo(bodyLeft, bodyTop);
            ctx.LineTo(bodyLeft + size * 0.22, bodyTop - size * 0.22);
            ctx.LineTo(tipX, tipY);
            ctx.LineTo(tipX - size * 0.14, tipY + size * 0.14);
            ctx.ClosePath();
            ctx.Fill();
            SetIconStroke(ctx, color, stroke * 0.65);
            ctx.Stroke();
        }

        private static void DrawEditorIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad;
            double right = x + size - pad;
            double top = y + pad + size * 0.10;
            double bottom = y + size - pad;
            SetIconStroke(ctx, color, stroke);
            ctx.Rectangle(left, top, right - left, bottom - top);
            ctx.Stroke();
            ctx.MoveTo(left + size * 0.14, top + size * 0.22);
            ctx.LineTo(right - size * 0.14, top + size * 0.22);
            ctx.MoveTo(left + size * 0.14, top + size * 0.38);
            ctx.LineTo(right - size * 0.30, top + size * 0.38);
            ctx.Stroke();
            double penTipX = right - size * 0.06;
            double penTipY = bottom - size * 0.06;
            SetIconFill(ctx, color);
            ctx.MoveTo(penTipX - size * 0.18, penTipY);
            ctx.LineTo(penTipX, penTipY - size * 0.18);
            ctx.LineTo(penTipX + size * 0.06, penTipY - size * 0.12);
            ctx.LineTo(penTipX - size * 0.12, penTipY + size * 0.06);
            ctx.ClosePath();
            ctx.Fill();
        }

        private static void DrawStartIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad;
            double right = x + size - pad;
            double top = y + pad;
            double bottom = y + size - pad;
            double midY = y + size / 2;
            SetIconFill(ctx, color);
            ctx.MoveTo(left, top);
            ctx.LineTo(right, midY);
            ctx.LineTo(left, bottom);
            ctx.ClosePath();
            ctx.Fill();
        }

        private static void DrawQuestIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad;
            double right = x + size - pad;
            double top = y + pad;
            double bottom = y + size - pad;
            double fold = size * 0.22;
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(left, top);
            ctx.LineTo(right - fold, top);
            ctx.LineTo(right, top + fold);
            ctx.LineTo(right, bottom);
            ctx.LineTo(left, bottom);
            ctx.ClosePath();
            ctx.Stroke();
            ctx.MoveTo(right - fold, top);
            ctx.LineTo(right - fold, top + fold);
            ctx.LineTo(right, top + fold);
            ctx.Stroke();
            ctx.MoveTo(left + size * 0.14, top + size * 0.40);
            ctx.LineTo(right - size * 0.22, top + size * 0.40);
            ctx.MoveTo(left + size * 0.14, top + size * 0.56);
            ctx.LineTo(right - size * 0.30, top + size * 0.56);
            ctx.Stroke();
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

        private void DrawAdminCheckbox(
            Cairo.Context ctx,
            double fitScale,
            LayoutRect area,
            bool isChecked,
            bool enabled,
            bool hovered)
        {
            double radius = 4 * fitScale;
            double[] background = !enabled
                ? [0.12, 0.13, 0.15, 0.7]
                : isChecked
                    ? QuestbookGuiLayout.AdminTileActiveBackgroundColor
                    : hovered
                        ? QuestbookGuiLayout.AdminTileHoverBackgroundColor
                        : QuestbookGuiLayout.AdminTileBackgroundColor;
            double[] border = !enabled
                ? QuestbookGuiLayout.AdminTileBorderColor
                : isChecked || hovered
                    ? QuestbookGuiLayout.AdminSaveButtonColor
                    : QuestbookGuiLayout.AdminTileBorderColor;

            FillRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius, background);
            StrokeRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius, 1.2 * fitScale, border);

            if (isChecked && enabled)
            {
                double iconPad = area.Width * 0.16;
                DrawCheckmarkIcon(
                    ctx,
                    area.X + iconPad,
                    area.Y + iconPad,
                    area.Width - (iconPad * 2),
                    QuestbookGuiLayout.AdminSaveButtonColor,
                    System.Math.Max(1.8, area.Width * 0.12));
            }
        }

        private static void DrawImageIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double left = x + pad;
            double top = y + pad;
            double width = size - (pad * 2);
            double height = size - (pad * 2);
            double radius = width * 0.12;

            StrokeRoundedRectangle(ctx, left, top, width, height, radius, stroke, color);

            double mountainBaseY = top + height * 0.78;
            SetIconFill(ctx, color);
            ctx.MoveTo(left + width * 0.14, mountainBaseY);
            ctx.LineTo(left + width * 0.42, top + height * 0.48);
            ctx.LineTo(left + width * 0.58, mountainBaseY);
            ctx.ClosePath();
            ctx.Fill();

            ctx.MoveTo(left + width * 0.46, mountainBaseY);
            ctx.LineTo(left + width * 0.68, top + height * 0.34);
            ctx.LineTo(left + width * 0.88, mountainBaseY);
            ctx.ClosePath();
            ctx.Fill();

            SetIconFill(ctx, color);
            ctx.Arc(left + width * 0.72, top + height * 0.28, width * 0.08, 0, Math.PI * 2);
            ctx.Fill();
        }

        private static void DrawCheckpointIcon(Cairo.Context ctx, double x, double y, double size, double[] color, double stroke, double pad)
        {
            double poleX = x + pad + size * 0.16;
            double top = y + pad;
            double bottom = y + size - pad;
            SetIconStroke(ctx, color, stroke);
            ctx.MoveTo(poleX, top);
            ctx.LineTo(poleX, bottom);
            ctx.Stroke();
            SetIconFill(ctx, color);
            ctx.MoveTo(poleX, top + size * 0.08);
            ctx.LineTo(x + size - pad, top + size * 0.22);
            ctx.LineTo(poleX, top + size * 0.36);
            ctx.ClosePath();
            ctx.Fill();
            ctx.MoveTo(poleX, top + size * 0.44);
            ctx.LineTo(x + size - pad - size * 0.06, top + size * 0.58);
            ctx.LineTo(poleX, top + size * 0.72);
            ctx.ClosePath();
            ctx.Fill();
        }
    }
}