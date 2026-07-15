// =============================================================================
// ClaimUseFilterDialog.cs
// -----------------------------------------------------------------------------
// Модальное окно выбора блоков для фильтра Use: сетка из инвентаря (hotbar +
// рюкзак), клик = вкл/выкл. Сохранение одним пакетом SetUseFilter.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Диалог настройки whitelist блоков для права Use в привате.
/// </summary>
public sealed class ClaimUseFilterDialog : GuiDialog
{
    private const int GridColumns = 8;
    private const int GridRows = 5;
    private const int MaxCodes = 64;

    /// <summary>Сдвиг сетки вправо, чтобы визуально центрировать ячейки в окне.</summary>
    private const double GridOffsetX = 30;

    private readonly ICoreClientAPI clientApi;
    private readonly IClientNetworkChannel channel;
    private readonly int claimId;
    private readonly string claimName;
    private readonly Action? onClosed;
    private readonly DummySlot renderSlot = new(null);

    private int draftMode;
    private readonly HashSet<string> draftCodes;
    private List<SkillItem> skillItems = [];
    private string statusMessage = "";
    private bool syncingModeSwitches;

    public override string ToggleKeyCombinationCode => null!;

    public override bool PrefersUngrabbedMouse => true;

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

        ComposeDialog();
    }

    public override void OnGuiClosed()
    {
        DisposeSkillItems();
        base.OnGuiClosed();
        onClosed?.Invoke();
    }

    public override void Dispose()
    {
        DisposeSkillItems();
        base.Dispose();
    }

    private void ComposeDialog()
    {
        DisposeSkillItems();
        skillItems = BuildSkillItems();

        // Размер диалога задаётся дочерним mainBounds; иначе bg = 0x0 и краш blur.
        var mainBounds = ElementBounds.Fixed(0, 0, 500, 530);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        bgBounds.WithChildren(mainBounds);

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);

        var claimBounds = ElementBounds.Fixed(18, 40, 464, 20);
        var modeLabelBounds = ElementBounds.Fixed(18, 68, 464, 20);

        // Флажки режима в одной строке: [switch] надпись | [switch] надпись
        var modeAllSwitchBounds = ElementBounds.Fixed(18, 94, 28, 28);
        var modeAllTextBounds = ElementBounds.Fixed(50, 98, 150, 22);
        var modeWhiteSwitchBounds = ElementBounds.Fixed(210, 94, 28, 28);
        var modeWhiteTextBounds = ElementBounds.Fixed(242, 98, 240, 22);

        var inventoryLabelBounds = ElementBounds.Fixed(18, 132, 464, 20);
        var gridBounds = ElementBounds.Fixed(18 + GridOffsetX, 156, 464, 270);
        var selectedBounds = ElementBounds.Fixed(18, 434, 464, 20);
        var messageBounds = ElementBounds.Fixed(18, 456, 464, 24);
        var cancelBounds = ElementBounds.Fixed(168, 486, 140, 34);
        var saveBounds = ElementBounds.Fixed(326, 486, 140, 34);

        ClearComposers();
        SingleComposer = capi.Gui
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
                Lang.Get("swixyclaimchunk:use-filter-inventory"),
                CairoFont.WhiteSmallText(),
                inventoryLabelBounds)
            .AddSkillItemGrid(skillItems, GridColumns, GridRows, OnSkillItemClick, gridBounds, "blockGrid")
            .AddDynamicText(BuildSelectedText(), CairoFont.WhiteSmallText(), selectedBounds, "selectedText")
            .AddDynamicText(statusMessage, CairoFont.WhiteDetailText(), messageBounds, "statusText")
            .AddButton(Lang.Get("swixyclaimchunk:use-filter-cancel"), CancelButton, cancelBounds, EnumButtonStyle.Normal, "cancel")
            .AddButton(Lang.Get("swixyclaimchunk:use-filter-save"), SaveButton, saveBounds, EnumButtonStyle.Normal, "save")
            .EndChildElements()
            .Compose();

        SyncModeSwitches();
    }

    private void OnModeAllSwitch(bool on)
    {
        if (syncingModeSwitches)
        {
            return;
        }

        // Радио-поведение: всегда один из двух режимов.
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

    private void OnSkillItemClick(int index)
    {
        if (index < 0 || index >= skillItems.Count)
        {
            return;
        }

        // Description хранит стабильный domain:path; Code.ToString() у AssetLocation может отличаться.
        var code = NormalizeCode(skillItems[index].Description);
        if (string.IsNullOrWhiteSpace(code))
        {
            code = NormalizeCode(skillItems[index].Code?.ToString());
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        if (draftCodes.Contains(code))
        {
            draftCodes.Remove(code);
        }
        else
        {
            if (draftCodes.Count >= MaxCodes)
            {
                statusMessage = Lang.Get("swixyclaimchunk:use-filter-error-limit", MaxCodes);
                SingleComposer?.GetDynamicText("statusText")?.SetNewText(statusMessage);
                return;
            }

            draftCodes.Add(code);
        }

        if (draftMode == ClaimUseFilterMode.AllowAll && draftCodes.Count > 0)
        {
            draftMode = ClaimUseFilterMode.Whitelist;
        }

        statusMessage = "";
        ComposeDialog();
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
                .Where(static code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static code => code, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        var codesRaw = ClaimUseFilterCodesCodec.Join(codes);
        clientApi.Logger.Notification(
            "[SwixyClaimChunk] Sending SetUseFilter claimId={0} mode={1} codes={2} rawLen={3}",
            claimId,
            mode,
            codes.Count,
            codesRaw.Length);

        channel.SendPacket(new ClaimAccessActionPacket
        {
            ClaimId = claimId,
            Action = ClaimAccessActionType.SetUseFilter,
            UseFilterMode = mode,
            UseFilterCodesRaw = codesRaw
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

    private void OnTitleBarClose()
    {
        TryClose();
    }

    private string BuildSelectedText()
    {
        if (draftMode == ClaimUseFilterMode.AllowAll)
        {
            return Lang.Get("swixyclaimchunk:use-filter-selected-all");
        }

        return Lang.Get("swixyclaimchunk:use-filter-selected-count", draftCodes.Count);
    }

    private List<SkillItem> BuildSkillItems()
    {
        var entries = CollectPickerEntries();
        var items = new List<SkillItem>(entries.Count);

        foreach (var entry in entries)
        {
            var code = NormalizeCode(entry.Code);
            var stack = entry.Stack;
            var selected = !string.IsNullOrWhiteSpace(code) && draftCodes.Contains(code);
            var displayName = stack.GetName();
            var skillItem = new SkillItem
            {
                Code = new AssetLocation(code),
                Name = displayName,
                // Стабильный ключ выбора/сохранения (не полагаемся на Code.ToString()).
                Description = code,
                Data = stack,
                Enabled = true,
                // Не ставим Texture-фон: зелёный fill ломает полупрозрачные предметы (фонари и т.п.).
                Texture = null
            };

            var renderStack = stack.Clone();
            var isSelected = selected;
            skillItem.RenderHandler = (_, dt, posX, posY) =>
            {
                // posX/posY — верхний левый угол ячейки + 1px (как в GuiElementSkillItemGrid).
                var absSlotSize = GuiElement.scaled(GuiElementPassiveItemSlot.unscaledSlotSize);
                var absItemSize = (float)GuiElement.scaled(GuiElementPassiveItemSlot.unscaledItemSize);
                var centerX = posX - 1 + (absSlotSize / 2);
                var centerY = posY - 1 + (absSlotSize / 2);

                // Сначала предмет как в инвентаре — без зелёного fill под ним.
                renderSlot.Itemstack = renderStack;
                clientApi.Render.RenderItemstackToGui(
                    renderSlot,
                    centerX,
                    centerY,
                    90,
                    absItemSize,
                    ColorUtil.WhiteArgb,
                    dt);

                // Выделение — только рамка поверх, не перекрывает текстуру.
                if (isSelected)
                {
                    DrawSelectedBorder((float)(posX - 1), (float)(posY - 1), (float)absSlotSize);
                }
            };

            items.Add(skillItem);
        }

        return items;
    }

    /// <summary>Зелёная рамка вокруг выбранной ячейки (4 тонких прямоугольника).</summary>
    private void DrawSelectedBorder(float x, float y, float slotSize)
    {
        // ColorUtil.ToRgba(a, r, g, b)
        var color = ColorUtil.ToRgba(230, 70, 210, 90);
        const float thickness = 2.5f;
        const float z = 50f;

        // top
        clientApi.Render.RenderRectangle(x, y, z, slotSize, thickness, color);
        // bottom
        clientApi.Render.RenderRectangle(x, y + slotSize - thickness, z, slotSize, thickness, color);
        // left
        clientApi.Render.RenderRectangle(x, y, z, thickness, slotSize, color);
        // right
        clientApi.Render.RenderRectangle(x + slotSize - thickness, y, z, thickness, slotSize, color);
    }

    private List<(string Code, ItemStack Stack)> CollectPickerEntries()
    {
        var result = new List<(string Code, ItemStack Stack)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in draftCodes.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
        {
            var normalized = NormalizeCode(code);
            var stack = ResolveStack(normalized);
            if (stack == null || string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
            {
                continue;
            }

            result.Add((normalized, stack));
        }

        var player = clientApi.World.Player;
        if (player?.InventoryManager == null)
        {
            return result;
        }

        foreach (var invName in new[] { GlobalConstants.hotBarInvClassName, GlobalConstants.backpackInvClassName })
        {
            var inventory = player.InventoryManager.GetOwnInventory(invName);
            if (inventory == null)
            {
                continue;
            }

            foreach (var slot in inventory)
            {
                var stack = slot.Itemstack;
                if (stack?.Block == null || stack.Block.Code == null)
                {
                    continue;
                }

                var code = NormalizeCode(stack.Block.Code.ToString());
                if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
                {
                    continue;
                }

                result.Add((code, stack.Clone()));
            }
        }

        return result;
    }

    private ItemStack? ResolveStack(string code)
    {
        code = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        AssetLocation location;
        try
        {
            location = new AssetLocation(code);
        }
        catch
        {
            return null;
        }

        var block = clientApi.World.GetBlock(location);
        if (block != null)
        {
            return new ItemStack(block);
        }

        var item = clientApi.World.GetItem(location);
        return item != null ? new ItemStack(item) : null;
    }

    private void DisposeSkillItems()
    {
        foreach (var item in skillItems)
        {
            item.Dispose();
        }

        skillItems = [];
    }
}
