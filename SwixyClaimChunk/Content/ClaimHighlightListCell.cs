// =============================================================================
// ClaimHighlightListCell.cs
// -----------------------------------------------------------------------------
// Клиентская ячейка списка приватов с тремя зонами клика: основная область (слева),
// переключатель подсветки (лампочка, справа сверху) и удаление (корзина, справа снизу).
// Рисует фон кнопки, многострочный заголовок/описание, иконки и оверлеи наведения.
// Вспомогательный класс ClaimCairoIcons — векторная
// отрисовка иконок через Cairo без внешних текстур.
// =============================================================================

using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Элемент списка приватов с интерактивной подсветкой и кнопкой удаления в правой колонке.
/// Реализует <see cref="IGuiElementCell"/> для встраивания в GuiElementList / диалоги Vintage Story.
/// Фон строки — Group 378.png (<c>textures/gui/claim_list_row.png</c>, 302×81).
/// </summary>
public sealed class ClaimHighlightListCell : GuiElementTextBase, IGuiElementCell
{
    /// <summary>Group 378.png design size (panel only; gap is CellList spacing).</summary>
    private const int RowTexW = 302;
    private const int RowTexH = 81;

    /// <summary>Правая колонка иконок внутри текстуры (~42px) — unscaled.</summary>
    private const int UnscaledRightBoxWidth = 42;

    /// <summary>Лампочка в колодце 36×36 — чуть крупнее корзины.</summary>
    private const int UnscaledIconSize = 22;

    /// <summary>
    /// Корзина как Trash_Full.svg 24×24 в колодце 36×36 (~20–22 design-px, не на весь колодец).
    /// </summary>
    private const int UnscaledTrashIconSize = 18;

    /// <summary>Глубина рельефа (emboss) кнопки в не масштабированных единицах.</summary>
    private static readonly int UnscaledDepth = 2;

    /// <summary>Panel height only (81). Gap = CellList.unscaledCellSpacing = 8.</summary>
    private const int UnscaledMinRowHeight = RowTexH;

    // Fallback face #412D1D / bevels if texture missing
    private static readonly double[] ColFace = [0.255, 0.176, 0.114];
    private static readonly double[] ColHi = [0.337, 0.243, 0.169];
    private static readonly double[] ColLo = [0.165, 0.118, 0.078];
    private static readonly double[] ColSelected = [0.32, 0.22, 0.15];

    /// <summary>Shared Cairo surface for Group 378 row chrome (loaded once).</summary>
    private static ImageSurface? rowTexture;

    /// <summary>True after a failed load attempt (do not retry every compose).</summary>
    private static bool rowTextureMissing;

    /// <summary>Данные строки списка (заголовок, описание, шрифты, флаги отрисовки).</summary>
    public SavegameCellEntry cellEntry;

    /// <summary>Активна ли сейчас подсветка привата в мире (влияет на вид лампочки).</summary>
    public bool HighlightActive;

    /// <summary>Можно ли удалить приват (красная корзина); иначе показывается серая неактивная.</summary>
    public bool AllowDelete = true;

    /// <summary>Обработчик клика по левой зоне (выбор привата / основное действие).</summary>
    public Action<int>? OnMouseDownOnCellLeft;

    /// <summary>Обработчик клика по верхней правой зоне (переключение подсветки).</summary>
    public Action<int>? OnMouseDownOnCellRight;

    /// <summary>Обработчик клика по нижней правой зоне (удаление привата).</summary>
    public Action<int>? OnMouseDownOnCellDelete;

    /// <summary>Текстура ячейки в отпущенном состоянии кнопки.</summary>
    private LoadedTexture releasedButtonTexture;

    /// <summary>Текстура ячейки в нажатом/выбранном состоянии.</summary>
    private LoadedTexture pressedButtonTexture;

    /// <summary>Оверлей наведения на левую зону.</summary>
    private LoadedTexture leftHighlightTexture;

    /// <summary>Оверлей наведения на верхнюю правую зону (лампочка).</summary>
    private LoadedTexture rightTopHighlightTexture;

    /// <summary>Оверлой наведения на нижнюю правую зону (корзина).</summary>
    private LoadedTexture rightBottomHighlightTexture;

    /// <summary>Высота блока заголовка после разметки (для позиционирования описания).</summary>
    private double titleTextHeight;

    /// <summary>Вертикальный сдвиг содержимого при «нажатой» кнопке (имитация вдавливания).</summary>
    private double pressedYOffset;

    /// <summary>
    /// Фиксированная высота ячейки в не масштабированных единицах; при null высота вычисляется по тексту.
    /// </summary>
    public double? FixedHeight { get; set; }

    /// <summary>Границы элемента для протокола <see cref="IGuiElementCell"/>.</summary>
    ElementBounds IGuiElementCell.Bounds => Bounds;

    /// <summary>
    /// Создаёт ячейку списка и инициализирует текстуры оверлеев; настраивает шрифты из <paramref name="cell"/>.
    /// </summary>
    /// <param name="capi">Клиентский API.</param>
    /// <param name="cell">Запись списка с текстами и параметрами отступов.</param>
    /// <param name="bounds">Границы элемента в дереве GUI.</param>
    /// <param name="highlightActive">Начальное состояние подсветки привата (для иконки лампочки).</param>
    public ClaimHighlightListCell(ICoreClientAPI capi, SavegameCellEntry cell, ElementBounds bounds, bool highlightActive)
        : base(capi, "", null, bounds)
    {
        cellEntry = cell;
        HighlightActive = highlightActive;
        leftHighlightTexture = new LoadedTexture(capi);
        rightTopHighlightTexture = new LoadedTexture(capi);
        rightBottomHighlightTexture = new LoadedTexture(capi);
        releasedButtonTexture = new LoadedTexture(capi);
        pressedButtonTexture = new LoadedTexture(capi);

        // Компактные шрифты — низкие строки списка, иначе текст обрезается.
        cell.TitleFont ??= CreateCompactTitleFont();
        cell.DetailTextFont ??= CreateCompactDetailFont();
    }

    /// <summary>Шрифт названия привата — на шаг меньше WhiteSmallishText.</summary>
    private static CairoFont CreateCompactTitleFont()
    {
        var font = CairoFont.WhiteSmallText();
        font.LineHeightMultiplier = 0.95;
        return font;
    }

    /// <summary>Шрифт статистики под названием — DetailText, приглушённый.</summary>
    private static CairoFont CreateCompactDetailFont()
    {
        var font = CairoFont.WhiteDetailText().WithFontSize(12);
        font.Color[3] *= 0.8;
        font.LineHeightMultiplier = 0.9;
        return font;
    }

    /// <summary>
    /// Предварительно растеризует все текстуры ячейки: фон (отпущенный/нажатый) и три зоны наведения.
    /// </summary>
    public void Compose()
    {
        Bounds.CalcWorldBounds();

        // Одна поверхность переиспользуется для двух состояний кнопки: сначала released, затем pressed.
        using (var surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt))
        using (var ctx = genContext(surface))
        {
            ComposeButton(ctx, false);
            generateTexture(surface, ref releasedButtonTexture);

            // Полная очистка кадра перед отрисовкой нажатого варианта.
            ctx.Operator = Operator.Clear;
            ctx.Paint();
            ctx.Operator = Operator.Over;

            ComposeButton(ctx, true);
            generateTexture(surface, ref pressedButtonTexture);
        }

        ComposeHover(HoverRegion.Left, ref leftHighlightTexture);
        ComposeHover(HoverRegion.RightTop, ref rightTopHighlightTexture);
        ComposeHover(HoverRegion.RightBottom, ref rightBottomHighlightTexture);
    }

    /// <summary>Логические зоны hit-testing и оверлея наведения внутри ячейки.</summary>
    private enum HoverRegion
    {
        /// <summary>Левая часть — заголовок, описание, выбор строки.</summary>
        Left,

        /// <summary>Верхняя правая четверть — переключатель подсветки.</summary>
        RightTop,

        /// <summary>Нижняя правая четверть — удаление.</summary>
        RightBottom
    }

    /// <summary>
    /// Рисует полное содержимое ячейки: фон-кнопку, тексты, разделители и иконки.
    /// </summary>
    /// <param name="ctx">Контекст Cairo на off-screen поверхности.</param>
    /// <param name="pressed">True для визуала выбранной/нажатой кнопки.</param>
    private void ComposeButton(Context ctx, bool pressed)
    {
        EnsureRowTexture(api);

        var rightBoxWidth = scaled(UnscaledRightBoxWidth);
        // Cell height = panel only (81). Gap is between cells via CellList spacing.
        var drawH = Bounds.OuterHeight;
        var drawW = Bounds.OuterWidth;
        pressedYOffset = 0;

        if (cellEntry.DrawAsButton)
        {
            // Group 378.png stretched to cell (81 design-px after FixedHeight + zero padding).
            if (rowTexture != null)
            {
                ctx.Save();
                ctx.Scale(drawW / rowTexture.Width, drawH / rowTexture.Height);
                ctx.SetSourceSurface(rowTexture, 0, 0);
                if (ctx.GetSource() is SurfacePattern pattern)
                {
                    pattern.Filter = Filter.Nearest;
                }

                ctx.Paint();
                ctx.Restore();
            }
            else
            {
                DrawFallbackPlate(ctx, drawW, drawH, pressed || cellEntry.Selected);
            }

            if (pressed)
            {
                pressedYOffset = scaled(UnscaledDepth) / 2;
            }
        }

        var textLeft = Bounds.absPaddingX + scaled(10);
        var textTop = scaled(System.Math.Max(6, cellEntry.LeftOffY)) + pressedYOffset;
        var textW = Bounds.OuterWidth - rightBoxWidth - textLeft - scaled(4);

        Font = cellEntry.TitleFont;
        titleTextHeight = textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            cellEntry.Title,
            textLeft,
            textTop,
            textW);

        Font = cellEntry.DetailTextFont;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            cellEntry.DetailText,
            textLeft,
            textTop + scaled(System.Math.Max(2, cellEntry.DetailTextOffY)) + titleTextHeight,
            textW);

        DrawLightbulbIcon(ctx, rightBoxWidth, pressedYOffset, drawH);
        DrawTrashIcon(ctx, rightBoxWidth, pressedYOffset, drawH);

        if (cellEntry.DrawAsButton && (pressed || cellEntry.Selected))
        {
            ctx.SetSourceRGBA(0, 0, 0, 0.12);
            ctx.Rectangle(0, 0, drawW, drawH);
            ctx.Fill();
        }
    }

    /// <summary>Loads Group 378 row texture once for all claim list cells.</summary>
    private static void EnsureRowTexture(ICoreClientAPI capi)
    {
        if (rowTexture != null || rowTextureMissing)
        {
            return;
        }

        try
        {
            var asset = capi.Assets.TryGet(new AssetLocation("swixyclaimchunk", "textures/gui/claim_list_row.png"));
            if (asset?.Data == null || asset.Data.Length == 0)
            {
                capi.Logger.Warning("[SwixyClaimChunk] claim_list_row.png (Group 378) not found");
                rowTextureMissing = true;
                return;
            }

            using var bitmap = capi.Render.BitmapCreateFromPng(asset.Data);
            rowTexture = GuiElement.getImageSurfaceFromAsset(bitmap);
        }
        catch (Exception ex)
        {
            capi.Logger.Error("[SwixyClaimChunk] Failed to load claim_list_row.png: {0}", ex.Message);
            rowTextureMissing = true;
        }
    }

    private static void DrawFallbackPlate(Context ctx, double drawW, double drawH, bool selected)
    {
        var face = selected ? ColSelected : ColFace;
        ctx.SetSourceRGB(face[0], face[1], face[2]);
        ctx.Rectangle(0, 0, drawW, drawH);
        ctx.Fill();

        var bevel = 3.0;
        ctx.SetSourceRGB(ColHi[0], ColHi[1], ColHi[2]);
        ctx.Rectangle(0, 0, drawW, bevel);
        ctx.Fill();
        ctx.Rectangle(0, 0, bevel, drawH);
        ctx.Fill();
        ctx.SetSourceRGB(ColLo[0], ColLo[1], ColLo[2]);
        ctx.Rectangle(0, drawH - bevel, drawW, bevel);
        ctx.Fill();
        ctx.Rectangle(drawW - bevel, 0, bevel, drawH);
        ctx.Fill();
    }

    /// <summary>
    /// X-координата вертикального разделителя между текстовой зоной и правой панелью иконок.
    /// </summary>
    private double GetDividerX(double rightBoxWidth) => Bounds.OuterWidth - rightBoxWidth;

    /// <summary>Рисует иконку лампочки в верхней половине правой панели.</summary>
    private void DrawLightbulbIcon(Context ctx, double rightBoxWidth, double yOffset, double faceH)
    {
        var dividerX = GetDividerX(rightBoxWidth);
        var zoneHeight = faceH / 2;
        var iconSize = GetZoneIconSize(rightBoxWidth, zoneHeight);
        GetZoneIconPosition(dividerX, rightBoxWidth, 0, zoneHeight, iconSize, yOffset, out var iconX, out var iconY);
        ClaimCairoIcons.DrawHighlight(ctx, iconX, iconY, iconSize, HighlightActive);
    }

    /// <summary>Рисует иконку корзины в нижней половине правой панели.</summary>
    private void DrawTrashIcon(Context ctx, double rightBoxWidth, double yOffset, double faceH)
    {
        var dividerX = GetDividerX(rightBoxWidth);
        var zoneHeight = faceH / 2;
        // Smaller than the lightbulb — matches SVG trash scale inside 36×36 well.
        var iconSize = GetZoneIconSize(rightBoxWidth, zoneHeight, UnscaledTrashIconSize, maxFill: 0.55);
        GetZoneIconPosition(dividerX, rightBoxWidth, zoneHeight, zoneHeight, iconSize, yOffset, out var iconX, out var iconY);
        ClaimCairoIcons.DrawTrash(ctx, iconX, iconY, iconSize, destructive: AllowDelete);
    }

    /// <summary>
    /// Размер иконки: design size, capped so it stays inset inside the 36px well.
    /// </summary>
    private double GetZoneIconSize(double rightBoxWidth, double zoneHeight, int unscaledSize, double maxFill = 0.62)
    {
        return System.Math.Min(
            scaled(unscaledSize),
            System.Math.Min(rightBoxWidth * maxFill, zoneHeight * maxFill));
    }

    private double GetZoneIconSize(double rightBoxWidth, double zoneHeight)
    {
        return GetZoneIconSize(rightBoxWidth, zoneHeight, UnscaledIconSize);
    }

    /// <summary>
    /// Центрирует квадратную иконку внутри прямоугольной зоны правой панели.
    /// </summary>
    /// <param name="dividerX">Левая граница правой панели.</param>
    /// <param name="rightBoxWidth">Ширина правой панели.</param>
    /// <param name="zoneTop">Верхняя координата Y зоны внутри ячейки.</param>
    /// <param name="zoneHeight">Высота зоны.</param>
    /// <param name="iconSize">Сторона квадрата иконки.</param>
    /// <param name="yOffset">Дополнительный сдвиг по Y при нажатой кнопке.</param>
    /// <param name="iconX">Выход: левый верхний угол иконки по X.</param>
    /// <param name="iconY">Выход: левый верхний угол иконки по Y.</param>
    private static void GetZoneIconPosition(
        double dividerX,
        double rightBoxWidth,
        double zoneTop,
        double zoneHeight,
        double iconSize,
        double yOffset,
        out double iconX,
        out double iconY)
    {
        iconX = dividerX + (rightBoxWidth - iconSize) / 2;
        iconY = zoneTop + (zoneHeight - iconSize) / 2 + yOffset;
    }

    /// <summary>
    /// Создаёт полупрозрачную текстуру оверлея для одной из трёх зон наведения.
    /// </summary>
    private void ComposeHover(HoverRegion region, ref LoadedTexture texture)
    {
        using var surface = new ImageSurface(Format.Argb32, (int)Bounds.OuterWidth, (int)Bounds.OuterHeight);
        using var ctx = genContext(surface);

        var rightBoxWidth = scaled(UnscaledRightBoxWidth);
        var dividerX = GetDividerX(rightBoxWidth);
        var faceH = Bounds.OuterHeight;
        var midY = faceH / 2;

        ctx.NewPath();
        switch (region)
        {
            case HoverRegion.Left:
                ctx.MoveTo(0, 0);
                ctx.LineTo(dividerX, 0);
                ctx.LineTo(dividerX, faceH);
                ctx.LineTo(0, faceH);
                break;
            case HoverRegion.RightTop:
                AppendRightZonePath(ctx, dividerX, rightBoxWidth, 0, midY);
                break;
            default:
                AppendRightZonePath(ctx, dividerX, rightBoxWidth, midY, faceH);
                break;
        }

        ctx.ClosePath();
        ctx.SetSourceRGBA(0, 0, 0, 0.15);
        ctx.Fill();
        generateTexture(surface, ref texture);
    }

    /// <summary>Добавляет контур прямоугольной зоны в правой панели (от y1 до y2).</summary>
    private static void AppendRightZonePath(Context ctx, double dividerX, double rightBoxWidth, double y1, double y2)
    {
        ctx.MoveTo(dividerX, y1);
        ctx.LineTo(dividerX + rightBoxWidth, y1);
        ctx.LineTo(dividerX + rightBoxWidth, y2);
        ctx.LineTo(dividerX, y2);
    }

    /// <summary>
    /// Определяет зону клика/наведения по локальным координатам внутри ячейки.
    /// </summary>
    /// <param name="posX">X относительно левого верхнего угла ячейки.</param>
    /// <param name="posY">Y относительно левого верхнего угла ячейки.</param>
    /// <param name="region">Выход: логическая зона.</param>
    /// <returns>Всегда true при валидных координатах внутри bounds (разделение по dividerX и midY).</returns>
    private bool TryGetHoverRegion(double posX, double posY, out HoverRegion region)
    {
        var rightBoxWidth = scaled(UnscaledRightBoxWidth);
        var dividerX = GetDividerX(rightBoxWidth);
        if (posX < dividerX)
        {
            region = HoverRegion.Left;
            return true;
        }

        var faceH = Bounds.OuterHeight;
        // Правая колонка делится пополам по вертикали: лампочка и корзина.
        region = posY < faceH / 2 ? HoverRegion.RightTop : HoverRegion.RightBottom;
        return true;
    }

    /// <summary>Высота = FixedHeight (81). Зазор 8px — у CellList.unscaledCellSpacing.</summary>
    public void UpdateCellHeight()
    {
        Bounds.CalcWorldBounds();
        Bounds.fixedPaddingX = 0;
        Bounds.fixedPaddingY = 0;
        Bounds.fixedHeight = FixedHeight ?? RowTexH;
    }

    /// <summary>
    /// Отрисовывает фон ячейки и оверлей наведения для активной зоны под курсором.
    /// </summary>
    public void OnRenderInteractiveElements(ICoreClientAPI capi, float deltaTime)
    {
        if (pressedButtonTexture.TextureId == 0)
        {
            Compose();
        }

        var texture = cellEntry.Selected ? pressedButtonTexture : releasedButtonTexture;
        capi.Render.Render2DTexturePremultipliedAlpha(
            texture.TextureId,
            (int)Bounds.absX,
            (int)Bounds.absY,
            Bounds.OuterWidthInt,
            Bounds.OuterHeightInt);

        if (!IsPositionInside(capi.Input.MouseX, capi.Input.MouseY))
        {
            return;
        }

        var pos = Bounds.PositionInside(capi.Input.MouseX, capi.Input.MouseY);
        if (pos == null)
        {
            return;
        }

        if (!TryGetHoverRegion(pos.X, pos.Y, out var region))
        {
            return;
        }

        if (region == HoverRegion.RightBottom && !AllowDelete)
        {
            return;
        }

        var hoverTexture = region switch
        {
            HoverRegion.Left => leftHighlightTexture,
            HoverRegion.RightTop => rightTopHighlightTexture,
            _ => rightBottomHighlightTexture
        };
        capi.Render.Render2DTexturePremultipliedAlpha(hoverTexture.TextureId, Bounds.absX, Bounds.absY, Bounds.OuterWidth, Bounds.OuterHeight);
    }

    /// <summary>
    /// Обрабатывает отпускание кнопки мыши: воспроизводит звук и вызывает колбэк зоны.
    /// </summary>
    public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
    {
        var pos = Bounds.PositionInside(api.Input.MouseX, api.Input.MouseY);
        if (pos == null)
        {
            return;
        }

        if (!TryGetHoverRegion(pos.X, pos.Y, out var region))
        {
            return;
        }

        switch (region)
        {
            case HoverRegion.RightTop:
                api.Gui.PlaySound("toggleswitch");
                OnMouseDownOnCellRight?.Invoke(elementIndex);
                break;
            case HoverRegion.RightBottom:
                if (AllowDelete)
                {
                    api.Gui.PlaySound("ui_click");
                    OnMouseDownOnCellDelete?.Invoke(elementIndex);
                }

                break;
            default:
                api.Gui.PlaySound("toggleswitch");
                OnMouseDownOnCellLeft?.Invoke(elementIndex);
                break;
        }

        args.Handled = true;
    }

    /// <summary>Нажатие мыши не обрабатывается — логика на <see cref="OnMouseUpOnElement"/>.</summary>
    public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
    {
    }

    /// <summary>Движение мыши не требует отдельной логики — hover рисуется в OnRenderInteractiveElements.</summary>
    public void OnMouseMoveOnElement(MouseEvent args, int elementIndex)
    {
    }

    /// <summary>Освобождает все предварительно созданные GPU-текстуры ячейки.</summary>
    public override void Dispose()
    {
        base.Dispose();
        leftHighlightTexture.Dispose();
        rightTopHighlightTexture.Dispose();
        rightBottomHighlightTexture.Dispose();
        releasedButtonTexture.Dispose();
        pressedButtonTexture.Dispose();
    }
}
