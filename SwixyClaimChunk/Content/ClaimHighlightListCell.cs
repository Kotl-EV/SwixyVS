// =============================================================================
// ClaimHighlightListCell.cs
// -----------------------------------------------------------------------------
// Клиентская ячейка списка приватов с тремя зонами клика: основная область (слева),
// переключатель подсветки (лампочка, справа сверху) и удаление (корзина, справа снизу).
// Рисует фон кнопки, многострочный заголовок/описание, иконки и оверлеи наведения.
// Вспомогательные классы ClaimHighlightIconDrawer и ClaimTrashIconDrawer — векторная
// отрисовка иконок через Cairo без внешних текстур.
// =============================================================================

using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Элемент списка приватов с интерактивной подсветкой и кнопкой удаления в правой колонке.
/// Реализует <see cref="IGuiElementCell"/> для встраивания в GuiElementList / диалоги Vintage Story.
/// </summary>
public sealed class ClaimHighlightListCell : GuiElementTextBase, IGuiElementCell
{
    /// <summary>Ширина правой панели с иконками в не масштабированных GUI-единицах.</summary>
    private const int UnscaledRightBoxWidth = 48;

    /// <summary>Базовый размер иконок лампочки и корзины до применения GUIScale.</summary>
    private const int UnscaledIconSize = 26;

    /// <summary>Глубина рельефа (emboss) кнопки в не масштабированных единицах.</summary>
    private static readonly int UnscaledDepth = 4;

    /// <summary>Минимальная высота строки, достаточная для заголовка, статистики и двух правых иконок.</summary>
    private const int UnscaledMinRowHeight = 48;

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
        var rightBoxWidth = scaled(UnscaledRightBoxWidth);
        pressedYOffset = 0;

        if (cellEntry.DrawAsButton)
        {
            RoundRectangle(ctx, 0, 0, Bounds.OuterWidthInt, Bounds.OuterHeightInt, 1);
            ctx.SetSourceRGB(GuiStyle.DialogDefaultBgColor[0], GuiStyle.DialogDefaultBgColor[1], GuiStyle.DialogDefaultBgColor[2]);
            ctx.Fill();

            // При нажатии текст и иконки смещаются вниз на половину глубины рельефа.
            if (pressed)
            {
                pressedYOffset = scaled(UnscaledDepth) / 2;
            }

            EmbossRoundRectangleElement(ctx, 0, 0, Bounds.OuterWidthInt, Bounds.OuterHeightInt, pressed, (int)scaled(UnscaledDepth));
        }

        // Заголовок: ширина минус правая колонка, вертикальные отступы удвоены (стиль списка VS).
        Font = cellEntry.TitleFont;
        var textTop = Bounds.absPaddingY + scaled(cellEntry.LeftOffY) + pressedYOffset;
        titleTextHeight = textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            cellEntry.Title,
            Bounds.absPaddingX,
            textTop,
            Bounds.InnerWidth - rightBoxWidth);

        // Статистика сразу под названием привата.
        Font = cellEntry.DetailTextFont;
        textUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            Font,
            cellEntry.DetailText,
            Bounds.absPaddingX,
            textTop + cellEntry.DetailTextOffY + titleTextHeight,
            Bounds.InnerWidth - rightBoxWidth);

        DrawRightBoxDividers(ctx, rightBoxWidth);
        DrawLightbulbIcon(ctx, rightBoxWidth, pressedYOffset);
        DrawTrashIcon(ctx, rightBoxWidth, pressedYOffset);

        // Полупрозрачное затемнение поверх всей кнопки в нажатом состоянии.
        if (cellEntry.DrawAsButton && pressed)
        {
            RoundRectangle(ctx, 0, 0, Bounds.OuterWidthInt, Bounds.OuterHeightInt, 1);
            ctx.SetSourceRGBA(0, 0, 0, 0.15);
            ctx.Fill();
        }
    }

    /// <summary>
    /// X-координата вертикального разделителя между текстовой зоной и правой панелью иконок.
    /// </summary>
    private double GetDividerX(double rightBoxWidth) => Bounds.OuterWidth - rightBoxWidth;

    /// <summary>
    /// Рисует вертикальный и горизонтальный разделители правой панели (двойные линии для объёма).
    /// </summary>
    private void DrawRightBoxDividers(Context ctx, double rightBoxWidth)
    {
        var dividerX = GetDividerX(rightBoxWidth);
        ctx.LineWidth = scaled(1);

        // Тёмная «тень» вертикального разделителя.
        ctx.SetSourceRGBA(0, 0, 0, 0.4);
        ctx.NewPath();
        ctx.MoveTo(dividerX, scaled(1));
        ctx.LineTo(dividerX, Bounds.OuterHeight - scaled(2));
        ctx.ClosePath();
        ctx.Stroke();

        // Светлая кромка справа от разделителя.
        ctx.SetSourceRGBA(1, 1, 1, 0.3);
        ctx.NewPath();
        ctx.MoveTo(dividerX + scaled(1), scaled(1));
        ctx.LineTo(dividerX + scaled(1), Bounds.OuterHeight - scaled(2));
        ctx.ClosePath();
        ctx.Stroke();

        // Горизонтальный разделитель посередине правой колонки (две зоны клика).
        var midY = Bounds.OuterHeight / 2;
        ctx.SetSourceRGBA(0, 0, 0, 0.4);
        ctx.NewPath();
        ctx.MoveTo(dividerX, midY);
        ctx.LineTo(Bounds.OuterWidth, midY);
        ctx.ClosePath();
        ctx.Stroke();

        ctx.SetSourceRGBA(1, 1, 1, 0.25);
        ctx.NewPath();
        ctx.MoveTo(dividerX, midY + scaled(1));
        ctx.LineTo(Bounds.OuterWidth, midY + scaled(1));
        ctx.ClosePath();
        ctx.Stroke();
    }

    /// <summary>Рисует иконку лампочки в верхней половине правой панели.</summary>
    private void DrawLightbulbIcon(Context ctx, double rightBoxWidth, double yOffset)
    {
        var dividerX = GetDividerX(rightBoxWidth);
        var zoneHeight = Bounds.OuterHeight / 2;
        var iconSize = GetZoneIconSize(rightBoxWidth, zoneHeight);
        GetZoneIconPosition(dividerX, rightBoxWidth, 0, zoneHeight, iconSize, yOffset, out var iconX, out var iconY);
        ClaimHighlightIconDrawer.Draw(ctx, iconX, iconY, iconSize, HighlightActive);
    }

    /// <summary>Рисует иконку корзины в нижней половине правой панели.</summary>
    private void DrawTrashIcon(Context ctx, double rightBoxWidth, double yOffset)
    {
        var dividerX = GetDividerX(rightBoxWidth);
        var zoneHeight = Bounds.OuterHeight / 2;
        var iconSize = GetZoneIconSize(rightBoxWidth, zoneHeight);
        // zoneTop = zoneHeight — нижняя зона начинается от середины ячейки.
        GetZoneIconPosition(dividerX, rightBoxWidth, zoneHeight, zoneHeight, iconSize, yOffset, out var iconX, out var iconY);
        ClaimTrashIconDrawer.Draw(ctx, iconX, iconY, iconSize, destructive: AllowDelete);
    }

    /// <summary>
    /// Вычисляет размер иконки с учётом GUIScale и доступного места в зоне (не вылезает за границы).
    /// </summary>
    private double GetZoneIconSize(double rightBoxWidth, double zoneHeight)
    {
        return System.Math.Min(scaled(UnscaledIconSize), System.Math.Min(rightBoxWidth * 0.82, zoneHeight * 0.78));
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
        var midY = Bounds.OuterHeight / 2;

        ctx.NewPath();
        switch (region)
        {
            case HoverRegion.Left:
                // Прямоугольник от левого края до вертикального разделителя.
                ctx.MoveTo(0, 0);
                ctx.LineTo(dividerX, 0);
                ctx.LineTo(dividerX, Bounds.OuterHeight);
                ctx.LineTo(0, Bounds.OuterHeight);
                break;
            case HoverRegion.RightTop:
                AppendRightZonePath(ctx, dividerX, rightBoxWidth, 0, midY);
                break;
            default:
                AppendRightZonePath(ctx, dividerX, rightBoxWidth, midY, Bounds.OuterHeight);
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

        // Правая колонка делится пополам по вертикали: лампочка и корзина.
        region = posY < Bounds.OuterHeight / 2 ? HoverRegion.RightTop : HoverRegion.RightBottom;
        return true;
    }

    /// <summary>
    /// Пересчитывает <see cref="ElementBounds.fixedHeight"/> по высоте многострочного текста или фиксированному значению.
    /// </summary>
    public void UpdateCellHeight()
    {
        Bounds.CalcWorldBounds();

        if (FixedHeight != null)
        {
            Bounds.fixedHeight = FixedHeight.Value;
            return;
        }

        // Перевод отступов и ширины текста в не масштабированные единицы для расчёта высоты шрифта.
        var unscaledPadding = Bounds.absPaddingY / RuntimeEnv.GUIScale;
        var boxWidth = Bounds.InnerWidth / RuntimeEnv.GUIScale - UnscaledRightBoxWidth;

        Font = cellEntry.TitleFont;
        text = cellEntry.Title;
        titleTextHeight = textUtil.GetMultilineTextHeight(Font, cellEntry.Title, boxWidth) / RuntimeEnv.GUIScale;

        Font = cellEntry.DetailTextFont;
        text = cellEntry.DetailText;
        var detailTextHeight = textUtil.GetMultilineTextHeight(Font, cellEntry.DetailText, boxWidth) / RuntimeEnv.GUIScale;

        var topOffset = System.Math.Max(0, cellEntry.LeftOffY);
        var detailOffset = System.Math.Max(0, cellEntry.DetailTextOffY);

        // Должно совпадать с ComposeButton: верхний отступ + title + detail offset + detail + нижний отступ.
        Bounds.fixedHeight = unscaledPadding + topOffset + titleTextHeight + detailOffset + detailTextHeight + unscaledPadding;
        if (Bounds.fixedHeight < UnscaledMinRowHeight)
        {
            Bounds.fixedHeight = UnscaledMinRowHeight;
        }
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

/// <summary>
/// Векторная отрисовка иконки лампочки (вкл/выкл подсветки привата) средствами Cairo.
/// </summary>
internal static class ClaimHighlightIconDrawer
{
    /// <summary>
    /// Рисует лампочку в заданном квадрате: свечение и насыщенные цвета при <paramref name="active"/>.
    /// </summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="x">Левый верхний угол области иконки.</param>
    /// <param name="y">Левый верхний угол области иконки.</param>
    /// <param name="size">Сторона квадрата иконки.</param>
    /// <param name="active">Подсветка включена — яркое свечение и «нить» внутри колбы.</param>
    public static void Draw(Context ctx, double x, double y, double size, bool active)
    {
        var bulbRadius = size * 0.28;
        var centerX = x + size * 0.5;
        var centerY = y + size * 0.5;

        // Внешнее мягкое свечение только в активном состоянии.
        if (active)
        {
            ctx.SetSourceRGBA(1, 0.88, 0.2, 0.28);
            ctx.Arc(centerX, centerY, bulbRadius * 1.55, 0, Math.PI * 2);
            ctx.Fill();
        }

        // Основной стеклянный шар колбы.
        ctx.SetSourceRGBA(active ? 0.98 : 0.72, active ? 0.9 : 0.74, active ? 0.35 : 0.55, active ? 0.95 : 0.65);
        ctx.Arc(centerX, centerY, bulbRadius, 0, Math.PI * 2);
        ctx.Fill();

        // Блик для объёма (смещён вверх-влево от центра).
        ctx.SetSourceRGBA(active ? 1 : 0.85, active ? 0.95 : 0.8, active ? 0.55 : 0.7, active ? 0.55 : 0.25);
        ctx.Arc(centerX - bulbRadius * 0.25, centerY - bulbRadius * 0.2, bulbRadius * 0.35, 0, Math.PI * 2);
        ctx.Fill();

        // Цоколь лампочки (два прямоугольника разной ширины).
        var baseY = centerY + bulbRadius * 0.82;
        ctx.SetSourceRGBA(0.5, 0.52, 0.56, 0.95);
        ctx.Rectangle(centerX - bulbRadius * 0.5, baseY, bulbRadius, size * 0.16);
        ctx.Fill();

        ctx.SetSourceRGBA(0.35, 0.36, 0.4, 0.95);
        ctx.Rectangle(centerX - bulbRadius * 0.35, baseY + size * 0.14, bulbRadius * 0.7, size * 0.08);
        ctx.Fill();

        // Горизонтальная «нить» накала при активной подсветке.
        if (active)
        {
            ctx.SetSourceRGBA(1, 0.82, 0.15, 0.95);
            ctx.LineWidth = 1.4;
            ctx.MoveTo(centerX - bulbRadius * 0.22, centerY + bulbRadius * 0.05);
            ctx.LineTo(centerX + bulbRadius * 0.22, centerY + bulbRadius * 0.05);
            ctx.Stroke();
        }
    }
}

/// <summary>
/// Векторная отрисовка иконки корзины (удаление привата) с режимом приглушённых цветов.
/// </summary>
internal static class ClaimTrashIconDrawer
{
    /// <summary>
    /// Рисует корзину: крышка, трапециевидное тело и вертикальные «прутья».
    /// </summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="x">Левый верхний угол области иконки.</param>
    /// <param name="y">Левый верхний угол области иконки.</param>
    /// <param name="size">Сторона квадрата иконки.</param>
    /// <param name="destructive">True — красная «опасная» палитра; false — нейтральная серая.</param>
    public static void Draw(Context ctx, double x, double y, double size, bool destructive = true)
    {
        var centerX = x + size * 0.5;
        var lidY = y + size * 0.14;
        var bodyTop = y + size * 0.28;

        if (destructive)
        {
            // Крышка и ручка — акцент удаления.
            ctx.SetSourceRGBA(0.9, 0.42, 0.42, 0.95);
            ctx.Rectangle(x + size * 0.18, lidY, size * 0.64, size * 0.1);
            ctx.Fill();

            ctx.SetSourceRGBA(0.98, 0.55, 0.55, 0.95);
            ctx.LineWidth = 1.3;
            ctx.MoveTo(centerX - size * 0.14, lidY);
            ctx.LineTo(centerX + size * 0.14, lidY);
            ctx.Stroke();

            // Тело корзины — трапеция (сужение к низу).
            ctx.SetSourceRGBA(0.82, 0.32, 0.32, 0.95);
            ctx.MoveTo(x + size * 0.24, bodyTop);
            ctx.LineTo(x + size * 0.76, bodyTop);
            ctx.LineTo(x + size * 0.66, y + size * 0.84);
            ctx.LineTo(x + size * 0.34, y + size * 0.84);
            ctx.ClosePath();
            ctx.Fill();

            ctx.SetSourceRGBA(0.62, 0.22, 0.22, 0.9);
        }
        else
        {
            // Неактивный/приглушённый вариант той же геометрии.
            ctx.SetSourceRGBA(0.56, 0.58, 0.62, 0.55);
            ctx.Rectangle(x + size * 0.18, lidY, size * 0.64, size * 0.1);
            ctx.Fill();

            ctx.SetSourceRGBA(0.64, 0.66, 0.7, 0.55);
            ctx.LineWidth = 1.3;
            ctx.MoveTo(centerX - size * 0.14, lidY);
            ctx.LineTo(centerX + size * 0.14, lidY);
            ctx.Stroke();

            ctx.SetSourceRGBA(0.5, 0.52, 0.56, 0.55);
            ctx.MoveTo(x + size * 0.24, bodyTop);
            ctx.LineTo(x + size * 0.76, bodyTop);
            ctx.LineTo(x + size * 0.66, y + size * 0.84);
            ctx.LineTo(x + size * 0.34, y + size * 0.84);
            ctx.ClosePath();
            ctx.Fill();

            ctx.SetSourceRGBA(0.42, 0.44, 0.48, 0.5);
        }

        // Три вертикальные линии-разделители на корпусе (общие для обоих режимов).
        ctx.LineWidth = 1.1;
        ctx.MoveTo(centerX - size * 0.1, bodyTop + size * 0.06);
        ctx.LineTo(centerX - size * 0.14, y + size * 0.76);
        ctx.MoveTo(centerX, bodyTop + size * 0.06);
        ctx.LineTo(centerX, y + size * 0.76);
        ctx.MoveTo(centerX + size * 0.1, bodyTop + size * 0.06);
        ctx.LineTo(centerX + size * 0.14, y + size * 0.76);
        ctx.Stroke();
    }
}
