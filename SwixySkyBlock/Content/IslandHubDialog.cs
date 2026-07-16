// =============================================================================
// IslandHubDialog.cs
// -----------------------------------------------------------------------------
// SkyBlock island hub GUI: island management tab and claim access tab.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

using Cairo;
using SwixySkyBlock.Core;
using SwixySkyBlock.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixySkyBlock.Content;

/// <summary>Island hub dialog with island actions and claim access management.</summary>
public sealed class IslandHubDialog : GuiDialog
{
    private const int PageIsland = 0;
    private const int PageAccess = 1;
    private const int PageGenerator = 2;
    private const int PageStory = 3;
    private const int PageTop = 4;

    private readonly ICoreClientAPI clientApi;
    private readonly IClientNetworkChannel channel;

    private IslandHubStatePacket? hubState;
    private IslandClaimListStatePacket? claimListState;
    private IslandGeneratorStatePacket? generatorState;
    private IslandTopStatePacket? topState;
    private StoryDungeonStatePacket? storyState;

    /// <summary>Хранит последнюю полную версию списка территорий (для сравнения с дельта).</summary>
    private List<IslandClaimInfoPacket>? lastFullClaimList = [];

    private int activePage = PageIsland;
    private int selectedClaimId;
    private int highlightedClaimId;
    private int pendingHighlightClaimId = -1;

    private string selectedMemberUid = "";
    private string memberNameInput = "";
    private string claimNameInput = "";

    private bool storyStatePollScheduled;

    private float memberListScrollValue;
    private float templateListScrollValue;
    private float generatorListScrollValue;
    private float topListScrollValue;
    private float storyListScrollValue;
    private ElementBounds? memberListTableBounds;
    private ElementBounds? memberListClipBounds;
    private ElementBounds? templateListTableBounds;
    private ElementBounds? templateListClipBounds;
    private ElementBounds? generatorListClipBounds;
    private ElementBounds? generatorListTableBounds;
    private ElementBounds? topListClipBounds;
    private ElementBounds? topListTableBounds;
    private ElementBounds? storyListClipBounds;
    private ElementBounds? storyListTableBounds;

    private bool claimsUiDeferScheduled;
    private Action? deferredClaimsUiAction;
    private bool generatorUiDeferScheduled;
    private Action? deferredGeneratorUiAction;
    private readonly TextDrawUtil templateHeaderTextUtil = new();

    public override string ToggleKeyCombinationCode => SkyBlockConstants.OpenIslandHubHotkeyCode;

    public override bool PrefersUngrabbedMouse => true;

    public IslandHubDialog(ICoreClientAPI api, IClientNetworkChannel channel)
        : base(api)
    {
        clientApi = api;
        this.channel = channel;
        ComposeDialog();
    }

    public void RequestRefresh()
    {
        channel.SendPacket(new IslandHubRequestPacket());
    }

    public void RequestClaimList()
    {
        channel.SendPacket(new IslandClaimListRequestPacket());
    }

    public void RequestGeneratorState()
    {
        EnsureLocalGeneratorState();
        channel.SendPacket(new IslandGeneratorStateRequestPacket());
    }

    public void RequestTopState()
    {
        channel.SendPacket(new IslandTopRequestPacket());
    }

    public void RequestStoryState()
    {
        channel.SendPacket(new StoryDungeonStateRequestPacket());
    }

    private void EnsureLocalGeneratorState()
    {
        generatorState ??= IslandGeneratorStateBuilder.Build(
            SkyBlockRuntime.GeneratorConfig,
            hubState?.HasIsland == true,
            generatorState?.CurrentLevel ?? 1);
    }

    public void ApplyHubState(IslandHubStatePacket packet)
    {
        var previousTemplateCount = hubState?.AvailableTemplates?.Count ?? -1;
        var previousHasIsland = hubState?.HasIsland;
        var previousIsResident = hubState?.IsIslandResident;

        hubState = packet;

        if (activePage == PageIsland)
        {
            if (previousTemplateCount != (packet.AvailableTemplates?.Count ?? 0)
                || previousHasIsland != packet.HasIsland
                || previousIsResident != packet.IsIslandResident)
            {
                ComposeDialog();
                return;
            }

            SingleComposer?.GetDynamicText("hubStatusText")?.SetNewText(packet.Message ?? "");
        }
    }

    public void ApplyClaimList(IslandClaimListStatePacket packet)
    {
        var savedClaimScroll = GetClaimListScrollOffset();
        var savedMemberScroll = GetMemberListScrollOffset();

        claimListState = packet;

        if (packet.Claims.Count == 0)
        {
            selectedClaimId = 0;
            selectedMemberUid = "";
        }
        else if (selectedClaimId == 0 || packet.Claims.All(claim => claim.ClaimId != selectedClaimId))
        {
            SelectClaim(packet.Claims[0]);
        }

        var selectedClaim = GetSelectedClaim();
        if (selectedClaim == null || selectedClaim.Members.All(member => member.PlayerUid != selectedMemberUid))
        {
            selectedMemberUid = "";
        }

        if (highlightedClaimId > 0 && packet.Claims.All(claim => claim.ClaimId != highlightedClaimId))
        {
            highlightedClaimId = 0;
        }

        if (activePage == PageAccess)
        {
            if (packet.Claims.Count == 0 || !TryRefreshClaimsPageInPlace(savedClaimScroll, savedMemberScroll))
            {
                ComposeDialog();
                RestoreMemberListScroll(savedMemberScroll);
                ApplyClaimsPageInputState();
            }

            SingleComposer?.GetDynamicText("claimsMessage")?.SetNewText(packet.Message ?? "");
        }

        // Сохраняем полный список для дельта-обновлений
        lastFullClaimList = packet.Claims;
    }

    public void ApplyGeneratorState(IslandGeneratorStatePacket packet)
    {
        generatorState = packet;
        if (!IsOpened() || activePage != PageGenerator)
        {
            return;
        }

        RunGeneratorUiDeferred(RefreshGeneratorPageUi);
    }

    public void ApplyTopState(IslandTopStatePacket packet)
    {
        var savedScroll = GetTopListScrollOffset();
        topState = packet;

        if (!IsOpened() || activePage != PageTop)
        {
            return;
        }

        if (!TryRefreshTopPageInPlace(savedScroll))
        {
            ComposeDialog();
            RestoreTopListScroll(savedScroll);
        }
    }

    public void ApplyStoryState(StoryDungeonStatePacket packet)
    {
        var savedScroll = GetStoryListScrollOffset();
        storyState = packet;

        if (!IsOpened() || activePage != PageStory)
        {
            ScheduleStoryStatePoll();
            return;
        }

        if (!TryRefreshStoryPageInPlace(savedScroll))
        {
            ComposeDialog();
            RestoreStoryListScroll(savedScroll);
        }

        ScheduleStoryStatePoll();
    }

    private void ScheduleStoryStatePoll()
    {
        if (storyState?.Sites.Any(static site => !site.Ready) != true)
        {
            return;
        }

        if (storyStatePollScheduled)
        {
            return;
        }

        storyStatePollScheduled = true;
        clientApi.Event.RegisterCallback(_ =>
        {
            storyStatePollScheduled = false;
            if (!IsOpened())
            {
                return;
            }

            if (storyState?.Sites.Any(static site => !site.Ready) == true)
            {
                RequestStoryState();
            }
        }, 3000);
    }

    /// <summary>Обработка дельта-пакета для оптимизированной передачи списка территорий.</summary>
    public void ApplyClaimListDelta(IslandClaimListDeltaPacket delta)
    {
        // Обновляем последнее состояние перед внесением изменений
        if (lastFullClaimList == null || lastFullClaimList.Count == 0)
        {
            // Если нет полной версии, обновляем её из текущего состояния
            var claims = claimListState?.Claims ?? [];
            lastFullClaimList = new List<IslandClaimInfoPacket>(claims);
        }

        switch (delta.MessageType.ToLowerInvariant())
        {
            case "add":
                AddClaimToList(lastFullClaimList, delta);
                break;
            case "remove":
                RemoveClaimFromList(lastFullClaimList, delta.ClaimKey);
                break;
            case "update":
                UpdateClaimInList(lastFullClaimList, delta.ClaimKey, delta);
                break;
        }

        claimListState = new IslandClaimListStatePacket
        {
            Claims = lastFullClaimList ?? [],
            Message = delta.MessageType switch
            {
                "add" => "territory-added",
                "remove" => "territory-removed",
                "update" => "territory-updated",
                _ => ""
            },
            MessageType = 0 // Используем новый тип пакета вместо этого
        };

        ApplyClaimList(claimListState!);
    }

    private void AddClaimToList(List<IslandClaimInfoPacket> list, IslandClaimListDeltaPacket delta)
    {
        var newClaim = new IslandClaimInfoPacket
        {
            ClaimId = 0, // Будет назначен сервером
            Name = delta.OwnerName ?? "",
            OwnerName = delta.OwnerName ?? "",
            AreaCount = 0,
            Volume = 0,
            Members = [],
            ChunkCount = 0,
            ViewerIsCoOwner = false,
            IsIslandClaim = true,
            ViewerCanLeave = false // По умолчанию нельзя покинуть (хозяин)
        };

        list.Insert(0, newClaim); // Добавляем в начало списка
    }

    private void RemoveClaimFromList(List<IslandClaimInfoPacket> list, string claimKey)
    {
        var originalIndex = lastFullClaimList?.FindIndex(c => c.OwnerName == claimKey) ?? -1;
        
        if (originalIndex >= 0 && originalIndex < list.Count)
        {
            var removedClaim = list[originalIndex];
            
            // Если удаляем текущий выбор, переносим его в конец списка
            if (selectedClaimId == removedClaim.ClaimId)
            {
                selectedClaimId = 0;
            }
            
            list.RemoveAt(originalIndex);
        }
    }

    private void UpdateClaimInList(List<IslandClaimInfoPacket> list, string claimKey, IslandClaimListDeltaPacket delta)
    {
        var index = list.FindIndex(c => c.OwnerName == claimKey);
        
        if (index >= 0 && index < list.Count)
        {
            var claim = list[index];
            
            // Обновляем имя владельца
            claim.OwnerName = delta.OwnerName ?? claim.OwnerName;
            
            // Если изменился шаблон, обновляем IsIslandClaim
            if (!string.IsNullOrEmpty(delta.TemplateName))
            {
                claim.IsIslandClaim = true;
            }
            
            // Обновляем информацию о доступе
            claim.ViewerCanLeave = delta.AccessGranted;
        }
    }

    public void ApplyClaimShow(IslandClaimShowStatePacket packet)
    {
        ApplyHighlightStateFromServer(packet.Active, packet.ClaimId);

        if (activePage == PageAccess)
        {
            RunClaimsUiDeferred(() => RefreshClaimHighlightIcons(GetClaimListScrollOffset()));
        }
    }

    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        RequestRefresh();
        RequestClaimList();
        RequestGeneratorState();

        if (highlightedClaimId > 0 && activePage == PageAccess)
        {
            RunClaimsUiDeferred(() => RefreshClaimHighlightIcons(GetClaimListScrollOffset()));
        }
    }

    public override bool OnEscapePressed()
    {
        TryClose();
        return true;
    }

    private void ComposeDialog()
    {
        var framePad = IslandHubTheme.DialogFramePadding;
        var contentW = IslandHubTheme.DialogContentWidth;
        var contentH = GetDialogHeight();
        var mainBounds = ElementBounds.Fixed(0, 0, contentW, contentH);
        var bgBounds = ElementBounds
            .Fixed(0, 0, contentW + framePad * 2, contentH + framePad * 2)
            .WithFixedPadding(framePad);
        bgBounds.BothSizing = ElementSizing.Fixed;
        bgBounds.WithChildren(mainBounds);

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);

        ClearComposers();
        var composer = clientApi.Gui
            .CreateCompo("islandhub", dialogBounds)
            .AddDynamicCustomDraw(bgBounds, DrawHubDialogBackground, "hubDialogBg")
            .AddDialogTitleBar(Lang.Get("swixyskyblock:island-hub-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds)
            .AddDynamicCustomDraw(IslandHubTheme.TabBarBounds, DrawTabBarBackground, "tabBarBg");

        AddHubButton(
            composer,
            Lang.Get("swixyskyblock:island-tab-island"),
            SwitchToIslandPage,
            IslandHubTheme.TabButtonBounds(0),
            activePage == PageIsland,
            IslandHubButtonKind.Tab,
            "islandTab");
        AddHubButton(
            composer,
            Lang.Get("swixyskyblock:island-tab-access"),
            SwitchToAccessPage,
            IslandHubTheme.TabButtonBounds(1),
            activePage == PageAccess,
            IslandHubButtonKind.Tab,
            "accessTab");
        AddHubButton(
            composer,
            Lang.Get("swixyskyblock:island-generator-tab"),
            SwitchToGeneratorPage,
            IslandHubTheme.TabButtonBounds(2),
            activePage == PageGenerator,
            IslandHubButtonKind.Tab,
            "generatorTab");
        AddHubButton(
            composer,
            Lang.Get("swixyskyblock:island-tab-story"),
            SwitchToStoryPage,
            IslandHubTheme.TabButtonBounds(3),
            activePage == PageStory,
            IslandHubButtonKind.Tab,
            "storyTab");
        AddHubButton(
            composer,
            Lang.Get("swixyskyblock:island-tab-top"),
            SwitchToTopPage,
            IslandHubTheme.TabButtonBounds(4),
            activePage == PageTop,
            IslandHubButtonKind.Tab,
            "topTab");

        if (activePage == PageIsland)
        {
            ComposeIslandPage(composer);
        }
        else if (activePage == PageAccess)
        {
            ComposeAccessPage(composer);
        }
        else if (activePage == PageGenerator)
        {
            ComposeGeneratorPage(composer);
        }
        else if (activePage == PageStory)
        {
            ComposeStoryPage(composer);
        }
        else
        {
            ComposeTopPage(composer);
        }

        SingleComposer = composer.EndChildElements().Compose();
        ApplyIslandPageScrollState();
        ApplyClaimsPageScrollState();
        ApplyGeneratorPageScrollState();
        ApplyStoryPageScrollState();
        ApplyTopPageScrollState();
        ApplyClaimsPageInputState();
        UpdateHubStatusText();
    }

    private int GetDialogHeight() => IslandHubTheme.DialogContentHeight;

    private void ComposeIslandPage(GuiComposer composer)
    {
        const int areaY = IslandHubTheme.ContentAreaY;
        const int leftX = IslandHubTheme.ContentAreaX;
        const int leftW = IslandHubTheme.IslandLeftPanelWidth;
        const int rightX = leftX + leftW + IslandHubTheme.IslandPanelGap;
        const int rightW = IslandHubTheme.IslandRightPanelWidth;
        const int actionPanelH = IslandHubTheme.IslandActionPanelHeight;
        const int templatePanelH = IslandHubTheme.ContentPanelHeight;
        const int spawnPanelY = areaY;
        const int homePanelY = areaY + actionPanelH + IslandHubTheme.IslandActionPanelGap;
        const int templateScrollH = IslandHubTheme.IslandTemplateListHeight;
        var hasIsland = hubState?.HasIsland == true;
        var hasHome = hasIsland || hubState?.IsIslandResident == true;

        composer
            .AddDynamicCustomDraw(ElementBounds.Fixed(rightX, areaY, rightW, templatePanelH), DrawSidePanelBackground, "templatePickerBg");

        var spawnActionBounds = ElementBounds.Fixed(leftX, spawnPanelY, leftW, actionPanelH);
        var homeActionBounds = ElementBounds.Fixed(leftX, homePanelY, leftW, actionPanelH);

        composer
            .AddCellList(spawnActionBounds, CreateActionCell, BuildActionCell("spawn", true), "spawnActionList")
            .AddCellList(homeActionBounds, CreateActionCell, BuildActionCell("home", hasHome), "homeActionList");

        composer.AddDynamicCustomDraw(
            ElementBounds.Fixed(
                rightX + 12,
                areaY + IslandHubTheme.IslandTemplateHeaderOffset,
                rightW - 24,
                IslandHubTheme.IslandTemplateHeaderHeight),
            DrawTemplateHeader,
            "templateHeaderStrip");

        var templateListBounds = ElementBounds.Fixed(
            rightX + IslandHubTheme.IslandTemplateListInsetX,
            areaY + IslandHubTheme.IslandTemplateListOffset,
            IslandHubTheme.IslandTemplateListTrackWidth,
            templateScrollH);
        templateListClipBounds = templateListBounds.ForkContainingChild(3, 3, 3, 3);
        templateListTableBounds = templateListClipBounds.ForkContainingChild(0, 0, 0, -3).WithFixedPadding(3);

        composer
            .AddDynamicCustomDraw(templateListBounds, DrawScrollAreaBackground, "templateListScrollBg")
            .AddInset(templateListBounds, 3, 0.85f)
            .AddVerticalScrollbar(OnTemplateListScroll, ElementStdBounds.VerticalScrollbar(templateListBounds), "templateListScroll")
            .BeginClip(templateListClipBounds)
            .AddCellList(templateListTableBounds, CreateTemplateCell, BuildTemplateCells(), "templateList")
            .EndClip();
    }

    private void ComposeGeneratorPage(GuiComposer composer)
    {
        const int areaY = IslandHubTheme.ContentAreaY;
        const int panelX = IslandHubTheme.ContentAreaX;
        const int panelW = IslandHubTheme.ContentPanelWidth;
        const int panelH = IslandHubTheme.ContentPanelHeight;
        const int headerH = 46;
        const int footerH = 42;
        const int sectionGap = 6;
        const int headerY = areaY;
        const int footerY = areaY + panelH - footerH;
        const int listY = headerY + headerH + sectionGap;
        const int listH = footerY - sectionGap - listY;
        const int innerPad = 18;
        const int statusH = 18;
        const int statusY = headerY + (headerH - statusH) / 2;
        const int messageY = footerY + 8;
        const int upgradeButtonH = 26;
        const int upgradeButtonY = headerY + (headerH - upgradeButtonH) / 2;

        var listTrackBounds = ElementBounds.Fixed(
            IslandHubTheme.PanelFullListTrackX,
            listY,
            IslandHubTheme.PanelFullListTrackWidth,
            listH);
        generatorListClipBounds = listTrackBounds.ForkContainingChild(3, 3, 3, 3);
        generatorListTableBounds = generatorListClipBounds.ForkContainingChild(0, 0, 0, -3).WithFixedPadding(0);

        var state = generatorState;
        var status = state == null
            ? Lang.Get("swixyskyblock:island-generator-loading")
            : state.HasIsland
            ? Lang.Get(
                "swixyskyblock:island-generator-status",
                state.CurrentLevel,
                state.MaxLevel,
                state.CostQuantity,
                FormatGeneratorCostItem(state.CostItemCode),
                state.PlayerCostItemCount)
            : Lang.Get("swixyskyblock:island-generator-no-island");

        var upgradeLabel = state is { CurrentLevel: var level, MaxLevel: var maxLevel }
            ? level >= maxLevel
                ? Lang.Get("swixyskyblock:island-generator-max-level")
                : Lang.Get("swixyskyblock:island-generator-upgrade")
            : Lang.Get("swixyskyblock:island-generator-upgrade");

        composer
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(panelX, headerY, panelW, headerH),
                DrawSidePanelBackground,
                "generatorHeaderBg")
            .AddDynamicCustomDraw(listTrackBounds, DrawScrollAreaBackground, "generatorListScrollBg")
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(panelX, footerY, panelW, footerH),
                DrawSidePanelBackground,
                "generatorFooterBg")
            .AddInset(listTrackBounds, 3, 0.85f)
            .AddVerticalScrollbar(OnGeneratorListScroll, ElementStdBounds.VerticalScrollbar(listTrackBounds), "generatorListScroll")
            .BeginClip(generatorListClipBounds)
            .AddCellList(generatorListTableBounds, CreateGeneratorLevelCell, BuildGeneratorLevelCells(), "generatorLevels")
            .EndClip()
            .AddDynamicText(
                status,
                CairoFont.WhiteDetailText(),
                ElementBounds.Fixed(panelX + innerPad, statusY, panelW - 220, statusH),
                "generatorStatus")

            .AddDynamicText(
                state?.Message ?? "",
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(panelX + innerPad, messageY, panelW - innerPad * 2, 32),
                "generatorMessage");

        AddHubButton(
            composer,
            upgradeLabel,
            OnUpgradeGeneratorButton,
            ElementBounds.Fixed(panelX + panelW - 164, upgradeButtonY, 130, upgradeButtonH),
            active: false,
            IslandHubButtonKind.Action,
            "generatorUpgrade",
            state?.CanUpgrade == true);
    }

    private void ComposeStoryPage(GuiComposer composer)
    {
        const int areaY = IslandHubTheme.ContentAreaY;
        const int panelX = IslandHubTheme.ContentAreaX;
        const int panelW = IslandHubTheme.ContentPanelWidth;
        const int panelH = IslandHubTheme.ContentPanelHeight;
        const int headerH = 72;
        const int sectionGap = 6;
        const int innerPad = 18;
        const int headerY = areaY;
        const int listY = headerY + headerH + sectionGap;
        const int listH = areaY + panelH - listY;
        const int columnsY = headerY + 48;

        var listTrackBounds = ElementBounds.Fixed(
            IslandHubTheme.PanelFullListTrackX,
            listY,
            IslandHubTheme.PanelFullListTrackWidth,
            listH);
        storyListClipBounds = listTrackBounds.ForkContainingChild(3, 3, 3, 3);
        storyListTableBounds = storyListClipBounds.ForkContainingChild(0, 0, 0, -3).WithFixedPadding(0);

        var sites = storyState?.Sites ?? [];
        var subtitle = storyState == null
            ? Lang.Get("swixyskyblock:story-loading")
            : Lang.Get("swixyskyblock:story-subtitle");

        composer
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(panelX, headerY, panelW, headerH),
                DrawSidePanelBackground,
                "storyHeaderBg")
            .AddDynamicCustomDraw(listTrackBounds, DrawScrollAreaBackground, "storyListScrollBg")
            .AddDynamicText(
                Lang.Get("swixyskyblock:story-title"),
                IslandHubTheme.CreateSectionTitleFont(),
                ElementBounds.Fixed(panelX + innerPad, headerY + 4, 260, 18),
                "storyTitle")
            .AddDynamicText(
                subtitle,
                CairoFont.WhiteDetailText().WithFontSize(11),
                ElementBounds.Fixed(panelX + innerPad, headerY + 22, panelW - innerPad * 2, 16),
                "storySubtitle")
            .AddDynamicText(
                Lang.Get("swixyskyblock:story-hint"),
                CairoFont.WhiteDetailText().WithFontSize(10),
                ElementBounds.Fixed(panelX + innerPad, headerY + 38, panelW - innerPad * 2, 28),
                "storyHint")
            .AddStaticText(
                Lang.Get("swixyskyblock:story-col-order"),
                CairoFont.WhiteDetailText().WithFontSize(11),
                ElementBounds.Fixed(panelX + innerPad, columnsY, 40, 16),
                "storyColOrder")
            .AddStaticText(
                Lang.Get("swixyskyblock:story-col-name"),
                CairoFont.WhiteDetailText().WithFontSize(11),
                ElementBounds.Fixed(panelX + innerPad + 40, columnsY, 220, 16),
                "storyColName")
            .AddStaticText(
                Lang.Get("swixyskyblock:story-col-status"),
                CairoFont.WhiteDetailText().WithFontSize(11),
                ElementBounds.Fixed(panelX + panelW - innerPad - 108, columnsY, 108, 16),
                "storyColStatus")
            .AddVerticalScrollbar(OnStoryListScroll, ElementStdBounds.VerticalScrollbar(listTrackBounds), "storyListScroll")
            .BeginClip(storyListClipBounds)
            .AddCellList(storyListTableBounds, CreateStoryCell, BuildStoryCells(), "storyList")
            .EndClip();
    }

    private void ComposeTopPage(GuiComposer composer)
    {
        const int areaY = IslandHubTheme.ContentAreaY;
        const int panelX = IslandHubTheme.ContentAreaX;
        const int panelW = IslandHubTheme.ContentPanelWidth;
        const int panelH = IslandHubTheme.ContentPanelHeight;
        const int headerH = 56;
        const int sectionGap = 6;
        const int innerPad = 18;
        const int headerY = areaY;
        const int listY = headerY + headerH + sectionGap;
        const int listH = areaY + panelH - listY;
        const int columnsY = headerY + 36;

        var listTrackBounds = ElementBounds.Fixed(
            IslandHubTheme.PanelFullListTrackX,
            listY,
            IslandHubTheme.PanelFullListTrackWidth,
            listH);
        topListClipBounds = listTrackBounds.ForkContainingChild(3, 3, 3, 3);
        topListTableBounds = topListClipBounds.ForkContainingChild(0, 0, 0, -3).WithFixedPadding(0);

        var entries = topState?.Entries ?? [];
        var subtitle = topState == null
            ? Lang.Get("swixyskyblock:island-top-loading")
            : Lang.Get("swixyskyblock:island-top-subtitle", entries.Count);

        composer
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(panelX, headerY, panelW, headerH),
                DrawSidePanelBackground,
                "topHeaderBg")
            .AddDynamicCustomDraw(listTrackBounds, DrawScrollAreaBackground, "topListScrollBg")
            .AddDynamicText(
                Lang.Get("swixyskyblock:island-top-title"),
                IslandHubTheme.CreateSectionTitleFont(),
                ElementBounds.Fixed(panelX + innerPad, headerY + 4, 220, 18),
                "topTitle")
            .AddDynamicText(
                subtitle,
                CairoFont.WhiteDetailText().WithFontSize(11),
                ElementBounds.Fixed(panelX + innerPad, headerY + 22, panelW - innerPad * 2, 16),
                "topSubtitle")
            .AddStaticText(
                Lang.Get("swixyskyblock:island-top-col-rank"),
                CairoFont.WhiteDetailText().WithFontSize(11),
                ElementBounds.Fixed(panelX + innerPad, columnsY, 44, 16),
                "topColRank")
            .AddStaticText(
                Lang.Get("swixyskyblock:island-top-col-player"),
                CairoFont.WhiteDetailText().WithFontSize(11),
                ElementBounds.Fixed(panelX + innerPad + 44, columnsY, 200, 16),
                "topColPlayer")
            .AddStaticText(
                Lang.Get("swixyskyblock:island-top-col-level"),
                CairoFont.WhiteDetailText().WithFontSize(11),
                ElementBounds.Fixed(panelX + panelW - innerPad - 192, columnsY, 72, 16),
                "topColLevel")
            .AddStaticText(
                Lang.Get("swixyskyblock:island-top-col-template"),
                CairoFont.WhiteDetailText().WithFontSize(11),
                ElementBounds.Fixed(panelX + panelW - innerPad - 120, columnsY, 120, 16),
                "topColTemplate")
            .AddInset(listTrackBounds, 3, 0.85f)
            .AddVerticalScrollbar(OnTopListScroll, ElementStdBounds.VerticalScrollbar(listTrackBounds), "topListScroll")
            .BeginClip(topListClipBounds)
            .AddCellList(topListTableBounds, CreateTopCell, BuildTopCells(), "topList")
            .EndClip();

        if (entries.Count == 0 && topState != null)
        {
            composer.AddStaticText(
                Lang.Get("swixyskyblock:island-top-empty"),
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(panelX + innerPad, listY + 12, panelW - innerPad * 2, 40),
                "topEmpty");
        }
    }

    private static string ResolveTemplateLabel(string templateName)
    {
        var key = $"swixyskyblock:island-template-{templateName}";
        var translated = Lang.Get(key);
        return translated == key ? templateName : translated;
    }

    private void ComposeAccessPage(GuiComposer composer)
    {
        const int areaX = IslandHubTheme.ContentAreaX;
        const int areaY = IslandHubTheme.ContentAreaY;
        const int panelW = IslandHubTheme.ContentPanelWidth;
        const int detailsY = IslandHubTheme.AccessDetailsPanelY;
        const int detailsPanelH = IslandHubTheme.AccessDetailsPanelHeight;
        const int detailsX = IslandHubTheme.AccessDetailsX;
        const int detailsW = IslandHubTheme.AccessDetailsWidth;
        const int headerSideInset = IslandHubTheme.AccessHeaderSideInset;

        composer.AddDynamicCustomDraw(
            ElementBounds.Fixed(areaX, areaY, panelW, IslandHubTheme.AccessHeaderHeight),
            DrawAccessHeaderBackground,
            "claimHeaderBg");

        var claims = claimListState?.Claims ?? [];
        if (claims.Count == 0)
        {
            composer.AddStaticText(
                Lang.Get("swixyskyblock:island-claims-empty"),
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(
                    areaX + headerSideInset,
                    areaY + IslandHubTheme.AccessHeaderTopInset + 16,
                    panelW - headerSideInset * 2,
                    28),
                "claimsEmpty");
        }
        else
        {
            var claimHeaderBounds = ElementBounds.Fixed(
                areaX + headerSideInset,
                areaY + IslandHubTheme.AccessHeaderTopInset,
                panelW - headerSideInset * 2,
                IslandHubTheme.AccessHeaderCellHeight);
            composer.AddCellList(
                claimHeaderBounds,
                CreateClaimHeaderCell,
                BuildClaimHeaderCells(),
                "claimHeader");
        }

        composer.AddDynamicCustomDraw(
            ElementBounds.Fixed(areaX, detailsY, panelW, detailsPanelH),
            DrawSidePanelBackground,
            "claimDetailsBg");

        var selectedClaim = GetSelectedClaim();
        if (selectedClaim == null)
        {
            composer.AddStaticText(
                Lang.Get("swixyskyblock:island-claims-select"),
                CairoFont.WhiteDetailText(),
                ElementBounds.Fixed(detailsX, detailsY + 20, detailsW, 40),
                "claimSelectHint");
            return;
        }

        const int labelW = 108;
        const int buttonW = 138;
        const int inputH = 32;
        const int buttonH = 24;
        const int inputInset = 4;
        const int inputTextH = inputH - inputInset * 2;
        const int labelGap = 6;
        const int buttonGap = 10;
        const int rowGap = 10;
        const int firstRowY = detailsY + 34;
        const int secondRowY = firstRowY + inputH + rowGap;
        const int fieldX = detailsX + labelW + labelGap;
        const int buttonX = detailsX + detailsW - buttonW;
        const int fieldW = buttonX - fieldX - buttonGap;
        const int messageY = detailsY + detailsPanelH - 42;
        var canManageClaim = selectedClaim is { ViewerCanLeave: false };
        composer
            .AddDynamicText(
                selectedClaim.ViewerCanLeave
                    ? Lang.Get("swixyskyblock:island-claims-stats-resident", selectedClaim.OwnerName, selectedClaim.AreaCount, selectedClaim.ChunkCount)
                    : selectedClaim.ViewerIsCoOwner
                    ? Lang.Get("swixyskyblock:island-claims-stats-coowner", selectedClaim.OwnerName, selectedClaim.AreaCount, selectedClaim.ChunkCount)
                    : Lang.Get("swixyskyblock:island-claims-stats", selectedClaim.AreaCount, selectedClaim.ChunkCount),
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(detailsX, detailsY + 12, detailsW, 20),
                "claimStats");

        if (canManageClaim)
        {
            composer
                .AddDynamicText(
                    Lang.Get("swixyskyblock:island-claims-rename"),
                    CairoFont.WhiteSmallText(),
                    ElementBounds.Fixed(detailsX, firstRowY + 6, labelW, 20),
                    "renameLabel")
                .AddDynamicCustomDraw(
                    ElementBounds.Fixed(fieldX, firstRowY, fieldW, inputH),
                    DrawTextInputBackground,
                    "claimNameInputBg")
                .AddTextInput(
                    ElementBounds.Fixed(fieldX + inputInset, firstRowY + inputInset, fieldW - inputInset * 2, inputTextH),
                    text => claimNameInput = text,
                    CairoFont.WhiteDetailText(),
                    "claimNameInput")
                .AddDynamicText(
                    Lang.Get("swixyskyblock:island-claims-player-name"),
                    CairoFont.WhiteSmallText(),
                    ElementBounds.Fixed(detailsX, secondRowY + 6, labelW, 20),
                    "memberNameLabel")
                .AddDynamicCustomDraw(
                    ElementBounds.Fixed(fieldX, secondRowY, fieldW, inputH),
                    DrawTextInputBackground,
                    "memberNameInputBg")
                .AddTextInput(
                    ElementBounds.Fixed(fieldX + inputInset, secondRowY + inputInset, fieldW - inputInset * 2, inputTextH),
                    text => memberNameInput = text,
                    CairoFont.WhiteDetailText(),
                    "memberNameInput");

            AddHubButton(
                composer,
                Lang.Get("swixyskyblock:island-claims-rename-button"),
                RenameClaimButton,
                ElementBounds.Fixed(buttonX, firstRowY, buttonW, buttonH),
                active: false,
                IslandHubButtonKind.Action,
                "renameClaim");
            AddHubButton(
                composer,
                Lang.Get("swixyskyblock:island-claims-add-player"),
                AddMemberButton,
                ElementBounds.Fixed(buttonX, secondRowY, buttonW, buttonH),
                active: false,
                IslandHubButtonKind.Action,
                "addMember");
        }

        var memberListY = canManageClaim ? secondRowY + inputH + rowGap : detailsY + 40;
        var memberListHeight = Math.Max(120, messageY - memberListY - 8);
        // Правый край скролла = buttonX + buttonW = detailsX + detailsW (gap 3 + width 20 после трека).
        var memberListTrackBounds = ElementBounds.Fixed(
            detailsX,
            memberListY,
            IslandHubTheme.AccessMemberListTrackWidth,
            memberListHeight);
        memberListClipBounds = memberListTrackBounds.ForkContainingChild(3, 3, 3, 3);
        memberListTableBounds = memberListClipBounds.ForkContainingChild(0, 0, 0, -3).WithFixedPadding(3);

        composer
            .AddDynamicCustomDraw(memberListTrackBounds, DrawScrollAreaBackground, "memberListScrollBg")
            .AddInset(memberListTrackBounds, 3, 0.85f)
            .AddVerticalScrollbar(
                OnMemberListScroll,
                ElementStdBounds.VerticalScrollbar(memberListTrackBounds),
                "memberListScroll")
            .BeginClip(memberListClipBounds)
            .AddCellList(memberListTableBounds, CreateMemberCell, BuildMemberCells(selectedClaim), "memberList")
            .EndClip()
            .AddDynamicText(
                claimListState?.Message ?? "",
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(detailsX, messageY, detailsW, 40),
                "claimsMessage");
    }

    private bool SwitchToIslandPage()
    {
        activePage = PageIsland;
        ComposeDialog();
        return true;
    }

    private bool SwitchToAccessPage()
    {
        activePage = PageAccess;
        RequestClaimList();
        ComposeDialog();
        return true;
    }

    private bool SwitchToGeneratorPage()
    {
        activePage = PageGenerator;
        EnsureLocalGeneratorState();
        channel.SendPacket(new IslandGeneratorStateRequestPacket());
        ComposeDialog();
        return true;
    }

    private bool SwitchToStoryPage()
    {
        activePage = PageStory;
        RequestStoryState();
        ComposeDialog();
        return true;
    }

    private bool SwitchToTopPage()
    {
        activePage = PageTop;
        RequestTopState();
        ComposeDialog();
        return true;
    }

    private void ComposeGeneratorPageSafe()
    {
        try
        {
            ComposeDialog();
            clientApi.Logger.Notification("[SwixySkyBlock][Hub] Generator page composed.");
        }
        catch (Exception ex)
        {
            clientApi.Logger.Error("[SwixySkyBlock][Hub] Generator page compose failed: {0}", ex);
        }
    }

    private bool OnUpgradeGeneratorButton()
    {
        if (generatorState?.CanUpgrade != true)
        {
            return true;
        }

        channel.SendPacket(new IslandGeneratorUpgradeRequestPacket());
        return true;
    }

    private static string FormatGeneratorCostItem(string itemCode) =>
        itemCode switch
        {
            "game:gear-rusty" => "rusty gear",
            "game:gear-temporal" => "temporal gear",
            _ => itemCode
        };

    private bool OnGoHomeButton()
    {
        if (hubState?.HasIsland != true && hubState?.IsIslandResident != true)
        {
            return true;
        }

        SendIslandAction(IslandHubActionType.GoHome);
        TryClose();
        return true;
    }

    private bool OnGoSpawnButton()
    {
        SendIslandAction(IslandHubActionType.GoSpawn);
        TryClose();
        return true;
    }

    private bool OnTemplateSelected(string templateName)
    {
        if (hubState?.HasIsland == true || hubState?.IsIslandResident == true)
        {
            SingleComposer?.GetDynamicText("hubStatusText")
                ?.SetNewText(Lang.Get(hubState.IsIslandResident
                    ? "swixyskyblock:island-error-resident-cannot-create"
                    : "swixyskyblock:island-error-already-exists"));
            return true;
        }

        if (string.IsNullOrWhiteSpace(templateName))
        {
            return true;
        }

        channel.SendPacket(new IslandActionPacket
        {
            Action = IslandHubActionType.Create,
            TemplateName = templateName
        });
        ComposeDialog();
        return true;
    }

    private void SendIslandAction(int action, string templateName = "")
    {
        channel.SendPacket(new IslandActionPacket
        {
            Action = action,
            TemplateName = templateName
        });
    }

    private void UpdateHubStatusText()
    {
        if (activePage != PageIsland)
        {
            return;
        }

        SingleComposer?.GetDynamicText("hubStatusText")?.SetNewText(hubState?.Message ?? "");
    }

    private bool SelectClaimButton(IslandClaimInfoPacket claim)
    {
        if (selectedClaimId == claim.ClaimId && activePage == PageAccess)
        {
            return true;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        SelectClaim(claim);
        RunClaimsUiDeferred(() => RefreshClaimsSelectionUi(savedClaimScroll, 0));
        return true;
    }

    private void SelectClaim(IslandClaimInfoPacket claim)
    {
        selectedClaimId = claim.ClaimId;
        selectedMemberUid = "";
        claimNameInput = claim.Name;
    }

    private bool SelectMemberButton(IslandClaimMemberPacket member)
    {
        if (selectedMemberUid == member.PlayerUid && activePage == PageAccess)
        {
            return true;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        var savedMemberScroll = GetMemberListScrollOffset();
        SelectMember(member);
        RunClaimsUiDeferred(() => RefreshMemberSelectionUi(savedClaimScroll, savedMemberScroll));
        return true;
    }

    private void SelectMember(IslandClaimMemberPacket member)
    {
        selectedMemberUid = member.PlayerUid;
        if (!member.IsOwner)
        {
            memberNameInput = member.PlayerName;
        }
    }

    private bool AddMemberButton()
    {
        memberNameInput = SingleComposer?.GetTextInput("memberNameInput")?.GetText() ?? memberNameInput;
        SendClaimAction(
            IslandAccessActionType.AddPlayer,
            memberNameInput,
            (int)(EnumBlockAccessFlags.Use | EnumBlockAccessFlags.BuildOrBreak),
            "");
        return true;
    }

    private void RemoveMemberByUid(string memberUid)
    {
        var member = FindMemberByUid(memberUid);
        if (member == null || member.IsOwner)
        {
            return;
        }

        SendClaimAction(IslandAccessActionType.RemovePlayer, member.PlayerName, 0, "", member.PlayerUid);
    }

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
        RefreshMemberAccessIcons(savedClaimScroll, savedMemberScroll);
        SendClaimAction(IslandAccessActionType.GrantCoOwnership, member.PlayerName, 0, "", member.PlayerUid);
    }

    private bool RenameClaimButton()
    {
        claimNameInput = SingleComposer?.GetTextInput("claimNameInput")?.GetText() ?? claimNameInput;
        SendClaimAction(IslandAccessActionType.RenameClaim, "", 0, claimNameInput);
        return true;
    }

    private void SendClaimAction(int action, string playerName, int accessFlags, string claimName, string playerUid = "")
    {
        if (selectedClaimId <= 0)
        {
            return;
        }

        channel.SendPacket(new IslandClaimAccessActionPacket
        {
            ClaimId = selectedClaimId,
            Action = action,
            PlayerName = playerName,
            PlayerUid = playerUid,
            AccessFlags = accessFlags,
            ClaimName = claimName
        });
    }

    private IGuiElementCell CreateClaimHeaderCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        var claim = GetSelectedClaim();
        var headerCell = new IslandClaimListCell(clientApi, cell, bounds, claim?.ClaimId == highlightedClaimId)
        {
            AllowLeave = claim?.ViewerCanLeave == true,
            AllowDelete = claim is { ViewerCanLeave: false, ViewerIsCoOwner: false },
            AllowRecreate = claim is { IsIslandClaim: true, ViewerCanLeave: false },
            FixedHeight = IslandHubTheme.AccessHeaderCellHeight,
            OnMouseDownOnCellLeft = SelectClaimCell,
            OnMouseDownOnCellRight = ToggleClaimHighlightCell,
            OnMouseDownOnCellRecreate = RecreateClaimCell,
            OnMouseDownOnCellDelete = DeleteClaimCell,
            OnMouseDownOnCellLeave = LeaveClaimCell
        };
        return headerCell;
    }

    private IGuiElementCell CreateMemberCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        var member = FindMemberForCell(cell);
        var flags = (EnumBlockAccessFlags)(member?.AccessFlags ?? 0);
        return new IslandMemberListCell(
            clientApi,
            cell,
            bounds,
            flags.HasFlag(EnumBlockAccessFlags.Use),
            flags.HasFlag(EnumBlockAccessFlags.BuildOrBreak),
            member?.IsOwner ?? false)
        {
            MemberUid = member?.PlayerUid ?? "",
            IsCoOwner = member?.IsCoOwner ?? false,
            AllowCoOwnerCrown = GetSelectedClaim() is { ViewerIsCoOwner: false, ViewerCanLeave: false },
            OnMouseDownOnCellLeft = SelectMemberCell,
            OnMakeOwner = GrantCoOwnershipByUid,
            OnToggleUse = ToggleMemberUseByUid,
            OnToggleBuild = ToggleMemberBuildByUid,
            OnDeleteMember = RemoveMemberByUid
        };
    }

    private IGuiElementCell CreateTemplateCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        return new IslandTemplateListCell(clientApi, cell, bounds)
        {
            OnSelect = SelectTemplateCell
        };
    }

    private IGuiElementCell CreateGeneratorLevelCell(GeneratorLevelCellEntry cell, ElementBounds bounds)
    {
        return new IslandGeneratorLevelListCell(clientApi, cell, bounds);
    }

    private IGuiElementCell CreateStoryCell(StoryDungeonCellEntry cell, ElementBounds bounds)
    {
        return new StoryDungeonListCell(clientApi, cell, bounds);
    }

    private IEnumerable<StoryDungeonCellEntry> BuildStoryCells()
    {
        foreach (var site in (storyState?.Sites ?? []).OrderBy(static site => site.Order))
        {
            yield return new StoryDungeonCellEntry
            {
                Site = site,
                OnTeleport = OnStoryTeleportRequested
            };
        }
    }

    private void OnStoryTeleportRequested(string code)
    {
        channel.SendPacket(new StoryDungeonTeleportRequestPacket { Code = code });
    }

    private IGuiElementCell CreateTopCell(IslandTopCellEntry cell, ElementBounds bounds)
    {
        return new IslandTopListCell(clientApi, cell, bounds);
    }

    private IEnumerable<IslandTopCellEntry> BuildTopCells()
    {
        foreach (var entry in topState?.Entries ?? [])
        {
            yield return new IslandTopCellEntry
            {
                Entry = entry,
                TemplateLabel = ResolveTemplateLabel(entry.TemplateName)
            };
        }
    }

    private IEnumerable<GeneratorLevelCellEntry> BuildGeneratorLevelCells()
    {
        foreach (var level in (generatorState?.Levels ?? []).OrderBy(static entry => entry.Level))
        {
            yield return new GeneratorLevelCellEntry { Level = level };
        }
    }

    private IGuiElementCell CreateActionCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        return new IslandHubActionListCell(clientApi, cell, bounds)
        {
            OnSelect = _ => SelectActionCell(cell.Title)
        };
    }

    private string BuildClaimListDetailText(IslandClaimInfoPacket claim)
    {
        return claim.ViewerCanLeave
            ? Lang.Get("swixyskyblock:island-claims-list-resident-stats", claim.OwnerName, claim.ChunkCount)
            : claim.ViewerIsCoOwner
            ? Lang.Get("swixyskyblock:island-claims-list-coowner-stats", claim.OwnerName, claim.ChunkCount)
            : Lang.Get("swixyskyblock:island-claims-list-stats", claim.ChunkCount);
    }

    private IEnumerable<SavegameCellEntry> BuildClaimHeaderCells()
    {
        var claim = GetSelectedClaim();
        if (claim == null)
        {
            yield break;
        }

        yield return new SavegameCellEntry
        {
            Title = claim.Name,
            DetailText = BuildClaimListDetailText(claim),
            LeftOffY = 2,
            DetailTextOffY = 2,
            HoverText = Lang.Get("swixyskyblock:island-claims-highlight-hint"),
            Selected = true,
            Enabled = true,
            DrawAsButton = true
        };
    }

    private IEnumerable<SavegameCellEntry> BuildMemberCells(IslandClaimInfoPacket selectedClaim)
    {
        foreach (var member in selectedClaim.Members ?? [])
        {
            yield return new SavegameCellEntry
            {
                Title = member.PlayerName,
                DetailText = "",
                Selected = false,
                Enabled = true,
                DrawAsButton = true
            };
        }
    }

    private IEnumerable<SavegameCellEntry> BuildTemplateCells()
    {
        foreach (var templateName in hubState?.AvailableTemplates ?? [])
        {
            var label = ResolveTemplateLabel(templateName);
            yield return new SavegameCellEntry
            {
                Title = templateName,
                DetailText = label == templateName ? $"schematics/islands/{templateName}.json" : label,
                LeftOffY = 2,
                DetailTextOffY = 2,
                Enabled = true,
                DrawAsButton = true
            };
        }
    }

    private static IEnumerable<SavegameCellEntry> BuildActionCell(string title, bool enabled)
    {
        yield return new SavegameCellEntry
        {
            Title = title,
            Enabled = enabled,
            DrawAsButton = true
        };
    }

    private void SelectActionCell(string action)
    {
        if (action == "spawn")
        {
            OnGoSpawnButton();
            return;
        }

        if (action == "home")
        {
            OnGoHomeButton();
        }
    }

    private void SelectTemplateCell(int index)
    {
        var templates = hubState?.AvailableTemplates;
        if (templates == null || index < 0 || index >= templates.Count)
        {
            return;
        }

        OnTemplateSelected(templates[index]);
    }

    private IslandClaimMemberPacket? FindMemberForCell(SavegameCellEntry cell)
    {
        foreach (var member in GetSelectedClaim()?.Members ?? [])
        {
            if (member.PlayerName == cell.Title)
            {
                return member;
            }
        }

        return null;
    }

    private void SelectClaimCell(int index)
    {
        var claim = GetClaimAt(index);
        if (claim != null)
        {
            SelectClaimButton(claim);
        }
    }

    private void DeleteClaimCell(int index)
    {
        var claim = GetClaimAt(index);
        if (claim == null || claim.ViewerIsCoOwner || claim.ViewerCanLeave)
        {
            return;
        }

        SelectClaim(claim);

        if (highlightedClaimId == claim.ClaimId)
        {
            channel.SendPacket(new IslandClaimShowRequestPacket
            {
                ClaimId = claim.ClaimId,
                Clear = true
            });
            highlightedClaimId = 0;
            pendingHighlightClaimId = -1;
        }

        SendClaimAction(IslandAccessActionType.DeleteClaim, "", 0, "");
    }

    private void RecreateClaimCell(int index)
    {
        var claim = GetClaimAt(index);
        if (claim == null || claim.ViewerCanLeave || !claim.IsIslandClaim)
        {
            return;
        }

        SelectClaim(claim);
        SendClaimAction(IslandAccessActionType.RecreateIsland, "", 0, "");
    }

    private void LeaveClaimCell(int index)
    {
        var claim = GetClaimAt(index);
        if (claim == null || !claim.ViewerCanLeave)
        {
            return;
        }

        SelectClaim(claim);
        SendClaimAction(IslandAccessActionType.LeaveIsland, "", 0, "");
    }

    private void ToggleMemberUseByUid(string memberUid)
    {
        ToggleMemberAccessByUid(memberUid, EnumBlockAccessFlags.Use);
    }

    private void ToggleMemberBuildByUid(string memberUid)
    {
        ToggleMemberAccessByUid(memberUid, EnumBlockAccessFlags.BuildOrBreak);
    }

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
        RefreshMemberAccessIcons(savedClaimScroll, savedMemberScroll);
        SendClaimAction(IslandAccessActionType.UpdateMemberAccess, member.PlayerName, member.AccessFlags, "", member.PlayerUid);
    }

    private IslandClaimMemberPacket? FindMemberByUid(string memberUid)
    {
        return GetSelectedClaim()?.Members.FirstOrDefault(member => member.PlayerUid == memberUid);
    }

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

        channel.SendPacket(new IslandClaimShowRequestPacket
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

    private void SetPendingHighlightState(int claimId)
    {
        pendingHighlightClaimId = claimId;
        highlightedClaimId = claimId;
    }

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

    private void RefreshClaimHighlightIcons(float savedScroll)
    {
        RefreshClaimHeader(savedScroll, reloadCells: false);
    }

    private void RefreshMemberAccessIcons(float savedClaimScroll, float savedMemberScroll)
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("memberList");
        if (cellList == null)
        {
            return;
        }

        UpdateMemberListIconsInPlace(cellList);
        RestoreMemberListScroll(savedMemberScroll);
    }

    private void UpdateMemberListIconsInPlace(GuiElementCellList<SavegameCellEntry> cellList)
    {
        for (var i = 0; i < cellList.elementCells.Count; i++)
        {
            if (cellList.elementCells[i] is not IslandMemberListCell memberCell)
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

    private void SelectMemberCell(int index)
    {
        var member = GetMemberAt(index);
        if (member != null)
        {
            SelectMemberButton(member);
        }
    }

    private IslandClaimInfoPacket? GetSelectedClaim()
    {
        return claimListState?.Claims.FirstOrDefault(claim => claim.ClaimId == selectedClaimId);
    }

    private IslandClaimInfoPacket? GetClaimAt(int index)
    {
        if (index != 0)
        {
            return null;
        }

        return GetSelectedClaim();
    }

    private IslandClaimMemberPacket? GetMemberAt(int index)
    {
        var members = GetSelectedClaim()?.Members;
        if (members == null || index < 0 || index >= members.Count)
        {
            return null;
        }

        return members[index];
    }

    private void RunClaimsUiDeferred(Action action)
    {
        deferredClaimsUiAction = action;
        if (claimsUiDeferScheduled)
        {
            return;
        }

        claimsUiDeferScheduled = true;
        clientApi.Event.RegisterCallback(_ =>
        {
            claimsUiDeferScheduled = false;
            var deferredAction = deferredClaimsUiAction;
            deferredClaimsUiAction = null;

            if (!IsOpened() || activePage != PageAccess)
            {
                return;
            }

            deferredAction?.Invoke();
        }, 0);
    }

    private void RefreshClaimsSelectionUi(float savedClaimScroll, float savedMemberScroll)
    {
        if (!TryRefreshClaimsPageInPlace(savedClaimScroll, savedMemberScroll, reloadClaimCells: false))
        {
            ComposeDialog();
            RestoreMemberListScroll(savedMemberScroll);
            ApplyClaimsPageInputState();
        }
    }

    private void RefreshMemberSelectionUi(float savedClaimScroll, float savedMemberScroll)
    {
        if (!TryRefreshMemberListInPlace(savedClaimScroll, savedMemberScroll))
        {
            ComposeDialog();
            RestoreMemberListScroll(savedMemberScroll);
            ApplyClaimsPageInputState();
        }
    }

    private bool TryRefreshClaimsPageInPlace(float savedClaimScroll, float savedMemberScroll, bool reloadClaimCells = true)
    {
        if (activePage != PageAccess || SingleComposer == null || GetSelectedClaim() == null)
        {
            return false;
        }

        if (SingleComposer.GetTextInput("claimNameInput") == null)
        {
            return false;
        }

        RefreshClaimHeader(savedClaimScroll, reloadClaimCells);
        RefreshClaimDetailsUi();
        RestoreMemberListScroll(savedMemberScroll);
        return true;
    }

    private bool TryRefreshMemberListInPlace(float savedClaimScroll, float savedMemberScroll)
    {
        if (activePage != PageAccess || SingleComposer == null || GetSelectedClaim() == null)
        {
            return false;
        }

        var memberList = SingleComposer.GetCellList<SavegameCellEntry>("memberList");
        if (memberList == null)
        {
            return false;
        }

        memberList.ReloadCells(BuildMemberCells(GetSelectedClaim()!));
        RestoreMemberListScroll(savedMemberScroll);
        ApplyClaimsPageInputState();
        return true;
    }

    private void RefreshClaimHeader(float savedScroll, bool reloadCells)
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("claimHeader");
        if (cellList == null)
        {
            return;
        }

        if (reloadCells)
        {
            cellList.ReloadCells(BuildClaimHeaderCells());
        }
        else
        {
            UpdateClaimHeaderInPlace(cellList);
        }
    }

    private void UpdateClaimHeaderInPlace(GuiElementCellList<SavegameCellEntry> cellList)
    {
        var claim = GetSelectedClaim();
        if (claim == null)
        {
            return;
        }

        foreach (var elementCell in cellList.elementCells)
        {
            if (elementCell is not IslandClaimListCell highlightCell)
            {
                continue;
            }

            highlightCell.cellEntry.Selected = true;
            highlightCell.HighlightActive = claim.ClaimId == highlightedClaimId;
            highlightCell.AllowLeave = claim.ViewerCanLeave;
            highlightCell.AllowDelete = !claim.ViewerIsCoOwner && !claim.ViewerCanLeave;
            highlightCell.AllowRecreate = claim.IsIslandClaim && !claim.ViewerCanLeave;
            highlightCell.Compose();
        }
    }

    private void RefreshClaimDetailsUi()
    {
        var selectedClaim = GetSelectedClaim();
        if (selectedClaim == null || SingleComposer == null)
        {
            return;
        }

        claimNameInput = selectedClaim.Name;
        SingleComposer.GetDynamicText("claimStats")?.SetNewText(
            selectedClaim.ViewerIsCoOwner
                ? Lang.Get("swixyskyblock:island-claims-stats-coowner", selectedClaim.OwnerName, selectedClaim.AreaCount, selectedClaim.ChunkCount)
                : Lang.Get("swixyskyblock:island-claims-stats", selectedClaim.AreaCount, selectedClaim.ChunkCount));

        var memberList = SingleComposer.GetCellList<SavegameCellEntry>("memberList");
        if (memberList != null)
        {
            memberList.ReloadCells(BuildMemberCells(selectedClaim));
            RestoreMemberListScroll(0);
        }

        ApplyClaimsPageInputState();
    }

    private float GetClaimListScrollOffset() => 0;

    private float GetMemberListScrollOffset()
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("memberList");
        if (cellList != null)
        {
            return (float)Math.Max(0, -cellList.Bounds.fixedY);
        }

        return memberListScrollValue;
    }

    private void OnMemberListScroll(float value)
    {
        memberListScrollValue = value;
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("memberList");
        if (cellList == null)
        {
            return;
        }

        cellList.Bounds.fixedY = -value;
        cellList.Bounds.CalcWorldBounds();
    }

    private void OnTemplateListScroll(float value)
    {
        templateListScrollValue = value;
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("templateList");
        if (cellList == null)
        {
            return;
        }

        cellList.Bounds.fixedY = -value;
        cellList.Bounds.CalcWorldBounds();
    }

    private void ApplyIslandPageScrollState()
    {
        if (activePage != PageIsland || SingleComposer == null)
        {
            return;
        }

        RestoreTemplateListScroll(templateListScrollValue);
    }

    private void ApplyClaimsPageScrollState()
    {
        if (activePage != PageAccess || SingleComposer == null)
        {
            return;
        }

        var memberList = SingleComposer.GetCellList<SavegameCellEntry>("memberList");
        if (memberList != null)
        {
            memberList.unscaledCellSpacing = 4;
        }

        RestoreMemberListScroll(memberListScrollValue);
    }

    private void RunGeneratorUiDeferred(Action action)
    {
        deferredGeneratorUiAction = action;
        if (generatorUiDeferScheduled)
        {
            return;
        }

        generatorUiDeferScheduled = true;
        clientApi.Event.RegisterCallback(_ =>
        {
            generatorUiDeferScheduled = false;
            var deferredAction = deferredGeneratorUiAction;
            deferredGeneratorUiAction = null;

            if (!IsOpened() || activePage != PageGenerator)
            {
                return;
            }

            deferredAction?.Invoke();
        }, 0);
    }

    private void RefreshGeneratorPageUi()
    {
        if (!TryRefreshGeneratorPageInPlace())
        {
            ComposeGeneratorPageSafe();
        }
    }

    private bool TryRefreshGeneratorPageInPlace()
    {
        if (activePage != PageGenerator || SingleComposer == null || generatorState == null)
        {
            return false;
        }

        var cellList = SingleComposer.GetCellList<GeneratorLevelCellEntry>("generatorLevels");
        if (cellList == null || SingleComposer.GetDynamicText("generatorStatus") == null)
        {
            return false;
        }

        var state = generatorState;
        var status = state.HasIsland
            ? Lang.Get(
                "swixyskyblock:island-generator-status",
                state.CurrentLevel,
                state.MaxLevel,
                state.CostQuantity,
                FormatGeneratorCostItem(state.CostItemCode),
                state.PlayerCostItemCount)
            : Lang.Get("swixyskyblock:island-generator-no-island");

        SingleComposer.GetDynamicText("generatorStatus")?.SetNewText(status);
        SingleComposer.GetDynamicText("generatorMessage")?.SetNewText(state.Message ?? "");
        cellList.ReloadCells(BuildGeneratorLevelCells());
        RestoreGeneratorListScroll(generatorListScrollValue);
        return true;
    }

    private void ApplyGeneratorPageScrollState()
    {
        if (activePage != PageGenerator || SingleComposer == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<GeneratorLevelCellEntry>("generatorLevels");
        if (cellList != null)
        {
            cellList.unscaledCellSpacing = 6;
            cellList.UnscaledCellHorPadding = 2;
            cellList.UnscaledCellVerPadding = 2;
            cellList.ReloadCells(BuildGeneratorLevelCells());
        }

        RestoreGeneratorListScroll(generatorListScrollValue);
    }

    private bool TryRefreshStoryPageInPlace(float savedScroll)
    {
        if (activePage != PageStory || SingleComposer == null || storyState == null)
        {
            return false;
        }

        var cellList = SingleComposer.GetCellList<StoryDungeonCellEntry>("storyList");
        if (cellList == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(storyState.Message))
        {
            SingleComposer.GetDynamicText("storyHint")?.SetNewText(storyState.Message);
        }

        cellList.ReloadCells(BuildStoryCells());
        RestoreStoryListScroll(savedScroll);
        return true;
    }

    private void ApplyStoryPageScrollState()
    {
        if (activePage != PageStory || SingleComposer == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<StoryDungeonCellEntry>("storyList");
        if (cellList != null)
        {
            cellList.unscaledCellSpacing = 4;
            cellList.UnscaledCellHorPadding = 2;
            cellList.UnscaledCellVerPadding = 2;
            cellList.ReloadCells(BuildStoryCells());
        }

        RestoreStoryListScroll(storyListScrollValue);
    }

    private float GetStoryListScrollOffset()
    {
        var cellList = SingleComposer?.GetCellList<StoryDungeonCellEntry>("storyList");
        if (cellList != null)
        {
            return (float)Math.Max(0, -cellList.Bounds.fixedY);
        }

        return storyListScrollValue;
    }

    private void OnStoryListScroll(float value)
    {
        storyListScrollValue = value;
        var cellList = SingleComposer?.GetCellList<StoryDungeonCellEntry>("storyList");
        if (cellList == null)
        {
            return;
        }

        cellList.Bounds.fixedY = -value;
        cellList.Bounds.CalcWorldBounds();
    }

    private void RestoreStoryListScroll(float scrollValue)
    {
        if (SingleComposer == null || storyListClipBounds == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<StoryDungeonCellEntry>("storyList");
        if (cellList == null)
        {
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        storyListClipBounds.CalcWorldBounds();

        var clipHeight = (float)storyListClipBounds.fixedHeight;
        var tableHeight = (float)cellList.Bounds.fixedHeight;
        var maxScroll = Math.Max(0, tableHeight - clipHeight);
        storyListScrollValue = Math.Clamp(scrollValue, 0, maxScroll);

        var scroll = SingleComposer.GetScrollbar("storyListScroll");
        if (scroll != null)
        {
            scroll.SetHeights(clipHeight, tableHeight);
            scroll.CurrentYPosition = storyListScrollValue;
            scroll.RecomposeHandle();
        }

        cellList.Bounds.fixedY = -storyListScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

    private bool TryRefreshTopPageInPlace(float savedScroll)
    {
        if (activePage != PageTop || SingleComposer == null || topState == null)
        {
            return false;
        }

        var cellList = SingleComposer.GetCellList<IslandTopCellEntry>("topList");
        var subtitle = SingleComposer.GetDynamicText("topSubtitle");
        if (cellList == null || subtitle == null)
        {
            return false;
        }

        subtitle.SetNewText(Lang.Get("swixyskyblock:island-top-subtitle", topState.Entries.Count));
        cellList.ReloadCells(BuildTopCells());
        RestoreTopListScroll(savedScroll);
        return true;
    }

    private void ApplyTopPageScrollState()
    {
        if (activePage != PageTop || SingleComposer == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<IslandTopCellEntry>("topList");
        if (cellList != null)
        {
            cellList.unscaledCellSpacing = 4;
            cellList.UnscaledCellHorPadding = 2;
            cellList.UnscaledCellVerPadding = 2;
            cellList.ReloadCells(BuildTopCells());
        }

        RestoreTopListScroll(topListScrollValue);
    }

    private float GetTopListScrollOffset()
    {
        var cellList = SingleComposer?.GetCellList<IslandTopCellEntry>("topList");
        if (cellList != null)
        {
            return (float)Math.Max(0, -cellList.Bounds.fixedY);
        }

        return topListScrollValue;
    }

    private void OnTopListScroll(float value)
    {
        topListScrollValue = value;
        var cellList = SingleComposer?.GetCellList<IslandTopCellEntry>("topList");
        if (cellList == null)
        {
            return;
        }

        cellList.Bounds.fixedY = -value;
        cellList.Bounds.CalcWorldBounds();
    }

    private void RestoreTopListScroll(float scrollValue)
    {
        if (SingleComposer == null || topListClipBounds == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<IslandTopCellEntry>("topList");
        if (cellList == null)
        {
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        topListClipBounds.CalcWorldBounds();

        var clipHeight = (float)topListClipBounds.fixedHeight;
        var tableHeight = (float)cellList.Bounds.fixedHeight;
        var maxScroll = Math.Max(0, tableHeight - clipHeight);
        topListScrollValue = Math.Clamp(scrollValue, 0, maxScroll);

        var scroll = SingleComposer.GetScrollbar("topListScroll");
        if (scroll != null)
        {
            scroll.SetHeights(clipHeight, tableHeight);
            scroll.CurrentYPosition = topListScrollValue;
            scroll.RecomposeHandle();
        }

        cellList.Bounds.fixedY = -topListScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

    private void OnGeneratorListScroll(float value)
    {
        generatorListScrollValue = value;
        var cellList = SingleComposer?.GetCellList<GeneratorLevelCellEntry>("generatorLevels");
        if (cellList == null)
        {
            return;
        }

        cellList.Bounds.fixedY = -value;
        cellList.Bounds.CalcWorldBounds();
    }

    private void RestoreGeneratorListScroll(float scrollValue)
    {
        if (SingleComposer == null || generatorListClipBounds == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<GeneratorLevelCellEntry>("generatorLevels");
        if (cellList == null)
        {
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        generatorListClipBounds.CalcWorldBounds();

        var clipHeight = (float)generatorListClipBounds.fixedHeight;
        var tableHeight = (float)cellList.Bounds.fixedHeight;
        var maxScroll = Math.Max(0, tableHeight - clipHeight);
        generatorListScrollValue = Math.Clamp(scrollValue, 0, maxScroll);

        var scroll = SingleComposer.GetScrollbar("generatorListScroll");
        if (scroll != null)
        {
            scroll.SetHeights(clipHeight, tableHeight);
            scroll.CurrentYPosition = generatorListScrollValue;
            scroll.RecomposeHandle();
        }

        cellList.Bounds.fixedY = -generatorListScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

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
        var maxScroll = Math.Max(0, tableHeight - clipHeight);
        memberListScrollValue = Math.Clamp(scrollValue, 0, maxScroll);

        var memberScroll = SingleComposer.GetScrollbar("memberListScroll");
        if (memberScroll != null)
        {
            memberScroll.SetHeights(clipHeight, tableHeight);
            memberScroll.CurrentYPosition = memberListScrollValue;
            memberScroll.RecomposeHandle();
        }

        cellList.Bounds.fixedY = -memberListScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

    private void RestoreTemplateListScroll(float scrollValue)
    {
        if (SingleComposer == null || templateListClipBounds == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<SavegameCellEntry>("templateList");
        if (cellList == null)
        {
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        templateListClipBounds.CalcWorldBounds();

        var clipHeight = (float)templateListClipBounds.fixedHeight;
        var tableHeight = (float)cellList.Bounds.fixedHeight;
        var maxScroll = Math.Max(0, tableHeight - clipHeight);
        templateListScrollValue = Math.Clamp(scrollValue, 0, maxScroll);

        var templateScroll = SingleComposer.GetScrollbar("templateListScroll");
        if (templateScroll != null)
        {
            templateScroll.SetHeights(clipHeight, tableHeight);
            templateScroll.CurrentYPosition = templateListScrollValue;
            templateScroll.RecomposeHandle();
        }

        cellList.Bounds.fixedY = -templateListScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

    private void ApplyClaimsPageInputState()
    {
        if (activePage != PageAccess || SingleComposer == null)
        {
            return;
        }

        SingleComposer.GetTextInput("claimNameInput")?.SetValue(claimNameInput, true);
        SingleComposer.GetTextInput("memberNameInput")?.SetValue(memberNameInput, true);
    }

    private void AddHubButton(
        GuiComposer composer,
        string label,
        ActionConsumable onClick,
        ElementBounds bounds,
        bool active,
        IslandHubButtonKind kind,
        string key,
        bool enabled = true)
    {
        composer.AddInteractiveElement(
            new IslandHubButtonElement(clientApi, label, bounds, onClick, active, kind, enabled),
            key);
    }

    private static void DrawHubDialogBackground(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        IslandHubTheme.DrawDialogBackground(ctx, bounds.OuterWidth, bounds.OuterHeight);
    }

    private static void DrawTabBarBackground(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        IslandHubTheme.DrawTabBar(ctx, bounds.OuterWidth, bounds.OuterHeight);
    }

    private void DrawTemplateHeader(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        IslandHubTheme.DrawSectionHeaderStrip(ctx, bounds.OuterWidth, bounds.OuterHeight);
        templateHeaderTextUtil.AutobreakAndDrawMultilineTextAt(
            ctx,
            IslandHubTheme.CreateSectionTitleFont(),
            Lang.Get("swixyskyblock:island-template-pick"),
            4,
            7,
            bounds.InnerWidth);
    }

    private static void DrawAccessHeaderBackground(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        IslandHubTheme.DrawPanel(ctx, bounds.OuterWidth, bounds.OuterHeight, 6);
    }

    private static void DrawSidePanelBackground(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        IslandHubTheme.DrawPanel(ctx, bounds.OuterWidth, bounds.OuterHeight);
    }

    private static void DrawTextInputBackground(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        IslandHubTheme.DrawTextInput(ctx, bounds.OuterWidth, bounds.OuterHeight);
    }

    private static void DrawScrollAreaBackground(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        IslandHubTheme.DrawScrollArea(ctx, bounds.OuterWidth, bounds.OuterHeight);
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }
}
