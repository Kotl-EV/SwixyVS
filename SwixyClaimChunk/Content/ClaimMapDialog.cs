// =============================================================================
// ClaimMapDialog.cs
// -----------------------------------------------------------------------------
// Главное GUI-окно мода приватов: две вкладки — карта чанков и настройки приватов.
// Клиент отправляет пакеты через IClientNetworkChannel; ответы применяются через
// ApplyState / ApplyClaimList / ApplyClaimShow без полной пересборки, где возможно.
// Скролл списков сохраняется при обновлении; тяжёлые обновления откладываются через
// RunClaimsUiDeferred (один кадр), чтобы не сбрасывать позицию прокрутки.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using SwixyClaimChunk.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

using SwixyClaimChunk.Core;
// ClaimFlagBits lives in root SwixyClaimChunk (Types.cs).

namespace SwixyClaimChunk.Content;

/// <summary>
/// Диалог карты приватов и редактора списка приватов/участников.
/// </summary>
public sealed class ClaimMapDialog : GuiDialog
{
    #region Константы и поля состояния

    /// <summary>Радиус окна карты по умолчанию (в чанках от центра).</summary>
    private const int DefaultRadius = 10;

    /// <summary>Индекс вкладки «Карта чанков».</summary>
    private const int PageMap = 0;

    /// <summary>Индекс вкладки «Мои приваты».</summary>
    private const int PageClaims = 1;

    // =============================================================================
    // Layout from "Claim Chunk _ Map (3).svg" viewBox 1920×1080 + Group 462 frame.
    // Frame origin (475, 189), dialog 973×706. Local = SVG − origin.
    //
    // Tabs:    Map(70,0,302×74) Claims(464,0,302×74) Actions(822,0,115×74) = pin + close
    // Panel:   (42,118) 885×542 #513A28
    // Faces (inner #412D1D / #4D3624) vs full PNG textures (outer chrome = face − 6):
    //   Limits face (68,196) 306×100 → tex (62,190) 318×112  Group 466
    //   Legend title (68,316) 306×32 + body (68,357) 267×108 + swatches @x=344
    //     → tex (62,310) 318×161  Group 387
    //   Center face (68,604) 306×30 → tex (62,598) 318×42  Group 464
    // Map:     (458,192) 447×446 + 2px #121212
    // =============================================================================
    private const int UiW = 973;
    private const int UiH = 706;

    private const int TabY = 0;
    private const int TabH = 74;
    private const int TabMapX = 70;
    private const int TabMapW = 302;
    private const int TabClaimsX = 464;
    private const int TabClaimsW = 302;
    // Right action plate from SVG (2): 1297.5,189 w=115 → REL 822,115 — pin (☰) + close (✕)
    private const int TabActionsX = 822;
    private const int TabActionsW = 115;
    private const int PinBtnX = 828;
    private const int PinBtnW = 48;
    private const int CloseBtnX = 882;
    private const int CloseBtnW = 48;

    private const int PanelX = 42;
    private const int PanelY = 118;
    private const int PanelW = 885;
    private const int PanelH = 542;

    /// <summary>Inner face X of left-column cards (face = texture + 6).</summary>
    private const int CardX = 68;
    private const int CardW = 306;
    private const int CardPadX = 12;
    private const int CardPadY = 10;

    // --- Section headers Group 471.svg ---
    // Titles: CLAIMS M176.821,165.288 / SETTINGS M621.817,165.288 fill #836750
    // Cap height of outlined glyphs ≈ 17–18 → Montserrat Bold ~22 design-px.
    // Dashes: y=174, left 64→382, right 458→909, stroke #836650 @0.16 width 4 dash 8 8.
    private const double SectionTitleBaselineY = 165.288;
    private const double FontSection = 22;
    private const double SectionDashY = 174;
    private const double SectionDashLeftX1 = 64;
    private const double SectionDashLeftX2 = 382;
    private const double SectionDashRightX1 = 458;
    private const double SectionDashRightX2 = 909;
    /// <summary>Left edge of CLAIMS title path (Group 471).</summary>
    private const double SectionClaimsTextX = 176.821;
    /// <summary>Left edge of SETTINGS title path (Group 471).</summary>
    private const double SectionSettingsTextX = 621.817;
    private const double SectionDashWidth = 4;
    private const double SectionDashOn = 8;
    private const double SectionDashOff = 8;
    private static readonly double[] ColSection = [0x83 / 255.0, 0x67 / 255.0, 0x50 / 255.0, 1.0]; // #836750
    /// <summary>
    /// SVG: stroke #836650 @ opacity 0.16 over panel #513A28.
    /// Baked opaque so VS layer compositing matches the SVG blend.
    /// </summary>
    private static readonly double[] ColSectionDash =
    [
        (0x83 * 0.16 + 0x51 * 0.84) / 255.0,
        (0x66 * 0.16 + 0x3A * 0.84) / 255.0,
        (0x50 * 0.16 + 0x28 * 0.84) / 255.0,
        1.0
    ];

    // --- Limits: Group 466.svg/png 318×112 @ REL(62,190) ---
    // Single left margin for all labels (SVG path X varies by glyph; UI must left-align).
    // Face starts at tex x=6; content pad ≈12 → left ink edge at 18.
    private const int LimitsX = 62;
    private const int LimitsY = 190;
    private const int LimitsTexW = 318;
    private const int LimitsTexH = 112;
    private const double LimitsTextLeftX = 18;
    private const double LimitsTitleBaselineY = 28;
    private const double LimitsValueRightX = 299.555;
    private const double LimitsLine1BaselineY = 66.192;
    private const double LimitsLine2BaselineY = 90.192;
    private const double FontLimitsTitle = 20;
    private const double FontLimitsBody = 17;

    // --- Legend: Group 387.svg/png 318×161 @ REL(62,310) ---
    // Same left margin as Limits so both plates share one column edge.
    private const int LegendX = 62;
    private const int LegendY = 310;
    private const int LegendTexW = 318;
    private const int LegendTexH = 161;
    private const double LegendTextLeftX = 18;
    private const double LegendTitleBaselineY = 28;
    private const double LegendLine1BaselineY = 68;
    private const double LegendLine2BaselineY = 107;
    private const double LegendLine3BaselineY = 146.192;
    private const double LegendTextMaxRightX = 300;
    private const double FontLegendTitle = 20;
    private const double FontLegendBody = 17;

    // Status between legend bottom (471) and center top (598).
    private const int MessageY = 490;

    // --- Center: Group 464.png 318×42 @ REL(62,598); face (68,604) 306×30 ---
    private const int CenterX = 62;
    private const int CenterY = 598;
    private const int CenterTexW = 318;
    private const int CenterTexH = 42;

    private const int MapX = 458;
    private const int MapY = 192;
    private const int MapW = 447;
    private const int MapH = 446;
    private const int MapBorder = 2;

    // --- Claims tab layout from Group 471.svg (dialog = viewBox 973×706, origin 0,0) ---
    // Claim rows Group 378: face name (70,198) 251×69 → full tex 302×81 @ (64,192). Step 89 (81+8).
    private const int ClaimsListX = 64;
    private const int ClaimsListY = 192;
    private const int ClaimsListW = 302;
    private const int ClaimsListH = 445;
    private const int ClaimsCardH = 81;
    /// <summary>Gap between claim panels AND gap from panel right edge to scrollbar.</summary>
    private const int ClaimsCardGap = 8;
    private const int ClaimsIconColW = 42;
    // Scroll 8px right of list: 64+302+8 = 374.
    private const int ClaimsScrollX = ClaimsListX + ClaimsListW + ClaimsCardGap;
    private const int ClaimsScrollY = ClaimsListY;
    private const int ClaimsScrollW = 6;
    private const int ClaimsScrollH = 450;
    private const double ClaimsScrollThumbMinH = 36;
    private static readonly double[] ColScrollTrack = [69 / 255.0, 50 / 255.0, 36 / 255.0];
    private static readonly double[] ColScrollThumb = [0x7E / 255.0, 0x5D / 255.0, 0x43 / 255.0];

    // --- Settings plate Group 468: face (464,198) 439×164, full tex 451×176 @ (458,192) ---
    private const int SettingsX = 464;
    private const int SettingsY = 198;
    private const int SettingsW = 439;
    private const int SettingsH = 164;
    private const int SettingsTexX = 458;
    private const int SettingsTexY = 192;
    private const int SettingsTexW = 451;
    private const int SettingsTexH = 176;
    // Fields absolute in Group 471: inputs (476,266) & (691,266) 200×30; btns (476,311) & (691,311) 200×42.
    private const int SettingsFieldW = 200;
    private const int SettingsInputH = 30;
    private const int SettingsBtnH = 42; // Group 469.png
    private const int SettingsLeftFieldX = 476;
    private const int SettingsRightFieldX = 691;
    private const int SettingsInputY = 266;
    private const int SettingsBtnY = 311;
    // Text baselines absolute Group 471 → relative to tex (458,192):
    // stats (475.856,220) → (17.856,28); labels y=258 → 66; btn text y=335 → 143.
    private const double SettingsStatsBaselineY = 28;
    private const double SettingsLabelBaselineY = 66;
    private const double SettingsBtnTextBaselineY = 143;
    private const double SettingsStatsTextX = 17.856;
    private const double SettingsLabelLeftX = 19.328;   // 477.328 − 458
    private const double SettingsLabelRightX = 234.328; // 692.328 − 458
    private const double SettingsBtnLeftCenterX = 118;  // mid 476..676 − 458
    private const double SettingsBtnRightCenterX = 333; // mid 691..891 − 458
    private const double FontSettingsStats = 16;
    private const double FontSettingsLabel = 16;
    private const double FontSettingsBtn = 16;
    /// <summary>SVG stats fill #836650 @ 0.64.</summary>
    private static readonly double[] ColSettingsStats = [0x83 / 255.0, 0x66 / 255.0, 0x50 / 255.0, 0.64];
    /// <summary>SVG button + member name labels #9F795B.</summary>
    private static readonly double[] ColSettingsBtn = [0x9F / 255.0, 0x79 / 255.0, 0x5B / 255.0, 1.0];

    // Members list Group 470 / 469(1): full row 435×58 @ (458,380); face name (464,386) 203×46.
    // Row faces y=386,450,514,578 → step 64 → gap 6 between 58px rows.
    private const int MembersX = 458;
    private const int MembersY = 380;
    private const int MembersW = 435;
    private const int MembersH = 258; // fits ~4 rows (58+6)*3+58
    private const int MembersRowH = 58;
    private const int MembersRowGap = 6;
    private const int MembersNameW = 203;
    private const int MembersBtnSize = 46;
    private const int MembersBtnGap = 9;
    private const int MembersScrollX = 901; // Group 471 vertical track ~902
    private const int MembersScrollY = MembersY;
    private const int MembersScrollW = ClaimsScrollW;
    private const int MembersScrollH = MembersH;

    // Right panel modes: settings/members vs use-filter catalog (in-panel, not modal).
    private const int ClaimsRightSettings = 0;
    private const int ClaimsRightUseFilter = 1;

    // Gear next to SETTINGS title (Group 471 section band).
    private const int SettingsGearSize = 26;
    private const int SettingsGearX = (int)SectionDashRightX2 - SettingsGearSize;
    private const int SettingsGearY = 140;

    /// <summary>Tab plate labels (Group 471 tab faces). Outlined SVG ~22px → design ~28–30 fills well.</summary>
    private const double FontTab = 28;
    private const double FontTitle = 16;
    private const double FontBody = 14;
    private const double FontCenter = 18;

    private static readonly double[] ColInset = [0.255, 0.176, 0.114];   // #412D1D
    private static readonly double[] ColHi = [0.337, 0.243, 0.169];      // #563E2B
    private static readonly double[] ColLo = [0.165, 0.118, 0.078];      // #2A1E14
    private static readonly double[] ColCenter = [0.302, 0.212, 0.141];  // #4D3624
    private static readonly double[] ColEdge = [0.071, 0.071, 0.071];    // #121212
    private static readonly double[] ColPanel = [0.318, 0.227, 0.157];   // #513A28
    private static readonly double[] ColTabActive = [1.0, 1.0, 1.0, 1.0];
    private static readonly double[] ColTabInactive = [0.624, 0.475, 0.357, 1.0]; // #9F795B from SVG
    private static readonly double[] ColIcon = [0.624, 0.475, 0.357, 1.0];      // #9F795B icons
    private static readonly double[] ColIconPinned = [1.0, 1.0, 1.0, 1.0];     // white when pinned
    private static readonly double[] ColLimitsTitle = [0.514, 0.400, 0.314, 1.0]; // #836650 title in Group 466

    /// <summary>Клиентский API Vintage Story.</summary>
    private readonly ICoreClientAPI clientApi;

    /// <summary>Сетевой канал SwixyClaimChunk для пакетов карты и приватов.</summary>
    private readonly IClientNetworkChannel channel;

    /// <summary>Фон-фрейм Group 462 (textures/gui/dialog_frame.png).</summary>
    private ImageSurface? frameSurface;

    /// <summary>Кнопка «К игроку» — Group 464.png (textures/gui/button_center.png).</summary>
    private ImageSurface? centerButtonSurface;

    /// <summary>Плашка легенды — Group 387 (1).png (textures/gui/panel_legend.png).</summary>
    private ImageSurface? legendPanelSurface;

    /// <summary>Плашка лимитов — Group 466.png (textures/gui/panel_limits.png).</summary>
    private ImageSurface? limitsPanelSurface;

    /// <summary>Плашка настроек — Group 468.png (textures/gui/panel_settings.png).</summary>
    private ImageSurface? settingsPanelSurface;

    /// <summary>Кнопки rename/add — Group 469.png (textures/gui/btn_settings.png).</summary>
    private ImageSurface? settingsButtonSurface;

    /// <summary>Трек скролла списка приватов — Rectangle 758.png (scrollbar_track.png).</summary>
    private ImageSurface? scrollTrackSurface;

    /// <summary>Сетка чанков на вкладке карты; null до первой компоновки.</summary>
    private ClaimMapGridElement? gridElement;

    /// <summary>Последний снимок карты с сервера.</summary>
    private ClaimMapStatePacket? mapState;

    /// <summary>Локальный статус (Working… / send error); null — брать mapState.Message.</summary>
    private string? mapStatusOverride;

    /// <summary>
    /// Как в ванильном title bar: Fixed (закреплено) / Movable (откреплено, можно таскать за верх).
    /// </summary>
    private bool isMovable;

    /// <summary>Смещение от центра экрана (ElementBounds.fixedOffset*), в design px.</summary>
    private double dialogOffsetX;
    private double dialogOffsetY;

    private bool dragArmed;
    private bool isDragging;
    private double dragStartMouseX;
    private double dragStartMouseY;
    private double dragStartOffsetX;
    private double dragStartOffsetY;
    private const double DragThresholdPx = 5;

    /// <summary>Кэш списка приватов игрока и сообщений UI.</summary>
    private ClaimListStatePacket? claimListState;

    /// <summary>Центр видимого окна карты по X (координата чанка).</summary>
    private int centerChunkX;

    /// <summary>Центр видимого окна карты по Z (координата чанка).</summary>
    private int centerChunkZ;

    /// <summary>Радиус окна карты в чанках (ограничивается сервером).</summary>
    private int radius = DefaultRadius;

    /// <summary>Активная вкладка: PageMap или PageClaims.</summary>
    private int activePage = PageMap;

    /// <summary>ClaimId выбранного привата на вкладке настроек (0 — нет выбора).</summary>
    private int selectedClaimId;

    /// <summary>Имя выбранного привата — запасной ключ, если ClaimId сменился после TouchClaim.</summary>
    private string selectedClaimName = "";

    /// <summary>Приват, подсвеченный в мире; 0 — подсветка выключена.</summary>
    private int highlightedClaimId;

    /// <summary>Ожидаемое состояние подсветки после клика (-1 — нет ожидания).</summary>
    private int pendingHighlightClaimId = -1;

    /// <summary>UID выбранного участника (для поля ввода и удаления).</summary>
    private string selectedMemberUid = "";

    /// <summary>Отображаемое имя выбранного участника.</summary>
    private string selectedMemberName = "";

    /// <summary>Текст в поле «добавить игрока».</summary>
    private string memberNameInput = "";

    /// <summary>Текст в поле переименования привата.</summary>
    private string claimNameInput = "";

    /// <summary>Сохранённая позиция скролла списка приватов (пиксели).</summary>
    private float claimListScrollValue;

    /// <summary>Drag кастомного тонкого скроллбара списка приватов.</summary>
    private bool claimScrollDragging;

    /// <summary>Y мыши при старте drag (screen) минус thumb top (screen).</summary>
    private double claimScrollGrabOffsetY;

    /// <summary>Сохранённая позиция скролла списка участников.</summary>
    private float memberListScrollValue;

    /// <summary>Drag кастомного скроллбара списка участников (настройки).</summary>
    private bool memberScrollDragging;

    private double memberScrollGrabOffsetY;

    /// <summary>Drag кастомного скроллбара use-filter плиток.</summary>
    private bool useFilterScrollDragging;

    private double useFilterScrollGrabOffsetY;

    /// <summary>Границы таблицы списка приватов (для скролла).</summary>
    private ElementBounds? claimListTableBounds;

    /// <summary>Границы клип-области списка приватов.</summary>
    private ElementBounds? claimListClipBounds;

    /// <summary>Границы таблицы списка участников.</summary>
    private ElementBounds? memberListTableBounds;

    /// <summary>Границы клип-области списка участников.</summary>
    private ElementBounds? memberListClipBounds;

    /// <summary>Флаг: отложенное обновление UI уже запланировано на следующий тик.</summary>
    private bool claimsUiDeferScheduled;

    /// <summary>Действие для отложенного обновления UI (иконки, выбор без ComposeDialog).</summary>
    private Action? deferredClaimsUiAction;

    /// <summary>Правая колонка: настройки/участники или каталог Use-блоков.</summary>
    private int claimsRightMode = ClaimsRightSettings;

    private int useFilterDraftMode = ClaimUseFilterMode.AllowAll;
    private readonly HashSet<string> useFilterDraftCodes = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Блоки, найденные сканом привата (не весь creative).</summary>
    private List<(string Code, string Label, DummySlot Slot)> useFilterCatalog = [];
    /// <summary>Visible tiles: selected first, then the rest (search-filtered).</summary>
    private List<(string Code, string Label, DummySlot Slot)> useFilterEntries = [];
    private string useFilterSearch = "";
    private string useFilterEntriesFilterKey = "\0";
    private ClaimUseFilterTileGridElement? useFilterGrid;
    private ElementBounds? useFilterViewportBounds;
    private float useFilterScroll;
    /// <summary>Идёт фоновый скан areas привата на клиенте.</summary>
    private bool useFilterScanning;
    /// <summary>ClaimId текущего клиентского скана каталога Use.</summary>
    private int useFilterScanClaimId;
    /// <summary>Клиентский скан привата (без сервера) — только для UI выбора блоков.</summary>
    private ClaimUseFilterClientScanner? useFilterClientScanner;

    #endregion

    /// <summary>Код горячей клавиши P для открытия карты приватов.</summary>
    public override string ToggleKeyCombinationCode => ClaimConstants.OpenMapHotkeyCode;

    /// <summary>Не захватывать мышь глобально — удобнее кликать по карте.</summary>
    public override bool PrefersUngrabbedMouse => true;

    #region Конструктор и жизненный цикл

    /// <summary>Создаёт диалог и центрирует карту на позиции игрока.</summary>
    public ClaimMapDialog(ICoreClientAPI capi, IClientNetworkChannel channel)
        : base(capi)
    {
        clientApi = capi;
        this.channel = channel;
        CenterOnPlayer();
        ComposeDialog();
    }

    #endregion

    #region Запросы к серверу и применение пакетов

    /// <summary>Запрашивает обновление карты чанков; при useMapView учитывает текущий viewport сетки.</summary>
    public void RequestRefresh(bool useMapView = false)
    {
        var requestCenterX = centerChunkX;
        var requestCenterZ = centerChunkZ;
        var requestRadius = radius;

        if (useMapView)
        {
            var request = gridElement?.GetVisibleRequest(centerChunkX, centerChunkZ, radius) ?? (centerChunkX, centerChunkZ, radius);
            requestCenterX = request.CenterChunkX;
            requestCenterZ = request.CenterChunkZ;
            requestRadius = request.Radius;
            centerChunkX = requestCenterX;
            centerChunkZ = requestCenterZ;
            radius = requestRadius;
        }

        channel.SendPacket(new ClaimMapRequestPacket
        {
            CenterChunkX = requestCenterX,
            CenterChunkZ = requestCenterZ,
            Radius = Math.Clamp(requestRadius, 1, ClaimConstants.MaxRadius)
        });
    }

    /// <summary>Применяет снимок карты с сервера к сетке и подписям квот.</summary>
    public void ApplyState(ClaimMapStatePacket packet)
    {
        mapState = packet;
        mapStatusOverride = null;
        centerChunkX = packet.CenterChunkX;
        centerChunkZ = packet.CenterChunkZ;
        radius = packet.Radius;

        gridElement?.SetState(packet);
        UpdateText(packet);
    }

    /// <summary>
    /// Обновляет кэш списка приватов. Сохраняет скролл; по возможности обновляет вкладку без полной пересборки.
    /// </summary>
    public void ApplyClaimList(ClaimListStatePacket packet)
    {
        var savedClaimScroll = GetClaimListScrollOffset();
        var savedMemberScroll = GetMemberListScrollOffset();

        claimListState = packet;

        if (packet.Claims.Count == 0)
        {
            selectedClaimId = 0;
            selectedClaimName = "";
            selectedMemberUid = "";
            selectedMemberName = "";
        }
        else if (selectedClaimId == 0 || packet.Claims.All(claim => claim.ClaimId != selectedClaimId))
        {
            // После TouchClaim индекс ClaimId может смениться — ищем по имени.
            var byName = !string.IsNullOrWhiteSpace(selectedClaimName)
                ? packet.Claims.FirstOrDefault(claim =>
                    string.Equals(claim.Name, selectedClaimName, StringComparison.OrdinalIgnoreCase))
                : null;
            SelectClaim(byName ?? packet.Claims[0]);
        }

        var selectedClaim = GetSelectedClaim();
        if (selectedClaim == null || selectedClaim.Members.All(member => member.PlayerUid != selectedMemberUid))
        {
            selectedMemberUid = "";
            selectedMemberName = "";
        }

        if (highlightedClaimId > 0 && packet.Claims.All(claim => claim.ClaimId != highlightedClaimId))
        {
            highlightedClaimId = 0;
        }

        if (activePage == PageClaims)
        {
            if (packet.Claims.Count == 0 || !TryRefreshClaimsPageInPlace(savedClaimScroll, savedMemberScroll))
            {
                ComposeDialog();
                RestoreClaimListScroll(savedClaimScroll);
                RestoreMemberListScroll(savedMemberScroll);
                ApplyClaimsPageInputState();
            }

            SingleComposer?.GetDynamicText("claimsMessage")?.SetNewText(packet.Message ?? "");
        }
    }

    /// <summary>Синхронизирует состояние подсветки привата в мире с ответом сервера.</summary>
    public void ApplyClaimShow(ClaimShowStatePacket packet)
    {
        ApplyHighlightStateFromServer(packet.Active, packet.ClaimId);

        if (activePage == PageClaims)
        {
            RunClaimsUiDeferred(() => RefreshClaimHighlightIcons(GetClaimListScrollOffset()));
        }
    }

    /// <summary>При открытии — центр на игроке, запрос карты и списка приватов.</summary>
    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        CenterOnPlayer();
        gridElement?.CenterMapOnPlayer();
        RequestRefresh();
        RequestClaimList();

        if (highlightedClaimId > 0 && activePage == PageClaims)
        {
            RunClaimsUiDeferred(() => RefreshClaimHighlightIcons(GetClaimListScrollOffset()));
        }
    }

    /// <summary>Escape: из use-filter → настройки; иначе закрыть окно.</summary>
    public override bool OnEscapePressed()
    {
        if (activePage == PageClaims && claimsRightMode == ClaimsRightUseFilter)
        {
            CloseUseFilterPanel();
            return true;
        }

        TryClose();
        return true;
    }

    public override void OnGuiClosed()
    {
        claimsRightMode = ClaimsRightSettings;
        DisposeFrameSurface();
        base.OnGuiClosed();
    }

    #endregion

    #region Компоновка GUI

    /// <summary>Полная пересборка диалога (обе вкладки) и восстановление скролла/полей ввода.</summary>
    private void ComposeDialog()
    {
        ClaimFontHelper.EnsureRegistered(clientApi);
        EnsureFrameSurface();

        // Centered frame + optional drag offset (vanilla Movable uses offset from center).
        var dialogBounds = ElementBounds.Fixed(0, 0, UiW, UiH)
            .WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(dialogOffsetX, dialogOffsetY);
        var bgBounds = ElementBounds.Fill;

        gridElement?.Dispose();
        gridElement = null;
        if (activePage == PageMap)
        {
            gridElement = new ClaimMapGridElement(
                clientApi,
                ElementBounds.Fixed(MapX, MapY, MapW, MapH),
                OnChunksSelected,
                () => RequestRefresh(useMapView: true));
        }

        ClearComposers();
        // Invisible hit targets — labels drawn in DrawDialogChrome with Montserrat.
        var hitFont = ClaimFontHelper.Create(1, [0, 0, 0, 0], bold: true);
        var composer = clientApi.Gui
            .CreateCompo("epclaimmap", dialogBounds)
            .AddDynamicCustomDraw(bgBounds.FlatCopy().WithFixedPadding(0), DrawDialogChrome, "dialogChrome")
            .BeginChildElements(bgBounds)
            .AddButton(
                " ",
                SwitchToMapPage,
                ElementBounds.Fixed(TabMapX, TabY, TabMapW, TabH),
                hitFont,
                EnumButtonStyle.None,
                "mapTab")
            .AddButton(
                " ",
                SwitchToClaimsPage,
                ElementBounds.Fixed(TabClaimsX, TabY, TabClaimsW, TabH),
                hitFont,
                EnumButtonStyle.None,
                "claimsTab")
            .AddButton(
                " ",
                TogglePinButton,
                ElementBounds.Fixed(PinBtnX, TabY, PinBtnW, TabH),
                hitFont,
                EnumButtonStyle.None,
                "pinBtn")
            .AddButton(
                " ",
                CloseButton,
                ElementBounds.Fixed(CloseBtnX, TabY, CloseBtnW, TabH),
                hitFont,
                EnumButtonStyle.None,
                "closeBtn");

        if (activePage == PageMap)
        {
            ComposeMapPage(composer);
        }
        else
        {
            ComposeClaimsPage(composer);
        }

        SingleComposer = composer.EndChildElements().Compose();
        if (activePage == PageClaims)
        {
            ConfigureClaimListSpacing();
            ConfigureMemberListSpacing();
        }

        ApplyClaimsPageScrollState();
        ApplyClaimsPageInputState();
        UpdateText(null);
    }

    /// <summary>
    /// Вкладка Map: только то, что в SVG — left cards + interactive map 447×446.
    /// </summary>
    private void ComposeMapPage(GuiComposer composer)
    {
        // One Cairo layer for the whole map page content (cards + map chrome + labels).
        composer.AddDynamicCustomDraw(
            ElementBounds.Fixed(PanelX, PanelY, PanelW, PanelH),
            DrawMapPageContent,
            "mapPageContent");

        // Interactive world map — exact SVG map rect (on top of chrome).
        if (gridElement != null)
        {
            composer.AddInteractiveElement(gridElement, "chunkGrid");
        }

        // Center hit area matches Group 464 texture bounds (label drawn in DrawMapPageContent).
        composer.AddButton(
            " ",
            CenterButton,
            ElementBounds.Fixed(CenterX, CenterY, CenterTexW, CenterTexH),
            ClaimFontHelper.Create(1, [0, 0, 0, 0]),
            EnumButtonStyle.None,
            "centerButton");
    }

    /// <summary>
    /// Вкладка приватов — layout "Claim Chunk _ claims.svg":
    /// CLAIMS list left, SETTINGS + members right.
    /// </summary>
    private void ComposeClaimsPage(GuiComposer composer)
    {
        var bodyFont = ClaimFontHelper.Body();
        var labelFont = ClaimFontHelper.Create(13, ClaimFontHelper.ColorAccent, bold: true);
        var actionButtonFont = ClaimFontHelper.Create(14, ClaimFontHelper.ColorCream, bold: true);
        var inputFont = ClaimFontHelper.Create(14, ClaimFontHelper.ColorCream, bold: true);

        // Section headers + settings plate chrome (Cairo).
        composer.AddDynamicCustomDraw(
            ElementBounds.Fixed(PanelX, PanelY, PanelW, PanelH),
            DrawClaimsPageChrome,
            "claimsPageChrome");

        // ----- Left: claim list + custom thin scroll (Rectangle 758, no VS chrome) -----
        // Parent bounds MUST be in the composer tree before BeginClip (else renderX NRE).
        // Spacing: FixedHeight=81 panel + CellList.unscaledCellSpacing=8 (see ConfigureClaimListSpacing).
        var claimListBounds = ElementBounds.Fixed(ClaimsListX, ClaimsListY, ClaimsListW, ClaimsListH);
        claimListClipBounds = claimListBounds.ForkContainingChild(0, 0, 0, 0);
        claimListTableBounds = claimListClipBounds.ForkContainingChild(0, 0, 0, 0);
        // Invisible hit target for track/thumb (drawn in DrawClaimsPageChrome).
        var claimScrollBounds = ElementBounds.Fixed(ClaimsScrollX, ClaimsScrollY, ClaimsScrollW, ClaimsScrollH);

        composer
            .AddDynamicCustomDraw(claimListBounds, DrawTransparentBounds, "claimListArea")
            .AddDynamicCustomDraw(claimScrollBounds, DrawTransparentBounds, "claimScrollHit")
            .BeginClip(claimListClipBounds)
            .AddCellList(claimListTableBounds, CreateClaimListCell, BuildClaimCells(), "claimList")
            .EndClip();

        var claims = claimListState?.Claims ?? [];
        if (claims.Count == 0)
        {
            composer.AddStaticText(
                Lang.Get("swixyclaimchunk:claims-empty").ToUpperInvariant(),
                bodyFont,
                ElementBounds.Fixed(ClaimsListX + 8, ClaimsListY + 12, ClaimsListW - 16, 40),
                "claimsEmpty");
        }

        // Gear next to SETTINGS title — opens use-filter in the right panel.
        var hitFont = ClaimFontHelper.Create(1, [0, 0, 0, 0], bold: true);
        composer.AddButton(
            " ",
            ToggleUseFilterPanel,
            ElementBounds.Fixed(SettingsGearX, SettingsGearY, SettingsGearSize, SettingsGearSize),
            hitFont,
            EnumButtonStyle.None,
            "settingsGearBtn");

        var selectedClaim = GetSelectedClaim();
        if (selectedClaim == null)
        {
            claimsRightMode = ClaimsRightSettings;
            composer.AddStaticText(
                Lang.Get("swixyclaimchunk:claims-select").ToUpperInvariant(),
                bodyFont,
                ElementBounds.Fixed(SettingsX + 16, SettingsY + 24, SettingsW - 32, 40),
                "claimSelectHint");
            return;
        }

        if (claimsRightMode == ClaimsRightUseFilter)
        {
            ComposeUseFilterPanel(composer, selectedClaim, labelFont, bodyFont, actionButtonFont, inputFont);
            return;
        }

        ComposeSettingsAndMembersPanel(composer, selectedClaim, labelFont, bodyFont, actionButtonFont, inputFont);
    }

    /// <summary>Правая колонка: rename / add member / members list.</summary>
    private void ComposeSettingsAndMembersPanel(
        GuiComposer composer,
        ClaimInfoPacket selectedClaim,
        CairoFont labelFont,
        CairoFont bodyFont,
        CairoFont actionButtonFont,
        CairoFont inputFont)
    {
        // Layout from Group 468.svg: two 200px columns, input @y+68, button @y+113 (Group 469).
        // Labels/stats/button text are drawn in DrawClaimsPageChrome (SVG baselines + colors).
        var leftColX = SettingsLeftFieldX;
        var rightColX = SettingsRightFieldX;
        const int fieldW = SettingsFieldW;
        const int inputH = SettingsInputH;
        const int btnH = SettingsBtnH;
        const int fieldInputY = SettingsInputY;
        const int fieldBtnY = SettingsBtnY;

        // Input text: cream (SVG placeholder is #D29F78@0.16; typed text must stay readable).
        var settingsInputFont = ClaimFontHelper.Create(15, ClaimFontHelper.ColorCream, bold: true);
        var hitFont = ClaimFontHelper.Create(1, [0, 0, 0, 0], bold: true);

        composer
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(leftColX, fieldInputY, fieldW, inputH),
                DrawTextInputBackground,
                "claimNameInputBg")
            .AddTextInput(
                ElementBounds.Fixed(leftColX + 8, fieldInputY + 5, fieldW - 16, inputH - 10),
                text => claimNameInput = text,
                settingsInputFont,
                "claimNameInput")
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(rightColX, fieldInputY, fieldW, inputH),
                DrawTextInputBackground,
                "memberNameInputBg")
            .AddTextInput(
                ElementBounds.Fixed(rightColX + 8, fieldInputY + 5, fieldW - 16, inputH - 10),
                text => memberNameInput = text,
                settingsInputFont,
                "memberNameInput")
            // Textured Group 469 hit targets — labels drawn centered in chrome.
            .AddButton(
                " ",
                RenameClaimButton,
                ElementBounds.Fixed(leftColX, fieldBtnY, fieldW, btnH),
                hitFont,
                EnumButtonStyle.None,
                "renameClaim")
            .AddButton(
                " ",
                AddMemberButton,
                ElementBounds.Fixed(rightColX, fieldBtnY, fieldW, btnH),
                hitFont,
                EnumButtonStyle.None,
                "addMember");

        var memberListBounds = ElementBounds.Fixed(MembersX, MembersY, MembersW, MembersH);
        memberListClipBounds = memberListBounds.ForkContainingChild(0, 0, 0, 0);
        memberListTableBounds = memberListClipBounds.ForkContainingChild(0, 0, 0, 0);
        var memberScrollBounds = ElementBounds.Fixed(MembersScrollX, MembersScrollY, MembersScrollW, MembersScrollH);

        composer
            .AddDynamicCustomDraw(memberListBounds, DrawTransparentBounds, "memberListArea")
            .AddDynamicCustomDraw(memberScrollBounds, DrawTransparentBounds, "memberScrollHit")
            .BeginClip(memberListClipBounds)
            .AddCellList(memberListTableBounds, CreateMemberCell, BuildMemberCells(selectedClaim), "memberList")
            .EndClip()
            .AddDynamicText(
                claimListState?.Message ?? "",
                bodyFont,
                // +30px right so "Use restriction saved" / status sits clearer under the members panel.
                ElementBounds.Fixed(MembersX + 30, MembersY + MembersH + 4, MembersW + ClaimsCardGap + MembersScrollW - 30, 24),
                "claimsMessage");
    }

    /// <summary>
    /// Правая колонка: одно окно-плитка (креатив). Выбранные плитки сверху списка.
    /// Поиск + прокрутка; укладывается в SettingsY → MembersY+MembersH.
    /// </summary>
    private void ComposeUseFilterPanel(
        GuiComposer composer,
        ClaimInfoPacket selectedClaim,
        CairoFont labelFont,
        CairoFont bodyFont,
        CairoFont actionButtonFont,
        CairoFont inputFont)
    {
        const int pad = 8;
        // Full right-column band up to the thin scroll strip.
        var x = SettingsX + pad;
        var contentW = MembersScrollX - x - ClaimsCardGap; // room for scrollbar
        if (contentW < 280)
        {
            contentW = MembersW - pad;
        }

        var scrollX = MembersScrollX;
        var y0 = SettingsY;
        // Footer sits slightly below the members band so status + buttons are lower.
        const int footerDrop = 14;
        var bottom = MembersY + MembersH + footerDrop;
        const int btnH = 30;
        const int footerH = btnH + 4;
        // Claim flags (one row with plates) + search + hint.
        const int flagsRowH = 34;
        const int flagsGap = 8;
        const int searchFieldH = 28;
        const int chromeH = 4 + flagsRowH + 6 + searchFieldH + 6 + 18;
        var gridY = y0 + chromeH;
        // Grid ends above footer with a small gap.
        var gridH = Math.Max(120, bottom - footerH - 8 - gridY);

        var cream = ClaimFontHelper.Create(13, ClaimFontHelper.ColorCream, bold: true);
        var creamSm = ClaimFontHelper.Create(12, ClaimFontHelper.ColorCream, bold: true);

        RefreshUseFilterEntryLists();

        useFilterViewportBounds = ElementBounds.Fixed(x, gridY, contentW, gridH);
        var scrollBounds = ElementBounds.Fixed(scrollX, gridY, MembersScrollW, gridH);

        useFilterGrid = new ClaimUseFilterTileGridElement(clientApi, useFilterViewportBounds)
        {
            OnTileClick = ToggleUseFilterCode,
            IsSelected = code =>
            {
                var n = NormalizeUseFilterCode(code);
                return useFilterDraftCodes.Any(c => ClaimCodeUtil.SameCatalogGroup(c, n));
            },
            EmptyHint = GetUseFilterEmptyHint(),
            OnScrollChanged = () =>
            {
                useFilterScroll = useFilterGrid?.ScrollOffset ?? 0f;
                SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();
            }
        };
        useFilterGrid.SetEntries(useFilterEntries);
        useFilterGrid.ScrollOffset = useFilterScroll;

        var flagsY = y0 + 2;
        var searchY = flagsY + flagsRowH + 6;
        var hintY = searchY + searchFieldH + 6;

        const int checkSize = 22;
        const int flagPad = 8;
        var halfW = (contentW - flagsGap) / 2;
        var pvpPlate = ElementBounds.Fixed(x, flagsY, halfW, flagsRowH);
        var animalsPlate = ElementBounds.Fixed(x + halfW + flagsGap, flagsY, halfW, flagsRowH);
        var searchBgBounds = ElementBounds.Fixed(x, searchY, contentW, searchFieldH);
        var searchInputBounds = ElementBounds.Fixed(x + 6, searchY + 3, contentW - 12, searchFieldH - 6);

        // Full-plate hit targets; checkbox is drawn on the chip (dark square when off).
        var hitFont = ClaimFontHelper.Create(1, [0, 0, 0, 0], bold: true);

        composer
            .AddDynamicCustomDraw(pvpPlate, DrawClaimFlagPvpChip, "claimFlagPvpBg")
            .AddDynamicCustomDraw(animalsPlate, DrawClaimFlagAnimalsChip, "claimFlagAnimalsBg")
            .AddButton(" ", ToggleClaimFlagPvpButton, pvpPlate.FlatCopy(), hitFont, EnumButtonStyle.None, "claimFlagPvpHit")
            .AddButton(" ", ToggleClaimFlagAnimalsButton, animalsPlate.FlatCopy(), hitFont, EnumButtonStyle.None, "claimFlagAnimalsHit")
            .AddDynamicText(
                Lang.Get("swixyclaimchunk:claim-flag-pvp"),
                cream,
                ElementBounds.Fixed(x + flagPad + checkSize + 8, flagsY + 7, halfW - flagPad * 2 - checkSize - 10, 22),
                "claimFlagPvpText")
            .AddDynamicText(
                Lang.Get("swixyclaimchunk:claim-flag-animals"),
                cream,
                ElementBounds.Fixed(
                    x + halfW + flagsGap + flagPad + checkSize + 8,
                    flagsY + 7,
                    halfW - flagPad * 2 - checkSize - 10,
                    22),
                "claimFlagAnimalsText")
            .AddDynamicCustomDraw(searchBgBounds, DrawTextInputBackground, "useFilterSearchBg")
            .AddTextInput(
                searchInputBounds,
                OnUseFilterSearchChanged,
                inputFont,
                "useFilterSearch")
            .AddDynamicText(
                Lang.Get("swixyclaimchunk:use-filter-list-hint"),
                creamSm,
                ElementBounds.Fixed(x, hintY, contentW, 18),
                "useFilterHint")
            .AddDynamicCustomDraw(scrollBounds, DrawTransparentBounds, "useFilterScrollHit")
            .AddInteractiveElement(useFilterGrid, "useFilterGrid")
            .AddDynamicText(
                BuildUseFilterSelectedText(),
                creamSm,
                ElementBounds.Fixed(x, bottom - footerH + 2, Math.Max(80, contentW - 220), 22),
                "useFilterSelectedText")
            .AddButton(
                Lang.Get("swixyclaimchunk:use-filter-cancel").ToUpperInvariant(),
                CloseUseFilterPanel,
                ElementBounds.Fixed(x + contentW - 210, bottom - footerH, 100, btnH),
                actionButtonFont,
                EnumButtonStyle.Small,
                "useFilterBack")
            .AddButton(
                Lang.Get("swixyclaimchunk:use-filter-save").ToUpperInvariant(),
                SaveUseFilterPanel,
                ElementBounds.Fixed(x + contentW - 100, bottom - footerH, 100, btnH),
                actionButtonFont,
                EnumButtonStyle.Small,
                "useFilterSave");

        // Montserrat + Small style often left-biases shorter labels ("ОТМЕНА") — force center.
        composer.GetButton("useFilterBack")?.SetOrientation(EnumTextOrientation.Center);
        composer.GetButton("useFilterSave")?.SetOrientation(EnumTextOrientation.Center);
    }

    /// <summary>Регистрирует ElementBounds в дереве GUI без отрисовки (для clip/scrollbar parents).</summary>
    private static void DrawTransparentBounds(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        // no-op: parent slot only
    }

    #endregion

    #region Карта — действия с чанками

    /// <summary>Отправляет пакет пакетного клейма/анклейма по выделенным чанкам сетки.</summary>
    private void OnChunksSelected(IReadOnlyList<(int ChunkX, int ChunkZ)> chunks)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        var request = gridElement?.GetVisibleRequest(centerChunkX, centerChunkZ, radius) ?? (centerChunkX, centerChunkZ, radius);
        centerChunkX = request.CenterChunkX;
        centerChunkZ = request.CenterChunkZ;
        radius = request.Radius;

        mapStatusOverride = Lang.Get("swixyclaimchunk:claim-map-working");
        RedrawMapSideColumn();

        try
        {
            // Cap client-side so we don't flood the server with huge selections.
            var limited = chunks.Count > ClaimConstants.MaxBatchChunks
                ? chunks.Take(ClaimConstants.MaxBatchChunks).ToList()
                : chunks;
            channel.SendPacket(new ClaimChunksBatchActionPacket
            {
                Chunks = limited.Select(chunk => new ClaimChunkCoordPacket
                {
                    ChunkX = chunk.ChunkX,
                    ChunkZ = chunk.ChunkZ
                }).ToList(),
                CenterChunkX = centerChunkX,
                CenterChunkZ = centerChunkZ,
                Radius = Math.Clamp(radius, 1, ClaimConstants.MaxRadius)
            });
        }
        catch (Exception exception)
        {
            clientApi.Logger.Error("Failed to send claim batch packet: {0}", exception);
            mapStatusOverride = Lang.Get("swixyclaimchunk:error-send-request-failed");
            RedrawMapSideColumn();
        }
    }

    #endregion

    #region Вкладки и запрос списка приватов

    /// <summary>Запрашивает у сервера актуальный список приватов игрока.</summary>
    private void RequestClaimList()
    {
        channel.SendPacket(new ClaimListRequestPacket());
    }

    /// <summary>Переключает на вкладку карты и пересобирает диалог.</summary>
    private bool SwitchToMapPage()
    {
        activePage = PageMap;
        ComposeDialog();
        if (mapState != null)
        {
            gridElement?.SetState(mapState);
        }

        return true;
    }

    /// <summary>Переключает на вкладку приватов, запрашивает список и пересобирает диалог.</summary>
    private bool SwitchToClaimsPage()
    {
        activePage = PageClaims;
        RequestClaimList();
        ComposeDialog();
        return true;
    }

    #endregion

    #region Выбор привата и участников

    /// <summary>Выбирает приват в списке; обновляет UI без полной пересборки, если возможно.</summary>
    private bool SelectClaimButton(ClaimInfoPacket claim)
    {
        if (selectedClaimId == claim.ClaimId && activePage == PageClaims)
        {
            return true;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        SelectClaim(claim);
        RunClaimsUiDeferred(() => RefreshClaimsSelectionUi(savedClaimScroll, 0));
        return true;
    }

    /// <summary>Открывает редактор привата (то же, что выбор в списке).</summary>
    private bool OpenClaimEditorButton(ClaimInfoPacket claim)
    {
        return SelectClaimButton(claim);
    }

    /// <summary>Устанавливает выбранный приват и сбрасывает выбор участника.</summary>
    private void SelectClaim(ClaimInfoPacket claim)
    {
        selectedClaimId = claim.ClaimId;
        selectedClaimName = claim.Name ?? "";
        selectedMemberUid = "";
        selectedMemberName = "";
        claimNameInput = claim.Name ?? "";
    }

    /// <summary>Выбирает участника; обновляет правую панель без полной пересборки.</summary>
    private bool SelectMemberButton(ClaimMemberPacket member)
    {
        if (selectedMemberUid == member.PlayerUid && activePage == PageClaims)
        {
            return true;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        var savedMemberScroll = GetMemberListScrollOffset();
        SelectMember(member);

        RunClaimsUiDeferred(() => RefreshMemberSelectionUi(savedClaimScroll, savedMemberScroll));
        return true;
    }

    /// <summary>Запоминает UID/ник участника; для не-владельца подставляет ник в поле добавления.</summary>
    private void SelectMember(ClaimMemberPacket member)
    {
        selectedMemberUid = member.PlayerUid;
        selectedMemberName = member.PlayerName;
        if (!member.IsOwner)
        {
            memberNameInput = member.PlayerName;
        }
    }

    #endregion

    #region Действия с приватами и участниками (сеть)

    /// <summary>Добавляет игрока из поля ввода с правами Use+Build.</summary>
    private bool AddMemberButton()
    {
        memberNameInput = SingleComposer?.GetTextInput("memberNameInput")?.GetText() ?? memberNameInput;
        SendClaimAction(
            ClaimAccessActionType.AddPlayer,
            memberNameInput,
            (int)(EnumBlockAccessFlags.Use | EnumBlockAccessFlags.BuildOrBreak),
            "");
        return true;
    }

    /// <summary>Удаляет участника по UID; владельца удалить нельзя.</summary>
    private void RemoveMemberByUid(string memberUid)
    {
        var member = FindMemberByUid(memberUid);
        if (member == null || member.IsOwner)
        {
            return;
        }

        SendClaimAction(ClaimAccessActionType.RemovePlayer, member.PlayerName, 0, "", member.PlayerUid);
    }

    /// <summary>Переключает статус со-владельца участника (корона).</summary>
    private void GrantCoOwnershipByUid(string memberUid)
    {
        var claim = GetSelectedClaim();
        var member = FindMemberByUid(memberUid);
        if (claim == null || member == null || member.IsOwner || claim.ViewerIsCoOwner)
        {
            return;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        var savedMemberScroll = GetMemberListScrollOffset();
        member.IsCoOwner = !member.IsCoOwner;
        member.AccessName = FormatMemberAccessName(member.AccessFlags, member.IsOwner, member.IsCoOwner);
        RefreshMemberAccessIcons(savedClaimScroll, savedMemberScroll);
        SendClaimAction(ClaimAccessActionType.GrantCoOwnership, member.PlayerName, 0, "", member.PlayerUid);
    }

    private bool RenameClaimButton()
    {
        claimNameInput = SingleComposer?.GetTextInput("claimNameInput")?.GetText() ?? claimNameInput;
        SendClaimAction(ClaimAccessActionType.RenameClaim, "", 0, claimNameInput);
        return true;
    }

    /// <summary>Шестерёнка у «НАСТРОЙКИ»: переключение in-panel use-filter.</summary>
    private bool ToggleUseFilterPanel()
    {
        if (GetSelectedClaim() == null)
        {
            return true;
        }

        if (claimsRightMode == ClaimsRightUseFilter)
        {
            return CloseUseFilterPanel();
        }

        return OpenUseFilterPanel();
    }

    private bool OpenUseFilterPanel()
    {
        var claim = GetSelectedClaim();
        if (claim == null)
        {
            return true;
        }

        // Mode is automatic: any selected codes → public Use whitelist.
        useFilterDraftMode = ClaimUseFilterMode.AllowAll;
        useFilterDraftCodes.Clear();
        // groupKey → display code (сливаем fence-oak + fence-birch из старых сейвов).
        var draftByGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in ClaimUseFilterCodesCodec.Split(claim.UseFilterCodesRaw))
        {
            // К стандартному виду (без ориентации из старых сейвов).
            var n = ClaimCodeUtil.ResolveStandardDisplayCode(clientApi.World, NormalizeUseFilterCode(code));
            if (string.IsNullOrWhiteSpace(n))
            {
                continue;
            }

            var gk = ClaimCodeUtil.GetCatalogGroupKey(n);
            if (string.IsNullOrWhiteSpace(gk))
            {
                gk = n;
            }

            if (!draftByGroup.ContainsKey(gk)
                || n.Length < draftByGroup[gk].Length)
            {
                draftByGroup[gk] = n;
            }
        }

        foreach (var n in draftByGroup.Values)
        {
            useFilterDraftCodes.Add(n);
        }

        if (useFilterDraftCodes.Count > 0)
        {
            useFilterDraftMode = ClaimUseFilterMode.Whitelist;
        }

        useFilterSearch = "";
        useFilterEntriesFilterKey = "\0";
        useFilterScroll = 0;
        // Сразу показываем уже включённые в Use (даже если дальше ±10).
        // Near-scan (±10) потом допишет остальные рядом.
        useFilterCatalog = BuildUseFilterCatalogFromCodes(useFilterDraftCodes, stacksByCode: null);
        useFilterEntries = [];
        useFilterScanning = true;
        useFilterScanClaimId = claim.ClaimId;

        RefreshUseFilterEntryLists();
        claimsRightMode = ClaimsRightUseFilter;
        ComposeDialog();
        SyncClaimFlagSwitches(claim);
        SingleComposer?.GetDynamicText("useFilterSelectedText")?.SetNewText(BuildUseFilterSelectedText());
        SingleComposer?.GetTextInput("useFilterSearch")?.SetValue(useFilterSearch, true);

        useFilterClientScanner ??= new ClaimUseFilterClientScanner(clientApi);
        useFilterClientScanner.Start(
            claim.ClaimId,
            OnClientUseFilterScanComplete,
            ClaimUseFilterClientScanner.DefaultRadius);
        return true;
    }

    private bool CloseUseFilterPanel()
    {
        useFilterClientScanner?.Cancel();
        useFilterScanning = false;
        claimsRightMode = ClaimsRightSettings;
        ComposeDialog();
        return true;
    }

    /// <summary>Клиент закончил скан рядом — обновляем плитки (стеки с attributes для фонарей).</summary>
    private void OnClientUseFilterScanComplete(
        int claimId,
        IReadOnlyList<string> codes,
        int scannedBlocks,
        IReadOnlyDictionary<string, ItemStack> stacksByCode)
    {
        if (!IsOpened() || claimsRightMode != ClaimsRightUseFilter)
        {
            return;
        }

        if (claimId != useFilterScanClaimId && claimId != selectedClaimId)
        {
            return;
        }

        useFilterScanning = false;

        // Nearby (±10) + always-include selected whitelist (any distance).
        useFilterCatalog = BuildUseFilterCatalogMerged(codes, stacksByCode);
        useFilterEntriesFilterKey = "\0";
        useFilterScroll = 0;
        RefreshUseFilterEntryLists();

        if (useFilterGrid != null)
        {
            useFilterGrid.EmptyHint = GetUseFilterEmptyHint();
            useFilterGrid.SetEntries(useFilterEntries);
            useFilterGrid.ScrollOffset = 0;
        }

        SingleComposer?.GetDynamicText("useFilterSelectedText")?.SetNewText(BuildUseFilterSelectedText());
        SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();

        clientApi.Logger.Notification(
            "[SwixyClaimChunk] Use-filter catalog (near scan): {0} codes (scanned={1}, selected={2})",
            useFilterCatalog.Count,
            scannedBlocks,
            useFilterDraftCodes.Count);
    }

    /// <summary>
    /// Nearby scan codes + draft whitelist. Selected Use blocks stay in the list
    /// even when farther than the scan radius.
    /// </summary>
    private List<(string Code, string Label, DummySlot Slot)> BuildUseFilterCatalogMerged(
        IEnumerable<string>? nearCodes,
        IReadOnlyDictionary<string, ItemStack>? stacksByCode)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (nearCodes != null)
        {
            foreach (var code in nearCodes)
            {
                var n = ClaimCodeUtil.ResolveStandardDisplayCode(clientApi.World, NormalizeUseFilterCode(code));
                if (!string.IsNullOrWhiteSpace(n))
                {
                    found.Add(n);
                }
            }
        }

        foreach (var code in useFilterDraftCodes)
        {
            var n = ClaimCodeUtil.ResolveStandardDisplayCode(clientApi.World, NormalizeUseFilterCode(code));
            if (string.IsNullOrWhiteSpace(n))
            {
                n = NormalizeUseFilterCode(code);
            }

            if (!string.IsNullOrWhiteSpace(n))
            {
                found.Add(n);
            }
        }

        return BuildUseFilterCatalogFromCodes(found, stacksByCode);
    }

    private bool SaveUseFilterPanel()
    {
        var claim = GetSelectedClaim();
        if (claim == null)
        {
            return true;
        }

        // Empty selection = no public blocks (AllowAll). No error — just clears public Use.
        var mode = useFilterDraftCodes.Count > 0
            ? ClaimUseFilterMode.Whitelist
            : ClaimUseFilterMode.AllowAll;
        // Один код на семью (first-part match на сервере покрывает все варианты).
        var codes = mode == ClaimUseFilterMode.Whitelist
            ? CollapseUseFilterCodesByGroup(
                    useFilterDraftCodes
                        .Select(c => ClaimCodeUtil.ResolveStandardDisplayCode(clientApi.World, NormalizeUseFilterCode(c)))
                        .Where(static c => !string.IsNullOrWhiteSpace(c)))
                .OrderBy(static c => c, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        channel.SendPacket(new ClaimAccessActionPacket
        {
            ClaimId = claim.ClaimId,
            Action = ClaimAccessActionType.SetUseFilter,
            UseFilterMode = mode,
            UseFilterCodesRaw = ClaimUseFilterCodesCodec.Join(codes)
        });

        claimsRightMode = ClaimsRightSettings;
        ComposeDialog();
        return true;
    }

    /// <summary>
    /// Каталог Use: usable-блоки из привата (клиентский скан)
    /// + уже выбранные в whitelist (даже если блока уже нет).
    /// Серверный scan-пакет тоже принимаем (совместимость), но UI больше его не запрашивает.
    /// </summary>
    public void ApplyUseFilterScanResult(ClaimUseFilterScanResultPacket packet)
    {
        if (claimsRightMode != ClaimsRightUseFilter)
        {
            return;
        }

        if (packet.ClaimId != useFilterScanClaimId
            && packet.ClaimId != selectedClaimId)
        {
            return;
        }

        useFilterScanning = false;

        useFilterCatalog = BuildUseFilterCatalogMerged(
            ClaimUseFilterCodesCodec.Split(packet.CodesRaw),
            stacksByCode: null);
        useFilterEntriesFilterKey = "\0";
        useFilterScroll = 0;
        RefreshUseFilterEntryLists();

        if (useFilterGrid != null)
        {
            useFilterGrid.EmptyHint = GetUseFilterEmptyHint();
            useFilterGrid.SetEntries(useFilterEntries);
            useFilterGrid.ScrollOffset = 0;
        }

        SingleComposer?.GetDynamicText("useFilterSelectedText")?.SetNewText(BuildUseFilterSelectedText());
        SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();

        clientApi.Logger.Notification(
            "[SwixyClaimChunk] Use-filter catalog (server scan packet): {0} codes (scanned={1})",
            useFilterCatalog.Count,
            packet.ScannedBlocks);
    }

    private string GetUseFilterEmptyHint()
    {
        if (useFilterScanning)
        {
            return Lang.Get("swixyclaimchunk:use-filter-scan-loading");
        }

        if (useFilterCatalog.Count == 0)
        {
            return Lang.Get("swixyclaimchunk:use-filter-scan-empty");
        }

        return Lang.Get("swixyclaimchunk:use-filter-catalog-empty");
    }

    private void OnUseFilterSearchChanged(string text)
    {
        useFilterSearch = text ?? "";
        useFilterEntriesFilterKey = "\0";
        useFilterScroll = 0;
        RefreshUseFilterEntryLists();
        if (useFilterGrid != null)
        {
            useFilterGrid.SetEntries(useFilterEntries);
            useFilterGrid.ScrollOffset = 0;
        }

        SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();
    }

    private string BuildUseFilterSelectedText()
    {
        if (useFilterScanning)
        {
            return Lang.Get("swixyclaimchunk:use-filter-scan-loading");
        }

        if (useFilterDraftCodes.Count == 0)
        {
            return Lang.Get("swixyclaimchunk:use-filter-selected-all");
        }

        return Lang.Get("swixyclaimchunk:use-filter-selected-count", useFilterDraftCodes.Count);
    }

    private void ToggleUseFilterCode(string code)
    {
        code = ClaimCodeUtil.ResolveStandardDisplayCode(clientApi.World, NormalizeUseFilterCode(code));
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        // Toggle by family: fence-oak selected covers fence-birch tile and vice versa.
        var wasSelected = useFilterDraftCodes.Any(c => ClaimCodeUtil.SameCatalogGroup(c, code));
        useFilterDraftCodes.RemoveWhere(c => ClaimCodeUtil.SameCatalogGroup(c, code));
        if (!wasSelected)
        {
            useFilterDraftCodes.Add(code);
            // Keep selected tile in catalog even if the block later leaves the ±10 radius.
            EnsureUseFilterCatalogHasCode(code);
        }

        useFilterDraftMode = useFilterDraftCodes.Count > 0
            ? ClaimUseFilterMode.Whitelist
            : ClaimUseFilterMode.AllowAll;

        SingleComposer?.GetDynamicText("useFilterSelectedText")?.SetNewText(BuildUseFilterSelectedText());

        // Reorder list: selected tiles bubble to the top; keep scroll near top of selection.
        useFilterEntriesFilterKey = "\0";
        RefreshUseFilterEntryLists();
        if (useFilterGrid != null)
        {
            useFilterGrid.SetEntries(useFilterEntries);
            useFilterGrid.ScrollOffset = Math.Min(useFilterScroll, useFilterGrid.MaxScroll);
            useFilterScroll = useFilterGrid.ScrollOffset;
        }

        SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();
    }

    /// <summary>Добавляет код в каталог, если его ещё нет (семья GetCatalogGroupKey).</summary>
    private void EnsureUseFilterCatalogHasCode(string code)
    {
        code = ClaimCodeUtil.ResolveStandardDisplayCode(clientApi.World, NormalizeUseFilterCode(code));
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var gk = ClaimCodeUtil.GetCatalogGroupKey(code);
        if (string.IsNullOrWhiteSpace(gk))
        {
            gk = code;
        }

        foreach (var entry in useFilterCatalog)
        {
            if (ClaimCodeUtil.SameCatalogGroup(entry.Code, code))
            {
                return;
            }
        }

        if (TryResolveUseFilterEntry(code, out var resolved))
        {
            useFilterCatalog.Add(resolved);
        }
        else
        {
            useFilterCatalog.Add((code, ClaimCodeUtil.GetFriendlyBlockLabel(code), new DummySlot(null)));
        }
    }

    /// <summary>
    /// Builds visible tiles: selected first, then other blocks found in this claim.
    /// Search filters both groups by label/code.
    /// </summary>
    private void RefreshUseFilterEntryLists()
    {
        var filter = (useFilterSearch ?? "").Trim();
        // Include selection set + catalog size in cache key so scan/toggle rebuilds order.
        var cacheKey = "C|" + filter + "|" + useFilterCatalog.Count + "|"
                       + useFilterDraftCodes.Count + "|" + string.Join(",", useFilterDraftCodes);
        if (string.Equals(cacheKey, useFilterEntriesFilterKey, StringComparison.Ordinal)
            && useFilterEntries.Count > 0)
        {
            return;
        }

        // Пока скан идёт и ещё нет даже выбранных — пустой список (EmptyHint = loading).
        // Если Use уже включён на блоках — показываем их сразу (даже дальше ±10).
        if (useFilterScanning && useFilterCatalog.Count == 0 && useFilterDraftCodes.Count == 0)
        {
            useFilterEntriesFilterKey = cacheKey;
            useFilterEntries = [];
            return;
        }

        useFilterEntriesFilterKey = cacheKey;
        var selected = new List<(string Code, string Label, DummySlot Slot)>(useFilterDraftCodes.Count);
        var rest = new List<(string Code, string Label, DummySlot Slot)>(Math.Max(16, useFilterCatalog.Count));
        // Match selection by catalog family (fence-oak ↔ fence-birch).
        var wantGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in useFilterDraftCodes)
        {
            var gk = ClaimCodeUtil.GetCatalogGroupKey(c);
            wantGroups.Add(string.IsNullOrWhiteSpace(gk) ? c : gk);
        }

        foreach (var entry in useFilterCatalog)
        {
            if (filter.Length > 0
                && entry.Label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                && entry.Code.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var eg = ClaimCodeUtil.GetCatalogGroupKey(entry.Code);
            if (string.IsNullOrWhiteSpace(eg))
            {
                eg = entry.Code;
            }

            if (wantGroups.Remove(eg))
            {
                selected.Add(entry);
            }
            else
            {
                rest.Add(entry);
            }
        }

        // Выбранные коды, которых нет в каталоге (старый whitelist / блок убрали).
        foreach (var code in useFilterDraftCodes)
        {
            var gk = ClaimCodeUtil.GetCatalogGroupKey(code);
            if (string.IsNullOrWhiteSpace(gk))
            {
                gk = code;
            }

            if (!wantGroups.Contains(gk))
            {
                continue; // already represented by a catalog tile
            }

            wantGroups.Remove(gk);
            if (TryResolveUseFilterEntry(code, out var entry))
            {
                if (filter.Length > 0
                    && entry.Label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && entry.Code.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                selected.Add(entry);
            }
            else
            {
                var label = ClaimCodeUtil.GetFriendlyBlockLabel(code);
                if (filter.Length > 0
                    && label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && code.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                selected.Add((code, label, new DummySlot(null)));
            }
        }

        selected.AddRange(rest);
        useFilterEntries = selected;
    }

    /// <summary>Один display-код на семью каталога (first-part / fruit / coal).</summary>
    private static List<string> CollapseUseFilterCodesByGroup(IEnumerable<string> codes)
    {
        var byGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in codes)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var gk = ClaimCodeUtil.GetCatalogGroupKey(raw);
            if (string.IsNullOrWhiteSpace(gk))
            {
                gk = raw;
            }

            if (!byGroup.TryGetValue(gk, out var existing)
                || PreferCatalogDisplayCode(raw, existing, stacksByCode: null))
            {
                byGroup[gk] = raw;
            }
        }

        return byGroup.Values.ToList();
    }

    /// <summary>
    /// Выбор представителя семьи для иконки: есть стек → короче → oak.
    /// </summary>
    private static bool PreferCatalogDisplayCode(
        string candidate,
        string existing,
        IReadOnlyDictionary<string, ItemStack>? stacksByCode)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.Equals(candidate, existing, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candHasStack = stacksByCode != null
                           && stacksByCode.TryGetValue(candidate, out var cs)
                           && cs?.Collectible != null;
        var existHasStack = stacksByCode != null
                            && stacksByCode.TryGetValue(existing, out var es)
                            && es?.Collectible != null;
        if (candHasStack && !existHasStack)
        {
            return true;
        }

        if (!candHasStack && existHasStack)
        {
            return false;
        }

        if (candidate.Length < existing.Length)
        {
            return true;
        }

        if (candidate.Contains("-oak", StringComparison.OrdinalIgnoreCase)
            && !existing.Contains("-oak", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>Собирает плитки только по кодам, найденным в привате (или в whitelist).</summary>
    private List<(string Code, string Label, DummySlot Slot)> BuildUseFilterCatalogFromCodes(
        IEnumerable<string> codes,
        IReadOnlyDictionary<string, ItemStack>? stacksByCode)
    {
        var result = new List<(string Code, string Label, DummySlot Slot)>(64);
        // Deduplicate by family: fence-oak + fence-birch → one tile.
        var seenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var preferredByGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in codes)
        {
            var code = NormalizeUseFilterCode(raw);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var gk = ClaimCodeUtil.GetCatalogGroupKey(code);
            if (string.IsNullOrWhiteSpace(gk))
            {
                gk = code;
            }

            if (!preferredByGroup.TryGetValue(gk, out var existing)
                || PreferCatalogDisplayCode(code, existing, stacksByCode))
            {
                preferredByGroup[gk] = code;
            }
        }

        foreach (var code in preferredByGroup.Values)
        {
            var gk = ClaimCodeUtil.GetCatalogGroupKey(code);
            if (string.IsNullOrWhiteSpace(gk))
            {
                gk = code;
            }

            if (!seenGroups.Add(gk))
            {
                continue;
            }

            // Стек со скана (фонарь с material) — приоритет.
            if (stacksByCode != null
                && stacksByCode.TryGetValue(code, out var scanStack)
                && scanStack?.Collectible != null)
            {
                var label = ClaimCodeUtil.GetFriendlyBlockLabel(code, scanStack);
                try
                {
                    var clone = scanStack.Clone();
                    clone.StackSize = 1;
                    result.Add((code, label, new DummySlot(clone)));
                    continue;
                }
                catch
                {
                    // fall through to resolve
                }
            }

            // groundstorage — без 3D, Cairo в grid.
            if (ClaimCodeUtil.NeedsCairoIcon(code))
            {
                var gsLabel = ClaimCodeUtil.GetFriendlyBlockLabel(code);
                result.Add((code, gsLabel, new DummySlot(null)));
                continue;
            }

            // coalpile / charcoalpile — GetName() часто «Unknown»; иконка = item coal/charcoal.
            if (ClaimCodeUtil.IsCoalOrCharcoalPile(code))
            {
                var pileLabel = ClaimCodeUtil.GetFriendlyBlockLabel(code);
                var pileStack = TryBuildCoalPileDisplayStack(code);
                result.Add((code, pileLabel, pileStack != null ? new DummySlot(pileStack) : new DummySlot(null)));
                continue;
            }

            // Фруктовое дерево / куст
            if (ClaimCodeUtil.IsFruitTreeOrBush(code))
            {
                var fruitCode = ClaimCodeUtil.GetFruitTreeWhitelistCode(code);
                var fruitLabel = ClaimCodeUtil.GetFriendlyBlockLabel(fruitCode);
                ItemStack? fruitStack = null;
                if (stacksByCode != null)
                {
                    stacksByCode.TryGetValue(code, out fruitStack);
                    fruitStack ??= stacksByCode.TryGetValue(fruitCode, out var fs) ? fs : null;
                }

                fruitStack ??= TryResolveItemStack("game:fruit-redapple")
                               ?? TryResolveItemStack("fruit-redapple");
                result.Add((fruitCode, fruitLabel, fruitStack != null ? new DummySlot(fruitStack) : new DummySlot(null)));
                continue;
            }

            // armorstand — item/entity, не блок
            if (code.Contains("armorstand", StringComparison.OrdinalIgnoreCase)
                || code.Contains("strawdummy", StringComparison.OrdinalIgnoreCase))
            {
                ItemStack? standStack = null;
                if (stacksByCode != null && stacksByCode.TryGetValue(code, out var ss))
                {
                    standStack = ss;
                }

                standStack ??= TryResolveItemStack(code) ?? TryResolveItemStack("game:armorstand");
                var standLabel = ClaimCodeUtil.GetFriendlyBlockLabel(code, standStack);
                if (ClaimCodeUtil.IsUnknownLabel(standLabel))
                {
                    standLabel = Lang.Get("swixyclaimchunk:use-filter-armorstand");
                }

                result.Add((code, standLabel, standStack != null ? new DummySlot(standStack) : new DummySlot(null)));
                continue;
            }

            if (TryResolveUseFilterEntry(code, out var entry))
            {
                // Подменить Unknown на lang.
                if (ClaimCodeUtil.IsUnknownLabel(entry.Label))
                {
                    entry = (entry.Code, ClaimCodeUtil.GetFriendlyBlockLabel(entry.Code, entry.Slot?.Itemstack), entry.Slot);
                }

                result.Add(entry);
            }
            else
            {
                result.Add((code, ClaimCodeUtil.GetFriendlyBlockLabel(code), new DummySlot(null)));
            }
        }

        result.Sort(static (a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>Резолв кода в ItemStack/название для иконки плитки.</summary>
    private bool TryResolveUseFilterEntry(
        string code,
        out (string Code, string Label, DummySlot Slot) entry)
    {
        entry = default;
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        ItemStack? stack = null;
        try
        {
            var loc = new AssetLocation(code);
            var block = clientApi.World.GetBlock(loc);
            if (block != null && block.Id != 0)
            {
                // Multiblock/EP: иконка только с creative-варианта (*-south) —
                // GuiTransform origin под него. Иначе меш «уезжает» в плитке.
                var standardCode = ClaimCodeUtil.ResolveStandardDisplayCode(clientApi.World, block);
                if (!string.IsNullOrWhiteSpace(standardCode))
                {
                    try
                    {
                        var stdBlock = clientApi.World.GetBlock(new AssetLocation(standardCode));
                        if (stdBlock != null && stdBlock.Id != 0)
                        {
                            block = stdBlock;
                            code = standardCode;
                        }
                    }
                    catch
                    {
                        // keep original block
                    }
                }

                stack = ClaimCodeUtil.TryGetFamilyCreativeStack(clientApi.World, block)
                        ?? TryGetPreferredCreativeStack(block)
                        ?? new ItemStack(block, 1);
                // Фонарь без attributes не рендерится — defaults.
                EnsureLanternDefaultAttributes(stack);
            }
            else
            {
                var item = clientApi.World.GetItem(loc);
                if (item != null && item.Id != 0)
                {
                    stack = TryGetPreferredCreativeStack(item) ?? new ItemStack(item, 1);
                }
            }
        }
        catch
        {
            return false;
        }

        if (stack?.Collectible == null)
        {
            return false;
        }

        try
        {
            stack = stack.Clone();
            stack.StackSize = 1;
        }
        catch
        {
            return false;
        }

        var label = ClaimCodeUtil.GetFriendlyBlockLabel(code, stack);
        entry = (code, label, new DummySlot(stack));
        return true;
    }

    private ItemStack? TryResolveItemStack(string code)
    {
        try
        {
            var item = clientApi.World.GetItem(new AssetLocation(code));
            if (item != null && item.Id != 0)
            {
                return new ItemStack(item, 1);
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    /// <summary>Иконка кучи угля/древесного угля — item, т.к. у блока нет нормального mesh.</summary>
    private ItemStack? TryBuildCoalPileDisplayStack(string code)
    {
        var path = code;
        var colon = code.IndexOf(':');
        if (colon >= 0 && colon + 1 < code.Length)
        {
            path = code[(colon + 1)..];
        }

        // Prefer items that always render in inventory.
        string[] itemCodes = path.Contains("charcoal", StringComparison.OrdinalIgnoreCase)
            ? ["game:charcoal", "charcoal"]
            : ["game:ore-bituminouscoal", "game:ore-lignite", "game:ore-anthracite", "game:charcoal", "charcoal"];

        foreach (var ic in itemCodes)
        {
            try
            {
                var item = clientApi.World.GetItem(new AssetLocation(ic));
                if (item != null && item.Id != 0)
                {
                    return new ItemStack(item, 1);
                }
            }
            catch
            {
                // next
            }
        }

        // Fallback: block itself
        try
        {
            var block = clientApi.World.GetBlock(new AssetLocation(code));
            if (block != null && block.Id != 0)
            {
                return new ItemStack(block, 1);
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    /// <summary>
    /// Стек как в креативе/инвентаре (с attributes) — иначе EP-машины рисуются со смещением.
    /// Ищет по самому collectible и по «родственным» блокам той же группы.
    /// </summary>
    private ItemStack? TryGetPreferredCreativeStack(CollectibleObject col)
    {
        var fromSelf = TryCreativeStacksOn(col);
        if (fromSelf != null)
        {
            return fromSelf;
        }

        // World-oriented block may lack stacks; look up standard-facing sibling.
        if (col is Block block && block.Code != null)
        {
            var standardCode = ClaimCodeUtil.ResolveStandardDisplayCode(clientApi.World, block);
            if (!string.IsNullOrWhiteSpace(standardCode)
                && !string.Equals(standardCode, block.Code.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var sibling = clientApi.World.GetBlock(new AssetLocation(standardCode));
                    var fromSibling = TryCreativeStacksOn(sibling);
                    if (fromSibling != null)
                    {
                        return fromSibling;
                    }

                    if (sibling != null && sibling.Id != 0)
                    {
                        return new ItemStack(sibling, 1);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        return null;
    }

    private ItemStack? TryCreativeStacksOn(CollectibleObject? col)
    {
        if (col is Block b)
        {
            return ClaimCodeUtil.TryGetFamilyCreativeStack(clientApi.World, b);
        }

        if (col?.CreativeInventoryStacks is not { Length: > 0 })
        {
            return null;
        }

        foreach (var tab in col.CreativeInventoryStacks)
        {
            if (tab?.Stacks == null)
            {
                continue;
            }

            foreach (var js in tab.Stacks)
            {
                if (js == null)
                {
                    continue;
                }

                try
                {
                    if (js.ResolvedItemstack == null)
                    {
                        js.Resolve(clientApi.World, "swixyclaimchunk use-filter", col.Code);
                    }

                    var stack = js.ResolvedItemstack;
                    if (stack?.Collectible != null)
                    {
                        return stack.Clone();
                    }
                }
                catch
                {
                    // next
                }
            }
        }

        return null;
    }

    private static void EnsureLanternDefaultAttributes(ItemStack stack)
    {
        var path = stack.Collectible?.Code?.Path ?? "";
        if (!path.Contains("lantern", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        stack.Attributes ??= new TreeAttribute();
        if (!stack.Attributes.HasAttribute("material"))
        {
            stack.Attributes.SetString("material", "copper");
        }

        if (!stack.Attributes.HasAttribute("lining"))
        {
            stack.Attributes.SetString("lining", "plain");
        }

        if (!stack.Attributes.HasAttribute("glass"))
        {
            stack.Attributes.SetString("glass", "quartz");
        }
    }

    private static string NormalizeUseFilterCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        var trimmed = raw.Trim();
        try
        {
            var loc = new AssetLocation(trimmed);
            if (string.IsNullOrWhiteSpace(loc.Domain) || string.IsNullOrWhiteSpace(loc.Path))
            {
                return trimmed;
            }

            return loc.ToString();
        }
        catch
        {
            return trimmed;
        }
    }

    /// <summary>Отправляет ClaimAccessActionPacket для выбранного привата.</summary>
    private void SendClaimAction(int action, string playerName, int accessFlags, string claimName, string playerUid = "")
    {
        if (selectedClaimId <= 0)
        {
            return;
        }

        channel.SendPacket(new ClaimAccessActionPacket
        {
            ClaimId = selectedClaimId,
            Action = action,
            PlayerName = playerName,
            PlayerUid = playerUid,
            AccessFlags = accessFlags,
            ClaimName = claimName
        });
    }

    #endregion

    #region Ячейки списков приватов и участников

    /// <summary>Фабрика ячейки списка приватов с колбэками клика/подсветки/удаления.</summary>
    private IGuiElementCell CreateClaimListCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        // CellList injects UnscaledCellHor/VerPadding into these bounds — zero them out.
        bounds.fixedPaddingX = 0;
        bounds.fixedPaddingY = 0;

        var claim = FindClaimForCell(cell);
        var element = new ClaimHighlightListCell(clientApi, cell, bounds, claim?.ClaimId == highlightedClaimId)
        {
            AllowDelete = claim is { ViewerIsCoOwner: false },
            // Panel height only; the 8px gap is CellList.unscaledCellSpacing (see ConfigureClaimListSpacing).
            FixedHeight = ClaimsCardH,
            OnMouseDownOnCellLeft = SelectClaimCell,
            OnMouseDownOnCellRight = ToggleClaimHighlightCell,
            OnMouseDownOnCellDelete = DeleteClaimCell
        };
        return element;
    }

    /// <summary>
    /// VS GuiElementCellList defaults: spacing=10, verPad=4, horPad=7 — kills SVG 8px gaps.
    /// Must set after Compose and after every ReloadCells.
    /// </summary>
    private void ConfigureClaimListSpacing()
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("claimList");
        if (cellList == null)
        {
            return;
        }

        cellList.unscaledCellSpacing = ClaimsCardGap; // 8
        cellList.UnscaledCellVerPadding = 0;
        cellList.UnscaledCellHorPadding = 0;

        foreach (var cell in cellList.elementCells)
        {
            cell.Bounds.fixedPaddingX = 0;
            cell.Bounds.fixedPaddingY = 0;
            if (cell is ClaimHighlightListCell row)
            {
                row.FixedHeight = ClaimsCardH;
            }

            cell.UpdateCellHeight();
        }

        cellList.CalcTotalHeight();
    }

    /// <summary>Фабрика ячейки участника с переключателями Use/Build и удалением.</summary>
    private IGuiElementCell CreateMemberCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        bounds.fixedPaddingX = 0;
        bounds.fixedPaddingY = 0;

        var member = FindMemberForCell(cell);
        var flags = (EnumBlockAccessFlags)(member?.AccessFlags ?? 0);
        var element = new ClaimMemberListCell(
            clientApi,
            cell,
            bounds,
            flags.HasFlag(EnumBlockAccessFlags.Use),
            flags.HasFlag(EnumBlockAccessFlags.BuildOrBreak),
            member?.IsOwner ?? false)
        {
            MemberUid = member?.PlayerUid ?? "",
            IsCoOwner = member?.IsCoOwner ?? false,
            AllowCoOwnerCrown = GetSelectedClaim() is { ViewerIsCoOwner: false },
            // Group 470 row 58; gap via CellList.unscaledCellSpacing.
            FixedHeight = MembersRowH,
            OnMouseDownOnCellLeft = SelectMemberCell,
            OnMakeOwner = GrantCoOwnershipByUid,
            OnToggleUse = ToggleMemberUseByUid,
            OnToggleBuild = ToggleMemberBuildByUid,
            OnDeleteMember = RemoveMemberByUid
        };
        return element;
    }

    /// <summary>Member list: Group 470 row 58 + spacing 8. Zero VS default pads.</summary>
    private void ConfigureMemberListSpacing()
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("memberList");
        if (cellList == null)
        {
            return;
        }

        cellList.unscaledCellSpacing = MembersRowGap;
        cellList.UnscaledCellVerPadding = 0;
        cellList.UnscaledCellHorPadding = 0;

        foreach (var cell in cellList.elementCells)
        {
            cell.Bounds.fixedPaddingX = 0;
            cell.Bounds.fixedPaddingY = 0;
            if (cell is ClaimMemberListCell row)
            {
                row.FixedHeight = MembersRowH;
            }

            cell.UpdateCellHeight();
        }

        cellList.CalcTotalHeight();
    }

    /// <summary>Подпись строки привата в левом списке (SVG: CHUNKS: n).</summary>
    private string BuildClaimListDetailText(ClaimInfoPacket claim)
    {
        return claim.ViewerIsCoOwner
            ? Lang.Get("swixyclaimchunk:claims-list-coowner-stats", claim.OwnerName, claim.ChunkCount).ToUpperInvariant()
            : Lang.Get("swixyclaimchunk:claims-list-stats", claim.ChunkCount).ToUpperInvariant();
    }

    /// <summary>Строит данные строк списка приватов из claimListState.</summary>
    private IEnumerable<SavegameCellEntry> BuildClaimCells()
    {
        var titleFont = ClaimFontHelper.Create(15, ClaimFontHelper.ColorCream, bold: true);
        var detailFont = ClaimFontHelper.Create(13, ClaimFontHelper.ColorAccent, bold: true);
        foreach (var claim in claimListState?.Claims ?? [])
        {
            yield return new SavegameCellEntry
            {
                Title = Lang.Get("swixyclaimchunk:claims-list-name", claim.Name).ToUpperInvariant(),
                DetailText = BuildClaimListDetailText(claim),
                TitleFont = titleFont,
                DetailTextFont = detailFont,
                LeftOffY = 8,
                DetailTextOffY = 4,
                HoverText = Lang.Get("swixyclaimchunk:claims-highlight-hint"),
                Selected = claim.ClaimId == selectedClaimId,
                Enabled = true,
                DrawAsButton = true
            };
        }
    }

    /// <summary>Строит данные строк списка участников выбранного привата.</summary>
    private IEnumerable<SavegameCellEntry> BuildMemberCells(ClaimInfoPacket selectedClaim)
    {
        // Group 471 member names: #9F795B @ ~16px, UPPERCASE.
        var titleFont = ClaimFontHelper.Create(16, ColSettingsBtn, bold: true);
        foreach (var member in selectedClaim.Members ?? [])
        {
            yield return new SavegameCellEntry
            {
                Title = member.PlayerName.ToUpperInvariant(),
                DetailText = "",
                TitleFont = titleFont,
                Selected = false,
                Enabled = true,
                DrawAsButton = true
            };
        }
    }

    /// <summary>Находит участника по заголовку ячейки (ник игрока, UPPERCASE в UI).</summary>
    private ClaimMemberPacket? FindMemberForCell(SavegameCellEntry cell)
    {
        foreach (var member in GetSelectedClaim()?.Members ?? [])
        {
            if (string.Equals(member.PlayerName, cell.Title, StringComparison.OrdinalIgnoreCase))
            {
                return member;
            }
        }

        return null;
    }

    /// <summary>Возвращает выбранного участника по selectedMemberUid.</summary>
    private ClaimMemberPacket? GetSelectedMember()
    {
        return GetSelectedClaim()?.Members.FirstOrDefault(member => member.PlayerUid == selectedMemberUid);
    }

    #endregion

    #region Обработчики кликов по ячейкам

    /// <summary>Клик по строке привата — выбор привата.</summary>
    private void SelectClaimCell(int index)
    {
        var claim = GetClaimAt(index);
        if (claim == null)
        {
            return;
        }

        SelectClaimButton(claim);
    }

    /// <summary>Удаление привата; при активной подсветке сначала снимает её на сервере.</summary>
    private void DeleteClaimCell(int index)
    {
        var claim = GetClaimAt(index);
        if (claim == null || claim.ViewerIsCoOwner)
        {
            return;
        }

        SelectClaim(claim);

        if (highlightedClaimId == claim.ClaimId)
        {
            channel.SendPacket(new ClaimShowRequestPacket
            {
                ClaimId = claim.ClaimId,
                Clear = true
            });
            highlightedClaimId = 0;
            pendingHighlightClaimId = -1;
        }

        SendClaimAction(ClaimAccessActionType.DeleteClaim, "", 0, "");
    }

    /// <summary>Переключает право Use у участника (оптимистичное обновление UI).</summary>
    private void ToggleMemberUseByUid(string memberUid)
    {
        ToggleMemberAccessByUid(memberUid, EnumBlockAccessFlags.Use);
    }

    /// <summary>Переключает право Build у участника (оптимистичное обновление UI).</summary>
    private void ToggleMemberBuildByUid(string memberUid)
    {
        ToggleMemberAccessByUid(memberUid, EnumBlockAccessFlags.BuildOrBreak);
    }

    /// <summary>Инвертирует флаг доступа и отправляет UpdateMemberAccess на сервер.</summary>
    private void ToggleMemberAccessByUid(string memberUid, EnumBlockAccessFlags flag)
    {
        var member = FindMemberByUid(memberUid);
        if (member == null || member.IsOwner)
        {
            return;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        var savedMemberScroll = GetMemberListScrollOffset();
        var flags = (EnumBlockAccessFlags)member.AccessFlags;
        member.AccessFlags = flags.HasFlag(flag)
            ? (int)(flags & ~flag)
            : (int)(flags | flag);
        member.AccessName = FormatMemberAccessName(member.AccessFlags, member.IsOwner, member.IsCoOwner);
        RefreshMemberAccessIcons(savedClaimScroll, savedMemberScroll);
        SendClaimAction(ClaimAccessActionType.UpdateMemberAccess, member.PlayerName, member.AccessFlags, "", member.PlayerUid);
    }

    /// <summary>Ищет участника в выбранном привате по UID.</summary>
    private ClaimMemberPacket? FindMemberByUid(string memberUid)
    {
        return GetSelectedClaim()?.Members.FirstOrDefault(member => member.PlayerUid == memberUid);
    }

    /// <summary>Клик по лампочке — вкл/выкл подсветку привата в мире.</summary>
    private void ToggleClaimHighlightCell(int index)
    {
        var claim = GetClaimAt(index);
        if (claim == null)
        {
            return;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        var claimChanged = selectedClaimId != claim.ClaimId;
        var turningOff = highlightedClaimId == claim.ClaimId;

        SelectClaim(claim);
        SetPendingHighlightState(turningOff ? 0 : claim.ClaimId);

        channel.SendPacket(new ClaimShowRequestPacket
        {
            ClaimId = claim.ClaimId,
            Clear = turningOff
        });

        if (claimChanged)
        {
            RunClaimsUiDeferred(() => RefreshClaimsSelectionUi(savedClaimScroll, 0));
        }
        else
        {
            RunClaimsUiDeferred(() => RefreshClaimHighlightIcons(savedClaimScroll));
        }
    }

    /// <summary>Сопоставляет ячейку списка с ClaimInfoPacket по имени и статистике.</summary>
    private ClaimInfoPacket? FindClaimForCell(SavegameCellEntry cell)
    {
        foreach (var claim in claimListState?.Claims ?? [])
        {
            var title = Lang.Get("swixyclaimchunk:claims-list-name", claim.Name).ToUpperInvariant();
            if (title == cell.Title && BuildClaimListDetailText(claim) == cell.DetailText)
            {
                return claim;
            }
        }

        return null;
    }

    #endregion

    #region Подсветка приватов в мире

    /// <summary>Локально выставляет ожидаемое состояние подсветки до ответа сервера.</summary>
    private void SetPendingHighlightState(int claimId)
    {
        pendingHighlightClaimId = claimId;
        highlightedClaimId = claimId;
    }

    /// <summary>Применяет подтверждённое сервером состояние подсветки; учитывает pending-флаг.</summary>
    private void ApplyHighlightStateFromServer(bool active, int claimId)
    {
        if (active)
        {
            if (pendingHighlightClaimId < 0 || pendingHighlightClaimId == claimId)
            {
                highlightedClaimId = claimId;
                pendingHighlightClaimId = -1;
            }

            return;
        }

        if (pendingHighlightClaimId > 0)
        {
            return;
        }

        if (pendingHighlightClaimId == 0 || pendingHighlightClaimId < 0)
        {
            highlightedClaimId = 0;
            pendingHighlightClaimId = -1;
        }
    }

    /// <summary>Обновляет иконки лампочек в списке приватов без перезагрузки ячеек.</summary>
    private void RefreshClaimHighlightIcons(float savedScroll)
    {
        RefreshClaimListSelection(savedScroll, reloadCells: false);
    }

    /// <summary>Обновляет иконки Use/Build в списке участников и восстанавливает скролл.</summary>
    private void RefreshMemberAccessIcons(float savedClaimScroll, float savedMemberScroll)
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("memberList");
        if (cellList == null)
        {
            return;
        }

        UpdateMemberListIconsInPlace(cellList);
        RestoreClaimListScroll(savedClaimScroll);
        RestoreMemberListScroll(savedMemberScroll);
    }

    /// <summary>Перекомпоновывает существующие ClaimMemberListCell по данным из кэша.</summary>
    private void UpdateMemberListIconsInPlace(GuiElementCellList<SavegameCellEntry> cellList)
    {
        for (var i = 0; i < cellList.elementCells.Count; i++)
        {
            if (cellList.elementCells[i] is not ClaimMemberListCell memberCell)
            {
                continue;
            }

            var member = GetMemberAt(i);
            if (member == null)
            {
                continue;
            }

            var flags = (EnumBlockAccessFlags)member.AccessFlags;
            memberCell.MemberUid = member.PlayerUid;
            memberCell.AccessUse = flags.HasFlag(EnumBlockAccessFlags.Use);
            memberCell.AccessBuild = flags.HasFlag(EnumBlockAccessFlags.BuildOrBreak);
            memberCell.IsOwner = member.IsOwner;
            memberCell.IsCoOwner = member.IsCoOwner;
            memberCell.AllowCoOwnerCrown = !(GetSelectedClaim()?.ViewerIsCoOwner ?? true);
            memberCell.Compose();
        }
    }

    /// <summary>Формирует локализованную строку прав доступа (Use, Build или «нет»).</summary>
    private static string FormatMemberAccessName(int accessFlags, bool isOwner = false, bool isCoOwner = false)
    {
        if (isOwner)
        {
            return Lang.Get("swixyclaimchunk:claims-owner-role");
        }

        if (isCoOwner)
        {
            return Lang.Get("swixyclaimchunk:claims-coowner-role");
        }

        var flags = (EnumBlockAccessFlags)accessFlags;
        var parts = new List<string>();
        if (flags.HasFlag(EnumBlockAccessFlags.Use))
        {
            parts.Add(Lang.Get("swixyclaimchunk:claims-access-use"));
        }

        if (flags.HasFlag(EnumBlockAccessFlags.BuildOrBreak))
        {
            parts.Add(Lang.Get("swixyclaimchunk:claims-access-build"));
        }

        return parts.Count > 0
            ? string.Join(", ", parts)
            : Lang.Get("swixyclaimchunk:claims-access-none");
    }

    /// <summary>Клик по строке участника — выбор участника.</summary>
    private void SelectMemberCell(int index)
    {
        var member = GetMemberAt(index);
        if (member == null)
        {
            return;
        }

        SelectMemberButton(member);
    }

    #endregion

    #region Доступ к данным списков

    /// <summary>Возвращает пакет выбранного привата или null.</summary>
    private ClaimInfoPacket? GetSelectedClaim()
    {
        return claimListState?.Claims.FirstOrDefault(claim => claim.ClaimId == selectedClaimId);
    }

    /// <summary>Возвращает приват по индексу в claimListState.Claims.</summary>
    private ClaimInfoPacket? GetClaimAt(int index)
    {
        var claims = claimListState?.Claims;
        if (claims == null || index < 0 || index >= claims.Count)
        {
            return null;
        }

        return claims[index];
    }

    /// <summary>Возвращает участника по индексу в Members выбранного привата.</summary>
    private ClaimMemberPacket? GetMemberAt(int index)
    {
        var members = GetSelectedClaim()?.Members;
        if (members == null || index < 0 || index >= members.Count)
        {
            return null;
        }

        return members[index];
    }

    #endregion

    #region Скролл списков и отложенное обновление UI

    /// <summary>Применяет scroll value к списку приватов и перерисовывает thumb.</summary>
    private void OnClaimListScroll(float value)
    {
        RestoreClaimListScroll(value);
        try
        {
            SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>clip/table heights and max scroll for the claims list.</summary>
    private void GetClaimListScrollMetrics(out float clipH, out float tableH, out float maxScroll)
    {
        clipH = ClaimsListH;
        tableH = ClaimsListH;
        maxScroll = 0;

        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("claimList");
        if (cellList != null)
        {
            cellList.CalcTotalHeight();
            cellList.Bounds.CalcWorldBounds();
            tableH = (float)cellList.Bounds.fixedHeight;
        }

        if (claimListClipBounds != null)
        {
            claimListClipBounds.CalcWorldBounds();
            clipH = (float)claimListClipBounds.fixedHeight;
        }

        maxScroll = Math.Max(0, tableH - clipH);
    }

    /// <summary>Thumb rect in dialog-local design px (for draw + hit).</summary>
    private void GetClaimScrollThumbDesign(out double thumbY, out double thumbH)
    {
        GetThumbDesign(
            claimListScrollValue,
            ClaimsScrollY,
            ClaimsScrollH,
            GetClaimListScrollMetrics,
            out thumbY,
            out thumbH);
    }

    private void GetMemberListScrollMetrics(out float clipH, out float tableH, out float maxScroll)
    {
        clipH = MembersH;
        tableH = MembersH;
        maxScroll = 0;

        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("memberList");
        if (cellList != null)
        {
            cellList.CalcTotalHeight();
            cellList.Bounds.CalcWorldBounds();
            tableH = (float)cellList.Bounds.fixedHeight;
        }

        if (memberListClipBounds != null)
        {
            memberListClipBounds.CalcWorldBounds();
            clipH = (float)memberListClipBounds.fixedHeight;
        }

        maxScroll = Math.Max(0, tableH - clipH);
    }

    private void GetMemberScrollThumbDesign(out double thumbY, out double thumbH)
    {
        GetThumbDesign(
            memberListScrollValue,
            MembersScrollY,
            MembersScrollH,
            GetMemberListScrollMetrics,
            out thumbY,
            out thumbH);
    }

    private void GetUseFilterScrollThumbDesign(out double thumbY, out double thumbH)
    {
        if (useFilterGrid == null || useFilterViewportBounds == null)
        {
            thumbY = 0;
            thumbH = 24;
            return;
        }

        useFilterGrid.GetScrollThumbDesign(
            useFilterViewportBounds.fixedY,
            useFilterViewportBounds.fixedHeight,
            out thumbY,
            out thumbH);
    }

    private void GetThumbDesign(
        float scrollValue,
        int trackY,
        int trackH,
        GetScrollMetrics metrics,
        out double thumbY,
        out double thumbH)
    {
        metrics(out var clipH, out var tableH, out var maxScroll);
        if (tableH <= clipH || maxScroll <= 0)
        {
            thumbY = trackY;
            thumbH = trackH;
            return;
        }

        thumbH = Math.Max(ClaimsScrollThumbMinH, trackH * (clipH / tableH));
        thumbH = Math.Min(thumbH, trackH);
        var t = scrollValue / maxScroll;
        thumbY = trackY + t * (trackH - thumbH);
    }

    private delegate void GetScrollMetrics(out float clipH, out float tableH, out float maxScroll);

    private void OnMemberListScrollCustom(float value)
    {
        RestoreMemberListScroll(value);
        try
        {
            SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Планирует обновление UI на следующий тик (0 мс). Объединяет несколько вызовов за кадр;
    /// не выполняется, если диалог закрыт или активна вкладка карты.
    /// </summary>
    private void RunClaimsUiDeferred(Action action)
    {
        deferredClaimsUiAction = action;
        if (claimsUiDeferScheduled)
        {
            return;
        }

        claimsUiDeferScheduled = true;
        // RegisterCallback(0) — один кадр задержки, чтобы не сбрасывать скролл при серии кликов
        clientApi.Event.RegisterCallback(_ =>
        {
            claimsUiDeferScheduled = false;
            var deferredAction = deferredClaimsUiAction;
            deferredClaimsUiAction = null;

            if (!IsOpened() || activePage != PageClaims)
            {
                return;
            }

            deferredAction?.Invoke();
        }, 0);
    }

    /// <summary>Обновляет выбор привата: in-place или полная пересборка с восстановлением скролла.</summary>
    private void RefreshClaimsSelectionUi(float savedClaimScroll, float savedMemberScroll)
    {
        if (!TryRefreshClaimsPageInPlace(savedClaimScroll, savedMemberScroll, reloadClaimCells: false))
        {
            ComposeDialog();
            RestoreClaimListScroll(savedClaimScroll);
            RestoreMemberListScroll(savedMemberScroll);
            ApplyClaimsPageInputState();
        }
    }

    /// <summary>Обновляет выбор участника: перезагрузка списка участников или ComposeDialog.</summary>
    private void RefreshMemberSelectionUi(float savedClaimScroll, float savedMemberScroll)
    {
        if (!TryRefreshMemberListInPlace(savedClaimScroll, savedMemberScroll))
        {
            ComposeDialog();
            RestoreClaimListScroll(savedClaimScroll);
            RestoreMemberListScroll(savedMemberScroll);
            ApplyClaimsPageInputState();
        }
    }

    /// <summary>Пытается обновить вкладку приватов без ComposeDialog (список, детали, скролл).</summary>
    private bool TryRefreshClaimsPageInPlace(float savedClaimScroll, float savedMemberScroll, bool reloadClaimCells = true)
    {
        if (activePage != PageClaims || SingleComposer == null || GetSelectedClaim() == null)
        {
            return false;
        }

        if (SingleComposer.GetTextInput("claimNameInput") == null)
        {
            return false;
        }

        RefreshClaimListSelection(savedClaimScroll, reloadClaimCells);
        RefreshClaimDetailsUi();
        RestoreMemberListScroll(savedMemberScroll);
        return true;
    }

    /// <summary>Перезагружает только список участников и восстанавливает оба скролла.</summary>
    private bool TryRefreshMemberListInPlace(float savedClaimScroll, float savedMemberScroll)
    {
        if (activePage != PageClaims || SingleComposer == null || GetSelectedClaim() == null)
        {
            return false;
        }

        var memberList = SingleComposer.GetCellList<SavegameCellEntry>("memberList");
        if (memberList == null)
        {
            return false;
        }

        memberList.ReloadCells(BuildMemberCells(GetSelectedClaim()!));
        ConfigureMemberListSpacing();
        RestoreClaimListScroll(savedClaimScroll);
        RestoreMemberListScroll(savedMemberScroll);
        ApplyClaimsPageInputState();
        return true;
    }

    /// <summary>Обновляет выделение/лампочки в списке приватов; опционально ReloadCells.</summary>
    private void RefreshClaimListSelection(float savedScroll, bool reloadCells)
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("claimList");
        if (cellList == null)
        {
            return;
        }

        if (reloadCells)
        {
            cellList.ReloadCells(BuildClaimCells());
            ConfigureClaimListSpacing();
        }
        else
        {
            UpdateClaimListSelectionInPlace(cellList);
        }

        RestoreClaimListScroll(savedScroll);
    }

    /// <summary>Обновляет Selected и HighlightActive у существующих ClaimHighlightListCell.</summary>
    private void UpdateClaimListSelectionInPlace(GuiElementCellList<SavegameCellEntry> cellList)
    {
        for (var i = 0; i < cellList.elementCells.Count; i++)
        {
            if (cellList.elementCells[i] is not ClaimHighlightListCell highlightCell)
            {
                continue;
            }

            var claim = GetClaimAt(i);
            if (claim == null)
            {
                continue;
            }

            highlightCell.cellEntry.Selected = claim.ClaimId == selectedClaimId;
            highlightCell.HighlightActive = claim.ClaimId == highlightedClaimId;
            highlightCell.AllowDelete = !claim.ViewerIsCoOwner;
            highlightCell.Compose();
        }
    }

    /// <summary>Обновляет статистику, поле имени и список участников правой панели.</summary>
    private void RefreshClaimDetailsUi()
    {
        var selectedClaim = GetSelectedClaim();
        if (selectedClaim == null || SingleComposer == null)
        {
            return;
        }

        claimNameInput = selectedClaim.Name;
        SingleComposer.GetCustomDraw("claimsPageChrome")?.Redraw();

        var memberList = SingleComposer.GetCellList<SavegameCellEntry>("memberList");
        if (memberList != null)
        {
            memberList.ReloadCells(BuildMemberCells(selectedClaim));
            ConfigureMemberListSpacing();
            RestoreMemberListScroll(0);
        }

        ApplyClaimsPageInputState();
    }

    /// <summary>Текущий сдвиг скролла списка приватов (из fixedY или кэша).</summary>
    private float GetClaimListScrollOffset()
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("claimList");
        if (cellList != null)
        {
            return (float)System.Math.Max(0, -cellList.Bounds.fixedY);
        }

        return claimListScrollValue;
    }

    /// <summary>Текущий сдвиг скролла списка участников.</summary>
    private float GetMemberListScrollOffset()
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("memberList");
        if (cellList != null)
        {
            return (float)System.Math.Max(0, -cellList.Bounds.fixedY);
        }

        return memberListScrollValue;
    }

    /// <summary>Применяет scroll value к списку участников (без VS scrollbar).</summary>
    private void OnMemberListScroll(float value)
    {
        OnMemberListScrollCustom(value);
    }

    /// <summary>Восстанавливает сохранённые позиции скролла после ComposeDialog.</summary>
    private void ApplyClaimsPageScrollState()
    {
        if (activePage == PageMap || SingleComposer == null)
        {
            return;
        }

        RestoreClaimListScroll(claimListScrollValue);
        RestoreMemberListScroll(memberListScrollValue);
    }

    /// <summary>Пересчитывает высоты, ограничивает scrollValue и синхронизирует скроллбар с таблицей.</summary>
    private void RestoreClaimListScroll(float scrollValue)
    {
        if (SingleComposer == null || claimListClipBounds == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<SavegameCellEntry>("claimList");
        if (cellList == null)
        {
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        claimListClipBounds.CalcWorldBounds();

        var clipHeight = (float)claimListClipBounds.fixedHeight;
        var tableHeight = (float)cellList.Bounds.fixedHeight;
        var maxScroll = System.Math.Max(0, tableHeight - clipHeight);
        claimListScrollValue = System.Math.Clamp(scrollValue, 0, maxScroll);

        cellList.Bounds.fixedY = -claimListScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

    /// <summary>Аналог RestoreClaimListScroll для списка участников.</summary>
    private void RestoreMemberListScroll(float scrollValue)
    {
        if (SingleComposer == null || memberListClipBounds == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<SavegameCellEntry>("memberList");
        if (cellList == null)
        {
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        memberListClipBounds.CalcWorldBounds();

        var clipHeight = (float)memberListClipBounds.fixedHeight;
        var tableHeight = (float)cellList.Bounds.fixedHeight;
        var maxScroll = System.Math.Max(0, tableHeight - clipHeight);
        memberListScrollValue = System.Math.Clamp(scrollValue, 0, maxScroll);

        cellList.Bounds.fixedY = -memberListScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

    /// <summary>Подставляет claimNameInput/memberNameInput и stats после Compose.</summary>
    private void ApplyClaimsPageInputState()
    {
        if (activePage == PageMap || SingleComposer == null)
        {
            return;
        }

        SingleComposer.GetTextInput("claimNameInput")?.SetValue(claimNameInput, true);
        SingleComposer.GetTextInput("memberNameInput")?.SetValue(memberNameInput, true);

        var selected = GetSelectedClaim();
        if (selected == null)
        {
            return;
        }

        claimNameInput = string.IsNullOrEmpty(claimNameInput) ? selected.Name : claimNameInput;
        SingleComposer.GetTextInput("claimNameInput")?.SetValue(claimNameInput, true);
        SingleComposer.GetCustomDraw("claimsPageChrome")?.Redraw();

        if (claimsRightMode == ClaimsRightUseFilter)
        {
            SyncClaimFlagSwitches(selected);
            SingleComposer.GetDynamicText("useFilterSelectedText")?.SetNewText(BuildUseFilterSelectedText());
            SingleComposer.GetTextInput("useFilterSearch")?.SetValue(useFilterSearch, true);
            // Re-apply after full compose so button textures use centered Montserrat labels.
            SingleComposer.GetButton("useFilterBack")?.SetOrientation(EnumTextOrientation.Center);
            SingleComposer.GetButton("useFilterSave")?.SetOrientation(EnumTextOrientation.Center);
        }
    }

    private void SyncClaimFlagSwitches(ClaimInfoPacket claim)
    {
        // Custom checkboxes are drawn on DynamicCustomDraw chips — just refresh textures.
        SingleComposer?.GetCustomDraw("claimFlagPvpBg")?.Redraw();
        SingleComposer?.GetCustomDraw("claimFlagAnimalsBg")?.Redraw();
    }

    private bool ToggleClaimFlagPvpButton()
    {
        var claim = GetSelectedClaim();
        if (claim == null)
        {
            return true;
        }

        ToggleClaimFlag(ClaimFlagBits.AllowPvp, (claim.ClaimFlags & ClaimFlagBits.AllowPvp) == 0);
        SyncClaimFlagSwitches(claim);
        return true;
    }

    private bool ToggleClaimFlagAnimalsButton()
    {
        var claim = GetSelectedClaim();
        if (claim == null)
        {
            return true;
        }

        // Checkbox = «защищать животных». Внутри: AllowAnimalDamage (инверсия).
        // Сейчас защищены → снять защиту (включить AllowAnimalDamage).
        // Сейчас не защищены → защитить (сбросить AllowAnimalDamage).
        var protectedNow = ClaimFlagBits.AreAnimalsProtected(claim.ClaimFlags);
        ToggleClaimFlag(ClaimFlagBits.AllowAnimalDamage, enabled: protectedNow);
        SyncClaimFlagSwitches(claim);
        return true;
    }

    private void ToggleClaimFlag(int bit, bool enabled)
    {
        var claim = GetSelectedClaim();
        if (claim == null)
        {
            return;
        }

        var flags = claim.ClaimFlags;
        if (enabled)
        {
            flags |= bit;
        }
        else
        {
            flags &= ~bit;
        }

        claim.ClaimFlags = flags;
        channel.SendPacket(new ClaimAccessActionPacket
        {
            ClaimId = claim.ClaimId,
            Action = ClaimAccessActionType.SetClaimFlags,
            ClaimFlags = flags
        });
    }

    private void DrawClaimFlagPvpChip(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var on = (GetSelectedClaim()?.ClaimFlags & ClaimFlagBits.AllowPvp) != 0;
        DrawFlagChipWithCheckbox(ctx, bounds, on);
    }

    private void DrawClaimFlagAnimalsChip(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        // Галочка = животные защищены (default ON when flags == 0).
        var on = ClaimFlagBits.AreAnimalsProtected(GetSelectedClaim()?.ClaimFlags ?? 0);
        DrawFlagChipWithCheckbox(ctx, bounds, on);
    }

    #endregion

    #region Карта — центрирование и подписи

    /// <summary>Кнопка «Центр»: позиция игрока и запрос обновления карты.</summary>
    private bool CenterButton()
    {
        CenterOnPlayer();
        gridElement?.CenterMapOnPlayer();
        RequestRefresh();
        return true;
    }

    /// <summary>Вычисляет centerChunkX/Z по блоковой позиции игрока.</summary>
    private void CenterOnPlayer()
    {
        var player = clientApi.World.Player?.Entity;
        if (player == null)
        {
            centerChunkX = 0;
            centerChunkZ = 0;
            return;
        }

        var blockPos = player.Pos.AsBlockPos;
        var chunkSize = GlobalConstants.ChunkSize;
        centerChunkX = FloorDiv(blockPos.X, chunkSize);
        centerChunkZ = FloorDiv(blockPos.Z, chunkSize);
    }

    /// <summary>Обновляет Limits / Legend / status на вкладке карты.</summary>
    private void UpdateText(ClaimMapStatePacket? packet)
    {
        packet ??= mapState;
        if (activePage != PageMap)
        {
            return;
        }

        RedrawMapSideColumn();
    }

    #endregion

    #region Отрисовка фрейма и панелей

    private bool CloseButton()
    {
        TryClose();
        return true;
    }

    /// <summary>Закрытие (legacy title-bar hook).</summary>
    private void OnTitleBarClose()
    {
        TryClose();
    }

    private void EnsureFrameSurface()
    {
        ClaimFontHelper.EnsureRegistered(clientApi);
        frameSurface ??= LoadGuiPng("textures/gui/dialog_frame.png", "dialog_frame.png");
        centerButtonSurface ??= LoadGuiPng("textures/gui/button_center.png", "button_center.png");
        legendPanelSurface ??= LoadGuiPng("textures/gui/panel_legend.png", "panel_legend.png");
        limitsPanelSurface ??= LoadGuiPng("textures/gui/panel_limits.png", "panel_limits.png");
        settingsPanelSurface ??= LoadGuiPng("textures/gui/panel_settings.png", "panel_settings.png");
        settingsButtonSurface ??= LoadGuiPng("textures/gui/btn_settings.png", "btn_settings.png");
        scrollTrackSurface ??= LoadGuiPng("textures/gui/scrollbar_track.png", "scrollbar_track.png");
    }

    private ImageSurface? LoadGuiPng(string assetPath, string logName)
    {
        try
        {
            var asset = clientApi.Assets.TryGet(new AssetLocation("swixyclaimchunk", assetPath));
            if (asset?.Data == null || asset.Data.Length == 0)
            {
                clientApi.Logger.Warning("[SwixyClaimChunk] {0} not found", logName);
                return null;
            }

            using var bitmap = clientApi.Render.BitmapCreateFromPng(asset.Data);
            RepairAlphaFringe(bitmap);
            return GuiElement.getImageSurfaceFromAsset(bitmap);
        }
        catch (Exception ex)
        {
            clientApi.Logger.Error("[SwixyClaimChunk] Failed to load {0}: {1}", logName, ex);
            return null;
        }
    }

    private void DisposeFrameSurface()
    {
        frameSurface?.Dispose();
        frameSurface = null;
        centerButtonSurface?.Dispose();
        centerButtonSurface = null;
        legendPanelSurface?.Dispose();
        legendPanelSurface = null;
        limitsPanelSurface?.Dispose();
        limitsPanelSurface = null;
        settingsPanelSurface?.Dispose();
        settingsPanelSurface = null;
        settingsButtonSurface?.Dispose();
        settingsButtonSurface = null;
        scrollTrackSurface?.Dispose();
        scrollTrackSurface = null;
    }

    /// <summary>
    /// Premultiplies RGB by alpha (same as Questbook) — fixes broken edges on PNG chrome.
    /// </summary>
    private static unsafe void RepairAlphaFringe(BitmapExternal bitmap)
    {
        var pixels = (uint*)bitmap.PixelsPtrAndLock.ToPointer();
        var count = bitmap.Width * bitmap.Height;
        for (var i = 0; i < count; i++)
        {
            var pixel = pixels[i];
            var b = (byte)(pixel & 0xFF);
            var g = (byte)((pixel >> 8) & 0xFF);
            var r = (byte)((pixel >> 16) & 0xFF);
            var a = (byte)((pixel >> 24) & 0xFF);

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

    /// <summary>
    /// Group 462 frame + tab labels / pin / close icons.
    /// </summary>
    private void DrawDialogChrome(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var w = bounds.OuterWidth;
        var h = bounds.OuterHeight;
        var s = Math.Max(0.01, RuntimeEnv.GUIScale);

        // Clear destination so alpha edges don't smear with previous frame.
        ctx.Operator = Operator.Source;
        ctx.SetSourceRGBA(0, 0, 0, 0);
        ctx.Rectangle(0, 0, w, h);
        ctx.Fill();
        ctx.Operator = Operator.Over;

        if (frameSurface != null && frameSurface.Width > 0 && frameSurface.Height > 0)
        {
            ctx.Save();
            // Integer-friendly scale: avoid subpixel blur that shreds pixel UI art.
            var sx = w / frameSurface.Width;
            var sy = h / frameSurface.Height;
            ctx.Scale(sx, sy);
            ctx.SetSourceSurface(frameSurface, 0, 0);
            // Nearest-neighbour: keep metal corners / wood crisp.
            ((SurfacePattern)ctx.GetSource()).Filter = Filter.Nearest;
            ctx.Paint();
            ctx.Restore();
        }
        else
        {
            SetRgb(ctx, ColPanel);
            ctx.Rectangle(0, 0, w, h);
            ctx.Fill();
        }

        // Tab labels: UPPERCASE Montserrat, centered in plate, nudged slightly down.
        // «ПРИВАТЫ» is wider than «КАРТА» — slight +X so ink sits in the visual center of the plate.
        var mapActive = activePage == PageMap;
        DrawTabLabel(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-tab-map").ToUpperInvariant(),
            TabMapX * s, TabY * s, TabMapW * s, TabH * s,
            FontTab,
            mapActive ? ColTabActive : ColTabInactive,
            s,
            nudgeXDesign: 0);
        DrawTabLabel(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-tab-claims").ToUpperInvariant(),
            TabClaimsX * s, TabY * s, TabClaimsW * s, TabH * s,
            FontTab,
            mapActive ? ColTabInactive : ColTabActive,
            s,
            nudgeXDesign: 6);
        // ☰ Fixed/Movable (vanilla title-bar menu) + ✕ close — Cairo on right plate.
        // White when Movable (откреплено / можно таскать), #9F795B when Fixed (закреплено).
        DrawMenuIcon(ctx, PinBtnX * s, TabY * s, PinBtnW * s, TabH * s, isMovable ? ColIconPinned : ColIcon);
        DrawCloseIcon(ctx, CloseBtnX * s, TabY * s, CloseBtnW * s, TabH * s, ColIcon);
    }

    /// <summary>
    /// ☰ Fixed ↔ Movable (как list menu ванильного title bar).
    /// Movable = можно таскать; Fixed = нельзя и сброс позиции в центр экрана.
    /// </summary>
    private bool TogglePinButton()
    {
        isMovable = !isMovable;
        isDragging = false;
        dragArmed = false;

        // Закрепить → вернуть окно в центр.
        if (!isMovable)
        {
            dialogOffsetX = 0;
            dialogOffsetY = 0;
            ApplyDialogOffset();
        }

        try
        {
            SingleComposer?.GetCustomDraw("dialogChrome")?.Redraw();
        }
        catch
        {
            // ignore
        }

        return true;
    }

    public override void OnMouseDown(MouseEvent args)
    {
        // Arm drag BEFORE base so Map/Claims buttons covering the tab strip don't block it.
        // Actual drag starts after a small movement threshold so tab clicks still work.
        dragArmed = false;
        isDragging = false;
        claimScrollDragging = false;
        memberScrollDragging = false;
        useFilterScrollDragging = false;

        if (isMovable
            && args.Button == EnumMouseButton.Left
            && SingleComposer?.Bounds != null
            && IsInTitleBarDragZone(args.X, args.Y))
        {
            dragArmed = true;
            dragStartMouseX = args.X;
            dragStartMouseY = args.Y;
            dragStartOffsetX = dialogOffsetX;
            dragStartOffsetY = dialogOffsetY;
        }

        if (activePage == PageClaims && args.Button == EnumMouseButton.Left)
        {
            if (TryBeginClaimScrollDrag(args.X, args.Y))
            {
                args.Handled = true;
                return;
            }

            if (claimsRightMode == ClaimsRightUseFilter && TryBeginUseFilterScrollDrag(args.X, args.Y))
            {
                args.Handled = true;
                return;
            }

            if (claimsRightMode != ClaimsRightUseFilter && TryBeginMemberScrollDrag(args.X, args.Y))
            {
                args.Handled = true;
                return;
            }
        }

        base.OnMouseDown(args);
    }

    public override void OnMouseMove(MouseEvent args)
    {
        if (claimScrollDragging)
        {
            UpdateClaimScrollDrag(args.Y);
            args.Handled = true;
            return;
        }

        if (useFilterScrollDragging)
        {
            UpdateUseFilterScrollDrag(args.Y);
            args.Handled = true;
            return;
        }

        if (memberScrollDragging)
        {
            UpdateMemberScrollDrag(args.Y);
            args.Handled = true;
            return;
        }

        if (dragArmed && isMovable && SingleComposer?.Bounds != null)
        {
            var dx = args.X - dragStartMouseX;
            var dy = args.Y - dragStartMouseY;

            if (!isDragging && (dx * dx + dy * dy) >= DragThresholdPx * DragThresholdPx)
            {
                isDragging = true;
            }

            if (isDragging)
            {
                var s = Math.Max(0.01, RuntimeEnv.GUIScale);
                dialogOffsetX = dragStartOffsetX + dx / s;
                dialogOffsetY = dragStartOffsetY + dy / s;
                ApplyDialogOffset();
                args.Handled = true;
            }
        }

        base.OnMouseMove(args);
    }

    public override void OnMouseUp(MouseEvent args)
    {
        dragArmed = false;
        isDragging = false;
        claimScrollDragging = false;
        memberScrollDragging = false;
        useFilterScrollDragging = false;
        base.OnMouseUp(args);
    }

    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
        if (activePage != PageClaims)
        {
            base.OnMouseWheel(args);
            return;
        }

        var mx = clientApi.Input.MouseX;
        var my = clientApi.Input.MouseY;
        var step = 40f;

        if (IsMouseOverRect(mx, my, ClaimsListX, ClaimsListY, ClaimsListW, ClaimsListH)
            || IsMouseOverRect(mx, my, ClaimsScrollX, ClaimsScrollY, ClaimsScrollW, ClaimsScrollH))
        {
            OnClaimListScroll(claimListScrollValue - args.delta * step);
            args.SetHandled(true);
            return;
        }

        if (claimsRightMode == ClaimsRightUseFilter
            && useFilterGrid != null
            && useFilterViewportBounds != null
            && (IsMouseOverElementBounds(mx, my, useFilterViewportBounds)
                || IsMouseOverUseFilterScrollTrack(mx, my)))
        {
            useFilterGrid.ScrollBy(-args.delta * step);
            useFilterScroll = useFilterGrid.ScrollOffset;
            SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();
            args.SetHandled(true);
            return;
        }

        if (claimsRightMode != ClaimsRightUseFilter
            && (IsMouseOverRect(mx, my, MembersX, MembersY, MembersW, MembersH)
                || IsMouseOverRect(mx, my, MembersScrollX, MembersScrollY, MembersScrollW, MembersScrollH)))
        {
            OnMemberListScrollCustom(memberListScrollValue - args.delta * step);
            args.SetHandled(true);
            return;
        }

        base.OnMouseWheel(args);
    }

    private bool IsMouseOverRect(int mouseX, int mouseY, int dx, int dy, int dw, int dh)
    {
        if (SingleComposer?.Bounds == null)
        {
            return false;
        }

        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var x = SingleComposer.Bounds.absX + dx * s;
        var y = SingleComposer.Bounds.absY + dy * s;
        return mouseX >= x && mouseX < x + dw * s && mouseY >= y && mouseY < y + dh * s;
    }

    private bool IsMouseOverElementBounds(int mouseX, int mouseY, ElementBounds bounds)
    {
        bounds.CalcWorldBounds();
        return mouseX >= bounds.absX
            && mouseX < bounds.absX + bounds.OuterWidth
            && mouseY >= bounds.absY
            && mouseY < bounds.absY + bounds.OuterHeight;
    }

    private bool TryBeginClaimScrollDrag(int mouseX, int mouseY)
    {
        if (SingleComposer?.Bounds == null)
        {
            return false;
        }

        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var originX = SingleComposer.Bounds.absX;
        var originY = SingleComposer.Bounds.absY;
        var trackX = originX + ClaimsScrollX * s;
        var trackY = originY + ClaimsScrollY * s;
        var trackW = ClaimsScrollW * s;
        var trackH = ClaimsScrollH * s;

        if (mouseX < trackX || mouseX >= trackX + trackW || mouseY < trackY || mouseY >= trackY + trackH)
        {
            return false;
        }

        GetClaimScrollThumbDesign(out var thumbDesignY, out var thumbDesignH);
        var thumbTop = originY + thumbDesignY * s;
        var thumbH = thumbDesignH * s;

        if (mouseY >= thumbTop && mouseY < thumbTop + thumbH)
        {
            claimScrollGrabOffsetY = mouseY - thumbTop;
        }
        else
        {
            claimScrollGrabOffsetY = thumbH * 0.5;
            UpdateClaimScrollDrag(mouseY);
        }

        claimScrollDragging = true;
        return true;
    }

    private bool TryBeginMemberScrollDrag(int mouseX, int mouseY)
    {
        if (SingleComposer?.Bounds == null)
        {
            return false;
        }

        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var originX = SingleComposer.Bounds.absX;
        var originY = SingleComposer.Bounds.absY;
        var trackX = originX + MembersScrollX * s;
        var trackY = originY + MembersScrollY * s;
        var trackW = MembersScrollW * s;
        var trackH = MembersScrollH * s;

        if (mouseX < trackX || mouseX >= trackX + trackW || mouseY < trackY || mouseY >= trackY + trackH)
        {
            return false;
        }

        GetMemberScrollThumbDesign(out var thumbDesignY, out var thumbDesignH);
        var thumbTop = originY + thumbDesignY * s;
        var thumbH = thumbDesignH * s;

        if (mouseY >= thumbTop && mouseY < thumbTop + thumbH)
        {
            memberScrollGrabOffsetY = mouseY - thumbTop;
        }
        else
        {
            memberScrollGrabOffsetY = thumbH * 0.5;
            UpdateMemberScrollDrag(mouseY);
        }

        memberScrollDragging = true;
        return true;
    }

    private bool TryBeginUseFilterScrollDrag(int mouseX, int mouseY)
    {
        if (SingleComposer?.Bounds == null
            || useFilterGrid == null
            || useFilterViewportBounds == null
            || useFilterGrid.MaxScroll <= 0.01f)
        {
            return false;
        }

        if (!IsMouseOverUseFilterScrollTrack(mouseX, mouseY))
        {
            return false;
        }

        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var originY = SingleComposer.Bounds.absY;
        GetUseFilterScrollThumbDesign(out var thumbDesignY, out var thumbDesignH);
        var thumbTop = originY + thumbDesignY * s;
        var thumbH = thumbDesignH * s;

        if (mouseY >= thumbTop && mouseY < thumbTop + thumbH)
        {
            useFilterScrollGrabOffsetY = mouseY - thumbTop;
        }
        else
        {
            useFilterScrollGrabOffsetY = thumbH * 0.5;
            UpdateUseFilterScrollDrag(mouseY);
        }

        useFilterScrollDragging = true;
        return true;
    }

    private bool IsMouseOverUseFilterScrollTrack(int mouseX, int mouseY)
    {
        if (SingleComposer?.Bounds == null || useFilterViewportBounds == null)
        {
            return false;
        }

        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var originX = SingleComposer.Bounds.absX;
        var originY = SingleComposer.Bounds.absY;
        var trackX = originX + MembersScrollX * s;
        var trackY = originY + useFilterViewportBounds.fixedY * s;
        var trackW = MembersScrollW * s;
        var trackH = useFilterViewportBounds.fixedHeight * s;

        return mouseX >= trackX
            && mouseX < trackX + trackW
            && mouseY >= trackY
            && mouseY < trackY + trackH;
    }

    private void UpdateUseFilterScrollDrag(int mouseY)
    {
        if (SingleComposer?.Bounds == null
            || useFilterGrid == null
            || useFilterViewportBounds == null)
        {
            return;
        }

        var maxScroll = useFilterGrid.MaxScroll;
        if (maxScroll <= 0.01f)
        {
            return;
        }

        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var originY = SingleComposer.Bounds.absY;
        var trackY = originY + useFilterViewportBounds.fixedY * s;
        var trackH = useFilterViewportBounds.fixedHeight * s;

        GetUseFilterScrollThumbDesign(out _, out var thumbDesignH);
        var thumbH = thumbDesignH * s;
        var travel = Math.Max(1.0, trackH - thumbH);
        var thumbTop = mouseY - useFilterScrollGrabOffsetY;
        var t = Math.Clamp((thumbTop - trackY) / travel, 0, 1);
        useFilterGrid.ScrollOffset = (float)(t * maxScroll);
        useFilterScroll = useFilterGrid.ScrollOffset;
        SingleComposer.GetCustomDraw("claimsPageChrome")?.Redraw();
    }

    private void UpdateClaimScrollDrag(int mouseY)
    {
        GetClaimListScrollMetrics(out _, out _, out var maxScroll);
        if (maxScroll <= 0)
        {
            return;
        }

        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var originY = SingleComposer!.Bounds.absY;
        var trackY = originY + ClaimsScrollY * s;
        var trackH = ClaimsScrollH * s;

        GetClaimScrollThumbDesign(out _, out var thumbDesignH);
        var thumbH = thumbDesignH * s;
        var travel = Math.Max(1.0, trackH - thumbH);
        var thumbTop = mouseY - claimScrollGrabOffsetY;
        var t = Math.Clamp((thumbTop - trackY) / travel, 0, 1);
        OnClaimListScroll((float)(t * maxScroll));
    }

    private void UpdateMemberScrollDrag(int mouseY)
    {
        GetMemberListScrollMetrics(out _, out _, out var maxScroll);
        if (maxScroll <= 0)
        {
            return;
        }

        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var originY = SingleComposer!.Bounds.absY;
        var trackY = originY + MembersScrollY * s;
        var trackH = MembersScrollH * s;

        GetMemberScrollThumbDesign(out _, out var thumbDesignH);
        var thumbH = thumbDesignH * s;
        var travel = Math.Max(1.0, trackH - thumbH);
        var thumbTop = mouseY - memberScrollGrabOffsetY;
        var t = Math.Clamp((thumbTop - trackY) / travel, 0, 1);
        OnMemberListScrollCustom((float)(t * maxScroll));
    }

    /// <summary>Верхняя полоса окна (табы), кроме кнопок ☰ и ✕.</summary>
    private bool IsInTitleBarDragZone(int mouseX, int mouseY)
    {
        var b = SingleComposer?.Bounds;
        if (b == null)
        {
            return false;
        }

        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        if (mouseX < b.absX || mouseX >= b.absX + b.OuterWidth
            || mouseY < b.absY || mouseY >= b.absY + TabH * s)
        {
            return false;
        }

        // ☰ / ✕ — свои кнопки, не зона drag.
        var pinLeft = b.absX + PinBtnX * s;
        var closeRight = b.absX + (CloseBtnX + CloseBtnW) * s;
        if (mouseX >= pinLeft && mouseX < closeRight)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Moves the dialog without re-rasterizing Cairo textures (MarkDirtyRecursive was
    /// shredding the frame PNG every mouse-move while dragging).
    /// </summary>
    private void ApplyDialogOffset()
    {
        if (SingleComposer?.Bounds == null)
        {
            return;
        }

        var b = SingleComposer.Bounds;
        b.fixedOffsetX = dialogOffsetX;
        b.fixedOffsetY = dialogOffsetY;
        RecalcBoundsTree(b);
    }

    private static void RecalcBoundsTree(ElementBounds bounds)
    {
        bounds.CalcWorldBounds();
        if (bounds.ChildBounds == null)
        {
            return;
        }

        foreach (var child in bounds.ChildBounds)
        {
            RecalcBoundsTree(child);
        }
    }

    /// <summary>Общий размер иконки вкладки (☰ / ✕) — один квадрат в hit-area.</summary>
    private static double GetTabActionIconSize(double w, double h) => Math.Min(w, h) * 0.38;

    /// <summary>Крестик закрытия — Cairo, цвет #9F795B как на SVG.</summary>
    private static void DrawCloseIcon(Context ctx, double x, double y, double w, double h, double[] color)
    {
        var size = GetTabActionIconSize(w, h);
        var cx = x + w * 0.5;
        var cy = y + h * 0.5;
        var half = size * 0.5;
        var thickness = Math.Max(2.5, size * 0.18);

        ctx.Save();
        ctx.SetSourceRGBA(color[0], color[1], color[2], color.Length > 3 ? color[3] : 1.0);
        ctx.LineWidth = thickness;
        ctx.LineCap = LineCap.Square;
        ctx.MoveTo(cx - half, cy - half);
        ctx.LineTo(cx + half, cy + half);
        ctx.Stroke();
        ctx.MoveTo(cx + half, cy - half);
        ctx.LineTo(cx - half, cy + half);
        ctx.Stroke();
        ctx.Restore();
    }

    /// <summary>
    /// ☰ pin/detach: 3 bars in the same square as the close ✕ (equal size, even spacing).
    /// </summary>
    private static void DrawMenuIcon(Context ctx, double x, double y, double w, double h, double[] color)
    {
        // Match close icon bounding square so both glyphs align on the action plate.
        var size = GetTabActionIconSize(w, h);
        var thickness = Math.Max(2.5, size * 0.18);
        var cx = x + w * 0.5;
        var cy = y + h * 0.5;
        var left = cx - size * 0.5;
        // Centers of top/mid/bottom bars evenly fill the square (outer edges = size).
        var span = size - thickness; // first center → last center
        var gap = span * 0.5;        // three lines: -gap, 0, +gap

        ctx.Save();
        ctx.SetSourceRGBA(color[0], color[1], color[2], color.Length > 3 ? color[3] : 1.0);
        ctx.LineWidth = thickness;
        ctx.LineCap = LineCap.Square;
        for (var i = -1; i <= 1; i++)
        {
            var ly = cy + i * gap;
            ctx.MoveTo(left, ly);
            ctx.LineTo(left + size, ly);
            ctx.Stroke();
        }

        ctx.Restore();
    }

    /// <summary>
    /// Claims tab chrome: CLAIMS / SETTINGS section headers + SETTINGS plate (claims.svg).
    /// Surface origin = PanelX/PanelY.
    /// </summary>
    private void DrawClaimsPageChrome(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        double Xd(double abs) => (abs - PanelX) * s;
        double Yd(double abs) => (abs - PanelY) * s;

        // Section headers: centered under each dashed band (ПРИВАТЫ left, НАСТРОЙКИ right).
        DrawSectionHeader(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-section-claims").ToUpperInvariant(),
            Xd(SectionDashLeftX1),
            Xd(SectionDashLeftX2),
            Yd(SectionTitleBaselineY),
            Yd(SectionDashY),
            s);
        DrawSectionHeader(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-section-settings").ToUpperInvariant(),
            Xd(SectionDashRightX1),
            Xd(SectionDashRightX2),
            Yd(SectionTitleBaselineY),
            Yd(SectionDashY),
            s);

        // SETTINGS plate (Group 468) + rename/add buttons (Group 469) — not in use-filter view.
        if (claimsRightMode != ClaimsRightUseFilter)
        {
            var settingsTexX = Xd(SettingsTexX);
            var settingsTexY = Yd(SettingsTexY);
            var settingsTexW = SettingsTexW * s;
            var settingsTexH = SettingsTexH * s;
            DrawSettingsPanelTexture(ctx, settingsTexX, settingsTexY, settingsTexW, settingsTexH);
            DrawSettingsButtonTexture(ctx, Xd(SettingsLeftFieldX), Yd(SettingsBtnY), SettingsFieldW * s, SettingsBtnH * s);
            DrawSettingsButtonTexture(ctx, Xd(SettingsRightFieldX), Yd(SettingsBtnY), SettingsFieldW * s, SettingsBtnH * s);
            DrawSettingsPlateTexts(ctx, settingsTexX, settingsTexY, settingsTexW, settingsTexH);
        }

        // Gear next to SETTINGS title (right side of section header band).
        ClaimCairoIcons.DrawGear(
            ctx,
            Xd(SettingsGearX),
            Yd(SettingsGearY),
            SettingsGearSize * s,
            active: claimsRightMode == ClaimsRightUseFilter,
            locked: false);

        // Thin scrolls: claims list (left) + members list (settings) or use-filter grid.
        DrawThinScrollBar(
            ctx,
            Xd(ClaimsScrollX),
            Yd(ClaimsScrollY),
            ClaimsScrollW * s,
            ClaimsScrollH * s,
            s,
            ClaimsScrollY,
            GetClaimScrollThumbDesign);
        if (claimsRightMode == ClaimsRightUseFilter
            && useFilterViewportBounds != null
            && useFilterGrid != null)
        {
            var fy = (int)Math.Round(useFilterViewportBounds.fixedY);
            var fh = (int)Math.Round(useFilterViewportBounds.fixedHeight);
            DrawThinScrollBar(
                ctx,
                Xd(MembersScrollX),
                Yd(fy),
                MembersScrollW * s,
                fh * s,
                s,
                fy,
                GetUseFilterScrollThumbDesign);
        }
        else if (claimsRightMode != ClaimsRightUseFilter)
        {
            DrawThinScrollBar(
                ctx,
                Xd(MembersScrollX),
                Yd(MembersScrollY),
                MembersScrollW * s,
                MembersScrollH * s,
                s,
                MembersScrollY,
                GetMemberScrollThumbDesign);
        }

        // Separator under stats (SVG line y=227.5) — only in settings view.
        if (claimsRightMode != ClaimsRightUseFilter)
        {
            var lineY = Yd(227.5);
            ctx.Save();
            ctx.SetSourceRGBA(0x83 / 255.0, 0x66 / 255.0, 0x50 / 255.0, 0.64);
            ctx.LineWidth = Math.Max(1.0, 2 * s);
            ctx.LineCap = LineCap.Round;
            ctx.MoveTo(Xd(474.5), lineY);
            ctx.LineTo(Xd(888.5), lineY);
            ctx.Stroke();
            ctx.Restore();
        }
    }

    private delegate void ThumbDesignGetter(out double thumbY, out double thumbH);

    /// <summary>
    /// Thin scroll: Rectangle 758 track + wood thumb. x/y/w/h already panel-scaled.
    /// </summary>
    private void DrawThinScrollBar(
        Context ctx,
        double x,
        double y,
        double w,
        double h,
        double s,
        int trackDesignY,
        ThumbDesignGetter getThumb)
    {
        EnsureFrameSurface();

        if (scrollTrackSurface != null && scrollTrackSurface.Width > 0 && scrollTrackSurface.Height > 0)
        {
            DrawGuiTexture(ctx, scrollTrackSurface, x, y, w, h, fallback: ColScrollTrack);
        }
        else
        {
            ctx.SetSourceRGB(ColScrollTrack[0], ColScrollTrack[1], ColScrollTrack[2]);
            ctx.Rectangle(x, y, w, h);
            ctx.Fill();
        }

        getThumb(out var thumbDesignY, out var thumbDesignH);
        var thumbY = y + (thumbDesignY - trackDesignY) * s;
        var thumbH = thumbDesignH * s;
        var inset = Math.Max(0.5, 1 * s);
        ctx.SetSourceRGB(ColScrollThumb[0], ColScrollThumb[1], ColScrollThumb[2]);
        ctx.Rectangle(x + inset, thumbY, Math.Max(1, w - inset * 2), thumbH);
        ctx.Fill();
    }

    /// <summary>
    /// Весь контент вкладки Map внутри panel (42,118 885×542).
    /// Surface origin = PanelX/PanelY. Координаты = SVG local − panel.
    /// </summary>
    private void DrawMapPageContent(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        double X(int abs) => (abs - PanelX) * s;
        double Y(int abs) => (abs - PanelY) * s;

        // ----- Interactive map well: SVG map (458,192) 447×446 + 2px #121212 -----
        var mx = X(MapX);
        var my = Y(MapY);
        var mw = MapW * s;
        var mh = MapH * s;
        var b = MapBorder * s;
        SetRgb(ctx, ColEdge);
        ctx.Rectangle(mx - b, my - b, mw + 2 * b, mh + 2 * b);
        ctx.Fill();
        // dark fill under grid (shows until tiles load)
        ctx.SetSourceRGB(0.05, 0.05, 0.055);
        ctx.Rectangle(mx, my, mw, mh);
        ctx.Fill();

        // ----- Section headers: exact Map (3).svg positions -----
        // X/Y helpers take dialog-local design coords (SVG − frame origin).
        double Xd(double abs) => (abs - PanelX) * s;
        double Yd(double abs) => (abs - PanelY) * s;

        // Map tab sections — same size/color as claims; center under dashes (map SVG).
        DrawSectionHeader(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-tab-map").ToUpperInvariant(),
            Xd(SectionDashLeftX1),
            Xd(SectionDashLeftX2),
            Yd(SectionTitleBaselineY),
            Yd(SectionDashY),
            s);
        DrawSectionHeader(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-section-interactive").ToUpperInvariant(),
            Xd(SectionDashRightX1),
            Xd(SectionDashRightX2),
            Yd(SectionTitleBaselineY),
            Yd(SectionDashY),
            s);

        // ----- Left column: Limits + Legend + Center (origins from Map (3).svg) -----
        // Full PNGs include 6px outer chrome; place at texture origin, not face.
        var limitsW = LimitsTexW * s;
        var limitsH = LimitsTexH * s;
        var legendW = LegendTexW * s;
        var legendH = LegendTexH * s;
        var centerW = CenterTexW * s;
        var centerH = CenterTexH * s;

        DrawLimitsPanelTexture(ctx, X(LimitsX), Y(LimitsY), limitsW, limitsH);
        DrawLegendPanelTexture(ctx, X(LegendX), Y(LegendY), legendW, legendH);
        DrawCenterButtonTexture(ctx, X(CenterX), Y(CenterY), centerW, centerH);

        var textX = X(CardX) + CardPadX * s;
        var textW = (CardW - CardPadX * 2) * s;

        // Limits / legend text — design coords relative to texture origin
        DrawLimitsTexts(ctx, X(LimitsX), Y(LimitsY), limitsW, limitsH);
        DrawLegendTexts(ctx, X(LegendX), Y(LegendY), legendW, legendH);

        // Claim result / working message on wood between legend and center.
        var msg = mapStatusOverride ?? mapState?.Message ?? "";
        if (!string.IsNullOrWhiteSpace(msg))
        {
            DrawMultilineSurface(ctx, msg, textX, Y(MessageY), textW, FontBody, ClaimFontHelper.ColorAccent, 18 * s);
        }

        // Center label (over Group 464) — «ЦЕНТР» UPPERCASE Montserrat Bold.
        DrawCenteredLabelSurface(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-center").ToUpperInvariant(),
            X(CenterX), Y(CenterY), centerW, centerH,
            FontCenter,
            ClaimFontHelper.ColorCream);
    }

    /// <summary>SVG inset card: face #412D1D, top/left #563E2B 3px, bottom/right #2A1E14 3px.</summary>
    private static void DrawSvgCard(Context ctx, double x, double y, double w, double h, double s)
    {
        SetRgb(ctx, ColInset);
        ctx.Rectangle(x, y, w, h);
        ctx.Fill();

        SetRgb(ctx, ColHi);
        ctx.Rectangle(x, y, w, 3 * s);
        ctx.Fill();
        ctx.Rectangle(x, y, 3 * s, h);
        ctx.Fill();

        SetRgb(ctx, ColLo);
        ctx.Rectangle(x, y + h - 3 * s, w, 3 * s);
        ctx.Fill();
        ctx.Rectangle(x + w - 3 * s, y, 3 * s, h);
        ctx.Fill();
    }

    /// <summary>Плашка лимитов Group 466.png.</summary>
    private void DrawLimitsPanelTexture(Context ctx, double x, double y, double w, double h)
    {
        DrawGuiTexture(ctx, limitsPanelSurface, x, y, w, h, fallback: ColInset);
    }

    /// <summary>Плашка легенды Group 387.svg / Group 387 (1).png.</summary>
    private void DrawLegendPanelTexture(Context ctx, double x, double y, double w, double h)
    {
        DrawGuiTexture(ctx, legendPanelSurface, x, y, w, h, fallback: ColInset);
    }

    /// <summary>Плашка настроек Group 468.png (rename / add player).</summary>
    private void DrawSettingsPanelTexture(Context ctx, double x, double y, double w, double h)
    {
        DrawGuiTexture(ctx, settingsPanelSurface, x, y, w, h, fallback: ColInset);
    }

    /// <summary>Кнопки rename/add Group 469.png.</summary>
    private void DrawSettingsButtonTexture(Context ctx, double x, double y, double w, double h)
    {
        DrawGuiTexture(ctx, settingsButtonSurface, x, y, w, h, fallback: ColCenter);
    }

    /// <summary>
    /// Group 468.svg text: stats #836650@0.64 baseline 28; labels #D29F78 baseline 66;
    /// buttons #9F795B baseline 143 centered on each Group 469 plate.
    /// </summary>
    private void DrawSettingsPlateTexts(Context ctx, double texX, double texY, double texW, double texH)
    {
        var sx = texW / SettingsTexW;
        var sy = texH / SettingsTexH;

        var claim = GetSelectedClaim();
        var stats = "";
        if (claim != null)
        {
            stats = (claim.ViewerIsCoOwner
                    ? Lang.Get(
                        "swixyclaimchunk:claims-stats-coowner",
                        claim.OwnerName,
                        claim.AreaCount,
                        claim.ChunkCount)
                    : Lang.Get(
                        "swixyclaimchunk:claims-stats",
                        claim.AreaCount,
                        claim.ChunkCount))
                .ToUpperInvariant();
        }

        if (!string.IsNullOrEmpty(stats))
        {
            DrawTextAtBaseline(
                ctx,
                stats,
                texX + SettingsStatsTextX * sx,
                texY + SettingsStatsBaselineY * sy,
                FontSettingsStats,
                ColSettingsStats);
        }

        DrawTextAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claims-rename").ToUpperInvariant(),
            texX + SettingsLabelLeftX * sx,
            texY + SettingsLabelBaselineY * sy,
            FontSettingsLabel,
            ClaimFontHelper.ColorAccent);

        DrawTextAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claims-player-name").ToUpperInvariant(),
            texX + SettingsLabelRightX * sx,
            texY + SettingsLabelBaselineY * sy,
            FontSettingsLabel,
            ClaimFontHelper.ColorAccent);

        // Button captions — horizontal center at SVG midpoints, baseline 143.
        DrawTextCenteredAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claims-rename-button").ToUpperInvariant(),
            texX + SettingsBtnLeftCenterX * sx,
            texY + SettingsBtnTextBaselineY * sy,
            FontSettingsBtn,
            ColSettingsBtn);

        DrawTextCenteredAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claims-add-player").ToUpperInvariant(),
            texX + SettingsBtnRightCenterX * sx,
            texY + SettingsBtnTextBaselineY * sy,
            FontSettingsBtn,
            ColSettingsBtn);
    }

    private static void DrawTextCenteredAtBaseline(
        Context ctx,
        string text,
        double centerX,
        double baselineY,
        double designFontSize,
        double[] color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ClaimFontHelper.SetupMontserrat(ctx, designFontSize, color, bold: true);
        var extents = ctx.TextExtents(text);
        ctx.MoveTo(centerX - extents.Width * 0.5 - extents.XBearing, baselineY);
        ctx.ShowText(text);
    }

    /// <summary>Кнопка «К игроку» из Group 464.png (button_center.png).</summary>
    private void DrawCenterButtonTexture(Context ctx, double x, double y, double w, double h)
    {
        DrawGuiTexture(ctx, centerButtonSurface, x, y, w, h, fallback: ColCenter);
    }

    private static void DrawGuiTexture(
        Context ctx,
        ImageSurface? tex,
        double x,
        double y,
        double w,
        double h,
        double[] fallback)
    {
        if (tex != null && tex.Width > 0 && tex.Height > 0)
        {
            ctx.Save();
            ctx.Translate(x, y);
            ctx.Scale(w / tex.Width, h / tex.Height);
            ctx.SetSourceSurface(tex, 0, 0);
            if (ctx.GetSource() is SurfacePattern pattern)
            {
                pattern.Filter = Filter.Nearest;
            }

            ctx.Paint();
            ctx.Restore();
            return;
        }

        SetRgb(ctx, fallback);
        ctx.Rectangle(x, y, w, h);
        ctx.Fill();
    }

    /// <summary>
    /// Limits text (UPPERCASE, Montserrat Bold): one left column + right-aligned values.
    /// </summary>
    private void DrawLimitsTexts(Context ctx, double panelX, double panelY, double panelW, double panelH)
    {
        var sx = panelW / LimitsTexW;
        var sy = panelH / LimitsTexH;
        var leftX = panelX + LimitsTextLeftX * sx;

        DrawTextAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-used").ToUpperInvariant(),
            leftX,
            panelY + LimitsTitleBaselineY * sy,
            FontLimitsTitle,
            ColLimitsTitle);

        GetLimitsValues(mapState, out var chunksUsed, out var chunksMax, out var areasUsed, out var areasMax);

        var chunksLabel = Lang.Get("swixyclaimchunk:claim-map-chunks-label").ToUpperInvariant();
        var areasLabel = Lang.Get("swixyclaimchunk:claim-map-areas-label").ToUpperInvariant();
        var chunksVal = Lang.Get("swixyclaimchunk:claim-map-value-ratio", chunksUsed, chunksMax).ToUpperInvariant();
        var areasVal = Lang.Get("swixyclaimchunk:claim-map-value-ratio", areasUsed, areasMax).ToUpperInvariant();

        var valueRight = panelX + LimitsValueRightX * sx;
        var y1 = panelY + LimitsLine1BaselineY * sy;
        var y2 = panelY + LimitsLine2BaselineY * sy;

        DrawTextAtBaseline(ctx, chunksLabel, leftX, y1, FontLimitsBody, ClaimFontHelper.ColorAccent);
        DrawTextRightAtBaseline(ctx, chunksVal, valueRight, y1, FontLimitsBody, ClaimFontHelper.ColorAccent);
        DrawTextAtBaseline(ctx, areasLabel, leftX, y2, FontLimitsBody, ClaimFontHelper.ColorAccent);
        DrawTextRightAtBaseline(ctx, areasVal, valueRight, y2, FontLimitsBody, ClaimFontHelper.ColorAccent);
    }

    /// <summary>
    /// Legend text (UPPERCASE): title + Free / Yours / Other, shared left margin.
    /// </summary>
    private void DrawLegendTexts(Context ctx, double panelX, double panelY, double panelW, double panelH)
    {
        var sx = panelW / LegendTexW;
        var sy = panelH / LegendTexH;
        var leftX = panelX + LegendTextLeftX * sx;
        var maxW = (LegendTextMaxRightX - LegendTextLeftX) * sx;

        DrawTextAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-legend-title").ToUpperInvariant(),
            leftX,
            panelY + LegendTitleBaselineY * sy,
            FontLegendTitle,
            ColLimitsTitle);

        DrawTextAtBaselineClamped(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-legend-free").ToUpperInvariant(),
            leftX,
            panelY + LegendLine1BaselineY * sy,
            maxW,
            FontLegendBody,
            ClaimFontHelper.ColorAccent);
        DrawTextAtBaselineClamped(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-legend-own").ToUpperInvariant(),
            leftX,
            panelY + LegendLine2BaselineY * sy,
            maxW,
            FontLegendBody,
            ClaimFontHelper.ColorAccent);
        DrawTextAtBaselineClamped(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-legend-other").ToUpperInvariant(),
            leftX,
            panelY + LegendLine3BaselineY * sy,
            maxW,
            FontLegendBody,
            ClaimFontHelper.ColorAccent);
    }

    /// <summary>Baseline text; truncates with … if wider than maxWidth. Montserrat Bold.</summary>
    private static void DrawTextAtBaselineClamped(
        Context ctx,
        string text,
        double x,
        double baselineY,
        double maxWidth,
        double designFontSize,
        double[] color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ClaimFontHelper.SetupMontserrat(ctx, designFontSize, color, bold: true);
        var draw = text;
        var extents = ctx.TextExtents(draw);
        if (extents.Width > maxWidth && maxWidth > 8)
        {
            while (draw.Length > 1 && ctx.TextExtents(draw + "…").Width > maxWidth)
            {
                draw = draw[..^1];
            }

            draw += "…";
            extents = ctx.TextExtents(draw);
        }

        ctx.MoveTo(x - extents.XBearing, baselineY);
        ctx.ShowText(draw);
    }

    private static void GetLimitsValues(
        ClaimMapStatePacket? packet,
        out string chunksUsed,
        out string chunksMax,
        out string areasUsed,
        out string areasMax)
    {
        if (packet == null)
        {
            chunksUsed = "—";
            chunksMax = "—";
            areasUsed = "—";
            areasMax = "—";
            return;
        }

        var chunkSize = packet.ChunkSize > 0 ? packet.ChunkSize : GlobalConstants.ChunkSize;
        var mapSizeY = packet.MapSizeY > 0 ? packet.MapSizeY : 256;
        var usedChunks = ClaimVolumeUtil.BlocksToChunkCount(packet.UsedVolume, chunkSize, mapSizeY);
        var maxChunks = packet.MaxVolume > 0
            ? ClaimVolumeUtil.BlocksToChunkCount(packet.MaxVolume, chunkSize, mapSizeY)
            : 0;
        chunksUsed = usedChunks.ToString();
        chunksMax = maxChunks > 0 ? maxChunks.ToString() : Lang.Get("swixyclaimchunk:claim-map-unlimited");
        areasUsed = packet.UsedAreas.ToString();
        areasMax = packet.MaxAreas > 0 ? packet.MaxAreas.ToString() : Lang.Get("swixyclaimchunk:claim-map-unlimited");
    }

    /// <summary>Текст с привязкой к baseline Y (как path M в SVG) — Montserrat Bold.</summary>
    private static void DrawTextAtBaseline(
        Context ctx,
        string text,
        double x,
        double baselineY,
        double designFontSize,
        double[] color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ClaimFontHelper.SetupMontserrat(ctx, designFontSize, color, bold: true);
        var extents = ctx.TextExtents(text);
        ctx.MoveTo(x - extents.XBearing, baselineY);
        ctx.ShowText(text);
    }

    /// <summary>
    /// Section title + dashed rule: Montserrat Bold #836750, centered in [leftX, rightX].
    /// </summary>
    private static void DrawSectionHeader(
        Context ctx,
        string text,
        double leftX,
        double rightX,
        double baselineY,
        double dashY,
        double s)
    {
        if (!string.IsNullOrEmpty(text))
        {
            ClaimFontHelper.SetupMontserrat(ctx, FontSection, ColSection, bold: true);
            var extents = ctx.TextExtents(text);
            var x = leftX + (rightX - leftX - extents.Width) * 0.5 - extents.XBearing;
            ctx.MoveTo(x, baselineY);
            ctx.ShowText(text);
        }

        DrawDashedLine(
            ctx,
            leftX,
            dashY,
            rightX,
            dashY,
            SectionDashWidth * s,
            SectionDashOn * s,
            SectionDashOff * s,
            ColSectionDash);
    }

    /// <summary>
    /// Section title left-aligned at SVG path start X (Group 471 CLAIMS / SETTINGS) — Montserrat Bold.
    /// </summary>
    private static void DrawSectionHeaderAt(
        Context ctx,
        string text,
        double textX,
        double dashLeftX,
        double dashRightX,
        double baselineY,
        double dashY,
        double s)
    {
        if (!string.IsNullOrEmpty(text))
        {
            ClaimFontHelper.SetupMontserrat(ctx, FontSection, ColSection, bold: true);
            var extents = ctx.TextExtents(text);
            // textX is left edge of first glyph in SVG path.
            ctx.MoveTo(textX - extents.XBearing, baselineY);
            ctx.ShowText(text);
        }

        DrawDashedLine(
            ctx,
            dashLeftX,
            dashY,
            dashRightX,
            dashY,
            SectionDashWidth * s,
            SectionDashOn * s,
            SectionDashOff * s,
            ColSectionDash);
    }

    /// <summary>
    /// Пунктир Map (3).svg: stroke #836650 @ 0.16, width 4, dasharray 8 8.
    /// Color is pre-blended onto panel wood (see <see cref="ColSectionDash"/>).
    /// </summary>
    private static void DrawDashedLine(
        Context ctx,
        double x1,
        double y1,
        double x2,
        double y2,
        double lineWidth,
        double dashOn,
        double dashOff,
        double[] color)
    {
        ctx.Save();
        ctx.Operator = Operator.Over;
        ctx.Antialias = Antialias.None; // crisp segments like pixel UI / Nearest textures
        ctx.SetSourceRGBA(color[0], color[1], color[2], color.Length > 3 ? color[3] : 1.0);
        ctx.LineWidth = Math.Max(1.0, lineWidth);
        ctx.LineCap = LineCap.Butt;
        ctx.LineJoin = LineJoin.Miter;
        ctx.SetDash([Math.Max(1.0, dashOn), Math.Max(1.0, dashOff)], 0);
        ctx.MoveTo(x1, y1);
        ctx.LineTo(x2, y2);
        ctx.Stroke();
        ctx.SetDash([], 0);
        ctx.Restore();
    }

    /// <summary>Правый край текста = rightX (right-aligned values в Group 466).</summary>
    private static void DrawTextRightAtBaseline(
        Context ctx,
        string text,
        double rightX,
        double baselineY,
        double designFontSize,
        double[] color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ClaimFontHelper.SetupMontserrat(ctx, designFontSize, color, bold: true);
        var extents = ctx.TextExtents(text);
        var x = rightX - extents.Width - extents.XBearing;
        ctx.MoveTo(x, baselineY);
        ctx.ShowText(text);
    }

    private void RedrawMapSideColumn()
    {
        try
        {
            SingleComposer?.GetCustomDraw("mapPageContent")?.Redraw();
        }
        catch
        {
            // Element may be missing on claims page.
        }
    }

    private static void SetRgb(Context ctx, double[] rgb)
    {
        ctx.SetSourceRGB(rgb[0], rgb[1], rgb[2]);
    }

    private static void DrawTextSurface(Context ctx, string text, double x, double y, double designFontSize, double[] color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ClaimFontHelper.SetupMontserrat(ctx, designFontSize, color, bold: true);
        // y is top of text box; convert to baseline via extents.
        var extents = ctx.TextExtents(text);
        ctx.MoveTo(x - extents.XBearing, y - extents.YBearing);
        ctx.ShowText(text);
    }

    private static void DrawMultilineSurface(
        Context ctx,
        string text,
        double x,
        double y,
        double maxWidth,
        double designFontSize,
        double[] color,
        double lineHeight)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ClaimFontHelper.SetupMontserrat(ctx, designFontSize, color, bold: true);

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var cy = y;
        foreach (var line in lines)
        {
            var extents = ctx.TextExtents(line);
            ctx.MoveTo(x - extents.XBearing, cy - extents.YBearing);
            ctx.ShowText(line);
            cy += lineHeight;
        }
    }

    private static void DrawCenteredLabelSurface(
        Context ctx,
        string text,
        double x,
        double y,
        double width,
        double height,
        double designFontSize,
        double[] color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ClaimFontHelper.SetupMontserrat(ctx, designFontSize, color, bold: true);
        var extents = ctx.TextExtents(text);
        var fe = ctx.FontExtents;
        // Horizontal + vertical center via baseline (ascent/descent), not YBearing alone.
        var tx = x + (width - extents.Width) * 0.5 - extents.XBearing;
        var ty = y + (height - fe.Height) * 0.5 + fe.Ascent;
        ctx.MoveTo(tx, ty);
        ctx.ShowText(text);
    }

    /// <summary>
    /// Tab plate label: Montserrat Bold, truly centered, slight downward nudge (wood plate optics).
    /// </summary>
    private static void DrawTabLabel(
        Context ctx,
        string text,
        double x,
        double y,
        double width,
        double height,
        double designFontSize,
        double[] color,
        double s,
        double nudgeXDesign = 0)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ClaimFontHelper.SetupMontserrat(ctx, designFontSize, color, bold: true);
        var extents = ctx.TextExtents(text);
        var fe = ctx.FontExtents;
        // Optical center: baseline mid of ascent/descent box, then nudge down on wood plate.
        const double nudgeDownDesign = 9;
        var tx = x + (width - extents.Width) * 0.5 - extents.XBearing + nudgeXDesign * s;
        var ty = y + (height - fe.Height) * 0.5 + fe.Ascent + nudgeDownDesign * s;
        ctx.MoveTo(tx, ty);
        ctx.ShowText(text);
    }

    /// <summary>Карточка #412D1D с фасками SVG (Claims page panels).</summary>
    private static void DrawInsetCard(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        DrawSvgCard(ctx, 0, 0, bounds.OuterWidth, bounds.OuterHeight, s);
    }

    /// <summary>Фон полей ввода на вкладке приватов.</summary>
    private static void DrawTextInputBackground(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var width = bounds.OuterWidth;
        var height = bounds.OuterHeight;

        ctx.SetSourceRGB(0.255, 0.176, 0.114); // #412D1D
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();

        ctx.SetSourceRGBA(0.827, 0.624, 0.471, 0.55); // #D29F78
        ctx.LineWidth = 1.5;
        ctx.Rectangle(1, 1, width - 2, height - 2);
        ctx.Stroke();
    }

    /// <summary>
    /// Чип флага: плашка + квадратный чекбокс.
    /// Выкл — тёмный квадрат; вкл — зелёный с галочкой.
    /// </summary>
    private static void DrawFlagChipWithCheckbox(Context ctx, ElementBounds bounds, bool isOn)
    {
        var width = bounds.OuterWidth;
        var height = bounds.OuterHeight;
        const double r = 4;
        const double pad = 8;
        const double box = 22;
        var boxY = (height - box) * 0.5;

        // Chip plate
        ctx.SetSourceRGB(0.255, 0.176, 0.114); // #412D1D
        RoundRectangle(ctx, 0, 0, width, height, r);
        ctx.Fill();

        ctx.SetSourceRGBA(0.827, 0.624, 0.471, 0.7); // #D29F78
        ctx.LineWidth = 1.5;
        RoundRectangle(ctx, 1, 1, width - 2, height - 2, r);
        ctx.Stroke();

        ctx.SetSourceRGBA(1, 1, 1, 0.06);
        RoundRectangle(ctx, 2, 2, width - 4, height * 0.45, r - 1);
        ctx.Fill();

        // Checkbox square — always a clear dark well so the off state is obvious.
        const double br = 3;
        ctx.SetSourceRGB(0.08, 0.06, 0.04); // near-black well
        RoundRectangle(ctx, pad, boxY, box, box, br);
        ctx.Fill();

        if (isOn)
        {
            // Filled active state
            ctx.SetSourceRGB(0.22, 0.48, 0.26);
            RoundRectangle(ctx, pad + 2, boxY + 2, box - 4, box - 4, br - 1);
            ctx.Fill();

            // Checkmark
            ctx.SetSourceRGB(0.996, 0.894, 0.812); // cream
            ctx.LineWidth = 2.4;
            ctx.LineCap = LineCap.Round;
            ctx.LineJoin = LineJoin.Round;
            var cx = pad + box * 0.5;
            var cy = boxY + box * 0.5;
            ctx.MoveTo(cx - 5.5, cy + 0.5);
            ctx.LineTo(cx - 1.5, cy + 4.5);
            ctx.LineTo(cx + 6.0, cy - 4.5);
            ctx.Stroke();
        }
        else
        {
            // Empty dark square with stronger rim
            ctx.SetSourceRGBA(0.55, 0.40, 0.28, 0.9);
            ctx.LineWidth = 1.75;
            RoundRectangle(ctx, pad + 1, boxY + 1, box - 2, box - 2, br);
            ctx.Stroke();
        }
    }

    private static void RoundRectangle(Context ctx, double x, double y, double w, double h, double r)
    {
        r = Math.Min(r, Math.Min(w, h) * 0.5);
        ctx.NewPath();
        ctx.MoveTo(x + r, y);
        ctx.LineTo(x + w - r, y);
        ctx.Arc(x + w - r, y + r, r, -Math.PI / 2, 0);
        ctx.LineTo(x + w, y + h - r);
        ctx.Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
        ctx.LineTo(x + r, y + h);
        ctx.Arc(x + r, y + h - r, r, Math.PI / 2, Math.PI);
        ctx.LineTo(x, y + r);
        ctx.Arc(x + r, y + r, r, Math.PI, 3 * Math.PI / 2);
        ctx.ClosePath();
    }

    /// <summary>Отдельный фон скролл-зоны списка участников.</summary>
    private static void DrawScrollAreaBackground(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var width = bounds.OuterWidth;
        var height = bounds.OuterHeight;

        ctx.SetSourceRGB(0.165, 0.118, 0.078); // #2A1E14
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();
    }

    /// <summary>Целочисленное деление с округлением вниз (для отрицательных координат).</summary>
    private static int FloorDiv(int value, int divisor)
    {
        return (int)System.Math.Floor((double)value / divisor);
    }

    #endregion
}
