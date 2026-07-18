// =============================================================================
// ClaimUseFilterDialog.cs
// -----------------------------------------------------------------------------
// Whitelist Use: creative-style catalog (items + blocks like creative menu).
// Search + scrollable list; click row to toggle whitelist.
// Opened via RMB on gear (Use) in the members list.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Core;
using SwixyClaimChunk.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Диалог настройки whitelist блоков/предметов для права Use в привате.
/// Каталог — как creative menu (CreativeInventoryTabs / Stacks).
/// </summary>
public sealed class ClaimUseFilterDialog : GuiDialog
{
    private const double ListWidth = 520;
    private const double ListHeight = 320;

    private readonly ICoreClientAPI clientApi;
    private readonly IClientNetworkChannel channel;
    private readonly int claimId;
    private readonly string claimName;
    private readonly Action? onClosed;

    private int draftMode;
    private readonly HashSet<string> draftCodes;

    /// <summary>Full creative catalog (built once per dialog open).</summary>
    private List<(string Code, string Label, ItemStack Stack)> catalog = [];

    private List<(string Code, ItemStack Stack)> entries = [];

    private string searchQuery = "";
    private string statusMessage = "";
    private bool syncingModeSwitches;
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

        catalog = BuildCreativeCatalog();
        statusMessage = Lang.Get("swixyclaimchunk:use-filter-catalog-ready", catalog.Count);
        ComposeDialog();
    }

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();
        onClosed?.Invoke();
    }

    /// <summary>Legacy scan result hook (no longer required for catalog).</summary>
    public void ApplyScanResult(ClaimUseFilterScanResultPacket packet)
    {
        // Catalog is creative-based; scan packets are ignored.
    }

    private void ComposeDialog()
    {
        entries = CollectPickerEntries();

        var scrollW = GuiElementScrollbar.DefaultScrollbarWidth
            + GuiElementScrollbar.DeafultScrollbarPadding * 2;
        var contentW = ListWidth + 6 + scrollW;

        var mainBounds = ElementBounds.Fixed(0, 0, Math.Max(540, contentW + 36), 600);
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
        var modeWhiteTextBounds = ElementBounds.Fixed(242, 98, 260, 22);

        var searchLabelBounds = ElementBounds.Fixed(18, 132, 80, 24);
        var searchInputBounds = ElementBounds.Fixed(100, 128, contentW - 100, 28);

        listViewportBounds = ElementBounds.Fixed(18, 168, ListWidth + 6, ListHeight + 6);
        listClipBounds = listViewportBounds.ForkContainingChild(3, 3, 3, 3);
        listTableBounds = listClipBounds.ForkContainingChild(0, 0, 0, -3).WithFixedPadding(3);

        var selectedBounds = ElementBounds.Fixed(18, 508, contentW, 20);
        var messageBounds = ElementBounds.Fixed(18, 530, contentW, 24);
        var cancelBounds = ElementBounds.Fixed(18 + contentW - 300, 560, 140, 34);
        var saveBounds = ElementBounds.Fixed(18 + contentW - 150, 560, 140, 34);

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
                Lang.Get("swixyclaimchunk:use-filter-search"),
                CairoFont.WhiteSmallText(),
                searchLabelBounds)
            .AddTextInput(searchInputBounds, OnSearchChanged, CairoFont.WhiteSmallText(), "searchInput")
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
        SingleComposer.GetTextInput("searchInput")?.SetValue(searchQuery, true);
        SyncModeSwitches();
        RestoreListScroll(listScrollValue);
    }

    private void OnSearchChanged(string text)
    {
        searchQuery = text ?? "";
        var saved = listScrollValue;
        entries = CollectPickerEntries();
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("blockList");
        if (cellList != null)
        {
            cellList.ReloadCells(BuildFilterCells());
            RestoreListScroll(0);
        }
        else
        {
            ComposeDialog();
            RestoreListScroll(saved);
        }
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

        statusMessage = "";
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

    private void OnModeAllSwitch(bool on)
    {
        if (syncingModeSwitches)
        {
            return;
        }

        draftMode = ClaimUseFilterMode.AllowAll;
        statusMessage = "";
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
        statusMessage = "";
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
        var filter = (searchQuery ?? "").Trim();
        var result = new List<(string Code, ItemStack Stack)>(512);

        // Selected first (always visible even if filtered out of search — only if match search).
        var selected = new List<(string Code, ItemStack Stack)>();
        var rest = new List<(string Code, ItemStack Stack)>();

        foreach (var (code, label, stack) in catalog)
        {
            if (filter.Length > 0
                && label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                && code.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (draftCodes.Contains(code))
            {
                selected.Add((code, stack));
            }
            else
            {
                rest.Add((code, stack));
            }
        }

        result.AddRange(selected);
        result.AddRange(rest);
        return result;
    }

    /// <summary>
    /// Same source as creative menu / Questbook admin picker:
    /// collectibles with CreativeInventoryTabs or CreativeInventoryStacks.
    /// </summary>
    private List<(string Code, string Label, ItemStack Stack)> BuildCreativeCatalog()
    {
        var result = new List<(string Code, string Label, ItemStack Stack)>(2048);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAddStack(ItemStack? stack)
        {
            if (stack?.Collectible?.Code == null)
            {
                return;
            }

            var code = NormalizeCode(stack.Collectible.Code.ToString());
            if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
            {
                return;
            }

            var path = stack.Collectible.Code.Path ?? "";
            if (path.Equals("air", StringComparison.OrdinalIgnoreCase)
                || path.Equals("unknown", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("creature-", StringComparison.OrdinalIgnoreCase)
                || path.Contains("-dead", StringComparison.OrdinalIgnoreCase)
                || path.Contains("armorstand", StringComparison.OrdinalIgnoreCase)
                || path.Contains("strawdummy", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ItemStack displayStack;
            try
            {
                displayStack = stack.Clone();
                displayStack.StackSize = 1;
            }
            catch
            {
                return;
            }

            string label;
            try
            {
                label = displayStack.GetName();
            }
            catch
            {
                label = code;
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                label = code;
            }

            result.Add((code, label, displayStack));
        }

        void TryAddCollectible(CollectibleObject? collectible)
        {
            if (collectible?.Code == null || collectible.Id == 0)
            {
                return;
            }

            var hasTabs = collectible.CreativeInventoryTabs is { Length: > 0 };
            var hasStacks = collectible.CreativeInventoryStacks is { Length: > 0 };
            if (!hasTabs && !hasStacks)
            {
                return;
            }

            if (hasStacks)
            {
                foreach (var tabList in collectible.CreativeInventoryStacks!)
                {
                    if (tabList?.Stacks == null)
                    {
                        continue;
                    }

                    foreach (var jstack in tabList.Stacks)
                    {
                        if (jstack == null)
                        {
                            continue;
                        }

                        try
                        {
                            if (jstack.ResolvedItemstack == null)
                            {
                                jstack.Resolve(clientApi.World, "swixyclaimchunk use-filter", collectible.Code);
                            }

                            if (jstack.ResolvedItemstack != null)
                            {
                                TryAddStack(jstack.ResolvedItemstack);
                            }
                        }
                        catch
                        {
                            // skip broken creative variants
                        }
                    }
                }

                return;
            }

            try
            {
                TryAddStack(new ItemStack(collectible, 1));
            }
            catch
            {
                // skip
            }
        }

        foreach (var item in clientApi.World.Items)
        {
            TryAddCollectible(item);
        }

        foreach (var block in clientApi.World.Blocks)
        {
            TryAddCollectible(block);
        }

        result.Sort(static (a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        return result;
    }
}
