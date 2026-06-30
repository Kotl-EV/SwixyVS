// =============================================================================
// IslandHubDialog.cs
// -----------------------------------------------------------------------------
// SkyBlock island hub GUI: island management tab and claim access tab.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
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

    private readonly ICoreClientAPI clientApi;
    private readonly IClientNetworkChannel channel;

    private IslandHubStatePacket? hubState;
    private IslandClaimListStatePacket? claimListState;

    /// <summary>Хранит последнюю полную версию списка территорий (для сравнения с дельта).</summary>
    private List<IslandClaimInfoPacket>? lastFullClaimList = [];

    private int activePage = PageIsland;
    private int selectedClaimId;
    private int highlightedClaimId;
    private int pendingHighlightClaimId = -1;

    private string selectedMemberUid = "";
    private string memberNameInput = "";
    private string claimNameInput = "";
    private bool showTemplatePicker;

    private float claimListScrollValue;
    private float memberListScrollValue;
    private ElementBounds? claimListTableBounds;
    private ElementBounds? claimListClipBounds;
    private ElementBounds? memberListTableBounds;
    private ElementBounds? memberListClipBounds;

    private bool claimsUiDeferScheduled;
    private Action? deferredClaimsUiAction;

    public override string ToggleKeyCombinationCode => SwixySkyBlockMod.OpenIslandHubHotkeyCode;

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

    public void ApplyHubState(IslandHubStatePacket packet)
    {
        hubState = packet;

        if (activePage == PageIsland)
        {
            SingleComposer?.GetDynamicText("hubStatusText")?.SetNewText(packet.Message ?? "");
            if (showTemplatePicker && (packet.AvailableTemplates == null || packet.AvailableTemplates.Count == 0))
            {
                showTemplatePicker = false;
                ComposeDialog();
            }
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
                RestoreClaimListScroll(savedClaimScroll);
                RestoreMemberListScroll(savedMemberScroll);
                ApplyClaimsPageInputState();
            }

            SingleComposer?.GetDynamicText("claimsMessage")?.SetNewText(packet.Message ?? "");
        }

        // Сохраняем полный список для дельта-обновлений
        lastFullClaimList = packet.Claims;
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

        if (highlightedClaimId > 0 && activePage == PageAccess)
        {
            RunClaimsUiDeferred(() => RefreshClaimHighlightIcons(GetClaimListScrollOffset()));
        }
    }

    public override bool OnEscapePressed()
    {
        if (showTemplatePicker)
        {
            showTemplatePicker = false;
            ComposeDialog();
            return true;
        }

        TryClose();
        return true;
    }

    private void ComposeDialog()
    {
        var mainBounds = ElementBounds.Fixed(0, 0, 780, 690);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        bgBounds.WithChildren(mainBounds);

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);

        ClearComposers();
        var composer = clientApi.Gui
            .CreateCompo("islandhub", dialogBounds)
            .AddShadedDialogBG(bgBounds, true)
            .AddDialogTitleBar(Lang.Get("swixyskyblock:island-hub-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds)
            .AddButton(
                Lang.Get("swixyskyblock:island-tab-island"),
                SwitchToIslandPage,
                ElementBounds.Fixed(18, 40, 110, 30),
                activePage == PageIsland ? EnumButtonStyle.Small : EnumButtonStyle.Normal,
                "islandTab")
            .AddButton(
                Lang.Get("swixyskyblock:island-tab-access"),
                SwitchToAccessPage,
                ElementBounds.Fixed(136, 40, 130, 30),
                activePage == PageAccess ? EnumButtonStyle.Small : EnumButtonStyle.Normal,
                "accessTab");

        if (activePage == PageIsland)
        {
            ComposeIslandPage(composer);
        }
        else
        {
            ComposeAccessPage(composer);
        }

        SingleComposer = composer.EndChildElements().Compose();
        ApplyClaimsPageScrollState();
        ApplyClaimsPageInputState();
        UpdateHubStatusText();
    }

    private void ComposeIslandPage(GuiComposer composer)
    {
        const int buttonY = 100;
        const int buttonW = 200;
        const int buttonH = 120;
        const int gap = 18;
        const int startX = 28;
        var hasIsland = hubState?.HasIsland == true;
        var hasHome = hasIsland || hubState?.IsIslandResident == true;
        var canCreate = !hasIsland && hubState?.IsIslandResident != true;

        AddHubActionCard(
            composer,
            startX,
            buttonY,
            buttonW,
            buttonH,
            "createIsland",
            "swixyskyblock:island-action-create",
            IslandHubIcons.DrawCreateIsland,
            OnCreateIslandButton,
            enabled: canCreate);
        AddHubActionCard(
            composer,
            startX + (buttonW + gap),
            buttonY,
            buttonW,
            buttonH,
            "homeIsland",
            "swixyskyblock:island-action-home",
            IslandHubIcons.DrawGoHome,
            OnGoHomeButton,
            enabled: hasHome);
        AddHubActionCard(
            composer,
            startX + (buttonW + gap) * 2,
            buttonY,
            buttonW,
            buttonH,
            "spawnIsland",
            "swixyskyblock:island-action-spawn",
            IslandHubIcons.DrawGoSpawn,
            OnGoSpawnButton);
        composer.AddDynamicText(
            hubState?.Message ?? "",
            CairoFont.WhiteDetailText(),
            ElementBounds.Fixed(40, 250, 700, 80),
            "hubStatusText");

        if (showTemplatePicker)
        {
            var templates = hubState?.AvailableTemplates ?? [];
            var templateY = 360;
            var templateX = 40;
            const int cardW = 180;
            const int cardH = 118;
            const int templateGap = 16;
            const int cols = 3;

            composer.AddDynamicText(
                Lang.Get("swixyskyblock:island-template-pick"),
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(templateX, templateY - 28, 500, 22),
                "templatePickLabel");

            for (var i = 0; i < templates.Count; i++)
            {
                var templateName = templates[i];
                var col = i % cols;
                var row = i / cols;
                var x = templateX + col * (cardW + templateGap);
                var y = templateY + row * (cardH + templateGap);
                var label = ResolveTemplateLabel(templateName);

                composer
                    .AddDynamicCustomDraw(
                        ElementBounds.Fixed(x, y, cardW, cardH),
                        (ctx, _, bounds) => IslandHubIcons.DrawTemplateCard(
                            ctx,
                            bounds.OuterWidth,
                            bounds.OuterHeight,
                            templateName),
                        $"templateCard{i}")
                    .AddButton(
                        label,
                        () => OnTemplateSelected(templateName),
                        ElementBounds.Fixed(x, y + cardH - 30, cardW, 28),
                        EnumButtonStyle.Small,
                        $"templateBtn{i}");
            }
        }
    }

    private static string ResolveTemplateLabel(string templateName)
    {
        var key = $"swixyskyblock:island-template-{templateName}";
        var translated = Lang.Get(key);
        return translated == key ? templateName : translated;
    }

    private static void AddHubActionCard(
        GuiComposer composer,
        int x,
        int y,
        int width,
        int height,
        string id,
        string labelKey,
        Action<Context, double, double, double> drawIcon,
        ActionConsumable onClick,
        bool enabled = true)
    {
        composer
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(x, y, width, height),
                (ctx, _, bounds) => IslandHubIcons.DrawActionCard(
                    ctx,
                    bounds.OuterWidth,
                    bounds.OuterHeight,
                    drawIcon,
                    enabled),
                $"{id}Draw")
            .AddButton(
                Lang.Get(labelKey),
                onClick,
                ElementBounds.Fixed(x, y + height - 28, width, 28),
                EnumButtonStyle.Small,
                $"{id}Label");
    }

    private void ComposeAccessPage(GuiComposer composer)
    {
        const int detailsX = 304;
        const int detailsW = 422;

        composer
            .AddDynamicCustomDraw(ElementBounds.Fixed(18, 84, 258, 570), DrawSidePanelBackground, "claimListBg")
            .AddDynamicCustomDraw(ElementBounds.Fixed(292, 84, 454, 570), DrawSidePanelBackground, "claimDetailsBg")
            .AddDynamicText(
                Lang.Get("swixyskyblock:island-claims-list-title"),
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(20, 92, 224, 20),
                "claimsListTitle");

        var claims = claimListState?.Claims ?? [];
        var claimListBounds = ElementBounds.Fixed(20, 116, 224, 528);
        claimListClipBounds = claimListBounds.ForkContainingChild(3, 3, 3, 3);
        claimListTableBounds = claimListClipBounds.ForkContainingChild(0, 0, 0, -3).WithFixedPadding(3);

        composer
            .AddInset(claimListBounds, 3, 0.85f)
            .AddVerticalScrollbar(OnClaimListScroll, ElementStdBounds.VerticalScrollbar(claimListBounds), "claimListScroll")
            .BeginClip(claimListClipBounds)
            .AddCellList(claimListTableBounds, CreateClaimListCell, BuildClaimCells(), "claimList")
            .EndClip();

        if (claims.Count == 0)
        {
            composer.AddStaticText(
                Lang.Get("swixyskyblock:island-claims-empty"),
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(20, 118, 224, 40),
                "claimsEmpty");
        }

        var selectedClaim = GetSelectedClaim();
        if (selectedClaim == null)
        {
            composer.AddStaticText(
                Lang.Get("swixyskyblock:island-claims-select"),
                CairoFont.WhiteDetailText(),
                ElementBounds.Fixed(detailsX, 104, detailsW, 40),
                "claimSelectHint");
            return;
        }

        const int labelW = 124;
        const int buttonW = 122;
        const int controlH = 32;
        const int labelGap = 2;
        const int buttonGap = 8;
        const int renameInputX = detailsX + labelW + labelGap;
        const int buttonX = detailsX + detailsW - buttonW;
        const int renameInputW = buttonX - renameInputX - buttonGap;
        const int memberInputX = detailsX + labelW + labelGap;
        const int memberInputW = buttonX - memberInputX - buttonGap;
        const int memberListX = detailsX - 10;
        var canManageClaim = selectedClaim is { ViewerCanLeave: false };
        var actionButtonFont = CairoFont.ButtonText().WithFontSize(13);

        composer
            .AddDynamicText(
                selectedClaim.ViewerCanLeave
                    ? Lang.Get("swixyskyblock:island-claims-stats-resident", selectedClaim.OwnerName, selectedClaim.AreaCount, selectedClaim.ChunkCount)
                    : selectedClaim.ViewerIsCoOwner
                    ? Lang.Get("swixyskyblock:island-claims-stats-coowner", selectedClaim.OwnerName, selectedClaim.AreaCount, selectedClaim.ChunkCount)
                    : Lang.Get("swixyskyblock:island-claims-stats", selectedClaim.AreaCount, selectedClaim.ChunkCount),
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(detailsX, 96, detailsW, 20),
                "claimStats");

        if (canManageClaim)
        {
            composer
            .AddDynamicCustomDraw(ElementBounds.Fixed(renameInputX, 116, renameInputW, controlH), DrawTextInputBackground, "claimNameInputBg")
            .AddTextInput(
                ElementBounds.Fixed(renameInputX + 4, 120, renameInputW - 8, 24),
                text => claimNameInput = text,
                CairoFont.WhiteDetailText(),
                "claimNameInput")
            .AddButton(
                Lang.Get("swixyskyblock:island-claims-rename-button"),
                RenameClaimButton,
                ElementBounds.Fixed(buttonX, 116, buttonW, controlH),
                actionButtonFont,
                EnumButtonStyle.Small,
                "renameClaim")
            .AddDynamicText(
                Lang.Get("swixyskyblock:island-claims-rename"),
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(detailsX, 120, labelW, 20),
                "renameLabel")
            .AddDynamicText(
                Lang.Get("swixyskyblock:island-claims-player-name"),
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(detailsX, 158, labelW, 20),
                "memberNameLabel")
            .AddDynamicCustomDraw(ElementBounds.Fixed(memberInputX, 154, memberInputW, controlH), DrawTextInputBackground, "memberNameInputBg")
            .AddTextInput(
                ElementBounds.Fixed(memberInputX + 4, 158, memberInputW - 8, 24),
                text => memberNameInput = text,
                CairoFont.WhiteDetailText(),
                "memberNameInput")
            .AddButton(
                Lang.Get("swixyskyblock:island-claims-add-player"),
                AddMemberButton,
                ElementBounds.Fixed(buttonX, 154, buttonW, controlH),
                actionButtonFont,
                EnumButtonStyle.Small,
                "addMember");
        }

        var memberListY = canManageClaim ? 196 : 124;
        var memberListHeight = canManageClaim ? 408 : 480;
        var memberListBounds = ElementBounds.Fixed(memberListX, memberListY, detailsW, memberListHeight);
        memberListClipBounds = memberListBounds.ForkContainingChild(3, 3, 3, 3);
        memberListTableBounds = memberListClipBounds.ForkContainingChild(0, 0, 0, -3).WithFixedPadding(3);

        composer
            .AddDynamicCustomDraw(memberListBounds, DrawScrollAreaBackground, "memberListScrollBg")
            .AddInset(memberListBounds, 3, 0.85f)
            .AddVerticalScrollbar(OnMemberListScroll, ElementStdBounds.VerticalScrollbar(memberListBounds), "memberListScroll")
            .BeginClip(memberListClipBounds)
            .AddCellList(memberListTableBounds, CreateMemberCell, BuildMemberCells(selectedClaim), "memberList")
            .EndClip()
            .AddDynamicText(
                claimListState?.Message ?? "",
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(detailsX, 612, detailsW, 40),
                "claimsMessage");
    }

    private bool SwitchToIslandPage()
    {
        activePage = PageIsland;
        showTemplatePicker = false;
        ComposeDialog();
        return true;
    }

    private bool SwitchToAccessPage()
    {
        activePage = PageAccess;
        showTemplatePicker = false;
        RequestClaimList();
        ComposeDialog();
        return true;
    }

    private bool OnCreateIslandButton()
    {
        if (hubState?.HasIsland == true || hubState?.IsIslandResident == true)
        {
            return true;
        }

        showTemplatePicker = true;
        ComposeDialog();
        return true;
    }

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
        showTemplatePicker = false;
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

    private IGuiElementCell CreateClaimListCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        var claim = FindClaimForCell(cell);
        return new IslandClaimListCell(clientApi, cell, bounds, claim?.ClaimId == highlightedClaimId)
        {
            AllowLeave = claim?.ViewerCanLeave == true,
            AllowDelete = claim is { ViewerCanLeave: false, ViewerIsCoOwner: false },
            AllowRecreate = claim is { IsIslandClaim: true, ViewerCanLeave: false },
            OnMouseDownOnCellLeft = SelectClaimCell,
            OnMouseDownOnCellRight = ToggleClaimHighlightCell,
            OnMouseDownOnCellRecreate = RecreateClaimCell,
            OnMouseDownOnCellDelete = DeleteClaimCell,
            OnMouseDownOnCellLeave = LeaveClaimCell
        };
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

    private string BuildClaimListDetailText(IslandClaimInfoPacket claim)
    {
        return claim.ViewerCanLeave
            ? Lang.Get("swixyskyblock:island-claims-list-resident-stats", claim.OwnerName, claim.ChunkCount)
            : claim.ViewerIsCoOwner
            ? Lang.Get("swixyskyblock:island-claims-list-coowner-stats", claim.OwnerName, claim.ChunkCount)
            : Lang.Get("swixyskyblock:island-claims-list-stats", claim.ChunkCount);
    }

    private IEnumerable<SavegameCellEntry> BuildClaimCells()
    {
        foreach (var claim in claimListState?.Claims ?? [])
        {
            yield return new SavegameCellEntry
            {
                Title = claim.Name,
                DetailText = BuildClaimListDetailText(claim),
                LeftOffY = 2,
                DetailTextOffY = 2,
                HoverText = Lang.Get("swixyskyblock:island-claims-highlight-hint"),
                Selected = claim.ClaimId == selectedClaimId,
                Enabled = true,
                DrawAsButton = true
            };
        }
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

    private IslandClaimInfoPacket? FindClaimForCell(SavegameCellEntry cell)
    {
        foreach (var claim in claimListState?.Claims ?? [])
        {
            if (claim.Name == cell.Title && BuildClaimListDetailText(claim) == cell.DetailText)
            {
                return claim;
            }
        }

        return null;
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
        RefreshClaimListSelection(savedScroll, reloadCells: false);
    }

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
        var claims = claimListState?.Claims;
        if (claims == null || index < 0 || index >= claims.Count)
        {
            return null;
        }

        return claims[index];
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

    private void OnClaimListScroll(float value)
    {
        claimListScrollValue = value;
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("claimList");
        if (cellList == null)
        {
            return;
        }

        cellList.Bounds.fixedY = -value;
        cellList.Bounds.CalcWorldBounds();
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
            RestoreClaimListScroll(savedClaimScroll);
            RestoreMemberListScroll(savedMemberScroll);
            ApplyClaimsPageInputState();
        }
    }

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

        RefreshClaimListSelection(savedClaimScroll, reloadClaimCells);
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
        RestoreClaimListScroll(savedClaimScroll);
        RestoreMemberListScroll(savedMemberScroll);
        ApplyClaimsPageInputState();
        return true;
    }

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
        }
        else
        {
            UpdateClaimListSelectionInPlace(cellList);
        }

        RestoreClaimListScroll(savedScroll);
    }

    private void UpdateClaimListSelectionInPlace(GuiElementCellList<SavegameCellEntry> cellList)
    {
        for (var i = 0; i < cellList.elementCells.Count; i++)
        {
            if (cellList.elementCells[i] is not IslandClaimListCell highlightCell)
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

    private float GetClaimListScrollOffset()
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("claimList");
        if (cellList != null)
        {
            return (float)Math.Max(0, -cellList.Bounds.fixedY);
        }

        return claimListScrollValue;
    }

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

    private void ApplyClaimsPageScrollState()
    {
        if (activePage == PageIsland || SingleComposer == null)
        {
            return;
        }

        RestoreClaimListScroll(claimListScrollValue);
        RestoreMemberListScroll(memberListScrollValue);
    }

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
        var maxScroll = Math.Max(0, tableHeight - clipHeight);
        claimListScrollValue = Math.Clamp(scrollValue, 0, maxScroll);

        var claimScroll = SingleComposer.GetScrollbar("claimListScroll");
        if (claimScroll != null)
        {
            claimScroll.SetHeights(clipHeight, tableHeight);
            claimScroll.CurrentYPosition = claimListScrollValue;
            claimScroll.RecomposeHandle();
        }

        cellList.Bounds.fixedY = -claimListScrollValue;
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

    private void ApplyClaimsPageInputState()
    {
        if (activePage == PageIsland || SingleComposer == null)
        {
            return;
        }

        SingleComposer.GetTextInput("claimNameInput")?.SetValue(claimNameInput, true);
        SingleComposer.GetTextInput("memberNameInput")?.SetValue(memberNameInput, true);
    }

    private static void DrawSidePanelBackground(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var width = bounds.OuterWidth;
        var height = bounds.OuterHeight;

        ctx.SetSourceRGBA(0, 0, 0, 0.96);
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();

        ctx.SetSourceRGBA(0, 0, 0, 1);
        ctx.LineWidth = 4;
        ctx.Rectangle(2, 2, width - 4, height - 4);
        ctx.Stroke();
    }

    private static void DrawTextInputBackground(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var width = bounds.OuterWidth;
        var height = bounds.OuterHeight;

        ctx.SetSourceRGBA(0.06, 0.065, 0.07, 0.98);
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();

        ctx.SetSourceRGBA(0.85, 0.9, 0.95, 0.7);
        ctx.LineWidth = 1.5;
        ctx.Rectangle(1, 1, width - 2, height - 2);
        ctx.Stroke();
    }

    private static void DrawScrollAreaBackground(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var width = bounds.OuterWidth;
        var height = bounds.OuterHeight;

        ctx.SetSourceRGBA(0.035, 0.04, 0.045, 0.98);
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }
}