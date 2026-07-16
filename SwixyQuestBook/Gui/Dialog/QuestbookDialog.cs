using Cairo;
using SwixyQuestBook.Client;
using SwixyQuestBook.Domain.Models;
using SwixyQuestBook.Network;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixyQuestBook.Gui
{
    public sealed partial class QuestbookDialog : GuiDialog
    {
        private readonly record struct LayoutRect(double X, double Y, double Width, double Height)
        {
            public bool Contains(double pointX, double pointY, double padding = 0)
            {
                return pointX >= X - padding
                    && pointX <= X + Width + padding
                    && pointY >= Y - padding
                    && pointY <= Y + Height + padding;
            }

            public bool Contains(LayoutRect other, double padding = 0)
            {
                return other.X >= X + padding
                    && other.Y >= Y + padding
                    && other.X + other.Width <= X + Width - padding
                    && other.Y + other.Height <= Y + Height - padding;
            }

            public LayoutRect Intersect(LayoutRect other)
            {
                double left = System.Math.Max(X, other.X);
                double top = System.Math.Max(Y, other.Y);
                double right = System.Math.Min(X + Width, other.X + other.Width);
                double bottom = System.Math.Min(Y + Height, other.Y + other.Height);

                if (right <= left || bottom <= top)
                {
                    return new LayoutRect(0, 0, 0, 0);
                }

                return new LayoutRect(left, top, right - left, bottom - top);
            }

            public bool IsEmpty => Width <= 0 || Height <= 0;

            public LayoutRect Offset(double offsetX, double offsetY)
            {
                return new LayoutRect(X + offsetX, Y + offsetY, Width, Height);
            }
        }

        private readonly record struct SidebarQuestEntry(string IconItemCode, string Title, int ProgressPercent, bool IsSelected);
        private readonly record struct QuestItemIconRenderRequest(
            string CollectibleCode,
            LayoutRect LocalSlotRect,
            bool ClipToRightPanel,
            int DisplayCount,
            QuestbookItemIconContext Context,
            /// <summary>Optional local-space viewport clip (scroll lists). Empty = none.</summary>
            LayoutRect LocalClipRect = default);
        private enum QuestNodeVisualState
        {
            Inactive,
            Active,
            Completed,
            Selected
        }

        private const string ComposerKey = "swixyquestbook-main-dialog";
        private const double ScreenMargin = 64;
        private static string TopMenuTitleText => QuestbookLang.GetLocal("title");
        private const string TopMenuSeparatorText = ">";
        private static string TopMenuCloseText => QuestbookLang.GetLocal("close");
        private const string TopMenuCloseHotkeyText = "(ESC)";
        private static string EmptyCategoryText => QuestbookLang.GetLocal("empty_category");
        private const string SidebarScrollbarKey = "swixyquestbook-sidebar-scrollbar";
        private const double GraphZoomFactor = 1.12;
        private const double MinGraphZoom = 0.45;
        private const double MaxGraphZoom = 3.0;
        private const double DefaultGraphZoom = 1.0;
        private static readonly bool DebugSidebarScrollLayout = false;
        private static readonly bool DebugDialogAlignmentLayout = false;

        private readonly QuestbookClientDataManager dataManager;
        private readonly QuestbookTextureHelper textureHelper;
        private readonly QuestbookGuiItemIconRenderer guiItemIconRenderer;
        private QuestbookCategoryDefinition[] categories = [];
        private readonly Dictionary<string, DummySlot> questItemSlotCache = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DummySlot> questItemIconSlotCache = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<DummySlot>> wildcardSlotCache = new(System.StringComparer.OrdinalIgnoreCase);
        private int wildcardCycleFrame = 0;
        private readonly List<QuestItemIconRenderRequest> questItemIconRenderRequests = [];
        private readonly List<QuestItemIconRenderRequest> adminEditorIconRenderRequests = [];
        private readonly List<QuestItemIconRenderRequest> sidebarIconRenderRequests = [];
        private readonly List<QuestItemIconRenderRequest> branchModalIconRenderRequests = [];

        private double lastWindowWidth = -1;
        private double lastWindowHeight = -1;
        private double lastGuiScale = -1;
        private double currentDialogX;
        private double currentDialogY;
        private LayoutRect rightPanelViewportLocal = new(0, 0, 0, 0);
        private double currentFitScale = 1;
        private double graphZoom = 1;
        private double graphPanX;
        private double graphPanY;
        private bool shouldCenterOnStartNode = true;
        private double dragStartMouseX;
        private double dragStartMouseY;
        private double dragStartGraphPanX;
        private double dragStartGraphPanY;
        private double rightPanelGraphBaseX;
        private double rightPanelGraphBaseY;
        private double viewportRectWidth;
        private double viewportRectHeight;
        private double sidebarScrollbarDragStartMouseY;
        private double sidebarScrollbarDragStartOffset;
        private double sidebarScrollbarHandleHeight;
        private double sidebarScrollbarHandlePosition;
        private double sidebarScrollConversionFactor = 1;
        private double sidebarScrollStep;
        private double maxSidebarScrollOffset;
        private double sidebarScrollOffset;
        private int lastInventoryStateHash;
        private float inventoryRefreshTimer;
        private const float InventoryRefreshIntervalSeconds = 0.35f;
        private QuestbookInventoryHelper.InventorySnapshot inventorySnapshot =
            QuestbookInventoryHelper.InventorySnapshot.Empty;
        private bool inventorySnapshotDirty = true;
        private bool pendingComposeDialog;
        private bool composeDialogRunning;
        private int selectedCategoryIndex;
        private int selectedQuestNodeId = -1;
        private int hoveredQuestNodeId = -1;
        private int pendingQuestNodeId = -1;
        private bool isSyncingSidebarScrollbar;
        private bool isDraggingRightPanel;
        private bool isDraggingSidebarScrollbar;
        private bool isCloseButtonHovered;
        private bool isDetachButtonHovered;
        private bool isDialogMovable;
        private bool isDraggingDialog;
        private bool didInitDialogMovableState;
        private double dialogDragStartMouseX;
        private double dialogDragStartMouseY;
        private LayoutRect detachButtonHitArea = new(0, 0, 0, 0);
        private LayoutRect topMenuDragHitArea = new(0, 0, 0, 0);
        private bool isQuestModalCloseButtonHovered;
        private bool isQuestModalOpen;
        private GuiComposer? adminPanelComposer;
        private LayoutRect dialogContentScreenRect = new(0, 0, 0, 0);
        private bool isQuestSubmitPending;
        private LayoutRect closeButtonHitArea = new(0, 0, 0, 0);
        private LayoutRect questModalCloseHitArea = new(0, 0, 0, 0);
        private LayoutRect questModalSubmitButtonHitArea = new(0, 0, 0, 0);
        private LayoutRect questModalGoalsViewportHitArea = new(0, 0, 0, 0);
        private LayoutRect questModalRewardsViewportHitArea = new(0, 0, 0, 0);
        private double questModalGoalsScrollOffset;
        private double questModalRewardsScrollOffset;
        private double questModalGoalsMaxScroll;
        private double questModalRewardsMaxScroll;
        private double questModalItemGridScrollStep;
        private LayoutRect sidebarViewportHitArea = new(0, 0, 0, 0);
        private LayoutRect sidebarScrollbarHitArea = new(0, 0, 0, 0);
        private LayoutRect sidebarScrollbarHandleHitArea = new(0, 0, 0, 0);
        private LayoutRect sidebarScrollbarVisualHitArea = new(0, 0, 0, 0);
        private LayoutRect sidebarScrollbarVisualHandleHitArea = new(0, 0, 0, 0);
        private LayoutRect rightPanelViewportHitArea = new(0, 0, 0, 0);
        private LayoutRect questModalOverlayHitArea = new(0, 0, 0, 0);
        private LayoutRect[] questCardHitAreas = [];
        private LayoutRect[] sidebarCardHitAreas = [];

        private readonly QuestbookAdminData adminData = new();
        private QuestbookCategoryDefinition[]? preAdminSnapshot;
        private LayoutRect adminSettingsButtonHitArea = new(0, 0, 0, 0);
        private LayoutRect adminSidebarEditButtonHitArea = new(0, 0, 0, 0);
        private string? pendingSelectCategoryHeaderTitle;
        private bool pendingOpenAdminEditor;
        private LayoutRect adminToolSelectHitArea = new(0, 0, 0, 0);
        private LayoutRect adminToolQuestHitArea = new(0, 0, 0, 0);
        private LayoutRect adminToolLinkHitArea = new(0, 0, 0, 0);
        private LayoutRect adminToolDeleteHitArea = new(0, 0, 0, 0);
        private LayoutRect adminToolSaveHitArea = new(0, 0, 0, 0);
        private LayoutRect adminToolClearHitArea = new(0, 0, 0, 0);
        private LayoutRect adminToolCloseHitArea = new(0, 0, 0, 0);
        private LayoutRect adminToolGridHitArea = new(0, 0, 0, 0);
        private bool isDraggingQuestNode;
        private int draggedQuestNodeId = -1;
        private LayoutRect adminTypeStartHitArea = new(0, 0, 0, 0);
        private LayoutRect adminTypeQuestHitArea = new(0, 0, 0, 0);
        private LayoutRect adminTypeCheckpointHitArea = new(0, 0, 0, 0);
        private LayoutRect adminTypeKillHitArea = new(0, 0, 0, 0);

        private LayoutRect[] adminInputFieldHitAreas = [];
        private bool isAdminInputFocused = false;
        private int focusedInputFieldIndex = -1;
        private string adminInputText = string.Empty;

        /// <summary>Branch modal name field has keyboard focus (shows blinking caret).</summary>
        private bool isBranchModalTitleFocused;
        private long caretBlinkEpochMs;
        private bool lastCaretBlinkVisible = true;

        private LayoutRect adminModeBranchesHitArea = new(0, 0, 0, 0);
        private LayoutRect adminModeQuestsHitArea = new(0, 0, 0, 0);
        private LayoutRect adminBranchAddHitArea = new(0, 0, 0, 0);
        private LayoutRect adminBranchRenameHitArea = new(0, 0, 0, 0);
        private LayoutRect adminBranchDeleteHitArea = new(0, 0, 0, 0);
        private LayoutRect adminBranchCloseHitArea = new(0, 0, 0, 0);
        private LayoutRect adminBranchListViewportHitArea = new(0, 0, 0, 0);
        private LayoutRect[] adminBranchCardHitAreas = [];
        private double adminBranchListScrollOffset;
        private bool isAdminSidebarEditHovered = false;
        private bool isAdminModeBranchesHovered = false;
        private bool isAdminModeQuestsHovered = false;
        private bool isAdminBranchAddHovered = false;
        private bool isAdminBranchRenameHovered = false;
        private bool isAdminBranchDeleteHovered = false;
        private bool isAdminBranchCloseHovered = false;
        private BranchModalMode branchModalMode;
        private bool isBranchModalOpen;
        private string branchModalTitleText = string.Empty;
        private string branchModalTargetHeaderTitle = string.Empty;
        private string branchModalSelectedIconItemCode = string.Empty;
        private (ItemSlot Slot, LayoutRect HitArea, string CollectibleCode)[] branchModalItemPickerSlots = [];
        private bool pendingAdminRefreshAfterDelete;
        private bool isBranchModalPrimaryHovered;
        private bool isBranchModalCancelHovered;
        private LayoutRect branchModalOverlayHitArea = new(0, 0, 0, 0);
        private LayoutRect branchModalPanelHitArea = new(0, 0, 0, 0);
        private LayoutRect branchModalTitleInputHitArea = new(0, 0, 0, 0);
        private LayoutRect branchModalPrimaryButtonHitArea = new(0, 0, 0, 0);
        private LayoutRect branchModalCancelButtonHitArea = new(0, 0, 0, 0);
        private bool isQuestEditModalOpen;
        private bool isQuestEditModalSaveHovered;
        private LayoutRect questEditModalOverlayHitArea = new(0, 0, 0, 0);
        private LayoutRect questEditModalPanelHitArea = new(0, 0, 0, 0);
        private LayoutRect questEditModalSaveButtonHitArea = new(0, 0, 0, 0);
        private LayoutRect[] questEditLangButtonHitAreas = [];
        private string[] questEditLangCodes = [];
        private LayoutRect goalsListViewportHitArea = new(0, 0, 0, 0);
        private LayoutRect awardsListViewportHitArea = new(0, 0, 0, 0);
        private LayoutRect goalsAddButtonHitArea = new(0, 0, 0, 0);
        private LayoutRect awardsAddButtonHitArea = new(0, 0, 0, 0);
        private LayoutRect[] goalsRemoveHitAreas = [];
        private LayoutRect[] awardsRemoveHitAreas = [];
        private LayoutRect[] goalsItemPickHitAreas = [];
        private LayoutRect[] awardsItemPickHitAreas = [];
        private LayoutRect[] goalsMatchToggleHitAreas = [];
        private LayoutRect[] awardsMatchToggleHitAreas = [];
        private LayoutRect[] goalsCraftToggleHitAreas = [];
        private LayoutRect[] goalsKillToggleHitAreas = [];
        private LayoutRect[] goalsTakeToggleHitAreas = [];
        private AdminItemPickerTarget? adminItemPickerTarget;
        private LayoutRect adminItemPickerPanelHitArea = new(0, 0, 0, 0);
        private LayoutRect adminItemPickerCancelHitArea = new(0, 0, 0, 0);
        /// <summary>AssignCode = item code or journal:piece for lore goals.</summary>
        private (ItemSlot Slot, LayoutRect HitArea, string Label, string AssignCode)[] adminItemPickerSlots = [];
        /// <summary>
        /// Kill-goal creature tiles. Code = entity type code for kill progress;
        /// Slot = ItemCreature stack for 3D render (same as creative).
        /// </summary>
        private (string EntityCode, string Label, DummySlot Slot, LayoutRect HitArea)[] adminEntityPickerSlots = [];
        private double adminEntityPickerScrollOffset;
        private LayoutRect adminEntityPickerViewportLocal = new(0, 0, 0, 0);
        private string? adminEntityPickerHoverLabel;
        /// <summary>Local-space rect of the hovered creature tile (for tooltip placement).</summary>
        private LayoutRect adminEntityPickerHoverRect = new(0, 0, 0, 0);
        private string adminEntityPickerSearchText = string.Empty;
        private bool adminEntityPickerSearchFocused;
        private LayoutRect adminEntityPickerSearchHitArea = new(0, 0, 0, 0);
        private LoadedTexture? entityPickerTooltipTexture;
        private string? entityPickerTooltipCachedText;
        /// <summary>Creative-inventory catalog (same entries as creative menu). Built once, slots reused.</summary>
        private List<(string Code, string Label, DummySlot Slot)>? adminItemCatalogCache;
        private string adminItemCatalogFilterKey = "\u0001";
        private List<(string Code, string Label, DummySlot Slot)>? adminItemCatalogFiltered;
        /// <summary>
        /// Highest node id ever issued per category this session (never reuse after delete —
        /// player progress is keyed by category:nodeId).
        /// </summary>
        private readonly Dictionary<string, int> adminCategoryNodeIdWatermark =
            new(StringComparer.OrdinalIgnoreCase);
        private AdminFormFieldRef[] adminInputFieldRefs = [];
        private double goalsListScrollOffset;
        private double awardsListScrollOffset;
        private bool isGoalsAddHovered;
        private bool isAwardsAddHovered;
        private double questNodePressMouseX;
        private double questNodePressMouseY;
        private bool questNodePressMoved;
        private const double QuestNodeClickMoveThreshold = 4;
        private bool isAdminToolSelectHovered = false;
        private bool isAdminToolQuestHovered = false;
        private bool isAdminToolLinkHovered = false;
        private bool isAdminToolDeleteHovered = false;
        private bool isAdminToolSaveHovered = false;
        private bool isAdminToolClearHovered = false;
        private bool isAdminToolCloseHovered = false;
        private bool isAdminToolGridHovered = false;
        private bool isAdminTypeStartHovered = false;
        private bool isAdminTypeQuestHovered = false;
        private bool isAdminTypeCheckpointHovered = false;
        private bool isAdminTypeKillHovered = false;


        public QuestbookDialog(ICoreClientAPI capi, QuestbookClientDataManager dataManager) : base(capi)
        {
            this.dataManager = dataManager;
            textureHelper = new QuestbookTextureHelper(capi);
            guiItemIconRenderer = new QuestbookGuiItemIconRenderer(capi);
            categories = dataManager.Categories;
            EnsureLayoutCurrent();
        }

        /// <summary>Requests full tree for the currently selected branch if still a stub.</summary>
        public void EnsureSelectedCategoryContentLoaded(bool includeI18n = false)
        {
            if (categories.Length == 0 && dataManager.Categories.Length > 0)
                categories = dataManager.Categories;

            if (categories.Length == 0)
                return;

            dataManager.EnsureCategoryContentLoaded(GetSelectedCategory().HeaderTitle, includeI18n);
        }

        public void RefreshData()
        {
            categories = dataManager.Categories;
            selectedCategoryIndex = System.Math.Clamp(selectedCategoryIndex, 0, System.Math.Max(0, categories.Length - 1));
            hoveredQuestNodeId = -1;
            InvalidateGraphCache();
            InvalidateInventorySnapshot();

            // Keep the open branch tree warm (lazy-loaded per category).
            EnsureSelectedCategoryContentLoaded();

            bool forceStructureRefresh = false;
            if (pendingAdminRefreshAfterDelete)
            {
                pendingAdminRefreshAfterDelete = false;
                forceStructureRefresh = true;
                selectedCategoryIndex = System.Math.Clamp(selectedCategoryIndex, 0, System.Math.Max(0, categories.Length - 1));
                shouldCenterOnStartNode = true;
            }

            if (!string.IsNullOrWhiteSpace(pendingSelectCategoryHeaderTitle))
            {
                string headerTitle = pendingSelectCategoryHeaderTitle;
                bool openEditor = pendingOpenAdminEditor;
                pendingSelectCategoryHeaderTitle = null;
                forceStructureRefresh = true;
                TrySelectCategoryByHeader(headerTitle, openEditor);
            }

            // НЕ сбрасываем позицию и зум при обновлении данных (сдача квеста и т.д.)
            // Progress-only updates: soft content refresh. Structural edits still full-compose.
            if (IsOpened() && SingleComposer != null && !forceStructureRefresh)
            {
                RequestContentRefresh();
                return;
            }

            EnsureLayoutCurrent(forceRecompose: true);
        }

        public override string ToggleKeyCombinationCode => "";
        // Возвращаем пустую строку — toggle обрабатывается нашим hotkey handler в QuestbookClientSystem.
        // Если вернуть реальный hotkey, VS вызовет Toggle() ДО нашего guard и диалог закроется при открытой админке.

        public override bool PrefersUngrabbedMouse => true;

        public override bool DisableMouseGrab => true;

        public override bool ShouldReceiveKeyboardEvents()
        {
            return IsOpened();
        }

        public bool IsAdminPanelOpen()
        {
            return adminData.IsAdminPanelOpen || isBranchModalOpen || isQuestEditModalOpen;
        }

        public bool TryDismissOpenSubDialogOnToggle()
        {
            // Hotkey is K — while typing in a field, swallow it so "k" is text, not dismiss.
            if (isQuestEditModalOpen
                && (!adminData.FocusedField.IsNone || adminEntityPickerSearchFocused))
            {
                return true;
            }

            if (isBranchModalOpen && isBranchModalTitleFocused)
                return true;

            if (isQuestEditModalOpen)
            {
                if (adminItemPickerTarget != null)
                    CloseAdminItemPicker();
                else
                    DismissQuestEditModal();

                ComposeDialog();
                return true;
            }

            if (isBranchModalOpen)
            {
                CloseBranchModal();
                ComposeDialog();
                return true;
            }

            if (isQuestModalOpen)
            {
                CloseQuestModal();
                ComposeDialog();
                return true;
            }

            return false;
        }

        private bool IsPlayerAdmin()
        {
            return capi.World?.Player?.HasPrivilege("controlserver") == true;
        }

        public override bool OnEscapePressed()
        {
            if (isQuestModalOpen)
            {
                CloseQuestModal();
                ComposeDialog();
                return true;
            }

            if (isBranchModalOpen)
            {
                CloseBranchModal();
                ComposeDialog();
                return true;
            }

            if (isQuestEditModalOpen)
            {
                if (adminItemPickerTarget != null)
                {
                    CloseAdminItemPicker();
                    ComposeDialog();
                    return true;
                }

                SaveAndCloseQuestEditModal();
                ComposeDialog();
                return true;
            }

            if (adminData.IsAdminPanelOpen)
            {
                CloseAdminEditor(restoreSnapshot: true);
                ComposeDialog();
                return true;
            }

            return TryClose();
        }

        public override void OnKeyDown(KeyEvent args)
        {
            if (HandleBranchModalKeyDown(args))
                return;

            if (HandleQuestEditModalKeyDown(args))
                return;

            base.OnKeyDown(args);
        }

        private bool HandleQuestEditModalKeyDown(KeyEvent args)
        {
            if (!isQuestEditModalOpen)
                return false;

            if (adminItemPickerTarget != null)
            {
                if (HandleEntityPickerKeyDown(args))
                    return true;

                if ((GlKeys)args.KeyCode == GlKeys.Escape)
                {
                    CloseAdminItemPicker();
                    ComposeDialog();
                }

                args.Handled = true;
                return true;
            }

            if (!adminData.HasSelectedNode || adminData.FocusedField.IsNone)
                return false;

            AdminFormFieldRef field = adminData.FocusedField;
            GlKeys key = (GlKeys)args.KeyCode;

            if (key == GlKeys.Back)
            {
                adminData.BackspaceField(field);
                ResetTextCaretBlink();
                SyncAdminFieldEdit();
                args.Handled = true;
                return true;
            }

            if (key == GlKeys.V && args.CtrlPressed)
            {
                string clipboard = capi.Input.ClipboardText;
                if (!string.IsNullOrEmpty(clipboard))
                {
                    adminData.SetFieldValue(field, clipboard.Trim());
                    ResetTextCaretBlink();
                    SyncAdminFieldEdit();
                }

                args.Handled = true;
                return true;
            }

            if (key == GlKeys.Tab)
            {
                AdminFormFieldRef nextField = adminData.GetNextFieldRef(field);
                if (!nextField.IsNone && nextField.IsCount && adminData.GetFieldValue(nextField) == "0")
                    adminData.SetFieldValue(nextField, "0");

                adminData.FocusedField = nextField;
                ResetTextCaretBlink();
                EnsureQuestEditFocusedRowVisible(nextField);
                ComposeDialog();
                args.Handled = true;
                return true;
            }

            if (key == GlKeys.Escape)
            {
                adminData.FocusedField = AdminFormFieldRef.None;
                ComposeDialog();
                args.Handled = true;
                return true;
            }

            if (args.KeyChar != '\0' && args.KeyChar != '\t' && args.KeyChar != '\n')
            {
                adminData.AppendToField(field, args.KeyChar);
                ResetTextCaretBlink();
                SyncAdminFieldEdit();
                args.Handled = true;
                return true;
            }

            args.Handled = true;
            return true;
        }

        public override void OnKeyPress(KeyEvent args)
        {
            if (HandleBranchModalKeyPress(args))
                return;

            if (HandleQuestEditModalKeyPress(args))
                return;

            base.OnKeyPress(args);
        }

        private bool HandleQuestEditModalKeyPress(KeyEvent args)
        {
            if (!isQuestEditModalOpen)
                return false;

            // Item / creature picker search typing.
            if (adminItemPickerTarget != null && adminEntityPickerSearchFocused
                && args.KeyChar != '\0' && args.KeyChar != '\t' && args.KeyChar != '\n' && args.KeyChar != '\r')
            {
                adminEntityPickerSearchText += args.KeyChar;
                adminEntityPickerScrollOffset = 0;
                RequestContentRefresh();
                args.Handled = true;
                return true;
            }

            if (!adminData.HasSelectedNode || adminData.FocusedField.IsNone)
                return false;

            if (args.KeyChar != '\0' && args.KeyChar != '\t' && args.KeyChar != '\n' && args.KeyChar != '\r')
            {
                adminData.AppendToField(adminData.FocusedField, args.KeyChar);
                ResetTextCaretBlink();
                SyncAdminFieldEdit();
                args.Handled = true;
                return true;
            }

            return false;
        }

        /// <summary>Search keyboard handling for both item and creature admin pickers.</summary>
        private bool HandleEntityPickerKeyDown(KeyEvent args)
        {
            if (adminItemPickerTarget == null)
                return false;

            GlKeys key = (GlKeys)args.KeyCode;

            // Auto-focus search when typing letters while the picker is open.
            if (!adminEntityPickerSearchFocused
                && args.KeyChar != '\0'
                && !char.IsControl(args.KeyChar)
                && key != GlKeys.Escape)
            {
                adminEntityPickerSearchFocused = true;
            }

            if (key == GlKeys.Escape)
            {
                if (adminEntityPickerSearchFocused && adminEntityPickerSearchText.Length > 0)
                {
                    adminEntityPickerSearchText = string.Empty;
                    adminEntityPickerScrollOffset = 0;
                    RequestContentRefresh();
                    args.Handled = true;
                    return true;
                }

                return false; // let outer handler close the picker
            }

            if (!adminEntityPickerSearchFocused)
                return false;

            if (key == GlKeys.Back)
            {
                if (adminEntityPickerSearchText.Length > 0)
                {
                    adminEntityPickerSearchText = adminEntityPickerSearchText[..^1];
                    adminEntityPickerScrollOffset = 0;
                    RequestContentRefresh();
                }

                args.Handled = true;
                return true;
            }

            if (key == GlKeys.V && args.CtrlPressed)
            {
                string clip = capi.Input.ClipboardText ?? string.Empty;
                if (clip.Length > 0)
                {
                    adminEntityPickerSearchText += clip.Trim();
                    adminEntityPickerScrollOffset = 0;
                    RequestContentRefresh();
                }

                args.Handled = true;
                return true;
            }

            return false;
        }

        public override void OnGuiOpened()
        {
            shouldCenterOnStartNode = true;
            graphZoom = DefaultGraphZoom;
            EnsureLayoutCurrent(forceRecompose: true);
            base.OnGuiOpened();
        }

        public override void OnGuiClosed()
        {
            QuestbookSoundHelper.PlayBookClosing(capi);

            adminData.IsAdminPanelOpen = false;
            adminData.ClearFields();
            preAdminSnapshot = null;
            CloseBranchModal();
            CloseQuestEditModal();

            isDraggingRightPanel = false;
            isDraggingQuestNode = false;
            draggedQuestNodeId = -1;
            isCloseButtonHovered = false;
            isDetachButtonHovered = false;
            isDraggingDialog = false;
            didInitDialogMovableState = false;
            hoveredQuestNodeId = -1;
            closeButtonHitArea = new LayoutRect(0, 0, 0, 0);
            detachButtonHitArea = new LayoutRect(0, 0, 0, 0);
            topMenuDragHitArea = new LayoutRect(0, 0, 0, 0);
            dialogContentScreenRect = new LayoutRect(0, 0, 0, 0);
            sidebarViewportHitArea = new LayoutRect(0, 0, 0, 0);
            sidebarScrollbarHitArea = new LayoutRect(0, 0, 0, 0);
            sidebarScrollbarHandleHitArea = new LayoutRect(0, 0, 0, 0);
            sidebarScrollbarVisualHitArea = new LayoutRect(0, 0, 0, 0);
            sidebarScrollbarVisualHandleHitArea = new LayoutRect(0, 0, 0, 0);
            rightPanelViewportHitArea = new LayoutRect(0, 0, 0, 0);
            questCardHitAreas = [];
            sidebarCardHitAreas = [];
            questItemIconRenderRequests.Clear();
            wildcardSlotCache.Clear();
            questItemIconSlotCache.Clear();
            wildcardCycleFrame = 0;
            CloseQuestModal();
            adminPanelComposer = null;
            ClearComposers();
            UnFocus();
            base.OnGuiClosed();
        }

        public override void Dispose()
        {
            DisposePerfCaches();
            textureHelper.Dispose();
            base.Dispose();
        }

        public override void OnRenderGUI(float deltaTime)
        {
            EnsureLayoutCurrent();
            RefreshInventoryDrivenState(deltaTime);
            FlushPendingCompose();

            wildcardCycleFrame++;
            int cycleIndex = wildcardCycleFrame / 60;
            if (cycleIndex != lastWildcardCycleIndex)
            {
                lastWildcardCycleIndex = cycleIndex;
                // Wildcard icon animation: soft redraw only (no composer rebuild).
                RequestContentRefresh();
            }

            // Blinking caret for focused text fields.
            if (HasFocusedTextInput())
            {
                bool caretVisible = IsTextCaretVisible();
                if (caretVisible != lastCaretBlinkVisible)
                {
                    lastCaretBlinkVisible = caretVisible;
                    RequestContentRefresh();
                }
            }

            FlushContentRefresh();

            SingleComposer?.Render(deltaTime);
            RefreshDialogScreenPosition();

            if (isQuestModalOpen)
            {
                questModalOverlayHitArea = GetQuestModalPanelRect(currentDialogX, currentDialogY, currentFitScale);
            }
            else
            {
                questModalOverlayHitArea = new LayoutRect(0, 0, 0, 0);
            }

            // Graph / sidebar icons stay visible around open modals (no full-window dimming),
            // but must not draw on top of the modal panel itself.
            RenderQueuedItemIcons(
                questItemIconRenderRequests,
                deltaTime,
                clipToRightPanelFilter: true,
                hideUnderOpenModals: true);
            RenderQueuedItemIcons(
                sidebarIconRenderRequests,
                deltaTime,
                hideUnderOpenModals: true);

            if (isQuestEditModalOpen)
            {
                RenderQueuedItemIcons(adminEditorIconRenderRequests, deltaTime);
                // Hover fill under meshes (Z 90), then icons (92), then ring + tooltip.
                RenderPickerHoverHighlight(underIcons: true);
                RenderPickerSlotIcons(
                    adminItemPickerSlots.Select(static e => (e.Slot, e.HitArea)),
                    deltaTime);
                // Creature tiles use the same itemstack→GUI path as creative (ItemCreature).
                RenderEntityCreaturePickerIcons(deltaTime);
                RenderPickerHoverHighlight(underIcons: false);
                RenderEntityPickerHoverTooltip();
            }

            if (isBranchModalOpen)
            {
                RenderQueuedItemIcons(branchModalIconRenderRequests, deltaTime);
                RenderPickerSlotIcons(
                    branchModalItemPickerSlots.Select(static entry => (entry.Slot, entry.HitArea)),
                    deltaTime);
            }

            if (isQuestModalOpen)
            {
                // Goals / rewards inside the quest modal (ClipToRightPanel == false).
                RenderQueuedItemIcons(questItemIconRenderRequests, deltaTime, clipToRightPanelFilter: false);
            }
        }

        private bool IconIntersectsOpenModal(LayoutRect screenRect)
        {
            if (isQuestModalOpen
                && !questModalOverlayHitArea.IsEmpty
                && !screenRect.Intersect(questModalOverlayHitArea).IsEmpty)
            {
                return true;
            }

            if (isQuestEditModalOpen
                && !questEditModalPanelHitArea.IsEmpty
                && !screenRect.Intersect(questEditModalPanelHitArea).IsEmpty)
            {
                return true;
            }

            if (isBranchModalOpen
                && !branchModalPanelHitArea.IsEmpty
                && !screenRect.Intersect(branchModalPanelHitArea).IsEmpty)
            {
                return true;
            }

            return false;
        }

        public override void OnMouseDown(MouseEvent args)
        {
            if (isQuestModalOpen)
            {
                if (TryHandleQuestModalMouseDown(args.X, args.Y))
                {
                    args.Handled = true;
                    return;
                }

                return;
            }

            if (TryHandleQuestEditModalMouseDown(args.X, args.Y))
            {
                args.Handled = true;
                return;
            }

            if (TryHandleBranchModalMouseDown(args.X, args.Y))
            {
                args.Handled = true;
                return;
            }

            if (TryHandleAdminSidebarEditClick(args.X, args.Y))
            {
                args.Handled = true;
                return;
            }

            if (adminData.IsAdminPanelOpen)
            {
                if (TryHandleAdminPanelMouseDown(args.X, args.Y))
                {
                    args.Handled = true;
                    return;
                }

                if (rightPanelViewportHitArea.Contains(args.X, args.Y)
                    && adminData.EditorSection == AdminEditorSection.Quests)
                {
                    int? nodeIdAtMouse = GetNodeIdAtMouse(args.X, args.Y);

                    if (adminData.ToolMode == AdminToolMode.Select
                        && args.Button == EnumMouseButton.Left
                        && nodeIdAtMouse is int dragNodeId)
                    {
                        BeginQuestNodeDrag(args.X, args.Y, dragNodeId);
                        args.Handled = true;
                        return;
                    }

                    if (args.Button == EnumMouseButton.Right
                        && nodeIdAtMouse is int rightClickNodeId
                        && adminData.ToolMode == AdminToolMode.LinkQuests)
                    {
                        HandleLinkToolRightClick(rightClickNodeId);
                        args.Handled = true;
                        return;
                    }

                    if (args.Button == EnumMouseButton.Left
                        && nodeIdAtMouse != null
                        && adminData.ToolMode is AdminToolMode.LinkQuests or AdminToolMode.DeleteNode)
                    {
                        TryHandleAdminGraphToolClick(args.X, args.Y);
                        args.Handled = true;
                        return;
                    }

                    if (args.Button == EnumMouseButton.Left)
                    {
                        isDraggingRightPanel = true;
                        dragStartMouseX = args.X;
                        dragStartMouseY = args.Y;
                        dragStartGraphPanX = graphPanX;
                        dragStartGraphPanY = graphPanY;
                    }
                }
                else if (rightPanelViewportHitArea.Contains(args.X, args.Y)
                    && adminData.EditorSection == AdminEditorSection.Branches
                    && args.Button == EnumMouseButton.Left)
                {
                    isDraggingRightPanel = true;
                    dragStartMouseX = args.X;
                    dragStartMouseY = args.Y;
                    dragStartGraphPanX = graphPanX;
                    dragStartGraphPanY = graphPanY;
                }

                args.Handled = true;
                return;
            }

            if (TryBeginSidebarScrollbarDrag(args))
            {
                args.Handled = true;
                return;
            }

            if (TryDetachFromMouse(args.X, args.Y))
            {
                args.Handled = true;
                return;
            }

            if (TryCloseFromMouse(args.X, args.Y))
            {
                args.Handled = true;
                return;
            }

            if (TryBeginDialogDrag(args))
            {
                args.Handled = true;
                return;
            }

            if (TrySelectCategoryFromMouse(args.X, args.Y))
            {
                args.Handled = true;
                return;
            }

            if (!adminData.IsAdminPanelOpen && TryOpenQuestFromMouse(args.X, args.Y))
            {
                args.Handled = true;
                return;
            }

            if (TryBeginRightPanelDrag(args))
            {
                args.Handled = true;
                return;
            }

            base.OnMouseDown(args);
        }

        public override void OnMouseMove(MouseEvent args)
        {
            if (isDraggingSidebarScrollbar)
            {
                double scrollbarMovableHeight = System.Math.Max(0, sidebarScrollbarHitArea.Height - sidebarScrollbarHandleHeight);
                double mouseDelta = args.Y - sidebarScrollbarDragStartMouseY;
                double offsetDelta = scrollbarMovableHeight <= 0
                    ? 0
                    : mouseDelta * sidebarScrollConversionFactor;

                sidebarScrollOffset = System.Math.Clamp(
                    sidebarScrollbarDragStartOffset + offsetDelta,
                    0,
                    maxSidebarScrollOffset
                );

                RequestContentRefresh();
                args.Handled = true;
                return;
            }

            if (isDraggingQuestNode)
            {
                UpdateQuestNodeDrag(args.X, args.Y);
                RequestContentRefresh();
                args.Handled = true;
                return;
            }

            if (isDraggingDialog)
            {
                UpdateDialogDrag(args.X, args.Y);
                args.Handled = true;
                return;
            }

            if (isDraggingRightPanel)
            {
                graphPanX = dragStartGraphPanX + (args.X - dragStartMouseX);
                graphPanY = dragStartGraphPanY + (args.Y - dragStartMouseY);
                RequestContentRefresh();
                args.Handled = true;
                return;
            }

            if (isQuestModalOpen)
            {
                bool newModalCloseHovered = questModalCloseHitArea.Contains(args.X, args.Y);
                if (isQuestModalCloseButtonHovered != newModalCloseHovered)
                {
                    isQuestModalCloseButtonHovered = newModalCloseHovered;
                    RequestContentRefresh();
                }
            }
            else if (isQuestEditModalOpen)
            {
                UpdateQuestEditModalHover(args.X, args.Y);
            }
            else if (isBranchModalOpen)
            {
                UpdateBranchModalHover(args.X, args.Y);
            }
            else
            {
                UpdateHoveredQuestNode(args.X, args.Y);
                UpdateTopMenuButtonHover(args.X, args.Y);
            }

            if (IsPlayerAdmin())
            {
                UpdateAdminPanelHover(args.X, args.Y);
            }

            base.OnMouseMove(args);
        }

        public override void OnMouseUp(MouseEvent args)
        {
            if (isDraggingSidebarScrollbar && args.Button == EnumMouseButton.Left)
            {
                isDraggingSidebarScrollbar = false;
                args.Handled = true;
                return;
            }

            if (isDraggingDialog && args.Button == EnumMouseButton.Left)
            {
                isDraggingDialog = false;
                SaveDialogPosition();
                ComposeDialog();
                args.Handled = true;
                return;
            }

            if (isDraggingRightPanel && args.Button == EnumMouseButton.Left)
            {
                if (adminData.IsAdminPanelOpen
                    && adminData.EditorSection == AdminEditorSection.Quests
                    && adminData.ToolMode != AdminToolMode.None)
                {
                    double panDeltaX = args.X - dragStartMouseX;
                    double panDeltaY = args.Y - dragStartMouseY;
                    if ((panDeltaX * panDeltaX) + (panDeltaY * panDeltaY)
                        <= QuestNodeClickMoveThreshold * QuestNodeClickMoveThreshold)
                    {
                        TryHandleAdminGraphToolClick(dragStartMouseX, dragStartMouseY);
                    }
                }

                isDraggingRightPanel = false;
                args.Handled = true;
                return;
            }

            if (isDraggingQuestNode && args.Button == EnumMouseButton.Left)
            {
                bool shouldOpenModal = !questNodePressMoved
                    && adminData.ToolMode == AdminToolMode.Select
                    && adminData.HasSelectedNode;
                isDraggingQuestNode = false;
                draggedQuestNodeId = -1;
                if (shouldOpenModal)
                {
                    OpenQuestEditModal();
                    ComposeDialog();
                }

                args.Handled = true;
                return;
            }

            base.OnMouseUp(args);
        }

        public override void OnMouseWheel(MouseWheelEventArgs args)
        {
            if (isQuestModalOpen)
            {
                if (TryHandleQuestModalMouseWheel(args))
                {
                    args.SetHandled();
                    return;
                }

                base.OnMouseWheel(args);
                return;
            }

            if (isBranchModalOpen)
            {
                base.OnMouseWheel(args);
                return;
            }

            if (isQuestEditModalOpen && TryHandleQuestEditModalMouseWheel(args))
            {
                args.SetHandled();
                return;
            }

            if (adminData.IsAdminPanelOpen
                && adminData.EditorSection == AdminEditorSection.Branches
                && TryHandleAdminBranchListMouseWheel(args))
            {
                args.SetHandled();
                return;
            }

            int mouseX = capi.Input.MouseX;
            int mouseY = capi.Input.MouseY;
            if (sidebarViewportHitArea.Contains(mouseX, mouseY) || sidebarScrollbarVisualHitArea.Contains(mouseX, mouseY))
            {
                float sidebarWheelDelta = args.deltaPrecise != 0 ? args.deltaPrecise : args.delta;
                if (sidebarWheelDelta == 0)
                {
                    base.OnMouseWheel(args);
                    return;
                }

                if (sidebarWheelDelta != 0 && sidebarScrollStep > 0)
                {
                    double direction = sidebarWheelDelta > 0 ? -1 : 1;
                    sidebarScrollOffset = System.Math.Clamp(
                        sidebarScrollOffset + (direction * sidebarScrollStep),
                        0,
                        maxSidebarScrollOffset
                    );

                    ComposeDialog();
                    args.SetHandled();
                    return;
                }
            }

            if (!rightPanelViewportHitArea.Contains(mouseX, mouseY))
            {
                base.OnMouseWheel(args);
                return;
            }

            float wheelDelta = args.deltaPrecise != 0 ? args.deltaPrecise : args.delta;
            if (wheelDelta == 0)
            {
                wheelDelta = args.valuePrecise != 0 ? args.valuePrecise : args.value;
            }

            if (wheelDelta == 0)
            {
                base.OnMouseWheel(args);
                return;
            }

            double zoomMultiplier = System.Math.Pow(GraphZoomFactor, wheelDelta);
            double nextZoom = graphZoom * zoomMultiplier;
            UpdateGraphZoom(mouseX, mouseY, nextZoom);
            ComposeDialog();
            args.SetHandled();
        }

        private void EnsureLayoutCurrent(bool forceRecompose = false)
        {
            double currentWindowWidth = capi.Gui.WindowBounds.OuterWidth;
            double currentWindowHeight = capi.Gui.WindowBounds.OuterHeight;
            double currentGuiScale = GuiElement.scaled(1);

            bool windowSizeChanged = currentWindowWidth != lastWindowWidth || currentWindowHeight != lastWindowHeight;
            bool guiScaleChanged = System.Math.Abs(currentGuiScale - lastGuiScale) > 0.001;
            if (!forceRecompose && !windowSizeChanged && !guiScaleChanged)
            {
                return;
            }

            lastWindowWidth = currentWindowWidth;
            lastWindowHeight = currentWindowHeight;
            lastGuiScale = currentGuiScale;
            ComposeDialog();
        }

        private static double[] GetProgressColor(int progressPercent)
        {
            if (progressPercent <= 0)
            {
                return QuestbookGuiLayout.TopMenuProgressZeroColor;
            }

            if (progressPercent >= 100)
            {
                return QuestbookGuiLayout.TopMenuProgressDoneColor;
            }

            return QuestbookGuiLayout.TopMenuProgressActiveColor;
        }

        private bool IsNodeCompleted(QuestbookQuestNodeDefinition node)
        {
            return node.State == QuestbookQuestNodeState.Completed;
        }

        private int GetNodeCurrentCount(QuestbookQuestNodeDefinition node)
        {
            if (node.State == QuestbookQuestNodeState.Completed)
            {
                return GetNodeRequiredTotalCount(node);
            }

            return GetNodeCurrentTotalCount(node);
        }

        private static int GetNodeRequiredTotalCount(QuestbookQuestNodeDefinition node)
        {
            int total = 0;
            foreach (QuestbookQuestItemRequirement item in node.RequiredItems)
            {
                total += item.Count;
            }

            return total;
        }

        private int GetNodeCurrentTotalCount(QuestbookQuestNodeDefinition node)
        {
            string categoryKey = GetSelectedCategory().HeaderTitle;
            int total = 0;
            foreach (QuestbookQuestItemRequirement item in node.RequiredItems)
            {
                if (string.IsNullOrWhiteSpace(item.CollectibleCode) || item.Count <= 0)
                {
                    continue;
                }

                // Progress: craft / kill counters or inventory.
                int current;
                if (item.IsKillObjective)
                {
                    current = dataManager.GetKillProgress(categoryKey, node.Id, item.CollectibleCode);
                }
                else if (item.IsCraftObjective && item.RequiresInventory)
                {
                    int crafted = dataManager.GetCraftProgress(categoryKey, node.Id, item.CollectibleCode);
                    int held = CountPlayerCollectibles(item.CollectibleCode);
                    current = System.Math.Min(crafted, held);
                }
                else if (item.IsCraftObjective)
                {
                    current = dataManager.GetCraftProgress(categoryKey, node.Id, item.CollectibleCode);
                }
                else
                {
                    current = CountPlayerCollectibles(item.CollectibleCode);
                }

                total += System.Math.Min(item.Count, current);
            }

            return total;
        }

        private bool IsNodeReadyToSubmit(QuestbookQuestNodeDefinition node)
        {
            string categoryKey = GetSelectedCategory().HeaderTitle;
            foreach (QuestbookQuestItemRequirement item in node.RequiredItems)
            {
                if (string.IsNullOrWhiteSpace(item.CollectibleCode) || item.Count <= 0)
                {
                    continue;
                }

                if (item.IsKillObjective
                    && dataManager.GetKillProgress(categoryKey, node.Id, item.CollectibleCode) < item.Count)
                {
                    return false;
                }

                // Craft axis.
                if (item.IsCraftObjective
                    && dataManager.GetCraftProgress(categoryKey, node.Id, item.CollectibleCode) < item.Count)
                {
                    return false;
                }

                // Inventory axis (have / detect / craft_have). Pure craft / kill skip this.
                if (item.RequiresInventory
                    && CountPlayerCollectibles(item.CollectibleCode) < item.Count)
                {
                    return false;
                }
            }

            return node.RequiredItems.Length > 0;
        }

        private int GetCategoryProgressPercent(QuestbookCategoryDefinition category)
        {
            int total = category.IsContentLoaded
                ? category.Nodes.Length
                : category.TotalNodeCount;

            if (total <= 0)
            {
                return 0;
            }

            int completedNodeCount;
            if (category.IsContentLoaded)
            {
                completedNodeCount = 0;
                foreach (QuestbookQuestNodeDefinition node in category.Nodes)
                {
                    if (IsNodeCompleted(node))
                    {
                        completedNodeCount++;
                    }
                }
            }
            else
            {
                completedNodeCount = dataManager.CountCompletedInCategory(category.HeaderTitle);
            }

            return (int)System.Math.Round((double)completedNodeCount / total * 100, MidpointRounding.AwayFromZero);
        }

        private int GetNodeProgressCount(QuestbookQuestNodeDefinition node)
        {
            if (node.State == QuestbookQuestNodeState.Completed)
            {
                return GetNodeRequiredTotalCount(node);
            }

            return GetNodeCurrentTotalCount(node);
        }

        private int CountPlayerCollectibles(string collectibleCode)
        {
            EnsureInventorySnapshot();
            return inventorySnapshot.Count(collectibleCode);
        }

        private void EnsureInventorySnapshot(bool force = false)
        {
            if (!force && !inventorySnapshotDirty)
            {
                return;
            }

            inventorySnapshot = QuestbookInventoryHelper.BuildSnapshot(capi.World.Player);
            inventorySnapshotDirty = false;
        }

        private void InvalidateInventorySnapshot()
        {
            inventorySnapshotDirty = true;
        }

        private int BuildInventoryStateHash()
        {
            EnsureInventorySnapshot();

            QuestbookCategoryDefinition category = GetSelectedCategory();
            int hash = selectedCategoryIndex;
            hash = (hash * 397) ^ (isQuestModalOpen ? 1 : 0);
            hash = (hash * 397) ^ (isQuestSubmitPending ? 1 : 0);
            hash = (hash * 397) ^ pendingQuestNodeId;
            hash = (hash * 397) ^ inventorySnapshot.ContentHash;
            // Craft goals use server craft progress, not inventory — must invalidate UI when it changes.
            hash = (hash * 397) ^ BuildCraftProgressFingerprint();

            // Only hash unlocked / incomplete quest nodes — completed ones never change with inventory.
            foreach (QuestbookQuestNodeDefinition node in category.Nodes)
            {
                if (node.State == QuestbookQuestNodeState.Completed || node.RequiredItems.Length == 0)
                {
                    continue;
                }

                if (!IsNodeUnlocked(category, node))
                {
                    continue;
                }

                hash = (hash * 397) ^ node.Id;
                // Uses craft progress for craft goals and inventory for "have" goals.
                hash = (hash * 397) ^ GetNodeCurrentCount(node);
            }

            return hash;
        }

        private void RefreshInventoryDrivenState(float deltaTime)
        {
            // Admin editing does not need live inventory progress on the graph every frame.
            if (adminData.IsAdminPanelOpen || isQuestEditModalOpen || isBranchModalOpen)
            {
                return;
            }

            inventoryRefreshTimer += deltaTime;
            if (inventoryRefreshTimer < InventoryRefreshIntervalSeconds)
            {
                return;
            }

            inventoryRefreshTimer = 0f;
            InvalidateInventorySnapshot();

            int currentHash = BuildInventoryStateHash();
            if (currentHash == lastInventoryStateHash)
            {
                return;
            }

            lastInventoryStateHash = currentHash;
            InvalidateGraphCache();
            RequestContentRefresh();
        }

        /// <summary>
        /// Coalesce expensive full recomposes: many input events per frame become one ComposeDialog.
        /// </summary>
        private void RequestComposeDialog()
        {
            pendingComposeDialog = true;
            InvalidateGraphCache();
        }

        private void FlushPendingCompose()
        {
            if (!pendingComposeDialog || composeDialogRunning)
            {
                return;
            }

            pendingComposeDialog = false;
            ComposeDialogImmediate();
        }

        private QuestbookCategoryDefinition GetSelectedCategory()
        {
            if (categories.Length == 0)
            {
                return new QuestbookCategoryDefinition(string.Empty, string.Empty, string.Empty, 0, [], []);
            }

            selectedCategoryIndex = System.Math.Clamp(selectedCategoryIndex, 0, categories.Length - 1);
            return categories[selectedCategoryIndex];
        }

        private bool IsNodeUnlocked(QuestbookCategoryDefinition category, QuestbookQuestNodeDefinition node)
        {
            foreach (QuestbookQuestConnectionDefinition connection in category.Connections)
            {
                if (connection.EndNodeId != node.Id)
                {
                    continue;
                }

                QuestbookQuestNodeDefinition? parentNode = GetNodeById(category, connection.StartNodeId);
                if (parentNode == null || !IsNodeCompleted(parentNode))
                {
                    return false;
                }
            }

            return true;
        }

        private QuestNodeVisualState GetNodeVisualState(QuestbookCategoryDefinition category, QuestbookQuestNodeDefinition node)
        {
            if (IsNodeCompleted(node))
            {
                return QuestNodeVisualState.Completed;
            }

            if (!IsNodeUnlocked(category, node))
            {
                return QuestNodeVisualState.Inactive;
            }

            return QuestNodeVisualState.Active;
        }

        private QuestbookQuestNodeDefinition? GetSelectedQuestNode()
        {
            if (!isQuestModalOpen)
            {
                return null;
            }

            return GetNodeById(GetSelectedCategory(), selectedQuestNodeId);
        }

        public void ApplyQuestSubmitResponse(QuestbookSubmitQuestResponse response)
        {
            isQuestSubmitPending = false;
            pendingQuestNodeId = -1;

            QuestbookQuestNodeDefinition? node = GetNodeByResponse(response);
            if (response.Success && node != null)
            {
                node.MarkCompleted();
                QuestbookSoundHelper.PlayCompleted(capi);
            }

            ComposeDialog();
        }

        private QuestbookQuestNodeDefinition? GetNodeByResponse(QuestbookSubmitQuestResponse response)
        {
            foreach (QuestbookCategoryDefinition category in categories)
            {
                if (!string.Equals(category.HeaderTitle, response.CategoryHeaderTitle, System.StringComparison.Ordinal))
                {
                    continue;
                }

                return GetNodeById(category, response.NodeId);
            }

            return null;
        }

        private bool CanSubmitNode(QuestbookQuestNodeDefinition node)
        {
            if (isQuestSubmitPending && pendingQuestNodeId == node.Id)
            {
                return false;
            }

            if (node.State == QuestbookQuestNodeState.Completed)
            {
                return false;
            }

            if (node.UsesInfoModalLayout)
            {
                return true;
            }

            return IsNodeReadyToSubmit(node);
        }

        private bool ShouldShowActiveNodeOverlay(QuestbookCategoryDefinition category, QuestbookQuestNodeDefinition node)
        {
            return node.IsObjectiveNode
                && IsNodeUnlocked(category, node)
                && node.State != QuestbookQuestNodeState.Completed
                && IsNodeReadyToSubmit(node);
        }

        private bool TrySubmitSelectedQuest()
        {
            QuestbookQuestNodeDefinition? node = GetSelectedQuestNode();
            if (node == null || !CanSubmitNode(node))
            {
                return true;
            }

            QuestbookQuestItemStackPacket[] requiredPackets = node.RequiredItems
                .Select(i => new QuestbookQuestItemStackPacket { CollectibleCode = i.CollectibleCode, Count = i.Count })
                .ToArray();
            QuestbookQuestItemStackPacket[] rewardPackets = node.RewardItems
                .Select(i => new QuestbookQuestItemStackPacket { CollectibleCode = i.CollectibleCode, Count = i.Count })
                .ToArray();

            bool requestSent = QuestbookMod.TrySendQuestSubmit(
                new QuestbookSubmitQuestRequest
                {
                    CategoryHeaderTitle = GetSelectedCategory().HeaderTitle,
                    NodeId = node.Id,
                    RequiredItems = requiredPackets,
                    RewardItems = rewardPackets
                }
            );

            if (!requestSent)
            {
                ComposeDialog();
                return true;
            }

            isQuestSubmitPending = true;
            pendingQuestNodeId = node.Id;
            ComposeDialog();
            return true;
        }

        private SidebarQuestEntry CreateSidebarEntry(QuestbookCategoryDefinition category, bool isSelected)
        {
            // Title is already language-resolved by the server.
            return new SidebarQuestEntry(category.IconItemCode, category.Title, GetCategoryProgressPercent(category), isSelected);
        }

        /// <summary>
        /// Display helper for texts that may still use legacy branch placeholders.
        /// Quest/branch content from the server is already language-resolved.
        /// </summary>
        private static string GetDisplayCategoryText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (text.StartsWith("branch:header:", StringComparison.Ordinal)
                && int.TryParse(text.AsSpan("branch:header:".Length), out int headerNumber))
            {
                return QuestbookLang.GetLocal("category.new_branch.header", headerNumber);
            }

            if (text.StartsWith("branch:", StringComparison.Ordinal)
                && int.TryParse(text.AsSpan("branch:".Length), out int titleNumber))
            {
                return QuestbookLang.GetLocal("category.new_branch.title", titleNumber);
            }

            // Legacy client-side lang keys (pre multi-lang quest data) — keep as fallback.
            if (text.Contains('.') && !text.Contains(' '))
            {
                string resolved = QuestbookLang.GetLocal(text);
                if (!resolved.StartsWith("swixyquestbook:", StringComparison.Ordinal))
                {
                    return resolved;
                }
            }

            return text;
        }

        private static string GetCategoryHeaderDisplay(QuestbookCategoryDefinition category)
        {
            if (!string.IsNullOrWhiteSpace(category.HeaderDisplay))
            {
                return category.HeaderDisplay;
            }

            return GetDisplayCategoryText(category.HeaderTitle);
        }

        private static LayoutRect CreateSidebarCardLayout(int index, double scale, double scrollOffset)
        {
            return new LayoutRect(
                QuestbookGuiLayout.SidebarCardOffsetX * scale,
                (QuestbookGuiLayout.SidebarCardOffsetY * scale) + (index * ((QuestbookGuiLayout.SidebarCardHeight + QuestbookGuiLayout.SidebarCardGap) * scale)) - scrollOffset,
                QuestbookGuiLayout.SidebarCardWidth * scale,
                QuestbookGuiLayout.SidebarCardHeight * scale
            );
        }

        private bool TrySelectCategoryFromMouse(double mouseX, double mouseY)
        {
            for (int index = 0; index < sidebarCardHitAreas.Length; index++)
            {
                if (!sidebarCardHitAreas[index].Contains(mouseX, mouseY))
                {
                    continue;
                }

                if (selectedCategoryIndex != index)
                {
                    selectedCategoryIndex = index;
                    hoveredQuestNodeId = -1;
                    CloseQuestModal();
                    shouldCenterOnStartNode = true;
                    graphZoom = DefaultGraphZoom;
                    dataManager.EnsureCategoryContentLoaded(categories[index].HeaderTitle);
                    ComposeDialog();
                }

                return true;
            }

            return false;
        }

        private bool TryOpenQuestFromMouse(double mouseX, double mouseY)
        {
            QuestbookCategoryDefinition category = GetSelectedCategory();
            for (int index = 0; index < questCardHitAreas.Length && index < category.Nodes.Length; index++)
            {
                if (!questCardHitAreas[index].Contains(mouseX, mouseY))
                {
                    continue;
                }

                QuestbookQuestNodeDefinition node = category.Nodes[index];
                QuestNodeVisualState visualState = GetNodeVisualState(category, node);
                if (visualState == QuestNodeVisualState.Inactive)
                {
                    return true;
                }

                if (node.NodeType == QuestbookQuestNodeType.Checkpoint && visualState != QuestNodeVisualState.Active)
                {
                    return true;
                }

                selectedQuestNodeId = node.Id;
                isQuestModalOpen = true;
                questModalGoalsScrollOffset = 0;
                questModalRewardsScrollOffset = 0;
                ComposeDialog();
                return true;
            }

            return false;
        }

        private bool TryDetachFromMouse(double mouseX, double mouseY)
        {
            if (!detachButtonHitArea.Contains(mouseX, mouseY))
                return false;

            ToggleDialogMovable();
            return true;
        }

        private bool TryCloseFromMouse(double mouseX, double mouseY)
        {
            if (!closeButtonHitArea.Contains(mouseX, mouseY))
                return false;

            TryClose();
            return true;
        }

        private void UpdateTopMenuButtonHover(double mouseX, double mouseY)
        {
            bool nextDetachHovered = detachButtonHitArea.Contains(mouseX, mouseY);
            bool nextCloseHovered = closeButtonHitArea.Contains(mouseX, mouseY);
            if (isDetachButtonHovered == nextDetachHovered && isCloseButtonHovered == nextCloseHovered)
                return;

            isDetachButtonHovered = nextDetachHovered;
            isCloseButtonHovered = nextCloseHovered;
            RequestContentRefresh();
        }

        private void ToggleDialogMovable()
        {
            if (!isDialogMovable)
            {
                ElementBounds? bounds = SingleComposer?.Bounds;
                if (bounds != null)
                {
                    bounds.CalcWorldBounds();
                    double guiScale = RuntimeEnv.GUIScale;
                    capi.Gui.SetDialogPosition(
                        ComposerKey,
                        new Vec2i((int)(bounds.absX / guiScale), (int)(bounds.absY / guiScale)));
                }

                isDialogMovable = true;
            }
            else
            {
                isDialogMovable = false;
                isDraggingDialog = false;
                capi.Gui.SetDialogPosition(ComposerKey, null);
                currentDialogX = 0;
                currentDialogY = 0;
            }

            ComposeDialog();
        }

        private static void ApplyAnchoredDialogBounds(ElementBounds dialogBounds)
        {
            dialogBounds.Alignment = EnumDialogArea.CenterMiddle;
            dialogBounds.fixedX = 0;
            dialogBounds.fixedY = 0;
            dialogBounds.fixedOffsetX = 0;
            dialogBounds.fixedOffsetY = 0;
            dialogBounds.absMarginX = 0;
            dialogBounds.absMarginY = 0;
        }

        private bool TryBeginDialogDrag(MouseEvent args)
        {
            if (!isDialogMovable
                || args.Button != EnumMouseButton.Left
                || !topMenuDragHitArea.Contains(args.X, args.Y)
                || detachButtonHitArea.Contains(args.X, args.Y)
                || closeButtonHitArea.Contains(args.X, args.Y))
            {
                return false;
            }

            isDraggingDialog = true;
            dialogDragStartMouseX = args.X;
            dialogDragStartMouseY = args.Y;
            return true;
        }

        private void UpdateDialogDrag(double mouseX, double mouseY)
        {
            ElementBounds? bounds = SingleComposer?.Bounds;
            if (bounds == null)
                return;

            double guiScale = RuntimeEnv.GUIScale;
            bounds.fixedX += (mouseX - dialogDragStartMouseX) / guiScale;
            bounds.fixedY += (mouseY - dialogDragStartMouseY) / guiScale;
            dialogDragStartMouseX = mouseX;
            dialogDragStartMouseY = mouseY;
            bounds.CalcWorldBounds();
            currentDialogX = bounds.absX;
            currentDialogY = bounds.absY;
        }

        private void SaveDialogPosition()
        {
            ElementBounds? bounds = SingleComposer?.Bounds;
            if (bounds == null)
                return;

            double guiScale = RuntimeEnv.GUIScale;
            double maxX = capi.Render.FrameWidth - (60 * guiScale);
            double maxY = capi.Render.FrameHeight - (60 * guiScale);
            double x = GameMath.Clamp(bounds.fixedX + bounds.fixedOffsetX, 0, maxX / guiScale) - bounds.fixedOffsetX;
            double y = GameMath.Clamp(bounds.fixedY + bounds.fixedOffsetY, 0, maxY / guiScale) - bounds.fixedOffsetY;

            bounds.fixedX = x;
            bounds.fixedY = y;
            bounds.CalcWorldBounds();
            currentDialogX = bounds.absX;
            currentDialogY = bounds.absY;
            capi.Gui.SetDialogPosition(ComposerKey, new Vec2i((int)x, (int)y));
        }

        private void InitDialogMovableStateFromSettings()
        {
            if (didInitDialogMovableState)
                return;

            if (capi.Gui.GetDialogPosition(ComposerKey) != null)
                isDialogMovable = true;

            didInitDialogMovableState = true;
        }

        private void ApplyDialogMovableBounds(ElementBounds dialogBounds)
        {
            dialogBounds.Alignment = EnumDialogArea.None;
            dialogBounds.absMarginX = 0;
            dialogBounds.absMarginY = 0;
            dialogBounds.fixedOffsetX = 0;
            dialogBounds.fixedOffsetY = 0;

            Vec2i? storedPos = capi.Gui.GetDialogPosition(ComposerKey);
            if (storedPos != null)
            {
                dialogBounds.fixedX = storedPos.X;
                dialogBounds.fixedY = System.Math.Max(-dialogBounds.fixedOffsetY, storedPos.Y);
                return;
            }

            if (currentDialogX > 0 || currentDialogY > 0)
            {
                double guiScale = RuntimeEnv.GUIScale;
                dialogBounds.fixedX = currentDialogX / guiScale;
                dialogBounds.fixedY = currentDialogY / guiScale;
            }
        }

        private void DrawTopMenuDetachButton(
            Cairo.Context ctx,
            double x,
            double y,
            double size,
            bool hovered,
            bool active)
        {
            double[] shadowColor = [0, 0, 0, 0.3];
            double[] color = active
                ? QuestbookGuiLayout.TopMenuDetachActiveColor
                : hovered
                    ? QuestbookGuiLayout.TopMenuDetachHoverColor
                    : QuestbookGuiLayout.TopMenuDetachColor;

            ctx.Operator = Operator.Over;
            capi.Gui.Icons.Drawmenuicon_svg(ctx, (int)(x + 2), (int)(y + 2), (int)size, (int)size, shadowColor);
            ctx.Operator = Operator.Over;
            capi.Gui.Icons.Drawmenuicon_svg(ctx, (int)x, (int)(y + 1), (int)size, (int)size, color);
        }

        private LayoutRect GetQuestbookDialogContentRect()
        {
            double width = QuestbookGuiLayout.BackgroundWidth * currentFitScale;
            double height = QuestbookGuiLayout.MainHeight * currentFitScale;
            return new LayoutRect(currentDialogX, currentDialogY, width, height);
        }

        private bool TryHandleQuestModalMouseDown(double mouseX, double mouseY)
        {
            if (!questModalOverlayHitArea.Contains(mouseX, mouseY))
            {
                CloseQuestModal();
                ComposeDialog();
                return true;
            }

            if (questModalCloseHitArea.Contains(mouseX, mouseY))
            {
                CloseQuestModal();
                ComposeDialog();
                return true;
            }

            if (questModalSubmitButtonHitArea.Contains(mouseX, mouseY))
            {
                return TrySubmitSelectedQuest();
            }

            return true;
        }

        private void CloseQuestModal()
        {
            isQuestModalOpen = false;
            isQuestModalCloseButtonHovered = false;
            selectedQuestNodeId = -1;
            questModalCloseHitArea = new LayoutRect(0, 0, 0, 0);
            questModalSubmitButtonHitArea = new LayoutRect(0, 0, 0, 0);
            questModalOverlayHitArea = new LayoutRect(0, 0, 0, 0);
            questModalGoalsViewportHitArea = new LayoutRect(0, 0, 0, 0);
            questModalRewardsViewportHitArea = new LayoutRect(0, 0, 0, 0);
            questModalGoalsScrollOffset = 0;
            questModalRewardsScrollOffset = 0;
            questModalGoalsMaxScroll = 0;
            questModalRewardsMaxScroll = 0;
        }

        private bool TryHandleQuestModalMouseWheel(MouseWheelEventArgs args)
        {
            int mouseX = capi.Input.MouseX;
            int mouseY = capi.Input.MouseY;
            float wheelDelta = args.deltaPrecise != 0 ? args.deltaPrecise : args.delta;
            if (wheelDelta == 0)
                return false;

            double scrollStep = questModalItemGridScrollStep > 0
                ? questModalItemGridScrollStep
                : 40 * currentFitScale;
            double direction = wheelDelta > 0 ? -1 : 1;

            if (questModalGoalsMaxScroll > 0 && questModalGoalsViewportHitArea.Contains(mouseX, mouseY))
            {
                double next = System.Math.Clamp(
                    questModalGoalsScrollOffset + (direction * scrollStep),
                    0,
                    questModalGoalsMaxScroll);
                if (System.Math.Abs(next - questModalGoalsScrollOffset) < 0.1)
                    return true;

                questModalGoalsScrollOffset = next;
                RequestContentRefresh();
                return true;
            }

            if (questModalRewardsMaxScroll > 0 && questModalRewardsViewportHitArea.Contains(mouseX, mouseY))
            {
                double next = System.Math.Clamp(
                    questModalRewardsScrollOffset + (direction * scrollStep),
                    0,
                    questModalRewardsMaxScroll);
                if (System.Math.Abs(next - questModalRewardsScrollOffset) < 0.1)
                    return true;

                questModalRewardsScrollOffset = next;
                RequestContentRefresh();
                return true;
            }

            return false;
        }

        private void OnSidebarScrollbarValue(float value)
        {
            if (isSyncingSidebarScrollbar)
            {
                return;
            }

            double nextOffset = System.Math.Max(0, value);
            if (System.Math.Abs(sidebarScrollOffset - nextOffset) < 0.1)
            {
                return;
            }

            sidebarScrollOffset = nextOffset;
            if (!isSyncingSidebarScrollbar)
            {
                ComposeDialog();
            }
        }

        private void UpdateHoveredQuestNode(double mouseX, double mouseY)
        {
            int nextHoveredQuestNodeId = -1;
            QuestbookCategoryDefinition category = GetSelectedCategory();
            for (int index = 0; index < questCardHitAreas.Length && index < category.Nodes.Length; index++)
            {
                if (!questCardHitAreas[index].Contains(mouseX, mouseY))
                {
                    continue;
                }

                if (!adminData.IsAdminPanelOpen
                    && GetNodeVisualState(category, category.Nodes[index]) == QuestNodeVisualState.Inactive)
                {
                    break;
                }

                nextHoveredQuestNodeId = category.Nodes[index].Id;
                break;
            }

            if (hoveredQuestNodeId == nextHoveredQuestNodeId)
            {
                return;
            }

            hoveredQuestNodeId = nextHoveredQuestNodeId;
            RequestContentRefresh();
        }

        private bool TryBeginRightPanelDrag(MouseEvent args)
        {
            if (args.Button != EnumMouseButton.Left || !rightPanelViewportHitArea.Contains(args.X, args.Y))
            {
                return false;
            }

            isDraggingRightPanel = true;
            dragStartMouseX = args.X;
            dragStartMouseY = args.Y;
            dragStartGraphPanX = graphPanX;
            dragStartGraphPanY = graphPanY;
            return true;
        }

        private bool TryBeginSidebarScrollbarDrag(MouseEvent args)
        {
            bool isInSidebarScrollbar = sidebarScrollbarVisualHitArea.Contains(args.X, args.Y);
            if (args.Button != EnumMouseButton.Left || !isInSidebarScrollbar)
            {
                return false;
            }

            bool isInSidebarHandle = sidebarScrollbarVisualHandleHitArea.Contains(args.X, args.Y);
            LayoutRect activeScrollbarHitArea = sidebarScrollbarVisualHitArea;
            if (!isInSidebarHandle)
            {
                double scrollbarMovableHeight = System.Math.Max(0, activeScrollbarHitArea.Height - sidebarScrollbarHandleHeight);
                double targetHandlePosition = System.Math.Clamp(
                    args.Y - activeScrollbarHitArea.Y - (sidebarScrollbarHandleHeight / 2),
                    0,
                    scrollbarMovableHeight
                );

                sidebarScrollOffset = System.Math.Clamp(
                    targetHandlePosition * sidebarScrollConversionFactor,
                    0,
                    maxSidebarScrollOffset
                );
                ComposeDialog();
            }

            isDraggingSidebarScrollbar = true;
            sidebarScrollbarDragStartMouseY = args.Y;
            sidebarScrollbarDragStartOffset = sidebarScrollOffset;
            return true;
        }

        private void UpdateGraphZoom(double anchorScreenX, double anchorScreenY, double targetZoom)
        {
            double nextZoom = System.Math.Clamp(targetZoom, MinGraphZoom, MaxGraphZoom);
            if (System.Math.Abs(nextZoom - graphZoom) < 0.0001)
            {
                return;
            }

            double anchorLocalX = anchorScreenX - rightPanelGraphBaseX;
            double anchorLocalY = anchorScreenY - rightPanelGraphBaseY;
            double worldX = (anchorLocalX - graphPanX) / graphZoom;
            double worldY = (anchorLocalY - graphPanY) / graphZoom;

            graphPanX = anchorLocalX - (worldX * nextZoom);
            graphPanY = anchorLocalY - (worldY * nextZoom);
            graphZoom = nextZoom;
        }

        private QuestbookQuestNodeDefinition? GetNodeById(QuestbookCategoryDefinition category, int nodeId)
        {
            if (graphCacheValid && graphCacheCategoryIndex == selectedCategoryIndex
                && graphNodeById.TryGetValue(nodeId, out QuestbookQuestNodeDefinition? cached))
            {
                return cached;
            }

            foreach (QuestbookQuestNodeDefinition node in category.Nodes)
            {
                if (node.Id == nodeId)
                {
                    return node;
                }
            }

            return null;
        }

        private CairoFont CreateMontserratFont(double renderSize, double[] color)
        {
            return GetMontserratFont(renderSize, color);
        }

        private CairoFont CreateTopMenuFont(double fitScale, double[] color)
        {
            return GetTopMenuFontCached(fitScale, color);
        }

        private static void DrawTopMenuText(Cairo.Context ctx, CairoFont font, string text, double x, double y)
        {
            ctx.Operator = Operator.Over;
            font.SetupContext(ctx);
            ctx.MoveTo(x, y);
            ctx.ShowText(text);

            if (font.RenderTwice)
            {
                ctx.MoveTo(x + 0.75, y);
                ctx.ShowText(text);
            }
        }

        private double MeasureTextWidth(CairoFont font, string text)
        {
            return MeasureTextWidthCached(font, text);
        }

        private static double GetTextBaselineY(CairoFont font, double boxY, double boxHeight, double lineHeight)
        {
            var fontExtents = font.GetFontExtents();
            double actualLineHeight = System.Math.Min(boxHeight, lineHeight);
            double lineBoxY = boxY + ((boxHeight - actualLineHeight) / 2);
            return lineBoxY + ((actualLineHeight - fontExtents.Height) / 2) + fontExtents.Ascent;
        }

        private static double GetTextVisualCenterY(CairoFont font, double baselineY)
        {
            FontExtents fontExtents = font.GetFontExtents();
            return baselineY - fontExtents.Ascent + (fontExtents.Height / 2);
        }

        private static void DrawText(Cairo.Context ctx, CairoFont font, string text, double x, double y)
        {
            ctx.Operator = Operator.Over;
            font.SetupContext(ctx);
            ctx.MoveTo(x, y);
            ctx.ShowText(text);
        }

        private bool HasFocusedTextInput()
        {
            if (isBranchModalOpen && isBranchModalTitleFocused && branchModalMode != BranchModalMode.DeleteConfirm)
                return true;

            if (isQuestEditModalOpen && adminData.HasSelectedNode && !adminData.FocusedField.IsNone)
                return true;

            return false;
        }

        private void ResetTextCaretBlink()
        {
            caretBlinkEpochMs = Environment.TickCount64;
            lastCaretBlinkVisible = true;
        }

        private bool IsTextCaretVisible()
        {
            // ~530ms half-period — classic text caret blink.
            long elapsed = Environment.TickCount64 - caretBlinkEpochMs;
            return (elapsed / 530) % 2 == 0;
        }

        /// <summary>
        /// Draws a vertical blinking caret after <paramref name="text"/> starting at <paramref name="textX"/>.
        /// </summary>
        private void DrawTextCaret(
            Cairo.Context ctx,
            CairoFont font,
            string text,
            double textX,
            double boxY,
            double boxHeight,
            double[]? color = null)
        {
            if (!IsTextCaretVisible())
                return;

            double textWidth = string.IsNullOrEmpty(text) ? 0 : MeasureTextWidth(font, text);
            double caretX = textX + textWidth + 1;
            double inset = System.Math.Max(3, boxHeight * 0.18);
            double y0 = boxY + inset;
            double y1 = boxY + boxHeight - inset;

            double[] c = color ?? QuestbookGuiLayout.AdminPanelTextColor;
            ctx.Operator = Operator.Over;
            ctx.SetSourceRGBA(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1.0);
            ctx.LineWidth = 1.5;
            ctx.MoveTo(caretX, y0);
            ctx.LineTo(caretX, y1);
            ctx.Stroke();
        }

        private List<string> WrapText(CairoFont font, string text, double maxWidth, int maxChars)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) { lines.Add(text); return lines; }

            if (text.Length > maxChars)
                text = text[..maxChars];

            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (string word in words)
            {
                if (MeasureTextWidth(font, word) > maxWidth)
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                        currentLine = "";
                    }
                    for (int i = 0; i < word.Length; i++)
                    {
                        string testLine = currentLine + word[i];
                        if (MeasureTextWidth(font, testLine) > maxWidth)
                        {
                            if (!string.IsNullOrEmpty(currentLine))
                                lines.Add(currentLine);
                            currentLine = word[i].ToString();
                        }
                        else
                        {
                            currentLine = testLine;
                        }
                    }
                }
                else
                {
                    string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    if (MeasureTextWidth(font, testLine) <= maxWidth)
                    {
                        currentLine = testLine;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(currentLine))
                            lines.Add(currentLine);
                        currentLine = word;
                    }
                }
            }
            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            if (lines.Count == 0) lines.Add("");
            return lines;
        }

        private static void DrawImageSurface(Cairo.Context ctx, ImageSurface surface, double x, double y, double width, double height)
        {
            if (surface.Width <= 0 || surface.Height <= 0 || width <= 0 || height <= 0)
            {
                return;
            }

            ctx.Save();
            ctx.Translate(x, y);
            ctx.Scale(width / surface.Width, height / surface.Height);
            ctx.SetSourceSurface(surface, 0, 0);
            ctx.Paint();
            ctx.Restore();
        }

        private static void DrawImageSurfaceRotated(Cairo.Context ctx, ImageSurface surface, double centerX, double centerY, double width, double height, double angleRadians)
        {
            if (surface.Width <= 0 || surface.Height <= 0 || width <= 0 || height <= 0)
            {
                return;
            }

            ctx.Save();
            ctx.Translate(centerX, centerY);
            ctx.Rotate(angleRadians);
            ctx.Translate(-width / 2, -height / 2);
            ctx.Scale(width / surface.Width, height / surface.Height);
            ctx.SetSourceSurface(surface, 0, 0);
            ctx.Paint();
            ctx.Restore();
        }

        private static void FillRectangle(Cairo.Context ctx, double x, double y, double width, double height, double[] color)
        {
            ctx.SetSourceRGBA(color[0], color[1], color[2], color[3]);
            GuiElement.Rectangle(ctx, x, y, width, height);
            ctx.Fill();
        }

        private static void DrawRectangleStroke(Cairo.Context ctx, double x, double y, double width, double height, double lineWidth, double[] color)
        {
            ctx.SetSourceRGBA(color[0], color[1], color[2], color[3]);
            ctx.LineWidth = lineWidth;
            GuiElement.Rectangle(ctx, x, y, width, height);
            ctx.Stroke();
        }

        private static double GetNodeRenderSize(QuestbookQuestNodeDefinition node, double graphScale)
        {
            double baseSize = node.NodeType == QuestbookQuestNodeType.Start
                ? QuestbookGuiLayout.GraphStartNodeSize
                : QuestbookGuiLayout.GraphNodeSize;
            return baseSize * graphScale;
        }

        private static double GetNodeCenterOffset(QuestbookQuestNodeDefinition node)
        {
            return node.NodeType == QuestbookQuestNodeType.Start
                ? 0
                : QuestbookGuiLayout.GraphNodeCenterOffset;
        }

        private static (double CenterX, double CenterY) GetNodeScreenCenter(
            QuestbookQuestNodeDefinition node,
            double viewportX,
            double viewportY,
            double graphPanX,
            double graphPanY,
            double graphScale)
        {
            double nodeSize = GetNodeRenderSize(node, graphScale);
            double offset = GetNodeCenterOffset(node) * graphScale;
            double x = viewportX + graphPanX + ((node.X + offset) * graphScale);
            double y = viewportY + graphPanY + ((node.Y + offset) * graphScale);
            return (x + nodeSize / 2, y + nodeSize / 2);
        }

        private static LayoutRect GetNodeScreenRect(
            QuestbookQuestNodeDefinition node,
            double viewportX,
            double viewportY,
            double graphPanX,
            double graphPanY,
            double graphScale)
        {
            double nodeSize = GetNodeRenderSize(node, graphScale);
            double offset = GetNodeCenterOffset(node) * graphScale;
            return new LayoutRect(
                viewportX + graphPanX + ((node.X + offset) * graphScale),
                viewportY + graphPanY + ((node.Y + offset) * graphScale),
                nodeSize,
                nodeSize
            );
        }

        private static string GetNodeBaseTexture(QuestNodeVisualState visualState, QuestbookQuestNodeType nodeType)
        {
            // Kill reuses quest art (no separate sprites yet).
            QuestbookQuestNodeType artType = nodeType == QuestbookQuestNodeType.Kill
                ? QuestbookQuestNodeType.Quest
                : nodeType;

            if (visualState == QuestNodeVisualState.Inactive)
            {
                return artType switch
                {
                    QuestbookQuestNodeType.Start => "start.png",
                    QuestbookQuestNodeType.Checkpoint => "checkpoint_notactive.png",
                    _ => "quest_notactive.png"
                };
            }

            if (visualState == QuestNodeVisualState.Completed)
            {
                return artType switch
                {
                    QuestbookQuestNodeType.Start => "start_completed.png",
                    QuestbookQuestNodeType.Checkpoint => "checkpoint_completed.png",
                    _ => "quest_completed.png"
                };
            }

            return artType switch
            {
                QuestbookQuestNodeType.Start => "start.png",
                QuestbookQuestNodeType.Checkpoint => "checkpoint.png",
                _ => "quest.png"
            };
        }

        private static string GetNodeHoverTexture(QuestbookQuestNodeType nodeType)
        {
            return nodeType == QuestbookQuestNodeType.Start ? "start_hover.png" : "quest_hover.png";
        }

        private static bool ShouldShowNodeHover(QuestbookQuestNodeDefinition? node, QuestNodeVisualState visualState, bool isHovered)
        {
            if (!isHovered || node == null)
            {
                return false;
            }

            if (node.NodeType == QuestbookQuestNodeType.Start)
            {
                return visualState != QuestNodeVisualState.Inactive;
            }

            if (node.NodeType == QuestbookQuestNodeType.Checkpoint)
            {
                return visualState == QuestNodeVisualState.Active;
            }

            return visualState == QuestNodeVisualState.Active || visualState == QuestNodeVisualState.Completed;
        }

        private static string GetConnectionLineTexture(
            QuestNodeVisualState startNodeState,
            QuestNodeVisualState endNodeState)
        {
            if (startNodeState == QuestNodeVisualState.Completed && endNodeState == QuestNodeVisualState.Completed)
            {
                return "line_completed.png";
            }

            if (startNodeState == QuestNodeVisualState.Inactive || endNodeState == QuestNodeVisualState.Inactive)
            {
                return "line_notactive.png";
            }

            return "line.png";
        }

        private static string GetLineTextureFileName(string primaryTextureFileName)
        {
            return primaryTextureFileName;
        }

        private static void DrawCubeIcon(Cairo.Context ctx, double x, double y, double size)
        {
            double half = size / 2;
            double quarter = size / 4;
            double threeQuarter = quarter * 3;

            ctx.SetSourceRGBA(
                QuestbookGuiLayout.GraphCubeTopColor[0],
                QuestbookGuiLayout.GraphCubeTopColor[1],
                QuestbookGuiLayout.GraphCubeTopColor[2],
                QuestbookGuiLayout.GraphCubeTopColor[3]
            );
            ctx.MoveTo(x + half, y);
            ctx.LineTo(x + size, y + quarter);
            ctx.LineTo(x + half, y + half);
            ctx.LineTo(x, y + quarter);
            ctx.ClosePath();
            ctx.Fill();

            ctx.SetSourceRGBA(
                QuestbookGuiLayout.GraphCubeLeftColor[0],
                QuestbookGuiLayout.GraphCubeLeftColor[1],
                QuestbookGuiLayout.GraphCubeLeftColor[2],
                QuestbookGuiLayout.GraphCubeLeftColor[3]
            );
            ctx.MoveTo(x, y + quarter);
            ctx.LineTo(x + half, y + half);
            ctx.LineTo(x + half, y + size);
            ctx.LineTo(x, y + threeQuarter);
            ctx.ClosePath();
            ctx.Fill();

            ctx.SetSourceRGBA(
                QuestbookGuiLayout.GraphCubeRightColor[0],
                QuestbookGuiLayout.GraphCubeRightColor[1],
                QuestbookGuiLayout.GraphCubeRightColor[2],
                QuestbookGuiLayout.GraphCubeRightColor[3]
            );
            ctx.MoveTo(x + size, y + quarter);
            ctx.LineTo(x + half, y + half);
            ctx.LineTo(x + half, y + size);
            ctx.LineTo(x + size, y + threeQuarter);
            ctx.ClosePath();
            ctx.Fill();
        }

        private void DrawCenteredText(Cairo.Context ctx, CairoFont font, string text, LayoutRect area)
        {
            double textWidth = MeasureTextWidth(font, text);
            double textX = area.X + ((area.Width - textWidth) / 2);
            double textY = GetTextBaselineY(font, area.Y, area.Height, font.UnscaledFontsize);
            DrawText(ctx, font, text, textX, textY);
        }

        private static double[] GetModalProgressStatusColor(int progressPercent, bool isCompleted)
        {
            if (isCompleted || progressPercent >= 100)
            {
                return QuestbookGuiLayout.ModalProgressDoneColor;
            }

            if (progressPercent <= 0)
            {
                return QuestbookGuiLayout.ModalProgressZeroColor;
            }

            return QuestbookGuiLayout.ModalProgressActiveColor;
        }

        private void DrawCenteredQuestStatusLine(
            Cairo.Context ctx,
            double fitScale,
            LayoutRect area,
            int progressPercent,
            bool isCompleted)
        {
            string questPrefix = QuestbookLang.GetLocal("modal.quest_prefix");
            string statusText = isCompleted
                ? QuestbookLang.GetLocal("modal.completed")
                : $"{progressPercent}%".ToUpperInvariant();
            double[] statusColor = GetModalProgressStatusColor(progressPercent, isCompleted);

            CairoFont prefixFont = CreateMontserratFont(32 * fitScale, QuestbookGuiLayout.ModalQuestLabelColor);
            CairoFont statusFont = CreateMontserratFont(32 * fitScale, statusColor);

            double prefixWidth = MeasureTextWidth(prefixFont, questPrefix);
            double statusWidth = MeasureTextWidth(statusFont, statusText);
            double totalWidth = prefixWidth + statusWidth;
            double startX = area.X + ((area.Width - totalWidth) / 2);
            double baselineY = GetTextBaselineY(prefixFont, area.Y, area.Height, 32 * fitScale);

            DrawText(ctx, prefixFont, questPrefix, startX, baselineY);
            DrawText(ctx, statusFont, statusText, startX + prefixWidth, baselineY);
        }

        private void DrawSidebarCard(
            Cairo.Context ctx,
            SidebarQuestEntry entry,
            LayoutRect cardRect,
            double scale,
            LayoutRect iconClipLocal = default)
        {
            ImageSurface? cardSurface = GetTextureSurface(entry.IsSelected ? "background_leftbar_hover.png" : "background_leftbar.png");
            if (cardSurface != null)
            {
                DrawImageSurface(ctx, cardSurface, cardRect.X, cardRect.Y, cardRect.Width, cardRect.Height);
            }
            else
            {
                FillRectangle(ctx, cardRect.X, cardRect.Y, cardRect.Width, cardRect.Height, QuestbookGuiLayout.ModalBorderColor);
            }

            double iconSize = QuestbookGuiLayout.SidebarIconSize * scale;
            double iconX = cardRect.X + (QuestbookGuiLayout.SidebarIconOffsetX * scale);
            double iconY = cardRect.Y + (QuestbookGuiLayout.SidebarIconOffsetY * scale);
            LayoutRect iconRect = new(iconX, iconY, iconSize, iconSize);
            if (!string.IsNullOrWhiteSpace(entry.IconItemCode))
            {
                DummySlot? iconSlot = GetQuestItemIconSlot(entry.IconItemCode);
                if (iconSlot?.Itemstack != null)
                {
                    sidebarIconRenderRequests.Add(new QuestItemIconRenderRequest(
                        entry.IconItemCode,
                        iconRect,
                        false,
                        0,
                        QuestbookItemIconContext.Sidebar,
                        iconClipLocal));
                }
                else
                {
                    DrawMissingIcon(ctx, iconX, iconY, iconSize);
                }
            }
            else
            {
                DrawMissingIcon(ctx, iconX, iconY, iconSize);
            }

            CairoFont sidebarFont = CreateMontserratFont(QuestbookGuiLayout.SidebarFontSize * scale, QuestbookGuiLayout.SidebarTitleColor);
            string progressText = $"{entry.ProgressPercent}%";
            double progressWidth = MeasureTextWidth(sidebarFont, progressText);
            double titleX = cardRect.X + (QuestbookGuiLayout.SidebarTitleOffsetX * scale);
            double progressX = cardRect.X + cardRect.Width - (QuestbookGuiLayout.SidebarProgressRightPadding * scale) - progressWidth;
            double textBaselineY = GetTextBaselineY(sidebarFont, cardRect.Y, cardRect.Height, QuestbookGuiLayout.SidebarLineHeight * scale);

            string displayTitle = entry.Title;
            if (displayTitle.Length > 14)
            {
                displayTitle = displayTitle.Substring(0, 12) + "...";
            }

            DrawText(ctx, sidebarFont, displayTitle, titleX, textBaselineY);
            DrawText(ctx, CreateMontserratFont(QuestbookGuiLayout.SidebarFontSize * scale, GetProgressColor(entry.ProgressPercent)), progressText, progressX, textBaselineY);
        }

        private void DrawMissingIcon(Cairo.Context ctx, double x, double y, double size)
        {
            ImageSurface? missingIcon = GetMissingIconSurface();
            if (missingIcon != null)
            {
                DrawImageSurface(ctx, missingIcon, x, y, size, size);
                return;
            }

            DrawCubeIcon(ctx, x, y, size);
        }

        // Заглушка отсутствующей иконки хранится в папке assets/swixyquestbook/icon.
        private ImageSurface? GetMissingIconSurface()
        {
            return GetSidebarIconSurface("icon_missing.png");
        }

        private LayoutRect DrawQuestNode(
    Cairo.Context ctx,
    QuestNodeVisualState visualState,
    bool isReadyToSubmit,
    bool isHovered,
    bool isSelected,
    double x,
    double y,
    double size,
    double scale,
    QuestbookQuestNodeDefinition? node = null)
        {
            QuestbookQuestNodeType nodeType = node?.NodeType ?? QuestbookQuestNodeType.Quest;
            string baseTextureFileName = GetNodeBaseTexture(visualState, nodeType);
            string hoverTextureFileName = GetNodeHoverTexture(nodeType);
            double nodeSize = node == null
                ? size
                : GetNodeRenderSize(node, scale);

            ImageSurface? baseSurface = GetTextureSurface(baseTextureFileName);
            if (baseSurface != null)
            {
                DrawImageSurface(ctx, baseSurface, x, y, nodeSize, nodeSize);
            }
            else
            {
                FillRectangle(ctx, x, y, nodeSize, nodeSize, QuestbookGuiLayout.ModalBorderColor);
            }

            if (isReadyToSubmit
                && nodeType is QuestbookQuestNodeType.Quest or QuestbookQuestNodeType.Kill)
            {
                ImageSurface? activeSurface = GetTextureSurface("active.png");
                if (activeSurface != null)
                {
                    DrawImageSurface(ctx, activeSurface, x, y, nodeSize, nodeSize);
                }
            }

            if (ShouldShowNodeHover(node, visualState, isHovered))
            {
                ImageSurface? hoverSurface = GetTextureSurface(hoverTextureFileName);
                if (hoverSurface != null)
                {
                    DrawImageSurface(ctx, hoverSurface, x, y, nodeSize, nodeSize);
                }
            }

            double iconSize = QuestbookGuiLayout.GraphNodeItemIconSize * scale;
            return new LayoutRect(
                x + ((nodeSize - iconSize) / 2),
                y + ((nodeSize - iconSize) / 2),
                iconSize,
                iconSize
            );
        }

        private void DrawQuestConnection(
            Cairo.Context ctx,
            QuestbookQuestNodeDefinition startNode,
            QuestbookQuestNodeDefinition endNode,
            QuestNodeVisualState startNodeState,
            QuestNodeVisualState endNodeState,
            double viewportX,
            double viewportY,
            double graphScale,
            LayoutRect viewportRect)
        {
            (double startX, double startY) = GetNodeScreenCenter(startNode, viewportX, viewportY, graphPanX, graphPanY, graphScale);
            (double endX, double endY) = GetNodeScreenCenter(endNode, viewportX, viewportY, graphPanX, graphPanY, graphScale);

            if (!SegmentMayHitViewport(startX, startY, endX, endY, viewportRect, GraphCullMargin))
            {
                return;
            }

            double deltaX = endX - startX;
            double deltaY = endY - startY;
            double distance = System.Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance <= 1.0)
            {
                return;
            }

            // Center-to-center (same as original textured lines). Connections are drawn
            // under node sprites, so they visually attach to the quest art without gaps.
            DrawConnectionLine(ctx, startX, startY, endX, endY, startNodeState, endNodeState, graphScale);
        }

        private static LayoutRect GetQuestModalPanelRect(double dialogX, double dialogY, double fitScale)
        {
            // Coordinates from ТЗ (1920x1080) - relative to main GUI
            double baseDialogX = (1920 - QuestbookGuiLayout.BackgroundWidth) / 2;
            double baseDialogY = (1080 - QuestbookGuiLayout.BackgroundHeight) / 2 - QuestbookGuiLayout.BackgroundOffsetY;
            double ToScreenX(double baseX) => dialogX + (baseX - baseDialogX) * fitScale;
            double ToScreenY(double baseY) => dialogY + (baseY - baseDialogY - 28) * fitScale;
            double ToScreenSize(double baseSize) => baseSize * fitScale;

            return new LayoutRect(
                ToScreenX(772),
                ToScreenY(256),
                ToScreenSize(712),
                ToScreenSize(630)
            );
        }

        private LayoutRect DrawQuestModal(
            Cairo.Context ctx,
            QuestbookQuestNodeDefinition node,
            int currentCount,
            bool canSubmit,
            bool isSubmitPending,
            bool isCompleted,
            double screenX,
            double screenY,
            double fitScale)
        {
            bool usesInfoModalLayout = node.UsesInfoModalLayout;
            bool isStartNode = node.NodeType == QuestbookQuestNodeType.Start;
            bool isCheckpointNode = node.NodeType == QuestbookQuestNodeType.Checkpoint;

            LayoutRect panelRect = GetQuestModalPanelRect(0, 0, fitScale);
            questModalOverlayHitArea = panelRect.Offset(screenX, screenY);

            double panelX = panelRect.X;
            double panelY = panelRect.Y;
            double panelWidth = panelRect.Width;
            double panelHeight = panelRect.Height;

            // Flow layout relative to modal panel — avoids hard-coded 1920 coords and overlaps.
            double padX = 36 * fitScale;
            double padTop = 28 * fitScale;
            // Design (1920): start button at Y=748 in a 630-tall panel starting at 256
            // → ~64px clearance under the button. Too-small padBottom sinks the CTA into the frame.
            double padBottom = usesInfoModalLayout ? 56 * fitScale : 40 * fitScale;
            double contentX = panelX + padX;
            double contentW = panelWidth - (padX * 2);
            double cursorY = panelY + padTop;

            double statusH = 34 * fitScale;
            double titleH = 36 * fitScale;
            double sectionGap = 14 * fitScale;
            double buttonH = QuestbookGuiLayout.ModalButtonHeight * fitScale;
            double closeSize = QuestbookGuiLayout.ModalCloseSize * fitScale;

            // Reserve bottom for action button first.
            double buttonY = panelY + panelHeight - padBottom - buttonH;
            double buttonW = contentW;
            double buttonX = contentX;

            int totalRequiredCount = GetNodeRequiredTotalCount(node);
            double percent = totalRequiredCount <= 0
                ? (isCompleted ? 100 : 0)
                : ((double)currentCount / System.Math.Max(1, totalRequiredCount)) * 100;
            int progressPercent = (int)System.Math.Clamp(System.Math.Round(percent, MidpointRounding.AwayFromZero), 0, 100);

            string buttonTextureFileName = isCompleted
                ? "button_completed.png"
                : canSubmit && !isSubmitPending
                    ? "button_active.png"
                    : "button_notactive.png";

            string buttonText;
            if (isStartNode)
            {
                buttonText = isCompleted
                    ? QuestbookLang.GetLocal("modal.path_started")
                    : isSubmitPending
                        ? QuestbookLang.GetLocal("modal.processing")
                        : QuestbookLang.GetLocal("modal.start_path");
            }
            else if (isCheckpointNode)
            {
                buttonText = isCompleted
                    ? QuestbookLang.GetLocal("modal.passed")
                    : isSubmitPending
                        ? QuestbookLang.GetLocal("modal.processing")
                        : QuestbookLang.GetLocal("modal.continue");
            }
            else
            {
                buttonText = isCompleted
                    ? QuestbookLang.GetLocal("modal.received")
                    : isSubmitPending
                        ? QuestbookLang.GetLocal("modal.processing")
                        : QuestbookLang.GetLocal("modal.claim_reward");
            }

            double[] buttonTextColor = isCompleted || (canSubmit && !isSubmitPending)
                ? QuestbookGuiLayout.TopMenuTitleColor
                : [102.0 / 255.0, 102.0 / 255.0, 102.0 / 255.0, 1.0];
            if (usesInfoModalLayout && isStartNode && canSubmit && !isSubmitPending && !isCompleted)
            {
                buttonTextColor = QuestbookGuiLayout.ModalStartButtonTextColor;
            }

            // Panel background (no full-window dimming — keeps the book UI visible behind the modal)
            ImageSurface? modalSurface = GetTextureSurface("modal.png");
            if (modalSurface != null)
            {
                DrawImageSurface(ctx, modalSurface, panelX, panelY, panelWidth, panelHeight);
            }
            else
            {
                FillRectangle(ctx, panelX, panelY, panelWidth, panelHeight, QuestbookGuiLayout.ModalBackgroundColor);
            }

            // Close button (top-right). Design ModalCloseY is a bit low in the live frame — nudge up ~30px.
            const double designModalPanelY = 256;
            const double designModalPanelRightInset = 72; // panel right (772+712) − close right (1368+44)
            const double closeYNudgeUp = 30;
            double closeButtonX = panelX + panelWidth - (designModalPanelRightInset * fitScale) - closeSize;
            double closeButtonY = panelY + ((QuestbookGuiLayout.ModalCloseY - designModalPanelY - closeYNudgeUp) * fitScale);
            string closeButtonTextureFileName = isQuestModalCloseButtonHovered ? "close_active.png" : "close.png";
            ImageSurface? closeButtonSurface = GetTextureSurface(closeButtonTextureFileName);
            if (closeButtonSurface != null)
            {
                DrawImageSurface(ctx, closeButtonSurface, closeButtonX, closeButtonY, closeSize, closeSize);
            }

            // Status row (leave space for close on the right)
            double statusW = contentW - closeSize - (12 * fitScale);
            LayoutRect questLabelRect = new(contentX, cursorY, statusW, statusH);
            DrawCenteredQuestStatusLine(ctx, fitScale, questLabelRect, progressPercent, isCompleted);
            cursorY += statusH + (8 * fitScale);

            // Title
            string displayTitle = isStartNode
                ? GetCategoryHeaderDisplay(GetSelectedCategory())
                : isCheckpointNode
                    ? (string.IsNullOrWhiteSpace(node.Description)
                        ? GetCategoryHeaderDisplay(GetSelectedCategory())
                        : node.Description)
                    : (node.RequiredItems.Length > 0
                        ? (GetQuestItemSlot(node.RequiredItems[0].CollectibleCode)?.Itemstack?.GetName()
                            ?? node.RequiredItems[0].CollectibleCode)
                        : "");
            if (displayTitle.Length > 28)
            {
                displayTitle = displayTitle[..25] + "...";
            }

            CairoFont titleFont = CreateMontserratFont(28 * fitScale, [1.0, 1.0, 1.0, 1.0]);
            LayoutRect titleRect = new(contentX, cursorY, contentW, titleH);
            DrawCenteredText(ctx, titleFont, displayTitle, titleRect);
            cursorY += titleH + sectionGap;

            double goalBoxX = 0, goalBoxY = 0, goalBoxWidth = 0, goalBoxHeight = 0;
            double rewardBoxX = 0, rewardBoxY = 0;
            ImageSurface? modalBoxSurface = GetTextureSurface("modalbox.png");
            ImageSurface? modalTextBoxSurface = GetTextureSurface("modaltextbox.png");

            if (usesInfoModalLayout)
            {
                questModalGoalsViewportHitArea = new LayoutRect(0, 0, 0, 0);
                questModalRewardsViewportHitArea = new LayoutRect(0, 0, 0, 0);
                questModalGoalsMaxScroll = 0;
                questModalRewardsMaxScroll = 0;

                // Description fills the space above the CTA, with a clear gap so the button
                // does not sit flush against the text box (was reading as "too low").
                double infoToButtonGap = 20 * fitScale;
                double infoBoxH = System.Math.Max(120 * fitScale, buttonY - cursorY - infoToButtonGap);
                double infoBoxX = contentX;
                double infoBoxY = cursorY;
                double infoBoxW = contentW;

                if (modalBoxSurface != null)
                {
                    DrawImageSurface(ctx, modalBoxSurface, infoBoxX, infoBoxY, infoBoxW, infoBoxH);
                }

                double textPad = 16 * fitScale;
                LayoutRect infoRect = new(
                    infoBoxX + textPad,
                    infoBoxY + textPad,
                    infoBoxW - (textPad * 2),
                    infoBoxH - (textPad * 2));

                CairoFont infoFont = CreateMontserratFont(16 * fitScale, QuestbookGuiLayout.ModalStartInfoTextColor);
                string infoText = string.IsNullOrWhiteSpace(node.Description)
                    ? QuestbookLang.GetLocal("modal.info_placeholder")
                    : node.Description;
                DrawWrappedInfoText(ctx, infoFont, infoText, infoRect, QuestbookGuiLayout.ModalStartDescriptionMaxLength, 24 * fitScale);
            }
            else
            {
                // Goals | Rewards boxes
                double colGap = 16 * fitScale;
                goalBoxWidth = (contentW - colGap) / 2;
                // Prefer a taller item area so goal labels (craft/turn-in/obtain) fit; overflow scrolls.
                double availableForBoxes = buttonY - cursorY - sectionGap - (100 * fitScale) - sectionGap;
                goalBoxHeight = System.Math.Clamp(
                    availableForBoxes > 0 ? availableForBoxes : 170 * fitScale,
                    140 * fitScale,
                    240 * fitScale);

                goalBoxX = contentX;
                goalBoxY = cursorY;
                rewardBoxX = contentX + goalBoxWidth + colGap;
                rewardBoxY = cursorY;

                if (modalBoxSurface != null)
                {
                    DrawImageSurface(ctx, modalBoxSurface, goalBoxX, goalBoxY, goalBoxWidth, goalBoxHeight);
                    DrawImageSurface(ctx, modalBoxSurface, rewardBoxX, rewardBoxY, goalBoxWidth, goalBoxHeight);
                }

                double labelH = 24 * fitScale;
                double goalsHintH = 16 * fitScale;
                CairoFont sectionLeftFont = CreateMontserratFont(20 * fitScale, [85.0 / 255.0, 255.0 / 255.0, 255.0 / 255.0, 1.0]);
                CairoFont sectionRightFont = CreateMontserratFont(20 * fitScale, [255.0 / 255.0, 170.0 / 255.0, 0.0, 1.0]);
                DrawCenteredText(ctx, sectionLeftFont, QuestbookLang.GetLocal("modal.goals"),
                    new LayoutRect(goalBoxX, goalBoxY + (6 * fitScale), goalBoxWidth, labelH));
                DrawCenteredText(ctx, sectionRightFont, QuestbookLang.GetLocal("modal.rewards"),
                    new LayoutRect(rewardBoxX, rewardBoxY + (6 * fitScale), goalBoxWidth, labelH));

                // Short player-facing hint: craft / turn in / obtain.
                string goalsHint = BuildGoalsActionHint(node);
                if (!string.IsNullOrWhiteSpace(goalsHint))
                {
                    CairoFont hintFont = CreateMontserratFont(11 * fitScale, [0.75, 0.82, 0.88, 0.95]);
                    DrawCenteredText(
                        ctx,
                        hintFont,
                        goalsHint,
                        new LayoutRect(goalBoxX + (6 * fitScale), goalBoxY + labelH + (2 * fitScale), goalBoxWidth - (12 * fitScale), goalsHintH));
                }

                double iconAreaTop = goalBoxY + labelH + goalsHintH + (10 * fitScale);
                double iconAreaH = goalBoxHeight - labelH - goalsHintH - (18 * fitScale);
                double iconSize = System.Math.Min(44 * fitScale, iconAreaH * 0.55);

                if (node.SupportsItemIcon)
                {
                    DrawModalItemGrid(
                        ctx,
                        node.RequiredItems,
                        goalBoxX,
                        iconAreaTop,
                        goalBoxWidth,
                        iconAreaH,
                        iconSize,
                        fitScale,
                        isGoal: true,
                        screenX,
                        screenY,
                        nodeId: node.Id,
                        nodeType: node.NodeType);
                    DrawModalItemGrid(
                        ctx,
                        node.RewardItems,
                        rewardBoxX,
                        iconAreaTop,
                        goalBoxWidth,
                        iconAreaH,
                        iconSize,
                        fitScale,
                        isGoal: false,
                        screenX,
                        screenY,
                        nodeType: node.NodeType);
                }
                else
                {
                    questModalGoalsViewportHitArea = new LayoutRect(0, 0, 0, 0);
                    questModalRewardsViewportHitArea = new LayoutRect(0, 0, 0, 0);
                    questModalGoalsMaxScroll = 0;
                    questModalRewardsMaxScroll = 0;
                }

                cursorY = goalBoxY + goalBoxHeight + sectionGap;

                // Description box
                double textBoxH = System.Math.Max(72 * fitScale, buttonY - cursorY - sectionGap);
                double textBoxX = contentX;
                double textBoxY = cursorY;
                double textBoxW = contentW;

                if (modalTextBoxSurface != null)
                {
                    DrawImageSurface(ctx, modalTextBoxSurface, textBoxX, textBoxY, textBoxW, textBoxH);
                }

                double textPad = 14 * fitScale;
                LayoutRect infoRect = new(
                    textBoxX + textPad,
                    textBoxY + textPad,
                    textBoxW - (textPad * 2),
                    textBoxH - (textPad * 2));
                CairoFont infoFont = CreateMontserratFont(15 * fitScale, [1.0, 1.0, 0.3333333333, 1.0]);
                string infoText = string.IsNullOrWhiteSpace(node.Description)
                    ? QuestbookLang.GetLocal("modal.info_placeholder")
                    : node.Description;
                DrawWrappedInfoText(ctx, infoFont, infoText, infoRect, 220, 22 * fitScale);
            }

            // Action button
            LayoutRect buttonRect = new(buttonX, buttonY, buttonW, buttonH);
            ImageSurface? buttonSurface = GetTextureSurface(buttonTextureFileName);
            if (buttonSurface != null)
            {
                DrawImageSurface(ctx, buttonSurface, buttonRect.X, buttonRect.Y, buttonRect.Width, buttonRect.Height);
            }
            else
            {
                FillRectangle(ctx, buttonRect.X, buttonRect.Y, buttonRect.Width, buttonRect.Height, QuestbookGuiLayout.ModalButtonDisabledColor);
            }

            CairoFont buttonFont = CreateMontserratFont(28 * fitScale, buttonTextColor);
            DrawCenteredText(ctx, buttonFont, buttonText, buttonRect);

            questModalCloseHitArea = new LayoutRect(closeButtonX, closeButtonY, closeSize, closeSize)
                .Offset(screenX, screenY);
            questModalSubmitButtonHitArea = canSubmit && !isSubmitPending
                ? buttonRect.Offset(screenX, screenY)
                : new LayoutRect(0, 0, 0, 0);

            return usesInfoModalLayout
                ? new LayoutRect(0, 0, 0, 0)
                : new LayoutRect(goalBoxX, goalBoxY, goalBoxWidth, goalBoxHeight);
        }

        private void DrawWrappedInfoText(
            Cairo.Context ctx,
            CairoFont infoFont,
            string infoText,
            LayoutRect infoRect,
            int infoCharLimit,
            double infoLineHeight)
        {
            List<string> infoLines = WrapText(infoFont, infoText, infoRect.Width, infoCharLimit);
            int infoMaxLines = System.Math.Max(1, (int)(infoRect.Height / infoLineHeight));
            if (infoLines.Count > infoMaxLines)
            {
                infoLines = infoLines.Take(infoMaxLines).ToList();
                string lastLine = infoLines[^1];
                if (lastLine.Length > 3)
                {
                    infoLines[^1] = lastLine[..^3] + "...";
                }
            }

            for (int li = 0; li < infoLines.Count; li++)
            {
                double lineY = infoRect.Y + (li * infoLineHeight);
                double lineBaselineY = GetTextBaselineY(infoFont, lineY, infoLineHeight, infoLineHeight);
                DrawText(ctx, infoFont, infoLines[li], infoRect.X, lineBaselineY);
            }
        }

        /// <summary>
        /// Player-facing goal action: craft / turn in (consume) / obtain (detect-only).
        /// Uses per-item flags.
        /// </summary>
        private static string GetGoalActionLabel(QuestbookQuestItemRequirement item, QuestbookQuestNodeType nodeType)
        {
            if (item.IsKillObjective)
                return QuestbookLang.GetLocal("modal.goal.kill");
            if (item.IsCraftObjective && item.Consume)
                return QuestbookLang.GetLocal("modal.goal.craft_turn_in");
            if (item.IsCraftObjective)
                return QuestbookLang.GetLocal("modal.goal.craft");
            if (item.IsDetectObjective || !item.Consume)
                return QuestbookLang.GetLocal("modal.goal.obtain");
            return QuestbookLang.GetLocal("modal.goal.turn_in");
        }

        private static double[] GetGoalActionColor(QuestbookQuestItemRequirement item, QuestbookQuestNodeType nodeType)
        {
            if (item.IsKillObjective)
                return [1.0, 0.40, 0.40, 1.0]; // kill — red
            if (item.IsCraftObjective && item.Consume)
                return [0.70, 0.85, 1.0, 1.0]; // craft + turn in
            if (item.IsCraftObjective)
                return [0.40, 0.95, 1.0, 1.0]; // craft — cyan
            if (item.IsDetectObjective || !item.Consume)
                return [0.55, 0.92, 0.55, 1.0]; // obtain / detect — green
            return [1.0, 0.72, 0.28, 1.0]; // turn in — amber
        }

        private static string BuildGoalsActionHint(QuestbookQuestNodeDefinition node)
        {
            if (node.RequiredItems.Length == 0)
                return string.Empty;

            bool hasCraft = false;
            bool hasCraftTurnIn = false;
            bool hasTurnIn = false;
            bool hasObtain = false;
            bool hasKill = false;
            foreach (QuestbookQuestItemRequirement item in node.RequiredItems)
            {
                if (string.IsNullOrWhiteSpace(item.CollectibleCode))
                    continue;
                if (item.IsKillObjective)
                    hasKill = true;
                else if (item.IsCraftObjective && item.Consume)
                    hasCraftTurnIn = true;
                else if (item.IsCraftObjective)
                    hasCraft = true;
                else if (item.IsDetectObjective || !item.Consume)
                    hasObtain = true;
                else
                    hasTurnIn = true;
            }

            int kinds = (hasCraft ? 1 : 0) + (hasCraftTurnIn ? 1 : 0) + (hasTurnIn ? 1 : 0)
                + (hasObtain ? 1 : 0) + (hasKill ? 1 : 0);
            if (kinds == 0)
                return string.Empty;
            if (kinds > 1)
                return QuestbookLang.GetLocal("modal.goals_hint.mixed");
            if (hasKill)
                return QuestbookLang.GetLocal("modal.goals_hint.kill");
            if (hasCraftTurnIn)
                return QuestbookLang.GetLocal("modal.goals_hint.craft_turn_in");
            if (hasCraft)
                return QuestbookLang.GetLocal("modal.goals_hint.craft");
            if (hasTurnIn)
                return QuestbookLang.GetLocal("modal.goals_hint.turn_in");
            return QuestbookLang.GetLocal("modal.goals_hint.obtain");
        }

        private int GetGoalProgressCurrent(QuestbookQuestItemRequirement item, int nodeId)
        {
            string categoryKey = GetSelectedCategory().HeaderTitle;

            if (item.IsKillObjective)
            {
                return dataManager.GetKillProgress(categoryKey, nodeId, item.CollectibleCode);
            }

            if (item.IsCraftObjective && item.RequiresInventory)
            {
                int crafted = dataManager.GetCraftProgress(categoryKey, nodeId, item.CollectibleCode);
                int held = CountPlayerCollectibles(item.CollectibleCode);
                return System.Math.Min(crafted, held);
            }

            if (item.IsCraftObjective)
            {
                return dataManager.GetCraftProgress(categoryKey, nodeId, item.CollectibleCode);
            }

            return CountPlayerCollectibles(item.CollectibleCode);
        }

        private void DrawModalItemGrid(
            Cairo.Context ctx,
            QuestbookQuestItemRequirement[] items,
            double boxX,
            double boxY,
            double boxWidth,
            double boxHeight,
            double baseIconSize,
            double fitScale,
            bool isGoal,
            double screenX,
            double screenY,
            int nodeId = 0,
            QuestbookQuestNodeType nodeType = QuestbookQuestNodeType.Quest)
        {
            LayoutRect viewportLocal = new(boxX, boxY, boxWidth, boxHeight);
            LayoutRect viewportScreen = viewportLocal.Offset(screenX, screenY);

            if (items.Length == 0)
            {
                if (isGoal)
                {
                    questModalGoalsViewportHitArea = viewportScreen;
                    questModalGoalsMaxScroll = 0;
                    questModalGoalsScrollOffset = 0;
                }
                else
                {
                    questModalRewardsViewportHitArea = viewportScreen;
                    questModalRewardsMaxScroll = 0;
                    questModalRewardsScrollOffset = 0;
                }

                return;
            }

            int itemCount = items.Length;
            double gap = 8 * fitScale;
            double hPad = 8 * fitScale;
            double scrollbarGap = 4 * fitScale;
            double scrollbarWidth = QuestbookGuiLayout.QuestEditModalListScrollbarWidth * fitScale;
            double availableHeight = System.Math.Max(8, boxHeight);

            // Goals reserve space under each icon for action label + progress.
            double actionLabelH = isGoal ? 13 * fitScale : 0;
            double progressLabelH = isGoal ? 12 * fitScale : 0;
            double labelGap = isGoal ? 2 * fitScale : 0;
            double cellExtra = isGoal ? (actionLabelH + progressLabelH + labelGap + (4 * fitScale)) : 0;

            // Keep a readable icon size; extra items wrap into more rows and scroll.
            double iconSize = System.Math.Clamp(baseIconSize, 26 * fitScale, isGoal ? 42 * fitScale : 48 * fitScale);
            double cellHeight = iconSize + cellExtra;
            double contentWidth = System.Math.Max(8, boxWidth - (hPad * 2));
            // Columns = how many fit by width, but never more than the item count
            // so 1–2 items sit as a centered group instead of left-aligned in a wide grid.
            // For goals with labels, prefer fewer columns so text is readable.
            double minCellW = isGoal ? System.Math.Max(iconSize, 52 * fitScale) : iconSize;
            int maxColumns = System.Math.Max(1, (int)System.Math.Floor((contentWidth + gap) / (minCellW + gap)));
            int columns = System.Math.Max(1, System.Math.Min(maxColumns, itemCount));
            int rows = (int)System.Math.Ceiling(itemCount / (double)columns);
            double contentHeight = (rows * cellHeight) + (System.Math.Max(0, rows - 1) * gap);
            double maxScroll = System.Math.Max(0, contentHeight - availableHeight);

            if (maxScroll > 0)
            {
                // Reserve track so icons never sit under the scrollbar.
                contentWidth = System.Math.Max(8, boxWidth - (hPad * 2) - scrollbarWidth - scrollbarGap);
                maxColumns = System.Math.Max(1, (int)System.Math.Floor((contentWidth + gap) / (minCellW + gap)));
                columns = System.Math.Max(1, System.Math.Min(maxColumns, itemCount));
                rows = (int)System.Math.Ceiling(itemCount / (double)columns);
                contentHeight = (rows * cellHeight) + (System.Math.Max(0, rows - 1) * gap);
                maxScroll = System.Math.Max(0, contentHeight - availableHeight);
            }

            double scrollOffset = isGoal ? questModalGoalsScrollOffset : questModalRewardsScrollOffset;
            scrollOffset = System.Math.Clamp(scrollOffset, 0, maxScroll);
            if (isGoal)
            {
                questModalGoalsScrollOffset = scrollOffset;
                questModalGoalsMaxScroll = maxScroll;
                questModalGoalsViewportHitArea = viewportScreen;
            }
            else
            {
                questModalRewardsScrollOffset = scrollOffset;
                questModalRewardsMaxScroll = maxScroll;
                questModalRewardsViewportHitArea = viewportScreen;
            }

            questModalItemGridScrollStep = (cellHeight + gap) * 0.85;

            // Fit: center vertically. Overflow: top-align and scroll.
            double startY = maxScroll > 0
                ? boxY - scrollOffset
                : boxY + System.Math.Max(0, (availableHeight - contentHeight) / 2);

            ctx.Save();
            ctx.Rectangle(boxX, boxY, boxWidth, boxHeight);
            ctx.Clip();

            for (int i = 0; i < itemCount; i++)
            {
                QuestbookQuestItemRequirement item = items[i];
                if (string.IsNullOrWhiteSpace(item.CollectibleCode))
                {
                    continue;
                }

                int col = i % columns;
                int row = i / columns;
                // Center each row by its actual item count (last row with 1–2 items stays centered).
                int rowStartIndex = row * columns;
                int itemsInRow = System.Math.Min(columns, itemCount - rowStartIndex);
                double cellW = isGoal ? System.Math.Max(iconSize, minCellW) : iconSize;
                double rowWidth = (itemsInRow * cellW) + (System.Math.Max(0, itemsInRow - 1) * gap);
                double rowStartX = boxX + hPad + System.Math.Max(0, (contentWidth - rowWidth) / 2);
                double cellX = rowStartX + (col * (cellW + gap));
                double iy = startY + (row * (cellHeight + gap));
                double ix = cellX + System.Math.Max(0, (cellW - iconSize) / 2);

                // Fully outside the scroll viewport — skip work.
                if (iy + cellHeight < boxY || iy > boxY + boxHeight)
                {
                    continue;
                }

                LayoutRect itemRect = new(ix, iy, iconSize, iconSize);
                DummySlot? itemSlot = GetQuestItemIconSlot(item.CollectibleCode);

                if (itemSlot?.Itemstack != null)
                {
                    questItemIconRenderRequests.Add(
                        new QuestItemIconRenderRequest(
                            item.CollectibleCode,
                            itemRect,
                            false,
                            item.Count,
                            QuestbookItemIconContext.Modal,
                            viewportLocal
                        )
                    );
                }
                else if (item.CollectibleCode.Contains('*'))
                {
                    // Don't draw long codes under icons (overflow) — use missing glyph.
                    DrawMissingIcon(ctx, itemRect.X, itemRect.Y, itemRect.Width);
                }
                else
                {
                    DrawMissingIcon(ctx, itemRect.X, itemRect.Y, itemRect.Width);
                }

                if (!isGoal)
                {
                    continue;
                }

                string actionText = GetGoalActionLabel(item, nodeType);
                double[] actionColor = GetGoalActionColor(item, nodeType);
                CairoFont coloredActionFont = CreateMontserratFont(11 * fitScale, actionColor);

                double labelY = iy + iconSize + labelGap;
                DrawCenteredText(
                    ctx,
                    coloredActionFont,
                    actionText,
                    new LayoutRect(cellX, labelY, cellW, actionLabelH));

                int current = System.Math.Min(item.Count, GetGoalProgressCurrent(item, nodeId));
                string progressText = QuestbookLang.GetLocal("modal.goal.progress", current, item.Count);
                bool done = current >= item.Count && item.Count > 0;
                double[] progressColor = done
                    ? QuestbookGuiLayout.ModalProgressDoneColor
                    : current > 0
                        ? QuestbookGuiLayout.ModalProgressActiveColor
                        : [0.70, 0.72, 0.75, 0.95];
                CairoFont coloredProgressFont = CreateMontserratFont(10 * fitScale, progressColor);
                DrawCenteredText(
                    ctx,
                    coloredProgressFont,
                    progressText,
                    new LayoutRect(cellX, labelY + actionLabelH, cellW, progressLabelH));
            }

            if (maxScroll > 0)
            {
                double trackX = boxX + boxWidth - hPad - scrollbarWidth;
                double trackY = boxY;
                double trackH = availableHeight;
                double thumbHeight = System.Math.Max(18 * fitScale, trackH * (availableHeight / contentHeight));
                double thumbTravel = System.Math.Max(1, trackH - thumbHeight);
                double thumbY = trackY + ((scrollOffset / maxScroll) * thumbTravel);
                FillRoundedRectangle(
                    ctx,
                    trackX,
                    trackY,
                    scrollbarWidth,
                    trackH,
                    3 * fitScale,
                    [0.22, 0.24, 0.27, 0.7]);
                FillRoundedRectangle(
                    ctx,
                    trackX,
                    thumbY,
                    scrollbarWidth,
                    thumbHeight,
                    3 * fitScale,
                    QuestbookGuiLayout.AdminTileBorderColor);
            }

            ctx.Restore();
        }

        private DummySlot? GetQuestItemIconSlot(string collectibleCode)
        {
            if (string.IsNullOrWhiteSpace(collectibleCode))
            {
                return null;
            }

            if (collectibleCode.Contains('*'))
                return GetCyclingWildcardSlot(collectibleCode);

            if (questItemIconSlotCache.TryGetValue(collectibleCode, out DummySlot? cachedIconSlot))
            {
                return cachedIconSlot;
            }

            DummySlot? slot = CreateQuestItemSlot(collectibleCode);

            if (slot != null)
            {
                questItemIconSlotCache[collectibleCode] = slot;
            }

            return slot;
        }

        private DummySlot? GetQuestItemSlot(string collectibleCode)
        {
            if (string.IsNullOrWhiteSpace(collectibleCode))
            {
                return null;
            }

            if (questItemSlotCache.TryGetValue(collectibleCode, out DummySlot? cachedSlot))
            {
                return cachedSlot;
            }

            if (collectibleCode.Contains('*'))
                return GetCyclingWildcardSlot(collectibleCode);

            DummySlot? slot = CreateQuestItemSlot(collectibleCode);
            if (slot != null)
            {
                questItemSlotCache[collectibleCode] = slot;
            }

            return slot;
        }

        private DummySlot? GetCyclingWildcardSlot(string collectibleCode)
        {
            List<DummySlot> slots = GetWildcardSlots(collectibleCode);
            if (slots.Count == 0)
                return null;

            return slots[(wildcardCycleFrame / 60) % slots.Count];
        }

        private List<DummySlot> GetWildcardSlots(string collectibleCode)
        {
            if (!wildcardSlotCache.TryGetValue(collectibleCode, out List<DummySlot>? slots))
            {
                // Cap matches and skip expensive mesh tesselation — only needed for a cycling icon.
                const int maxWildcardIcons = 8;
                slots = FindAllMatchingSlots(collectibleCode, maxWildcardIcons);
                // Kill goals use entity codes (drifter-*); icons live on creature-drifter-*.
                if (slots.Count == 0 && !collectibleCode.Contains("creature-", StringComparison.OrdinalIgnoreCase))
                {
                    AssetLocation loc = new(collectibleCode);
                    string path = loc.Path ?? collectibleCode;
                    if (!path.StartsWith("creature-", StringComparison.OrdinalIgnoreCase))
                    {
                        string domain = string.IsNullOrEmpty(loc.Domain) ? "game" : loc.Domain;
                        slots = FindAllMatchingSlots($"{domain}:creature-{path}", maxWildcardIcons);
                    }
                }

                if (slots.Count == 0)
                {
                    slots = [];
                }

                wildcardSlotCache[collectibleCode] = slots;
            }

            return slots;
        }

        private DummySlot? CreateQuestItemSlot(string collectibleCode)
        {
            ItemStack? stack = QuestbookItemDisplayHelper.CreateDisplayStack(capi, collectibleCode);
            return stack != null ? new DummySlot(stack) : null;
        }

        private List<DummySlot> FindAllMatchingSlots(string pattern, int maxResults = 32)
        {
            var result = new List<DummySlot>(System.Math.Min(maxResults, 16));
            if (maxResults <= 0)
            {
                return result;
            }

            string cleanPattern = pattern.Contains(':') ? pattern[(pattern.IndexOf(':') + 1)..] : pattern;

            string prefix = cleanPattern.TrimEnd('*');
            string suffix = cleanPattern.TrimStart('*');
            string middle = cleanPattern.Trim('*');
            bool startsWith = cleanPattern.EndsWith('*') && !cleanPattern.StartsWith('*');
            bool endsWith = cleanPattern.StartsWith('*') && !cleanPattern.EndsWith('*');
            bool contains = cleanPattern.StartsWith('*') && cleanPattern.EndsWith('*');

            bool TryAdd(CollectibleObject? collectible)
            {
                if (result.Count >= maxResults)
                {
                    return false;
                }

                if (collectible?.Code == null)
                {
                    return true;
                }

                string code = collectible.Code.Path;
                if (string.IsNullOrEmpty(code))
                {
                    return true;
                }

                bool match = (startsWith && code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                          || (endsWith && code.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                          || (contains && code.Contains(middle, StringComparison.OrdinalIgnoreCase));

                if (match)
                {
                    result.Add(new DummySlot(new ItemStack(collectible)));
                }

                return result.Count < maxResults;
            }

            foreach (CollectibleObject? item in capi.World.Items)
            {
                if (!TryAdd(item))
                {
                    return result;
                }
            }

            foreach (CollectibleObject? block in capi.World.Blocks)
            {
                if (!TryAdd(block))
                {
                    return result;
                }
            }

            return result;
        }

        private void RefreshDialogScreenPosition()
        {
            ElementBounds? bounds = SingleComposer?.Bounds;
            if (bounds == null)
                return;

            bounds.CalcWorldBounds();
            currentDialogX = bounds.absX;
            currentDialogY = bounds.absY;
        }

        private LayoutRect ToScreenRect(LayoutRect localRect)
        {
            if (localRect.IsEmpty)
                return localRect;

            return localRect.Offset(currentDialogX, currentDialogY);
        }

        private void RenderQueuedItemIcons(
            IReadOnlyList<QuestItemIconRenderRequest> requests,
            float deltaTime,
            bool? clipToRightPanelFilter = null,
            bool hideUnderOpenModals = false)
        {
            LayoutRect rightPanelViewport = ToScreenRect(rightPanelViewportLocal);

            foreach (QuestItemIconRenderRequest request in requests)
            {
                if (clipToRightPanelFilter.HasValue && request.ClipToRightPanel != clipToRightPanelFilter.Value)
                    continue;

                DummySlot? slot = GetQuestItemIconSlot(request.CollectibleCode);
                if (slot?.Itemstack == null)
                    continue;

                LayoutRect screenSlot = ToScreenRect(request.LocalSlotRect);

                if (hideUnderOpenModals && IconIntersectsOpenModal(screenSlot))
                    continue;

                // Resolve optional viewport clip (graph / scroll lists) in screen space.
                LayoutRect screenClip = default;
                if (request.ClipToRightPanel)
                {
                    screenClip = rightPanelViewport;
                }
                else if (!request.LocalClipRect.IsEmpty)
                {
                    screenClip = ToScreenRect(request.LocalClipRect);
                }

                if (!screenClip.IsEmpty)
                {
                    // Skip only when the icon is completely outside the viewport.
                    if (screenSlot.Intersect(screenClip).IsEmpty)
                        continue;
                }

                bool showStackSize = !request.ClipToRightPanel && request.DisplayCount > 0;
                if (!screenClip.IsEmpty)
                {
                    guiItemIconRenderer.Render(
                        slot,
                        screenSlot.X,
                        screenSlot.Y,
                        (float)screenSlot.Width,
                        GetItemIconRenderZ(request.Context),
                        deltaTime,
                        request.DisplayCount > 0 ? request.DisplayCount : 1,
                        showStackSize,
                        screenClip.X,
                        screenClip.Y,
                        screenClip.Width,
                        screenClip.Height);
                }
                else
                {
                    guiItemIconRenderer.Render(
                        slot,
                        screenSlot.X,
                        screenSlot.Y,
                        (float)screenSlot.Width,
                        GetItemIconRenderZ(request.Context),
                        deltaTime,
                        request.DisplayCount > 0 ? request.DisplayCount : 1,
                        showStackSize);
                }
            }
        }

        private static float GetItemIconRenderZ(QuestbookItemIconContext context)
        {
            return context switch
            {
                QuestbookItemIconContext.QuestNode => 90f,
                QuestbookItemIconContext.Sidebar => 91f,
                QuestbookItemIconContext.Modal => 92f,
                _ => 90f,
            };
        }

        private void RenderPickerSlotIcons(
            IEnumerable<(ItemSlot Slot, LayoutRect HitArea)> slots,
            float deltaTime)
        {
            LayoutRect viewportScreen = adminEntityPickerViewportLocal.Width > 0
                ? ToScreenRect(adminEntityPickerViewportLocal)
                : default;
            double clipX = viewportScreen.Width > 0 ? viewportScreen.X : double.NaN;
            double clipY = viewportScreen.Width > 0 ? viewportScreen.Y : double.NaN;
            double clipW = viewportScreen.Width > 0 ? viewportScreen.Width : double.NaN;
            double clipH = viewportScreen.Width > 0 ? viewportScreen.Height : double.NaN;

            foreach ((ItemSlot slot, LayoutRect hitArea) in slots)
            {
                if (slot.Itemstack?.Collectible?.Code == null)
                    continue;

                LayoutRect screenSlot = ToScreenRect(hitArea);
                if (viewportScreen.Width > 0
                    && (screenSlot.Y + screenSlot.Height < viewportScreen.Y
                        || screenSlot.Y > viewportScreen.Y + viewportScreen.Height))
                    continue;

                guiItemIconRenderer.Render(
                    slot,
                    screenSlot.X,
                    screenSlot.Y,
                    (float)screenSlot.Width,
                    GetItemIconRenderZ(QuestbookItemIconContext.Modal),
                    deltaTime,
                    displayCount: 1,
                    showStackSize: false,
                    clipX: clipX,
                    clipY: clipY,
                    clipWidth: clipW,
                    clipHeight: clipH);
            }
        }

        /// <summary>
        /// Cheap GL hover for catalog tiles — avoids Cairo recompose on every mouse move.
        /// <paramref name="underIcons"/> draws the fill under meshes; false draws the border on top.
        /// Clipped to the scroll viewport so half-visible rows don't stick under the search box.
        /// </summary>
        private void RenderPickerHoverHighlight(bool underIcons)
        {
            if (adminEntityPickerHoverRect.Width <= 0 || adminEntityPickerHoverRect.Height <= 0)
                return;
            if (adminItemPickerTarget == null)
                return;

            LayoutRect screen = ToScreenRect(adminEntityPickerHoverRect);
            if (adminEntityPickerViewportLocal.Width > 0)
            {
                screen = screen.Intersect(ToScreenRect(adminEntityPickerViewportLocal));
                if (screen.IsEmpty)
                    return;
            }

            if (underIcons)
            {
                capi.Render.RenderRectangle(
                    (float)screen.X,
                    (float)screen.Y,
                    89f,
                    (float)screen.Width,
                    (float)screen.Height,
                    ColorUtil.ToRgba(90, 40, 160, 55));
                return;
            }

            // Border above meshes (92), below tooltip (200).
            const float z = 96f;
            int green = ColorUtil.ToRgba(230, 90, 251, 87);
            capi.Render.RenderRectangle((float)screen.X, (float)screen.Y, z, (float)screen.Width, 2.2f, green);
            capi.Render.RenderRectangle((float)screen.X, (float)(screen.Y + screen.Height - 2.2), z, (float)screen.Width, 2.2f, green);
            capi.Render.RenderRectangle((float)screen.X, (float)screen.Y, z, 2.2f, (float)screen.Height, green);
            capi.Render.RenderRectangle((float)(screen.X + screen.Width - 2.2), (float)screen.Y, z, 2.2f, (float)screen.Height, green);
        }

        /// <summary>
        /// Renders ItemCreature stacks in kill picker tiles (vanilla creative uses the same path).
        /// </summary>
        private void RenderEntityCreaturePickerIcons(float deltaTime)
        {
            if (adminEntityPickerSlots.Length == 0)
                return;

            LayoutRect viewportScreen = adminEntityPickerViewportLocal.Width > 0
                ? ToScreenRect(adminEntityPickerViewportLocal)
                : default;

            double clipX = viewportScreen.Width > 0 ? viewportScreen.X : double.NaN;
            double clipY = viewportScreen.Width > 0 ? viewportScreen.Y : double.NaN;
            double clipW = viewportScreen.Width > 0 ? viewportScreen.Width : double.NaN;
            double clipH = viewportScreen.Width > 0 ? viewportScreen.Height : double.NaN;

            foreach ((string _, string _, DummySlot slot, LayoutRect hitArea) in adminEntityPickerSlots)
            {
                if (slot.Itemstack?.Collectible == null)
                    continue;

                LayoutRect screen = ToScreenRect(hitArea);
                if (viewportScreen.Width > 0
                    && (screen.Y + screen.Height < viewportScreen.Y
                        || screen.Y > viewportScreen.Y + viewportScreen.Height))
                    continue;

                guiItemIconRenderer.Render(
                    slot,
                    screen.X,
                    screen.Y,
                    (float)screen.Width,
                    GetItemIconRenderZ(QuestbookItemIconContext.Modal),
                    deltaTime,
                    displayCount: 1,
                    showStackSize: false,
                    clipX: clipX,
                    clipY: clipY,
                    clipWidth: clipW,
                    clipHeight: clipH);
            }
        }

        /// <summary>
        /// Hover name tooltip drawn after 3D models.
        /// Uses vanilla TextBackground so the panel has a solid fill (not just bare text).
        /// Z must be above Modal item icons (92) or models cover the tooltip.
        /// </summary>
        private void RenderEntityPickerHoverTooltip()
        {
            if (string.IsNullOrWhiteSpace(adminEntityPickerHoverLabel))
                return;

            try
            {
                CairoFont font = CreateMontserratFont(13, QuestbookGuiLayout.TopMenuTitleColor);
                if (entityPickerTooltipTexture == null)
                    entityPickerTooltipTexture = new LoadedTexture(capi);

                if (!string.Equals(entityPickerTooltipCachedText, adminEntityPickerHoverLabel, StringComparison.Ordinal))
                {
                    // Bake opaque background + border into the text texture (vanilla hover-text path).
                    var background = new TextBackground
                    {
                        HorPadding = 10,
                        Radius = 4,
                        BorderWidth = 1.6,
                        FillColor = [0.07, 0.08, 0.10, 0.96],
                        BorderColor =
                        [
                            QuestbookGuiLayout.AdminSaveButtonColor[0],
                            QuestbookGuiLayout.AdminSaveButtonColor[1],
                            QuestbookGuiLayout.AdminSaveButtonColor[2],
                            1.0
                        ],
                        Shade = true
                    };
                    var util = new TextTextureUtil(capi);
                    util.GenOrUpdateTextTexture(
                        adminEntityPickerHoverLabel,
                        font,
                        ref entityPickerTooltipTexture,
                        background);
                    entityPickerTooltipCachedText = adminEntityPickerHoverLabel;
                }

                if (entityPickerTooltipTexture.TextureId <= 0)
                    return;

                // Above creature models (Modal Z = 92).
                const float tooltipZ = 202f;
                double boxW = entityPickerTooltipTexture.Width;
                double boxH = entityPickerTooltipTexture.Height;

                // Prefer centered just above the hovered tile so the name sits over the creature.
                double boxX;
                double boxY;
                if (adminEntityPickerHoverRect.Width > 0 && adminEntityPickerHoverRect.Height > 0)
                {
                    LayoutRect screenTile = ToScreenRect(adminEntityPickerHoverRect);
                    // Clip tile to viewport so tooltip anchors to the visible part.
                    if (adminEntityPickerViewportLocal.Width > 0)
                        screenTile = screenTile.Intersect(ToScreenRect(adminEntityPickerViewportLocal));
                    if (screenTile.IsEmpty)
                        return;

                    boxX = screenTile.X + ((screenTile.Width - boxW) / 2);
                    boxY = screenTile.Y - boxH - 6;
                    // If not enough space above, place just below the tile.
                    if (boxY < 4)
                        boxY = screenTile.Y + screenTile.Height + 6;
                }
                else
                {
                    int mouseX = capi.Input.MouseX;
                    int mouseY = capi.Input.MouseY;
                    boxX = mouseX + 14;
                    boxY = mouseY - boxH - 10;
                }

                if (boxX < 4)
                    boxX = 4;
                if (boxX + boxW > capi.Render.FrameWidth - 4)
                    boxX = capi.Render.FrameWidth - boxW - 4;
                if (boxY < 4)
                    boxY = 4;
                if (boxY + boxH > capi.Render.FrameHeight - 4)
                    boxY = capi.Render.FrameHeight - boxH - 4;

                capi.Render.Render2DTexturePremultipliedAlpha(
                    entityPickerTooltipTexture.TextureId,
                    (float)boxX,
                    (float)boxY,
                    entityPickerTooltipTexture.Width,
                    entityPickerTooltipTexture.Height,
                    tooltipZ);
            }
            catch
            {
                // Ignore tooltip draw failures — picker still usable.
            }
        }

        private ImageSurface? GetSidebarIconSurface(string iconFileName)
        {
            return textureHelper.GetIcon(iconFileName);
        }

        private ImageSurface? GetTextureSurface(string textureFileName)
        {
            return textureHelper.GetTexture(textureFileName);
        }

        private void ComposeDialog()
        {
            // Structural / interactive changes: recompose immediately, but never recurse.
            ComposeDialogImmediate();
        }

        private void ComposeDialogImmediate()
        {
            if (composeDialogRunning)
            {
                pendingComposeDialog = true;
                return;
            }

            composeDialogRunning = true;
            pendingComposeDialog = false;
            try
            {
                ComposeDialogCore();
            }
            finally
            {
                composeDialogRunning = false;
            }
        }

        private void ComposeDialogCore()
        {
            double windowWidth = capi.Gui.WindowBounds.OuterWidth;
            double windowHeight = capi.Gui.WindowBounds.OuterHeight;
            double guiScale = System.Math.Max(0.0001, GuiElement.scaled(1));
            double margin = GuiElement.scaled(ScreenMargin);
            double availableWidth = System.Math.Max(1, windowWidth - margin);
            double availableHeight = System.Math.Max(1, windowHeight - margin);
            double fitScale = guiScale;
            double scaledMainWidth = QuestbookGuiLayout.MainWidth * fitScale;
            double scaledMainHeight = QuestbookGuiLayout.MainHeight * fitScale;

            if (scaledMainWidth > availableWidth || scaledMainHeight > availableHeight)
            {
                fitScale = System.Math.Min(
                    availableWidth / QuestbookGuiLayout.MainWidth,
                    availableHeight / QuestbookGuiLayout.MainHeight);
            }

            double contentWidth = QuestbookGuiLayout.BackgroundWidth * fitScale;
            double contentHeight = QuestbookGuiLayout.MainHeight * fitScale;
            currentFitScale = fitScale;
            double sidebarViewportHeight = QuestbookGuiLayout.SidebarViewportHeight * fitScale;
            ElementBounds mainBounds = ElementBounds.Fixed(0, 0, contentWidth / guiScale, contentHeight / guiScale);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, contentWidth / guiScale, contentHeight / guiScale);
            bgBounds.BothSizing = ElementSizing.Fixed;
            bgBounds.WithChildren(mainBounds);
            InitDialogMovableStateFromSettings();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle);
            if (isDialogMovable)
                ApplyDialogMovableBounds(dialogBounds);
            else
                ApplyAnchoredDialogBounds(dialogBounds);
            double sidebarViewportX = QuestbookGuiLayout.SidebarCardOffsetX * fitScale;
            double sidebarViewportY = QuestbookGuiLayout.SidebarCardOffsetY * fitScale;
            double sidebarViewportWidth = QuestbookGuiLayout.SidebarCardWidth * fitScale;
            double backgroundBottom = (QuestbookGuiLayout.BackgroundOffsetY + QuestbookGuiLayout.BackgroundHeight) * fitScale;
            double sidebarVisibleHeight = System.Math.Max(
                0,
                System.Math.Min(sidebarViewportHeight, backgroundBottom - sidebarViewportY - (24 * fitScale))
            );
            double sidebarContentHeight = (categories.Length * (QuestbookGuiLayout.SidebarCardHeight + QuestbookGuiLayout.SidebarCardGap) * fitScale)
                - (QuestbookGuiLayout.SidebarCardGap * fitScale);
            sidebarScrollStep = (QuestbookGuiLayout.SidebarCardHeight + QuestbookGuiLayout.SidebarCardGap) * fitScale;
            double sidebarAdminButtonsOffset = GetSidebarAdminButtonsOffset(fitScale);
            double sidebarListVisibleHeight = System.Math.Max(0, sidebarVisibleHeight - sidebarAdminButtonsOffset);
            maxSidebarScrollOffset = System.Math.Max(0, sidebarContentHeight - sidebarListVisibleHeight);
            sidebarScrollOffset = System.Math.Clamp(sidebarScrollOffset, 0, maxSidebarScrollOffset);
            double sidebarScrollbarX = sidebarViewportX
                + sidebarViewportWidth
                + (QuestbookGuiLayout.SidebarScrollbarGap * fitScale)
                + (QuestbookGuiLayout.SidebarScrollbarOffsetX * fitScale);
            double sidebarScrollbarY = sidebarViewportY
                + sidebarAdminButtonsOffset
                + (QuestbookGuiLayout.SidebarScrollbarOffsetY * fitScale);
            double sidebarScrollbarWidth = QuestbookGuiLayout.SidebarScrollbarWidth * fitScale;
            double sidebarScrollbarHeight = System.Math.Max(
                0,
                System.Math.Min(
                    sidebarListVisibleHeight,
                    backgroundBottom - sidebarScrollbarY - (24 * fitScale))
            );
            double totalScrollableHeight = System.Math.Max(sidebarListVisibleHeight, sidebarContentHeight);
            sidebarScrollbarHandleHeight = sidebarScrollbarHeight;
            sidebarScrollbarHandlePosition = 0;
            sidebarScrollConversionFactor = 1;
            if (totalScrollableHeight > 0 && sidebarScrollbarHeight > 0)
            {
                double heightDiffFactor = System.Math.Clamp(sidebarListVisibleHeight / totalScrollableHeight, 0, 1);
                sidebarScrollbarHandleHeight = System.Math.Max(10, heightDiffFactor * sidebarScrollbarHeight);
                double scrollbarMovableHeight = System.Math.Max(0, sidebarScrollbarHeight - sidebarScrollbarHandleHeight);
                double innerMovableHeight = System.Math.Max(0, totalScrollableHeight - sidebarListVisibleHeight);
                sidebarScrollConversionFactor = scrollbarMovableHeight <= 0 || innerMovableHeight <= 0
                    ? 1
                    : innerMovableHeight / scrollbarMovableHeight;
                sidebarScrollbarHandlePosition = sidebarScrollConversionFactor <= 0
                    ? 0
                    : System.Math.Clamp(sidebarScrollOffset / sidebarScrollConversionFactor, 0, scrollbarMovableHeight);
            }
            ElementBounds sidebarScrollbarBounds = ElementBounds.Fixed(
                sidebarScrollbarX / guiScale,
                sidebarScrollbarY / guiScale,
                sidebarScrollbarWidth / guiScale,
                sidebarScrollbarHeight / guiScale
            );
            sidebarScrollbarHitArea = new LayoutRect(
                sidebarScrollbarX,
                sidebarScrollbarY,
                sidebarScrollbarWidth,
                sidebarScrollbarHeight
            );
            sidebarScrollbarHandleHitArea = new LayoutRect(
                sidebarScrollbarX,
                sidebarScrollbarY + sidebarScrollbarHandlePosition,
                sidebarScrollbarWidth,
                sidebarScrollbarHandleHeight
            );
            ClearComposers();
            sidebarCardHitAreas = new LayoutRect[categories.Length];
            questCardHitAreas = new LayoutRect[GetSelectedCategory().Nodes.Length];
            questItemIconRenderRequests.Clear();
            questModalCloseHitArea = new LayoutRect(0, 0, 0, 0);
            questModalSubmitButtonHitArea = new LayoutRect(0, 0, 0, 0);

            var composer = capi.Gui
                .CreateCompo(ComposerKey, dialogBounds)
                .BeginChildElements(bgBounds)
                .AddDynamicCustomDraw(mainBounds, (ctx, surface, bounds) =>
                {
                    questItemIconRenderRequests.Clear();
                    adminEditorIconRenderRequests.Clear();
                    sidebarIconRenderRequests.Clear();
                    branchModalIconRenderRequests.Clear();
                    double screenX = bounds.absX;
                    double screenY = bounds.absY;
                    currentDialogX = screenX;
                    currentDialogY = screenY;
                    dialogContentScreenRect = new LayoutRect(screenX, screenY, contentWidth, contentHeight);

                    QuestbookCategoryDefinition selectedCategory = GetSelectedCategory();
                    int progressPercent = System.Math.Clamp(GetCategoryProgressPercent(selectedCategory), 0, 100);
                    string progressText = $"{progressPercent}%";

                    CairoFont titleFont = CreateTopMenuFont(fitScale, QuestbookGuiLayout.TopMenuTitleColor);
                    CairoFont separatorFont = CreateTopMenuFont(fitScale, QuestbookGuiLayout.TopMenuSeparatorColor);
                    CairoFont sectionFont = CreateTopMenuFont(fitScale, QuestbookGuiLayout.TopMenuSectionColor);
                    CairoFont progressFont = CreateTopMenuFont(fitScale, GetProgressColor(progressPercent));
                    CairoFont closeFont = CreateTopMenuFont(
                        fitScale,
                        isCloseButtonHovered ? QuestbookGuiLayout.TopMenuCloseHoverColor : QuestbookGuiLayout.TopMenuCloseColor);
                    CairoFont closeHotkeyFont = CreateTopMenuFont(fitScale, QuestbookGuiLayout.TopMenuCloseHotkeyColor);
                    double topMenuPaddingX = QuestbookGuiLayout.TopMenuPaddingX * fitScale;
                    double topMenuWidth = QuestbookGuiLayout.TopMenuWidth * fitScale;
                    double topMenuTitleGap = QuestbookGuiLayout.TopMenuTitleGap * fitScale;
                    double topMenuSectionGap = QuestbookGuiLayout.TopMenuSectionGap * fitScale;
                    double topMenuProgressGap = QuestbookGuiLayout.TopMenuProgressGap * fitScale;
                    double topMenuCloseGap = QuestbookGuiLayout.TopMenuCloseGap * fitScale;
                    double topMenuDetachGap = QuestbookGuiLayout.TopMenuDetachGap * fitScale;
                    double topMenuDetachButtonSize = QuestbookGuiLayout.TopMenuDetachButtonSize * fitScale;
                    double topMenuDetachIconSize = QuestbookGuiLayout.TopMenuDetachIconSize * fitScale;
                    double topMenuRightPadding = QuestbookGuiLayout.TopMenuRightPadding * fitScale;
                    double topMenuHeight = QuestbookGuiLayout.TopMenuHeight * fitScale;
                    double topMenuLineHeight = QuestbookGuiLayout.TopMenuLineHeight * fitScale;

                    double titleWidth = MeasureTextWidth(titleFont, TopMenuTitleText);
                    double separatorWidth = MeasureTextWidth(separatorFont, TopMenuSeparatorText);
                    double sectionWidth = MeasureTextWidth(sectionFont, GetCategoryHeaderDisplay(selectedCategory));
                    double progressWidth = MeasureTextWidth(progressFont, progressText);
                    double closeWidth = MeasureTextWidth(closeFont, TopMenuCloseText);
                    double closeHotkeyWidth = MeasureTextWidth(closeHotkeyFont, TopMenuCloseHotkeyText);
                    double topTextBaselineY = GetTextBaselineY(titleFont, 0, topMenuHeight, topMenuLineHeight);

                    if (DebugDialogAlignmentLayout)
                    {
                        double screenCenterX = windowWidth / 2;
                        double screenCenterY = windowHeight / 2;
                        double backgroundX = 0;
                        double backgroundY = QuestbookGuiLayout.BackgroundOffsetY * fitScale;
                        double backgroundWidth = QuestbookGuiLayout.BackgroundWidth * fitScale;
                        double backgroundHeight = QuestbookGuiLayout.BackgroundHeight * fitScale;
                        double centeredBackgroundX = (windowWidth - backgroundWidth) / 2;
                        double centeredBackgroundY = (windowHeight - backgroundHeight) / 2;
                        double markerThickness = System.Math.Max(1, 2 * fitScale);

                        FillRectangle(ctx, screenCenterX - 1, 0, 2, windowHeight, [0.98, 0.84, 0.18, 0.28]);
                        FillRectangle(ctx, 0, screenCenterY - 1, windowWidth, 2, [0.18, 0.82, 0.98, 0.28]);
                        DrawRectangleStroke(ctx, centeredBackgroundX, centeredBackgroundY, backgroundWidth, backgroundHeight, markerThickness, [0.97, 0.26, 0.84, 0.95]);
                        DrawRectangleStroke(ctx, backgroundX, backgroundY, backgroundWidth, backgroundHeight, markerThickness, [0.24, 0.95, 0.42, 0.95]);
                    }

                    ImageSurface? backgroundSurface = GetTextureSurface("background.png");
                    if (backgroundSurface != null)
                    {
                        DrawImageSurface(
                            ctx,
                            backgroundSurface,
                            0,
                            QuestbookGuiLayout.BackgroundOffsetY * fitScale,
                            QuestbookGuiLayout.BackgroundWidth * fitScale,
                            QuestbookGuiLayout.BackgroundHeight * fitScale
                        );
                    }

                    double leftTextX = topMenuPaddingX;
                    DrawTopMenuText(ctx, titleFont, TopMenuTitleText, leftTextX, topTextBaselineY);
                    leftTextX += titleWidth + topMenuTitleGap;
                    DrawTopMenuText(ctx, separatorFont, TopMenuSeparatorText, leftTextX, topTextBaselineY);
                    leftTextX += separatorWidth + topMenuSectionGap;
                    DrawTopMenuText(ctx, sectionFont, GetCategoryHeaderDisplay(selectedCategory), leftTextX, topTextBaselineY);
                    leftTextX += sectionWidth + topMenuProgressGap;
                    DrawTopMenuText(ctx, progressFont, progressText, leftTextX, topTextBaselineY);

                    double closeHotkeyX = topMenuWidth - topMenuRightPadding - closeHotkeyWidth;
                    double closeTextX = closeHotkeyX - topMenuCloseGap - closeWidth;
                    double closeTextCenterY = GetTextVisualCenterY(closeFont, topTextBaselineY);
                    double detachButtonX = closeTextX - topMenuDetachGap - topMenuDetachButtonSize;
                    double detachButtonY = closeTextCenterY - (topMenuDetachButtonSize / 2);
                    double detachIconX = detachButtonX + ((topMenuDetachButtonSize - topMenuDetachIconSize) / 2);
                    double detachIconY = closeTextCenterY - (topMenuDetachIconSize / 2);

                    topMenuDragHitArea = new LayoutRect(0, 0, topMenuWidth, topMenuHeight).Offset(screenX, screenY);
                    detachButtonHitArea = new LayoutRect(
                        detachButtonX,
                        detachButtonY,
                        topMenuDetachButtonSize,
                        topMenuDetachButtonSize).Offset(screenX, screenY);
                    closeButtonHitArea = new LayoutRect(
                        closeTextX,
                        0,
                        (closeHotkeyX + closeHotkeyWidth) - closeTextX,
                        topMenuHeight).Offset(screenX, screenY);

                    DrawTopMenuDetachButton(
                        ctx,
                        detachIconX,
                        detachIconY,
                        topMenuDetachIconSize,
                        isDetachButtonHovered,
                        isDialogMovable);
                    DrawTopMenuText(ctx, closeFont, TopMenuCloseText, closeTextX, topTextBaselineY);
                    DrawTopMenuText(ctx, closeHotkeyFont, TopMenuCloseHotkeyText, closeHotkeyX, topTextBaselineY);

                    LayoutRect sidebarViewportRect = new(
                        sidebarViewportX,
                        sidebarViewportY,
                        sidebarViewportWidth,
                        sidebarVisibleHeight
                    );
                    sidebarViewportHitArea = sidebarViewportRect.Offset(screenX, screenY);

                    double sidebarListOffsetY = 0;
                    if (IsPlayerAdmin() && !adminData.IsAdminPanelOpen)
                    {
                        DrawAdminSidebarEditButton(ctx, fitScale, screenX, screenY);
                        sidebarListOffsetY = GetSidebarAdminButtonsOffset(fitScale);
                    }

                    if (adminData.IsAdminPanelOpen)
                    {
                        DrawAdminPanel(ctx, fitScale);
                    }
                    else
                    {
                        LayoutRect sidebarListViewportRect = new(
                            sidebarViewportRect.X,
                            sidebarViewportRect.Y + sidebarListOffsetY,
                            sidebarViewportRect.Width,
                            System.Math.Max(0, sidebarViewportRect.Height - sidebarListOffsetY)
                        );

                        ctx.Save();
                        ctx.Rectangle(sidebarListViewportRect.X, sidebarListViewportRect.Y, sidebarListViewportRect.Width, sidebarListViewportRect.Height);
                        ctx.Clip();

                        for (int index = 0; index < categories.Length; index++)
                        {
                            SidebarQuestEntry entry = CreateSidebarEntry(categories[index], index == selectedCategoryIndex);
                            LayoutRect localCardRect = CreateSidebarCardLayout(index, fitScale, sidebarScrollOffset);
                            localCardRect = new LayoutRect(
                                localCardRect.X,
                                localCardRect.Y + sidebarListOffsetY,
                                localCardRect.Width,
                                localCardRect.Height
                            );
                            // Hit-test only the visible portion; draw full card so layout
                            // (icon/text) stays correct — Cairo clip + GL scissor handle overflow.
                            LayoutRect clippedCardRect = localCardRect.Intersect(sidebarListViewportRect);
                            sidebarCardHitAreas[index] = clippedCardRect.Offset(screenX, screenY);
                            if (clippedCardRect.IsEmpty)
                            {
                                continue;
                            }

                            DrawSidebarCard(ctx, entry, localCardRect, fitScale, sidebarListViewportRect);
                        }

                        ctx.Restore();
                    }

                    LayoutRect viewportRect = new(
                        QuestbookGuiLayout.GraphViewportX * fitScale,
                        QuestbookGuiLayout.GraphViewportY * fitScale,
                        QuestbookGuiLayout.GraphViewportWidth * fitScale,
                        QuestbookGuiLayout.GraphViewportHeight * fitScale
                    );
                    rightPanelViewportLocal = viewportRect;
                    rightPanelViewportHitArea = viewportRect.Offset(screenX, screenY);
                    rightPanelGraphBaseX = rightPanelViewportHitArea.X;
                    rightPanelGraphBaseY = rightPanelViewportHitArea.Y;
                    viewportRectWidth = viewportRect.Width;
                    viewportRectHeight = viewportRect.Height;

                    if (shouldCenterOnStartNode)
                    {
                        shouldCenterOnStartNode = false;
                        double centerGraphScale = fitScale * graphZoom;

                        QuestbookQuestNodeDefinition? startNode = null;
                        foreach (var node in selectedCategory.Nodes)
                        {
                            if (node.NodeType == QuestbookQuestNodeType.Start)
                            {
                                startNode = node;
                                break;
                            }
                        }

                        double startNodeRenderSize = QuestbookGuiLayout.GraphStartNodeSize * centerGraphScale;
                        double startNodeOffset = startNode != null ? GetNodeCenterOffset(startNode) * centerGraphScale : 0;

                        // Смещение на 36px влево для лучшей центровки
                        double horizontalOffset = 36 * fitScale;

                        if (startNode != null)
                        {
                            graphPanX = (viewportRect.Width / 2) - ((startNode.X + startNodeOffset) * centerGraphScale) - (startNodeRenderSize / 2) - horizontalOffset;
                            graphPanY = (viewportRect.Height / 2) - ((startNode.Y + startNodeOffset) * centerGraphScale) - (startNodeRenderSize / 2);
                        }
                        else
                        {
                            graphPanX = (viewportRect.Width / 2) - (startNodeRenderSize / 2) - horizontalOffset;
                            graphPanY = (viewportRect.Height / 2) - (startNodeRenderSize / 2);
                        }
                    }

                    ctx.Save();
                    ctx.Rectangle(viewportRect.X, viewportRect.Y, viewportRect.Width, viewportRect.Height);
                    ctx.Clip();

                    double graphScale = fitScale * graphZoom;

                    if (adminData.IsAdminPanelOpen && adminData.EditorSection == AdminEditorSection.Quests && adminData.ShowGrid)
                    {
                        DrawAdminGraphGrid(ctx, viewportRect, graphScale);
                    }

                    if (selectedCategory.Nodes.Length == 0)
                    {
                        CairoFont emptyFont = CreateMontserratFont(24 * fitScale, QuestbookGuiLayout.ModalBodyTextColor);
                        DrawCenteredText(ctx, emptyFont, EmptyCategoryText, viewportRect);
                    }
                    else
                    {
                        EnsureGraphCache(selectedCategory);

                        foreach (QuestbookQuestConnectionDefinition connection in selectedCategory.Connections)
                        {
                            QuestbookQuestNodeDefinition? startNode = GetCachedNodeById(connection.StartNodeId)
                                ?? GetNodeById(selectedCategory, connection.StartNodeId);
                            QuestbookQuestNodeDefinition? endNode = GetCachedNodeById(connection.EndNodeId)
                                ?? GetNodeById(selectedCategory, connection.EndNodeId);
                            if (startNode == null || endNode == null)
                            {
                                continue;
                            }

                            QuestNodeVisualState startNodeState = GetNodeVisualStateCached(startNode.Id);
                            QuestNodeVisualState endNodeState = GetNodeVisualStateCached(endNode.Id);
                            DrawQuestConnection(
                                ctx, startNode, endNode, startNodeState, endNodeState,
                                viewportRect.X, viewportRect.Y, graphScale, viewportRect);
                        }

                        questCardHitAreas = new LayoutRect[selectedCategory.Nodes.Length];
                        for (int index = 0; index < selectedCategory.Nodes.Length; index++)
                        {
                            QuestbookQuestNodeDefinition node = selectedCategory.Nodes[index];
                            LayoutRect localNodeRect = GetNodeScreenRect(node, viewportRect.X, viewportRect.Y, graphPanX, graphPanY, graphScale);
                            questCardHitAreas[index] = localNodeRect.Offset(screenX, screenY);

                            // Always keep hit areas, but skip expensive drawing for off-screen nodes.
                            if (!IntersectsViewport(localNodeRect, viewportRect, GraphCullMargin))
                            {
                                continue;
                            }

                            bool isHovered = hoveredQuestNodeId == node.Id;
                            bool isSelected = selectedQuestNodeId == node.Id
                                || adminData.SelectedNodeId == node.Id
                                || adminData.LinkSourceNodeId == node.Id;
                            bool isReadyToSubmit = IsNodeReadyCached(node.Id);
                            QuestNodeVisualState nodeVisualState = GetNodeVisualStateCached(node.Id);

                            LayoutRect iconRect = DrawQuestNode(
                                ctx,
                                nodeVisualState,
                                isReadyToSubmit,
                                isHovered,
                                isSelected,
                                localNodeRect.X,
                                localNodeRect.Y,
                                localNodeRect.Width,
                                graphScale,
                                node
                            );
                            string graphIconCode = node.RequiredItems.Length > 0
                                ? node.RequiredItems[0].CollectibleCode
                                : "";
                            int graphIconCount = node.RequiredItems.Length > 0
                                ? node.RequiredItems[0].Count
                                : 0;
                            if (node.SupportsItemIcon && !string.IsNullOrWhiteSpace(graphIconCode))
                            {
                                DummySlot? graphIconSlot = GetQuestItemIconSlot(graphIconCode);
                                if (graphIconSlot?.Itemstack != null)
                                {
                                    questItemIconRenderRequests.Add(
                                        new QuestItemIconRenderRequest(
                                            graphIconCode,
                                            iconRect,
                                            true,
                                            graphIconCount,
                                            QuestbookItemIconContext.QuestNode
                                        )
                                    );
                                }
                                else
                                {
                                    DrawMissingIcon(ctx, iconRect.X, iconRect.Y, iconRect.Width);
                                }
                            }
                        }
                    }

                    ctx.Restore();

                    if (isQuestModalOpen)
                    {
                        QuestbookQuestNodeDefinition? modalNode = GetSelectedQuestNode();
                        if (modalNode != null)
                        {
                            int modalCurrentCount = GetNodeCurrentCount(modalNode);
                            bool modalCompleted = modalNode.State == QuestbookQuestNodeState.Completed;
                            bool modalCanSubmit = CanSubmitNode(modalNode);
                            DrawQuestModal(
                                ctx,
                                modalNode,
                                modalCurrentCount,
                                modalCanSubmit,
                                isQuestSubmitPending && pendingQuestNodeId == modalNode.Id,
                                modalCompleted,
                                screenX,
                                screenY,
                                fitScale);
                        }
                    }

                    if (isQuestEditModalOpen)
                        DrawQuestEditModal(ctx, fitScale, screenX, screenY);

                    if (isBranchModalOpen)
                        DrawBranchModal(ctx, fitScale, screenX, screenY);
                }, "swixyquestbookContent");

            if (!adminData.IsAdminPanelOpen && categories.Length > 0)
            {
                composer.AddCompactVerticalScrollbar(OnSidebarScrollbarValue, sidebarScrollbarBounds, SidebarScrollbarKey);
            }

            SingleComposer = composer.EndChildElements().Compose();

            adminPanelComposer = null;

            GuiElementCompactScrollbar? sidebarScrollbar = SingleComposer.GetCompactScrollbar(SidebarScrollbarKey);
            if (sidebarScrollbar != null)
            {
                float scrollVisibleHeight = (float)sidebarListVisibleHeight;
                float scrollContentHeight = (float)System.Math.Max(sidebarListVisibleHeight, sidebarContentHeight);

                isSyncingSidebarScrollbar = true;
                sidebarScrollbar.SetHeights(scrollVisibleHeight, scrollContentHeight);
                sidebarScrollbar.CurrentYPosition = (float)sidebarScrollOffset;
                sidebarScrollbar.TriggerChanged();
                isSyncingSidebarScrollbar = false;

                sidebarScrollbar.Bounds.CalcWorldBounds();
                sidebarScrollbarVisualHitArea = new LayoutRect(
                    sidebarScrollbar.Bounds.absX,
                    sidebarScrollbar.Bounds.absY,
                    sidebarScrollbar.Bounds.OuterWidth,
                    sidebarScrollbar.Bounds.OuterHeight
                );

                double visualTrackHeight = sidebarScrollbarVisualHitArea.Height;
                double scrollableRange = System.Math.Max(0, scrollContentHeight - scrollVisibleHeight);
                if (scrollableRange <= 0.001 || visualTrackHeight <= 0.001)
                {
                    sidebarScrollbarHandleHeight = visualTrackHeight;
                    sidebarScrollbarHandlePosition = 0;
                }
                else
                {
                    double handleRatio = scrollVisibleHeight / scrollContentHeight;
                    sidebarScrollbarHandleHeight = System.Math.Max(10, handleRatio * visualTrackHeight);
                    double movableTrack = System.Math.Max(0, visualTrackHeight - sidebarScrollbarHandleHeight);
                    double conversion = sidebarScrollbar.ScrollConversionFactor;
                    if (conversion <= 0.0001)
                    {
                        conversion = sidebarScrollConversionFactor;
                    }

                    sidebarScrollbarHandlePosition = System.Math.Clamp(
                        sidebarScrollOffset / conversion,
                        0,
                        movableTrack);
                }

                sidebarScrollConversionFactor = sidebarScrollbar.ScrollConversionFactor > 0.0001
                    ? sidebarScrollbar.ScrollConversionFactor
                    : sidebarScrollConversionFactor;

                sidebarScrollbarVisualHandleHitArea = new LayoutRect(
                    sidebarScrollbarVisualHitArea.X,
                    sidebarScrollbarVisualHitArea.Y + sidebarScrollbarHandlePosition,
                    sidebarScrollbarVisualHitArea.Width,
                    sidebarScrollbarHandleHeight
                );
            }
        }
    }
}
