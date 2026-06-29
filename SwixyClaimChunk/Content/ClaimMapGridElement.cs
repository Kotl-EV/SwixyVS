// ============================================================================
// Файл: ClaimMapGridElement.cs
// Модуль: Electrical Progressive — Claims
// Назначение: GUI-элемент сетки чанков поверх мировой карты Vintage Story.
//             Отображает состояние приватов (свободно / свой / чужой), позволяет
//             выделять чанки мышью, панорамировать карту и рисовать оверлей
//             с контурами и названиями связанных регионов одного привата.
// Зависимости: Cairo (отрисовка), Vintagestory.API.Client, WorldMapManager.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using SwixyClaimChunk.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Интерактивная сетка чанков на карте приватов.
/// Встраивается в диалог ClaimMapDialog и синхронизируется с серверным состоянием
/// через <see cref="ClaimMapStatePacket"/>.
/// </summary>
public sealed class ClaimMapGridElement : GuiElement
{
    #region Поля и константы

    /// <summary>Фиксированное число видимых чанков по одной оси (квадрат 16×16).</summary>
    private const int FixedVisibleChunks = 16;

    /// <summary>Максимальный радиус запроса чанков у сервера при смене области просмотра.</summary>
    private const int MaxVisibleRequestRadius = 16;

    /// <summary>Толщина декоративной рамки вокруг карты в пикселях.</summary>
    private const double MapBorderThickness = 6;

    /// <summary>Белый цвет для отрисовки текстуры оверлея без тонирования.</summary>
    private static readonly Vec4f White = new(1, 1, 1, 1);

    /// <summary>Колбэк при завершении выделения чанков левой кнопкой мыши.</summary>
    private readonly Action<IReadOnlyList<(int ChunkX, int ChunkZ)>> onChunksSelected;

    /// <summary>Колбэк при изменении области просмотра карты (панорамирование, зум).</summary>
    private readonly Action onViewChanged;

    /// <summary>Встроенный элемент мировой карты Vintage Story (тайлы рельефа и слоёв).</summary>
    private readonly GuiElementMap? worldMapElement;

    /// <summary>Хост-диалог мировой карты; создаётся локально, если глобальный недоступен.</summary>
    private readonly GuiDialogWorldMap? worldMapHost;

    /// <summary>Сетевой канал worldmap для синхронизации видимой области с сервером карт.</summary>
    private readonly IClientNetworkChannel? worldMapChannel;

    /// <summary>Набор выделенных чанков (упакованные координаты long).</summary>
    private readonly HashSet<long> selectedChunks = [];

    /// <summary>Текущее состояние карты, полученное от сервера мода.</summary>
    private ClaimMapStatePacket? state;

    /// <summary>Админ может выделять и снимать приват с чужих чанков.</summary>
    private bool canAdminUnclaimOthers;

    /// <summary>Идентификатор OpenGL-текстуры с оверлеем сетки и подписей.</summary>
    private int textureId;

    /// <summary>Координата X чанка под курсором; int.MinValue — курсор вне сетки.</summary>
    private int hoverChunkX = int.MinValue;

    /// <summary>Координата Z чанка под курсором; int.MinValue — курсор вне сетки.</summary>
    private int hoverChunkZ = int.MinValue;

    /// <summary>Флаг необходимости перерисовки оверлея в следующем кадре.</summary>
    private bool overlayDirty = true;

    /// <summary>Идёт ли сейчас протягивание выделения левой кнопкой.</summary>
    private bool isSelecting;

    /// <summary>Идёт ли панорамирование правой кнопкой.</summary>
    private bool isPanning;

    /// <summary>Было ли реальное смещение во время текущего панорамирования.</summary>
    private bool panMoved;

    /// <summary>X координата мыши в момент начала панорамирования.</summary>
    private double panStartMouseX;

    /// <summary>Y координата мыши в момент начала панорамирования.</summary>
    private double panStartMouseY;

    /// <summary>Мировая X координата центра вида на момент начала панорамирования.</summary>
    private double panViewCenterX;

    /// <summary>Мировая Z координата центра вида на момент начала панорамирования.</summary>
    private double panViewCenterZ;

    /// <summary>Половина ширины вида в блоках на момент начала панорамирования.</summary>
    private double panViewHalfWidth;

    /// <summary>Половина длины вида в блоках на момент начала панорамирования.</summary>
    private double panViewHalfLength;

    /// <summary>Время последней динамической перерисовки оверлея (мс).</summary>
    private long lastDynamicRedrawMs;

    /// <summary>Время последней проверки загрузки тайлов карты (мс).</summary>
    private long lastMapLoadCheckMs;

    /// <summary>Время последнего вызова onViewChanged (для троттлинга 250 мс).</summary>
    private long lastViewChangedCallbackMs;

    #endregion

    /// <summary>
    /// Создаёт элемент сетки чанков и при возможности инициализирует вложенную мировую карту.
    /// </summary>
    /// <param name="capi">Клиентский API Vintage Story.</param>
    /// <param name="bounds">Границы элемента на экране.</param>
    /// <param name="onChunksSelected">Обработчик завершённого выделения чанков.</param>
    /// <param name="onViewChanged">Обработчик смены области просмотра.</param>
    public ClaimMapGridElement(
        ICoreClientAPI capi,
        ElementBounds bounds,
        Action<IReadOnlyList<(int ChunkX, int ChunkZ)>> onChunksSelected,
        Action onViewChanged)
        : base(capi, bounds)
    {
        this.onChunksSelected = onChunksSelected;
        this.onViewChanged = onViewChanged;
        MouseOverCursor = "pointer";

        // Пытаемся подключить стандартную мировую карту игры как подложку
        var worldMapManager = capi.ModLoader.GetModSystem<WorldMapManager>();
        if (worldMapManager == null || capi.World.Player?.Entity == null)
        {
            return;
        }

        GuiDialogWorldMap? host = null;
        try
        {
            worldMapChannel = capi.Network.GetChannel("worldmap");
            host = worldMapManager.worldMapDlg ?? CreateWorldMapHost(capi, worldMapManager);
            worldMapHost = host;
            worldMapElement = new GuiElementMap(worldMapManager.MapLayers, capi, host, bounds, false)
            {
                viewChanged = OnViewChanged,
                viewChangedSync = OnViewChangedSync
            };
        }
        catch (Exception exception)
        {
            // При ошибке инициализации карты показываем только сетку без тайлов
            capi.Logger.Warning("Claim map world map init failed, showing grid only: {0}", exception.Message);
            if (host != null && worldMapManager.worldMapDlg != host)
            {
                host.Dispose();
            }
        }
    }

    /// <summary>
    /// Создаёт локальный экземпляр диалога мировой карты с заглушками колбэков.
    /// </summary>
    /// <param name="capi">Клиентский API.</param>
    /// <param name="worldMapManager">Менеджер слоёв мировой карты.</param>
    /// <returns>Новый диалог мировой карты.</returns>
    private static GuiDialogWorldMap CreateWorldMapHost(ICoreClientAPI capi, WorldMapManager worldMapManager)
    {
        var tabNames = GetWorldMapTabNames(worldMapManager);
        return new GuiDialogWorldMap(OnViewChangedStub, OnViewChangedSyncStub, capi, tabNames);
    }

    /// <summary>Заглушка асинхронного колбэка смены вида (не используется в хосте-заглушке).</summary>
    private static void OnViewChangedStub(List<FastVec2i> _, List<FastVec2i> __)
    {
    }

    /// <summary>Заглушка синхронного колбэка смены вида (не используется в хосте-заглушке).</summary>
    private static void OnViewChangedSyncStub(int _, int __, int ___, int ____)
    {
    }

    /// <summary>
    /// Собирает отсортированный список вкладок мировой карты из зарегистрированных слоёв.
    /// </summary>
    /// <param name="worldMapManager">Менеджер карт мира.</param>
    /// <returns>Имена групп слоёв для вкладок диалога.</returns>
    private static List<string> GetWorldMapTabNames(WorldMapManager worldMapManager)
    {
        var tabs = new Dictionary<string, double>();

        foreach (var layer in worldMapManager.MapLayers)
        {
            if (string.IsNullOrEmpty(layer.LayerGroupCode) || tabs.ContainsKey(layer.LayerGroupCode))
            {
                continue;
            }

            if (!worldMapManager.LayerGroupPositions.TryGetValue(layer.LayerGroupCode, out var position))
            {
                position = 1;
            }

            tabs[layer.LayerGroupCode] = position;
        }

        if (tabs.Count == 0)
        {
            foreach (var entry in worldMapManager.LayerGroupPositions.OrderBy(pair => pair.Value))
            {
                tabs[entry.Key] = entry.Value;
            }
        }

        if (tabs.Count == 0)
        {
            return ["chunks"];
        }

        return tabs.OrderBy(pair => pair.Value).Select(pair => pair.Key).ToList();
    }

    /// <summary>
    /// Применяет новое серверное состояние карты и сбрасывает оверлей на перерисовку.
    /// </summary>
    /// <param name="newState">Пакет состояния от сервера.</param>
    public void SetState(ClaimMapStatePacket newState)
    {
        state = newState;
        canAdminUnclaimOthers = newState.CanAdminUnclaimOthers;
        ApplyFixedMapViewSize();
        MarkOverlayDirty();
    }

    /// <summary>
    /// Центрирует карту на текущей позиции игрока и запрашивает обновление данных.
    /// </summary>
    public void CenterMapOnPlayer()
    {
        var playerPos = api.World.Player?.Entity?.Pos;
        if (playerPos == null)
        {
            return;
        }

        CenterMapToWorld(playerPos.X, playerPos.Z);
        EnsureMapLoaded();
        MarkOverlayDirty();
        NotifyMapViewChanged();
    }

    /// <summary>
    /// Вычисляет параметры запроса чанков у сервера по текущей видимой области карты.
    /// </summary>
    /// <param name="fallbackCenterChunkX">Центр по X, если карта недоступна.</param>
    /// <param name="fallbackCenterChunkZ">Центр по Z, если карта недоступна.</param>
    /// <param name="fallbackRadius">Радиус по умолчанию.</param>
    /// <returns>Кортеж (центр X, центр Z, радиус в чанках).</returns>
    public (int CenterChunkX, int CenterChunkZ, int Radius) GetVisibleRequest(int fallbackCenterChunkX, int fallbackCenterChunkZ, int fallbackRadius)
    {
        if (worldMapElement == null)
        {
            return (fallbackCenterChunkX, fallbackCenterChunkZ, fallbackRadius);
        }

        var view = worldMapElement.CurrentBlockViewBounds;
        if (view.Width <= 1 || view.Length <= 1)
        {
            return (fallbackCenterChunkX, fallbackCenterChunkZ, fallbackRadius);
        }

        // Переводим границы вида из блоков в индексы чанков
        var chunkSize = state?.ChunkSize > 0 ? state.ChunkSize : GlobalConstants.ChunkSize;
        var minChunkX = FloorDiv((int)Math.Floor(view.MinX), chunkSize);
        var maxChunkX = FloorDiv((int)Math.Ceiling(view.MaxX), chunkSize);
        var minChunkZ = FloorDiv((int)Math.Floor(view.MinZ), chunkSize);
        var maxChunkZ = FloorDiv((int)Math.Ceiling(view.MaxZ), chunkSize);
        var centerChunkX = FloorDiv(minChunkX + maxChunkX, 2);
        var centerChunkZ = FloorDiv(minChunkZ + maxChunkZ, 2);
        // +3 чанка запаса по краям, чтобы сервер прислал данные чуть шире видимой области
        var radius = Math.Max(maxChunkX - minChunkX, maxChunkZ - minChunkZ) / 2 + 3;

        return (centerChunkX, centerChunkZ, Math.Clamp(radius, 1, MaxVisibleRequestRadius));
    }

    #region Отрисовка

    /// <summary>
    /// Компонует статические элементы: подложку мировой карты и текстуру оверлея.
    /// </summary>
    /// <param name="ctxStatic">Контекст Cairo для статической отрисовки.</param>
    /// <param name="surface">Поверхность изображения.</param>
    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        worldMapElement?.ComposeElements(ctxStatic, surface);
        ApplyFixedMapViewSize();
        EnsureMapLoaded();
        RecomposeTexture();
    }

    /// <summary>
    /// Отрисовывает интерактивные слои: карту мира и текстуру оверлея сетки.
    /// </summary>
    /// <param name="deltaTime">Время с прошлого кадра.</param>
    public override void RenderInteractiveElements(float deltaTime)
    {
        // Периодически догружаем тайлы карты (не чаще раза в 100 мс)
        if (api.ElapsedMilliseconds - lastMapLoadCheckMs > 100)
        {
            EnsureMapLoaded();
            lastMapLoadCheckMs = api.ElapsedMilliseconds;
        }

        worldMapElement?.RenderInteractiveElements(deltaTime);

        // Перерисовываем оверлей при изменениях или не реже чем раз в 250 мс (hover и т.д.)
        if (overlayDirty || api.ElapsedMilliseconds - lastDynamicRedrawMs > 250)
        {
            RecomposeTexture();
            lastDynamicRedrawMs = api.ElapsedMilliseconds;
        }

        if (textureId != 0)
        {
            Render2DTexture(textureId, Bounds, 70, White);
        }
    }

    /// <summary>Делегирует пост-отрисовку вложенному элементу мировой карты.</summary>
    /// <param name="deltaTime">Время с прошлого кадра.</param>
    public override void PostRenderInteractiveElements(float deltaTime)
    {
        worldMapElement?.PostRenderInteractiveElements(deltaTime);
    }

    /// <summary>
    /// Пересоздаёт Cairo-текстуру оверлея и загружает её в GPU.
    /// </summary>
    private void RecomposeTexture()
    {
        if (Bounds.OuterWidthInt <= 0 || Bounds.OuterHeightInt <= 0)
        {
            return;
        }

        if (textureId != 0)
        {
            api.Render.GLDeleteTexture(textureId);
            textureId = 0;
        }

        using var surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt);
        using var ctx = genContext(surface);
        DrawOverlay(ctx, Bounds.OuterWidth, Bounds.OuterHeight);
        generateTexture(surface, ref textureId, false);
        overlayDirty = false;
    }

    /// <summary>
    /// Рисует полный оверлей: заливку чанков, выделение, контуры приватов и подписи.
    /// </summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="width">Ширина области в пикселях.</param>
    /// <param name="height">Высота области в пикселях.</param>
    private void DrawOverlay(Context ctx, double width, double height)
    {
        ctx.Operator = Operator.Clear;
        ctx.Paint();
        ctx.Operator = Operator.Over;

        if (worldMapElement == null)
        {
            ctx.SetSourceRGB(0.075, 0.078, 0.08);
            ctx.Paint();
            DrawCenteredText(ctx, width, height, "World map unavailable");
            DrawMapBorder(ctx, width, height);
            return;
        }

        if (state == null)
        {
            DrawCenteredText(ctx, width, height, "Loading...");
            DrawMapBorder(ctx, width, height);
            return;
        }

        // Индекс чанков по упакованным координатам для O(1) поиска при отрисовке
        var cellsByCoord = new Dictionary<long, ClaimChunkCellPacket>(state.Chunks.Count);
        foreach (var chunk in state.Chunks)
        {
            cellsByCoord[Pack(chunk.ChunkX, chunk.ChunkZ)] = chunk;
        }

        // Слой 1: заливка цветом по состоянию чанка
        foreach (var chunk in state.Chunks)
        {
            DrawChunk(ctx, chunk);
        }

        // Слой 2: подсветка выделенных чанков
        foreach (var packed in selectedChunks)
        {
            if (cellsByCoord.TryGetValue(packed, out var selectedChunk))
            {
                DrawChunkSelection(ctx, selectedChunk);
            }
        }

        // Слой 3: тонкие границы всех чанков
        foreach (var chunk in state.Chunks)
        {
            DrawChunkBorder(ctx, chunk, 0, 0, 0, 0.42, 1.0);
        }

        // Слой 4: внешние контуры приватов (только грани без соседа того же claimId)
        DrawClaimContours(ctx, cellsByCoord);
        // Слой 5: названия приватов по связным 4-связным регионам
        DrawClaimNames(ctx, cellsByCoord);

        // Подсветка чанка под курсором (если не идёт выделение)
        if (!isSelecting
            && hoverChunkX != int.MinValue
            && cellsByCoord.TryGetValue(Pack(hoverChunkX, hoverChunkZ), out var hoveredChunk))
        {
            DrawChunkBorder(ctx, hoveredChunk, 1, 1, 1, 0.95, 3.0);
        }

        DrawMapBorder(ctx, width, height);
    }

    /// <summary>Рисует чёрную рамку по периметру карты.</summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="width">Ширина области.</param>
    /// <param name="height">Высота области.</param>
    private static void DrawMapBorder(Context ctx, double width, double height)
    {
        var thickness = MapBorderThickness;
        ctx.SetSourceRGBA(0, 0, 0, 1);
        ctx.Rectangle(0, 0, width, thickness);
        ctx.Fill();
        ctx.Rectangle(0, height - thickness - 1, width, thickness + 1);
        ctx.Fill();
        ctx.Rectangle(0, 0, thickness, height);
        ctx.Fill();
        ctx.Rectangle(width - thickness - 1, 0, thickness + 1, height);
        ctx.Fill();
    }

    /// <summary>Заливает прямоугольник чанка цветом, соответствующим его состоянию.</summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="chunk">Данные ячейки чанка.</param>
    private void DrawChunk(Context ctx, ClaimChunkCellPacket chunk)
    {
        if (!TryGetChunkScreenRect(chunk.ChunkX, chunk.ChunkZ, out var x, out var y, out var width, out var height))
        {
            return;
        }

        SetCellColor(ctx, chunk.State);
        ctx.Rectangle(x, y, width, height);
        ctx.Fill();
    }

    /// <summary>Рисует полупрозрачную жёлтую заливку и рамку выделенного чанка.</summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="chunk">Выделенный чанк.</param>
    private void DrawChunkSelection(Context ctx, ClaimChunkCellPacket chunk)
    {
        if (!TryGetChunkScreenRect(chunk.ChunkX, chunk.ChunkZ, out var x, out var y, out var width, out var height))
        {
            return;
        }

        ctx.SetSourceRGBA(1, 0.86, 0.2, 0.34);
        ctx.Rectangle(x, y, width, height);
        ctx.Fill();
        DrawChunkBorder(ctx, chunk, 1, 0.86, 0.2, 0.98, 2.5);
    }

    /// <summary>Рисует обводку прямоугольника чанка заданным цветом и толщиной.</summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="chunk">Чанк для обводки.</param>
    /// <param name="r">Красный канал (0–1).</param>
    /// <param name="g">Зелёный канал (0–1).</param>
    /// <param name="b">Синий канал (0–1).</param>
    /// <param name="a">Прозрачность (0–1).</param>
    /// <param name="lineWidth">Толщина линии в пикселях.</param>
    private void DrawChunkBorder(Context ctx, ClaimChunkCellPacket chunk, double r, double g, double b, double a, double lineWidth)
    {
        if (!TryGetChunkScreenRect(chunk.ChunkX, chunk.ChunkZ, out var x, out var y, out var width, out var height))
        {
            return;
        }

        ctx.SetSourceRGBA(r, g, b, a);
        ctx.LineWidth = lineWidth;
        ctx.Rectangle(x, y, width, height);
        ctx.Stroke();
    }

    /// <summary>
    /// Рисует внешние рёбра контура привата: сторону чанка рисуем только если
    /// соседний чанк принадлежит другому привату или отсутствует в данных.
    /// </summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="cellsByCoord">Индекс чанков по координатам.</param>
    private void DrawClaimContours(Context ctx, IReadOnlyDictionary<long, ClaimChunkCellPacket> cellsByCoord)
    {
        foreach (var chunk in cellsByCoord.Values)
        {
            if (!IsClaimedChunk(chunk) || !TryGetChunkScreenRect(chunk.ChunkX, chunk.ChunkZ, out var x, out var y, out var width, out var height))
            {
                continue;
            }

            SetClaimContourColor(ctx, chunk.State);
            ctx.LineWidth = 3.0;

            // Левая грань — если слева нет чанка того же привата
            if (!HasSameClaimNeighbor(cellsByCoord, chunk, -1, 0))
            {
                DrawLine(ctx, x, y, x, y + height);
            }

            // Правая грань
            if (!HasSameClaimNeighbor(cellsByCoord, chunk, 1, 0))
            {
                DrawLine(ctx, x + width, y, x + width, y + height);
            }

            // Верхняя грань (по Z-)
            if (!HasSameClaimNeighbor(cellsByCoord, chunk, 0, -1))
            {
                DrawLine(ctx, x, y, x + width, y);
            }

            // Нижняя грань (по Z+)
            if (!HasSameClaimNeighbor(cellsByCoord, chunk, 0, 1))
            {
                DrawLine(ctx, x, y + height, x + width, y + height);
            }
        }
    }

    /// <summary>Рисует отрезок линии в контексте Cairo.</summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="x1">Начало X.</param>
    /// <param name="y1">Начало Y.</param>
    /// <param name="x2">Конец X.</param>
    /// <param name="y2">Конец Y.</param>
    private static void DrawLine(Context ctx, double x1, double y1, double x2, double y2)
    {
        ctx.MoveTo(x1, y1);
        ctx.LineTo(x2, y2);
        ctx.Stroke();
    }

    /// <summary>Рисует текст по центру области (сообщения «Loading...» и т.п.).</summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="width">Ширина области.</param>
    /// <param name="height">Высота области.</param>
    /// <param name="text">Текст для отображения.</param>
    private static void DrawCenteredText(Context ctx, double width, double height, string text)
    {
        ctx.SelectFontFace("sans-serif", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(18);
        var extents = ctx.TextExtents(text);
        ctx.SetSourceRGBA(1, 1, 1, 0.78);
        ctx.MoveTo((width - extents.Width) / 2 - extents.XBearing, (height - extents.Height) / 2 - extents.YBearing);
        ctx.ShowText(text);
    }

    /// <summary>
    /// Преобразует мировые координаты чанка в экранный прямоугольник с учётом зума карты.
    /// </summary>
    /// <param name="chunkX">Индекс чанка по X.</param>
    /// <param name="chunkZ">Индекс чанка по Z.</param>
    /// <param name="x">Выход: левый край в пикселях.</param>
    /// <param name="y">Выход: верхний край в пикселях.</param>
    /// <param name="width">Выход: ширина в пикселях.</param>
    /// <param name="height">Выход: высота в пикселях.</param>
    /// <returns>true, если прямоугольник пересекается с видимой областью элемента.</returns>
    private bool TryGetChunkScreenRect(int chunkX, int chunkZ, out double x, out double y, out double width, out double height)
    {
        x = y = width = height = 0;
        if (worldMapElement == null || state == null)
        {
            return false;
        }

        var chunkSize = state.ChunkSize > 0 ? state.ChunkSize : GlobalConstants.ChunkSize;
        var x1 = chunkX * chunkSize;
        var z1 = chunkZ * chunkSize;
        var x2 = x1 + chunkSize;
        var z2 = z1 + chunkSize;

        var topLeft = WorldToLocal(new Vec3d(x1, 0, z1));
        var bottomRight = WorldToLocal(new Vec3d(x2, 0, z2));
        x = Math.Min(topLeft.X, bottomRight.X);
        y = Math.Min(topLeft.Y, bottomRight.Y);
        width = Math.Abs(bottomRight.X - topLeft.X);
        height = Math.Abs(bottomRight.Y - topLeft.Y);

        if (width < 1 || height < 1)
        {
            return false;
        }

        return x <= Bounds.OuterWidth && y <= Bounds.OuterHeight && x + width >= 0 && y + height >= 0;
    }

    /// <summary>Преобразует мировую позицию в локальные координаты вида карты.</summary>
    /// <param name="worldPos">Позиция в мире.</param>
    /// <returns>Локальные координаты вида.</returns>
    private Vec2d WorldToLocal(Vec3d worldPos)
    {
        var viewPos = new Vec2f();
        worldMapElement?.TranslateWorldPosToViewPos(worldPos, ref viewPos);
        return new Vec2d(viewPos.X, viewPos.Y);
    }

    /// <summary>Преобразует экранные координаты мыши в локальные координаты элемента.</summary>
    /// <param name="mouseX">X на экране.</param>
    /// <param name="mouseY">Y на экране.</param>
    /// <returns>Локальные координаты относительно Bounds.</returns>
    private Vec2f ScreenToLocal(int mouseX, int mouseY)
    {
        return new Vec2f((float)(mouseX - Bounds.renderX), (float)(mouseY - Bounds.renderY));
    }

    /// <summary>Центрирует вид карты на заданных мировых координатах.</summary>
    /// <param name="x">Мировая X.</param>
    /// <param name="z">Мировая Z.</param>
    private void CenterMapToWorld(double x, double z)
    {
        if (worldMapElement == null)
        {
            return;
        }

        SetFixedMapView(x, z);
    }

    /// <summary>
    /// Поддерживает фиксированный размер вида (16×16 чанков); сохраняет центр или ставит на игрока.
    /// </summary>
    private void ApplyFixedMapViewSize()
    {
        if (worldMapElement == null)
        {
            return;
        }

        var view = worldMapElement.CurrentBlockViewBounds;
        if (view.Width > 1 && view.Length > 1)
        {
            SetFixedMapView((view.MinX + view.MaxX) / 2, (view.MinZ + view.MaxZ) / 2);
            return;
        }

        var playerPos = api.World.Player?.Entity?.Pos;
        if (playerPos != null)
        {
            SetFixedMapView(playerPos.X, playerPos.Z);
        }
    }

    /// <summary>
    /// Устанавливает зум и границы вида так, чтобы на экране помещалось ровно FixedVisibleChunks чанков.
    /// </summary>
    /// <param name="centerX">Центр вида по X в блоках.</param>
    /// <param name="centerZ">Центр вида по Z в блоках.</param>
    private void SetFixedMapView(double centerX, double centerZ)
    {
        if (worldMapElement == null || Bounds.InnerWidth <= 0)
        {
            return;
        }

        var sizeInBlocks = GetChunkSize() * FixedVisibleChunks;
        var halfSize = sizeInBlocks / 2.0;

        worldMapElement.ZoomLevel = (float)(Bounds.InnerWidth / sizeInBlocks);
        worldMapElement.CurrentBlockViewBounds = new Cuboidd(
            centerX - halfSize,
            0,
            centerZ - halfSize,
            centerX + halfSize,
            0,
            centerZ + halfSize);
    }

    /// <summary>Запрашивает полную догрузку тайлов мировой карты для текущего вида.</summary>
    private void EnsureMapLoaded()
    {
        worldMapElement?.EnsureMapFullyLoaded();
    }

    /// <summary>Помечает оверлей как устаревший для перерисовки в следующем кадре.</summary>
    private void MarkOverlayDirty()
    {
        overlayDirty = true;
    }

    #endregion

    #region Ввод

    /// <summary>
    /// Обрабатывает нажатие мыши: ЛКМ — начало выделения, ПКМ — начало панорамирования.
    /// </summary>
    /// <param name="api">Клиентский API.</param>
    /// <param name="args">Аргументы события мыши.</param>
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!Bounds.PointInside(args.X, args.Y))
        {
            return;
        }

        args.Handled = true;

        if (args.Button == EnumMouseButton.Left)
        {
            if (TryGetSelectableChunkAt(args.X, args.Y, out var chunkX, out var chunkZ))
            {
                isSelecting = true;
                AddSelectedChunk(chunkX, chunkZ);
            }

            return;
        }

        if (args.Button == EnumMouseButton.Right && worldMapElement != null)
        {
            BeginPan(args.X, args.Y);
        }
    }

    /// <summary>Обрабатывает отпускание кнопки мыши над элементом.</summary>
    /// <param name="api">Клиентский API.</param>
    /// <param name="args">Аргументы события мыши.</param>
    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        HandleMouseUp(args);
    }

    /// <summary>Обрабатывает глобальное отпускание кнопки мыши (выделение за пределами элемента).</summary>
    /// <param name="api">Клиентский API.</param>
    /// <param name="args">Аргументы события мыши.</param>
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        HandleMouseUp(args);
    }

    /// <summary>
    /// Обрабатывает движение мыши: панорамирование, протягивание выделения, обновление hover.
    /// </summary>
    /// <param name="api">Клиентский API.</param>
    /// <param name="args">Аргументы события мыши.</param>
    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (isPanning && api.Input.MouseButton.Right)
        {
            UpdatePan(args.X, args.Y);
        }

        if (isSelecting && api.Input.MouseButton.Left)
        {
            if (TryGetSelectableChunkAt(args.X, args.Y, out var chunkX, out var chunkZ))
            {
                AddSelectedChunk(chunkX, chunkZ);
            }
        }

        var previousX = hoverChunkX;
        var previousZ = hoverChunkZ;

        if (!TryGetChunkAt(args.X, args.Y, out hoverChunkX, out hoverChunkZ))
        {
            hoverChunkX = int.MinValue;
            hoverChunkZ = int.MinValue;
        }

        if (previousX != hoverChunkX || previousZ != hoverChunkZ)
        {
            MarkOverlayDirty();
        }
    }

    /// <summary>Перехватывает колесо мыши над картой, чтобы не прокручивать фон диалога.</summary>
    /// <param name="api">Клиентский API.</param>
    /// <param name="args">Аргументы колеса мыши.</param>
    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (Bounds.PointInside(api.Input.MouseX, api.Input.MouseY))
        {
            args.SetHandled(true);
        }
    }

    /// <summary>
    /// Освобождает GPU-текстуру, вложенную карту и локальный хост диалога.
    /// </summary>
    public override void Dispose()
    {
        if (textureId != 0)
        {
            api.Render.GLDeleteTexture(textureId);
            textureId = 0;
        }

        worldMapElement?.Dispose();

        if (worldMapHost != null && api.ModLoader.GetModSystem<WorldMapManager>()?.worldMapDlg != worldMapHost)
        {
            worldMapHost.Dispose();
        }

        base.Dispose();
    }

    /// <summary>Завершает выделение или панорамирование при отпускании соответствующей кнопки.</summary>
    /// <param name="args">Аргументы события мыши.</param>
    private void HandleMouseUp(MouseEvent args)
    {
        if (args.Button == EnumMouseButton.Left && isSelecting)
        {
            CommitSelection();
            isSelecting = false;
            args.Handled = true;
            return;
        }

        if (args.Button == EnumMouseButton.Right && isPanning)
        {
            EndPan();
            args.Handled = true;
        }
    }

    /// <summary>
    /// Отправляет накопленное выделение через колбэк и очищает временный набор.
    /// </summary>
    private void CommitSelection()
    {
        if (selectedChunks.Count == 0)
        {
            return;
        }

        var chunks = new List<(int ChunkX, int ChunkZ)>(selectedChunks.Count);
        foreach (var packed in selectedChunks)
        {
            chunks.Add((UnpackX(packed), UnpackZ(packed)));
        }

        selectedChunks.Clear();
        MarkOverlayDirty();
        onChunksSelected(chunks);
    }

    /// <summary>Запоминает начальное состояние для панорамирования правой кнопкой.</summary>
    /// <param name="mouseX">X мыши.</param>
    /// <param name="mouseY">Y мыши.</param>
    private void BeginPan(double mouseX, double mouseY)
    {
        if (worldMapElement == null)
        {
            return;
        }

        var view = worldMapElement.CurrentBlockViewBounds;
        if (view.Width <= 1 || view.Length <= 1)
        {
            return;
        }

        isPanning = true;
        panMoved = false;
        panStartMouseX = mouseX;
        panStartMouseY = mouseY;
        panViewCenterX = (view.MinX + view.MaxX) / 2;
        panViewCenterZ = (view.MinZ + view.MaxZ) / 2;
        panViewHalfWidth = view.Width / 2;
        panViewHalfLength = view.Length / 2;
    }

    /// <summary>
    /// Смещает центр вида карты пропорционально движению мыши с учётом текущего зума.
    /// </summary>
    /// <param name="mouseX">Текущая X мыши.</param>
    /// <param name="mouseY">Текущая Y мыши.</param>
    private void UpdatePan(double mouseX, double mouseY)
    {
        if (worldMapElement == null)
        {
            return;
        }

        var dx = mouseX - panStartMouseX;
        var dy = mouseY - panStartMouseY;
        if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1)
        {
            panMoved = true;
        }

        // Перевод пикселей экрана в блоки мира через уровень зума
        var zoom = Math.Max(worldMapElement.ZoomLevel, 0.0001f);
        var centerX = panViewCenterX - dx / zoom;
        var centerZ = panViewCenterZ - dy / zoom;

        worldMapElement.CurrentBlockViewBounds = new Cuboidd(
            centerX - panViewHalfWidth,
            0,
            centerZ - panViewHalfLength,
            centerX + panViewHalfWidth,
            0,
            centerZ + panViewHalfLength);
        MarkOverlayDirty();
    }

    /// <summary>Завершает панорамирование; при реальном сдвиге уведомляет об изменении вида.</summary>
    private void EndPan()
    {
        isPanning = false;
        if (panMoved)
        {
            NotifyMapViewChanged();
        }
    }

    /// <summary>Добавляет чанк в набор выделения, если его там ещё не было.</summary>
    /// <param name="chunkX">Индекс чанка X.</param>
    /// <param name="chunkZ">Индекс чанка Z.</param>
    private void AddSelectedChunk(int chunkX, int chunkZ)
    {
        if (selectedChunks.Add(Pack(chunkX, chunkZ)))
        {
            MarkOverlayDirty();
        }
    }

    /// <summary>
    /// Определяет чанк под курсором, доступный для выделения (свободный или свой).
    /// </summary>
    /// <param name="mouseX">X мыши.</param>
    /// <param name="mouseY">Y мыши.</param>
    /// <param name="chunkX">Выход: индекс чанка X.</param>
    /// <param name="chunkZ">Выход: индекс чанка Z.</param>
    /// <returns>true, если чанк найден и его можно выделить.</returns>
    private bool TryGetSelectableChunkAt(int mouseX, int mouseY, out int chunkX, out int chunkZ)
    {
        if (!TryGetChunkAt(mouseX, mouseY, out chunkX, out chunkZ))
        {
            return false;
        }

        return TryGetChunkCell(chunkX, chunkZ, out var cell)
            && (cell.State == ClaimChunkCellState.Free
                || cell.State == ClaimChunkCellState.Own
                || (canAdminUnclaimOthers && cell.State == ClaimChunkCellState.Other));
    }

    /// <summary>
    /// Преобразует экранные координаты в индексы чанка под курсором.
    /// </summary>
    /// <param name="mouseX">X мыши.</param>
    /// <param name="mouseY">Y мыши.</param>
    /// <param name="chunkX">Выход: индекс чанка X.</param>
    /// <param name="chunkZ">Выход: индекс чанка Z.</param>
    /// <returns>true, если точка внутри элемента и карта доступна.</returns>
    private bool TryGetChunkAt(int mouseX, int mouseY, out int chunkX, out int chunkZ)
    {
        chunkX = 0;
        chunkZ = 0;

        if (!Bounds.PointInside(mouseX, mouseY) || worldMapElement == null)
        {
            return false;
        }

        var worldPos = ScreenToWorld(mouseX, mouseY);
        var chunkSize = state?.ChunkSize > 0 ? state.ChunkSize : GlobalConstants.ChunkSize;
        chunkX = FloorDiv((int)Math.Floor(worldPos.X), chunkSize);
        chunkZ = FloorDiv((int)Math.Floor(worldPos.Z), chunkSize);
        return true;
    }

    /// <summary>Преобразует экранные координаты мыши в мировые блоки через карту.</summary>
    /// <param name="mouseX">X мыши.</param>
    /// <param name="mouseY">Y мыши.</param>
    /// <returns>Мировая позиция (Y не используется).</returns>
    private Vec3d ScreenToWorld(int mouseX, int mouseY)
    {
        if (worldMapElement == null)
        {
            return Vec3d.Zero;
        }

        var worldPos = new Vec3d();
        worldMapElement.TranslateViewPosToWorldPos(ScreenToLocal(mouseX, mouseY), ref worldPos);
        return worldPos;
    }

    /// <summary>
    /// Колбэк смены вида мировой карты: обновляет слои и запрашивает перерисовку оверлея.
    /// </summary>
    /// <param name="nowVisibleChunks">Чанки, ставшие видимыми.</param>
    /// <param name="nowHiddenChunks">Чанки, скрывшиеся из вида.</param>
    private void OnViewChanged(List<FastVec2i> nowVisibleChunks, List<FastVec2i> nowHiddenChunks)
    {
        if (worldMapElement != null)
        {
            foreach (var layer in worldMapElement.mapLayers)
            {
                layer.OnViewChangedClient(nowVisibleChunks, nowHiddenChunks);
            }
        }

        MarkOverlayDirty();
        NotifyMapViewChanged();
    }

    /// <summary>
    /// Синхронно уведомляет сервер worldmap о новых границах вида для подгрузки тайлов.
    /// </summary>
    /// <param name="x1">Минимальная X граница вида.</param>
    /// <param name="z1">Минимальная Z граница вида.</param>
    /// <param name="x2">Максимальная X граница вида.</param>
    /// <param name="z2">Максимальная Z граница вида.</param>
    private void OnViewChangedSync(int x1, int z1, int x2, int z2)
    {
        worldMapChannel?.SendPacket(new OnViewChangedPacket
        {
            X1 = x1,
            Z1 = z1,
            X2 = x2,
            Z2 = z2
        });

        NotifyMapViewChanged();
    }

    /// <summary>
    /// Вызывает onViewChanged с троттлингом 250 мс, чтобы не спамить запросами к серверу.
    /// </summary>
    private void NotifyMapViewChanged()
    {
        var now = api.ElapsedMilliseconds;
        if (now - lastViewChangedCallbackMs < 250)
        {
            return;
        }

        lastViewChangedCallbackMs = now;
        onViewChanged();
    }

    #endregion

    #region Состояния чанков

    /// <summary>Ищет ячейку чанка в текущем серверном состоянии по координатам.</summary>
    /// <param name="chunkX">Индекс чанка X.</param>
    /// <param name="chunkZ">Индекс чанка Z.</param>
    /// <param name="cell">Выход: найденная ячейка.</param>
    /// <returns>true, если чанк присутствует в state.Chunks.</returns>
    private bool TryGetChunkCell(int chunkX, int chunkZ, out ClaimChunkCellPacket cell)
    {
        cell = null!;
        if (state == null)
        {
            return false;
        }

        foreach (var chunk in state.Chunks)
        {
            if (chunk.ChunkX == chunkX && chunk.ChunkZ == chunkZ)
            {
                cell = chunk;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Проверяет, есть ли у чанка сосед с тем же ClaimId (для отрисовки внутренних границ).
    /// </summary>
    /// <param name="cellsByCoord">Индекс чанков.</param>
    /// <param name="chunk">Текущий чанк.</param>
    /// <param name="offsetX">Смещение соседа по X (-1, 0, 1).</param>
    /// <param name="offsetZ">Смещение соседа по Z (-1, 0, 1).</param>
    /// <returns>true, если сосед занят тем же приватом.</returns>
    private static bool HasSameClaimNeighbor(
        IReadOnlyDictionary<long, ClaimChunkCellPacket> cellsByCoord,
        ClaimChunkCellPacket chunk,
        int offsetX,
        int offsetZ)
    {
        return cellsByCoord.TryGetValue(Pack(chunk.ChunkX + offsetX, chunk.ChunkZ + offsetZ), out var neighbor)
            && neighbor.ClaimId == chunk.ClaimId
            && IsClaimedChunk(neighbor);
    }

    /// <summary>Определяет, занят ли чанк каким-либо приватом (свой или чужой).</summary>
    /// <param name="chunk">Ячейка чанка.</param>
    /// <returns>true для состояний Own и Other с ненулевым ClaimId.</returns>
    private static bool IsClaimedChunk(ClaimChunkCellPacket chunk)
    {
        return chunk.ClaimId > 0
            && (chunk.State == ClaimChunkCellState.Own || chunk.State == ClaimChunkCellState.Other);
    }

    /// <summary>Устанавливает цвет заливки чанка в зависимости от <see cref="ClaimChunkCellState"/>.</summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="cellState">Числовой код состояния ячейки.</param>
    private static void SetCellColor(Context ctx, int cellState)
    {
        switch (cellState)
        {
            case ClaimChunkCellState.Free:
                ctx.SetSourceRGBA(0.2, 0.72, 0.38, 0.18);
                break;
            case ClaimChunkCellState.Own:
                ctx.SetSourceRGBA(0.0, 0.78, 0.92, 0.34);
                break;
            case ClaimChunkCellState.Other:
                ctx.SetSourceRGBA(0.9, 0.18, 0.14, 0.38);
                break;
            default:
                ctx.SetSourceRGBA(0.02, 0.02, 0.025, 0.58);
                break;
        }
    }

    /// <summary>Устанавливает цвет контура привата: голубой для своих, красный для чужих.</summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="cellState">Состояние ячейки.</param>
    private static void SetClaimContourColor(Context ctx, int cellState)
    {
        if (cellState == ClaimChunkCellState.Own)
        {
            ctx.SetSourceRGBA(0.0, 0.92, 1.0, 0.98);
            return;
        }

        ctx.SetSourceRGBA(1.0, 0.26, 0.2, 0.98);
    }

    /// <summary>Возвращает размер чанка из состояния или глобальную константу игры.</summary>
    /// <returns>Размер стороны чанка в блоках.</returns>
    private int GetChunkSize()
    {
        return state?.ChunkSize > 0 ? state.ChunkSize : GlobalConstants.ChunkSize;
    }

    #endregion

    #region Оверлей названий приватов

    /// <summary>
    /// Рисует по одной подписи на каждый связный 4-связный регион чанков одного привата.
    /// Алгоритм: обход в ширину (BFS) по соседям с одинаковым ClaimId; подпись — по центру bounding box региона.
    /// </summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="cellsByCoord">Индекс всех чанков в текущем состоянии.</param>
    private void DrawClaimNames(Context ctx, IReadOnlyDictionary<long, ClaimChunkCellPacket> cellsByCoord)
    {
        var visited = new HashSet<long>();

        foreach (var chunk in cellsByCoord.Values)
        {
            if (!IsClaimedChunk(chunk))
            {
                continue;
            }

            var startKey = Pack(chunk.ChunkX, chunk.ChunkZ);
            // Уже обработанный чанк входит в регион, найденный ранее из другой стартовой точки
            if (visited.Contains(startKey))
            {
                continue;
            }

            // BFS: собираем все чанки одного привата, 4-связные друг с другом
            var region = CollectConnectedClaimRegion(cellsByCoord, chunk, visited);
            if (region.Count == 0 || string.IsNullOrWhiteSpace(region[0].ClaimName))
            {
                continue;
            }

            if (!TryGetRegionScreenBounds(region, out var x, out var y, out var width, out var height))
            {
                continue;
            }

            DrawClaimNameLabel(ctx, region[0].ClaimName, x, y, width, height);
        }
    }

    /// <summary>
    /// Обход в ширину: собирает все чанки одного ClaimId, смежные по сторонам (не по диагонали).
    /// </summary>
    /// <param name="cellsByCoord">Индекс чанков.</param>
    /// <param name="start">Стартовый чанк региона.</param>
    /// <param name="visited">Глобальный набор уже обработанных координат (между регионами).</param>
    /// <returns>Список чанков связного компонента.</returns>
    private static List<ClaimChunkCellPacket> CollectConnectedClaimRegion(
        IReadOnlyDictionary<long, ClaimChunkCellPacket> cellsByCoord,
        ClaimChunkCellPacket start,
        HashSet<long> visited)
    {
        var region = new List<ClaimChunkCellPacket>();
        var queue = new Queue<ClaimChunkCellPacket>();
        var claimId = start.ClaimId;
        var startKey = Pack(start.ChunkX, start.ChunkZ);

        visited.Add(startKey);
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            region.Add(current);

            // 4-связность: только ортогональные соседи
            TryEnqueueNeighbor(cellsByCoord, current.ChunkX - 1, current.ChunkZ, claimId, visited, queue);
            TryEnqueueNeighbor(cellsByCoord, current.ChunkX + 1, current.ChunkZ, claimId, visited, queue);
            TryEnqueueNeighbor(cellsByCoord, current.ChunkX, current.ChunkZ - 1, claimId, visited, queue);
            TryEnqueueNeighbor(cellsByCoord, current.ChunkX, current.ChunkZ + 1, claimId, visited, queue);
        }

        return region;
    }

    /// <summary>
    /// Добавляет соседний чанк в очередь BFS, если он того же привата и ещё не посещён.
    /// </summary>
    /// <param name="cellsByCoord">Индекс чанков.</param>
    /// <param name="chunkX">X соседа.</param>
    /// <param name="chunkZ">Z соседа.</param>
    /// <param name="claimId">Идентификатор привата для фильтрации.</param>
    /// <param name="visited">Посещённые координаты.</param>
    /// <param name="queue">Очередь обхода.</param>
    private static void TryEnqueueNeighbor(
        IReadOnlyDictionary<long, ClaimChunkCellPacket> cellsByCoord,
        int chunkX,
        int chunkZ,
        int claimId,
        HashSet<long> visited,
        Queue<ClaimChunkCellPacket> queue)
    {
        var key = Pack(chunkX, chunkZ);
        if (visited.Contains(key)
            || !cellsByCoord.TryGetValue(key, out var neighbor)
            || !IsClaimedChunk(neighbor)
            || neighbor.ClaimId != claimId)
        {
            return;
        }

        visited.Add(key);
        queue.Enqueue(neighbor);
    }

    /// <summary>
    /// Вычисляет экранный ограничивающий прямоугольник региона чанков.
    /// </summary>
    /// <param name="region">Список чанков связного региона.</param>
    /// <param name="x">Выход: левый край.</param>
    /// <param name="y">Выход: верхний край.</param>
    /// <param name="width">Выход: ширина.</param>
    /// <param name="height">Выход: высота.</param>
    /// <returns>true, если регион достаточно велик для отображения текста (≥12×10 px).</returns>
    private bool TryGetRegionScreenBounds(
        IReadOnlyList<ClaimChunkCellPacket> region,
        out double x,
        out double y,
        out double width,
        out double height)
    {
        x = y = width = height = 0;
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var hasBounds = false;

        foreach (var chunk in region)
        {
            if (!TryGetChunkScreenRect(chunk.ChunkX, chunk.ChunkZ, out var chunkX, out var chunkY, out var chunkW, out var chunkH))
            {
                continue;
            }

            hasBounds = true;
            minX = Math.Min(minX, chunkX);
            minY = Math.Min(minY, chunkY);
            maxX = Math.Max(maxX, chunkX + chunkW);
            maxY = Math.Max(maxY, chunkY + chunkH);
        }

        if (!hasBounds)
        {
            return false;
        }

        x = minX;
        y = minY;
        width = maxX - minX;
        height = maxY - minY;
        return width >= 12 && height >= 10;
    }

    /// <summary>
    /// Рисует название привата по центру региона с тенью и подбором размера шрифта.
    /// </summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="claimName">Текст названия.</param>
    /// <param name="x">Левый край области.</param>
    /// <param name="y">Верхний край области.</param>
    /// <param name="width">Ширина области.</param>
    /// <param name="height">Высота области.</param>
    private static void DrawClaimNameLabel(Context ctx, string claimName, double x, double y, double width, double height)
    {
        ctx.SelectFontFace("sans-serif", FontSlant.Normal, FontWeight.Bold);

        var maxWidth = Math.Max(8, width - 6);
        var maxHeight = Math.Max(8, height - 4);
        var fontSize = ResolveClaimLabelFontSize(ctx, claimName, maxWidth, maxHeight, width, height);
        ctx.SetFontSize(fontSize);

        var text = FitTextToWidth(ctx, claimName, maxWidth);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var extents = ctx.TextExtents(text);
        var textX = x + (width - extents.Width) / 2 - extents.XBearing;
        var textY = y + (height - extents.Height) / 2 - extents.YBearing;

        // Тень для читаемости на любом фоне карты
        ctx.SetSourceRGBA(0, 0, 0, 0.8);
        ctx.MoveTo(textX + 1, textY + 1);
        ctx.ShowText(text);

        ctx.SetSourceRGBA(1, 1, 1, 0.96);
        ctx.MoveTo(textX, textY);
        ctx.ShowText(text);
    }

    /// <summary>
    /// Подбирает максимальный размер шрифта, при котором текст помещается в прямоугольник региона.
    /// Начальная оценка — от площади региона (sqrt(area) * 0.17), затем уменьшение до 8 pt.
    /// </summary>
    /// <param name="ctx">Контекст Cairo.</param>
    /// <param name="claimName">Исходное название.</param>
    /// <param name="maxWidth">Доступная ширина текста.</param>
    /// <param name="maxHeight">Доступная высота текста.</param>
    /// <param name="regionWidth">Ширина региона (для эвристики).</param>
    /// <param name="regionHeight">Высота региона (для эвристики).</param>
    /// <returns>Размер шрифта в пунктах.</returns>
    private static double ResolveClaimLabelFontSize(
        Context ctx,
        string claimName,
        double maxWidth,
        double maxHeight,
        double regionWidth,
        double regionHeight)
    {
        var targetSize = Math.Clamp(Math.Sqrt(regionWidth * regionHeight) * 0.17, 9, 34);
        var trimmed = claimName.Trim();

        for (var size = (int)Math.Floor(targetSize); size >= 8; size--)
        {
            ctx.SetFontSize(size);
            var fitted = FitTextToWidth(ctx, trimmed, maxWidth);
            var extents = ctx.TextExtents(fitted);
            if (extents.Width <= maxWidth && extents.Height <= maxHeight)
            {
                return size;
            }
        }

        return 8;
    }

    /// <summary>
    /// Обрезает текст с многоточием, если он шире допустимой ширины.
    /// </summary>
    /// <param name="ctx">Контекст Cairo (должен иметь установленный размер шрифта).</param>
    /// <param name="text">Исходный текст.</param>
    /// <param name="maxWidth">Максимальная ширина в пикселях.</param>
    /// <returns>Умещённый текст или «...».</returns>
    private static string FitTextToWidth(Context ctx, string text, double maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var trimmed = text.Trim();
        if (ctx.TextExtents(trimmed).Width <= maxWidth)
        {
            return trimmed;
        }

        const string ellipsis = "...";
        for (var length = trimmed.Length - 1; length > 0; length--)
        {
            var candidate = trimmed[..length] + ellipsis;
            if (ctx.TextExtents(candidate).Width <= maxWidth)
            {
                return candidate;
            }
        }

        return ellipsis;
    }

    #endregion

    #region Вспомогательные методы координат

    /// <summary>Упаковывает пару координат чанка в один long для HashSet/Dictionary.</summary>
    /// <param name="chunkX">Индекс X.</param>
    /// <param name="chunkZ">Индекс Z.</param>
    /// <returns>Упакованный ключ.</returns>
    private static long Pack(int chunkX, int chunkZ)
    {
        return ((long)chunkX << 32) ^ (uint)chunkZ;
    }

    /// <summary>Извлекает координату X из упакованного ключа.</summary>
    /// <param name="packed">Упакованные координаты.</param>
    /// <returns>Индекс чанка X.</returns>
    private static int UnpackX(long packed)
    {
        return (int)(packed >> 32);
    }

    /// <summary>Извлекает координату Z из упакованного ключа.</summary>
    /// <param name="packed">Упакованные координаты.</param>
    /// <returns>Индекс чанка Z.</returns>
    private static int UnpackZ(long packed)
    {
        return (int)(packed & uint.MaxValue);
    }

    /// <summary>Целочисленное деление с округлением вниз (корректно для отрицательных координат).</summary>
    /// <param name="value">Делимое.</param>
    /// <param name="divisor">Делитель.</param>
    /// <returns>Результат floor(value / divisor).</returns>
    private static int FloorDiv(int value, int divisor)
    {
        return (int)Math.Floor((double)value / divisor);
    }

    #endregion
}
