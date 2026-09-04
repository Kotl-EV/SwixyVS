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
using System.Reflection;
using Cairo;
using SwixyClaimChunk.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

using SwixyClaimChunk.Core;
// ClaimFlagBits lives in root SwixyClaimChunk (Types.cs).

namespace SwixyClaimChunk.Content;

/// <summary>
/// Диалог карты приватов и редактора списка приватов/участников.
/// </summary>
public sealed partial class ClaimMapDialog : GuiDialog
{
    #region Константы и поля состояния

    /// <summary>Радиус окна карты по умолчанию (в чанках от центра).</summary>
    private const int DefaultRadius = 10;

    /// <summary>Индекс вкладки «Карта чанков».</summary>
    private const int PageMap = 0;

    /// <summary>Индекс вкладки «Мои приваты».</summary>
    private const int PageClaims = 1;

    // =============================================================================
    // Frame: Group 1012 dialog_frame.png 1012×760.
    // Map tab: Group 1350.svg. Claims tab: Group 1349.svg.
    //
    // Tabs: Map(72,0,304×74) Claims(468,0,304×74) Close(858,0,85×74)
    // Map well: (467,215) 475×474 + 2px #121212 (outer 465,213)
    // Limits face (73,217) 304×84 | Legend title (73,321) 304×30 + rows 268×30
    // Center face (73,655) 304×32 | Dashes y=187 left 69→379 / right 465→943
    // =============================================================================
    private const int UiW = 1012;
    private const int UiH = 760;

    private const int TabY = 0;
    private const int TabH = 74;
    private const int TabMapX = 72;
    private const int TabMapW = 304;
    private const int TabClaimsX = 468;
    private const int TabClaimsW = 304;
    // Right plate Group 1344 / 1012: x=858 w=85 (to 943) — close.
    // Close glyph is pixel-art at (884,30)–(916,62), fill #836045 (see CloseIconRects).
    // Pin/detach: vanilla Fixed/Movable list menu on RMB over the top chrome (TabY…PanelY).
    private const int CloseBtnX = 858;
    private const int CloseBtnW = 85;
    private const string DialogComposerName = "epclaimmap";
    /// <summary>Header band used for RMB pin menu + drag when Movable (above content panel).</summary>
    private const int DetachChromeH = PanelY;
    private static readonly double[] ColCloseIcon = [0x83 / 255.0, 0x60 / 255.0, 0x45 / 255.0, 1.0]; // #836045

    private const int PanelX = 42;
    private const int PanelY = 118;
    private const int PanelW = 920;
    private const int PanelH = 590;

    /// <summary>Left-column face X/W from Group 1350 (limits/legend/center).</summary>
    private const int CardX = 73;
    private const int CardW = 304;
    private const int CardPadX = 12;
    private const int CardPadY = 10;

    // --- Section headers Group 1350/1349: dashes y=187, titles ~165–172 ---
    private const double SectionTitleBaselineY = 165.288;
    private const double FontSection = 22;
    private const double SectionDashY = 187;
    private const double SectionDashLeftX1 = 69;
    private const double SectionDashLeftX2 = 379;
    private const double SectionDashRightX1 = 465;
    private const double SectionDashRightX2 = 943;
    private const double SectionClaimsTextX = 176.821;
    private const double SectionSettingsTextX = 621.817;
    private const double SectionDashWidth = 4;
    private const double SectionDashOn = 8;
    private const double SectionDashOff = 8;
    private static readonly double[] ColSection = [0x83 / 255.0, 0x67 / 255.0, 0x50 / 255.0, 1.0]; // #836750
    /// <summary>
    /// SVG: stroke #836650 @ opacity 0.32 over Group 1012 face (dashed section rules).
    /// </summary>
    private static readonly double[] ColSectionDash =
    [
        (0x83 * 0.32 + 0x2F * 0.68) / 255.0,
        (0x66 * 0.32 + 0x1D * 0.68) / 255.0,
        (0x50 * 0.32 + 0x11 * 0.68) / 255.0,
        1.0
    ];

    // --- Left column faces (Group 1350 absolute) = Group 1355 faces at local (4,4)/(4,108) ---
    private const int LimitsX = 73;
    private const int LimitsY = 217;
    private const int LimitsFaceW = 304;
    private const int LimitsFaceH = 84;
    private const int LimitsTexW = LimitsFaceW;
    private const int LimitsTexH = LimitsFaceH;

    // Group 1355 text relative to brown face origin (face at local 4,4):
    // left x=16 → +12; values end x≈294 → right edge +290; baselines y=27/58/78 → +23/54/74.
    private const double LimitsTextLeftX = 12;
    private const double LimitsValueRightX = 290;
    private const double LimitsTitleBaselineY = 23;
    private const double LimitsLine1BaselineY = 54;
    private const double LimitsLine2BaselineY = 74;
    private const double FontLimitsTitle = 13;
    private const double FontLimitsBody = 12;

    // Legend title face absolute y=321 (= Group 1355 local y=108). Design stack H=138.
    private const int LegendX = LimitsX;
    private const int LegendY = 321;
    private const int LegendTitleW = LimitsFaceW;
    private const int LegendTitleH = 30;
    private const int LegendRowX = LimitsX;
    private const int LegendRowW = 268;
    private const int LegendRowH = 30;
    private const int LegendRow1Y = 357;
    private const int LegendRow2Y = 393;
    private const int LegendRow3Y = 429;
    private const int LegendSwatchX = LegendX + LimitsFaceW - LegendRowH;
    private const int LegendSwatchSize = 30;
    private const int LegendTexW = LimitsFaceW;
    private const int LegendDesignH = LegendRow3Y + LegendRowH - LegendY; // 138
    private const int LegendTexH = LegendDesignH;
    // Group 1355 text relative to legend title face (local 4,108): left +12; baselines +20/+56/+92/+128.
    private const double LegendTextLeftX = 12;
    private const double LegendTitleBaselineY = 20;
    private const double LegendRow1BaselineY = 56;
    private const double LegendRow2BaselineY = 92;
    private const double LegendRow3BaselineY = 128;
    private const double FontLegendTitle = 13;
    private const double FontLegendBody = 12;

    // Measured #412D1D face bbox inside PNGs (panel_* 318×N). Used to map tex → design face 1:1.
    private const int LimitsPngFaceX = 6;
    private const int LimitsPngFaceY = 6;
    private const int LimitsPngFaceW = 306;
    private const int LimitsPngFaceH = 100;
    private const int LegendPngFaceX = 6;
    private const int LegendPngFaceY = 6;
    private const int LegendPngFaceW = 306;
    private const int LegendPngFaceH = 149;
    private const int CenterPngFaceX = 6;
    private const int CenterPngFaceY = 6;
    private const int CenterPngFaceW = 306;
    private const int CenterPngFaceH = 30;

    // Status between legend bottom (459) and center top (655).
    private const int MessageY = 480;

    // --- Center Group 1350: face (73,655) 304×32 #4D3624 ---
    private const int CenterX = 73;
    private const int CenterY = 655;
    private const int CenterTexW = 304;
    private const int CenterTexH = 32;

    // --- Interactive map Group 1350: (467,215) 475×474 + 2px #121212 ---
    private const int MapX = 467;
    private const int MapY = 215;
    private const int MapW = 475;
    private const int MapH = 474;
    private const int MapBorder = 2;

    // Lift content toward dashed section rule (SectionDashY=174): SVG Y − 10.
    private const int ContentYNudge = 10;

    // --- Claims list Group 1349.svg: outer 302×76 @ (69,215), step 82 (gap 6) ---
    // Name face 256×70 @ (73,217); icon wells 32×32 @ (335,217) & (335,255).
    private const int ClaimsListX = 69;
    private const int ClaimsListY = 215 - ContentYNudge;
    private const int ClaimsListW = 302;
    private const int ClaimsListH = 486; // 6 rows × 82 − last gap
    private const int ClaimsCardH = 76;
    /// <summary>Gap between claim rows (SVG step 82 − row 76).</summary>
    private const int ClaimsCardGap = 6;
    private const int ClaimsIconColW = 32;
    // Scroll just right of list outer edge 371.
    private const int ClaimsScrollX = ClaimsListX + ClaimsListW + 8;
    private const int ClaimsScrollY = ClaimsListY;
    private const int ClaimsScrollW = 6;
    private const int ClaimsScrollH = ClaimsListH;
    private const double ClaimsScrollThumbMinH = 36;
    private static readonly double[] ColScrollTrack = [69 / 255.0, 50 / 255.0, 36 / 255.0];
    private static readonly double[] ColScrollThumb = [0x7E / 255.0, 0x5D / 255.0, 0x43 / 255.0];

    // --- Settings block Group 1347.svg → panel_settings.png 478×174 @ dialog (465,215) ---
    // Local SVG coords + origin = absolute. Rows local y=4 / 56 / 100 / 138, face H=32.
    private const int SettingsTexX = 465;
    private const int SettingsTexY = 215 - ContentYNudge;
    private const int SettingsTexW = 478;
    private const int SettingsTexH = 174;
    private const int SettingsX = SettingsTexX;
    private const int SettingsY = SettingsTexY;
    private const int SettingsW = SettingsTexW;
    private const int SettingsH = SettingsTexH;

    private const int PlateH = 32;

    // Row 1 local: AREAS(4,4,213) CHUNKS(223,4,213) GEAR(442,4,32)
    private const int StatAreasX = SettingsTexX + 4;
    private const int StatAreasY = SettingsTexY + 4;
    private const int StatAreasW = 213;
    private const int StatChunksX = SettingsTexX + 223;
    private const int StatChunksW = 213;
    private const int StatGearX = SettingsTexX + 442;
    private const int StatGearW = 32;
    // Gear hub @ local (458,20), 24px well → draw origin local (446,8)
    private const double StatGearIconLocalX = 446;
    private const double StatGearIconLocalY = 8;
    private const double StatGearIconSize = 24;

    // Row 2 local: flags (4,56) & (242,56) w=232; checkbox (15,63)/(253,63) 18×18; labels (44,77)/(289,68.25)
    private const int FlagPvpX = SettingsTexX + 4;
    private const int FlagPvpY = SettingsTexY + 56;
    private const int FlagPlateW = 232;
    private const int FlagAnimalsX = SettingsTexX + 242;
    private const int FlagCheckLocalX = 11; // 15−4 / 253−242
    private const int FlagCheckLocalY = 7;  // 63−56
    private const int FlagCheckSize = 18;

    // Row 3–4 local: input(4,100/138,314) + btn(324,100/138,150)
    private const int RenameInputX = SettingsTexX + 4;
    private const int RenameInputY = SettingsTexY + 100;
    private const int RenameInputW = 314;
    private const int RenameBtnX = SettingsTexX + 324;
    private const int RenameBtnW = 150;
    private const int AddInputX = SettingsTexX + 4;
    private const int AddInputY = SettingsTexY + 138;
    private const int AddInputW = 314;
    private const int AddBtnX = SettingsTexX + 324;
    private const int AddBtnW = 150;

    // Text layout: consistent pad/baseline inside 32px faces (SVG paths were English pixel-font anchors).
    private const int FacePadX = 12;
    /// <summary>Baseline from face top for ~14px Montserrat in 32px face (optical center).</summary>
    private const int FaceTextBaseline = 21;
    private const double StatAreasTextX = StatAreasX + FacePadX;
    private const double StatChunksTextX = StatChunksX + FacePadX;
    private const double StatTextBaselineY = StatAreasY + FaceTextBaseline;
    // After checkbox (11+18) + gap 8 → text starts ~37 from face left.
    private const double FlagLabelLocalX = FlagCheckLocalX + FlagCheckSize + 8;
    private const double FlagPvpTextX = FlagPvpX + FlagLabelLocalX;
    private const double FlagAnimalsTextX = FlagAnimalsX + FlagLabelLocalX;
    private const double FlagPvpTextBaselineY = FlagPvpY + FaceTextBaseline;
    private const double FlagAnimalsTextBaselineY = FlagPvpY + FaceTextBaseline;
    private const double RenamePlaceholderX = RenameInputX + FacePadX;
    private const double RenamePlaceholderBaselineY = RenameInputY + FaceTextBaseline;
    private const double AddPlaceholderX = AddInputX + FacePadX;
    private const double AddPlaceholderBaselineY = AddInputY + FaceTextBaseline;
    // Button labels centered in plates.
    private const double RenameBtnCenterX = RenameBtnX + RenameBtnW * 0.5;
    private const double AddBtnCenterX = AddBtnX + AddBtnW * 0.5;
    private const double RenameBtnBaselineY = RenameInputY + FaceTextBaseline;
    private const double AddBtnBaselineY = AddInputY + FaceTextBaseline;
    private const double FontSettingsStats = 14;
    private const double FontSettingsLabel = 14;
    private const double FontSettingsBtn = 14;
    /// <summary>AREAS #6B513D.</summary>
    private static readonly double[] ColStatAreas = [0x6B / 255.0, 0x51 / 255.0, 0x3D / 255.0, 1.0];
    /// <summary>CHUNKS #836650 @ 0.64.</summary>
    private static readonly double[] ColStatChunks = [0x83 / 255.0, 0x66 / 255.0, 0x50 / 255.0, 0.64];
    /// <summary>Flags / buttons #9F795B.</summary>
    private static readonly double[] ColSettingsBtn = [0x9F / 255.0, 0x79 / 255.0, 0x5B / 255.0, 1.0];
    /// <summary>RENAME/ADD #9F795B @ 0.64.</summary>
    private static readonly double[] ColSettingsBtnMuted = [0x9F / 255.0, 0x79 / 255.0, 0x5B / 255.0, 0.64];
    /// <summary>Placeholders RegionName… / PlayerName… #836650 @ 0.32.</summary>
    private static readonly double[] ColSettingsPlaceholder = [0x83 / 255.0, 0x66 / 255.0, 0x50 / 255.0, 0.32];
    /// <summary>Gear stroke #FEE4CF (Group 1347).</summary>
    private static readonly double[] ColSettingsGear = [0xFE / 255.0, 0xE4 / 255.0, 0xCF / 255.0, 1.0];

    // --- Below settings: members list OR use-filter — same band (465…943) ---
    // Shared so gear swap does not shift left claims list or settings plates.
    private const int MembersX = SettingsTexX; // 465
    private const int MembersY = 417 - ContentYNudge;
    private const int MembersRight = SettingsTexX + SettingsTexW; // 943
    private const int MembersScrollW = ClaimsScrollW;
    private const int MembersScrollX = MembersRight - MembersScrollW; // 937
    private const int MembersW = MembersScrollX - ClaimsCardGap - MembersX; // 466
    private const int MembersRowH = 58;
    private const int MembersRowGap = 6;
    private const int MembersNameW = 203;
    private const int MembersBtnSize = 46;
    private const int MembersBtnGap = 9;
    private const int MembersScrollY = MembersY;
    private const int MembersScrollH = 274; // fills band under settings to ~bottom
    private const int MembersH = MembersScrollH;

    // Use-filter (Group 1349): Search + PublicUse on first row, then 48×48 grid step 58.
    // X positions relative to MembersX so left edge matches members list.
    private const int UseSearchH = 32;
    private const int UseSearchW = 262;
    private const int UseStatusW = 202;
    private const int UseStatusGap = 6;
    private const int UseSearchX = MembersX + 4; // face inset like settings plates
    private const int UseSearchY = MembersY;
    private const int UseStatusX = UseSearchX + UseSearchW + UseStatusGap;
    private const int UseStatusY = UseSearchY;
    private const int UseStatusH = UseSearchH;
    private const int UseGridX = MembersX + 4;
    private const int UseGridY = MembersY + UseSearchH + 20; // 469−417 = 52 in SVG
    private const int UseTile = 48;
    private const int UseTileGap = 10;
    private const int UseGridCols = 8;
    private const int UseGridW = MembersW - 8; // same content width as members face band
    private const int UseGridH = MembersY + MembersH - UseGridY;

    // Right panel modes: settings/members vs use-filter catalog (in-panel, not modal).
    private const int ClaimsRightSettings = 0;
    private const int ClaimsRightUseFilter = 1;

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
    private static readonly double[] ColPanel = [0.184, 0.114, 0.067];   // #2F1D11 Group 1012 face
    private static readonly double[] ColTabActive = [1.0, 1.0, 1.0, 1.0];
    private static readonly double[] ColTabInactive = [0.624, 0.475, 0.357, 1.0]; // #9F795B from SVG
    private static readonly double[] ColLimitsTitle = [0.514, 0.400, 0.314, 1.0]; // #836650 title in Group 466

    /// <summary>Клиентский API Vintage Story.</summary>
    private readonly ICoreClientAPI clientApi;

    /// <summary>Сетевой канал SwixyClaimChunk для пакетов карты и приватов.</summary>
    private readonly IClientNetworkChannel channel;

    /// <summary>Фон-фрейм Group 1012 (textures/gui/dialog_frame.png).</summary>
    private ImageSurface? frameSurface;

    /// <summary>Кнопка «К игроку» — Group 464.png (textures/gui/button_center.png).</summary>
    private ImageSurface? centerButtonSurface;

    /// <summary>Плашка легенды — Group 387 (1).png (textures/gui/panel_legend.png).</summary>
    private ImageSurface? legendPanelSurface;

    /// <summary>Плашка лимитов — Group 466.png (textures/gui/panel_limits.png).</summary>
    private ImageSurface? limitsPanelSurface;

    /// <summary>Блок настроек — Group 1347.svg plates (textures/gui/panel_settings.png, 478×174).</summary>
    private ImageSurface? settingsPanelSurface;

    /// <summary>Legacy Group 469 buttons (optional; labels now drawn over Group 1346).</summary>
    private ImageSurface? settingsButtonSurface;

    /// <summary>Трек скролла списка приватов — Rectangle 758.png (scrollbar_track.png).</summary>
    private ImageSurface? scrollTrackSurface;

    /// <summary>Сетка чанков на вкладке карты; null до первой компоновки.</summary>
    private ClaimMapGridElement? gridElement;

    /// <summary>Последний снимок карты с сервера.</summary>
    private ClaimMapStatePacket? mapState;

    /// <summary>Локальный статус (Working… / send error); null — брать mapState.Message.</summary>
    private string? mapStatusOverride;

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

    /// <summary>Внешняя область списка участников (паркуется при открытой шестерёнке).</summary>
    private ElementBounds? memberListAreaBounds;

    /// <summary>Hit-зона скролла участников.</summary>
    private ElementBounds? memberScrollAreaBounds;

    /// <summary>Сообщение под списком участников.</summary>
    private ElementBounds? claimsMessageAreaBounds;

    /// <summary>Поле поиска use-filter (паркуется, когда шестерёнка выкл.).</summary>
    private ElementBounds? useFilterSearchAreaBounds;

    /// <summary>Hit-зона скролла use-filter.</summary>
    private ElementBounds? useFilterScrollAreaBounds;

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

    /// <summary>Vanilla Fixed/Movable dropdown (same as GuiElementDialogTitleBar).</summary>
    private GuiElementListMenu? detachListMenu;

    /// <summary>True after user picks Movable (or restored from saved dialog position).</summary>
    private bool dialogMovable;

    /// <summary>LMB drag of the whole window while Movable.</summary>
    private bool dialogDragging;

    private readonly Vec2i dialogDragStart = new();

    /// <summary>Snapshot of centered bounds before first detach (restore on Fixed).</summary>
    private ElementBounds? dialogAnchoredBoundsBefore;

    #endregion

    /// <summary>Код горячей клавиши P для открытия карты приватов.</summary>
    public override string ToggleKeyCombinationCode => ClaimConstants.OpenMapHotkeyCode;

    /// <summary>Не захватывать мышь глобально — удобнее кликать по карте.</summary>
    public override bool PrefersUngrabbedMouse => true;

    /// <summary>VS clickable pointer (same as Questbook — assets/game/textures/gui/cursors/linkselect.png).</summary>
    private const string ClickableCursor = "linkselect";

    /// <summary>Text-field cursor over rename / add / use-filter search.</summary>
    private const string TextCursor = "textselect";

    /// <summary>Move cursor when dragging a Movable dialog by the header chrome.</summary>
    private const string MoveCursor = "move";

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
            // No claims left — discard use-filter draft so it cannot save onto a later selection.
            DiscardUseFilterDraftIfOpen(applyUi: false);
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
        EnsureFrameSurface();
        // Constructor ComposeDialog runs before TryOpen: CustomDraw GPU textures can be
        // empty until a tab switch rebuilds the composer. Recompose now that we are registered.
        ComposeDialog();
        CenterOnPlayer();
        gridElement?.CenterMapOnPlayer();
        RequestRefresh();
        RequestClaimList();
        RefreshOpenGuiTextures();
        // One more redraw next frame — Fill/clip bounds settle after the first GUI render.
        clientApi.Event.RegisterCallback(_ =>
        {
            if (!IsOpened())
            {
                return;
            }

            EnsureFrameSurface();
            RefreshOpenGuiTextures();
            gridElement?.CenterMapOnPlayer();
        }, 0);

        if (highlightedClaimId > 0 && activePage == PageClaims)
        {
            RunClaimsUiDeferred(() => RefreshClaimHighlightIcons(GetClaimListScrollOffset()));
        }
    }

    /// <summary>Escape: из use-filter → настройки; иначе закрыть окно.</summary>
    public override bool OnEscapePressed()
    {
        if (deleteConfirmOpen)
        {
            CloseDeleteConfirm();
            return true;
        }

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
        // Stop background near-scan so it cannot touch UI after close.
        useFilterClientScanner?.Cancel();
        useFilterScanning = false;
        claimsRightMode = ClaimsRightSettings;
        dialogDragging = false;
        MouseOverCursor = null;
        CloseDetachListMenu();
        CloseDeleteConfirm();
        // Keep PNG Cairo surfaces: closing used to Dispose them, then ApplyState/Redraw
        // painted fallback plates until a tab switch called ComposeDialog again.
        base.OnGuiClosed();
    }

    public override void Dispose()
    {
        DisposeFrameSurface();
        base.Dispose();
    }

    /// <summary>
    /// Base copies composer → MouseOverCursor; we override like Questbook so invisible
    /// hit buttons (tabs/flags/gear) still show the linkselect pointer.
    /// </summary>
    public override void OnRenderGUI(float deltaTime)
    {
        if (deleteConfirmOpen)
        {
            SyncDeleteConfirmBounds();
        }

        base.OnRenderGUI(deltaTime);
        UpdateHoverCursor(clientApi.Input.MouseX, clientApi.Input.MouseY);
    }

    /// <summary>
    /// Set <see cref="GuiDialog.MouseOverCursor"/> for clickable hit areas.
    /// Must run at the end of <see cref="OnRenderGUI"/> — that is when GuiManager samples it.
    /// </summary>
    private void UpdateHoverCursor(int mouseX, int mouseY)
    {
        if (dialogDragging)
        {
            MouseOverCursor = MoveCursor;
            return;
        }

        if (deleteConfirmOpen)
        {
            MouseOverCursor = IsOverDeleteConfirmButton(mouseX, mouseY) ? ClickableCursor : null;
            return;
        }

        if (IsOverTextInput(mouseX, mouseY))
        {
            MouseOverCursor = TextCursor;
            return;
        }

        if (dialogMovable
            && IsDetachChromeHit(mouseX, mouseY)
            && !IsHeaderControlHit(mouseX, mouseY)
            && detachListMenu is not { IsOpened: true })
        {
            MouseOverCursor = MoveCursor;
            return;
        }

        MouseOverCursor = IsOverClickableControl(mouseX, mouseY) ? ClickableCursor : null;
    }

    private bool IsOverTextInput(int mouseX, int mouseY)
    {
        if (activePage != PageClaims || GetSelectedClaim() == null)
        {
            return false;
        }

        // Rename / add player fields (settings plates).
        if (IsMouseOverRect(mouseX, mouseY, RenameInputX, RenameInputY, RenameInputW, PlateH)
            || IsMouseOverRect(mouseX, mouseY, AddInputX, AddInputY, AddInputW, PlateH))
        {
            return true;
        }

        // Use-filter search when gear panel is open.
        return claimsRightMode == ClaimsRightUseFilter
            && useFilterSearchAreaBounds != null
            && IsMouseOverElementBounds(mouseX, mouseY, useFilterSearchAreaBounds);
    }

    private bool IsOverClickableControl(int mouseX, int mouseY)
    {
        // Top tabs + close
        if (IsHeaderControlHit(mouseX, mouseY))
        {
            return true;
        }

        // Detach menu open / header RMB affordance
        if (detachListMenu is { IsOpened: true } && detachListMenu.IsPositionInside(mouseX, mouseY))
        {
            return true;
        }

        if (IsDetachChromeHit(mouseX, mouseY))
        {
            return true;
        }

        if (activePage == PageMap)
        {
            if (IsMouseOverRect(mouseX, mouseY, MapX, MapY, MapW, MapH)
                || IsMouseOverRect(mouseX, mouseY, CenterX, CenterY, CenterTexW, CenterTexH))
            {
                return true;
            }
        }
        else if (activePage == PageClaims && GetSelectedClaim() != null)
        {
            // Left claims list + thin scroll track
            if (IsMouseOverRect(mouseX, mouseY, ClaimsListX, ClaimsListY, ClaimsListW, ClaimsListH)
                || IsMouseOverRect(mouseX, mouseY, ClaimsScrollX, ClaimsScrollY, ClaimsScrollW, ClaimsScrollH))
            {
                return true;
            }

            // Settings hit plates: flags, gear, rename/add buttons
            if (IsMouseOverRect(mouseX, mouseY, FlagPvpX, FlagPvpY, FlagPlateW, PlateH)
                || IsMouseOverRect(mouseX, mouseY, FlagAnimalsX, FlagPvpY, FlagPlateW, PlateH)
                || IsMouseOverRect(mouseX, mouseY, StatGearX, StatAreasY, StatGearW, PlateH)
                || IsMouseOverRect(mouseX, mouseY, RenameBtnX, RenameInputY, RenameBtnW, PlateH)
                || IsMouseOverRect(mouseX, mouseY, AddBtnX, AddInputY, AddBtnW, PlateH))
            {
                return true;
            }

            if (claimsRightMode == ClaimsRightUseFilter)
            {
                if (useFilterViewportBounds != null
                    && IsMouseOverElementBounds(mouseX, mouseY, useFilterViewportBounds))
                {
                    return true;
                }

                if (IsMouseOverUseFilterScrollTrack(mouseX, mouseY))
                {
                    return true;
                }
            }
            else
            {
                if (IsMouseOverRect(mouseX, mouseY, MembersX, MembersY, MembersW, MembersH)
                    || IsMouseOverRect(mouseX, mouseY, MembersScrollX, MembersScrollY, MembersScrollW, MembersScrollH))
                {
                    return true;
                }
            }
        }

        return false;
    }

    #endregion

    #region Компоновка GUI

    /// <summary>Полная пересборка диалога (обе вкладки) и восстановление скролла/полей ввода.</summary>
    private void ComposeDialog()
    {
        ClaimFontHelper.EnsureRegistered(clientApi);
        EnsureFrameSurface();

        // Fixed = centered; Movable = free position from client settings (vanilla dialog pin).
        var dialogBounds = BuildDialogBounds();
        var bgBounds = ElementBounds.Fill;

        gridElement?.Dispose();
        gridElement = null;
        detachListMenu = null;
        if (activePage == PageMap)
        {
            gridElement = new ClaimMapGridElement(
                clientApi,
                ElementBounds.Fixed(MapX, MapY, MapW, MapH),
                OnChunksSelected,
                () => RequestRefresh(useMapView: true));
        }

        ClearComposers();
        // Invisible hit targets — labels drawn in DrawDialogChrome (Minecraft faces).
        var hitFont = ClaimFontHelper.Create(1, [0, 0, 0, 0], bold: true);
        var composer = clientApi.Gui
            .CreateCompo(DialogComposerName, dialogBounds)
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
                CloseButton,
                ElementBounds.Fixed(CloseBtnX, TabY, CloseBtnW, TabH),
                hitFont,
                EnumButtonStyle.None,
                "closeBtn");

        // Vanilla Fixed/Movable list (GuiElementDialogTitleBar menu) — opened via RMB on header.
        var detachMenuBounds = ElementBounds.Fixed(UiW - 160, 8, 140, 22);
        detachListMenu = new GuiElementListMenu(
            clientApi,
            ["auto", "manual"],
            [Lang.Get("Fixed"), Lang.Get("Movable")],
            dialogMovable ? 1 : 0,
            OnDetachMenuSelection,
            detachMenuBounds,
            CairoFont.WhiteSmallText(),
            multiSelect: false)
        {
            HoveredIndex = dialogMovable ? 1 : 0
        };
        composer.AddInteractiveElement(detachListMenu, "detachListMenu");

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
            // Sync Enabled for parked band after full compose (bounds already parked in compose).
            SyncGearModeElementEnabled();
        }

        ApplyClaimsPageScrollState();
        ApplyClaimsPageInputState();
        UpdateText(null);
        RefreshOpenGuiTextures();
        if (deleteConfirmOpen)
        {
            ComposeDeleteConfirmOverlay();
        }
    }

    /// <summary>
    /// Regenerates CustomDraw GPU textures (frame, map cards, claims chrome) after bounds exist.
    /// </summary>
    private void RefreshOpenGuiTextures()
    {
        EnsureFrameSurface();
        try
        {
            SingleComposer?.GetCustomDraw("dialogChrome")?.Redraw();
        }
        catch
        {
            // Composer not ready.
        }

        try
        {
            SingleComposer?.GetCustomDraw("mapPageContent")?.Redraw();
        }
        catch
        {
            // Map tab not built.
        }

        try
        {
            SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();
            SingleComposer?.GetCustomDraw("claimFlagPvpBg")?.Redraw();
            SingleComposer?.GetCustomDraw("claimFlagAnimalsBg")?.Redraw();
        }
        catch
        {
            // Claims tab not built.
        }
    }

    /// <summary>
    /// Dialog root bounds: CenterMiddle when Fixed, saved free position when Movable
    /// (same keys as vanilla title-bar pin via <see cref="IGuiAPI.GetDialogPosition"/>).
    /// </summary>
    private ElementBounds BuildDialogBounds()
    {
        var dialogBounds = ElementBounds.Fixed(0, 0, UiW, UiH);
        var savedPos = clientApi.Gui.GetDialogPosition(DialogComposerName);
        if (savedPos != null)
        {
            dialogMovable = true;
            dialogBounds.Alignment = EnumDialogArea.None;
            dialogBounds.fixedX = savedPos.X;
            dialogBounds.fixedY = Math.Max(0, savedPos.Y);
            dialogBounds.absMarginX = 0;
            dialogBounds.absMarginY = 0;
            return dialogBounds;
        }

        dialogMovable = false;
        return dialogBounds.WithAlignment(EnumDialogArea.CenterMiddle);
    }

    /// <summary>Vanilla list values: "auto" = Fixed, "manual" = Movable.</summary>
    private void OnDetachMenuSelection(string val, bool on)
    {
        // Single-select also fires on=false for the previous row. That callback
        // must be ignored — otherwise picking Movable is immediately undone by auto/false.
        if (!on)
        {
            return;
        }

        var parent = SingleComposer?.Bounds;
        if (parent == null)
        {
            return;
        }

        if (val == "auto")
        {
            // Fixed — re-center (restore snapshot if we still have one from this session).
            if (dialogAnchoredBoundsBefore != null)
            {
                parent.fixedX = dialogAnchoredBoundsBefore.fixedX;
                parent.fixedY = dialogAnchoredBoundsBefore.fixedY;
                parent.fixedOffsetX = dialogAnchoredBoundsBefore.fixedOffsetX;
                parent.fixedOffsetY = dialogAnchoredBoundsBefore.fixedOffsetY;
                parent.Alignment = dialogAnchoredBoundsBefore.Alignment;
                parent.absMarginX = dialogAnchoredBoundsBefore.absMarginX;
                parent.absMarginY = dialogAnchoredBoundsBefore.absMarginY;
            }
            else
            {
                parent.Alignment = EnumDialogArea.CenterMiddle;
                parent.fixedX = 0;
                parent.fixedY = 0;
                parent.fixedOffsetX = 0;
                parent.fixedOffsetY = 0;
                parent.absMarginX = 0;
                parent.absMarginY = 0;
            }

            dialogMovable = false;
            dialogDragging = false;
            clientApi.Gui.SetDialogPosition(DialogComposerName, null);
            parent.MarkDirtyRecursive();
            parent.CalcWorldBounds();
            return;
        }

        // Movable — free drag; remember position in client settings.
        if (!dialogMovable)
        {
            dialogAnchoredBoundsBefore = parent.FlatCopy();
        }

        dialogMovable = true;
        parent.Alignment = EnumDialogArea.None;
        parent.fixedOffsetX = 0;
        parent.fixedOffsetY = 0;
        parent.fixedX = parent.absX / RuntimeEnv.GUIScale;
        parent.fixedY = parent.absY / RuntimeEnv.GUIScale;
        parent.absMarginX = 0;
        parent.absMarginY = 0;
        parent.MarkDirtyRecursive();
        parent.CalcWorldBounds();
        clientApi.Gui.SetDialogPosition(
            DialogComposerName,
            new Vec2i((int)parent.fixedX, (int)parent.fixedY));
    }

    private void OpenDetachListMenu(int mouseX, int mouseY)
    {
        if (detachListMenu == null || SingleComposer?.Bounds == null)
        {
            return;
        }

        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var parent = SingleComposer.Bounds;
        // Anchor dropdown near cursor. Leave room below for both Fixed/Movable rows
        // (clamping into DetachChromeH put the 2nd row outside the list hit box).
        const int menuW = 150;
        const int menuTriggerH = 22;
        const int dropRowsH = 52;
        detachListMenu.Bounds.fixedX = Math.Clamp((mouseX - parent.absX) / s, 8, UiW - menuW);
        detachListMenu.Bounds.fixedY = Math.Clamp(
            (mouseY - parent.absY) / s,
            4,
            Math.Max(4, UiH - menuTriggerH - dropRowsH));
        detachListMenu.Bounds.CalcWorldBounds();
        detachListMenu.SetSelectedIndex(dialogMovable ? 1 : 0);
        detachListMenu.HoveredIndex = dialogMovable ? 1 : 0;
        detachListMenu.Open();
        clientApi.Gui.PlaySound("menubutton");
    }

    /// <summary>
    /// <see cref="GuiElementListMenu.Close"/> is internal — invoke via reflection (same as title bar).
    /// </summary>
    private void CloseDetachListMenu()
    {
        if (detachListMenu == null || !detachListMenu.IsOpened)
        {
            return;
        }

        typeof(GuiElementListMenu)
            .GetMethod("Close", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.Invoke(detachListMenu, null);
    }

    private bool IsDetachChromeHit(int mouseX, int mouseY)
    {
        return IsMouseOverRect(mouseX, mouseY, 0, 0, UiW, DetachChromeH);
    }

    private bool IsHeaderControlHit(int mouseX, int mouseY)
    {
        return IsMouseOverRect(mouseX, mouseY, TabMapX, TabY, TabMapW, TabH)
            || IsMouseOverRect(mouseX, mouseY, TabClaimsX, TabY, TabClaimsW, TabH)
            || IsMouseOverRect(mouseX, mouseY, CloseBtnX, TabY, CloseBtnW, TabH);
    }

    private void PersistDialogPositionIfMovable()
    {
        if (!dialogMovable || SingleComposer?.Bounds == null)
        {
            return;
        }

        var b = SingleComposer.Bounds;
        clientApi.Gui.SetDialogPosition(DialogComposerName, new Vec2i((int)b.fixedX, (int)b.fixedY));
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
        // Group 1349: FixedHeight=76 + CellList spacing=6 (step 82).
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

        // Always the same element tree (settings + members + use-filter).
        // Use-filter is parked off-screen when inactive so gear toggle does not change left claims list structure.
        ComposeSettingsAndMembersPanel(composer, selectedClaim, labelFont, bodyFont, actionButtonFont, inputFont);
        ComposeUseFilterPanel(composer, selectedClaim, labelFont, bodyFont, actionButtonFont, inputFont);
    }

    /// <summary>Правая колонка: settings plates + members list (always; gear overlays use-filter on top).</summary>
    private void ComposeSettingsAndMembersPanel(
        GuiComposer composer,
        ClaimInfoPacket selectedClaim,
        CairoFont labelFont,
        CairoFont bodyFont,
        CairoFont actionButtonFont,
        CairoFont inputFont)
    {
        // Input text cream; plates drawn in DrawClaimsPageChrome (Group 1346 faces).
        var settingsInputFont = ClaimFontHelper.Create(14, ClaimFontHelper.ColorCream, bold: true);
        var hitFont = ClaimFontHelper.Create(1, [0, 0, 0, 0], bold: true);

        // Text field inset — same pad as plate labels / placeholders.
        const int inputPadX = FacePadX;
        const int inputPadY = 6;

        composer
            // Flag plates (hit = full face).
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(FlagPvpX, FlagPvpY, FlagPlateW, PlateH),
                DrawClaimFlagPvpPlate,
                "claimFlagPvpBg")
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(FlagAnimalsX, FlagPvpY, FlagPlateW, PlateH),
                DrawClaimFlagAnimalsPlate,
                "claimFlagAnimalsBg")
            .AddButton(
                " ",
                ToggleClaimFlagPvpButton,
                ElementBounds.Fixed(FlagPvpX, FlagPvpY, FlagPlateW, PlateH),
                hitFont,
                EnumButtonStyle.None,
                "claimFlagPvpHit")
            .AddButton(
                " ",
                ToggleClaimFlagAnimalsButton,
                ElementBounds.Fixed(FlagAnimalsX, FlagPvpY, FlagPlateW, PlateH),
                hitFont,
                EnumButtonStyle.None,
                "claimFlagAnimalsHit")
            // Gear plate next to stats — open use-filter overlay.
            .AddButton(
                " ",
                ToggleUseFilterPanel,
                ElementBounds.Fixed(StatGearX, StatAreasY, StatGearW, PlateH),
                hitFont,
                EnumButtonStyle.None,
                "settingsPlateGearBtn")
            // Rename row: wide input + short button.
            .AddTextInput(
                ElementBounds.Fixed(
                    RenameInputX + inputPadX,
                    RenameInputY + inputPadY,
                    RenameInputW - inputPadX * 2,
                    PlateH - inputPadY * 2),
                text => claimNameInput = text,
                settingsInputFont,
                "claimNameInput")
            .AddButton(
                " ",
                RenameClaimButton,
                ElementBounds.Fixed(RenameBtnX, RenameInputY, RenameBtnW, PlateH),
                hitFont,
                EnumButtonStyle.None,
                "renameClaim")
            // Add player row.
            .AddTextInput(
                ElementBounds.Fixed(
                    AddInputX + inputPadX,
                    AddInputY + inputPadY,
                    AddInputW - inputPadX * 2,
                    PlateH - inputPadY * 2),
                text => memberNameInput = text,
                settingsInputFont,
                "memberNameInput")
            .AddButton(
                " ",
                AddMemberButton,
                ElementBounds.Fixed(AddBtnX, AddInputY, AddBtnW, PlateH),
                hitFont,
                EnumButtonStyle.None,
                "addMember");

        // Members list — parked off-screen while use-filter (gear) is open.
        var memPark = claimsRightMode == ClaimsRightUseFilter ? UiParkY : 0;
        memberListAreaBounds = ElementBounds.Fixed(MembersX, MembersY + memPark, MembersW, MembersH);
        memberListClipBounds = memberListAreaBounds.ForkContainingChild(0, 0, 0, 0);
        memberListTableBounds = memberListClipBounds.ForkContainingChild(0, 0, 0, 0);
        memberScrollAreaBounds = ElementBounds.Fixed(
            MembersScrollX,
            MembersScrollY + memPark,
            MembersScrollW,
            MembersScrollH);
        claimsMessageAreaBounds = ElementBounds.Fixed(
            MembersX + 30,
            MembersY + MembersH + 4 + memPark,
            MembersW + ClaimsCardGap + MembersScrollW - 30,
            24);

        composer
            .AddDynamicCustomDraw(memberListAreaBounds, DrawTransparentBounds, "memberListArea")
            .AddDynamicCustomDraw(memberScrollAreaBounds, DrawTransparentBounds, "memberScrollHit")
            .BeginClip(memberListClipBounds)
            .AddCellList(memberListTableBounds, CreateMemberCell, BuildMemberCells(selectedClaim), "memberList")
            .EndClip()
            .AddDynamicText(
                claimListState?.Message ?? "",
                bodyFont,
                claimsMessageAreaBounds,
                "claimsMessage");
    }

    /// <summary>
    /// Group 1349: Search + PublicUse + grid. Always in tree; parked off-screen when gear is off.
    /// </summary>
    private void ComposeUseFilterPanel(
        GuiComposer composer,
        ClaimInfoPacket selectedClaim,
        CairoFont labelFont,
        CairoFont bodyFont,
        CairoFont actionButtonFont,
        CairoFont inputFont)
    {
        RefreshUseFilterEntryLists();

        var ufPark = claimsRightMode == ClaimsRightUseFilter ? 0 : UiParkY;

        useFilterViewportBounds = ElementBounds.Fixed(UseGridX, UseGridY + ufPark, UseGridW, UseGridH);
        useFilterScrollAreaBounds = ElementBounds.Fixed(
            MembersScrollX,
            UseGridY + ufPark,
            MembersScrollW,
            UseGridH);

        useFilterGrid = new ClaimUseFilterTileGridElement(clientApi, useFilterViewportBounds)
        {
            OnTileClick = code =>
            {
                if (claimsRightMode != ClaimsRightUseFilter)
                {
                    return;
                }

                ToggleUseFilterCode(code);
            },
            IsSelected = code =>
            {
                var n = NormalizeUseFilterCode(code);
                return useFilterDraftCodes.Any(c => ClaimCodeUtil.SameCatalogGroup(c, n));
            },
            EmptyHint = GetUseFilterEmptyHint(),
            OnScrollChanged = () =>
            {
                if (claimsRightMode != ClaimsRightUseFilter)
                {
                    return;
                }

                useFilterScroll = useFilterGrid?.ScrollOffset ?? 0f;
                SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();
            }
        };
        useFilterGrid.SetEntries(useFilterEntries);
        useFilterGrid.ScrollOffset = useFilterScroll;

        useFilterSearchAreaBounds = ElementBounds.Fixed(
            UseSearchX + FacePadX,
            UseSearchY + 6 + ufPark,
            UseSearchW - FacePadX * 2,
            UseSearchH - 12);

        composer
            .AddTextInput(
                useFilterSearchAreaBounds,
                text =>
                {
                    if (claimsRightMode != ClaimsRightUseFilter)
                    {
                        return;
                    }

                    OnUseFilterSearchChanged(text);
                },
                ClaimFontHelper.Create(14, ClaimFontHelper.ColorCream, bold: true),
                "useFilterSearch")
            .AddDynamicCustomDraw(useFilterScrollAreaBounds, DrawTransparentBounds, "useFilterScrollHit")
            .AddInteractiveElement(useFilterGrid, "useFilterGrid");
    }

    private const int UiParkY = -20000;

    /// <summary>
    /// Gear open/close without ComposeDialog: park members / unpark use-filter (or reverse).
    /// Left claims list is never touched.
    /// </summary>
    private void ApplyGearModeUi()
    {
        // Tree must already contain both bands (compose always builds both).
        if (SingleComposer == null
            || memberListAreaBounds == null
            || memberListClipBounds == null
            || useFilterViewportBounds == null
            || useFilterGrid == null)
        {
            RecomposeClaimsUiPreservingClaimList();
            return;
        }

        ApplyGearModeBounds();
        SyncGearModeElementEnabled();

        if (claimsRightMode == ClaimsRightUseFilter && useFilterGrid != null)
        {
            useFilterGrid.EmptyHint = GetUseFilterEmptyHint();
            useFilterGrid.SetEntries(useFilterEntries);
            useFilterGrid.ScrollOffset = useFilterScroll;
        }

        try
        {
            SingleComposer.GetCustomDraw("claimsPageChrome")?.Redraw();
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// TextInput has Enabled (GuiElementControl). Grid/cell-list rely on parking bounds.
    /// </summary>
    private void SyncGearModeElementEnabled()
    {
        if (SingleComposer == null)
        {
            return;
        }

        var useOn = claimsRightMode == ClaimsRightUseFilter;
        var search = SingleComposer.GetTextInput("useFilterSearch");
        if (search != null)
        {
            search.Enabled = useOn;
            if (useOn)
            {
                search.SetValue(useFilterSearch ?? "", true);
            }
        }
    }

    /// <summary>
    /// Park/unpark right-band bounds only. Clip/table stay relative to memberListAreaBounds
    /// (do not assign absolute MembersY to forked children — scroll uses table.fixedY).
    /// </summary>
    private void ApplyGearModeBounds()
    {
        var useOn = claimsRightMode == ClaimsRightUseFilter;
        var memY = useOn ? MembersY + UiParkY : MembersY;
        var ufYGrid = useOn ? UseGridY : UseGridY + UiParkY;
        var ufYSearch = useOn ? UseSearchY + 6 : UseSearchY + 6 + UiParkY;

        // Members: move outer area; clip/table recalculate as children (keep scroll fixedY).
        SetBoundsY(memberListAreaBounds, memY);
        if (memberListAreaBounds != null)
        {
            RecalcBoundsTree(memberListAreaBounds);
        }

        SetBoundsY(memberScrollAreaBounds, useOn ? MembersScrollY + UiParkY : MembersScrollY);
        SetBoundsY(claimsMessageAreaBounds, MembersY + MembersH + 4 + (useOn ? UiParkY : 0));

        // Use-filter: absolute Fixed bounds (+ grid element).
        SetBoundsY(useFilterViewportBounds, ufYGrid);
        SetBoundsY(useFilterScrollAreaBounds, ufYGrid);
        SetBoundsY(useFilterSearchAreaBounds, ufYSearch);
        if (useFilterGrid != null)
        {
            SetBoundsY(useFilterGrid.Bounds, ufYGrid);
            RecalcBoundsTree(useFilterGrid.Bounds);
        }
        else if (useFilterViewportBounds != null)
        {
            RecalcBoundsTree(useFilterViewportBounds);
        }

        // Re-apply member scroll offset after world-bounds recalc.
        if (!useOn)
        {
            RestoreMemberListScroll(memberListScrollValue);
        }
    }

    private static void SetBoundsY(ElementBounds? bounds, double fixedY)
    {
        if (bounds == null)
        {
            return;
        }

        bounds.fixedY = fixedY;
        bounds.CalcWorldBounds();
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
        if (selectedClaimId != claim.ClaimId)
        {
            // Same name + gear open ≈ ClaimId remapped after TouchClaim — keep draft, rebind scan id.
            // Different claim while gear open: discard draft so Save cannot hit the wrong claim.
            var sameLogicalClaim = selectedClaimId != 0
                && !string.IsNullOrWhiteSpace(selectedClaimName)
                && string.Equals(selectedClaimName, claim.Name ?? "", StringComparison.OrdinalIgnoreCase);

            if (sameLogicalClaim && claimsRightMode == ClaimsRightUseFilter)
            {
                useFilterScanClaimId = claim.ClaimId;
            }
            else
            {
                DiscardUseFilterDraftIfOpen(applyUi: true);
            }
        }

        selectedClaimId = claim.ClaimId;
        selectedClaimName = claim.Name ?? "";
        selectedMemberUid = "";
        selectedMemberName = "";
        claimNameInput = claim.Name ?? "";
    }

    /// <summary>
    /// Cancels near-scan and exits use-filter mode without saving.
    /// <paramref name="applyUi"/> parks members/use-filter when the claims page is live.
    /// </summary>
    private void DiscardUseFilterDraftIfOpen(bool applyUi)
    {
        if (claimsRightMode != ClaimsRightUseFilter)
        {
            return;
        }

        useFilterClientScanner?.Cancel();
        useFilterScanning = false;
        useFilterDraftCodes.Clear();
        useFilterSearch = "";
        useFilterEntriesFilterKey = "\0";
        useFilterScroll = 0;
        useFilterCatalog = [];
        useFilterEntries = [];
        claimsRightMode = ClaimsRightSettings;

        if (applyUi && IsOpened() && activePage == PageClaims && SingleComposer != null)
        {
            ApplyGearModeUi();
        }
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

    /// <summary>Удаляет участника по UID; владельца удалить нельзя — сначала модалка.</summary>
    private void RemoveMemberByUid(string memberUid)
    {
        var member = FindMemberByUid(memberUid);
        if (member == null || member.IsOwner)
        {
            return;
        }

        OpenDeleteMemberConfirm(member);
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
            // Gear again = apply whitelist and return to members list (no Save/Cancel buttons in SVG).
            return SaveUseFilterPanel();
        }

        return OpenUseFilterPanel();
    }

    /// <summary>
    /// Gear toggle recompose: identical element tree every time + hard restore of left claims scroll.
    /// </summary>
    private void RecomposeClaimsUiPreservingClaimList()
    {
        claimListScrollValue = GetClaimListScrollOffset();
        var savedClaimScroll = claimListScrollValue;
        var savedMemberScroll = GetMemberListScrollOffset();
        ComposeDialog();
        // Spacing can reset cell fixedY — re-apply after both configures.
        ConfigureClaimListSpacing();
        ConfigureMemberListSpacing();
        RestoreClaimListScroll(savedClaimScroll);
        RestoreMemberListScroll(savedMemberScroll);
        // Next frame: VS may recalc cell heights after first layout pass.
        clientApi.Event.RegisterCallback(
            _ =>
            {
                if (!IsOpened() || activePage != PageClaims)
                {
                    return;
                }

                ConfigureClaimListSpacing();
                RestoreClaimListScroll(savedClaimScroll);
                try
                {
                    SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();
                }
                catch
                {
                    // ignore
                }
            },
            1);
        try
        {
            SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();
        }
        catch
        {
            // ignore
        }
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
        // No ComposeDialog — left claims list must stay pixel-identical.
        ApplyGearModeUi();
        SyncClaimFlagSwitches(claim);

        useFilterClientScanner ??= new ClaimUseFilterClientScanner(clientApi);
        useFilterClientScanner.Start(
            claim.ClaimId,
            OnClientUseFilterScanComplete,
            ClaimUseFilterClientScanner.DefaultRadius);
        return true;
    }

    private bool CloseUseFilterPanel()
    {
        // Escape / leave gear without saving — same discard path as claim switch.
        DiscardUseFilterDraftIfOpen(applyUi: true);
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
        useFilterScanning = false;
        useFilterClientScanner?.Cancel();
        ApplyGearModeUi();
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

        cellList.unscaledCellSpacing = ClaimsCardGap; // 6 (SVG step 82)
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

    /// <summary>Подпись строки привата (Group 1351: sentence case, not ALL CAPS).</summary>
    private string BuildClaimListDetailText(ClaimInfoPacket claim)
    {
        return claim.ViewerIsCoOwner
            ? Lang.Get("swixyclaimchunk:claims-list-coowner-stats", claim.OwnerName, claim.ChunkCount)
            : Lang.Get("swixyclaimchunk:claims-list-stats", claim.ChunkCount);
    }

    /// <summary>Строка списка приватов: имя как введено, статистика как в lang (Group 1351).</summary>
    private IEnumerable<SavegameCellEntry> BuildClaimCells()
    {
        var titleFont = ClaimFontHelper.Create(15, ClaimFontHelper.ColorCream, bold: true);
        var detailFont = ClaimFontHelper.Create(13, ClaimFontHelper.ColorAccent, bold: true);
        foreach (var claim in claimListState?.Claims ?? [])
        {
            yield return new SavegameCellEntry
            {
                // Do not force UPPERCASE — Group 1351 uses normal casing for name + "Chunks: n".
                Title = Lang.Get("swixyclaimchunk:claims-list-name", claim.Name),
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

    /// <summary>Удаление привата — сначала модалка подтверждения (Group modal.svg).</summary>
    private void DeleteClaimCell(int index)
    {
        var claim = GetClaimAt(index);
        if (claim == null || claim.ViewerIsCoOwner)
        {
            return;
        }

        OpenDeleteConfirm(claim);
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
            var title = Lang.Get("swixyclaimchunk:claims-list-name", claim.Name);
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
            SingleComposer.GetTextInput("useFilterSearch")?.SetValue(useFilterSearch, true);
        }
    }

    private void SyncClaimFlagSwitches(ClaimInfoPacket claim)
    {
        // Flag labels live on Group 1346 chrome + optional transparent overlays.
        SingleComposer?.GetCustomDraw("claimFlagPvpBg")?.Redraw();
        SingleComposer?.GetCustomDraw("claimFlagAnimalsBg")?.Redraw();
        SingleComposer?.GetCustomDraw("claimsPageChrome")?.Redraw();
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

    /// <summary>Group 1347 flag plate overlay: checkbox + label (face from panel_settings texture).</summary>
    private void DrawClaimFlagPvpPlate(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var on = (GetSelectedClaim()?.ClaimFlags & ClaimFlagBits.AllowPvp) != 0;
        DrawSettingsFlagOverlay(
            ctx,
            bounds,
            Lang.Get("swixyclaimchunk:claim-flag-pvp"),
            on,
            textX: FlagLabelLocalX,
            textBaselineY: FaceTextBaseline);
    }

    private void DrawClaimFlagAnimalsPlate(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var on = ClaimFlagBits.AreAnimalsProtected(GetSelectedClaim()?.ClaimFlags ?? 0);
        DrawSettingsFlagOverlay(
            ctx,
            bounds,
            Lang.Get("swixyclaimchunk:claim-flag-animals"),
            on,
            textX: FlagLabelLocalX,
            textBaselineY: FaceTextBaseline);
    }

    private static void DrawSettingsFlagOverlay(
        Context ctx,
        ElementBounds bounds,
        string label,
        bool on,
        double textX,
        double textBaselineY)
    {
        var w = bounds.OuterWidth;
        var h = bounds.OuterHeight;
        ctx.Operator = Operator.Source;
        ctx.SetSourceRGBA(0, 0, 0, 0);
        ctx.Rectangle(0, 0, w, h);
        ctx.Fill();
        ctx.Operator = Operator.Over;

        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        // Checkbox Group 1347: 18×18 @ (11,7) inside face, stroke #9F795B w=2.
        DrawGroup1347Checkbox(ctx, FlagCheckLocalX * s, FlagCheckLocalY * s, FlagCheckSize * s, on);
        DrawTextAtBaseline(
            ctx,
            label.ToUpperInvariant(),
            textX * s,
            textBaselineY * s,
            FontSettingsLabel,
            ColSettingsBtn);
    }

    /// <summary>Rounded checkbox from Group 1347 paths (empty stroke / checked with tick).</summary>
    private static void DrawGroup1347Checkbox(Context ctx, double x, double y, double size, bool isOn)
    {
        var r = size * (3.0 / 18.0); // SVG corner radius ~3 on 18px well
        var stroke = Math.Max(1.25, size * (2.0 / 18.0));
        ctx.Save();
        ctx.SetSourceRGBA(ColSettingsBtn[0], ColSettingsBtn[1], ColSettingsBtn[2], 1.0);
        ctx.LineWidth = stroke;
        ctx.LineCap = LineCap.Round;
        ctx.LineJoin = LineJoin.Round;
        RoundRectangle(ctx, x + stroke * 0.5, y + stroke * 0.5, size - stroke, size - stroke, r);
        ctx.Stroke();

        if (isOn)
        {
            // Tick relative to 18px box: (5,9) → (7.67,12) → (13,6)
            var x0 = x + size * (5.0 / 18.0);
            var y0 = y + size * (9.0 / 18.0);
            var x1 = x + size * (7.667 / 18.0);
            var y1 = y + size * (12.0 / 18.0);
            var x2 = x + size * (13.0 / 18.0);
            var y2 = y + size * (6.0 / 18.0);
            ctx.MoveTo(x0, y0);
            ctx.LineTo(x1, y1);
            ctx.LineTo(x2, y2);
            ctx.Stroke();
        }

        ctx.Restore();
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
        deleteModalSurface ??= LoadGuiPng("textures/gui/modal_delete.png", "modal_delete.png");
        deleteCancelBtnSurface ??= LoadGuiPng("textures/gui/btn_modal_cancel.png", "btn_modal_cancel.png");
        deleteConfirmBtnSurface ??= LoadGuiPng("textures/gui/btn_modal_delete.png", "btn_modal_delete.png");
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
        deleteModalSurface?.Dispose();
        deleteModalSurface = null;
        deleteCancelBtnSurface?.Dispose();
        deleteCancelBtnSurface = null;
        deleteConfirmBtnSurface?.Dispose();
        deleteConfirmBtnSurface = null;
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
    /// Group 1012 frame + tab labels / close icon.
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
            // 1:1 when UiW/UiH match PNG; nearest keeps metal corners / wood crisp.
            var sx = w / frameSurface.Width;
            var sy = h / frameSurface.Height;
            ctx.Scale(sx, sy);
            ctx.SetSourceSurface(frameSurface, 0, 0);
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

        // Tab labels: UPPERCASE Minecraft Five (quest title face), centered in plate.
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
        // ✕ close — pixel-art from Group 1344.svg (not a fat stroke X).
        DrawCloseIcon(ctx, s);
    }

    public override void OnMouseDown(MouseEvent args)
    {
        if (TryHandleDeleteConfirmMouseDown(args))
        {
            return;
        }

        claimScrollDragging = false;
        memberScrollDragging = false;
        useFilterScrollDragging = false;

        // Pin menu is open: give it the click first (dropdown sits over tabs/chrome).
        // Closing before OnMouseDown made "Movable" (second row) miss the hit test.
        if (detachListMenu is { IsOpened: true })
        {
            detachListMenu.OnMouseDown(clientApi, args);
            if (!args.Handled && !detachListMenu.IsPositionInside(args.X, args.Y))
            {
                CloseDetachListMenu();
            }

            args.Handled = true;
            return;
        }

        // RMB on top chrome → vanilla Fixed / Movable menu (pin / detach).
        if (args.Button == EnumMouseButton.Right && IsDetachChromeHit(args.X, args.Y))
        {
            OpenDetachListMenu(args.X, args.Y);
            args.Handled = true;
            return;
        }

        // LMB drag window when Movable (skip tabs / close so they still work).
        if (dialogMovable
            && args.Button == EnumMouseButton.Left
            && IsDetachChromeHit(args.X, args.Y)
            && !IsHeaderControlHit(args.X, args.Y)
            && detachListMenu is not { IsOpened: true })
        {
            dialogDragging = true;
            dialogDragStart.Set(args.X, args.Y);
            args.Handled = true;
            return;
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
        if (dialogDragging && SingleComposer?.Bounds != null)
        {
            var scale = Math.Max(0.01f, RuntimeEnv.GUIScale);
            var parent = SingleComposer.Bounds;
            parent.fixedX += (args.X - dialogDragStart.X) / scale;
            parent.fixedY += (args.Y - dialogDragStart.Y) / scale;
            dialogDragStart.Set(args.X, args.Y);
            parent.CalcWorldBounds();
            args.Handled = true;
            return;
        }

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

        base.OnMouseMove(args);
    }

    public override void OnMouseUp(MouseEvent args)
    {
        if (dialogDragging)
        {
            dialogDragging = false;
            PersistDialogPositionIfMovable();
        }

        claimScrollDragging = false;
        memberScrollDragging = false;
        useFilterScrollDragging = false;

        if (deleteConfirmOpen)
        {
            args.Handled = true;
            return;
        }

        base.OnMouseUp(args);
    }

    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
        if (deleteConfirmOpen)
        {
            args.SetHandled(true);
            return;
        }

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

    /// <summary>
    /// Close ✕ from Group 1344.svg — small pixel rects (#836045), outer box ~32×32 @ (884,30).
    /// </summary>
    private static readonly (int X, int Y, int W, int H)[] CloseIconRects =
    [
        // Outer corners
        (884, 30, 5, 5), (911, 30, 5, 5), (884, 57, 5, 5), (911, 57, 5, 5),
        // Main diagonals (5×5 steps)
        (886, 32, 5, 5), (909, 32, 5, 5), (907, 34, 5, 5), (888, 34, 5, 5),
        (890, 36, 5, 5), (905, 36, 5, 5), (892, 38, 5, 5), (903, 38, 5, 5),
        (894, 40, 5, 5), (901, 40, 5, 5), (894, 47, 5, 5), (901, 47, 5, 5),
        (892, 49, 5, 5), (903, 49, 5, 5), (890, 51, 5, 5), (905, 51, 5, 5),
        (888, 53, 5, 5), (907, 53, 5, 5), (886, 55, 5, 5), (909, 55, 5, 5),
        // Center cluster (2×2)
        (899, 45, 2, 2), (898, 46, 2, 2), (898, 44, 2, 2), (900, 44, 2, 2),
        (900, 46, 2, 2), (899, 48, 2, 2), (896, 45, 2, 2), (899, 42, 2, 2),
        (902, 45, 2, 2),
    ];

    /// <summary>Крестик закрытия — pixel-art Group 1344.svg, fill #836045.</summary>
    private static void DrawCloseIcon(Context ctx, double s)
    {
        ctx.Save();
        ctx.SetSourceRGBA(ColCloseIcon[0], ColCloseIcon[1], ColCloseIcon[2], ColCloseIcon[3]);
        foreach (var r in CloseIconRects)
        {
            ctx.Rectangle(r.X * s, r.Y * s, r.W * s, r.H * s);
            ctx.Fill();
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

        // Section titles + dashed underlines (keep dashes; solid line below was the problem).
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

        // Group 1347 settings plates always when a claim is selected (gear stays visible).
        if (GetSelectedClaim() != null)
        {
            DrawSettingsPlates(ctx, s);
        }

        // Group 1349: Search + PublicUse plates when picking public-use blocks.
        if (claimsRightMode == ClaimsRightUseFilter && GetSelectedClaim() != null)
        {
            DrawUseFilterChrome(ctx, s);
        }

        // Left claims scrollbar — always the same (independent of gear mode).
        DrawThinScrollBar(
            ctx,
            Xd(ClaimsScrollX),
            Yd(ClaimsScrollY),
            ClaimsScrollW * s,
            ClaimsScrollH * s,
            s,
            ClaimsScrollY,
            GetClaimScrollThumbDesign);

        // Right band scrollbar: members or use-filter grid (same track X).
        if (claimsRightMode == ClaimsRightUseFilter && useFilterGrid != null)
        {
            DrawThinScrollBar(
                ctx,
                Xd(MembersScrollX),
                Yd(UseGridY),
                MembersScrollW * s,
                UseGridH * s,
                s,
                UseGridY,
                GetUseFilterScrollThumbDesign);
        }
        else
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
    /// Map tab content — Group 1350.svg. Surface origin = PanelX/PanelY.
    /// </summary>
    private void DrawMapPageContent(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        double X(int abs) => (abs - PanelX) * s;
        double Y(int abs) => (abs - PanelY) * s;
        double Xd(double abs) => (abs - PanelX) * s;
        double Yd(double abs) => (abs - PanelY) * s;

        // ----- Interactive map well: (467,215) 475×474 + 2px #121212 -----
        var mx = X(MapX);
        var my = Y(MapY);
        var mw = MapW * s;
        var mh = MapH * s;
        var b = MapBorder * s;
        SetRgb(ctx, ColEdge);
        ctx.Rectangle(mx - b, my - b, mw + 2 * b, mh + 2 * b);
        ctx.Fill();
        ctx.SetSourceRGB(0.05, 0.05, 0.055);
        ctx.Rectangle(mx, my, mw, mh);
        ctx.Fill();

        // Section headers MAP / INTERACTIVE MAP + dashed rules y=187.
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

        // Left column: map each PNG so its measured face rect lands on the design face.
        // Text uses Group 1355 offsets from that face (not from the outer texture box).
        DrawMappedPanelTexture(
            ctx,
            limitsPanelSurface,
            LimitsX,
            LimitsY,
            LimitsFaceW,
            LimitsFaceH,
            LimitsPngFaceX,
            LimitsPngFaceY,
            LimitsPngFaceW,
            LimitsPngFaceH,
            s,
            out var limFaceX,
            out var limFaceY,
            out var limFaceW,
            out var limFaceH);
        DrawMappedPanelTexture(
            ctx,
            legendPanelSurface,
            LegendX,
            LegendY,
            LimitsFaceW,
            LegendDesignH,
            LegendPngFaceX,
            LegendPngFaceY,
            LegendPngFaceW,
            LegendPngFaceH,
            s,
            out var legFaceX,
            out var legFaceY,
            out var legFaceW,
            out var legFaceH);
        DrawMappedPanelTexture(
            ctx,
            centerButtonSurface,
            CenterX,
            CenterY,
            CenterTexW,
            CenterTexH,
            CenterPngFaceX,
            CenterPngFaceY,
            CenterPngFaceW,
            CenterPngFaceH,
            s,
            out var cenFaceX,
            out var cenFaceY,
            out var cenFaceW,
            out var cenFaceH);

        DrawLimitsTextsOnFace(ctx, limFaceX, limFaceY, limFaceW, limFaceH);
        DrawLegendTextsOnFace(ctx, legFaceX, legFaceY, legFaceW, legFaceH);

        var textX = X(CardX) + CardPadX * s;
        var textW = (CardW - CardPadX * 2) * s;
        var msg = mapStatusOverride ?? mapState?.Message ?? "";
        if (!string.IsNullOrWhiteSpace(msg))
        {
            DrawMultilineSurface(ctx, msg, textX, Y(MessageY), textW, FontBody, ClaimFontHelper.ColorAccent, 18 * s);
        }

        DrawCenteredLabelSurface(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-center"),
            cenFaceX,
            cenFaceY,
            cenFaceW,
            cenFaceH,
            FontCenter,
            ColSettingsBtn);
    }

    /// <summary>
    /// Draw a panel PNG so the brown face region in the texture lands exactly on the
    /// design face (faceDesignX/Y/W/H). Outer chrome extends outside that face.
    /// </summary>
    private void DrawMappedPanelTexture(
        Context ctx,
        ImageSurface? tex,
        int faceDesignX,
        int faceDesignY,
        int faceDesignW,
        int faceDesignH,
        int texFaceX,
        int texFaceY,
        int texFaceW,
        int texFaceH,
        double s,
        out double faceDrawX,
        out double faceDrawY,
        out double faceDrawW,
        out double faceDrawH)
    {
        faceDrawX = (faceDesignX - PanelX) * s;
        faceDrawY = (faceDesignY - PanelY) * s;
        faceDrawW = faceDesignW * s;
        faceDrawH = faceDesignH * s;

        if (tex == null || tex.Width <= 0 || tex.Height <= 0 || texFaceW <= 0 || texFaceH <= 0)
        {
            DrawFacePlateChrome(ctx, faceDrawX, faceDrawY, faceDrawW, faceDrawH);
            return;
        }

        var scaleX = faceDrawW / texFaceW;
        var scaleY = faceDrawH / texFaceH;
        var drawW = tex.Width * scaleX;
        var drawH = tex.Height * scaleY;
        var drawX = faceDrawX - texFaceX * scaleX;
        var drawY = faceDrawY - texFaceY * scaleY;
        DrawGuiTexture(ctx, tex, drawX, drawY, drawW, drawH, fallback: ColInset);
    }

    /// <summary>
    /// Fallback swatch when panel_legend.png is missing.
    /// Full face bevel: edge #121212, hi #563E2B, lo #2A1E14 (same as DrawFacePlateChrome).
    /// </summary>
    private static void DrawMapLegendSwatch(Context ctx, double x, double y, double size, double r, double g, double b)
    {
        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var edge = 2 * s;
        var bevel = Math.Max(1.0, 2 * s);

        SetRgb(ctx, ColEdge);
        ctx.Rectangle(x - edge, y - edge, size + edge * 2, size + edge * 2);
        ctx.Fill();

        ctx.SetSourceRGB(r, g, b);
        ctx.Rectangle(x, y, size, size);
        ctx.Fill();

        SetRgb(ctx, ColHi);
        ctx.Rectangle(x, y, size, bevel);
        ctx.Fill();
        ctx.Rectangle(x, y, bevel, size);
        ctx.Fill();

        SetRgb(ctx, ColLo);
        ctx.Rectangle(x, y + size - bevel, size, bevel);
        ctx.Fill();
        ctx.Rectangle(x + size - bevel, y, bevel, size);
        ctx.Fill();
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

    /// <summary>Плашка лимитов — panel_limits.png (bevels included). Fallback: face chrome.</summary>
    private void DrawLimitsPanelTexture(Context ctx, double x, double y, double w, double h)
    {
        if (limitsPanelSurface != null && limitsPanelSurface.Width > 0 && limitsPanelSurface.Height > 0)
        {
            DrawGuiTexture(ctx, limitsPanelSurface, x, y, w, h, fallback: ColInset);
            return;
        }

        DrawFacePlateChrome(ctx, x, y, w, h);
    }

    /// <summary>
    /// Full legend chrome: panel_legend.png (title + rows + swatches with bevels).
    /// Falls back to programmatic plates + swatches if the PNG failed to load.
    /// </summary>
    private void DrawLegendPanelTexture(Context ctx, double x, double y, double w, double h)
    {
        if (legendPanelSurface != null && legendPanelSurface.Width > 0 && legendPanelSurface.Height > 0)
        {
            DrawGuiTexture(ctx, legendPanelSurface, x, y, w, h, fallback: ColInset);
            return;
        }

        // Fallback without PNG — full width of limits, swatches on the right edge.
        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        DrawFacePlateChrome(ctx, x, y, w, LegendTitleH * s);
        var row1y = y + (LegendRow1Y - LegendY) * s;
        var row2y = y + (LegendRow2Y - LegendY) * s;
        var row3y = y + (LegendRow3Y - LegendY) * s;
        var rowH = LegendRowH * s;
        var sw = LegendSwatchSize * s;
        var gap = 6 * s;
        var rowW = w - sw - gap;
        DrawFacePlateChrome(ctx, x, row1y, rowW, rowH);
        DrawFacePlateChrome(ctx, x, row2y, rowW, rowH);
        DrawFacePlateChrome(ctx, x, row3y, rowW, rowH);
        var swX = x + w - sw;
        DrawMapLegendSwatch(ctx, swX, row1y, sw, 0x4F / 255.0, 0x59 / 255.0, 0x52 / 255.0);
        DrawMapLegendSwatch(ctx, swX, row2y, sw, 0x03 / 255.0, 0xE9 / 255.0, 0xFD / 255.0);
        DrawMapLegendSwatch(ctx, swX, row3y, sw, 0xFC / 255.0, 0x4B / 255.0, 0x3C / 255.0);
    }

    /// <summary>
    /// Group 1349.svg use-filter chrome under settings: Search plate + PublicUse:N plate.
    /// </summary>
    private void DrawUseFilterChrome(Context ctx, double s)
    {
        double X(double abs) => (abs - PanelX) * s;
        double Y(double abs) => (abs - PanelY) * s;

        DrawFacePlateChrome(ctx, X(UseSearchX), Y(UseSearchY), UseSearchW * s, UseSearchH * s);
        DrawFacePlateChrome(ctx, X(UseStatusX), Y(UseStatusY), UseStatusW * s, UseStatusH * s);

        if (string.IsNullOrWhiteSpace(useFilterSearch))
        {
            DrawTextAtBaseline(
                ctx,
                Lang.Get("swixyclaimchunk:use-filter-search-placeholder").ToUpperInvariant(),
                X(UseSearchX + FacePadX),
                Y(UseSearchY + FaceTextBaseline),
                FontSettingsLabel,
                ColSettingsPlaceholder);
        }

        DrawTextAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:use-filter-public-count", useFilterDraftCodes.Count).ToUpperInvariant(),
            X(UseStatusX + FacePadX),
            Y(UseStatusY + FaceTextBaseline),
            FontSettingsLabel,
            ColStatChunks);
    }

    /// <summary>Inset face plate (same bevel language as Group 1347 faces).</summary>
    private static void DrawFacePlateChrome(Context ctx, double x, double y, double w, double h)
    {
        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var edge = 4 * s;
        var bevel = 2 * s;
        SetRgb(ctx, ColEdge);
        ctx.Rectangle(x - edge, y - edge, w + edge * 2, h + edge * 2);
        ctx.Fill();
        SetRgb(ctx, ColInset);
        ctx.Rectangle(x, y, w, h);
        ctx.Fill();
        SetRgb(ctx, ColHi);
        ctx.Rectangle(x, y, w, bevel);
        ctx.Fill();
        ctx.Rectangle(x, y, bevel, h);
        ctx.Fill();
        SetRgb(ctx, ColLo);
        ctx.Rectangle(x, y + h - bevel, w, bevel);
        ctx.Fill();
        ctx.Rectangle(x + w - bevel, y, bevel, h);
        ctx.Fill();
    }

    /// <summary>
    /// Group 1347.svg settings block (478×174 @ 465,215) + dynamic text/icons on top.
    /// Surface origin = PanelX/PanelY.
    /// </summary>
    private void DrawSettingsPlates(Context ctx, double s)
    {
        double X(double abs) => (abs - PanelX) * s;
        double Y(double abs) => (abs - PanelY) * s;
        var claim = GetSelectedClaim();

        DrawGuiTexture(
            ctx,
            settingsPanelSurface,
            X(SettingsTexX),
            Y(SettingsTexY),
            SettingsTexW * s,
            SettingsTexH * s,
            fallback: ColInset);

        if (claim != null)
        {
            DrawTextAtBaseline(
                ctx,
                Lang.Get("swixyclaimchunk:claims-stats-areas", claim.AreaCount).ToUpperInvariant(),
                X(StatAreasTextX),
                Y(StatTextBaselineY),
                FontSettingsStats,
                ColStatAreas);
            DrawTextAtBaseline(
                ctx,
                Lang.Get("swixyclaimchunk:claims-stats-chunks", claim.ChunkCount).ToUpperInvariant(),
                X(StatChunksTextX),
                Y(StatTextBaselineY),
                FontSettingsStats,
                ColStatChunks);
        }

        // Gear #FEE4CF, stroke-opacity 0.32 idle / 1.0 when use-filter open (Group 1347).
        ClaimCairoIcons.DrawGear(
            ctx,
            X(SettingsTexX + StatGearIconLocalX),
            Y(SettingsTexY + StatGearIconLocalY),
            StatGearIconSize * s,
            active: claimsRightMode == ClaimsRightUseFilter,
            locked: false,
            r: ColSettingsGear[0],
            g: ColSettingsGear[1],
            b: ColSettingsGear[2],
            inactiveAlpha: 0.32);

        // Flags: also on claimFlag*Bg for live toggle; draw here so first paint is correct.
        var pvpOn = claim != null && (claim.ClaimFlags & ClaimFlagBits.AllowPvp) != 0;
        var animalsOn = claim != null && ClaimFlagBits.AreAnimalsProtected(claim.ClaimFlags);
        DrawGroup1347Checkbox(
            ctx,
            X(FlagPvpX + FlagCheckLocalX),
            Y(FlagPvpY + FlagCheckLocalY),
            FlagCheckSize * s,
            pvpOn);
        DrawGroup1347Checkbox(
            ctx,
            X(FlagAnimalsX + FlagCheckLocalX),
            Y(FlagPvpY + FlagCheckLocalY),
            FlagCheckSize * s,
            animalsOn);
        DrawTextAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claim-flag-pvp").ToUpperInvariant(),
            X(FlagPvpTextX),
            Y(FlagPvpTextBaselineY),
            FontSettingsLabel,
            ColSettingsBtn);
        DrawTextAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claim-flag-animals").ToUpperInvariant(),
            X(FlagAnimalsTextX),
            Y(FlagAnimalsTextBaselineY),
            FontSettingsLabel,
            ColSettingsBtn);

        // Placeholders when inputs empty (SVG RegionName… / PlayerName… @ 0.32).
        if (string.IsNullOrWhiteSpace(claimNameInput))
        {
            DrawTextAtBaseline(
                ctx,
                Lang.Get("swixyclaimchunk:claims-rename-placeholder").ToUpperInvariant(),
                X(RenamePlaceholderX),
                Y(RenamePlaceholderBaselineY),
                FontSettingsLabel,
                ColSettingsPlaceholder);
        }

        if (string.IsNullOrWhiteSpace(memberNameInput))
        {
            DrawTextAtBaseline(
                ctx,
                Lang.Get("swixyclaimchunk:claims-player-placeholder").ToUpperInvariant(),
                X(AddPlaceholderX),
                Y(AddPlaceholderBaselineY),
                FontSettingsLabel,
                ColSettingsPlaceholder);
        }

        // Button labels centered in RENAME / ADD plates.
        DrawTextCenteredAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claims-rename-button").ToUpperInvariant(),
            X(RenameBtnCenterX),
            Y(RenameBtnBaselineY),
            FontSettingsBtn,
            ColSettingsBtnMuted);
        DrawTextCenteredAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claims-add-player").ToUpperInvariant(),
            X(AddBtnCenterX),
            Y(AddBtnBaselineY),
            FontSettingsBtn,
            ColSettingsBtnMuted);
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

    /// <summary>Кнопка «Центр» — button_center.png. Fallback: face chrome.</summary>
    private void DrawCenterButtonTexture(Context ctx, double x, double y, double w, double h)
    {
        if (centerButtonSurface != null && centerButtonSurface.Width > 0 && centerButtonSurface.Height > 0)
        {
            DrawGuiTexture(ctx, centerButtonSurface, x, y, w, h, fallback: ColCenter);
            return;
        }

        DrawFacePlateChrome(ctx, x, y, w, h);
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
    /// Limits text on the mapped face — Group 1355 offsets from face origin (local 4,4).
    /// </summary>
    private void DrawLimitsTextsOnFace(Context ctx, double faceX, double faceY, double faceW, double faceH)
    {
        var ux = faceW / LimitsFaceW;
        var uy = faceH / LimitsFaceH;
        var leftX = faceX + LimitsTextLeftX * ux;
        var valueRight = faceX + LimitsValueRightX * ux;

        DrawTextAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-used").ToUpperInvariant(),
            leftX,
            faceY + LimitsTitleBaselineY * uy,
            FontLimitsTitle,
            ColLimitsTitle);

        GetLimitsValues(mapState, out var chunksUsed, out var chunksMax, out var areasUsed, out var areasMax);
        var y1 = faceY + LimitsLine1BaselineY * uy;
        var y2 = faceY + LimitsLine2BaselineY * uy;
        DrawTextAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-chunks-label"),
            leftX,
            y1,
            FontLimitsBody,
            ClaimFontHelper.ColorAccent);
        DrawTextRightAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-value-ratio", chunksUsed, chunksMax),
            valueRight,
            y1,
            FontLimitsBody,
            ClaimFontHelper.ColorAccent);
        DrawTextAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-areas-label"),
            leftX,
            y2,
            FontLimitsBody,
            ClaimFontHelper.ColorAccent);
        DrawTextRightAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-value-ratio", areasUsed, areasMax),
            valueRight,
            y2,
            FontLimitsBody,
            ClaimFontHelper.ColorAccent);
    }

    /// <summary>
    /// Legend text on the mapped face — Group 1355 offsets from title face (local 4,108).
    /// </summary>
    private void DrawLegendTextsOnFace(Context ctx, double faceX, double faceY, double faceW, double faceH)
    {
        var ux = faceW / LimitsFaceW;
        var uy = faceH / LegendDesignH;
        var leftX = faceX + LegendTextLeftX * ux;
        var maxW = faceW - (LegendSwatchSize + 6 + LegendTextLeftX) * ux;

        DrawTextAtBaseline(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-legend-title").ToUpperInvariant(),
            leftX,
            faceY + LegendTitleBaselineY * uy,
            FontLegendTitle,
            ColLimitsTitle);

        DrawTextAtBaselineClamped(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-legend-free"),
            leftX,
            faceY + LegendRow1BaselineY * uy,
            maxW,
            FontLegendBody,
            ClaimFontHelper.ColorAccent);
        DrawTextAtBaselineClamped(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-legend-own"),
            leftX,
            faceY + LegendRow2BaselineY * uy,
            maxW,
            FontLegendBody,
            ClaimFontHelper.ColorAccent);
        DrawTextAtBaselineClamped(
            ctx,
            Lang.Get("swixyclaimchunk:claim-map-legend-other"),
            leftX,
            faceY + LegendRow3BaselineY * uy,
            maxW,
            FontLegendBody,
            ClaimFontHelper.ColorAccent);
    }

    /// <summary>Baseline text; truncates with … if wider than maxWidth.</summary>
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

    /// <summary>Текст с привязкой к baseline Y (как path M в SVG).</summary>
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
        DrawSectionTitleOnly(ctx, text, leftX, rightX, baselineY);
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

    /// <summary>Section title without dashed underline (claims page).</summary>
    private static void DrawSectionTitleOnly(
        Context ctx,
        string text,
        double leftX,
        double rightX,
        double baselineY)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ClaimFontHelper.SetupMontserrat(ctx, FontSection, ColSection, bold: true);
        var extents = ctx.TextExtents(text);
        var x = leftX + (rightX - leftX - extents.Width) * 0.5 - extents.XBearing;
        ctx.MoveTo(x, baselineY);
        ctx.ShowText(text);
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
    /// Tab plate label: Minecraft Five (quest title face), centered, slight downward nudge.
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

        ClaimFontHelper.SetupTitle(ctx, designFontSize, color);
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
