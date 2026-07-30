namespace SwixyQuestBook.Gui
{
    public static class QuestbookGuiLayout
    {
        // Основное окно
        public const double MainWidth = 1583;
        public const double MainHeight = 824;
        public const double MainOffsetX = 0;
        public const double MainOffsetY = 0;
        public static readonly double[] ScreenDimmingColor = { 0.0705882353, 0.0823529412, 0.0941176471, 0.64 };

        // Фон книги (background.png / Background.svg = 1583×769).
        // Левая колонка текстуры ~x 0..350, лицо книги внутри рамки.
        public const double BackgroundOffsetY = 55;
        public const double BackgroundWidth = 1583;
        public const double BackgroundHeight = 769;

        // Верхнее меню
        public const double TopMenuHeight = 39;
        public const double TopMenuWidth = 1583;
        public const double TopMenuFontSize = 32;
        public const double TopMenuLineHeight = 39;
        public const double TopMenuPaddingX = 40;
        public const double TopMenuTitleGap = 16;
        public const double TopMenuSectionGap = 16;
        public const double TopMenuProgressGap = 26;
        public const double TopMenuCloseGap = 16;
        public const double TopMenuDetachGap = 12;
        public const double TopMenuDetachButtonSize = 32;
        public const double TopMenuDetachIconSize = 24;
        public const double TopMenuRightPadding = 40;

        public static readonly double[] TopMenuTitleColor = { 0.9803921569, 1.0, 0.9921568627, 1.0 };
        public static readonly double[] TopMenuSeparatorColor = { 0.9803921569, 1.0, 0.9921568627, 1.0 };
        public static readonly double[] TopMenuSectionColor = { 0.3529411765, 0.9843137255, 0.3411764706, 1.0 };
        public static readonly double[] TopMenuCloseColor = { 0.6823529412, 0.6823529412, 0.6823529412, 1.0 };
        public static readonly double[] TopMenuCloseHoverColor = { 0.9098039216, 0.9098039216, 0.9098039216, 1.0 };
        public static readonly double[] TopMenuDetachColor = { 0.6823529412, 0.6823529412, 0.6823529412, 1.0 };
        public static readonly double[] TopMenuDetachHoverColor = { 0.9098039216, 0.9098039216, 0.9098039216, 1.0 };
        public static readonly double[] TopMenuDetachActiveColor = { 0.3529411765, 0.9843137255, 0.3411764706, 1.0 };
        public static readonly double[] TopMenuCloseHotkeyColor = { 0.9921568627, 0.3529411765, 0.3254901961, 1.0 };
        public static readonly double[] TopMenuProgressDoneColor = { 0.3529411765, 0.9843137255, 0.3411764706, 1.0 };
        public static readonly double[] TopMenuProgressZeroColor = { 0.9921568627, 0.3529411765, 0.3254901961, 1.0 };
        public static readonly double[] TopMenuProgressActiveColor = { 1.0, 0.9960784314, 0.3372549020, 1.0 };

        // Боковая панель — Group 671.svg (289×655 panel-local, admin BRANCHES).
        // Book face (209,221); left column 350×724; selected card 278×40;
        // icon (8,8) 24×24; next cards every 44px → gap 4.
        // background.png 1583×769 has 19px frame pad → face at texture (19,19).
        // Dialog: bg at Y=BackgroundOffsetY → card = (19+35, 55+19+35) = (54, 109).
        public const double BackgroundFramePad = 19;
        public const double BackgroundLeftColumnWidth = 350;
        public const double SidebarCardOffsetX = BackgroundFramePad + 35; // 54
        public const double SidebarCardOffsetY = BackgroundOffsetY + BackgroundFramePad + 35; // 109
        public const double SidebarCardWidth = 278;
        public const double SidebarCardHeight = 40;
        public const double SidebarCardGap = 4; // step 44
        // Group 671 panel height (and full-book left face usable height).
        public const double SidebarViewportHeight = 655;
        public const double SidebarScrollbarGap = 6;
        public const double SidebarScrollbarWidth = 4;
        public const double SidebarScrollbarOffsetX = 0;
        public const double SidebarScrollbarOffsetY = 0;
        // Branch card icons — Group 671: 24×24 at (8,8) in the 40px row.
        public const double SidebarIconSize = 24;
        public const double SidebarIconOffsetX = 8;
        /// <summary>Gap between icon right edge and branch title (title starts ~x42).</summary>
        public const double SidebarTitleGap = 10;
        public const double SidebarTitleOffsetX = SidebarIconOffsetX + SidebarIconSize + SidebarTitleGap; // 42
        public const double SidebarFontSize = 14; // SVG title glyph ~h14
        public const double SidebarLineHeight = 18;
        public const double SidebarProgressRightPadding = 12;
        public static readonly double[] SidebarTitleColor = { 0.3529411765, 0.9843137255, 0.3411764706, 1.0 }; // #5AFB57
        public static readonly double[] SidebarScrollbarTrackColor = { 0.2705882353, 0.2000000000, 0.1411764706, 1.0 }; // #453324
        public static readonly double[] SidebarScrollbarThumbColor = { 0.6000000000, 0.4941176471, 0.4078431373, 1.0 }; // #997E68

        // Admin left panel — Group 671.svg (289×655): mode 139×69 gap 10;
        // CREATE @ y=82.5, EDIT 126.5, DELETE 170.5 (288×39, gap 5);
        // header y≈258; cards from y=286; EXIT @ y=615.5 (288×39).
        public const double SidebarEditButtonHeight = 48;
        /// <summary>Top mode tiles BRANCHES | QUESTS (Group 671: 139×69).</summary>
        public const double SidebarAdminModeBarHeight = 69;
        /// <summary>Gap under mode row before CREATE (82.5 − 69.5 ≈ 13).</summary>
        public const double SidebarAdminModeBarGap = 13;
        /// <summary>Horizontal gap between mode tiles (Group 671: 10px).</summary>
        public const double SidebarAdminModeButtonGap = 10;
        /// <summary>Left inset of admin buttons from left-column face (Group 1150: 244.5−209).</summary>
        public const double SidebarAdminPanelPadLeft = 35;
        /// <summary>Right inset of admin buttons from left-column face (350−35−288≈27).</summary>
        public const double SidebarAdminPanelPadRight = 27;
        /// <summary>Full-width admin chrome (Group 671: 288px; cards 278).</summary>
        public const double SidebarAdminButtonWidth = 288;
        public const double SidebarAdminModeTileWidth = 139;
        public const double SidebarAdminQuestContentOffsetY = SidebarAdminModeBarHeight + SidebarAdminModeBarGap; // 82
        // Quest tools — full-width 39px rows (SELECT…DELETE), then status, SAVE, EXIT.
        public const double SidebarAdminToolbarButtonHeight = 39;
        public const double SidebarAdminToolbarButtonGap = 5;
        public const int SidebarAdminToolbarColumns = 1;
        public const int SidebarAdminToolbarButtonCount = 6; // Select, New, Link, Grid, Edit, Delete
        public const int SidebarAdminToolbarRows = SidebarAdminToolbarButtonCount;
        public const double SidebarAdminToolbarHeight =
            (SidebarAdminToolbarButtonHeight * SidebarAdminToolbarRows)
            + (SidebarAdminToolbarButtonGap * (SidebarAdminToolbarRows - 1));
        /// <summary>Gray hint under tools (SVG ~y353–366, #555555), centered.</summary>
        public const double SidebarAdminQuestStatusOffsetY = 353;
        public const double SidebarAdminQuestStatusHeight = 16;
        public const double SidebarAdminQuestStatusFontSize = 12;
        /// <summary>SAVE row (SVG y=571.5), green centered label.</summary>
        public const double SidebarAdminQuestSaveOffsetY = 571.5;
        /// <summary>EXIT row (Group 671 y=615.5), red centered label.</summary>
        public const double SidebarAdminQuestExitOffsetY = 615.5;
        /// <summary>Branch action rows CREATE / EDIT / DELETE (288×39, gap 5).</summary>
        public const double SidebarAdminBranchActionHeight = 39;
        public const double SidebarAdminBranchActionGap = 5;
        public const int SidebarAdminBranchActionCount = 3;
        public const double SidebarAdminBranchActionsHeight =
            (SidebarAdminBranchActionHeight * SidebarAdminBranchActionCount)
            + (SidebarAdminBranchActionGap * (SidebarAdminBranchActionCount - 1));
        public const double SidebarAdminStatusHeight = 22;
        public const double SidebarAdminSelectedHintY = 8;
        /// <summary>Gray section label above the branch list (Group 671 ~y257.8 h12).</summary>
        public const double SidebarAdminBranchListHeaderOffsetY = 258;
        public const double SidebarAdminBranchListHeaderHeight = 14;
        public const double SidebarAdminBranchListHeaderFontSize = 12;
        /// <summary>First branch card top (Group 671 y=286, 278×40).</summary>
        public const double SidebarAdminBranchListOffsetY = 286;
        /// <summary>
        /// Bottom of the branch list / scrollbar track (Group 671: scroll track
        /// rotate -90 at (285,551) length 265 → ends at y=551). EXIT is at 615.5,
        /// so empty gap below the list is ~64.5px — not flush to EXIT.
        /// </summary>
        public const double SidebarAdminBranchListBottomY = 551;
        /// <summary>List viewport height (551 − 286 = 265).</summary>
        public const double SidebarAdminBranchListHeight =
            SidebarAdminBranchListBottomY - SidebarAdminBranchListOffsetY;
        /// <summary>EXIT sits at y=615.5 in 655 → 39.5 from bottom; use 39.</summary>
        public const double SidebarAdminBranchCloseOffsetFromBottom = SidebarAdminBranchActionHeight;
        /// <summary>Gap between list bottom (551) and EXIT top (615.5) = 64.5.</summary>
        public const double SidebarAdminBranchListBottomGap =
            SidebarAdminQuestExitOffsetY - SidebarAdminBranchListBottomY;
        /// <summary>
        /// Admin button column width (Group 671 = 288).
        /// Branch list cards stay <see cref="SidebarCardWidth"/> (278).
        /// </summary>
        public const double SidebarAdminPanelWidth = SidebarAdminButtonWidth;
        public const double SidebarAdminEmptyHintY = 12;
        /// <summary>EXIT label color (SVG #FD5A53).</summary>
        public static readonly double[] AdminExitButtonColor = { 253.0 / 255.0, 90.0 / 255.0, 83.0 / 255.0, 1.0 };
        /// <summary>Branch list section header (SVG #555555).</summary>
        public static readonly double[] AdminBranchListHeaderColor = { 85.0 / 255.0, 85.0 / 255.0, 85.0 / 255.0, 1.0 };

        // Quest edit modal — Group 1169.svg + modal_questedit.png (1080×608).
        public const double QuestEditModalWidth = 1080;
        public const double QuestEditModalHeight = 608;
        public const string QuestEditModalTexture = "modal_questedit.png";
        /// <summary>Group 767 path cache (binary polygons, design 950×87, color #2D2D2D).</summary>
        public const string QuestEditTextFieldBorderPaths = "group_767.bin";
        public const double QuestEditTextFieldBorderSrcW = 950;
        public const double QuestEditTextFieldBorderSrcH = 87;
        /// <summary>Line 31.png — vertical divider strip between GOALS and AWARDS (7×143).</summary>
        public const string QuestEditWaveDividerTexture = "line_31.png";
        /// <summary>Absolute design X of the divider center (goals right 501.5, awards left 562.5 → mid ≈532; SVG wave ~540).</summary>
        public const double QuestEditModalWaveDividerX = 532;
        /// <summary>Divider texture design width (Line 31 is 7px wide).</summary>
        public const double QuestEditModalWaveDividerWidth = 7;
        /// <summary>Legacy offset — quest-edit is now centered in the graph area.</summary>
        public const double QuestEditModalOffsetX = 0;
        /// <summary>Content left (Group 1169: type/save at x=70.5).</summary>
        public const double QuestEditModalPadX = 70.5;
        /// <summary>Space above title (title text ~y70–87).</summary>
        public const double QuestEditModalPadTop = 50;
        /// <summary>Group 1214 left header «Selecting A Quest» (white, ~x5 y1.5 h20 in 950 content).</summary>
        public const double QuestEditModalHeaderTitleX = 75;
        public const double QuestEditModalHeaderTitleY = 48;
        public const double QuestEditModalHeaderTitleHeight = 22;
        public const double QuestEditModalHeaderTitleFontSize = 16;
        /// <summary>Group 1214 right id «Quest #0» (#555, ~x863 y3 h14).</summary>
        public const double QuestEditModalHeaderIdY = 50;
        public const double QuestEditModalHeaderIdHeight = 16;
        public const double QuestEditModalHeaderIdFontSize = 13;
        public static readonly double[] QuestEditModalHeaderIdColor = { 85.0 / 255.0, 85.0 / 255.0, 85.0 / 255.0, 1.0 };
        /// <summary>Space below SAVE (SAVE bottom 538.5 → 608−538.5=69.5).</summary>
        public const double QuestEditModalPadBottom = 69.5;
        /// <summary>Legacy alias — prefer <see cref="QuestEditModalPadX"/>.</summary>
        public const double QuestEditModalPadding = QuestEditModalPadX;
        /// <summary>Content width for type bar / SAVE (939).</summary>
        public const double QuestEditModalContentWidth = 939;
        /// <summary>Type chips START|QUEST|CHECKPOINT|KILL (231×39, gap 5).</summary>
        public const double QuestEditModalTypeBarY = 106.5;
        public const double QuestEditModalTypeBarHeight = 39;
        public const double QuestEditModalTypeButtonWidth = 231;
        public const double QuestEditModalTypeButtonGap = 5;
        public const double QuestEditModalSectionGap = 10;
        /// <summary>Repeat row under description text field (above SAVE).</summary>
        public const double QuestEditModalRepeatRowGap = 6;
        public const double QuestEditModalRepeatRowHeight = 28;
        /// <summary>Top flag legend (Group 896: 18×18 chips next to header title).</summary>
        public const double QuestEditModalLegendY = 48;
        public const double QuestEditModalLegendIconSize = 18;
        /// <summary>
        /// Legend chips — Group 896 content x (339 / 430 / 511) + PadX 70.5 → modal X.
        /// Purple Create (take) · cyan Craft (paperclip/craft) · yellow All Types (variants).
        /// </summary>
        public const double QuestEditModalLegendTakeX = QuestEditModalPadX + 339; // 409.5 Create
        public const double QuestEditModalLegendCraftX = QuestEditModalPadX + 430; // 500.5 Craft
        public const double QuestEditModalLegendVariantsX = QuestEditModalPadX + 511; // 581.5 All Types
        public const double QuestEditModalLegendLabelGap = 6;
        /// <summary>GOALS / AWARDS section titles (pixel text ~y165–179).</summary>
        public const double QuestEditModalListHeaderY = 165;
        public const double QuestEditModalListHeaderHeight = 16;
        /// <summary>Document+plus add buttons (SVG 482–498 / 974–990 × 165–181).</summary>
        public const double QuestEditModalAddButtonY = 165;
        public const double QuestEditModalAddButtonSize = 16;
        public const double QuestEditModalGoalsAddX = 482;
        public const double QuestEditModalAwardsAddX = 974;
        /// <summary>GOALS / AWARDS panels (431×111 at y=191.5, gap 61).</summary>
        public const double QuestEditModalListPanelY = 191.5;
        public const double QuestEditModalListHeight = 111;
        public const double QuestEditModalListColumnWidth = 431;
        public const double QuestEditModalListColumnGap = 61;
        /// <summary>Row chips are 31×31; step 34 (197.5 → 231.5 → 265.5).</summary>
        public const double QuestEditModalRowHeight = 31;
        public const double QuestEditModalRowGap = 3;
        public const double QuestEditModalChipSize = 31;
        public const double QuestEditModalChipRadius = 5.5;
        /// <summary>
        /// GOALS row (Group 1171.svg) panel-local:
        /// pick 6.5 · name 44.5→240.5 · qty→271.5 · craft 288.5 · variants 322.5 · take 356.5 · del 390.5
        /// </summary>
        public const double QuestEditModalChipPadX = 6;
        public const double QuestEditModalNameFrameX = 44;
        public const double QuestEditModalNameJoinX = 240;
        public const double QuestEditModalQtyRightX = 271;
        // Goal flag chips — Group 896: Create/take 288.5 · Craft 322.5 · All Types 356.5 · delete 390.5
        public const double QuestEditModalTakeX = 288;
        public const double QuestEditModalCraftX = 322;
        public const double QuestEditModalVariantsX = 356;
        public const double QuestEditModalDeleteX = 390;
        /// <summary>
        /// AWARDS row — name + qty only, then delete (no take chip).
        /// pick · name → join · qty → right · gap · delete@390
        /// </summary>
        public const double QuestEditModalAwardsNameJoinX = 333;
        public const double QuestEditModalAwardsQtyRightX = 373;
        /// <summary>
        /// KILL goals row (Group 1171 (2).svg) panel-local — no craft/take:
        /// pick 6 · name 44→308 · qty→339 · variants@356 · del 390
        /// </summary>
        public const double QuestEditModalKillNameJoinX = 308;
        public const double QuestEditModalKillQtyRightX = 339;
        /// <summary>Legacy alias — qty field starts at the dual-frame join (goals).</summary>
        public const double QuestEditModalQtyX = QuestEditModalNameJoinX;
        public const double QuestEditModalMatchToggleSize = 31;
        public const double QuestEditModalInfoHeight = 0;
        public const double QuestEditModalCloseButtonHeight = 39;
        public const double QuestEditModalSaveY = 499.5;
        public const double QuestEditModalSaveWidth = 939;
        public const double QuestEditModalNumInputWidth = 31;
        public const double QuestEditModalRemoveButtonWidth = 31;
        public const double QuestEditModalPickSlotSize = 31;
        /// <summary>Group 1172.svg item picker panel (939×221) over lower modal content.</summary>
        public const double QuestEditModalPickerPanelHeight = 221;
        public const double QuestEditModalPickerPanelY = 317.5; // aligns with lang row top
        /// <summary>Search bar inset (panel-local): dual field + CANCEL.</summary>
        public const double QuestEditModalPickerSearchInsetX = 6;
        public const double QuestEditModalPickerSearchInsetY = 6;
        public const double QuestEditModalPickerSearchHeight = 31;
        public const double QuestEditModalPickerSearchWidth = 783;
        public const double QuestEditModalPickerCancelX = 806;
        public const double QuestEditModalPickerCancelWidth = 127;
        /// <summary>Item grid: 59×59 slots, step 64, 14 columns (Group 1172).</summary>
        public const double QuestEditModalPickerSlotSize = 59;
        public const double QuestEditModalPickerSlotGap = 5;
        public const double QuestEditModalPickerSlotStep = 64;
        public const double QuestEditModalPickerGridY = 59; // 308.5 − 249.5
        public const int QuestEditModalPickerColumns = 14;
        public const double QuestEditModalPickerScrollbarWidth = 4;
        public const double QuestEditModalPickerScrollbarX = 929.5; // panel-local
        /// <summary>Scrollbar outside panel (goals right 501.5 → track x=514).</summary>
        public const double QuestEditModalListScrollbarWidth = 4;
        public const double QuestEditModalListScrollbarOutside = 12.5;
        /// <summary>Language chips (46×28, step 49) two rows — under goals for Quest/Kill.</summary>
        public const double QuestEditModalLangY = 317.5;
        /// <summary>
        /// Group 1174 checkpoint layout: langs right under type bar (SVG y=130.5 → modal ~162.5).
        /// Used for Start / Checkpoint (no goals-awards lists).
        /// </summary>
        public const double QuestEditModalCheckpointLangY = 162.5;
        public const double QuestEditModalLangChipWidth = 46;
        public const double QuestEditModalLangChipHeight = 28;
        public const double QuestEditModalLangChipStep = 49;
        public const double QuestEditModalLangRowGap = 3;

        // Branch create/edit modal — Group 1208.svg + group_1192.png (558×484) + group_714.png buttons.
        public const double AddBranchModalWidth = 558;
        public const double AddBranchModalHeight = 484;
        public const string AddBranchModalTexture = "group_1192.png";
        public const string AddBranchModalButtonTexture = "group_714.png";
        public const double AddBranchModalTitleY = 70;
        /// <summary>Subtitle under title — Group 1208 gray “Name [LANG]” at y≈98–112.</summary>
        public const double AddBranchModalSubtitleY = 100;
        public const double AddBranchModalLangX = 84.5;
        public const double AddBranchModalLangY = 148.5;
        public const double AddBranchModalLangChipWidth = 46;
        public const double AddBranchModalLangChipHeight = 28;
        public const double AddBranchModalLangStepX = 49;
        public const double AddBranchModalLangStepY = 31; // 179.5 − 148.5
        public const int AddBranchModalLangColumns = 8;
        /// <summary>Group 1208 wavy frame around languages (#2D2D2D paths ~71–489 × 138–280).</summary>
        public const double AddBranchModalLangFrameX = 71;
        public const double AddBranchModalLangFrameY = 138;
        public const double AddBranchModalLangFrameW = 418;
        public const double AddBranchModalLangFrameH = 142;
        /// <summary>Extra thickness for branch lang frame vs thin text fields.</summary>
        public const double AddBranchModalLangFrameThickness = 2.15;
        /// <summary>Labels above name/icon — Group 1208 pixel text #AEAEAE at y≈296–308.</summary>
        public const double AddBranchModalNameLabelX = 83;
        public const double AddBranchModalNameLabelY = 296;
        public const double AddBranchModalIconLabelX = 350;
        public const double AddBranchModalIconLabelY = 296;
        public const double AddBranchModalNameX = 71.5;
        public const double AddBranchModalNameY = 314.5;
        public const double AddBranchModalNameWidth = 363;
        public const double AddBranchModalNameHeight = 35;
        public const double AddBranchModalIconX = 447.5;
        public const double AddBranchModalIconY = 314.5;
        public const double AddBranchModalIconSize = 35;
        public const double AddBranchModalButtonY = 374;
        public const double AddBranchModalButtonWidth = 203;
        public const double AddBranchModalButtonHeight = 40;
        public const double AddBranchModalPrimaryButtonX = 71;
        public const double AddBranchModalCancelButtonX = 284;
        /// <summary>Legacy aliases used by catalog overlay / old layout helpers.</summary>
        public const double AddBranchModalLangRowHeight = AddBranchModalLangChipHeight;
        public const double AddBranchModalLangRowGap = 3;
        public const double AddBranchModalItemSlotSize = 44;
        public const double AddBranchModalItemSlotGap = 4;
        public const int AddBranchModalItemColumns = 8;
        public const double AddBranchModalItemGridHeight = 200;
        public const double AddBranchModalSearchHeight = 28;
        public const double AddBranchModalPickerHeight = 280;
        public const double AddBranchModalPadding = 40;
        public const double AddBranchModalPadX = 71.5;
        public const double AddBranchModalPadTop = 70;
        public const double AddBranchModalPadBottom = 70;
        public const double AddBranchModalInputHeight = AddBranchModalNameHeight;
        public const double AddBranchModalButtonGap = 10;

        // Граф квестов — справа от левой колонки (~350px).
        // Inset from book divider textures so the panel doesn't overlap seams:
        // +10 left, −5 right, +5 top, −5 bottom.
        // All floating modals (quest / start / branch / quest-edit) are centered in this rect.
        public const double GraphViewportX = 393; // was 383 (+10 left)
        public const double GraphViewportY = 118; // was 113 (+5 top)
        public const double GraphViewportWidth = 1139; // was 1154 (−10 left −5 right)
        public const double GraphViewportHeight = 647; // was 657 (−5 top −5 bottom)

        /// <summary>Dialog-local X that centers a panel of the given design width in the graph area.</summary>
        public static double CenterInGraphX(double panelWidth) =>
            GraphViewportX + ((GraphViewportWidth - panelWidth) / 2.0);

        /// <summary>Dialog-local Y that centers a panel of the given design height in the graph area.</summary>
        public static double CenterInGraphY(double panelHeight) =>
            GraphViewportY + ((GraphViewportHeight - panelHeight) / 2.0);
        public const double GraphNodeSize = 114;
        public const double AdminGraphGridStep = GraphNodeSize;
        public const double GraphStartNodeSize = 114;
        public const double GraphNodeItemIconSize = 32;
        public const double GraphLineThickness = 10;
        public const double GraphStartToQuestDistance = 64;
        public const double GraphQuestToQuestDistance = 36;
        public const double GraphQuestToCheckpointDistance = 100;
        public const double GraphNodeCenterOffset = (GraphStartNodeSize - GraphNodeSize) / 2;
        public const double GraphStartToQuestSpan = GraphStartNodeSize + GraphStartToQuestDistance - GraphNodeCenterOffset;
        public const double GraphQuestToQuestSpan = GraphNodeSize + GraphQuestToQuestDistance;
        public const double GraphQuestToCheckpointSpan = GraphNodeSize + GraphQuestToCheckpointDistance;

        // ── Player modal shell (Group 1213.svg) — panel-local coords, 781×596 ──
        // Shared by quest/kill (goals+awards) and start/checkpoint (large text body).
        public const string ModalQuestTexture = "modal.png";
        public const double ModalPanelWidth = 781;
        public const double ModalPanelHeight = 596;
        /// <summary>Dialog-local origin: centered in <see cref="GraphViewportX"/>… area.</summary>
        public static double ModalPanelX => CenterInGraphX(ModalPanelWidth);
        public static double ModalPanelY => CenterInGraphY(ModalPanelHeight);
        public const double ModalPadX = 81;
        public const double ModalWidth = 619;
        public const double ModalHeight = ModalPanelHeight;
        public const double ModalPadding = ModalPadX;
        public const double ModalIconBoxSize = 72;
        public const double ModalIconSize = 64;
        public const double ModalIconGap = 6;
        /// <summary>Group 1212.png claim button (full chrome 619×67 at 81,459).</summary>
        public const string ModalQuestButtonTexture = "button_quest.png";
        public const string ModalQuestButtonActiveTexture = "button_quest_active.png";
        public const string ModalQuestButtonCompletedTexture = "button_quest_completed.png";
        public const double ModalButtonWidth = 619;
        public const double ModalButtonHeight = 67;
        // Text bands from Group 1213 path bboxes (panel-local).
        // Status «QUEST:» #555 y=70 h=21 + «0%» #AAA — centered as a pair (cx≈390).
        public const double ModalQuestStatusX = 0;
        public const double ModalQuestStatusY = 70;
        public const double ModalQuestStatusWidth = ModalPanelWidth;
        public const double ModalQuestStatusHeight = 21;
        public const double ModalQuestStatusFontSize = 16;
        // Title «Sticks» white y=106 h=28, cx≈388.5.
        public const double ModalTitleX = 0;
        public const double ModalTitleY = 106;
        public const double ModalTitleWidth = ModalPanelWidth;
        public const double ModalTitleHeight = 28;
        public const double ModalTitleFontSize = 24;
        /// <summary>
        /// Goals+Awards share one wavy outer frame (Group 1213 ~81,162 619×147)
        /// with a vertical wavy divider at x≈388.
        /// </summary>
        public const double ModalSectionsFrameX = 81;
        public const double ModalSectionsFrameY = 162;
        public const double ModalSectionsFrameWidth = 619;
        public const double ModalSectionsFrameHeight = 147;
        /// <summary>Absolute panel X of the vertical wave divider center.</summary>
        public const double ModalSectionsDividerX = 388;
        public const double ModalSectionsDividerWidth = 7;
        /// <summary>Inset so the divider meets the outer wave without overshooting corners.</summary>
        public const double ModalSectionsDividerInsetY = 4;
        public const string ModalSectionsDividerTexture = "line_31.png";
        // Content columns inside the shared frame.
        public const double ModalGoalsBoxX = ModalSectionsFrameX;
        public const double ModalGoalsBoxY = ModalSectionsFrameY;
        public const double ModalGoalsBoxWidth = ModalSectionsDividerX - ModalSectionsFrameX; // 307
        public const double ModalGoalsBoxHeight = ModalSectionsFrameHeight;
        public const double ModalRewardsBoxX = ModalSectionsDividerX;
        public const double ModalRewardsBoxY = ModalSectionsFrameY;
        public const double ModalRewardsBoxWidth = ModalSectionsFrameX + ModalSectionsFrameWidth - ModalSectionsDividerX; // 312
        public const double ModalRewardsBoxHeight = ModalSectionsFrameHeight;
        public const double ModalColumnGap = 0;
        // Goals/Awards labels y=176.5 h=17.5 → offset from box top 14.5.
        public const double ModalSectionLabelOffsetY = 14.5;
        public const double ModalSectionLabelHeight = 18;
        public const double ModalSectionHeaderFontSize = 14;
        // Hints «(Info…)» y=199.8 h=12.3 → offset 37.8.
        public const double ModalSectionHintOffsetY = 37.8;
        public const double ModalSectionHintHeight = 13;
        public const double ModalSectionHintFontSize = 11;
        // Icons y=231 (offset 69), 64×64.
        public const double ModalSectionIconOffsetY = 69;
        // Description frame 81,321 619×108; text body y=330.8 h=84, x=93.
        public const double ModalDescriptionBoxX = 81;
        public const double ModalDescriptionBoxY = 321;
        public const double ModalDescriptionBoxWidth = 619;
        public const double ModalDescriptionBoxHeight = 108;
        public const double ModalDescriptionPadX = 12; // 93 − 81
        public const double ModalDescriptionPadY = 10; // 330.8 − 321
        public const double ModalDescriptionFontSize = 12;
        public const double ModalDescriptionLineHeight = 15;
        // Button chrome Group 1212: 81,459 619×67 (inner face was 87,465 607×55).
        // Label #666 y=482.6 h=22.4, centered in the chrome.
        public const double ModalQuestButtonX = 81;
        public const double ModalQuestButtonY = 459;
        public const double ModalQuestButtonFontSize = 18;
        public const double ModalCloseX = 656;
        public const double ModalCloseY = 82;
        public const double ModalCloseSize = 44;

        // ── Start / checkpoint — same shell as quest, one tall text frame ──────
        // Covers the Goals|Awards band + description band (y 162 → 429).
        public const double ModalInfoBoxX = ModalSectionsFrameX;
        public const double ModalInfoBoxY = ModalSectionsFrameY;
        public const double ModalInfoBoxWidth = ModalSectionsFrameWidth;
        public const double ModalInfoBoxHeight =
            (ModalDescriptionBoxY + ModalDescriptionBoxHeight) - ModalSectionsFrameY; // 267
        public const double ModalInfoPadX = 16;
        public const double ModalInfoPadY = 16;
        public const double ModalInfoFontSize = 13;
        public const double ModalInfoLineHeight = 18;
        public const int ModalStartDescriptionMaxLength = 900;
        // Legacy aliases (same shell as quest modal).
        public const string ModalStartTexture = ModalQuestTexture;
        public const double ModalStartPanelWidth = ModalPanelWidth;
        public const double ModalStartPanelHeight = ModalPanelHeight;
        public static double ModalStartPanelX => ModalPanelX;
        public static double ModalStartPanelY => ModalPanelY;
        public const double ModalStartInfoBoxX = ModalInfoBoxX;
        public const double ModalStartInfoBoxY = ModalInfoBoxY;
        public const double ModalStartInfoBoxWidth = ModalInfoBoxWidth;
        public const double ModalStartInfoBoxHeight = ModalInfoBoxHeight;
        public const double ModalStartButtonX = ModalQuestButtonX;
        public const double ModalStartButtonY = ModalQuestButtonY;
        public const double ModalStartButtonWidth = ModalButtonWidth;
        public const double ModalStartButtonHeight = ModalButtonHeight;
        public const double ModalStartCloseX = ModalCloseX;
        public const double ModalStartCloseY = ModalCloseY;
        public const double ModalStartCloseSize = ModalCloseSize;

        // Group 1213 palette (quest)
        public static readonly double[] ModalQuestLabelColor = { 85.0 / 255.0, 85.0 / 255.0, 85.0 / 255.0, 1.0 }; // #555555
        public static readonly double[] ModalProgressZeroColor = { 170.0 / 255.0, 170.0 / 255.0, 170.0 / 255.0, 1.0 }; // #AAAAAA
        public static readonly double[] ModalProgressActiveColor = { 255.0 / 255.0, 254.0 / 255.0, 86.0 / 255.0, 1.0 };
        public static readonly double[] ModalProgressDoneColor = { 90.0 / 255.0, 251.0 / 255.0, 87.0 / 255.0, 1.0 };
        public static readonly double[] ModalGoalsHeaderColor = { 85.0 / 255.0, 1.0, 1.0, 1.0 }; // #55FFFF
        public static readonly double[] ModalAwardsHeaderColor = { 1.0, 170.0 / 255.0, 0.0, 1.0 }; // #FFAA00
        public static readonly double[] ModalSectionHintColor = { 85.0 / 255.0, 85.0 / 255.0, 85.0 / 255.0, 1.0 };
        public static readonly double[] ModalDescriptionTextColor = { 170.0 / 255.0, 170.0 / 255.0, 170.0 / 255.0, 1.0 };
        public static readonly double[] ModalStartInfoTextColor = { 1.0, 1.0, 85.0 / 255.0, 1.0 };
        public static readonly double[] ModalStartButtonTextColor = { 235.0 / 255.0, 235.0 / 255.0, 235.0 / 255.0, 1.0 };
        public static readonly double[] ModalButtonIdleTextColor = { 102.0 / 255.0, 102.0 / 255.0, 102.0 / 255.0, 1.0 }; // #666666
        public static readonly double[] ModalOverlayColor = { 0.0, 0.0, 0.0, 0.76 };
        public static readonly double[] ModalBorderColor = { 0.1764705882, 0.1098039216, 0.0745098039, 1.0 };
        public static readonly double[] ModalBackgroundColor = { 20.0 / 255.0, 20.0 / 255.0, 20.0 / 255.0, 1.0 };
        public static readonly double[] ModalBodyTextColor = { 0.85, 0.85, 0.85, 1.0 };
        public static readonly double[] ModalButtonDisabledColor = { 63.0 / 255.0, 63.0 / 255.0, 63.0 / 255.0, 1.0 }; // #3F3F3F
        public static readonly double[] ModalButtonActiveColor = { 0.3529411765, 0.9843137255, 0.3411764706, 1.0 };
        public static readonly double[] ModalButtonActiveTextColor = { 0.1215686275, 0.1215686275, 0.1215686275, 1.0 };
        public static readonly double[] ModalButtonDisabledTextColor = { 0.9803921569, 1.0, 0.9921568627, 1.0 };

        public static readonly double[] GraphCubeTopColor = { 0.5803921569, 0.5803921569, 0.5803921569, 1.0 };
        public static readonly double[] GraphCubeLeftColor = { 0.3607843137, 0.3607843137, 0.3607843137, 1.0 };
        public static readonly double[] GraphCubeRightColor = { 0.2549019608, 0.2549019608, 0.2549019608, 1.0 };

        // Админ-кнопка — правый нижний угол поля квестов (GraphViewport), вплотную к краям.
        public const double AdminSettingsButtonWidth = 64;
        public const double AdminSettingsButtonHeight = 64;
        public const double AdminSettingsButtonOffsetX =
            GraphViewportX + GraphViewportWidth - AdminSettingsButtonWidth;
        public const double AdminSettingsButtonOffsetY =
            GraphViewportY + GraphViewportHeight - AdminSettingsButtonHeight;
        public const string AdminSettingsButtonTexture = "admsettings.png";
        public const string AdminSettingsButtonHoverTexture = "admsettings_hover.png";

        public const double AdminBackgroundX = BackgroundFramePad;
        public const double AdminBackgroundY = BackgroundOffsetY + BackgroundFramePad;
        public const double AdminBackgroundWidth = BackgroundLeftColumnWidth;
        public const double AdminBackgroundHeight = 724;

        public const double AdminContentOffsetX = SidebarCardOffsetX;
        public const double AdminContentOffsetY = 36;
        public const double AdminPanelWidth = SidebarCardWidth;
        public const double AdminPanelBarHeight = 40;

        public const double AdminTitleX = 0;
        public const double AdminTitleY = 0;
        public const double AdminTitleHeight = 22;
        public const double AdminPresetBarX = 0;
        public const double AdminPresetBarY = 34;
        public const double AdminQvestBoxButtonWidth = 50;
        public const double AdminQvestBoxButtonHeight = 40;
        public const double AdminQvestBoxButtonGap = 11;
        public const double AdminQvestBoxStartX = 0;
        public const double AdminQvestBoxY = 96;
        public const string AdminQvestBoxTexture = "barnam.png";
        public const string AdminQvestBoxActiveTexture = "barnam_active.png";
        public const string AdminQvestBoxModalBoxTexture = "modalbox.png";

        public const double AdminInputFieldsY = 144;
        public const double AdminInputFieldsHeight = 200;
        public const double AdminGoalsY1 = 144;
        public const double AdminGoalsY2 = 178;
        public const double AdminGoalsY3 = 212;
        public const double AdminGoalsY4 = 246;
        public const double AdminAwardsY1 = 280;
        public const double AdminAwardsY2 = 314;

        public const double AdminInfoTextX = 0;
        public const double AdminInfoTextY = 348;
        public const double AdminInfoModalBoxWidth = 296;
        public const double AdminInfoModalBoxHeight = 101;
        public const double AdminInfoTextContentX = 6;
        public const double AdminInfoTextContentY = 6;
        public const double AdminInfoTextContentWidth = 282;
        public const double AdminInfoTextContentHeight = 89;

        public const double AdminDirectionX = 0;
        public const double AdminDirectionY = 453;
        public const double AdminDirectionHeight = 30;
        public const double AdminPresetCountX = 0;
        public const double AdminPresetCountY = 487;
        public const double AdminPresetCountHeight = 30;

        public const double AdminMinibarWidth = 145;
        public const double AdminMinibarHeight = 40;
        public const double AdminAddButtonX = 0;
        public const double AdminDeleteButtonX = 151;
        public const double AdminActionButtonsY = 563;
        public const double AdminClearButtonY = 611;
        public const double AdminSaveButtonY = 657;

        public const double AdminInputBoxWidth = 213;
        public const double AdminInputBoxHeight = 30;
        public const double AdminNumInputBoxWidth = 79;
        public const double AdminNumInputX = 217;

        public const string AdminBarTexture = "bar.png";
        public const string AdminBarClearHoverTexture = "bar_clear.png";
        public const string AdminBarSaveHoverTexture = "bar_save.png";
        public const string AdminMinibarTexture = "minibar.png";
        public const string AdminMinibarAddHoverTexture = "minibar_add.png";
        public const string AdminMinibarDeleteHoverTexture = "minibar_delete.png";

        public static readonly double[] AdminPanelTextColor = { 0.9803921569, 1.0, 0.9921568627, 1.0 };
        public static readonly double[] AdminPanelPlaceholderColor = { 118.0 / 255.0, 118.0 / 255.0, 118.0 / 255.0, 0.32 };
        public static readonly double[] AdminClearButtonColor = { 253.0 / 255.0, 90.0 / 255.0, 83.0 / 255.0, 1.0 }; // #FD5A53
        public static readonly double[] AdminSaveButtonColor = { 90.0 / 255.0, 251.0 / 255.0, 87.0 / 255.0, 1.0 }; // #5AFB57
        public static readonly double[] AdminTitleColor = { 0.6823529412, 0.6823529412, 0.6823529412, 1.0 };

        // Group 1169.svg / admin chrome palette.
        public const double AdminTileCornerRadius = 5.5;
        public const double AdminTileIconScale = 0.68;
        public static readonly double[] AdminTileBackgroundColor = { 23.0 / 255.0, 23.0 / 255.0, 23.0 / 255.0, 1.0 }; // #171717
        public static readonly double[] AdminTileActiveBackgroundColor = { 23.0 / 255.0, 23.0 / 255.0, 23.0 / 255.0, 1.0 };
        public static readonly double[] AdminTileHoverBackgroundColor = { 45.0 / 255.0, 45.0 / 255.0, 45.0 / 255.0, 1.0 }; // #2D2D2D
        public static readonly double[] AdminTileBorderColor = { 53.0 / 255.0, 52.0 / 255.0, 50.0 / 255.0, 1.0 }; // #353432
        public static readonly double[] AdminTileIdleContentColor = { 174.0 / 255.0, 174.0 / 255.0, 174.0 / 255.0, 1.0 }; // #AEAEAE
        public static readonly double[] AdminTileActiveContentColor = { 90.0 / 255.0, 251.0 / 255.0, 87.0 / 255.0, 1.0 }; // #5AFB57
        public static readonly double[] AdminModalTitleColor = { 1.0, 1.0, 1.0, 1.0 };
        public static readonly double[] AdminModalSectionColor = { 85.0 / 255.0, 1.0, 1.0, 1.0 }; // #55FFFF
        public static readonly double[] AdminModalChipTextColor = { 250.0 / 255.0, 1.0, 253.0 / 255.0, 1.0 }; // #FAFFFD
        public static readonly double[] AdminModalMutedIconColor = { 60.0 / 255.0, 60.0 / 255.0, 60.0 / 255.0, 1.0 }; // #3C3C3C
        /// <summary>Take / hand-in flag (Group 1169 #FFC74C).</summary>
        /// <summary>All Types / take — yellow #FFC74C (Group 896 right chip, tile glyph).</summary>
        public static readonly double[] AdminFlagTakeColor = { 255.0 / 255.0, 199.0 / 255.0, 76.0 / 255.0, 1.0 };
        /// <summary>Craft flag (Group 1169 #AD00C8).</summary>
        public static readonly double[] AdminFlagCraftColor = { 173.0 / 255.0, 0.0, 200.0 / 255.0, 1.0 };
        /// <summary>All-variants flag (Group 1169 #5BD5DD).</summary>
        /// <summary>Craft / variants — cyan #5BD5DD (Group 896 middle chip, paperclip).</summary>
        public static readonly double[] AdminFlagVariantsColor = { 91.0 / 255.0, 213.0 / 255.0, 221.0 / 255.0, 1.0 };
        /// <summary>Delete / remove flag (Group 1169 #FD5A53).</summary>
        public static readonly double[] AdminFlagDeleteColor = { 253.0 / 255.0, 90.0 / 255.0, 83.0 / 255.0, 1.0 };
    }
}
