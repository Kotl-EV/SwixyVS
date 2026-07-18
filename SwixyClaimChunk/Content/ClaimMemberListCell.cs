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
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Ячейка списка участников привата с четырьмя колонками действий справа.
/// Фон — Group 470.svg / Group 469 (1).png (<c>textures/gui/member_list_row.png</c>, 435×58).
/// </summary>
public sealed class ClaimMemberListCell : GuiElementTextBase, IGuiElementCell
{
    /// <summary>Group 470 full row design size.</summary>
    private const int RowTexW = 435;
    private const int RowTexH = 58;

    /// <summary>Количество колонок в правой панели: владелец, use, build, удаление.</summary>
    private const int ColumnCount = 4;

    /// <summary>Квадратные кнопки действий (Group 470: 46×46, step 55).</summary>
    private const int UnscaledMinSquareSize = 46;

    /// <summary>Зазор между name plate и кнопками / между кнопками (SVG step 55 − 46).</summary>
    private const int UnscaledBtnGap = 9;

    /// <summary>Name plate width inside texture (face 203 @ x=6).</summary>
    private const int UnscaledNameFaceW = 203;

    /// <summary>Базовый размер иконки в колонке (до масштабирования GUI).</summary>
    private const int UnscaledIconSize = 26;

    /// <summary>Высота face внутри текстуры (46); full row includes outer chrome → 58.</summary>
    private const int UnscaledRowFaceHeight = 46;

    /// <summary>Face top inset inside full 58px texture.</summary>
    private const int UnscaledFaceTop = 6;

    /// <summary>Глубина рельефа кнопочного фона строки.</summary>
    private static readonly int UnscaledDepth = 2;

    private static readonly double[] ColFace = [0.255, 0.176, 0.114]; // #412D1D
    private static readonly double[] ColHi = [0.337, 0.243, 0.169];
    private static readonly double[] ColLo = [0.165, 0.118, 0.078];

    /// <summary>Shared Group 470 / 469(1) row chrome.</summary>
    private static ImageSurface? rowTexture;
    private static bool rowTextureMissing;

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

    /// <summary>Колбэк переключения права use для <see cref="MemberUid"/> (ЛКМ по шестерёнке).</summary>
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

        // Group 471 member names: fill #9F795B (same as settings button labels).
        cell.TitleFont ??= ClaimFontHelper.Create(16, [0x9F / 255.0, 0x79 / 255.0, 0x5B / 255.0, 1.0], bold: true);
        cell.DetailTextFont ??= ClaimFontHelper.Create(12, ClaimFontHelper.ColorAccent, bold: true);
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

    private double GetBtnGap() => scaled(UnscaledBtnGap);

    /// <summary>Ширина правого блока: 4 кнопки + 3 промежутка (SVG step 55).</summary>
    private double GetRightBoxWidth() => GetSquareSize() * ColumnCount + GetBtnGap() * (ColumnCount - 1);

    /// <summary>
    /// X начала правых кнопок. Group 470: first btn @ x=218 of 435 → scale to cell width.
    /// </summary>
    private double GetDividerX(double rightBoxWidth)
    {
        // Prefer texture ratio so hit zones match chrome (218/435).
        var fromTex = Bounds.OuterWidth * (218.0 / RowTexW);
        var fromRight = Bounds.OuterWidth - rightBoxWidth - scaled(6); // right chrome ~6
        // Prefer the texture anchor when cell is ~full row width.
        return System.Math.Abs(Bounds.OuterWidth - scaled(RowTexW)) < scaled(8)
            ? fromTex
            : fromRight;
    }

    /// <summary>Шаг колонки = кнопка + gap (последняя без trailing gap).</summary>
    private double GetColumnStep() => GetSquareSize() + GetBtnGap();

    /// <summary>Доступная ширина name plate слева.</summary>
    private double GetTextAreaWidth(double rightBoxWidth) =>
        System.Math.Max(8, GetDividerX(rightBoxWidth) - scaled(16) - Bounds.absPaddingX);

    /// <summary>
    /// Отрисовывает содержимое ячейки: Group 470 row texture + имя + иконки в 4 колонках.
    /// </summary>
    private void ComposeButton(Context ctx, bool pressed)
    {
        EnsureRowTexture(api);

        var drawW = Bounds.OuterWidth;
        var drawH = Bounds.OuterHeight;
        var rightBoxWidth = GetRightBoxWidth();
        var faceTop = scaled(UnscaledFaceTop);
        var faceH = System.Math.Min(scaled(UnscaledRowFaceHeight), drawH - faceTop);
        pressedYOffset = 0;

        if (cellEntry.DrawAsButton)
        {
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
                // Fallback: name plate + 4 faces (old SVG geometry).
                var nameW = GetDividerX(rightBoxWidth) - GetBtnGap();
                DrawSvgFace(ctx, scaled(6), faceTop, nameW, faceH, pressed);
                var sq = GetSquareSize();
                var step = GetColumnStep();
                var bx0 = GetDividerX(rightBoxWidth);
                for (var i = 0; i < ColumnCount; i++)
                {
                    DrawSvgFace(ctx, bx0 + i * step, faceTop, sq, faceH, pressed);
                }
            }

            if (pressed)
            {
                pressedYOffset = scaled(UnscaledDepth) / 2;
            }
        }

        Font = cellEntry.TitleFont;
        // Name plate content starts ~x=6+pad inside texture.
        var textLeft = Bounds.absPaddingX + scaled(16);
        var textWidth = GetTextAreaWidth(rightBoxWidth);
        titleTextHeight = textUtil.GetMultilineTextHeight(Font, cellEntry.Title, textWidth);
        var titleY = faceTop + (faceH - titleTextHeight) / 2 + pressedYOffset;
        textUtil.AutobreakAndDrawMultilineTextAt(ctx, Font, cellEntry.Title, textLeft, titleY, textWidth);

        DrawAccessIcons(ctx, rightBoxWidth, faceTop + pressedYOffset, faceH);

        if (cellEntry.DrawAsButton && pressed)
        {
            ctx.SetSourceRGBA(0, 0, 0, 0.12);
            ctx.Rectangle(0, 0, drawW, drawH);
            ctx.Fill();
        }
    }

    /// <summary>Loads Group 470 / 469(1) row texture once for all member cells.</summary>
    private static void EnsureRowTexture(ICoreClientAPI capi)
    {
        if (rowTexture != null || rowTextureMissing)
        {
            return;
        }

        try
        {
            var asset = capi.Assets.TryGet(new AssetLocation("swixyclaimchunk", "textures/gui/member_list_row.png"));
            if (asset?.Data == null || asset.Data.Length == 0)
            {
                capi.Logger.Warning("[SwixyClaimChunk] member_list_row.png (Group 470) not found");
                rowTextureMissing = true;
                return;
            }

            using var bitmap = capi.Render.BitmapCreateFromPng(asset.Data);
            rowTexture = GuiElement.getImageSurfaceFromAsset(bitmap);
        }
        catch (Exception ex)
        {
            capi.Logger.Error("[SwixyClaimChunk] Failed to load member_list_row.png: {0}", ex.Message);
            rowTextureMissing = true;
        }
    }

    private static void DrawSvgFace(Context ctx, double x, double y, double w, double h, bool pressed)
    {
        ctx.SetSourceRGB(ColFace[0], ColFace[1], ColFace[2]);
        ctx.Rectangle(x, y, w, h);
        ctx.Fill();

        var bevel = 2.0;
        ctx.SetSourceRGB(ColHi[0], ColHi[1], ColHi[2]);
        ctx.Rectangle(x, y, w, bevel);
        ctx.Fill();
        ctx.Rectangle(x, y, bevel, h);
        ctx.Fill();

        ctx.SetSourceRGB(ColLo[0], ColLo[1], ColLo[2]);
        ctx.Rectangle(x, y + h - bevel, w, bevel);
        ctx.Fill();
        ctx.Rectangle(x + w - bevel, y, bevel, h);
        ctx.Fill();

        if (pressed)
        {
            ctx.SetSourceRGBA(0, 0, 0, 0.12);
            ctx.Rectangle(x, y, w, h);
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
        var sq = GetSquareSize();
        var step = GetColumnStep();
        var faceTop = scaled(UnscaledFaceTop);
        var faceH = System.Math.Min(scaled(UnscaledRowFaceHeight), Bounds.OuterHeight - faceTop);
        var columnIndex = region switch
        {
            HoverRegion.Owner => 0,
            HoverRegion.Use => 1,
            HoverRegion.Build => 2,
            _ => 3
        };

        var x1 = dividerX + columnIndex * step;
        ctx.Rectangle(x1, faceTop, sq, faceH);
        ctx.SetSourceRGBA(0, 0, 0, 0.15);
        ctx.Fill();
        generateTexture(surface, ref texture);
    }

    /// <summary>
    /// Отрисовывает иконки в четырёх кнопках справа: владелец, use, build, удаление.
    /// </summary>
    private void DrawAccessIcons(Context ctx, double rightBoxWidth, double faceTop, double faceH)
    {
        var dividerX = GetDividerX(rightBoxWidth);
        var sq = GetSquareSize();
        var step = GetColumnStep();
        var iconSize = System.Math.Min(scaled(UnscaledIconSize), sq * 0.72);
        var iconY = faceTop + (faceH - iconSize) / 2;

        DrawColumnIcon(ctx, GetIconX(dividerX, step, sq, 0, iconSize), iconY, iconSize, HoverRegion.Owner);
        DrawColumnIcon(ctx, GetIconX(dividerX, step, sq, 1, iconSize), iconY, iconSize, HoverRegion.Use);
        DrawColumnIcon(ctx, GetIconX(dividerX, step, sq, 2, iconSize), iconY, iconSize, HoverRegion.Build);
        ClaimCairoIcons.DrawTrash(
            ctx,
            GetIconX(dividerX, step, sq, 3, iconSize),
            iconY,
            iconSize,
            destructive: !IsOwner);
    }

    private static double GetIconX(double dividerX, double step, double square, int columnIndex, double iconSize)
    {
        return dividerX + columnIndex * step + (square - iconSize) * 0.5;
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
        var faceTop = scaled(UnscaledFaceTop);
        var faceH = System.Math.Min(scaled(UnscaledRowFaceHeight), Bounds.OuterHeight - faceTop);
        if (posY < faceTop || posY > faceTop + faceH)
        {
            region = HoverRegion.Left;
            return false;
        }

        // Hit region: левая name plate
        if (posX < dividerX)
        {
            region = HoverRegion.Left;
            return true;
        }

        var sq = GetSquareSize();
        var step = GetColumnStep();
        var localX = posX - dividerX;
        var columnIndex = (int)(localX / step);
        if (columnIndex < 0)
        {
            columnIndex = 0;
        }
        else if (columnIndex >= ColumnCount)
        {
            columnIndex = ColumnCount - 1;
        }

        // Click only counts if inside the square button, not the gap.
        var inBtn = localX - columnIndex * step;
        if (inBtn < 0 || inBtn > sq)
        {
            region = HoverRegion.Left;
            return false;
        }

        region = columnIndex switch
        {
            0 => HoverRegion.Owner,
            1 => HoverRegion.Use,
            2 => HoverRegion.Build,
            _ => HoverRegion.Delete
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
        Bounds.fixedPaddingX = 0;
        Bounds.fixedPaddingY = 0;

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
        if (args.Button != EnumMouseButton.Left)
        {
            return;
        }

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
