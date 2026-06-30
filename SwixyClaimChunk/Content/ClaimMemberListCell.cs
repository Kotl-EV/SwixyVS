// =============================================================================
// ClaimMemberListCell.cs
// =============================================================================
// Ячейка списка участников привата (claim) в GUI Vintage Story.
// Отображает имя участника слева и четыре интерактивные колонки справа:
// владелец (корона), использование (шестерёнка), строительство (молоток), удаление (корзина).
// Подсветка при наведении только на колонки иконок справа; область с ником без hover.
// =============================================================================

using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Ячейка списка участников привата с четырьмя колонками действий справа.
/// Реализует <see cref="IGuiElementCell"/> для встраивания в список сохранений/участников.
/// Отрисовка выполняется через Cairo: фон кнопки, текст заголовка, разделители и иконки доступа.
/// </summary>
public sealed class ClaimMemberListCell : GuiElementTextBase, IGuiElementCell
{
    /// <summary>Количество колонок в правой панели: владелец, use, build, удаление.</summary>
    private const int ColumnCount = 4;

    /// <summary>Минимальный размер одной квадратной колонки (до масштабирования GUI).</summary>
    private const int UnscaledMinSquareSize = 56;

    /// <summary>Базовый размер иконки в колонке (до масштабирования GUI).</summary>
    private const int UnscaledIconSize = 40;

    /// <summary>Глубина рельефа кнопочного фона строки.</summary>
    private static readonly int UnscaledDepth = 4;

    /// <summary>Запись ячейки списка: заголовок, шрифты, флаг отрисовки как кнопки.</summary>
    public SavegameCellEntry cellEntry;

    /// <summary>Уникальный идентификатор участника для колбэков переключения прав и удаления.</summary>
    public string MemberUid = "";

    /// <summary>Право на использование (use) у участника.</summary>
    public bool AccessUse;

    /// <summary>Право на строительство (build) у участника.</summary>
    public bool AccessBuild;

    /// <summary>Является ли участник владельцем привата (владельца нельзя удалить, права заблокированы).</summary>
    public bool IsOwner;

    /// <summary>Назначен ли участник со-владельцем (корона), независимо от Use/Build.</summary>
    public bool IsCoOwner;

    /// <summary>Может ли текущий игрок менять статус со-владельца (только владелец привата).</summary>
    public bool AllowCoOwnerCrown = true;

    /// <summary>Корона: владелец — светится, со-владелец — цветная, участник — серая.</summary>
    private CrownVisualState CrownState
    {
        get
        {
            if (IsOwner)
            {
                return CrownVisualState.Owner;
            }

            if (IsCoOwner)
            {
                return CrownVisualState.CoOwner;
            }

            return CrownVisualState.Member;
        }
    }

    /// <summary>Колбэк при клике по левой области или колонке владельца (выбор ячейки).</summary>
    public Action<int>? OnMouseDownOnCellLeft;

    /// <summary>Колбэк переключения права use для <see cref="MemberUid"/>.</summary>
    public Action<string>? OnToggleUse;

    /// <summary>Колбэк переключения права build для <see cref="MemberUid"/>.</summary>
    public Action<string>? OnToggleBuild;

    /// <summary>Колбэк передачи владения участнику <see cref="MemberUid"/>.</summary>
    public Action<string>? OnMakeOwner;

    /// <summary>Колбэк удаления участника <see cref="MemberUid"/> из привата.</summary>
    public Action<string>? OnDeleteMember;

    /// <summary>Текстура ячейки в отпущенном (не нажатом) состоянии кнопки.</summary>
    private LoadedTexture releasedButtonTexture;

    /// <summary>Текстура ячейки в нажатом состоянии (с эффектом вдавливания).</summary>
    private LoadedTexture pressedButtonTexture;

    /// <summary>Подсветка при наведении на колонку владельца.</summary>
    private LoadedTexture ownerHighlightTexture;

    /// <summary>Подсветка при наведении на колонку use.</summary>
    private LoadedTexture useHighlightTexture;

    /// <summary>Подсветка при наведении на колонку build.</summary>
    private LoadedTexture buildHighlightTexture;

    /// <summary>Подсветка при наведении на колонку удаления.</summary>
    private LoadedTexture deleteHighlightTexture;

    /// <summary>Вычисленная высота многострочного заголовка для вертикального центрирования текста.</summary>
    private double titleTextHeight;

    /// <summary>Смещение контента при отрисовке нажатой кнопки.</summary>
    private double pressedYOffset;

    /// <summary>Фиксированная высота ячейки; если задана — <see cref="UpdateCellHeight"/> не пересчитывает по тексту.</summary>
    public double? FixedHeight { get; set; }

    /// <summary>Границы ячейки для интерфейса <see cref="IGuiElementCell"/>.</summary>
    ElementBounds IGuiElementCell.Bounds => Bounds;

    /// <summary>
    /// Создаёт ячейку списка участника привата с заданными правами и флагом владельца.
    /// Инициализирует текстуры подсветки и кнопки, настраивает шрифты записи ячейки.
    /// </summary>
    /// <param name="capi">Клиентский API Vintage Story.</param>
    /// <param name="cell">Данные ячейки (заголовок, шрифты).</param>
    /// <param name="bounds">Границы элемента в родительском контейнере.</param>
    /// <param name="accessUse">Начальное значение права use.</param>
    /// <param name="accessBuild">Начальное значение права build.</param>
    /// <param name="isOwner">Участник является владельцем привата.</param>
    public ClaimMemberListCell(
        ICoreClientAPI capi,
        SavegameCellEntry cell,
        ElementBounds bounds,
        bool accessUse,
        bool accessBuild,
        bool isOwner)
        : base(capi, "", null, bounds)
    {
        cellEntry = cell;
        AccessUse = accessUse;
        AccessBuild = accessBuild;
        IsOwner = isOwner;

        // Текстуры основного состояния кнопки: отпущена / нажата
        releasedButtonTexture = new LoadedTexture(capi);
        pressedButtonTexture = new LoadedTexture(capi);
        ownerHighlightTexture = new LoadedTexture(capi);
        useHighlightTexture = new LoadedTexture(capi);
        buildHighlightTexture = new LoadedTexture(capi);
        deleteHighlightTexture = new LoadedTexture(capi);

        // Шрифты заголовка и деталей по умолчанию, если не заданы в cell
        cell.TitleFont ??= CairoFont.WhiteSmallishText();
        cell.DetailTextFont ??= CairoFont.WhiteSmallText();
        cell.DetailTextFont.Color[3] *= 0.8; // приглушённая альфа для вторичного текста
        cell.DetailTextFont.LineHeightMultiplier = 1.1;
    }

    /// <summary>
    /// Собирает текстуры ячейки (released/pressed).
    /// Вызывается лениво из <see cref="OnRenderInteractiveElements"/> при первом рендере.
    /// </summary>
    public void Compose()
    {
        Bounds.CalcWorldBounds();

        // Одна поверхность Cairo: сначала released, затем pressed (с очисткой между проходами)
        using (var surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt))
        using (var ctx = genContext(surface))
        {
            ComposeButton(ctx, false);
            generateTexture(surface, ref releasedButtonTexture);

            ctx.Operator = Operator.Clear;
            ctx.Paint();
            ctx.Operator = Operator.Over;

            ComposeButton(ctx, true);
            generateTexture(surface, ref pressedButtonTexture);
        }

        ComposeColumnHover(HoverRegion.Owner, ref ownerHighlightTexture);
        ComposeColumnHover(HoverRegion.Use, ref useHighlightTexture);
        ComposeColumnHover(HoverRegion.Build, ref buildHighlightTexture);
        ComposeColumnHover(HoverRegion.Delete, ref deleteHighlightTexture);
    }

    /// <summary>
    /// Идентификаторы интерактивных зон ячейки для hit-testing кликов.
    /// Левая область — текст; четыре колонки справа — owner, use, build, delete.
    /// </summary>
    private enum HoverRegion
    {
        /// <summary>Левая часть ячейки с именем/заголовком участника.</summary>
        Left,

        /// <summary>Колонка 0: иконка владельца (корона).</summary>
        Owner,

        /// <summary>Колонка 1: иконка права use (шестерёнка).</summary>
        Use,

        /// <summary>Колонка 2: иконка права build (молоток).</summary>
        Build,

        /// <summary>Колонка 3: иконка удаления участника (корзина).</summary>
        Delete
    }

    /// <summary>Возвращает масштабированный размер одной квадратной колонки справа.</summary>
    private double GetSquareSize() => scaled(UnscaledMinSquareSize);

    /// <summary>Ширина правого блока из четырёх колонок: 4 × размер квадрата.</summary>
    private double GetRightBoxWidth() => GetSquareSize() * ColumnCount;

    /// <summary>X-координата вертикального разделителя между текстом слева и блоком из 4 колонок.</summary>
    private double GetDividerX(double rightBoxWidth) => Bounds.OuterWidth - rightBoxWidth;

    /// <summary>Ширина одной из четырёх равных колонок в правом блоке.</summary>
    private double GetColumnWidth(double rightBoxWidth) => rightBoxWidth / ColumnCount;

    /// <summary>Доступная ширина для многострочного заголовка слева (с учётом отступа).</summary>
    private double GetTextAreaWidth(double rightBoxWidth) => Bounds.OuterWidth - rightBoxWidth - Bounds.absPaddingX;

    /// <summary>
    /// Отрисовывает содержимое ячейки-кнопки: фон, заголовок, разделители и иконки в 4 колонках.
    /// </summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="pressed">true — состояние нажатой кнопки (смещение и затемнение).</param>
    private void ComposeButton(Context ctx, bool pressed)
    {
        var rightBoxWidth = GetRightBoxWidth();
        pressedYOffset = 0;

        if (cellEntry.DrawAsButton)
        {
            RoundRectangle(ctx, 0, 0, Bounds.OuterWidthInt, Bounds.OuterHeightInt, 1);
            ctx.SetSourceRGB(GuiStyle.DialogDefaultBgColor[0], GuiStyle.DialogDefaultBgColor[1], GuiStyle.DialogDefaultBgColor[2]);
            ctx.Fill();

            if (pressed)
            {
                pressedYOffset = scaled(UnscaledDepth) / 2;
            }

            EmbossRoundRectangleElement(ctx, 0, 0, Bounds.OuterWidthInt, Bounds.OuterHeightInt, pressed, (int)scaled(UnscaledDepth));
        }

        // Многострочный заголовок (имя участника) — вертикально по центру левой области
        Font = cellEntry.TitleFont;
        var textWidth = GetTextAreaWidth(rightBoxWidth);
        titleTextHeight = textUtil.GetMultilineTextHeight(Font, cellEntry.Title, textWidth);
        var titleY = (Bounds.OuterHeight - titleTextHeight) / 2 + pressedYOffset;
        textUtil.AutobreakAndDrawMultilineTextAt(ctx, Font, cellEntry.Title, Bounds.absPaddingX, titleY, textWidth);

        // Вертикальные разделители между 4 колонками и границей с текстом
        DrawRightBoxDividers(ctx, rightBoxWidth);

        // Иконки доступа в каждой из 4 колонок (корона, шестерёнка, молоток, корзина)
        DrawAccessIcons(ctx, rightBoxWidth, pressedYOffset);

        if (cellEntry.DrawAsButton && pressed)
        {
            RoundRectangle(ctx, 0, 0, Bounds.OuterWidthInt, Bounds.OuterHeightInt, 1);
            ctx.SetSourceRGBA(0, 0, 0, 0.15);
            ctx.Fill();
        }
    }

    /// <summary>Создаёт текстуру подсветки для одной колонки справа.</summary>
    private void ComposeColumnHover(HoverRegion region, ref LoadedTexture texture)
    {
        using var surface = new ImageSurface(Format.Argb32, (int)Bounds.OuterWidth, (int)Bounds.OuterHeight);
        using var ctx = genContext(surface);

        var rightBoxWidth = GetRightBoxWidth();
        var dividerX = GetDividerX(rightBoxWidth);
        var columnWidth = GetColumnWidth(rightBoxWidth);
        var columnIndex = region switch
        {
            HoverRegion.Owner => 0,
            HoverRegion.Use => 1,
            HoverRegion.Build => 2,
            _ => 3
        };

        var x1 = dividerX + columnIndex * columnWidth;
        var x2 = x1 + columnWidth;
        ctx.NewPath();
        ctx.MoveTo(x1, 0);
        ctx.LineTo(x2, 0);
        ctx.LineTo(x2, Bounds.OuterHeight);
        ctx.LineTo(x1, Bounds.OuterHeight);
        ctx.ClosePath();
        ctx.SetSourceRGBA(0, 0, 0, 0.15);
        ctx.Fill();
        generateTexture(surface, ref texture);
    }

    /// <summary>
    /// Рисует вертикальные разделители правого блока: граница с текстом и три линии между 4 колонками.
    /// Каждая линия — пара штрихов (тёмный + светлый) для объёмного эффекта.
    /// </summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="rightBoxWidth">Ширина блока из четырёх колонок.</param>
    private void DrawRightBoxDividers(Context ctx, double rightBoxWidth)
    {
        var dividerX = GetDividerX(rightBoxWidth);
        var columnWidth = GetColumnWidth(rightBoxWidth);
        ctx.LineWidth = scaled(1);

        // Левая граница правого блока (отделяет текст от колонок) — тёмная тень
        ctx.SetSourceRGBA(0, 0, 0, 0.4);
        ctx.NewPath();
        ctx.MoveTo(dividerX, scaled(1));
        ctx.LineTo(dividerX, Bounds.OuterHeight - scaled(2));
        ctx.ClosePath();
        ctx.Stroke();

        // Светлая кромка справа от тёмной линии (имитация рельефа)
        ctx.SetSourceRGBA(1, 1, 1, 0.3);
        ctx.NewPath();
        ctx.MoveTo(dividerX + scaled(1), scaled(1));
        ctx.LineTo(dividerX + scaled(1), Bounds.OuterHeight - scaled(2));
        ctx.ClosePath();
        ctx.Stroke();

        // Внутренние разделители между колонками 0|1, 1|2, 2|3 (всего ColumnCount - 1 линий)
        for (var column = 1; column < ColumnCount; column++)
        {
            var x = dividerX + column * columnWidth;

            // Тёмная линия между соседними колонками
            ctx.SetSourceRGBA(0, 0, 0, 0.35);
            ctx.NewPath();
            ctx.MoveTo(x, scaled(1));
            ctx.LineTo(x, Bounds.OuterHeight - scaled(2));
            ctx.ClosePath();
            ctx.Stroke();

            // Светлая кромка для объёма
            ctx.SetSourceRGBA(1, 1, 1, 0.22);
            ctx.NewPath();
            ctx.MoveTo(x + scaled(1), scaled(1));
            ctx.LineTo(x + scaled(1), Bounds.OuterHeight - scaled(2));
            ctx.ClosePath();
            ctx.Stroke();
        }
    }

    /// <summary>
    /// Отрисовывает иконки в четырёх колонках справа: владелец, use, build, удаление.
    /// Иконки центрируются по горизонтали в своей колонке и по вертикали в ячейке.
    /// </summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="rightBoxWidth">Ширина правого блока (4 колонки).</param>
    /// <param name="yOffset">Вертикальное смещение при нажатой кнопке.</param>
    private void DrawAccessIcons(Context ctx, double rightBoxWidth, double yOffset)
    {
        var dividerX = GetDividerX(rightBoxWidth);
        var columnWidth = GetColumnWidth(rightBoxWidth);

        // Размер иконки ограничен 90% ширины колонки, чтобы не выходить за границы hit region
        var iconSize = System.Math.Min(scaled(UnscaledIconSize), columnWidth * 0.9);
        var iconY = (Bounds.OuterHeight - iconSize) / 2 + yOffset;

        // Колонка 0: корона владельца
        DrawColumnIcon(ctx, GetIconX(dividerX, columnWidth, 0, iconSize), iconY, iconSize, HoverRegion.Owner);

        // Колонка 1: шестерёнка права use
        DrawColumnIcon(ctx, GetIconX(dividerX, columnWidth, 1, iconSize), iconY, iconSize, HoverRegion.Use);

        // Колонка 2: молоток права build
        DrawColumnIcon(ctx, GetIconX(dividerX, columnWidth, 2, iconSize), iconY, iconSize, HoverRegion.Build);

        // Колонка 3: корзина удаления (destructive-стиль, если не владелец)
        ClaimCairoIcons.DrawTrash(
            ctx,
            GetIconX(dividerX, columnWidth, 3, iconSize),
            iconY,
            iconSize,
            destructive: !IsOwner);
    }

    /// <summary>
    /// Вычисляет левую X-координату иконки для центрирования в колонке с заданным индексом (0..3).
    /// </summary>
    /// <param name="dividerX">X начала правого блока из 4 колонок.</param>
    /// <param name="columnWidth">Ширина одной колонки.</param>
    /// <param name="columnIndex">Индекс колонки: 0 — owner, 1 — use, 2 — build, 3 — delete.</param>
    /// <param name="iconSize">Сторона квадрата иконки.</param>
    /// <returns>Координата левого верхнего угла иконки по X.</returns>
    private static double GetIconX(double dividerX, double columnWidth, int columnIndex, double iconSize)
    {
        var columnCenter = dividerX + (columnIndex + 0.5) * columnWidth;
        return columnCenter - iconSize * 0.5;
    }

    /// <summary>
    /// Делегирует отрисовку иконки соответствующему методу <see cref="ClaimCairoIcons"/> по типу колонки.
    /// Колонка Delete рисуется отдельно через <see cref="ClaimCairoIcons.DrawTrash"/>.
    /// </summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="x">Левая координата иконки.</param>
    /// <param name="y">Верхняя координата иконки.</param>
    /// <param name="size">Размер иконки.</param>
    /// <param name="column">Колонка: Owner, Use или Build.</param>
    private void DrawColumnIcon(Context ctx, double x, double y, double size, HoverRegion column)
    {
        switch (column)
        {
            case HoverRegion.Owner:
                ClaimCairoIcons.DrawOwner(ctx, x, y, size, CrownState);
                break;
            case HoverRegion.Use:
                ClaimCairoIcons.DrawGear(ctx, x, y, size, AccessUse, IsOwner);
                break;
            case HoverRegion.Build:
                ClaimCairoIcons.DrawPickaxe(ctx, x, y, size, AccessBuild, IsOwner);
                break;
        }
    }

    /// <summary>
    /// Определяет зону попадания (hit region) по локальным координатам мыши внутри ячейки.
    /// Слева от dividerX — Left; справа — одна из 4 колонок по индексу localX / columnWidth.
    /// </summary>
    /// <param name="posX">Локальная X относительно ячейки.</param>
    /// <param name="posY">Локальная Y (не используется для разбиения, регионы на всю высоту).</param>
    /// <param name="region">Найденная зона попадания.</param>
    /// <returns>true, если координаты попадают в известную зону.</returns>
    private bool TryGetHoverRegion(double posX, double posY, out HoverRegion region)
    {
        var rightBoxWidth = GetRightBoxWidth();
        var dividerX = GetDividerX(rightBoxWidth);

        // Hit region: левая текстовая область
        if (posX < dividerX)
        {
            region = HoverRegion.Left;
            return true;
        }

        // Hit region: одна из 4 колонок по горизонтали
        var columnWidth = GetColumnWidth(rightBoxWidth);
        var localX = posX - dividerX;
        var columnIndex = (int)(localX / columnWidth);

        // Ограничение индекса в пределах 0..3
        if (columnIndex < 0)
        {
            columnIndex = 0;
        }
        else if (columnIndex >= ColumnCount)
        {
            columnIndex = ColumnCount - 1;
        }

        region = columnIndex switch
        {
            0 => HoverRegion.Owner,   // колонка 0
            1 => HoverRegion.Use,     // колонка 1
            2 => HoverRegion.Build,   // колонка 2
            _ => HoverRegion.Delete   // колонка 3
        };
        return true;
    }

    /// <summary>
    /// Пересчитывает фиксированную высоту ячейки по высоте многострочного заголовка
    /// или использует <see cref="FixedHeight"/>, если задана явно. Минимум 48 единиц.
    /// </summary>
    public void UpdateCellHeight()
    {
        Bounds.CalcWorldBounds();

        if (FixedHeight != null)
        {
            Bounds.fixedHeight = FixedHeight.Value;
            return;
        }

        var unscaledPadding = Bounds.absPaddingY / RuntimeEnv.GUIScale;

        // Ширина текста = внутренняя ширина минус место под 4 квадратные колонки
        var boxWidth = Bounds.InnerWidth / RuntimeEnv.GUIScale - UnscaledMinSquareSize * ColumnCount;

        Font = cellEntry.TitleFont;
        text = cellEntry.Title;
        titleTextHeight = textUtil.GetMultilineTextHeight(Font, cellEntry.Title, boxWidth) / RuntimeEnv.GUIScale;

        Bounds.fixedHeight = unscaledPadding + titleTextHeight + unscaledPadding;
        if (Bounds.fixedHeight < 48)
        {
            Bounds.fixedHeight = 48;
        }
    }

    /// <summary>Рендерит ячейку; подсветка при наведении только на колонки иконок справа.</summary>
    /// <param name="capi">Клиентский API.</param>
    /// <param name="deltaTime">Дельта времени кадра (не используется).</param>
    public void OnRenderInteractiveElements(ICoreClientAPI capi, float deltaTime)
    {
        if (pressedButtonTexture.TextureId == 0)
        {
            Compose();
        }

        capi.Render.Render2DTexturePremultipliedAlpha(
            releasedButtonTexture.TextureId,
            (int)Bounds.absX,
            (int)Bounds.absY,
            Bounds.OuterWidthInt,
            Bounds.OuterHeightInt);

        if (!IsPositionInside(capi.Input.MouseX, capi.Input.MouseY))
        {
            return;
        }

        var pos = Bounds.PositionInside(capi.Input.MouseX, capi.Input.MouseY);
        if (pos == null || !TryGetHoverRegion(pos.X, pos.Y, out var region) || region == HoverRegion.Left)
        {
            return;
        }

        if (region == HoverRegion.Delete && IsOwner)
        {
            return;
        }

        var hoverTexture = region switch
        {
            HoverRegion.Owner => ownerHighlightTexture,
            HoverRegion.Use => useHighlightTexture,
            HoverRegion.Build => buildHighlightTexture,
            _ => deleteHighlightTexture
        };
        capi.Render.Render2DTexturePremultipliedAlpha(
            hoverTexture.TextureId,
            Bounds.absX,
            Bounds.absY,
            Bounds.OuterWidth,
            Bounds.OuterHeight);
    }

    /// <summary>
    /// Обрабатывает отпускание кнопки мыши: определяет hit region и вызывает действие клика.
    /// </summary>
    /// <param name="args">Аргументы события мыши.</param>
    /// <param name="elementIndex">Индекс элемента в списке.</param>
    public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
    {
        if (!IsPositionInside(api.Input.MouseX, api.Input.MouseY))
        {
            return;
        }

        var pos = Bounds.PositionInside(api.Input.MouseX, api.Input.MouseY);
        if (pos == null || !TryGetHoverRegion(pos.X, pos.Y, out var region))
        {
            return;
        }

        args.Handled = true;
        HandleRegionClick(region, elementIndex);
    }

    /// <summary>
    /// Помечает событие нажатия мыши как обработанное, если курсор над интерактивной зоной ячейки.
    /// </summary>
    /// <param name="args">Аргументы события мыши.</param>
    /// <param name="elementIndex">Индекс элемента в списке.</param>
    public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
    {
        if (!IsPositionInside(api.Input.MouseX, api.Input.MouseY))
        {
            return;
        }

        var pos = Bounds.PositionInside(api.Input.MouseX, api.Input.MouseY);
        if (pos == null || !TryGetHoverRegion(pos.X, pos.Y, out _))
        {
            return;
        }

        args.Handled = true;
    }

    /// <summary>
    /// Выполняет действие по клику в зависимости от hit region: переключение прав, удаление или выбор ячейки.
    /// Для не-владельца доступны use, build и delete; владелец блокирует изменение прав и удаление.
    /// </summary>
    /// <param name="region">Зона, по которой кликнули.</param>
    /// <param name="elementIndex">Индекс ячейки в списке (для левого клика).</param>
    private void HandleRegionClick(HoverRegion region, int elementIndex)
    {
        switch (region)
        {
            case HoverRegion.Use:
                if (!IsOwner && !string.IsNullOrEmpty(MemberUid))
                {
                    api.Gui.PlaySound("toggleswitch");
                    OnToggleUse?.Invoke(MemberUid);
                }

                return;
            case HoverRegion.Build:
                if (!IsOwner && !string.IsNullOrEmpty(MemberUid))
                {
                    api.Gui.PlaySound("toggleswitch");
                    OnToggleBuild?.Invoke(MemberUid);
                }

                return;
            case HoverRegion.Delete:
                if (!IsOwner && !string.IsNullOrEmpty(MemberUid))
                {
                    api.Gui.PlaySound("ui_click");
                    OnDeleteMember?.Invoke(MemberUid);
                }

                return;
            case HoverRegion.Owner:
                if (!IsOwner && AllowCoOwnerCrown && !string.IsNullOrEmpty(MemberUid))
                {
                    api.Gui.PlaySound("ui_click");
                    OnMakeOwner?.Invoke(MemberUid);
                    return;
                }

                api.Gui.PlaySound("toggleswitch");
                OnMouseDownOnCellLeft?.Invoke(elementIndex);
                return;
            case HoverRegion.Left:
                api.Gui.PlaySound("toggleswitch");
                OnMouseDownOnCellLeft?.Invoke(elementIndex);
                return;
        }
    }

    /// <summary>Движение мыши не обрабатывается.</summary>
    /// <param name="args">Аргументы события мыши.</param>
    /// <param name="elementIndex">Индекс элемента в списке.</param>
    public void OnMouseMoveOnElement(MouseEvent args, int elementIndex)
    {
    }

    /// <summary>
    /// Освобождает все загруженные текстуры ячейки и базовые ресурсы.
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        ownerHighlightTexture.Dispose();
        useHighlightTexture.Dispose();
        buildHighlightTexture.Dispose();
        deleteHighlightTexture.Dispose();
        releasedButtonTexture.Dispose();
        pressedButtonTexture.Dispose();
    }
}

/// <summary>Визуальное состояние короны в списке участников.</summary>
internal enum CrownVisualState
{
    /// <summary>Обычный участник — серая корона.</summary>
    Member,

    /// <summary>Со-владелец — цветная корона без свечения.</summary>
    CoOwner,

    /// <summary>Владелец — яркая корона со свечением.</summary>
    Owner
}
