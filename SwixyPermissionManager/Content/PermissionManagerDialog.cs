// =============================================================================
// Permission Manager — чистый VS-диалог (без claim GUI chrome).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using SwixyPermissionManager.Core;
using SwixyPermissionManager.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixyPermissionManager.Content;

public sealed class PermissionManagerDialog : GuiDialog
{
    private readonly ICoreClientAPI clientApi;
    private readonly IClientNetworkChannel channel;

    private PermissionStatePacket? state;
    private string selectedRoleCode = "";
    private string selectedPrivilegeCode = "";
    private string statusMessage = "";
    private int statusType;

    private string newRoleInput = "";
    private string renameInput = "";
    private string playerInput = "";
    private string privilegeFilter = "";

    private string levelInput = "";
    private string allowanceInput = "";
    private string maxAreasInput = "";
    private string minXInput = "";
    private string minYInput = "";
    private string minZInput = "";

    private bool deleteConfirmArmed;
    private bool pickCompareRole;
    private string compareRoleCode = "";

    private float roleScrollValue;
    private float privilegeScrollValue;
    private ElementBounds? roleClipBounds;
    private ElementBounds? privilegeClipBounds;
    /// <summary>Hit-test for mouse wheel — bounds that live in the composer tree.</summary>
    private ElementBounds? roleListBounds;
    private ElementBounds? privListBounds;
    private bool scrollRestoreScheduled;

    public override string ToggleKeyCombinationCode => PermissionConstants.OpenGuiHotkeyCode;
    public override bool PrefersUngrabbedMouse => true;

    public PermissionManagerDialog(ICoreClientAPI api, IClientNetworkChannel channel)
        : base(api)
    {
        clientApi = api;
        this.channel = channel;
        ComposeDialog();
    }

    public void RequestRefresh() => channel.SendPacket(new PermissionStateRequestPacket());

    public void ApplyState(PermissionStatePacket packet)
    {
        // Сохранить скролл ДО смены state (ответ grant/revoke не должен прыгать список).
        CaptureScrollPositions();
        var savedRoleScroll = roleScrollValue;
        var savedPrivScroll = privilegeScrollValue;
        var prevRole = selectedRoleCode;

        // Состав ролей изменился (create/delete/clone) → нужна полная пересборка списка.
        var prevCodes = new HashSet<string>(
            state?.Roles?.Select(r => r.Code ?? "") ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        var nextCodes = new HashSet<string>(
            packet.Roles?.Select(r => r.Code ?? "") ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        var rolesSetChanged = prevCodes.Count != nextCodes.Count || !prevCodes.SetEquals(nextCodes);

        state = packet;
        if (!string.IsNullOrEmpty(packet.StatusMessage))
        {
            statusMessage = packet.StatusMessage;
            statusType = packet.MessageType;
        }

        var roles = packet.Roles ?? [];
        var privileges = packet.Privileges ?? [];

        // Case-insensitive: create отдаёт code lowercase, packet.RoleCode мог быть «David».
        if (!string.IsNullOrEmpty(packet.SelectedRoleCode)
            && roles.Any(r =>
                string.Equals(r.Code, packet.SelectedRoleCode, StringComparison.OrdinalIgnoreCase)))
        {
            selectedRoleCode = roles
                .First(r => string.Equals(r.Code, packet.SelectedRoleCode, StringComparison.OrdinalIgnoreCase))
                .Code;
        }

        if (string.IsNullOrEmpty(selectedRoleCode)
            || roles.All(r =>
                !string.Equals(r.Code, selectedRoleCode, StringComparison.OrdinalIgnoreCase)))
        {
            selectedRoleCode = roles.FirstOrDefault()?.Code ?? "";
        }

        if (!string.IsNullOrEmpty(selectedPrivilegeCode)
            && privileges.All(p => p.Code != selectedPrivilegeCode))
        {
            selectedPrivilegeCode = "";
        }

        var roleChanged = !string.Equals(prevRole, selectedRoleCode, StringComparison.OrdinalIgnoreCase);
        if (roleChanged || packet.MessageType == 0 || string.IsNullOrEmpty(levelInput))
        {
            SyncClaimInputsFromRole(SelectedRole);
        }

        if (!IsOpened())
        {
            return;
        }

        clientApi.Logger.Notification(
            "[SwixyPermissionManager] ApplyState roles={0} selected='{1}' rolesSetChanged={2} status='{3}'",
            packet.Roles?.Count ?? 0, selectedRoleCode, rolesSetChanged, packet.StatusMessage);

        // Create/delete/clone: ReloadCells alone often doesn't grow the left list correctly.
        if (rolesSetChanged)
        {
            roleScrollValue = roleChanged ? 0f : savedRoleScroll;
            privilegeScrollValue = roleChanged ? 0f : savedPrivScroll;
            ComposeDialog();
            return;
        }

        // In-place: не ComposeDialog — иначе скролл прав сбрасывается после RMB grant/revoke.
        roleScrollValue = savedRoleScroll;
        privilegeScrollValue = roleChanged ? 0f : savedPrivScroll;
        try
        {
            RefreshRoleSelectionInPlace(resetPrivilegeScroll: roleChanged);
            if (!roleChanged)
            {
                privilegeScrollValue = savedPrivScroll;
                roleScrollValue = savedRoleScroll;
                RestorePrivilegeScroll(savedPrivScroll);
                RestoreRoleScroll(savedRoleScroll);
                ScheduleScrollRestore();
            }
        }
        catch (Exception ex)
        {
            clientApi.Logger.Warning("[SwixyPermissionManager] ApplyState in-place failed: {0}", ex.Message);
            roleScrollValue = savedRoleScroll;
            privilegeScrollValue = savedPrivScroll;
            ComposeDialog();
        }
    }

    public void SetStatus(string message, int type)
    {
        statusMessage = message ?? "";
        statusType = type;
        SingleComposer?.GetDynamicText("statusText")?.SetNewText(statusMessage);
    }

    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        RequestRefresh();
    }

    private RolePacket? SelectedRole =>
        state?.Roles?.FirstOrDefault(r => r.Code == selectedRoleCode);

    private RolePacket? CompareRole =>
        string.IsNullOrEmpty(compareRoleCode)
            ? null
            : state?.Roles?.FirstOrDefault(r => r.Code == compareRoleCode);

    private void SyncClaimInputsFromRole(RolePacket? role)
    {
        if (role == null)
        {
            levelInput = allowanceInput = maxAreasInput = minXInput = minYInput = minZInput = "";
            renameInput = "";
            return;
        }

        levelInput = role.PrivilegeLevel.ToString();
        allowanceInput = role.LandClaimAllowance.ToString();
        maxAreasInput = role.LandClaimMaxAreas.ToString();
        minXInput = role.LandClaimMinX.ToString();
        minYInput = role.LandClaimMinY.ToString();
        minZInput = role.LandClaimMinZ.ToString();
        renameInput = role.Name;
    }

    private void ComposeDialog()
    {
        // Сохранить позицию скролла ДО уничтожения composer (иначе clamp по высоте 0 сбрасывает в 0).
        CaptureScrollPositions();

        var roles = state?.Roles ?? [];
        var privileges = state?.Privileges ?? [];
        var role = SelectedRole;
        var granted = new HashSet<string>(role?.Privileges ?? [], StringComparer.OrdinalIgnoreCase);

        var filtered = GetFilteredPrivileges(privileges);

        // Layout (top → bottom)
        // lists end ~360; bottom dark-blue panel (description + actions) is larger.

        const int pad = PermissionTheme.Pad;
        const int leftX = PermissionTheme.LeftX;
        const int leftW = PermissionTheme.LeftW;
        const int rightX = PermissionTheme.RightX;
        const int rightW = PermissionTheme.RightW;
        const int inputH = PermissionTheme.InputH;
        const int btnH = PermissionTheme.BtnH;

        const int yStatus = 4;
        const int yColTitle = 28;
        const int yTopRow = 52;
        const int yLists = 90;
        const int listH = 250; // shorter lists → room for big bottom panel
        const int yDetail = 358;
        const int detailH = 150; // larger description + actions area
        const int contentH = yDetail + detailH + 8; // ~516

        // Inside detail panel:
        //  +8  description (~70px)
        //  +80 player hint (~22px)
        //  +108 action buttons
        const int detailTextY = yDetail + 10;
        const int detailTextH = 68;
        const int detailHintY = yDetail + 82;
        const int detailBtnY = yDetail + 108;

        roleListBounds = ElementBounds.Fixed(leftX, yLists, leftW, listH);
        roleClipBounds = roleListBounds.ForkContainingChild(2, 2, 2, 2);
        var roleTable = roleClipBounds.ForkContainingChild(0, 0, 0, 0);

        // Right: claim row → filter → priv list (ends with left list)
        const int claimRowH = 28;
        const int filterY = yLists + claimRowH + 8;
        const int privListY = filterY + inputH + 8;
        const int privListH = yLists + listH - privListY;

        privListBounds = ElementBounds.Fixed(rightX, privListY, rightW, privListH);
        privilegeClipBounds = privListBounds.ForkContainingChild(2, 2, 2, 2);
        var privTable = privilegeClipBounds.ForkContainingChild(0, 0, 0, 0);

        var detailBounds = ElementBounds.Fixed(pad, yDetail, PermissionTheme.UiW - pad * 2, detailH);

        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        var mainBounds = ElementBounds.Fixed(0, 0, PermissionTheme.UiW, contentH);
        bgBounds.WithChildren(mainBounds);

        var deleteLabel = deleteConfirmArmed
            ? Lang.Get("swixypermissionmanager:btn-delete-confirm")
            : Lang.Get("swixypermissionmanager:btn-delete-role");
        var diffLabel = pickCompareRole
            ? Lang.Get("swixypermissionmanager:btn-diff-picking")
            : string.IsNullOrEmpty(compareRoleCode)
                ? Lang.Get("swixypermissionmanager:btn-diff")
                : Lang.Get("swixypermissionmanager:btn-diff-clear");

        var roleTitle = role == null
            ? Lang.Get("swixypermissionmanager:select-role")
            : $"{role.Name}  ({role.Code})";

        var privDetail = BuildPrivilegeDetailText(role, privileges, filtered, granted);
        var playerHint = BuildPlayersHint(role);

        var titleFont = CairoFont.WhiteSmallishText().WithColor(PermissionTheme.ColText);
        var mutedFont = CairoFont.WhiteDetailText().WithColor(PermissionTheme.ColTextMuted);
        var inputFont = CairoFont.TextInput().WithColor(PermissionTheme.ColText);

        ClearComposers();
        var composer = capi.Gui
            .CreateCompo("swixypermissionmanager-dialog", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("swixypermissionmanager:dialog-title"), () => TryClose())
            .BeginChildElements(bgBounds)
            // Column header strips (icons + labels)
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(leftX, yColTitle - 2, leftW, 24),
                (ctx, s, b) => DrawSectionHeader(ctx, b, isRoles: true, Lang.Get("swixypermissionmanager:roles-label")),
                "rolesHeader")
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(rightX, yColTitle - 2, rightW, 24),
                (ctx, s, b) => DrawSectionHeader(ctx, b, isRoles: false, roleTitle),
                "privHeader")
            // Status
            .AddDynamicText(
                statusMessage,
                CairoFont.WhiteSmallText().WithColor(statusType == 0
                    ? PermissionTheme.ColOk
                    : PermissionTheme.ColDanger),
                ElementBounds.Fixed(pad, yStatus, PermissionTheme.UiW - pad * 2, 20),
                "statusText")
            // Left top: create role
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(leftX, yTopRow, leftW - 88, inputH),
                DrawInputBackground, "newRoleBg")
            .AddTextInput(
                ElementBounds.Fixed(leftX + 4, yTopRow + 3, leftW - 96, inputH - 6),
                v => newRoleInput = v ?? "",
                inputFont,
                "newRoleInput")
            .AddSmallButton(
                Lang.Get("swixypermissionmanager:btn-create"),
                OnCreateRole,
                ElementBounds.Fixed(leftX + leftW - 84, yTopRow, 84, btnH))
            // Right top: rename + assign
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(rightX, yTopRow, 160, inputH),
                DrawInputBackground, "renameBg")
            .AddTextInput(
                ElementBounds.Fixed(rightX + 4, yTopRow + 3, 152, inputH - 6),
                v => renameInput = v ?? "",
                inputFont,
                "renameInput")
            .AddSmallButton(
                Lang.Get("swixypermissionmanager:btn-rename"),
                OnRenameRole,
                ElementBounds.Fixed(rightX + 166, yTopRow, 84, btnH))
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(rightX + 260, yTopRow, 160, inputH),
                DrawInputBackground, "playerBg")
            .AddTextInput(
                ElementBounds.Fixed(rightX + 264, yTopRow + 3, 152, inputH - 6),
                OnPlayerInputChanged,
                inputFont,
                "playerInput")
            .AddSmallButton(
                Lang.Get("swixypermissionmanager:btn-assign"),
                OnAssignPlayer,
                ElementBounds.Fixed(rightX + rightW - 84, yTopRow, 84, btnH))
            // Role list well
            .AddDynamicCustomDraw(roleListBounds, DrawListWell, "roleWell")
            .AddVerticalScrollbar(
                OnRoleScroll,
                ElementStdBounds.VerticalScrollbar(roleListBounds),
                "roleScroll")
            .BeginClip(roleClipBounds)
            .AddCellList(roleTable, CreateRoleCell, BuildRoleCells(roles), "roleList")
            .EndClip()
            // Claim fields strip
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(rightX, yLists - 2, rightW, inputH + 4),
                DrawClaimStripBackground, "claimStrip")
            .AddStaticText("Lv", mutedFont,
                ElementBounds.Fixed(rightX + 6, yLists + 5, 22, 18))
            .AddTextInput(ElementBounds.Fixed(rightX + 28, yLists + 2, 44, inputH - 2),
                v => levelInput = v ?? "", inputFont, "levelInput")
            .AddStaticText("ch", mutedFont,
                ElementBounds.Fixed(rightX + 78, yLists + 5, 22, 18))
            .AddTextInput(ElementBounds.Fixed(rightX + 100, yLists + 2, 72, inputH - 2),
                v => allowanceInput = v ?? "", inputFont, "allowanceInput")
            .AddStaticText("N", mutedFont,
                ElementBounds.Fixed(rightX + 178, yLists + 5, 14, 18))
            .AddTextInput(ElementBounds.Fixed(rightX + 192, yLists + 2, 40, inputH - 2),
                v => maxAreasInput = v ?? "", inputFont, "maxAreasInput")
            .AddStaticText("min", mutedFont,
                ElementBounds.Fixed(rightX + 238, yLists + 5, 26, 18))
            .AddTextInput(ElementBounds.Fixed(rightX + 266, yLists + 2, 36, inputH - 2),
                v => minXInput = v ?? "", inputFont, "minXInput")
            .AddTextInput(ElementBounds.Fixed(rightX + 306, yLists + 2, 36, inputH - 2),
                v => minYInput = v ?? "", inputFont, "minYInput")
            .AddTextInput(ElementBounds.Fixed(rightX + 346, yLists + 2, 36, inputH - 2),
                v => minZInput = v ?? "", inputFont, "minZInput")
            .AddSmallButton(
                Lang.Get("swixypermissionmanager:btn-apply-claim"),
                OnApplyClaimSettings,
                ElementBounds.Fixed(rightX + rightW - 100, yLists, 100, btnH))
            // Filter
            .AddDynamicCustomDraw(
                ElementBounds.Fixed(rightX, filterY, rightW - 92, inputH),
                DrawInputBackground, "filterBg")
            .AddTextInput(
                ElementBounds.Fixed(rightX + 4, filterY + 3, rightW - 100, inputH - 6),
                OnFilterChanged,
                inputFont,
                "filterInput")
            .AddSmallButton(
                Lang.Get("swixypermissionmanager:btn-filter-clear"),
                () =>
                {
                    privilegeFilter = "";
                    privilegeScrollValue = 0;
                    ComposeDialog();
                    return true;
                },
                ElementBounds.Fixed(rightX + rightW - 88, filterY, 88, btnH))
            // Privilege list well
            .AddDynamicCustomDraw(privListBounds, DrawListWell, "privWell")
            .AddVerticalScrollbar(
                OnPrivilegeScroll,
                ElementStdBounds.VerticalScrollbar(privListBounds),
                "privScroll")
            .BeginClip(privilegeClipBounds)
            .AddCellList(
                privTable,
                CreatePrivilegeCell,
                BuildPrivilegeCells(filtered, granted),
                "privList")
            .EndClip()
            // Bottom panel: dark blue + icons + description + actions
            .AddDynamicCustomDraw(detailBounds, DrawDetailPanelBackground, "detailPanelBg")
            .AddDynamicText(
                privDetail,
                CairoFont.WhiteSmallText().WithColor(PermissionTheme.ColDetailText),
                ElementBounds.Fixed(pad + 10, detailTextY, PermissionTheme.UiW - pad * 2 - 20, detailTextH),
                "privDetailText")
            .AddDynamicText(
                playerHint,
                CairoFont.WhiteDetailText().WithColor(PermissionTheme.ColTextMuted),
                ElementBounds.Fixed(pad + 10, detailHintY, PermissionTheme.UiW - pad * 2 - 20, 20),
                "playersHint")
            .AddSmallButton(Lang.Get("swixypermissionmanager:btn-grant"), OnGrantSelectedPrivilege,
                ElementBounds.Fixed(pad + 8, detailBtnY, 90, btnH))
            .AddSmallButton(Lang.Get("swixypermissionmanager:btn-revoke"), OnRevokeSelectedPrivilege,
                ElementBounds.Fixed(pad + 104, detailBtnY, 90, btnH))
            .AddSmallButton(Lang.Get("swixypermissionmanager:btn-clone"), OnCloneRole,
                ElementBounds.Fixed(pad + 200, detailBtnY, 80, btnH))
            .AddSmallButton(deleteLabel, OnDeleteRole,
                ElementBounds.Fixed(pad + 286, detailBtnY, 110, btnH))
            .AddSmallButton(diffLabel, OnDiffButton,
                ElementBounds.Fixed(pad + 402, detailBtnY, 110, btnH))
            .AddSmallButton(Lang.Get("swixypermissionmanager:btn-refresh"), () =>
                {
                    RequestRefresh();
                    return true;
                },
                ElementBounds.Fixed(pad + 518, detailBtnY, 90, btnH))
            .AddSmallButton(Lang.Get("swixypermissionmanager:btn-close"), () =>
                {
                    TryClose();
                    return true;
                },
                ElementBounds.Fixed(pad + 614, detailBtnY, 90, btnH))
            .EndChildElements()
            .Compose();

        SingleComposer = composer;

        SingleComposer.GetTextInput("newRoleInput")?.SetValue(newRoleInput, true);
        SingleComposer.GetTextInput("filterInput")?.SetValue(privilegeFilter, true);
        SingleComposer.GetTextInput("playerInput")?.SetValue(playerInput, true);
        SingleComposer.GetTextInput("renameInput")?.SetValue(
            string.IsNullOrEmpty(renameInput) ? (role?.Name ?? "") : renameInput, true);
        SingleComposer.GetTextInput("levelInput")?.SetValue(levelInput, true);
        SingleComposer.GetTextInput("allowanceInput")?.SetValue(allowanceInput, true);
        SingleComposer.GetTextInput("maxAreasInput")?.SetValue(maxAreasInput, true);
        SingleComposer.GetTextInput("minXInput")?.SetValue(minXInput, true);
        SingleComposer.GetTextInput("minYInput")?.SetValue(minYInput, true);
        SingleComposer.GetTextInput("minZInput")?.SetValue(minZInput, true);

        try
        {
            var rl = SingleComposer.GetCellList<SavegameCellEntry>("roleList");
            if (rl != null)
            {
                rl.unscaledCellSpacing = 4;
            }

            var pl = SingleComposer.GetCellList<SavegameCellEntry>("privList");
            if (pl != null)
            {
                pl.unscaledCellSpacing = 3;
            }
        }
        catch
        {
            // ignore
        }

        RestoreRoleScroll(roleScrollValue);
        RestorePrivilegeScroll(privilegeScrollValue);
        ScheduleScrollRestore();
    }

    private void CaptureScrollPositions()
    {
        try
        {
            var roleScroll = SingleComposer?.GetScrollbar("roleScroll");
            if (roleScroll != null)
            {
                roleScrollValue = roleScroll.CurrentYPosition;
            }

            var privScroll = SingleComposer?.GetScrollbar("privScroll");
            if (privScroll != null)
            {
                privilegeScrollValue = privScroll.CurrentYPosition;
            }

            // Backup: offset списка (если scrollbar рассинхронизирован).
            var roleList = SingleComposer?.GetCellList<SavegameCellEntry>("roleList");
            if (roleList != null && roleList.Bounds.fixedY < 0)
            {
                roleScrollValue = Math.Max(roleScrollValue, (float)(-roleList.Bounds.fixedY));
            }

            var privList = SingleComposer?.GetCellList<SavegameCellEntry>("privList");
            if (privList != null && privList.Bounds.fixedY < 0)
            {
                privilegeScrollValue = Math.Max(privilegeScrollValue, (float)(-privList.Bounds.fixedY));
            }
        }
        catch
        {
            // composer may be mid-dispose
        }
    }

    private List<PrivilegeInfoPacket> GetFilteredPrivileges(List<PrivilegeInfoPacket>? privileges = null)
    {
        privileges ??= state?.Privileges ?? [];
        return privileges
            .Where(p =>
                string.IsNullOrWhiteSpace(privilegeFilter)
                || p.Code.Contains(privilegeFilter, StringComparison.OrdinalIgnoreCase)
                || (p.Title?.Contains(privilegeFilter, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.Description?.Contains(privilegeFilter, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }

    private List<SavegameCellEntry> BuildRoleCells(List<RolePacket> roles)
    {
        return roles.Select(r =>
        {
            var mark = r.Code == selectedRoleCode ? "► " : "";
            if (r.Code == compareRoleCode)
            {
                mark = r.Code == selectedRoleCode ? "►B " : "B ";
            }

            return new SavegameCellEntry
            {
                Title = mark + (r.Name ?? r.Code),
                DetailText = $"{r.Code} · lv {r.PrivilegeLevel} · {r.MemberCount}p",
                Selected = r.Code == selectedRoleCode || r.Code == compareRoleCode,
                Enabled = true,
            };
        }).ToList();
    }

    private List<SavegameCellEntry> BuildPrivilegeCells(List<PrivilegeInfoPacket> list, HashSet<string> granted)
    {
        var bSet = CompareRole == null
            ? null
            : new HashSet<string>(CompareRole.Privileges ?? [], StringComparer.OrdinalIgnoreCase);

        return list.Select(p =>
        {
            var detail = p.Title ?? "";
            if (bSet != null)
            {
                var inA = granted.Contains(p.Code);
                var inB = bSet.Contains(p.Code);
                detail = (inA, inB) switch
                {
                    (true, true) => "A+B · " + detail,
                    (true, false) => "A · " + detail,
                    (false, true) => "B · " + detail,
                    _ => "— · " + detail,
                };
            }

            return new SavegameCellEntry
            {
                Title = p.Code,
                DetailText = detail,
                Selected = p.Code == selectedPrivilegeCode,
                Enabled = true,
            };
        }).ToList();
    }

    private IGuiElementCell CreateRoleCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        var codeKey = "";
        if (!string.IsNullOrEmpty(cell.DetailText))
        {
            codeKey = cell.DetailText.Split(['·'], 2)[0].Trim();
        }

        return new PermissionRoleListCell(clientApi, cell, bounds)
        {
            OnSelect = () =>
            {
                if (string.IsNullOrEmpty(codeKey))
                {
                    return;
                }

                // NEVER ComposeDialog/ReloadCells inside CellList mouse handler —
                // GuiElementCellList enumerates cells → "Collection was modified" crash.
                if (pickCompareRole)
                {
                    CaptureScrollPositions();
                    var a = selectedRoleCode;
                    compareRoleCode = string.Equals(codeKey, a, StringComparison.OrdinalIgnoreCase)
                        ? ""
                        : codeKey;
                    pickCompareRole = false;
                    deleteConfirmArmed = false;
                    DeferUi(() =>
                    {
                        SetStatus(
                            string.IsNullOrEmpty(compareRoleCode)
                                ? Lang.Get("swixypermissionmanager:diff-cleared")
                                : Lang.Get("swixypermissionmanager:diff-ready", a, compareRoleCode),
                            0);
                        // In-place: keep left scroll (ComposeDialog сбрасывал его).
                        RefreshRoleSelectionInPlace(resetPrivilegeScroll: false);
                    });
                    return;
                }

                if (string.Equals(selectedRoleCode, codeKey, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                CaptureScrollPositions();
                selectedRoleCode = codeKey;
                deleteConfirmArmed = false;
                SyncClaimInputsFromRole(state?.Roles?.FirstOrDefault(r => r.Code == codeKey));
                // Как справа: без ComposeDialog — скролл ролей не прыгает.
                DeferUi(() => RefreshRoleSelectionInPlace(resetPrivilegeScroll: true));
            },
        };
    }

    /// <summary>
    /// Фабрика ячеек прав. Granted считается по ТЕКУЩЕЙ выбранной роли
    /// (нельзя замыкать HashSet из ComposeDialog — ReloadCells тогда не обновляет галочки).
    /// </summary>
    private IGuiElementCell CreatePrivilegeCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        var code = cell.Title ?? "";
        var isGranted = IsPrivilegeOnSelectedRole(code);
        return new PermissionPrivilegeListCell(clientApi, cell, bounds)
        {
            Granted = isGranted,
            // ЛКМ — только выбор
            OnSelect = () =>
            {
                CaptureScrollPositions();
                selectedPrivilegeCode = code;
                var role = SelectedRole;
                var g = CurrentGrantedSet();
                var filtered = GetFilteredPrivileges();
                var detail = BuildPrivilegeDetailText(role, state?.Privileges ?? [], filtered, g);
                SingleComposer?.GetDynamicText("privDetailText")?.SetNewText(detail);
                DeferUi(RefreshPrivilegeListInPlace);
            },
            // ПКМ — выдать, ещё раз ПКМ — забрать
            OnToggleGrant = () =>
            {
                CaptureScrollPositions();
                selectedPrivilegeCode = code;
                DeferUi(() => TogglePrivilegeOnSelectedRole(code));
            },
        };
    }

    private bool IsPrivilegeOnSelectedRole(string code)
    {
        var role = SelectedRole;
        if (role?.Privileges == null || string.IsNullOrEmpty(code))
        {
            return false;
        }

        return role.Privileges.Any(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase));
    }

    private HashSet<string> CurrentGrantedSet() =>
        new(SelectedRole?.Privileges ?? [], StringComparer.OrdinalIgnoreCase);

    /// <summary>ПКМ по праву: grant если нет, revoke если есть.</summary>
    private void TogglePrivilegeOnSelectedRole(string privilegeCode)
    {
        if (string.IsNullOrEmpty(selectedRoleCode))
        {
            SetStatus(Lang.Get("swixypermissionmanager:select-role"), 1);
            return;
        }

        if (string.IsNullOrEmpty(privilegeCode))
        {
            SetStatus(Lang.Get("swixypermissionmanager:error-select-privilege"), 1);
            return;
        }

        CaptureScrollPositions();
        var savedPrivScroll = privilegeScrollValue;
        var savedRoleScroll = roleScrollValue;

        var role = SelectedRole;
        var has = role?.Privileges?.Any(p =>
            string.Equals(p, privilegeCode, StringComparison.OrdinalIgnoreCase)) == true;

        // Optimistic UI: обновить локальный state, чтобы список мигнул сразу без ComposeDialog.
        if (role != null)
        {
            role.Privileges ??= [];
            if (has)
            {
                role.Privileges.RemoveAll(p =>
                    string.Equals(p, privilegeCode, StringComparison.OrdinalIgnoreCase));
            }
            else if (!role.Privileges.Any(p =>
                         string.Equals(p, privilegeCode, StringComparison.OrdinalIgnoreCase)))
            {
                role.Privileges.Add(privilegeCode);
            }
        }

        if (has)
        {
            SetStatus(Lang.Get("swixypermissionmanager:message-privilege-revoked", privilegeCode, selectedRoleCode), 0);
            channel.SendPacket(new PermissionActionPacket
            {
                Action = PermissionActionType.RevokePrivilege,
                RoleCode = selectedRoleCode,
                TextValue = privilegeCode,
            });
            clientApi.Logger.Notification(
                "[SwixyPermissionManager] → RMB Revoke role={0} priv={1}", selectedRoleCode, privilegeCode);
        }
        else
        {
            SetStatus(Lang.Get("swixypermissionmanager:message-privilege-granted", privilegeCode, selectedRoleCode), 0);
            channel.SendPacket(new PermissionActionPacket
            {
                Action = PermissionActionType.GrantPrivilege,
                RoleCode = selectedRoleCode,
                TextValue = privilegeCode,
            });
            clientApi.Logger.Notification(
                "[SwixyPermissionManager] → RMB Grant role={0} priv={1}", selectedRoleCode, privilegeCode);
        }

        // Перерисовать галочки, скролл оставить.
        privilegeScrollValue = savedPrivScroll;
        roleScrollValue = savedRoleScroll;
        RefreshPrivilegeListInPlacePreserving(savedRoleScroll, savedPrivScroll);
    }

    private void RefreshPrivilegeListInPlacePreserving(float savedRole, float savedPriv)
    {
        if (SingleComposer == null)
        {
            return;
        }

        var role = SelectedRole;
        var granted = CurrentGrantedSet();
        var filtered = GetFilteredPrivileges();

        try
        {
            var privList = SingleComposer.GetCellList<SavegameCellEntry>("privList");
            privList?.ReloadCells(BuildPrivilegeCells(filtered, granted));
        }
        catch (Exception ex)
        {
            clientApi.Logger.Warning("[SwixyPermissionManager] ReloadCells after toggle: {0}", ex.Message);
            roleScrollValue = savedRole;
            privilegeScrollValue = savedPriv;
            ComposeDialog();
            return;
        }

        var detail = BuildPrivilegeDetailText(role, state?.Privileges ?? [], filtered, granted);
        SingleComposer.GetDynamicText("privDetailText")?.SetNewText(detail);
        SingleComposer.GetDynamicText("playersHint")?.SetNewText(BuildPlayersHint(role));

        privilegeScrollValue = savedPriv;
        roleScrollValue = savedRole;
        RestorePrivilegeScroll(savedPriv);
        RestoreRoleScroll(savedRole);
        ScheduleScrollRestore();
    }

    /// <summary>
    /// Отложить UI-мутацию списка: нельзя ReloadCells/Compose во время OnMouseUp CellList.
    /// </summary>
    private void DeferUi(Action action)
    {
        if (clientApi == null)
        {
            return;
        }

        clientApi.Event.RegisterCallback(_ =>
        {
            if (!IsOpened())
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                clientApi.Logger.Error("[SwixyPermissionManager] Deferred UI failed: {0}", ex);
            }
        }, 1);
    }

    /// <summary>
    /// Обновить выделение/описания привилегий без полной пересборки диалога (скролл не прыгает).
    /// Вызывать только ВНЕ обработчика мыши CellList.
    /// </summary>
    private void RefreshPrivilegeListInPlace()
    {
        if (SingleComposer == null)
        {
            return;
        }

        CaptureScrollPositions();
        var savedPriv = privilegeScrollValue;
        var savedRole = roleScrollValue;
        var role = SelectedRole;
        var granted = CurrentGrantedSet();
        var filtered = GetFilteredPrivileges();

        try
        {
            var privList = SingleComposer.GetCellList<SavegameCellEntry>("privList");
            // ReloadCells вызывает CreatePrivilegeCell заново → Granted из SelectedRole.
            privList?.ReloadCells(BuildPrivilegeCells(filtered, granted));
        }
        catch (Exception ex)
        {
            clientApi.Logger.Warning("[SwixyPermissionManager] ReloadCells failed, full compose: {0}", ex.Message);
            ComposeDialog();
            return;
        }

        var detail = BuildPrivilegeDetailText(role, state?.Privileges ?? [], filtered, granted);
        SingleComposer.GetDynamicText("privDetailText")?.SetNewText(detail);
        SingleComposer.GetDynamicText("playersHint")?.SetNewText(BuildPlayersHint(role));

        RestorePrivilegeScroll(savedPriv);
        RestoreRoleScroll(savedRole);
        ScheduleScrollRestore();
    }

    /// <summary>
    /// Смена выбранной роли без ComposeDialog: ReloadCells + поля ввода.
    /// Скролл списка ролей сохраняется; список прав пересобирается под новую роль.
    /// </summary>
    private void RefreshRoleSelectionInPlace(bool resetPrivilegeScroll)
    {
        if (SingleComposer == null)
        {
            ComposeDialog();
            return;
        }

        CaptureScrollPositions();
        var savedRole = roleScrollValue;
        var savedPriv = resetPrivilegeScroll ? 0f : privilegeScrollValue;
        if (resetPrivilegeScroll)
        {
            privilegeScrollValue = 0;
        }

        // selectedRoleCode already set by caller — SelectedRole is the new role.
        var role = SelectedRole;
        var granted = CurrentGrantedSet();
        var filtered = GetFilteredPrivileges();

        try
        {
            var roleList = SingleComposer.GetCellList<SavegameCellEntry>("roleList");
            roleList?.ReloadCells(BuildRoleCells(state?.Roles ?? []));

            var privList = SingleComposer.GetCellList<SavegameCellEntry>("privList");
            // Critical: rebuild privilege rows so checkmarks match the newly selected role.
            privList?.ReloadCells(BuildPrivilegeCells(filtered, granted));
        }
        catch (Exception ex)
        {
            clientApi.Logger.Warning("[SwixyPermissionManager] Role ReloadCells failed: {0}", ex.Message);
            roleScrollValue = savedRole;
            privilegeScrollValue = savedPriv;
            ComposeDialog();
            return;
        }

        // Claim / rename fields
        if (role != null)
        {
            SingleComposer.GetTextInput("renameInput")?.SetValue(role.Name ?? "", true);
            SingleComposer.GetTextInput("levelInput")?.SetValue(levelInput, true);
            SingleComposer.GetTextInput("allowanceInput")?.SetValue(allowanceInput, true);
            SingleComposer.GetTextInput("maxAreasInput")?.SetValue(maxAreasInput, true);
            SingleComposer.GetTextInput("minXInput")?.SetValue(minXInput, true);
            SingleComposer.GetTextInput("minYInput")?.SetValue(minYInput, true);
            SingleComposer.GetTextInput("minZInput")?.SetValue(minZInput, true);
        }

        var detail = BuildPrivilegeDetailText(role, state?.Privileges ?? [], filtered, granted);
        SingleComposer.GetDynamicText("privDetailText")?.SetNewText(detail);
        SingleComposer.GetDynamicText("playersHint")?.SetNewText(BuildPlayersHint(role));
        if (!string.IsNullOrEmpty(statusMessage))
        {
            SingleComposer.GetDynamicText("statusText")?.SetNewText(statusMessage);
        }

        RestoreRoleScroll(savedRole);
        RestorePrivilegeScroll(savedPriv);
        ScheduleScrollRestore();
    }

    private string BuildPrivilegeDetailText(
        RolePacket? role,
        List<PrivilegeInfoPacket> privileges,
        List<PrivilegeInfoPacket> filtered,
        HashSet<string> granted)
    {
        if (!string.IsNullOrEmpty(compareRoleCode) && CompareRole != null && role != null)
        {
            var a = new HashSet<string>(role.Privileges ?? [], StringComparer.OrdinalIgnoreCase);
            var b = new HashSet<string>(CompareRole.Privileges ?? [], StringComparer.OrdinalIgnoreCase);
            var onlyA = a.Except(b, StringComparer.OrdinalIgnoreCase).Count();
            var onlyB = b.Except(a, StringComparer.OrdinalIgnoreCase).Count();
            var both = a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count();
            var header = Lang.Get("swixypermissionmanager:diff-header", role.Code, CompareRole.Code)
                         + "  "
                         + Lang.Get("swixypermissionmanager:diff-summary", onlyA, onlyB, both);
            if (!string.IsNullOrEmpty(selectedPrivilegeCode))
            {
                var p = privileges.FirstOrDefault(x => x.Code == selectedPrivilegeCode);
                if (p != null)
                {
                    return header + "  |  " + p.Title + ": " + p.Description;
                }
            }

            return header;
        }

        var privInfo = privileges.FirstOrDefault(p => p.Code == selectedPrivilegeCode)
                       ?? filtered.FirstOrDefault();
        if (privInfo == null)
        {
            return Lang.Get("swixypermissionmanager:priv-hint");
        }

        var onRole = role != null && granted.Contains(privInfo.Code);
        var flag = onRole
            ? Lang.Get("swixypermissionmanager:priv-on-role")
            : Lang.Get("swixypermissionmanager:priv-off-role");
        return $"{privInfo.Title} ({privInfo.Code}) — {flag}. {privInfo.Description}  [{Lang.Get("swixypermissionmanager:priv-mouse-hint")}]";
    }

    private string BuildPlayersHint(RolePacket? role)
    {
        var effective = BuildEffectivePrivilegesHint();
        if (!string.IsNullOrEmpty(effective))
        {
            return effective;
        }

        if (state?.Players == null || state.Players.Count == 0)
        {
            return Lang.Get("swixypermissionmanager:no-players");
        }

        if (role != null)
        {
            var ofRole = state.Players
                .Where(p => string.Equals(p.RoleCode, role.Code, StringComparison.OrdinalIgnoreCase))
                .Take(8)
                .ToList();
            if (ofRole.Count > 0)
            {
                return Lang.Get("swixypermissionmanager:players-in-role") + " "
                       + string.Join(", ", ofRole.Select(p => p.Online ? p.Name : p.Name + "*"));
            }
        }

        return Lang.Get("swixypermissionmanager:players-sample") + " "
               + string.Join(", ", state.Players.Take(6).Select(p =>
                   $"{p.Name}:{(string.IsNullOrEmpty(p.RoleCode) ? "?" : p.RoleCode)}"));
    }

    private string BuildEffectivePrivilegesHint()
    {
        var name = (SingleComposer?.GetTextInput("playerInput")?.GetText() ?? playerInput).Trim();
        if (string.IsNullOrEmpty(name) || state?.Players == null)
        {
            return "";
        }

        var player = state.Players.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Uid, name, StringComparison.OrdinalIgnoreCase));
        if (player == null)
        {
            return "";
        }

        var pRole = state.Roles?.FirstOrDefault(r =>
            string.Equals(r.Code, player.RoleCode, StringComparison.OrdinalIgnoreCase));
        if (pRole == null)
        {
            return Lang.Get("swixypermissionmanager:effective-no-role", player.Name, player.RoleCode ?? "?");
        }

        var codes = (pRole.Privileges ?? []).OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        return Lang.Get("swixypermissionmanager:effective-header", player.Name, pRole.Name, pRole.Code, codes.Count)
               + " " + string.Join(", ", codes.Take(10));
    }

    private void DrawDetailPanelBackground(Context ctx, ImageSurface surface, ElementBounds currentBounds)
    {
        var w = currentBounds.OuterWidth;
        var h = currentBounds.OuterHeight;
        var c = PermissionTheme.ColDetailPanel;
        var b = PermissionTheme.ColDetailBorder;

        ctx.SetSourceRGBA(c[0], c[1], c[2], c[3]);
        RoundRectPath(ctx, 1, 1, w - 2, h - 2, 8);
        ctx.Fill();

        ctx.SetSourceRGBA(b[0], b[1], b[2], b[3]);
        ctx.LineWidth = 1.5;
        RoundRectPath(ctx, 1.5, 1.5, w - 3, h - 3, 8);
        ctx.Stroke();

        // Top accent line
        var a = PermissionTheme.ColAccent;
        ctx.SetSourceRGBA(a[0], a[1], a[2], 0.45);
        ctx.Rectangle(8, 3, w - 16, 2);
        ctx.Fill();

        // Key icon near description
        PermissionCairoIcons.DrawKey(ctx, 10, 12, 18, true);

        // Action icons above button row (aligned with buttons)
        var iconY = h - 38;
        var icon = 16.0;
        PermissionCairoIcons.DrawPlus(ctx, 28, iconY, icon);
        PermissionCairoIcons.DrawMinus(ctx, 124, iconY, icon);
        PermissionCairoIcons.DrawClone(ctx, 218, iconY, icon);
        PermissionCairoIcons.DrawTrash(ctx, 316, iconY, icon);
        PermissionCairoIcons.DrawDiff(ctx, 432, iconY, icon);
        PermissionCairoIcons.DrawRefresh(ctx, 538, iconY, icon);
        PermissionCairoIcons.DrawClose(ctx, 634, iconY, icon);
    }

    private void DrawListWell(Context ctx, ImageSurface surface, ElementBounds currentBounds)
    {
        var w = currentBounds.OuterWidth;
        var h = currentBounds.OuterHeight;
        var well = PermissionTheme.ColWell;
        var border = PermissionTheme.ColBorder;

        ctx.SetSourceRGBA(well[0], well[1], well[2], well[3]);
        RoundRectPath(ctx, 0, 0, w, h, 6);
        ctx.Fill();

        ctx.SetSourceRGBA(border[0], border[1], border[2], 0.75);
        ctx.LineWidth = 1.2;
        RoundRectPath(ctx, 0.5, 0.5, w - 1, h - 1, 6);
        ctx.Stroke();
    }

    private void DrawInputBackground(Context ctx, ImageSurface surface, ElementBounds currentBounds)
    {
        var w = currentBounds.OuterWidth;
        var h = currentBounds.OuterHeight;
        var fill = PermissionTheme.ColInput;
        var border = PermissionTheme.ColBorder;

        ctx.SetSourceRGBA(fill[0], fill[1], fill[2], fill[3]);
        RoundRectPath(ctx, 0, 0, w, h, 4);
        ctx.Fill();

        ctx.SetSourceRGBA(border[0], border[1], border[2], 0.7);
        ctx.LineWidth = 1;
        RoundRectPath(ctx, 0.5, 0.5, w - 1, h - 1, 4);
        ctx.Stroke();
    }

    private void DrawClaimStripBackground(Context ctx, ImageSurface surface, ElementBounds currentBounds)
    {
        var w = currentBounds.OuterWidth;
        var h = currentBounds.OuterHeight;
        ctx.SetSourceRGBA(0.10, 0.14, 0.22, 0.9);
        RoundRectPath(ctx, 0, 0, w, h, 5);
        ctx.Fill();
        var border = PermissionTheme.ColBorder;
        ctx.SetSourceRGBA(border[0], border[1], border[2], 0.55);
        ctx.LineWidth = 1;
        RoundRectPath(ctx, 0.5, 0.5, w - 1, h - 1, 5);
        ctx.Stroke();
        PermissionCairoIcons.DrawApply(ctx, w - 28, (h - 16) / 2, 16);
    }

    private void DrawSectionHeader(Context ctx, ElementBounds bounds, bool isRoles, string title)
    {
        var w = bounds.OuterWidth;
        var h = bounds.OuterHeight;
        // underline
        var accent = PermissionTheme.ColAccent;
        ctx.SetSourceRGBA(accent[0], accent[1], accent[2], 0.35);
        ctx.Rectangle(0, h - 2, w, 2);
        ctx.Fill();

        var icon = 18.0;
        if (isRoles)
        {
            PermissionCairoIcons.DrawShield(ctx, 0, (h - icon) / 2, icon, true);
        }
        else
        {
            PermissionCairoIcons.DrawKey(ctx, 0, (h - icon) / 2, icon, true);
        }

        ctx.SetSourceRGBA(
            PermissionTheme.ColText[0],
            PermissionTheme.ColText[1],
            PermissionTheme.ColText[2],
            1);
        ctx.SelectFontFace("sans-serif", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(13);
        ctx.MoveTo(icon + 6, h * 0.72);
        // Truncate long titles
        var t = title ?? "";
        if (t.Length > 48)
        {
            t = t[..48] + "…";
        }

        ctx.ShowText(t);
    }

    private static void RoundRectPath(Context ctx, double x, double y, double w, double h, double r)
    {
        ctx.NewPath();
        ctx.MoveTo(x + r, y);
        ctx.LineTo(x + w - r, y);
        ctx.CurveTo(x + w, y, x + w, y, x + w, y + r);
        ctx.LineTo(x + w, y + h - r);
        ctx.CurveTo(x + w, y + h, x + w, y + h, x + w - r, y + h);
        ctx.LineTo(x + r, y + h);
        ctx.CurveTo(x, y + h, x, y + h, x, y + h - r);
        ctx.LineTo(x, y + r);
        ctx.CurveTo(x, y, x, y, x + r, y);
        ctx.ClosePath();
    }

    private void OnRoleScroll(float value)
    {
        roleScrollValue = value;
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("roleList");
        if (cellList == null)
        {
            return;
        }

        cellList.Bounds.fixedY = -value;
        cellList.Bounds.CalcWorldBounds();
    }

    private void OnPrivilegeScroll(float value)
    {
        privilegeScrollValue = value;
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("privList");
        if (cellList == null)
        {
            return;
        }

        cellList.Bounds.fixedY = -value;
        cellList.Bounds.CalcWorldBounds();
    }

    /// <summary>
    /// Колёсико только у списка под курсором (иначе VS крутит «правый» scrollbar).
    /// </summary>
    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
        if (SingleComposer == null || !IsOpened())
        {
            base.OnMouseWheel(args);
            return;
        }

        var mx = clientApi.Input.MouseX;
        var my = clientApi.Input.MouseY;
        const float step = 42f;
        var delta = -args.delta * step;

        // Left roles under cursor
        if (IsMouseOverBounds(mx, my, roleListBounds)
            || IsMouseOverBounds(mx, my, roleClipBounds)
            || IsMouseOverScrollbar("roleScroll"))
        {
            ApplyWheelToList(
                "roleList",
                "roleScroll",
                roleClipBounds,
                roleScrollValue + delta,
                OnRoleScroll,
                v => roleScrollValue = v);
            args.SetHandled(true);
            return;
        }

        // Right privileges under cursor
        if (IsMouseOverBounds(mx, my, privListBounds)
            || IsMouseOverBounds(mx, my, privilegeClipBounds)
            || IsMouseOverScrollbar("privScroll"))
        {
            ApplyWheelToList(
                "privList",
                "privScroll",
                privilegeClipBounds,
                privilegeScrollValue + delta,
                OnPrivilegeScroll,
                v => privilegeScrollValue = v);
            args.SetHandled(true);
            return;
        }

        // Не над списками — не отдаём wheel «чужому» scrollbar'у (иначе всегда крутится правый).
        args.SetHandled(true);
    }

    private void ApplyWheelToList(
        string cellListName,
        string scrollbarName,
        ElementBounds? clipBounds,
        float nextValue,
        Action<float> applyScroll,
        Action<float> storeValue)
    {
        if (SingleComposer == null || clipBounds == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<SavegameCellEntry>(cellListName);
        if (cellList == null)
        {
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        clipBounds.CalcWorldBounds();

        var clipH = (float)clipBounds.fixedHeight;
        var tableH = (float)cellList.Bounds.fixedHeight;
        var maxScroll = Math.Max(0f, tableH - clipH);
        nextValue = Math.Clamp(nextValue, 0f, maxScroll);
        storeValue(nextValue);
        applyScroll(nextValue);

        var scroll = SingleComposer.GetScrollbar(scrollbarName);
        if (scroll != null)
        {
            scroll.SetHeights(clipH, Math.Max(clipH, tableH));
            scroll.CurrentYPosition = nextValue;
            scroll.RecomposeHandle();
        }
    }

    private bool IsMouseOverBounds(int mouseX, int mouseY, ElementBounds? bounds)
    {
        if (bounds == null || SingleComposer?.Bounds == null)
        {
            return false;
        }

        // Safe hit-test like ClaimMapDialog: dialog origin + fixed*scale (no renderX — can NRE).
        var s = Math.Max(0.01, RuntimeEnv.GUIScale);
        var pad = GuiStyle.ElementToDialogPadding * s;
        // Title bar offset roughly matches dialog chrome above child area.
        var titleH = GuiStyle.TitleBarHeight * s;

        var x = SingleComposer.Bounds.absX + pad + bounds.fixedX * s;
        var y = SingleComposer.Bounds.absY + pad + titleH + bounds.fixedY * s;
        var w = bounds.fixedWidth * s;
        var h = bounds.fixedHeight * s;

        // Also try abs* if parent chain filled them (more accurate when available).
        try
        {
            bounds.CalcWorldBounds();
            if (bounds.absX > 1 || bounds.absY > 1)
            {
                x = bounds.absX;
                y = bounds.absY;
                w = bounds.OuterWidth;
                h = bounds.OuterHeight;
            }
        }
        catch
        {
            // keep fallback
        }

        return mouseX >= x && mouseX <= x + w && mouseY >= y && mouseY <= y + h;
    }

    private bool IsMouseOverScrollbar(string name)
    {
        try
        {
            var scroll = SingleComposer?.GetScrollbar(name);
            if (scroll?.Bounds == null)
            {
                return false;
            }

            return IsMouseOverBounds(clientApi.Input.MouseX, clientApi.Input.MouseY, scroll.Bounds);
        }
        catch
        {
            return false;
        }
    }

    private void RestoreRoleScroll(float scrollValue)
    {
        if (SingleComposer == null || roleClipBounds == null)
        {
            roleScrollValue = scrollValue;
            return;
        }

        var cellList = SingleComposer.GetCellList<SavegameCellEntry>("roleList");
        if (cellList == null)
        {
            roleScrollValue = scrollValue;
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        roleClipBounds.CalcWorldBounds();
        var clipH = (float)roleClipBounds.fixedHeight;
        var tableH = (float)cellList.Bounds.fixedHeight;

        // Высота ещё 0 сразу после Compose — не затирать желаемый offset.
        if (tableH < 8f && scrollValue > 0.5f)
        {
            roleScrollValue = scrollValue;
            cellList.Bounds.fixedY = -scrollValue;
            cellList.Bounds.CalcWorldBounds();
            return;
        }

        var maxScroll = Math.Max(0f, tableH - clipH);
        roleScrollValue = Math.Clamp(scrollValue, 0f, maxScroll);
        var scroll = SingleComposer.GetScrollbar("roleScroll");
        if (scroll != null)
        {
            scroll.SetHeights(clipH, Math.Max(clipH, tableH));
            scroll.CurrentYPosition = roleScrollValue;
            scroll.RecomposeHandle();
        }

        cellList.Bounds.fixedY = -roleScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

    private void RestorePrivilegeScroll(float scrollValue)
    {
        if (SingleComposer == null || privilegeClipBounds == null)
        {
            privilegeScrollValue = scrollValue;
            return;
        }

        var cellList = SingleComposer.GetCellList<SavegameCellEntry>("privList");
        if (cellList == null)
        {
            privilegeScrollValue = scrollValue;
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        privilegeClipBounds.CalcWorldBounds();
        var clipH = (float)privilegeClipBounds.fixedHeight;
        var tableH = (float)cellList.Bounds.fixedHeight;

        if (tableH < 8f && scrollValue > 0.5f)
        {
            privilegeScrollValue = scrollValue;
            cellList.Bounds.fixedY = -scrollValue;
            cellList.Bounds.CalcWorldBounds();
            return;
        }

        var maxScroll = Math.Max(0f, tableH - clipH);
        privilegeScrollValue = Math.Clamp(scrollValue, 0f, maxScroll);
        var scroll = SingleComposer.GetScrollbar("privScroll");
        if (scroll != null)
        {
            scroll.SetHeights(clipH, Math.Max(clipH, tableH));
            scroll.CurrentYPosition = privilegeScrollValue;
            scroll.RecomposeHandle();
        }

        cellList.Bounds.fixedY = -privilegeScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

    /// <summary>Повторно применить скролл на следующем тике (после layout CellList).</summary>
    private void ScheduleScrollRestore()
    {
        if (scrollRestoreScheduled || clientApi == null)
        {
            return;
        }

        scrollRestoreScheduled = true;
        var roleSaved = roleScrollValue;
        var privSaved = privilegeScrollValue;
        clientApi.Event.RegisterCallback(_ =>
        {
            scrollRestoreScheduled = false;
            if (!IsOpened())
            {
                return;
            }

            RestoreRoleScroll(roleSaved);
            RestorePrivilegeScroll(privSaved);
        }, 1);
    }

    private void OnFilterChanged(string value)
    {
        var next = value ?? "";
        if (string.Equals(next, privilegeFilter, StringComparison.Ordinal))
        {
            return;
        }

        privilegeFilter = next;
        privilegeScrollValue = 0;
        ComposeDialog();
        SingleComposer?.GetTextInput("filterInput")?.SetValue(privilegeFilter, true);
    }

    private void OnPlayerInputChanged(string value)
    {
        playerInput = value ?? "";
        var hint = BuildPlayersHint(SelectedRole);
        SingleComposer?.GetDynamicText("playersHint")?.SetNewText(hint);
    }

    private bool OnCreateRole()
    {
        var name = SingleComposer?.GetTextInput("newRoleInput")?.GetText() ?? newRoleInput;
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus(Lang.Get("swixypermissionmanager:error-invalid-name"), 1);
            return true;
        }

        var trimmed = name.Trim();
        // Не показываем success заранее — ждём ActionResult/State с сервера
        // (иначе «создано», а список пуст при ошибке concrete Roles).
        SetStatus(Lang.Get("swixypermissionmanager:status-creating", trimmed), 0);
        channel.SendPacket(new PermissionActionPacket
        {
            Action = PermissionActionType.CreateRole,
            RoleCode = trimmed,
            TextValue = trimmed,
        });
        clientApi.Logger.Notification("[SwixyPermissionManager] → CreateRole '{0}'", trimmed);
        newRoleInput = "";
        SingleComposer?.GetTextInput("newRoleInput")?.SetValue("", true);
        return true;
    }

    private bool OnRenameRole()
    {
        if (string.IsNullOrEmpty(selectedRoleCode))
        {
            return true;
        }

        var name = SingleComposer?.GetTextInput("renameInput")?.GetText() ?? renameInput;
        channel.SendPacket(new PermissionActionPacket
        {
            Action = PermissionActionType.RenameRole,
            RoleCode = selectedRoleCode,
            TextValue = name,
        });
        return true;
    }

    private bool OnDeleteRole()
    {
        if (string.IsNullOrEmpty(selectedRoleCode))
        {
            return true;
        }

        if (!deleteConfirmArmed)
        {
            deleteConfirmArmed = true;
            SetStatus(Lang.Get("swixypermissionmanager:delete-confirm-hint", selectedRoleCode), 1);
            ComposeDialog();
            return true;
        }

        deleteConfirmArmed = false;
        channel.SendPacket(new PermissionActionPacket
        {
            Action = PermissionActionType.DeleteRole,
            RoleCode = selectedRoleCode,
        });
        return true;
    }

    private bool OnCloneRole()
    {
        if (string.IsNullOrEmpty(selectedRoleCode))
        {
            SetStatus(Lang.Get("swixypermissionmanager:select-role"), 1);
            return true;
        }

        var customName = SingleComposer?.GetTextInput("newRoleInput")?.GetText() ?? newRoleInput;
        channel.SendPacket(new PermissionActionPacket
        {
            Action = PermissionActionType.CloneRole,
            RoleCode = selectedRoleCode,
            TextValue = string.IsNullOrWhiteSpace(customName) ? "" : customName.Trim(),
        });
        return true;
    }

    private bool OnDiffButton()
    {
        if (!string.IsNullOrEmpty(compareRoleCode) && !pickCompareRole)
        {
            compareRoleCode = "";
            pickCompareRole = false;
            SetStatus(Lang.Get("swixypermissionmanager:diff-cleared"), 0);
            ComposeDialog();
            return true;
        }

        if (string.IsNullOrEmpty(selectedRoleCode))
        {
            SetStatus(Lang.Get("swixypermissionmanager:select-role"), 1);
            return true;
        }

        pickCompareRole = true;
        SetStatus(Lang.Get("swixypermissionmanager:diff-pick-hint"), 0);
        ComposeDialog();
        return true;
    }

    private bool OnGrantSelectedPrivilege()
    {
        var roleCode = selectedRoleCode;
        var privCode = selectedPrivilegeCode;
        if (string.IsNullOrEmpty(roleCode))
        {
            SetStatus(Lang.Get("swixypermissionmanager:select-role"), 1);
            return true;
        }

        if (string.IsNullOrEmpty(privCode))
        {
            SetStatus(Lang.Get("swixypermissionmanager:error-select-privilege"), 1);
            return true;
        }

        SetStatus(Lang.Get("swixypermissionmanager:message-privilege-granted", privCode, roleCode), 0);
        channel.SendPacket(new PermissionActionPacket
        {
            Action = PermissionActionType.GrantPrivilege,
            RoleCode = roleCode,
            TextValue = privCode,
        });
        clientApi.Logger.Notification(
            "[SwixyPermissionManager] → GrantPrivilege role={0} priv={1}", roleCode, privCode);
        return true;
    }

    private bool OnRevokeSelectedPrivilege()
    {
        var roleCode = selectedRoleCode;
        var privCode = selectedPrivilegeCode;
        if (string.IsNullOrEmpty(roleCode))
        {
            SetStatus(Lang.Get("swixypermissionmanager:select-role"), 1);
            return true;
        }

        if (string.IsNullOrEmpty(privCode))
        {
            SetStatus(Lang.Get("swixypermissionmanager:error-select-privilege"), 1);
            return true;
        }

        SetStatus(Lang.Get("swixypermissionmanager:message-privilege-revoked", privCode, roleCode), 0);
        channel.SendPacket(new PermissionActionPacket
        {
            Action = PermissionActionType.RevokePrivilege,
            RoleCode = roleCode,
            TextValue = privCode,
        });
        clientApi.Logger.Notification(
            "[SwixyPermissionManager] → RevokePrivilege role={0} priv={1}", roleCode, privCode);
        return true;
    }

    private bool OnAssignPlayer()
    {
        if (string.IsNullOrEmpty(selectedRoleCode))
        {
            SetStatus(Lang.Get("swixypermissionmanager:select-role"), 1);
            return true;
        }

        var player = SingleComposer?.GetTextInput("playerInput")?.GetText() ?? playerInput;
        if (string.IsNullOrWhiteSpace(player))
        {
            SetStatus(Lang.Get("swixypermissionmanager:error-player-name"), 1);
            return true;
        }

        channel.SendPacket(new PermissionActionPacket
        {
            Action = PermissionActionType.SetPlayerRole,
            RoleCode = selectedRoleCode,
            TextValue = player.Trim(),
        });
        return true;
    }

    private bool OnApplyClaimSettings()
    {
        if (string.IsNullOrEmpty(selectedRoleCode))
        {
            return true;
        }

        levelInput = SingleComposer?.GetTextInput("levelInput")?.GetText() ?? levelInput;
        allowanceInput = SingleComposer?.GetTextInput("allowanceInput")?.GetText() ?? allowanceInput;
        maxAreasInput = SingleComposer?.GetTextInput("maxAreasInput")?.GetText() ?? maxAreasInput;
        minXInput = SingleComposer?.GetTextInput("minXInput")?.GetText() ?? minXInput;
        minYInput = SingleComposer?.GetTextInput("minYInput")?.GetText() ?? minYInput;
        minZInput = SingleComposer?.GetTextInput("minZInput")?.GetText() ?? minZInput;

        if (!int.TryParse(levelInput.Trim(), out var level)
            || !int.TryParse(allowanceInput.Trim(), out var allowance)
            || !int.TryParse(maxAreasInput.Trim(), out var maxAreas)
            || !int.TryParse(minXInput.Trim(), out var minX)
            || !int.TryParse(minYInput.Trim(), out var minY)
            || !int.TryParse(minZInput.Trim(), out var minZ))
        {
            SetStatus(Lang.Get("swixypermissionmanager:error-invalid-numbers"), 1);
            return true;
        }

        channel.SendPacket(new PermissionActionPacket
        {
            Action = PermissionActionType.SetClaimSettings,
            RoleCode = selectedRoleCode,
            IntValue = level,
            IntValue2 = allowance,
            IntValue3 = maxAreas,
            TextValue = $"{minX},{minY},{minZ}",
        });
        return true;
    }
}
