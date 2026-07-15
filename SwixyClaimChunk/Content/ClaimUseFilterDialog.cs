// =============================================================================
// ClaimUseFilterDialog.cs
// -----------------------------------------------------------------------------
// Whitelist Use: список interactable-блоков из скана.
// Строка = иконка слева + название справа, вертикальный скролл.
// Без ItemSlotGrid / PassiveItemSlot — нет network-пакетов.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Диалог настройки whitelist блоков для права Use в привате.
/// </summary>
public sealed class ClaimUseFilterDialog : GuiDialog
{
    private const double ListWidth = 460;
    private const double ListHeight = 280;

    private readonly ICoreClientAPI clientApi;
    private readonly IClientNetworkChannel channel;
    private readonly int claimId;
    private readonly string claimName;
    private readonly Action? onClosed;

    private int draftMode;
    private readonly HashSet<string> draftCodes;
    private readonly HashSet<string> scannedCodes = new(StringComparer.OrdinalIgnoreCase);

    private List<(string Code, ItemStack Stack)> entries = [];

    private string statusMessage = "";
    private bool syncingModeSwitches;
    private bool scanPending;
    private float listScrollValue;

    private ElementBounds? listClipBounds;
    private ElementBounds? listTableBounds;
    private ElementBounds? listViewportBounds;

    public override string ToggleKeyCombinationCode => null!;

    public override bool PrefersUngrabbedMouse => true;

    public int ClaimId => claimId;

    public ClaimUseFilterDialog(
        ICoreClientAPI capi,
        IClientNetworkChannel channel,
        int claimId,
        string claimName,
        int useFilterMode,
        IEnumerable<string>? useFilterCodes,
        Action? onClosed = null)
        : base(capi)
    {
        clientApi = capi;
        this.channel = channel;
        this.claimId = claimId;
        this.claimName = claimName;
        this.onClosed = onClosed;
        draftMode = useFilterMode == ClaimUseFilterMode.Whitelist
            ? ClaimUseFilterMode.Whitelist
            : ClaimUseFilterMode.AllowAll;
        draftCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (useFilterCodes != null)
        {
            foreach (var code in useFilterCodes)
            {
                var normalized = NormalizeCode(code);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    draftCodes.Add(normalized);
                }
            }
        }

        statusMessage = Lang.Get("swixyclaimchunk:use-filter-scan-loading");
        scanPending = true;
        ComposeDialog();
        RequestClaimScan();
    }

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();
        onClosed?.Invoke();
    }

    public void ApplyScanResult(ClaimUseFilterScanResultPacket packet)
    {
        if (packet == null || packet.ClaimId != claimId)
        {
            return;
        }

        var savedScroll = listScrollValue;
        scanPending = false;
        scannedCodes.Clear();
        foreach (var code in ClaimUseFilterCodesCodec.Split(packet.CodesRaw))
        {
            var normalized = NormalizeCode(code);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                scannedCodes.Add(normalized);
            }
        }

        statusMessage = string.IsNullOrWhiteSpace(packet.Message)
            ? Lang.Get("swixyclaimchunk:use-filter-scan-ok", scannedCodes.Count)
            : packet.Message;

        ComposeDialog();
        RestoreListScroll(savedScroll);
    }

    private void RequestClaimScan()
    {
        try
        {
            scanPending = true;
            statusMessage = Lang.Get("swixyclaimchunk:use-filter-scan-loading");
            channel.SendPacket(new ClaimUseFilterScanRequestPacket { ClaimId = claimId });
            SingleComposer?.GetDynamicText("statusText")?.SetNewText(statusMessage);
        }
        catch (Exception exception)
        {
            scanPending = false;
            statusMessage = Lang.Get("swixyclaimchunk:error-send-request-failed");
            clientApi.Logger.Warning("[SwixyClaimChunk] Use filter scan request failed: {0}", exception.Message);
            SingleComposer?.GetDynamicText("statusText")?.SetNewText(statusMessage);
        }
    }

    private void ComposeDialog()
    {
        entries = CollectPickerEntries();

        var scrollW = GuiElementScrollbar.DefaultScrollbarWidth
            + GuiElementScrollbar.DeafultScrollbarPadding * 2;
        var contentW = ListWidth + 6 + scrollW;

        var mainBounds = ElementBounds.Fixed(0, 0, Math.Max(500, contentW + 36), 560);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        bgBounds.WithChildren(mainBounds);

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);

        var claimBounds = ElementBounds.Fixed(18, 40, contentW, 20);
        var modeLabelBounds = ElementBounds.Fixed(18, 68, contentW, 20);

        var modeAllSwitchBounds = ElementBounds.Fixed(18, 94, 28, 28);
        var modeAllTextBounds = ElementBounds.Fixed(50, 98, 150, 22);
        var modeWhiteSwitchBounds = ElementBounds.Fixed(210, 94, 28, 28);
        var modeWhiteTextBounds = ElementBounds.Fixed(242, 98, 240, 22);

        var rescanW = 120;
        var listLabelBounds = ElementBounds.Fixed(18, 132, contentW - rescanW - 16, 24);
        var rescanBounds = ElementBounds.Fixed(18 + contentW - rescanW, 128, rescanW, 28);

        listViewportBounds = ElementBounds.Fixed(18, 160, ListWidth + 6, ListHeight + 6);
        listClipBounds = listViewportBounds.ForkContainingChild(3, 3, 3, 3);
        listTableBounds = listClipBounds.ForkContainingChild(0, 0, 0, -3).WithFixedPadding(3);

        var selectedBounds = ElementBounds.Fixed(18, 458, contentW, 20);
        var messageBounds = ElementBounds.Fixed(18, 480, contentW, 28);
        var cancelBounds = ElementBounds.Fixed(18 + contentW - 300, 520, 140, 34);
        var saveBounds = ElementBounds.Fixed(18 + contentW - 150, 520, 140, 34);

        ClearComposers();
        var composer = capi.Gui
            .CreateCompo("swixyclaimchunk-use-filter", dialogBounds)
            .AddShadedDialogBG(bgBounds, true)
            .AddDialogTitleBar(Lang.Get("swixyclaimchunk:use-filter-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds)
            .AddDynamicText(
                Lang.Get("swixyclaimchunk:use-filter-claim", claimName),
                CairoFont.WhiteSmallText(),
                claimBounds,
                "claimLabel")
            .AddStaticText(
                Lang.Get("swixyclaimchunk:use-filter-mode"),
                CairoFont.WhiteSmallText(),
                modeLabelBounds)
            .AddSwitch(OnModeAllSwitch, modeAllSwitchBounds, "modeAllSwitch", 25, 2)
            .AddStaticText(
                Lang.Get("swixyclaimchunk:use-filter-mode-all"),
                CairoFont.WhiteSmallText(),
                modeAllTextBounds)
            .AddSwitch(OnModeWhitelistSwitch, modeWhiteSwitchBounds, "modeWhitelistSwitch", 25, 2)
            .AddStaticText(
                Lang.Get("swixyclaimchunk:use-filter-mode-whitelist"),
                CairoFont.WhiteSmallText(),
                modeWhiteTextBounds)
            .AddStaticText(
                Lang.Get("swixyclaimchunk:use-filter-list-hint"),
                CairoFont.WhiteSmallText(),
                listLabelBounds)
            .AddSmallButton(
                Lang.Get("swixyclaimchunk:use-filter-rescan"),
                RescanButton,
                rescanBounds,
                EnumButtonStyle.Normal,
                "rescan")
            .AddInset(listViewportBounds, 3, 0.85f)
            .AddVerticalScrollbar(OnListScroll, ElementStdBounds.VerticalScrollbar(listViewportBounds), "blockListScroll")
            .BeginClip(listClipBounds)
            .AddCellList(listTableBounds, CreateFilterCell, BuildFilterCells(), "blockList")
            .EndClip()
            .AddDynamicText(BuildSelectedText(), CairoFont.WhiteSmallText(), selectedBounds, "selectedText")
            .AddDynamicText(statusMessage, CairoFont.WhiteDetailText(), messageBounds, "statusText")
            .AddButton(Lang.Get("swixyclaimchunk:use-filter-cancel"), CancelButton, cancelBounds, EnumButtonStyle.Normal, "cancel")
            .AddButton(Lang.Get("swixyclaimchunk:use-filter-save"), SaveButton, saveBounds, EnumButtonStyle.Normal, "save")
            .EndChildElements()
            .Compose();

        SingleComposer = composer;
        SyncModeSwitches();
        RestoreListScroll(listScrollValue);
    }

    private IEnumerable<SavegameCellEntry> BuildFilterCells()
    {
        foreach (var entry in entries)
        {
            var name = GetDisplayName(entry.Stack, entry.Code);
            yield return new SavegameCellEntry
            {
                Title = name,
                DetailText = entry.Code,
                HoverText = entry.Code,
                Selected = draftCodes.Contains(entry.Code),
                Enabled = true,
                DrawAsButton = true,
                LeftOffY = 0
            };
        }
    }

    private IGuiElementCell CreateFilterCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        var code = NormalizeCode(cell.DetailText);
        ItemStack? stack = null;
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Code, code, StringComparison.OrdinalIgnoreCase))
            {
                stack = entries[i].Stack;
                break;
            }
        }

        return new ClaimUseFilterListCell(
            clientApi,
            cell,
            bounds,
            code,
            stack,
            draftCodes.Contains(code))
        {
            OnToggle = ToggleCode
        };
    }

    private static string GetDisplayName(ItemStack stack, string code)
    {
        try
        {
            var name = stack?.GetName();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        catch
        {
            // ignore
        }

        return code;
    }

    private void OnListScroll(float value)
    {
        listScrollValue = value;
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("blockList");
        if (cellList == null)
        {
            return;
        }

        cellList.Bounds.fixedY = -value;
        cellList.Bounds.CalcWorldBounds();
    }

    private void RestoreListScroll(float scrollValue)
    {
        if (SingleComposer == null || listClipBounds == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<SavegameCellEntry>("blockList");
        if (cellList == null)
        {
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        listClipBounds.CalcWorldBounds();

        var clipHeight = (float)listClipBounds.fixedHeight;
        var tableHeight = (float)cellList.Bounds.fixedHeight;
        var maxScroll = Math.Max(0f, tableHeight - clipHeight);
        listScrollValue = Math.Clamp(scrollValue, 0f, maxScroll);

        var scroll = SingleComposer.GetScrollbar("blockListScroll");
        if (scroll != null)
        {
            scroll.SetHeights(clipHeight, tableHeight);
            scroll.CurrentYPosition = listScrollValue;
            scroll.RecomposeHandle();
        }

        cellList.Bounds.fixedY = -listScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

    private void ToggleCode(string code)
    {
        code = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        if (!draftCodes.Add(code))
        {
            draftCodes.Remove(code);
        }

        if (draftMode == ClaimUseFilterMode.AllowAll && draftCodes.Count > 0)
        {
            draftMode = ClaimUseFilterMode.Whitelist;
        }

        if (!scanPending)
        {
            statusMessage = "";
        }

        // Обновляем только визуал строк — без полного ComposeDialog.
        UpdateWhitelistVisualsInPlace();
        SyncModeSwitches();
        SingleComposer?.GetDynamicText("selectedText")?.SetNewText(BuildSelectedText());
        SingleComposer?.GetDynamicText("statusText")?.SetNewText(statusMessage);
    }

    private void UpdateWhitelistVisualsInPlace()
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("blockList");
        if (cellList == null)
        {
            return;
        }

        for (var i = 0; i < cellList.elementCells.Count; i++)
        {
            if (cellList.elementCells[i] is not ClaimUseFilterListCell row)
            {
                continue;
            }

            row.SetWhitelisted(draftCodes.Contains(row.BlockCode));
        }
    }

    private bool RescanButton()
    {
        RequestClaimScan();
        return true;
    }

    private void OnModeAllSwitch(bool on)
    {
        if (syncingModeSwitches)
        {
            return;
        }

        draftMode = ClaimUseFilterMode.AllowAll;
        statusMessage = scanPending ? Lang.Get("swixyclaimchunk:use-filter-scan-loading") : "";
        SyncModeSwitches();
        SingleComposer?.GetDynamicText("selectedText")?.SetNewText(BuildSelectedText());
        SingleComposer?.GetDynamicText("statusText")?.SetNewText(statusMessage);
    }

    private void OnModeWhitelistSwitch(bool on)
    {
        if (syncingModeSwitches)
        {
            return;
        }

        draftMode = ClaimUseFilterMode.Whitelist;
        statusMessage = scanPending ? Lang.Get("swixyclaimchunk:use-filter-scan-loading") : "";
        SyncModeSwitches();
        SingleComposer?.GetDynamicText("selectedText")?.SetNewText(BuildSelectedText());
        SingleComposer?.GetDynamicText("statusText")?.SetNewText(statusMessage);
    }

    private void SyncModeSwitches()
    {
        if (SingleComposer == null)
        {
            return;
        }

        syncingModeSwitches = true;
        try
        {
            SingleComposer.GetSwitch("modeAllSwitch")?.SetValue(draftMode == ClaimUseFilterMode.AllowAll);
            SingleComposer.GetSwitch("modeWhitelistSwitch")?.SetValue(draftMode == ClaimUseFilterMode.Whitelist);
        }
        finally
        {
            syncingModeSwitches = false;
        }
    }

    private bool SaveButton()
    {
        if (draftMode == ClaimUseFilterMode.Whitelist && draftCodes.Count == 0)
        {
            statusMessage = Lang.Get("swixyclaimchunk:use-filter-error-empty");
            SingleComposer?.GetDynamicText("statusText")?.SetNewText(statusMessage);
            return true;
        }

        var mode = draftMode == ClaimUseFilterMode.Whitelist && draftCodes.Count > 0
            ? ClaimUseFilterMode.Whitelist
            : ClaimUseFilterMode.AllowAll;

        var codes = mode == ClaimUseFilterMode.Whitelist
            ? draftCodes
                .Select(NormalizeCode)
                .Where(static c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static c => c, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        channel.SendPacket(new ClaimAccessActionPacket
        {
            ClaimId = claimId,
            Action = ClaimAccessActionType.SetUseFilter,
            UseFilterMode = mode,
            UseFilterCodesRaw = ClaimUseFilterCodesCodec.Join(codes)
        });

        TryClose();
        return true;
    }

    private static string NormalizeCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        var trimmed = raw.Trim();
        try
        {
            var location = new AssetLocation(trimmed);
            if (string.IsNullOrWhiteSpace(location.Domain) || string.IsNullOrWhiteSpace(location.Path))
            {
                return trimmed;
            }

            return location.ToString();
        }
        catch
        {
            return trimmed;
        }
    }

    private bool CancelButton()
    {
        TryClose();
        return true;
    }

    private void OnTitleBarClose() => TryClose();

    private string BuildSelectedText()
    {
        if (draftMode == ClaimUseFilterMode.AllowAll)
        {
            return Lang.Get("swixyclaimchunk:use-filter-selected-all");
        }

        return Lang.Get("swixyclaimchunk:use-filter-selected-count", draftCodes.Count);
    }

    private List<(string Code, ItemStack Stack)> CollectPickerEntries()
    {
        var result = new List<(string Code, ItemStack Stack)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in draftCodes.OrderBy(static v => v, StringComparer.OrdinalIgnoreCase))
        {
            TryAddEntry(result, seen, code);
        }

        foreach (var code in scannedCodes.OrderBy(static v => v, StringComparer.OrdinalIgnoreCase))
        {
            TryAddEntry(result, seen, code);
        }

        return result;
    }

    private void TryAddEntry(
        List<(string Code, ItemStack Stack)> result,
        HashSet<string> seen,
        string code)
    {
        var normalized = NormalizeCode(code);
        var stack = ResolveStack(normalized);
        if (stack == null || string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
        {
            return;
        }

        result.Add((normalized, stack));
    }

    private ItemStack? ResolveStack(string code)
    {
        code = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        // Только варианты той же «семьи» (без ориентации): wood ≠ metal у windmillrotor.
        var groupKey = SwixyClaimChunkMod.StripVariantSuffixes(code);
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            groupKey = code;
        }

        var family = CollectGroupCollectibles(code, groupKey);
        if (family.Count == 0)
        {
            return null;
        }

        // 1) CreativeInventoryStacks / handbook — фонари без material в attributes «кривые».
        //    Сначала ищем лучший handbook-стек в группе (не bare ItemStack от wall-north).
        ItemStack? bestHandbook = null;
        var bestHandbookScore = int.MinValue;
        CollectibleObject? bestCol = null;
        var bestColScore = int.MinValue;

        foreach (var col in family)
        {
            var colCode = NormalizeCode(col.Code?.ToString());
            if (string.IsNullOrWhiteSpace(colCode))
            {
                continue;
            }

            var score = ScoreDisplayCandidate(colCode, col, code, groupKey);
            if (score > bestColScore)
            {
                bestColScore = score;
                bestCol = col;
            }

            if (col.CreativeInventoryStacks is not { Length: > 0 })
            {
                continue;
            }

            try
            {
                var handbook = col.GetHandBookStacks(clientApi);
                if (handbook is not { Count: > 0 })
                {
                    continue;
                }

                // Первый creative-стек обычно copper plain quartz — как в creative menu.
                var stack = handbook[0].Clone();
                stack.ResolveBlockOrItem(clientApi.World);
                stack.StackSize = 1;
                // Stacks с attributes важнее bare-блока.
                var stackScore = score + 5000;
                if (stackScore > bestHandbookScore)
                {
                    bestHandbookScore = stackScore;
                    bestHandbook = stack;
                }
            }
            catch
            {
                // ignore
            }
        }

        if (bestHandbook != null)
        {
            return bestHandbook;
        }

        if (bestCol == null)
        {
            return null;
        }

        var result = new ItemStack(bestCol);
        result.ResolveBlockOrItem(clientApi.World);
        result.StackSize = 1;
        EnsureDefaultDisplayAttributes(result);
        return result;
    }

    /// <summary>
    /// Lantern и подобные: material/lining/glass живут в attributes.
    /// Bare ItemStack без них рендерится криво — подставляем vanilla defaults (BELantern).
    /// </summary>
    private static void EnsureDefaultDisplayAttributes(ItemStack stack)
    {
        if (stack?.Collectible?.Code == null)
        {
            return;
        }

        var path = stack.Collectible.Code.Path ?? "";
        if (!path.StartsWith("lantern", StringComparison.OrdinalIgnoreCase)
            && !path.Contains("lantern-", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        stack.Attributes ??= new Vintagestory.API.Datastructures.TreeAttribute();
        if (string.IsNullOrWhiteSpace(stack.Attributes.GetString("material")))
        {
            stack.Attributes.SetString("material", "copper");
        }

        if (string.IsNullOrWhiteSpace(stack.Attributes.GetString("lining")))
        {
            stack.Attributes.SetString("lining", "plain");
        }

        if (string.IsNullOrWhiteSpace(stack.Attributes.GetString("glass")))
        {
            stack.Attributes.SetString("glass", "quartz");
        }
    }

    /// <summary>
    /// Собирает collectible той же groupKey (StripVariantSuffixes).
    /// Не ходит по firstPart-* — иначе wood-ротор тянет metal-иконку.
    /// </summary>
    private List<CollectibleObject> CollectGroupCollectibles(string code, string groupKey)
    {
        var result = new List<CollectibleObject>();
        var seen = new HashSet<int>();

        void TryAdd(CollectibleObject? col)
        {
            if (col?.Code == null || col.Id <= 0 || !seen.Add(col.Id))
            {
                return;
            }

            var colCode = NormalizeCode(col.Code.ToString());
            var colGroup = SwixyClaimChunkMod.StripVariantSuffixes(colCode);
            if (string.IsNullOrWhiteSpace(colGroup))
            {
                colGroup = colCode;
            }

            // В группу: exact, тот же stripped path, или code является префиксом/вариантом group.
            if (!string.Equals(colGroup, groupKey, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(colCode, code, StringComparison.OrdinalIgnoreCase)
                && !colCode.StartsWith(groupKey + "-", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            result.Add(col);
        }

        AssetLocation location;
        AssetLocation groupLocation;
        try
        {
            location = new AssetLocation(code);
            groupLocation = new AssetLocation(groupKey);
        }
        catch
        {
            return result;
        }

        TryAdd(clientApi.World.GetBlock(location));
        TryAdd(clientApi.World.GetItem(location));
        TryAdd(clientApi.World.GetBlock(groupLocation));
        TryAdd(clientApi.World.GetItem(groupLocation));

        var domain = groupLocation.Domain ?? "game";
        var groupPath = groupLocation.Path ?? "";

        // Creative-ориентации: windmillrotor-wood-north, torchholder-*-empty-north и т.п.
        foreach (var suffix in new[]
                 {
                     "", "-south", "-north", "-east", "-west", "-normal",
                     "-empty-north", "-empty-south", "-empty-east", "-empty-west",
                     "-empty", "-full", "-closed", "-open"
                 })
        {
            try
            {
                TryAdd(clientApi.World.GetBlock(new AssetLocation(domain, groupPath + suffix)));
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            foreach (var b in clientApi.World.SearchBlocks(new AssetLocation(domain, groupPath + "-*")))
            {
                TryAdd(b);
            }
        }
        catch
        {
            // ignore
        }

        return result;
    }

    private static int ScoreDisplayCandidate(
        string colCode,
        CollectibleObject col,
        string requestedCode,
        string groupKey)
    {
        var score = 0;

        // Stacks (фонарь с material) важнее exact wall-ориентации.
        if (col.CreativeInventoryStacks is { Length: > 0 })
        {
            score += 1500;
        }
        else if (col.CreativeInventoryTabs is { Length: > 0 })
        {
            score += 1000;
        }

        if (string.Equals(colCode, requestedCode, StringComparison.OrdinalIgnoreCase))
        {
            // Exact без stacks — слабый бонус (стена фонаря без attributes).
            score += col.CreativeInventoryStacks is { Length: > 0 } ? 500 : 50;
        }

        var colGroup = SwixyClaimChunkMod.StripVariantSuffixes(colCode);
        if (string.Equals(colGroup, groupKey, StringComparison.OrdinalIgnoreCase))
        {
            score += 200;
        }

        var lower = colCode.ToLowerInvariant();
        // Lantern creative = *-up; rotor/EP = *-north / *-south.
        if (lower.EndsWith("-up") || lower.Contains("-up-"))
        {
            score += 120;
        }
        else if (lower.EndsWith("-north") || lower.Contains("-north-"))
        {
            score += 80;
        }
        else if (lower.EndsWith("-south") || lower.Contains("-south-"))
        {
            score += 40;
        }

        if (lower.Contains("-burned") || lower.Contains("-broken") || lower.Contains("-ruined"))
        {
            score -= 120;
        }

        return score;
    }
}


