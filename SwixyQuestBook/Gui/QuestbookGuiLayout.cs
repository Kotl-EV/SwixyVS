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

        // Фон книги
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

        // Боковая панель
        public const double SidebarCardOffsetX = 40;
        public const double SidebarCardOffsetY = 90;
        public const double SidebarCardWidth = 278;
        public const double SidebarCardHeight = 40;
        public const double SidebarCardGap = 6;
        public const double SidebarViewportHeight = 680;
        public const double SidebarScrollbarGap = 6;
        public const double SidebarScrollbarWidth = 10;
        public const double SidebarScrollbarOffsetX = 0;
        public const double SidebarScrollbarOffsetY = 0;
        public const double SidebarIconSize = 24;
        public const double SidebarIconOffsetX = 8;
        public const double SidebarIconOffsetY = 8;
        public const double SidebarTitleOffsetX = 44;
        public const double SidebarFontSize = 16;
        public const double SidebarLineHeight = 20;
        public const double SidebarProgressRightPadding = 8;
        public static readonly double[] SidebarTitleColor = { 0.3529411765, 0.9843137255, 0.3411764706, 1.0 };

        // Редактор квестов в левой панели (Better Questing)
        public const double SidebarEditButtonHeight = 48;
        public const double SidebarAdminModeBarHeight = 48;
        public const double SidebarAdminModeBarGap = 6;
        public const double SidebarAdminQuestContentOffsetY = SidebarAdminModeBarHeight + SidebarAdminModeBarGap;
        // Tools: 2 buttons per row (8 tools → 4 rows).
        public const double SidebarAdminToolbarButtonHeight = 56;
        public const double SidebarAdminToolbarButtonGap = 6;
        public const int SidebarAdminToolbarColumns = 2;
        public const int SidebarAdminToolbarButtonCount = 8;
        public const int SidebarAdminToolbarRows =
            (SidebarAdminToolbarButtonCount + SidebarAdminToolbarColumns - 1) / SidebarAdminToolbarColumns;
        public const double SidebarAdminToolbarHeight =
            (SidebarAdminToolbarButtonHeight * SidebarAdminToolbarRows)
            + (SidebarAdminToolbarButtonGap * (SidebarAdminToolbarRows - 1));
        public const double SidebarAdminStatusHeight = 22;
        public const double SidebarAdminSelectedHintY = 8;
        public const double SidebarAdminBranchActionsHeight =
            (SidebarAdminToolbarButtonHeight * 3) + (SidebarAdminToolbarButtonGap * 2);
        public const double SidebarAdminBranchListOffsetY = SidebarAdminQuestContentOffsetY + SidebarAdminBranchActionsHeight + SidebarAdminStatusHeight + SidebarAdminToolbarButtonGap;
        public const double SidebarAdminBranchCloseOffsetFromBottom = SidebarAdminToolbarButtonHeight + SidebarAdminToolbarButtonGap;
        public const double SidebarAdminPanelWidth = SidebarCardWidth;
        public const double SidebarAdminEmptyHintY = 12;

        public const double QuestEditModalWidth = 640;
        public const double QuestEditModalHeight = 520;
        public const double QuestEditModalPadding = 20;
        public const double QuestEditModalTypeBarHeight = 44;
        public const double QuestEditModalSectionGap = 10;
        public const double QuestEditModalListHeight = 156;
        public const double QuestEditModalListColumnGap = 12;
        public const double QuestEditModalRowHeight = 32;
        public const double QuestEditModalRowGap = 5;
        public const double QuestEditModalMatchToggleSize = 26;
        public const double QuestEditModalInfoHeight = 88;
        public const double QuestEditModalCloseButtonHeight = 40;
        public const double QuestEditModalAddButtonSize = 36;
        public const double QuestEditModalNumInputWidth = 58;
        public const double QuestEditModalRemoveButtonWidth = 28;
        public const double QuestEditModalPickSlotSize = 34;
        public const double QuestEditModalPickerPanelHeight = 172;
        public const double QuestEditModalPickerSlotSize = 36;
        public const double QuestEditModalPickerSlotGap = 4;
        public const int QuestEditModalPickerColumns = 10;
        public const double QuestEditModalListScrollbarWidth = 8;

        public const double AddBranchModalWidth = 420;
        public const double AddBranchModalHeight = 400;
        public const double AddBranchModalItemSlotSize = 36;
        public const double AddBranchModalItemSlotGap = 4;
        public const int AddBranchModalItemColumns = 8;
        public const double AddBranchModalItemGridHeight = 168;
        public const double AddBranchModalPadding = 20;
        public const double AddBranchModalInputHeight = 36;
        public const double AddBranchModalButtonHeight = 36;
        public const double AddBranchModalButtonGap = 8;

        // Граф квестов
        public const double GraphViewportX = 383;
        public const double GraphViewportY = 113;
        public const double GraphViewportWidth = 1154;
        public const double GraphViewportHeight = 657;
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

        // Модальное окно
        public const double ModalWidth = 560;
        public const double ModalHeight = 300;
        public const double ModalPadding = 24;
        public const double ModalIconBoxSize = 72;
        public const double ModalIconSize = 24;
        public const double ModalButtonWidth = 567;
        public const double ModalStartButtonWidth = 568;
        public const double ModalButtonHeight = 74;
        public const double ModalQuestStatusX = 893;
        public const double ModalQuestStatusY = 320;
        public const double ModalQuestStatusWidth = 470;
        public const double ModalQuestStatusHeight = 39;
        public const double ModalTitleX = 893;
        public const double ModalTitleY = 365;
        public const double ModalTitleWidth = 470;
        public const double ModalTitleHeight = 39;
        public const double ModalStartInfoBoxX = 844;
        public const double ModalStartInfoBoxY = 422;
        public const double ModalStartInfoBoxWidth = 568;
        public const double ModalStartInfoBoxHeight = 280;
        public const double ModalStartInfoTextX = 862;
        public const double ModalStartInfoTextY = 440;
        public const double ModalStartInfoTextWidth = 532;
        public const double ModalStartInfoTextHeight = 244;
        public const int ModalStartDescriptionMaxLength = 624;
        public const double ModalStartButtonX = 844;
        public const double ModalStartButtonY = 748;
        public const double ModalCloseX = 1368;
        public const double ModalCloseY = 340;
        public const double ModalCloseSize = 44;

        public static readonly double[] ModalQuestLabelColor = { 85.0 / 255.0, 85.0 / 255.0, 85.0 / 255.0, 1.0 };
        public static readonly double[] ModalProgressZeroColor = { 170.0 / 255.0, 170.0 / 255.0, 170.0 / 255.0, 1.0 };
        public static readonly double[] ModalProgressActiveColor = { 255.0 / 255.0, 254.0 / 255.0, 86.0 / 255.0, 1.0 };
        public static readonly double[] ModalProgressDoneColor = { 90.0 / 255.0, 251.0 / 255.0, 87.0 / 255.0, 1.0 };
        public static readonly double[] ModalStartInfoTextColor = { 1.0, 1.0, 85.0 / 255.0, 1.0 };
        public static readonly double[] ModalStartButtonTextColor = { 235.0 / 255.0, 235.0 / 255.0, 235.0 / 255.0, 1.0 };
        public static readonly double[] ModalOverlayColor = { 0.0, 0.0, 0.0, 0.76 };
        public static readonly double[] ModalBorderColor = { 0.1764705882, 0.1098039216, 0.0745098039, 1.0 };
        public static readonly double[] ModalBackgroundColor = { 0.9098039216, 0.8705882353, 0.7607843137, 0.98 };
        public static readonly double[] ModalBodyTextColor = { 0.2078431373, 0.1764705882, 0.1176470588, 1.0 };
        public static readonly double[] ModalButtonDisabledColor = { 0.4588235294, 0.4392156863, 0.3921568627, 1.0 };
        public static readonly double[] ModalButtonActiveColor = { 0.3529411765, 0.9843137255, 0.3411764706, 1.0 };
        public static readonly double[] ModalButtonActiveTextColor = { 0.1215686275, 0.1215686275, 0.1215686275, 1.0 };
        public static readonly double[] ModalButtonDisabledTextColor = { 0.9803921569, 1.0, 0.9921568627, 1.0 };

        public static readonly double[] GraphCubeTopColor = { 0.5803921569, 0.5803921569, 0.5803921569, 1.0 };
        public static readonly double[] GraphCubeLeftColor = { 0.3607843137, 0.3607843137, 0.3607843137, 1.0 };
        public static readonly double[] GraphCubeRightColor = { 0.2549019608, 0.2549019608, 0.2549019608, 1.0 };

        // Админ-панель
        public const double AdminSettingsButtonWidth = 64;
        public const double AdminSettingsButtonHeight = 64;
        public const double AdminSettingsButtonOffsetX = 1497;
        public const double AdminSettingsButtonOffsetY = 734;
        public const string AdminSettingsButtonTexture = "admsettings.png";
        public const string AdminSettingsButtonHoverTexture = "admsettings_hover.png";

        public const double AdminBackgroundX = 0;
        public const double AdminBackgroundY = 55;
        public const double AdminBackgroundWidth = 347;
        public const double AdminBackgroundHeight = 769;

        public const double AdminContentOffsetX = 40;
        public const double AdminContentOffsetY = 36;
        public const double AdminPanelWidth = 296;
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
        public static readonly double[] AdminClearButtonColor = { 253.0 / 255.0, 90.0 / 255.0, 83.0 / 255.0, 1.0 };
        public static readonly double[] AdminSaveButtonColor = { 90.0 / 255.0, 251.0 / 255.0, 87.0 / 255.0, 1.0 };
        public static readonly double[] AdminTitleColor = { 0.6823529412, 0.6823529412, 0.6823529412, 1.0 };

        public const double AdminTileCornerRadius = 6;
        public const double AdminTileIconScale = 0.68;
        public static readonly double[] AdminTileBackgroundColor = { 0.1215686275, 0.1411764706, 0.1607843137, 0.96 };
        public static readonly double[] AdminTileActiveBackgroundColor = { 0.1019607843, 0.1882352941, 0.0980392157, 1.0 };
        public static readonly double[] AdminTileHoverBackgroundColor = { 0.1764705882, 0.1960784314, 0.2156862745, 1.0 };
        public static readonly double[] AdminTileBorderColor = { 0.3294117647, 0.3568627451, 0.3803921569, 0.85 };
    }
}
