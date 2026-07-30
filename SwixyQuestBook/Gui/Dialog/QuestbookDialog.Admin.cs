using System.IO;
using Cairo;
using SwixyQuestBook.Client;
using SwixyQuestBook.Domain.Models;
using SwixyQuestBook.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace SwixyQuestBook.Gui
{
    public sealed partial class QuestbookDialog
    {
        private enum BranchModalMode
        {
            None,
            Add,
            Rename,
            DeleteConfirm
        }

        private bool TryHandleAdminSettingsClick(double mouseX, double mouseY)
        {
            if (!IsPlayerAdmin() || adminData.IsAdminPanelOpen) return false;
            if (!adminSettingsButtonHitArea.Contains(mouseX, mouseY)) return false;

            OpenAdminEditor();
            ComposeDialog();
            return true;
        }

        private void OpenBranchModal(BranchModalMode mode)
        {
            branchModalMode = mode;
            isBranchModalOpen = true;
            isBranchModalPrimaryHovered = false;
            isBranchModalCancelHovered = false;
            isBranchModalTitleFocused = mode != BranchModalMode.DeleteConfirm;
            branchModalIconSearchFocused = false;
            branchModalIconSearchText = string.Empty;
            branchModalIconScrollOffset = 0;
            isBranchModalItemPickerOpen = false;
            branchModalIconPreviewHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalIconSearchHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalIconViewportLocal = new LayoutRect(0, 0, 0, 0);
            branchModalIconPickerPanelLocal = new LayoutRect(0, 0, 0, 0);
            branchModalIconPickerCancelHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalItemPickerSlots = [];
            branchModalLangCodes = [];
            branchModalLangButtonHitAreas = [];
            branchModalEditorLang = QuestbookLocalizedText.NormalizeLang(Lang.CurrentLocale);
            branchModalTitleByLang.Clear();
            if (isBranchModalTitleFocused)
                ResetTextCaretBlink();
            branchModalTargetHeaderTitle = categories.Length > 0 ? GetSelectedCategory().HeaderTitle : string.Empty;

            if (mode == BranchModalMode.Rename && categories.Length > 0)
            {
                QuestbookCategoryDefinition category = GetSelectedCategory();
                // Need full TitleByLang for multi-language editing.
                dataManager.EnsureCategoryContentLoaded(category.HeaderTitle, includeI18n: true);
                category = GetSelectedCategory();
                foreach ((string lang, string text) in category.TitleByLang)
                {
                    if (!string.IsNullOrWhiteSpace(lang) && !string.IsNullOrWhiteSpace(text))
                        branchModalTitleByLang[QuestbookLocalizedText.NormalizeLang(lang)] = text;
                }

                branchModalTitleText = branchModalTitleByLang.TryGetValue(branchModalEditorLang, out string? forLang)
                    ? forLang
                    : GetEditableCategoryTitle(category);
                branchModalSelectedIconItemCode = category.IconItemCode ?? string.Empty;
            }
            else
            {
                branchModalTitleText = string.Empty;
                branchModalSelectedIconItemCode = string.Empty;
            }
        }

        private void OpenQuestEditModal()
        {
            if (!adminData.HasSelectedNode)
                return;

            adminData.EditorLanguage = QuestbookLocalizedText.NormalizeLang(Lang.CurrentLocale);
            // Re-bind description text to the active editor language.
            if (adminData.HasSelectedNode)
            {
                var node = GetSelectedCategory()?.Nodes.FirstOrDefault(n => n.Id == adminData.SelectedNodeId);
                if (node != null)
                    adminData.LoadFromNode(node);
            }

            isQuestEditModalOpen = true;
            isQuestEditModalSaveHovered = false;
            isGoalsAddHovered = false;
            isAwardsAddHovered = false;
            goalsListScrollOffset = 0;
            awardsListScrollOffset = 0;
            adminData.FocusedField = AdminFormFieldRef.None;
        }

        private void SaveAndCloseQuestEditModal()
        {
            adminData.CommitCooldownMinutesDraft();
            ApplyFormToSelectedNode();
            HandleAdminSave();
            CloseQuestEditModal();
        }

        private void DismissQuestEditModal()
        {
            adminData.CommitCooldownMinutesDraft();
            ApplyFormToSelectedNode();
            CloseQuestEditModal();
        }

        private void CloseQuestEditModal()
        {
            isQuestEditModalOpen = false;
            isQuestEditModalSaveHovered = false;
            isGoalsAddHovered = false;
            isAwardsAddHovered = false;
            goalsListScrollOffset = 0;
            awardsListScrollOffset = 0;
            adminData.FocusedField = AdminFormFieldRef.None;
            questEditModalOverlayHitArea = new LayoutRect(0, 0, 0, 0);
            questEditModalPanelHitArea = new LayoutRect(0, 0, 0, 0);
            questEditModalSaveButtonHitArea = new LayoutRect(0, 0, 0, 0);
            goalsListViewportHitArea = new LayoutRect(0, 0, 0, 0);
            awardsListViewportHitArea = new LayoutRect(0, 0, 0, 0);
            goalsAddButtonHitArea = new LayoutRect(0, 0, 0, 0);
            awardsAddButtonHitArea = new LayoutRect(0, 0, 0, 0);
            goalsRemoveHitAreas = [];
            awardsRemoveHitAreas = [];
            goalsItemPickHitAreas = [];
            awardsItemPickHitAreas = [];
            goalsMatchToggleHitAreas = [];
            awardsMatchToggleHitAreas = [];
            goalsTakeToggleHitAreas = [];
            awardsTakeToggleHitAreas = [];
            goalsCraftToggleHitAreas = [];
            goalsKillToggleHitAreas = [];
            CloseAdminItemPicker();
            adminInputFieldHitAreas = [];
            adminInputFieldRefs = [];
            adminTypeStartHitArea = new LayoutRect(0, 0, 0, 0);
            adminRepeatOnceHitArea = new LayoutRect(0, 0, 0, 0);
            adminRepeatCooldownHitArea = new LayoutRect(0, 0, 0, 0);
            adminRepeatInstantHitArea = new LayoutRect(0, 0, 0, 0);
            adminRepeatHoursHitArea = new LayoutRect(0, 0, 0, 0);
            adminTypeQuestHitArea = new LayoutRect(0, 0, 0, 0);
            adminTypeCheckpointHitArea = new LayoutRect(0, 0, 0, 0);
            adminTypeKillHitArea = new LayoutRect(0, 0, 0, 0);

        }

        private void CloseBranchModal()
        {
            branchModalMode = BranchModalMode.None;
            isBranchModalOpen = false;
            isBranchModalTitleFocused = false;
            branchModalIconSearchFocused = false;
            branchModalIconSearchText = string.Empty;
            branchModalIconScrollOffset = 0;
            isBranchModalItemPickerOpen = false;
            branchModalTitleText = string.Empty;
            branchModalTargetHeaderTitle = string.Empty;
            isBranchModalPrimaryHovered = false;
            isBranchModalCancelHovered = false;
            branchModalOverlayHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalPanelHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalTitleInputHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalPrimaryButtonHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalCancelButtonHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalIconPreviewHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalIconSearchHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalIconViewportLocal = new LayoutRect(0, 0, 0, 0);
            branchModalIconPickerPanelLocal = new LayoutRect(0, 0, 0, 0);
            branchModalIconPickerCancelHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalSelectedIconItemCode = string.Empty;
            branchModalItemPickerSlots = [];
            branchModalTitleByLang.Clear();
            branchModalLangCodes = [];
            branchModalLangButtonHitAreas = [];
            branchModalEditorLang = "en";
        }

        private void OpenBranchModalItemPicker()
        {
            isBranchModalItemPickerOpen = true;
            isBranchModalTitleFocused = false;
            branchModalIconSearchFocused = true;
            branchModalIconSearchText = string.Empty;
            branchModalIconScrollOffset = 0;
            branchModalItemPickerSlots = [];
            ResetTextCaretBlink();
        }

        private void CloseBranchModalItemPicker()
        {
            isBranchModalItemPickerOpen = false;
            branchModalIconSearchFocused = false;
            branchModalIconSearchText = string.Empty;
            branchModalIconScrollOffset = 0;
            branchModalItemPickerSlots = [];
            branchModalIconSearchHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalIconViewportLocal = new LayoutRect(0, 0, 0, 0);
            branchModalIconPickerPanelLocal = new LayoutRect(0, 0, 0, 0);
            branchModalIconPickerCancelHitArea = new LayoutRect(0, 0, 0, 0);
        }

        private string GetEditableCategoryTitle(QuestbookCategoryDefinition category)
        {
            // Already language-resolved by the server.
            return category.Title;
        }

        private void FlushBranchModalTitleToLangMap()
        {
            string lang = QuestbookLocalizedText.NormalizeLang(branchModalEditorLang);
            string text = branchModalTitleText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                branchModalTitleByLang.Remove(lang);
            else
                branchModalTitleByLang[lang] = text;
        }

        private void SwitchBranchModalLanguage(string lang)
        {
            string next = QuestbookLocalizedText.NormalizeLang(lang);
            if (string.Equals(next, branchModalEditorLang, StringComparison.OrdinalIgnoreCase))
                return;

            FlushBranchModalTitleToLangMap();
            branchModalEditorLang = next;
            branchModalTitleText = branchModalTitleByLang.TryGetValue(next, out string? text)
                ? text
                : string.Empty;
            isBranchModalTitleFocused = true;
            branchModalIconSearchFocused = false;
            ResetTextCaretBlink();
        }

        private QuestbookLangTextPacket[] BuildBranchTitleI18nPackets()
        {
            FlushBranchModalTitleToLangMap();
            return branchModalTitleByLang
                .Where(static kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                .Select(static kv => new QuestbookLangTextPacket
                {
                    Lang = QuestbookLocalizedText.NormalizeLang(kv.Key),
                    Text = kv.Value.Trim()
                })
                .OrderBy(static p => p.Lang, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private string GetPrimaryBranchTitleFromMap()
        {
            FlushBranchModalTitleToLangMap();
            string lang = QuestbookLocalizedText.NormalizeLang(branchModalEditorLang);
            if (branchModalTitleByLang.TryGetValue(lang, out string? current) && !string.IsNullOrWhiteSpace(current))
                return current.Trim();
            foreach (string value in branchModalTitleByLang.Values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return branchModalTitleText.Trim();
        }

        private void SubmitBranchModal()
        {
            switch (branchModalMode)
            {
                case BranchModalMode.Add:
                    SubmitAddBranchModal();
                    break;
                case BranchModalMode.Rename:
                    SubmitRenameBranchModal();
                    break;
                case BranchModalMode.DeleteConfirm:
                    SubmitDeleteBranchModal();
                    break;
            }
        }

        private void SubmitAddBranchModal()
        {
            string title = GetPrimaryBranchTitleFromMap();
            if (string.IsNullOrWhiteSpace(title))
                return;

            QuestbookLangTextPacket[] titleI18n = BuildBranchTitleI18nPackets();
            // Stay on Branches after create — do not jump into quest-edit tools.
            pendingOpenAdminEditor = false;
            adminData.EditorSection = AdminEditorSection.Branches;
            MarkBranchMetadataDirty();
            QuestbookClientSystem.SendAdminAddCategory(new QuestbookAdminAddCategoryRequest
            {
                Title = title,
                HeaderTitle = title.ToUpperInvariant(),
                IconItemCode = branchModalSelectedIconItemCode,
                TitleI18n = titleI18n
            });
            CloseBranchModal();
        }

        private void SubmitRenameBranchModal()
        {
            if (string.IsNullOrWhiteSpace(branchModalTargetHeaderTitle))
                return;

            string title = GetPrimaryBranchTitleFromMap();
            if (string.IsNullOrWhiteSpace(title))
                return;

            QuestbookLangTextPacket[] titleI18n = BuildBranchTitleI18nPackets();
            pendingOpenAdminEditor = false;
            MarkBranchMetadataDirty();
            QuestbookClientSystem.SendAdminRenameCategory(new QuestbookAdminRenameCategoryRequest
            {
                CategoryHeaderTitle = branchModalTargetHeaderTitle,
                Title = title,
                HeaderTitle = title.ToUpperInvariant(),
                IconItemCode = branchModalSelectedIconItemCode,
                TitleI18n = titleI18n
            });
            CloseBranchModal();
        }

        private void SubmitDeleteBranchModal()
        {
            if (string.IsNullOrWhiteSpace(branchModalTargetHeaderTitle))
                return;

            MarkBranchMetadataDirty();
            pendingAdminRefreshAfterDelete = true;
            QuestbookClientSystem.SendAdminDeleteCategory(new QuestbookAdminDeleteCategoryRequest
            {
                CategoryHeaderTitle = branchModalTargetHeaderTitle
            });
            CloseBranchModal();
        }

        private bool TryHandleBranchModalMouseDown(double mouseX, double mouseY)
        {
            if (!isBranchModalOpen)
                return false;

            // Item catalog overlay (opened by clicking the preview icon slot).
            if (isBranchModalItemPickerOpen)
            {
                if (branchModalIconPickerCancelHitArea.Contains(mouseX, mouseY)
                    || ToScreenRect(branchModalIconPickerCancelHitArea).Contains(mouseX, mouseY))
                {
                    CloseBranchModalItemPicker();
                    ComposeDialog();
                    return true;
                }

                if (branchModalIconSearchHitArea.Contains(mouseX, mouseY)
                    || ToScreenRect(branchModalIconSearchHitArea).Contains(mouseX, mouseY))
                {
                    branchModalIconSearchFocused = true;
                    isBranchModalTitleFocused = false;
                    ResetTextCaretBlink();
                    RequestContentRefresh();
                    return true;
                }

                foreach ((ItemSlot slot, LayoutRect hitArea, string collectibleCode) in branchModalItemPickerSlots)
                {
                    LayoutRect screenHit = ToScreenRect(hitArea);
                    LayoutRect viewportScreen = branchModalIconViewportLocal.Width > 0
                        ? ToScreenRect(branchModalIconViewportLocal)
                        : screenHit;
                    LayoutRect visible = screenHit.Intersect(viewportScreen);
                    if (visible.IsEmpty || !visible.Contains(mouseX, mouseY) || slot.Itemstack == null)
                        continue;

                    branchModalSelectedIconItemCode = collectibleCode;
                    CloseBranchModalItemPicker();
                    ComposeDialog();
                    return true;
                }

                bool inPickerPanel = branchModalIconPickerPanelLocal.Contains(mouseX, mouseY)
                    || ToScreenRect(branchModalIconPickerPanelLocal).Contains(mouseX, mouseY);
                if (inPickerPanel)
                {
                    if (branchModalIconSearchFocused)
                    {
                        branchModalIconSearchFocused = false;
                        RequestContentRefresh();
                    }

                    return true;
                }

                // Click on langs / outside picker region: close catalog, keep branch modal.
                // Language chips still work after close on next click; this click only dismisses picker.
                CloseBranchModalItemPicker();
                ComposeDialog();
                return true;
            }

            if (branchModalCancelButtonHitArea.Contains(mouseX, mouseY))
            {
                CloseBranchModal();
                ComposeDialog();
                return true;
            }

            if (branchModalPrimaryButtonHitArea.Contains(mouseX, mouseY))
            {
                SubmitBranchModal();
                ComposeDialog();
                return true;
            }

            for (int i = 0; i < branchModalLangButtonHitAreas.Length && i < branchModalLangCodes.Length; i++)
            {
                if (!branchModalLangButtonHitAreas[i].Contains(mouseX, mouseY))
                    continue;

                SwitchBranchModalLanguage(branchModalLangCodes[i]);
                ComposeDialog();
                return true;
            }

            if (branchModalTitleInputHitArea.Contains(mouseX, mouseY))
            {
                isBranchModalTitleFocused = true;
                branchModalIconSearchFocused = false;
                ResetTextCaretBlink();
                RequestContentRefresh();
                return true;
            }

            if (branchModalIconPreviewHitArea.Contains(mouseX, mouseY))
            {
                OpenBranchModalItemPicker();
                ComposeDialog();
                return true;
            }

            if (!branchModalPanelHitArea.Contains(mouseX, mouseY))
            {
                CloseBranchModal();
                ComposeDialog();
                return true;
            }

            if (isBranchModalTitleFocused || branchModalIconSearchFocused)
            {
                isBranchModalTitleFocused = false;
                branchModalIconSearchFocused = false;
                RequestContentRefresh();
            }

            return true;
        }

        private bool TryHandleBranchModalMouseWheel(MouseWheelEventArgs args)
        {
            if (!isBranchModalOpen || !isBranchModalItemPickerOpen)
                return false;
            if (branchModalIconViewportLocal.Width <= 0)
                return false;

            int mouseX = capi.Input.MouseX;
            int mouseY = capi.Input.MouseY;
            if (!ToScreenRect(branchModalIconViewportLocal).Contains(mouseX, mouseY)
                && !branchModalIconViewportLocal.Contains(mouseX, mouseY)
                && !ToScreenRect(branchModalIconPickerPanelLocal).Contains(mouseX, mouseY))
            {
                return false;
            }

            float wheelDelta = args.deltaPrecise != 0 ? args.deltaPrecise : args.delta;
            if (wheelDelta == 0)
                return false;

            double step = QuestbookGuiLayout.AddBranchModalItemSlotSize * currentFitScale * 0.9;
            double direction = wheelDelta > 0 ? -1 : 1;
            branchModalIconScrollOffset = System.Math.Max(0, branchModalIconScrollOffset + (direction * step));
            RequestContentRefresh();
            return true;
        }

        private void UpdateBranchModalHover(double mouseX, double mouseY)
        {
            if (!isBranchModalOpen)
                return;

            UpdateHover(ref isBranchModalPrimaryHovered, branchModalPrimaryButtonHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isBranchModalCancelHovered, branchModalCancelButtonHitArea.Contains(mouseX, mouseY));
        }

        private bool HandleBranchModalKeyDown(KeyEvent args)
        {
            if (!isBranchModalOpen)
                return false;

            GlKeys key = (GlKeys)args.KeyCode;

            if (key == GlKeys.Escape)
            {
                if (isBranchModalItemPickerOpen)
                    CloseBranchModalItemPicker();
                else
                    CloseBranchModal();
                ComposeDialog();
                args.Handled = true;
                return true;
            }

            if (key == GlKeys.Enter || key == GlKeys.KeypadEnter)
            {
                // Enter in search just confirms selection path via Create/Save.
                SubmitBranchModal();
                ComposeDialog();
                args.Handled = true;
                return true;
            }

            if (branchModalMode == BranchModalMode.DeleteConfirm)
            {
                args.Handled = true;
                return true;
            }

            if ((isBranchModalTitleFocused || branchModalIconSearchFocused)
                && TryApplyBranchModalTextKey(args))
            {
                args.Handled = true;
                return true;
            }

            args.Handled = true;
            return true;
        }

        private bool HandleBranchModalKeyPress(KeyEvent args)
        {
            if (!isBranchModalOpen || branchModalMode == BranchModalMode.DeleteConfirm)
                return false;

            if (!isBranchModalTitleFocused && !branchModalIconSearchFocused)
                return true;

            if (TryApplyBranchModalTextKey(args))
            {
                args.Handled = true;
                return true;
            }

            return true;
        }

        private bool TryApplyBranchModalTextKey(KeyEvent args)
        {
            GlKeys key = (GlKeys)args.KeyCode;
            bool editingSearch = branchModalIconSearchFocused && !isBranchModalTitleFocused;

            if (key == GlKeys.Back)
            {
                if (editingSearch)
                {
                    if (branchModalIconSearchText.Length > 0)
                        branchModalIconSearchText = branchModalIconSearchText[..^1];
                    branchModalIconScrollOffset = 0;
                }
                else if (branchModalTitleText.Length > 0)
                {
                    branchModalTitleText = branchModalTitleText[..^1];
                }

                ResetTextCaretBlink();
                RequestContentRefresh();
                return true;
            }

            if (key == GlKeys.V && args.CtrlPressed)
            {
                string clipboard = capi.Input.ClipboardText?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(clipboard))
                {
                    if (editingSearch)
                    {
                        branchModalIconSearchText = clipboard.Length <= 64 ? clipboard : clipboard[..64];
                        branchModalIconScrollOffset = 0;
                    }
                    else
                    {
                        branchModalTitleText = TruncateBranchTitle(clipboard);
                    }

                    ResetTextCaretBlink();
                    RequestContentRefresh();
                    return true;
                }
            }

            if (args.KeyChar != '\0' && args.KeyChar != '\t' && args.KeyChar != '\n' && args.KeyChar != '\r')
            {
                if (editingSearch)
                {
                    if (branchModalIconSearchText.Length < 64)
                        branchModalIconSearchText += args.KeyChar;
                    branchModalIconScrollOffset = 0;
                }
                else if (branchModalTitleText.Length < 80)
                {
                    branchModalTitleText += args.KeyChar;
                }

                ResetTextCaretBlink();
                RequestContentRefresh();
                return true;
            }

            return false;
        }

        private static string TruncateBranchTitle(string value)
        {
            value = value.Trim();
            return value.Length <= 80 ? value : value[..80];
        }

        private void DrawBranchModal(Cairo.Context ctx, double fitScale, double screenX, double screenY)
        {
            if (!isBranchModalOpen)
                return;

            branchModalOverlayHitArea = GetQuestbookDialogContentRect();

            // Group 1208: fixed design size 558×484 (group_1192.png frame).
            double designW = QuestbookGuiLayout.AddBranchModalWidth;
            double designH = QuestbookGuiLayout.AddBranchModalHeight;
            double modalWidth = designW * fitScale;
            double modalHeight = designH * fitScale;
            double unit = modalWidth / designW;
            double D(double v) => v * unit;

            // Centered in the right-hand quest graph area.
            double rightX = QuestbookGuiLayout.GraphViewportX * fitScale;
            double rightY = QuestbookGuiLayout.GraphViewportY * fitScale;
            double rightW = QuestbookGuiLayout.GraphViewportWidth * fitScale;
            double rightH = QuestbookGuiLayout.GraphViewportHeight * fitScale;
            double modalX = rightX + ((rightW - modalWidth) / 2);
            double modalY = rightY + ((rightH - modalHeight) / 2);
            branchModalPanelHitArea = new LayoutRect(modalX, modalY, modalWidth, modalHeight).Offset(screenX, screenY);

            ImageSurface? modalSurface = GetTextureSurface(QuestbookGuiLayout.AddBranchModalTexture);
            if (modalSurface != null)
                DrawImageSurface(ctx, modalSurface, modalX, modalY, modalWidth, modalHeight);
            else
                FillRectangle(ctx, modalX, modalY, modalWidth, modalHeight, QuestbookGuiLayout.ModalBorderColor);

            // Title — centered (Group 1208 ~y70–91).
            string modalTitle = branchModalMode switch
            {
                BranchModalMode.Rename => QuestbookLang.GetLocal("admin.rename_branch.title"),
                BranchModalMode.DeleteConfirm => QuestbookLang.GetLocal("admin.delete_branch.title"),
                _ => QuestbookLang.GetLocal("admin.add_branch.title")
            };
            CairoFont titleFont = CreateMontserratFont(D(18), QuestbookGuiLayout.AdminModalTitleColor);
            double titleW = MeasureTextWidth(titleFont, modalTitle);
            DrawText(
                ctx,
                titleFont,
                modalTitle,
                modalX + ((modalWidth - titleW) / 2),
                GetTextBaselineY(titleFont, modalY + D(QuestbookGuiLayout.AddBranchModalTitleY), D(22), D(22)));

            // Subtitle under title — Group 1208 #555555 “Name [RU]” with active editor language.
            if (branchModalMode != BranchModalMode.DeleteConfirm)
            {
                string langCode = QuestbookLocalizedText.NormalizeLang(branchModalEditorLang).ToUpperInvariant();
                string subtitle = $"Name: [{langCode}]";
                CairoFont subtitleFont = CreateMontserratFont(D(13), [0x55 / 255.0, 0x55 / 255.0, 0x55 / 255.0, 1.0]);
                double subW = MeasureTextWidth(subtitleFont, subtitle);
                DrawText(
                    ctx,
                    subtitleFont,
                    subtitle,
                    modalX + ((modalWidth - subW) / 2),
                    GetTextBaselineY(subtitleFont, modalY + D(QuestbookGuiLayout.AddBranchModalSubtitleY), D(16), D(16)));
            }

            if (branchModalMode == BranchModalMode.DeleteConfirm)
            {
                branchModalTitleInputHitArea = new LayoutRect(0, 0, 0, 0);
                branchModalIconPreviewHitArea = new LayoutRect(0, 0, 0, 0);
                branchModalIconSearchHitArea = new LayoutRect(0, 0, 0, 0);
                branchModalIconViewportLocal = new LayoutRect(0, 0, 0, 0);
                branchModalIconPickerPanelLocal = new LayoutRect(0, 0, 0, 0);
                branchModalIconPickerCancelHitArea = new LayoutRect(0, 0, 0, 0);
                branchModalLangCodes = [];
                branchModalLangButtonHitAreas = [];
                branchModalItemPickerSlots = [];
                isBranchModalItemPickerOpen = false;
                CairoFont messageFont = CreateMontserratFont(D(14), QuestbookGuiLayout.AdminModalChipTextColor);
                string branchName = GetEditableCategoryTitle(GetSelectedCategory());
                string message = QuestbookLang.GetLocal("admin.delete_branch.confirm", branchName);
                double msgW = MeasureTextWidth(messageFont, message);
                DrawText(
                    ctx,
                    messageFont,
                    message,
                    modalX + ((modalWidth - msgW) / 2),
                    GetTextBaselineY(messageFont, modalY + D(220), D(20), D(20)));
            }
            else
            {
                // Language chips — always visible (Group 1208).
                double langOriginX = modalX + D(QuestbookGuiLayout.AddBranchModalLangX);
                double langOriginY = modalY + D(QuestbookGuiLayout.AddBranchModalLangY);
                DrawBranchModalLanguageBar(ctx, unit, langOriginX, langOriginY);

                DrawGroup767BorderCairo(
                    ctx,
                    modalX + D(QuestbookGuiLayout.AddBranchModalLangFrameX),
                    modalY + D(QuestbookGuiLayout.AddBranchModalLangFrameY),
                    D(QuestbookGuiLayout.AddBranchModalLangFrameW),
                    D(QuestbookGuiLayout.AddBranchModalLangFrameH),
                    QuestbookGuiLayout.AddBranchModalLangFrameThickness);

                if (isBranchModalItemPickerOpen)
                {
                    // Icon picker replaces name field + CREATE/CANCEL in-place (no separate overlay).
                    branchModalTitleInputHitArea = new LayoutRect(0, 0, 0, 0);
                    branchModalIconPreviewHitArea = new LayoutRect(0, 0, 0, 0);
                    branchModalPrimaryButtonHitArea = new LayoutRect(0, 0, 0, 0);
                    branchModalCancelButtonHitArea = new LayoutRect(0, 0, 0, 0);
                    isBranchModalTitleFocused = false;

                    // Region from name-label Y down to bottom of buttons (+30 design px taller).
                    double pickerX = modalX + D(QuestbookGuiLayout.AddBranchModalNameX);
                    double pickerY = modalY + D(QuestbookGuiLayout.AddBranchModalNameLabelY);
                    double pickerW = D(QuestbookGuiLayout.AddBranchModalLangFrameW);
                    double pickerBottom = modalY + D(QuestbookGuiLayout.AddBranchModalButtonY
                        + QuestbookGuiLayout.AddBranchModalButtonHeight);
                    double pickerH = System.Math.Max(D(120), pickerBottom - pickerY) + D(30);
                    // Keep inside modal frame.
                    double maxBottom = modalY + modalHeight - D(28);
                    if (pickerY + pickerH > maxBottom)
                        pickerH = System.Math.Max(D(120), maxBottom - pickerY);
                    DrawBranchModalItemCatalogInPlace(ctx, unit, pickerX, pickerY, pickerW, pickerH);
                }
                else
                {
                    branchModalItemPickerSlots = [];
                    branchModalIconSearchHitArea = new LayoutRect(0, 0, 0, 0);
                    branchModalIconViewportLocal = new LayoutRect(0, 0, 0, 0);
                    branchModalIconPickerPanelLocal = new LayoutRect(0, 0, 0, 0);
                    branchModalIconPickerCancelHitArea = new LayoutRect(0, 0, 0, 0);

                    // Name field + icon slot (71.5 / 447.5, y=314.5) — positions first for label align.
                    double nameX = modalX + D(QuestbookGuiLayout.AddBranchModalNameX);
                    double nameY = modalY + D(QuestbookGuiLayout.AddBranchModalNameY);
                    double nameW = D(QuestbookGuiLayout.AddBranchModalNameWidth);
                    double nameH = D(QuestbookGuiLayout.AddBranchModalNameHeight);
                    double iconX = modalX + D(QuestbookGuiLayout.AddBranchModalIconX);
                    double iconSize = D(QuestbookGuiLayout.AddBranchModalIconSize);

                    // Labels — muted gray #AEAEAE: name left-aligned, icon hint right-aligned to slot.
                    double[] labelGray = [0xAE / 255.0, 0xAE / 255.0, 0xAE / 255.0, 1.0];
                    CairoFont labelFont = CreateMontserratFont(D(12), labelGray);
                    double labelY = modalY + D(QuestbookGuiLayout.AddBranchModalNameLabelY);
                    double labelBaseline = GetTextBaselineY(labelFont, labelY, D(14), D(14));
                    string nameLabel = QuestbookLang.GetLocal("admin.add_branch.name_label");
                    string iconLabel = QuestbookLang.GetLocal("admin.branch_icon.label");
                    DrawText(ctx, labelFont, nameLabel, nameX, labelBaseline);
                    double iconLabelW = MeasureTextWidth(labelFont, iconLabel);
                    DrawText(ctx, labelFont, iconLabel, iconX + iconSize - iconLabelW, labelBaseline);
                    branchModalTitleInputHitArea = new LayoutRect(nameX, nameY, nameW, nameH);

                    DrawQuestEditTextField(ctx, nameX, nameY, nameW, nameH, D(5.5), System.Math.Max(1.0, unit),
                        isBranchModalTitleFocused);

                    bool titleEmpty = string.IsNullOrWhiteSpace(branchModalTitleText);
                    string displayText = titleEmpty && !isBranchModalTitleFocused
                        ? QuestbookLang.GetLocal("admin.add_branch.name_placeholder")
                        : branchModalTitleText;
                    double[] inputColor = titleEmpty && !isBranchModalTitleFocused
                        ? QuestbookGuiLayout.AdminPanelPlaceholderColor
                        : QuestbookGuiLayout.AdminModalChipTextColor;
                    CairoFont inputFont = CreateMontserratFont(D(13), inputColor);
                    double textX = nameX + D(10);
                    DrawText(ctx, inputFont, displayText, textX,
                        GetTextBaselineY(inputFont, nameY, nameH, nameH));
                    if (isBranchModalTitleFocused)
                    {
                        DrawTextCaret(ctx, inputFont, branchModalTitleText, textX, nameY, nameH, inputColor);
                    }

                    DrawBranchModalIconSlot(ctx, unit, modalX, modalY);

                    // CREATE / CANCEL — group_714.png at (71,374) and (284,374), 203×40.
                    ImageSurface? btnTex = GetTextureSurface(QuestbookGuiLayout.AddBranchModalButtonTexture);
                    double btnY = modalY + D(QuestbookGuiLayout.AddBranchModalButtonY);
                    double btnW = D(QuestbookGuiLayout.AddBranchModalButtonWidth);
                    double btnH = D(QuestbookGuiLayout.AddBranchModalButtonHeight);
                    branchModalPrimaryButtonHitArea = new LayoutRect(
                        modalX + D(QuestbookGuiLayout.AddBranchModalPrimaryButtonX), btnY, btnW, btnH);
                    branchModalCancelButtonHitArea = new LayoutRect(
                        modalX + D(QuestbookGuiLayout.AddBranchModalCancelButtonX), btnY, btnW, btnH);

                    string primaryLabel = branchModalMode switch
                    {
                        BranchModalMode.Rename => QuestbookLang.GetLocal("admin.rename_branch.save"),
                        _ => QuestbookLang.GetLocal("admin.add_branch.create")
                    };

                    DrawBranchModalButton(ctx, unit, btnTex, branchModalPrimaryButtonHitArea, primaryLabel,
                        isBranchModalPrimaryHovered, QuestbookGuiLayout.AdminSaveButtonColor);
                    DrawBranchModalButton(ctx, unit, btnTex, branchModalCancelButtonHitArea,
                        QuestbookLang.GetLocal("admin.add_branch.cancel"), isBranchModalCancelHovered,
                        QuestbookGuiLayout.AdminClearButtonColor);
                }
            }

            // Delete-confirm still needs CREATE/CANCEL buttons below the message.
            if (branchModalMode == BranchModalMode.DeleteConfirm)
            {
                ImageSurface? btnTex = GetTextureSurface(QuestbookGuiLayout.AddBranchModalButtonTexture);
                double btnY = modalY + D(QuestbookGuiLayout.AddBranchModalButtonY);
                double btnW = D(QuestbookGuiLayout.AddBranchModalButtonWidth);
                double btnH = D(QuestbookGuiLayout.AddBranchModalButtonHeight);
                branchModalPrimaryButtonHitArea = new LayoutRect(
                    modalX + D(QuestbookGuiLayout.AddBranchModalPrimaryButtonX), btnY, btnW, btnH);
                branchModalCancelButtonHitArea = new LayoutRect(
                    modalX + D(QuestbookGuiLayout.AddBranchModalCancelButtonX), btnY, btnW, btnH);

                DrawBranchModalButton(ctx, unit, btnTex, branchModalPrimaryButtonHitArea,
                    QuestbookLang.GetLocal("admin.delete_branch.confirm_button"),
                    isBranchModalPrimaryHovered, QuestbookGuiLayout.AdminClearButtonColor);
                DrawBranchModalButton(ctx, unit, btnTex, branchModalCancelButtonHitArea,
                    QuestbookLang.GetLocal("admin.add_branch.cancel"), isBranchModalCancelHovered,
                    QuestbookGuiLayout.AdminClearButtonColor);
            }

            if (!branchModalTitleInputHitArea.IsEmpty)
                branchModalTitleInputHitArea = branchModalTitleInputHitArea.Offset(screenX, screenY);
            if (!branchModalIconPreviewHitArea.IsEmpty)
                branchModalIconPreviewHitArea = branchModalIconPreviewHitArea.Offset(screenX, screenY);
            if (!branchModalIconSearchHitArea.IsEmpty)
                branchModalIconSearchHitArea = branchModalIconSearchHitArea.Offset(screenX, screenY);
            if (!branchModalIconPickerCancelHitArea.IsEmpty)
                branchModalIconPickerCancelHitArea = branchModalIconPickerCancelHitArea.Offset(screenX, screenY);
            for (int i = 0; i < branchModalLangButtonHitAreas.Length; i++)
                branchModalLangButtonHitAreas[i] = branchModalLangButtonHitAreas[i].Offset(screenX, screenY);
            branchModalPrimaryButtonHitArea = branchModalPrimaryButtonHitArea.Offset(screenX, screenY);
            branchModalCancelButtonHitArea = branchModalCancelButtonHitArea.Offset(screenX, screenY);
        }

        /// <summary>Language chip grid — Group 1208: 46×28, step 49×31, 8 columns.</summary>
        private void DrawBranchModalLanguageBar(Cairo.Context ctx, double unit, double originX, double originY)
        {
            string[] langs = GetRegisteredLanguageCodes();
            if (langs.Length == 0)
                langs = ["en"];

            foreach (string existing in branchModalTitleByLang.Keys)
            {
                string code = QuestbookLocalizedText.NormalizeLang(existing);
                if (!langs.Contains(code, StringComparer.OrdinalIgnoreCase))
                    langs = langs.Append(code).OrderBy(static c => c, StringComparer.OrdinalIgnoreCase).ToArray();
            }

            string active = QuestbookLocalizedText.NormalizeLang(branchModalEditorLang);
            if (!langs.Contains(active, StringComparer.OrdinalIgnoreCase))
                langs = langs.Append(active).OrderBy(static c => c, StringComparer.OrdinalIgnoreCase).ToArray();

            branchModalLangCodes = langs;
            branchModalLangButtonHitAreas = new LayoutRect[langs.Length];

            double chipW = QuestbookGuiLayout.AddBranchModalLangChipWidth * unit;
            double chipH = QuestbookGuiLayout.AddBranchModalLangChipHeight * unit;
            double stepX = QuestbookGuiLayout.AddBranchModalLangStepX * unit;
            double stepY = QuestbookGuiLayout.AddBranchModalLangStepY * unit;
            int columns = QuestbookGuiLayout.AddBranchModalLangColumns;
            double radius = 5.5 * unit;
            double borderW = System.Math.Max(1.0, unit);

            for (int i = 0; i < langs.Length; i++)
            {
                string lang = langs[i];
                int col = i % columns;
                int row = i / columns;
                double bx = originX + (col * stepX);
                double by = originY + (row * stepY);
                LayoutRect rect = new(bx, by, chipW, chipH);
                branchModalLangButtonHitAreas[i] = rect;

                bool isActive = string.Equals(lang, branchModalEditorLang, StringComparison.OrdinalIgnoreCase);
                bool hasText = branchModalTitleByLang.TryGetValue(lang, out string? text)
                    && !string.IsNullOrWhiteSpace(text);
                if (isActive && !string.IsNullOrWhiteSpace(branchModalTitleText))
                    hasText = true;

                FillRoundedRectangle(ctx, rect.X, rect.Y, rect.Width, rect.Height, radius,
                    QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(ctx, rect.X, rect.Y, rect.Width, rect.Height, radius,
                    isActive ? borderW * 1.6 : borderW,
                    isActive
                        ? QuestbookGuiLayout.AdminTileActiveContentColor
                        : QuestbookGuiLayout.AdminTileBorderColor);

                string label = lang.ToUpperInvariant();
                if (hasText && !isActive)
                    label += " ·";

                double[] color = isActive
                    ? QuestbookGuiLayout.AdminTileActiveContentColor
                    : QuestbookGuiLayout.AdminModalChipTextColor;
                CairoFont chipFont = CreateMontserratFont(System.Math.Clamp(chipH * 0.42, 10 * unit, 13 * unit), color);
                DrawCenteredText(ctx, chipFont, label, rect);
            }
        }

        /// <summary>Icon slot next to name field (Group 1208: 35×35 at 447.5, 314.5).</summary>
        private void DrawBranchModalIconSlot(Cairo.Context ctx, double unit, double modalX, double modalY)
        {
            if (!isBranchModalItemPickerOpen)
            {
                branchModalItemPickerSlots = [];
                branchModalIconSearchHitArea = new LayoutRect(0, 0, 0, 0);
                branchModalIconViewportLocal = new LayoutRect(0, 0, 0, 0);
                branchModalIconPickerPanelLocal = new LayoutRect(0, 0, 0, 0);
                branchModalIconPickerCancelHitArea = new LayoutRect(0, 0, 0, 0);
            }

            double size = QuestbookGuiLayout.AddBranchModalIconSize * unit;
            double px = modalX + QuestbookGuiLayout.AddBranchModalIconX * unit;
            double py = modalY + QuestbookGuiLayout.AddBranchModalIconY * unit;
            LayoutRect previewRect = new(px, py, size, size);
            branchModalIconPreviewHitArea = previewRect;

            double radius = 5.5 * unit;
            double borderW = System.Math.Max(1.0, unit);
            FillRoundedRectangle(ctx, px, py, size, size, radius, QuestbookGuiLayout.AdminTileBackgroundColor);
            StrokeRoundedRectangle(ctx, px, py, size, size, radius,
                isBranchModalItemPickerOpen ? borderW * 1.8 : borderW,
                isBranchModalItemPickerOpen
                    ? QuestbookGuiLayout.AdminTileActiveContentColor
                    : QuestbookGuiLayout.AdminTileBorderColor);

            // Plus cross (SVG small + inside slot).
            if (string.IsNullOrWhiteSpace(branchModalSelectedIconItemCode) || isBranchModalItemPickerOpen)
            {
                double[] plusColor = QuestbookGuiLayout.AdminTileBorderColor;
                ctx.SetSourceRGBA(plusColor[0], plusColor[1], plusColor[2], plusColor[3]);
                ctx.LineWidth = System.Math.Max(1.5, unit * 1.5);
                ctx.LineCap = Cairo.LineCap.Round;
                double cx = px + size / 2;
                double cy = py + size / 2;
                double arm = size * 0.18;
                ctx.NewPath();
                ctx.MoveTo(cx - arm, cy);
                ctx.LineTo(cx + arm, cy);
                ctx.MoveTo(cx, cy - arm);
                ctx.LineTo(cx, cy + arm);
                ctx.Stroke();
            }

            if (!isBranchModalItemPickerOpen
                && !string.IsNullOrWhiteSpace(branchModalSelectedIconItemCode))
            {
                branchModalIconRenderRequests.Add(new QuestItemIconRenderRequest(
                    branchModalSelectedIconItemCode,
                    previewRect,
                    false,
                    0,
                    QuestbookItemIconContext.Modal));
            }
        }

        /// <summary>
        /// Icon catalog drawn in-place where name field + CREATE/CANCEL normally sit.
        /// Same chrome as quest item picker (search + CANCEL + grid + scrollbar).
        /// </summary>
        private void DrawBranchModalItemCatalogInPlace(
            Cairo.Context ctx,
            double unit,
            double panelX,
            double panelY,
            double panelW,
            double panelH)
        {
            branchModalIconPickerPanelLocal = new LayoutRect(panelX, panelY, panelW, panelH);

            double radius = QuestbookGuiLayout.QuestEditModalChipRadius * unit;
            double borderW = System.Math.Max(1.0, unit);
            FillRoundedRectangle(ctx, panelX, panelY, panelW, panelH, radius,
                QuestbookGuiLayout.AdminTileBackgroundColor);
            StrokeRoundedRectangle(ctx, panelX, panelY, panelW, panelH, radius, borderW,
                QuestbookGuiLayout.AdminTileBorderColor);

            // Scale quest-picker design into this panel.
            double designW = QuestbookGuiLayout.QuestEditModalContentWidth;
            double designH = QuestbookGuiLayout.QuestEditModalPickerPanelHeight;
            double u = System.Math.Min(panelW / designW, panelH / designH);

            double searchH = QuestbookGuiLayout.QuestEditModalPickerSearchHeight * u;
            double searchInsetX = QuestbookGuiLayout.QuestEditModalPickerSearchInsetX * u;
            double searchInsetY = QuestbookGuiLayout.QuestEditModalPickerSearchInsetY * u;
            // Fit search + cancel into panel width.
            double cancelW = QuestbookGuiLayout.QuestEditModalPickerCancelWidth * u;
            double gap = 8 * u;
            double searchX = panelX + searchInsetX;
            double searchY = panelY + searchInsetY;
            double searchW = System.Math.Max(40 * u, panelW - searchInsetX * 2 - cancelW - gap);
            double cancelX = searchX + searchW + gap;

            branchModalIconSearchHitArea = new LayoutRect(searchX, searchY, searchW, searchH);
            branchModalIconPickerCancelHitArea = new LayoutRect(cancelX, searchY, cancelW, searchH);

            DrawQuestEditTextField(ctx, searchX, searchY, searchW, searchH, radius, borderW,
                branchModalIconSearchFocused);

            CairoFont searchFont = CreateMontserratFont(12 * u,
                string.IsNullOrEmpty(branchModalIconSearchText) && !branchModalIconSearchFocused
                    ? QuestbookGuiLayout.AdminPanelPlaceholderColor
                    : QuestbookGuiLayout.AdminModalChipTextColor);
            string searchDisplay = string.IsNullOrEmpty(branchModalIconSearchText) && !branchModalIconSearchFocused
                ? QuestbookLang.GetLocal("admin.quest_edit.item_search_placeholder")
                : branchModalIconSearchText;
            double textPad = 8 * u;
            DrawText(ctx, searchFont, searchDisplay, searchX + textPad,
                GetTextBaselineY(searchFont, searchY, searchH, searchH));
            if (branchModalIconSearchFocused)
            {
                DrawTextCaret(ctx, searchFont, branchModalIconSearchText,
                    searchX + textPad, searchY, searchH, QuestbookGuiLayout.AdminModalChipTextColor);
            }

            FillRoundedRectangle(ctx, cancelX, searchY, cancelW, searchH, radius,
                QuestbookGuiLayout.AdminTileBackgroundColor);
            StrokeRoundedRectangle(ctx, cancelX, searchY, cancelW, searchH, radius, borderW,
                QuestbookGuiLayout.AdminTileBorderColor);
            CairoFont cancelFont = CreateMontserratFont(12 * u, [0xAE / 255.0, 0xAE / 255.0, 0xAE / 255.0, 1.0]);
            DrawCenteredText(ctx, cancelFont, QuestbookLang.GetLocal("admin.quest_edit.picker_cancel"),
                branchModalIconPickerCancelHitArea);

            // Grid fills remaining height under search — fewer, larger slots than quest picker.
            double tileGap = System.Math.Max(6 * u, QuestbookGuiLayout.QuestEditModalPickerSlotGap * u * 1.4);
            double listTop = searchY + searchH + (10 * u);
            double listLeft = panelX + searchInsetX;
            double listRight = panelX + panelW - searchInsetX;
            double listWidth = System.Math.Max(tileGap * 4, listRight - listLeft);
            double listHeight = System.Math.Max(24 * u, panelY + panelH - listTop - 6 * u);

            // Cap columns so icons stay chunky (~6× across, ~48–72px tiles).
            const int maxColumns = 6;
            double preferredTile = 56 * u;
            int columns = System.Math.Max(3, (int)System.Math.Floor((listWidth + tileGap) / (preferredTile + tileGap)));
            columns = System.Math.Min(columns, maxColumns);
            double tileStep = listWidth / columns;
            double tileSize = System.Math.Max(40 * u, tileStep - tileGap);
            double tileR = System.Math.Min(radius, tileSize * 0.16);

            branchModalIconViewportLocal = new LayoutRect(listLeft, listTop, listWidth, listHeight);

            IReadOnlyList<(string Code, string Label, DummySlot Slot)> catalog =
                GetItemCatalogEntries(branchModalIconSearchText);
            int rows = System.Math.Max(1, (int)System.Math.Ceiling(catalog.Count / (double)columns));
            double contentH = rows * tileStep;
            double maxScroll = System.Math.Max(0, contentH - listHeight);
            branchModalIconScrollOffset = System.Math.Clamp(branchModalIconScrollOffset, 0, maxScroll);

            int firstVisibleRow = System.Math.Max(0, (int)System.Math.Floor(branchModalIconScrollOffset / tileStep) - 1);
            int visibleRowCount = (int)System.Math.Ceiling(listHeight / tileStep) + 2;
            int firstIndex = firstVisibleRow * columns;
            int lastIndex = System.Math.Min(catalog.Count, (firstVisibleRow + visibleRowCount) * columns);

            ctx.Save();
            ctx.Rectangle(listLeft, listTop, listWidth, listHeight);
            ctx.Clip();

            var pickerSlots = new List<(ItemSlot Slot, LayoutRect HitArea, string CollectibleCode)>(
                System.Math.Max(16, lastIndex - firstIndex));

            for (int i = firstIndex; i < lastIndex; i++)
            {
                int col = i % columns;
                int row = i / columns;
                double cellX = listLeft + (col * tileStep);
                double cellY = listTop + (row * tileStep) - branchModalIconScrollOffset;
                if (cellY + tileSize < listTop || cellY > listTop + listHeight)
                    continue;

                (string collectibleCode, string _, DummySlot slot) = catalog[i];
                LayoutRect tileRect = new(cellX, cellY, tileSize, tileSize);
                bool selected = string.Equals(
                    collectibleCode,
                    branchModalSelectedIconItemCode,
                    StringComparison.OrdinalIgnoreCase);

                FillRoundedRectangle(ctx, tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height, tileR,
                    selected
                        ? QuestbookGuiLayout.AdminTileActiveBackgroundColor
                        : QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(ctx, tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height, tileR,
                    selected ? borderW * 1.8 : borderW,
                    selected
                        ? QuestbookGuiLayout.AdminTileActiveContentColor
                        : QuestbookGuiLayout.AdminTileBorderColor);

                pickerSlots.Add((slot, tileRect, collectibleCode));
            }

            ctx.Restore();

            if (maxScroll > 0)
            {
                double trackW = System.Math.Max(3 * u, 4 * u);
                double trackX = listLeft + listWidth - trackW;
                double thumbH = System.Math.Max(16 * u, listHeight * (listHeight / contentH));
                double thumbTravel = System.Math.Max(1, listHeight - thumbH);
                double thumbY = listTop + ((branchModalIconScrollOffset / maxScroll) * thumbTravel);
                FillRoundedRectangle(ctx, trackX, listTop, trackW, listHeight, trackW * 0.5,
                    QuestbookGuiLayout.AdminTileBorderColor);
                FillRoundedRectangle(ctx, trackX, thumbY, trackW, thumbH, trackW * 0.5,
                    [0x74 / 255.0, 0x74 / 255.0, 0x74 / 255.0, 1.0]);
            }

            if (catalog.Count == 0)
            {
                CairoFont emptyFont = CreateMontserratFont(12 * u, QuestbookGuiLayout.AdminPanelPlaceholderColor);
                DrawCenteredText(
                    ctx,
                    emptyFont,
                    QuestbookLang.GetLocal("admin.quest_edit.item_search_empty"),
                    new LayoutRect(listLeft, listTop, listWidth, listHeight));
            }

            branchModalItemPickerSlots = pickerSlots.ToArray();
        }

        private void DrawBranchModalButton(
            Cairo.Context ctx,
            double unit,
            ImageSurface? buttonSurface,
            LayoutRect area,
            string label,
            bool hovered,
            double[]? accent)
        {
            // Group 714.png wood plaque (203×40 design).
            if (buttonSurface != null)
                DrawImageSurface(ctx, buttonSurface, area.X, area.Y, area.Width, area.Height);
            else
            {
                FillRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, 4 * unit,
                    QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, 4 * unit,
                    System.Math.Max(1.0, unit), QuestbookGuiLayout.AdminTileBorderColor);
            }

            double[] color = hovered
                ? accent ?? QuestbookGuiLayout.AdminTileActiveContentColor
                : QuestbookGuiLayout.AdminModalChipTextColor;
            CairoFont font = CreateMontserratFont(System.Math.Clamp(area.Height * 0.38, 11 * unit, 15 * unit), color);
            double textWidth = MeasureTextWidth(font, label);
            DrawText(ctx, font, label, area.X + ((area.Width - textWidth) / 2),
                GetTextBaselineY(font, area.Y, area.Height, area.Height));
        }

        private void MarkBranchMetadataDirty()
        {
            preAdminSnapshot = null;
        }

        public void HandleAdminResponse(QuestbookAdminResponse response)
        {
            if (!response.Success)
                return;

            categories = dataManager.Categories;

            if (pendingAdminRefreshAfterDelete)
            {
                pendingAdminRefreshAfterDelete = false;
                selectedCategoryIndex = System.Math.Clamp(selectedCategoryIndex, 0, System.Math.Max(0, categories.Length - 1));
                shouldCenterOnStartNode = true;
                ComposeDialog();
                return;
            }

            if (string.IsNullOrWhiteSpace(response.CategoryHeaderTitle))
                return;

            TrySelectCategoryByHeader(response.CategoryHeaderTitle, pendingOpenAdminEditor);
            pendingOpenAdminEditor = false;
        }

        private void TrySelectCategoryByHeader(string headerTitle, bool openEditor)
        {
            for (int i = 0; i < categories.Length; i++)
            {
                if (categories[i].HeaderTitle != headerTitle)
                    continue;

                selectedCategoryIndex = i;
                shouldCenterOnStartNode = true;
                dataManager.EnsureCategoryContentLoaded(headerTitle, includeI18n: openEditor || adminData.IsAdminPanelOpen);
                if (openEditor)
                {
                    if (!adminData.IsAdminPanelOpen)
                        OpenAdminEditor();
                    // Prefer Branches when the admin panel is already on that section
                    // (e.g. after creating a branch); only default to Quests for a fresh open.
                    if (adminData.EditorSection != AdminEditorSection.Branches)
                        adminData.EditorSection = AdminEditorSection.Quests;
                }

                ComposeDialog();
                return;
            }

            pendingSelectCategoryHeaderTitle = headerTitle;
            pendingOpenAdminEditor = openEditor;
        }

        /// <summary>
        /// Reserved space above the category list for admin chrome.
        /// Edit entry is the bottom-right settings button (no left-sidebar offset).
        /// </summary>
        private double GetSidebarAdminButtonsOffset(double fitScale)
        {
            return 0;
        }

        private void OpenAdminEditor()
        {
            adminData.IsAdminPanelOpen = true;
            adminData.SelectedCategoryIndex = selectedCategoryIndex;
            adminData.EditorSection = categories.Length > 0
                ? AdminEditorSection.Quests
                : AdminEditorSection.Branches;
            adminData.ClearFields();
            adminData.SetToolMode(AdminToolMode.Select);
            preAdminSnapshot = categories.ToArray();
            adminBranchListScrollOffset = 0;
            if (categories.Length > 0)
                dataManager.EnsureCategoryContentLoaded(GetSelectedCategory().HeaderTitle, includeI18n: true);
        }

        private void SwitchAdminEditorSection(AdminEditorSection section)
        {
            if (adminData.EditorSection == section)
                return;

            adminData.EditorSection = section;
            if (section == AdminEditorSection.Branches)
            {
                CloseQuestEditModal();
                adminData.ResetToolState();
                adminData.ClearSelection();
                adminBranchListScrollOffset = 0;
            }
            else
            {
                adminData.SetToolMode(AdminToolMode.Select);
            }

            ComposeDialog();
        }

        private void CloseAdminEditor(bool restoreSnapshot)
        {
            if (restoreSnapshot && preAdminSnapshot != null)
            {
                categories = MergeCategoriesForAdminClose(preAdminSnapshot, dataManager.Categories);
                dataManager.UpdateCategories(categories);
            }
            else
            {
                categories = dataManager.Categories;
            }

            preAdminSnapshot = null;
            CloseQuestEditModal();
            adminData.IsAdminPanelOpen = false;
            adminData.ClearFields();
        }

        private static QuestbookCategoryDefinition[] MergeCategoriesForAdminClose(
            QuestbookCategoryDefinition[] snapshot,
            QuestbookCategoryDefinition[] serverCategories)
        {
            var merged = new List<QuestbookCategoryDefinition>(serverCategories.Length);

            foreach (QuestbookCategoryDefinition serverCategory in serverCategories)
            {
                QuestbookCategoryDefinition? snapshotCategory = snapshot.FirstOrDefault(candidate =>
                    string.Equals(candidate.HeaderTitle, serverCategory.HeaderTitle, StringComparison.Ordinal)
                    || string.Equals(candidate.Title, serverCategory.Title, StringComparison.Ordinal));

                if (snapshotCategory != null && HasLocalQuestGraphEdits(snapshotCategory, serverCategory))
                {
                    merged.Add(new QuestbookCategoryDefinition(
                        serverCategory.IconItemCode,
                        serverCategory.Title,
                        serverCategory.HeaderTitle,
                        serverCategory.ProgressPercent,
                        snapshotCategory.Nodes,
                        snapshotCategory.Connections,
                        serverCategory.HeaderDisplay,
                        snapshotCategory.HasFullI18n ? snapshotCategory.TitleByLang : serverCategory.TitleByLang,
                        snapshotCategory.HasFullI18n ? snapshotCategory.HeaderByLang : serverCategory.HeaderByLang,
                        isContentLoaded: true,
                        totalNodeCount: snapshotCategory.Nodes.Length,
                        hasFullI18n: snapshotCategory.HasFullI18n || serverCategory.HasFullI18n));
                }
                else
                {
                    merged.Add(serverCategory);
                }
            }

            return merged.ToArray();
        }

        private static bool HasLocalQuestGraphEdits(
            QuestbookCategoryDefinition snapshotCategory,
            QuestbookCategoryDefinition serverCategory)
        {
            if (snapshotCategory.Nodes.Length != serverCategory.Nodes.Length
                || snapshotCategory.Connections.Length != serverCategory.Connections.Length)
            {
                return true;
            }

            for (int i = 0; i < snapshotCategory.Nodes.Length; i++)
            {
                QuestbookQuestNodeDefinition snapshotNode = snapshotCategory.Nodes[i];
                QuestbookQuestNodeDefinition? serverNode = serverCategory.Nodes.FirstOrDefault(node => node.Id == snapshotNode.Id);
                if (serverNode == null
                    || System.Math.Abs(snapshotNode.X - serverNode.X) > 0.001
                    || System.Math.Abs(snapshotNode.Y - serverNode.Y) > 0.001
                    || snapshotNode.NodeType != serverNode.NodeType
                    || !string.Equals(snapshotNode.Description, serverNode.Description, StringComparison.Ordinal)
                    || snapshotNode.RequiredItems.Length != serverNode.RequiredItems.Length
                    || snapshotNode.RewardItems.Length != serverNode.RewardItems.Length)
                {
                    return true;
                }
            }

            foreach (QuestbookQuestConnectionDefinition snapshotConnection in snapshotCategory.Connections)
            {
                if (!serverCategory.Connections.Any(connection =>
                        connection.StartNodeId == snapshotConnection.StartNodeId
                        && connection.EndNodeId == snapshotConnection.EndNodeId))
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawAdminSettingsButton(Cairo.Context ctx, double fitScale, double screenX, double screenY)
        {
            // Only for admins, and only outside edit mode — hides after opening the editor.
            if (!IsPlayerAdmin() || adminData.IsAdminPanelOpen)
            {
                adminSettingsButtonHitArea = new LayoutRect(0, 0, 0, 0);
                return;
            }

            double buttonX = QuestbookGuiLayout.AdminSettingsButtonOffsetX * fitScale;
            double buttonY = QuestbookGuiLayout.AdminSettingsButtonOffsetY * fitScale;
            double buttonWidth = QuestbookGuiLayout.AdminSettingsButtonWidth * fitScale;
            double buttonHeight = QuestbookGuiLayout.AdminSettingsButtonHeight * fitScale;
            LayoutRect localButtonRect = new(buttonX, buttonY, buttonWidth, buttonHeight);
            adminSettingsButtonHitArea = localButtonRect.Offset(screenX, screenY);

            string textureFileName = isAdminSettingsButtonHovered
                ? QuestbookGuiLayout.AdminSettingsButtonHoverTexture
                : QuestbookGuiLayout.AdminSettingsButtonTexture;

            ImageSurface? buttonSurface = GetTextureSurface(textureFileName);
            if (buttonSurface != null)
            {
                DrawImageSurface(ctx, buttonSurface, buttonX, buttonY, buttonWidth, buttonHeight);
            }
            else
            {
                // Fallback if textures fail to load — keep the hit target visible.
                FillRectangle(
                    ctx,
                    buttonX,
                    buttonY,
                    buttonWidth,
                    buttonHeight,
                    isAdminSettingsButtonHovered
                        ? QuestbookGuiLayout.AdminTileActiveBackgroundColor
                        : QuestbookGuiLayout.AdminTileBackgroundColor);
            }
        }

        private void DrawAdminPanel(Cairo.Context ctx, double fitScale)
        {
            if (!adminData.IsAdminPanelOpen) return;


            double screenX = currentDialogX;
            double screenY = currentDialogY;
            double panelX = QuestbookGuiLayout.SidebarCardOffsetX * fitScale;
            double panelY = QuestbookGuiLayout.SidebarCardOffsetY * fitScale;
            double contentTop = panelY + (QuestbookGuiLayout.SidebarAdminQuestContentOffsetY * fitScale);
            double panelWidth = QuestbookGuiLayout.SidebarAdminPanelWidth * fitScale;
            double panelHeight = QuestbookGuiLayout.SidebarViewportHeight * fitScale;

            ctx.Save();
            ctx.Rectangle(panelX, panelY, panelWidth, panelHeight);
            ctx.Clip();

            DrawAdminModeSwitcher(ctx, fitScale, panelX, panelY);

            if (adminData.EditorSection == AdminEditorSection.Branches)
            {
                ClearQuestEditorHitAreas();
                DrawAdminBranchEditor(ctx, fitScale, panelX, contentTop, panelY, panelWidth, panelHeight, screenX, screenY);
            }
            else
            {
                ClearBranchEditorHitAreas();
                DrawAdminSidebarToolbar(ctx, fitScale, panelX, contentTop);
                // Single gray status line under DELETE (left-aligned) — no second hint.
                DrawAdminStatusText(ctx, fitScale, panelX, contentTop);
            }

            ctx.Restore();
            OffsetAdminPanelHitAreas(screenX, screenY);
        }

        private void OffsetAdminPanelHitAreas(double screenX, double screenY)
        {
            adminModeBranchesHitArea = adminModeBranchesHitArea.Offset(screenX, screenY);
            adminModeQuestsHitArea = adminModeQuestsHitArea.Offset(screenX, screenY);
            adminBranchAddHitArea = adminBranchAddHitArea.Offset(screenX, screenY);
            adminBranchRenameHitArea = adminBranchRenameHitArea.Offset(screenX, screenY);
            adminBranchDeleteHitArea = adminBranchDeleteHitArea.Offset(screenX, screenY);
            adminBranchCloseHitArea = adminBranchCloseHitArea.Offset(screenX, screenY);
            adminBranchListViewportHitArea = adminBranchListViewportHitArea.Offset(screenX, screenY);
            for (int i = 0; i < adminBranchCardHitAreas.Length; i++)
                adminBranchCardHitAreas[i] = adminBranchCardHitAreas[i].Offset(screenX, screenY);

            adminToolSelectHitArea = adminToolSelectHitArea.Offset(screenX, screenY);
            adminToolQuestHitArea = adminToolQuestHitArea.Offset(screenX, screenY);
            adminToolLinkHitArea = adminToolLinkHitArea.Offset(screenX, screenY);
            adminToolDeleteHitArea = adminToolDeleteHitArea.Offset(screenX, screenY);
            adminToolSaveHitArea = adminToolSaveHitArea.Offset(screenX, screenY);
            adminToolClearHitArea = adminToolClearHitArea.Offset(screenX, screenY);
            adminToolGridHitArea = adminToolGridHitArea.Offset(screenX, screenY);
            adminToolCloseHitArea = adminToolCloseHitArea.Offset(screenX, screenY);
        }

        private void ClearQuestEditorHitAreas()
        {
            adminToolSelectHitArea = new LayoutRect(0, 0, 0, 0);
            adminToolQuestHitArea = new LayoutRect(0, 0, 0, 0);
            adminToolLinkHitArea = new LayoutRect(0, 0, 0, 0);
            adminToolDeleteHitArea = new LayoutRect(0, 0, 0, 0);
            adminToolSaveHitArea = new LayoutRect(0, 0, 0, 0);
            adminToolClearHitArea = new LayoutRect(0, 0, 0, 0);
            adminToolGridHitArea = new LayoutRect(0, 0, 0, 0);
            adminToolCloseHitArea = new LayoutRect(0, 0, 0, 0);
            adminInputFieldHitAreas = [];
            adminTypeStartHitArea = new LayoutRect(0, 0, 0, 0);
            adminRepeatOnceHitArea = new LayoutRect(0, 0, 0, 0);
            adminRepeatCooldownHitArea = new LayoutRect(0, 0, 0, 0);
            adminRepeatInstantHitArea = new LayoutRect(0, 0, 0, 0);
            adminRepeatHoursHitArea = new LayoutRect(0, 0, 0, 0);
            adminTypeQuestHitArea = new LayoutRect(0, 0, 0, 0);
            adminTypeCheckpointHitArea = new LayoutRect(0, 0, 0, 0);
            adminTypeKillHitArea = new LayoutRect(0, 0, 0, 0);

        }

        private void ClearBranchEditorHitAreas()
        {
            adminBranchAddHitArea = new LayoutRect(0, 0, 0, 0);
            adminBranchRenameHitArea = new LayoutRect(0, 0, 0, 0);
            adminBranchDeleteHitArea = new LayoutRect(0, 0, 0, 0);
            adminBranchCloseHitArea = new LayoutRect(0, 0, 0, 0);
            adminBranchListViewportHitArea = new LayoutRect(0, 0, 0, 0);
            adminBranchCardHitAreas = [];
        }

        private void DrawAdminModeSwitcher(Cairo.Context ctx, double fitScale, double panelX, double panelY)
        {
            // Group 671: BRANCHES | QUESTS — 139×69 tiles, gap 10 (panel 288).
            double buttonHeight = QuestbookGuiLayout.SidebarAdminModeBarHeight * fitScale;
            double gap = QuestbookGuiLayout.SidebarAdminModeButtonGap * fitScale;
            double buttonWidth = QuestbookGuiLayout.SidebarAdminModeTileWidth * fitScale;

            adminModeBranchesHitArea = new LayoutRect(panelX, panelY, buttonWidth, buttonHeight);
            adminModeQuestsHitArea = new LayoutRect(panelX + buttonWidth + gap, panelY, buttonWidth, buttonHeight);

            DrawAdminTileButton(
                ctx,
                fitScale,
                adminModeBranchesHitArea,
                AdminToolbarIcon.Branches,
                adminData.EditorSection == AdminEditorSection.Branches,
                isAdminModeBranchesHovered,
                null,
                GetAdminToolbarLabel(AdminToolbarIcon.Branches));
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminModeQuestsHitArea,
                AdminToolbarIcon.Quests,
                adminData.EditorSection == AdminEditorSection.Quests,
                isAdminModeQuestsHovered,
                null,
                GetAdminToolbarLabel(AdminToolbarIcon.Quests));
        }

        private void DrawAdminBranchEditor(
            Cairo.Context ctx,
            double fitScale,
            double panelX,
            double contentTop,
            double panelY,
            double panelWidth,
            double panelHeight,
            double screenX,
            double screenY)
        {
            // Group 1142.svg full branch panel:
            // CREATE / EDIT / DELETE BRANCH → gray header → cards 278×40 from y=286 → EXIT.
            double buttonHeight = QuestbookGuiLayout.SidebarAdminBranchActionHeight * fitScale;
            double gap = QuestbookGuiLayout.SidebarAdminBranchActionGap * fitScale;
            double rowStep = buttonHeight + gap;

            adminBranchAddHitArea = new LayoutRect(panelX, contentTop, panelWidth, buttonHeight);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminBranchAddHitArea,
                AdminToolbarIcon.Add,
                false,
                isAdminBranchAddHovered,
                null,
                GetAdminToolbarLabel(AdminToolbarIcon.Add),
                labelOnRight: true);

            double row2Y = contentTop + rowStep;
            adminBranchRenameHitArea = new LayoutRect(panelX, row2Y, panelWidth, buttonHeight);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminBranchRenameHitArea,
                AdminToolbarIcon.EditBranch,
                false,
                isAdminBranchRenameHovered,
                null,
                GetAdminToolbarLabel(AdminToolbarIcon.Rename),
                labelOnRight: true);

            double row3Y = contentTop + (rowStep * 2);
            adminBranchDeleteHitArea = new LayoutRect(panelX, row3Y, panelWidth, buttonHeight);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminBranchDeleteHitArea,
                AdminToolbarIcon.Delete,
                false,
                isAdminBranchDeleteHovered,
                null,
                QuestbookLang.GetLocal("admin.icon.delete_branch"),
                labelOnRight: true);

            // Section header above the list — Group 671 #555 ~y258, centered «Branch "Name"».
            double headerY = panelY + (QuestbookGuiLayout.SidebarAdminBranchListHeaderOffsetY * fitScale);
            double headerH = QuestbookGuiLayout.SidebarAdminBranchListHeaderHeight * fitScale;
            CairoFont headerFont = CreateMontserratFont(
                QuestbookGuiLayout.SidebarAdminBranchListHeaderFontSize * fitScale,
                QuestbookGuiLayout.AdminBranchListHeaderColor);
            string headerText = categories.Length > 0
                ? QuestbookLang.GetLocal(
                    "admin.branch.list_header_named",
                    GetEditableCategoryTitle(GetSelectedCategory()))
                : QuestbookLang.GetLocal("admin.branch.list_header");
            double headerTextWidth = MeasureTextWidth(headerFont, headerText);
            double headerTextX = panelX + ((panelWidth - headerTextWidth) / 2);
            DrawText(
                ctx,
                headerFont,
                headerText,
                headerTextX,
                GetTextBaselineY(headerFont, headerY, headerH, headerH));

            // Cards y=286…551 (Group 671 scroll track), then ~64px gap, EXIT at y=615.5.
            double listTop = panelY + (QuestbookGuiLayout.SidebarAdminBranchListOffsetY * fitScale);
            double listHeight = QuestbookGuiLayout.SidebarAdminBranchListHeight * fitScale;
            double closeHeight = buttonHeight;
            double closeY = panelY + (QuestbookGuiLayout.SidebarAdminQuestExitOffsetY * fitScale);
            adminBranchCloseHitArea = new LayoutRect(panelX, closeY, panelWidth, closeHeight);
            DrawAdminCenteredTextButton(
                ctx,
                fitScale,
                adminBranchCloseHitArea,
                QuestbookLang.GetLocal("admin.icon.close"),
                isAdminBranchCloseHovered,
                QuestbookGuiLayout.AdminExitButtonColor);

            // Card column is 278 wide; scrollbar sits on the right edge (SVG x≈285, w=4).
            double scrollbarWidth = QuestbookGuiLayout.SidebarScrollbarWidth * fitScale;
            double cardWidth = System.Math.Min(panelWidth, QuestbookGuiLayout.SidebarCardWidth * fitScale);
            adminBranchListViewportHitArea = new LayoutRect(panelX, listTop, panelWidth, listHeight);
            adminBranchCardHitAreas = new LayoutRect[categories.Length];
            double cardHeight = QuestbookGuiLayout.SidebarCardHeight * fitScale;
            double cardGap = QuestbookGuiLayout.SidebarCardGap * fitScale;
            double cardStep = cardHeight + cardGap;
            double contentHeight = categories.Length > 0
                ? (categories.Length * cardStep) - cardGap
                : 0;
            double maxScroll = System.Math.Max(0, contentHeight - listHeight);
            adminBranchListScrollOffset = System.Math.Clamp(adminBranchListScrollOffset, 0, maxScroll);
            bool showScrollbar = maxScroll > 0;
            double listContentWidth = showScrollbar
                ? System.Math.Min(cardWidth, panelWidth - scrollbarWidth - (QuestbookGuiLayout.SidebarScrollbarGap * fitScale))
                : cardWidth;

            ctx.Save();
            ctx.Rectangle(panelX, listTop, panelWidth, listHeight);
            ctx.Clip();

            for (int index = 0; index < categories.Length; index++)
            {
                double cardY = listTop + (index * cardStep) - adminBranchListScrollOffset;
                if (cardY + cardHeight < listTop || cardY > listTop + listHeight)
                {
                    adminBranchCardHitAreas[index] = new LayoutRect(0, 0, 0, 0);
                    continue;
                }

                LayoutRect cardRect = new(panelX, cardY, listContentWidth, cardHeight);
                adminBranchCardHitAreas[index] = cardRect;

                SidebarQuestEntry entry = CreateSidebarEntry(categories[index], index == selectedCategoryIndex);
                // Clip GL item icons to the list viewport so half-visible rows do not bleed.
                DrawSidebarCard(ctx, entry, cardRect, fitScale, adminBranchListViewportHitArea);
            }

            if (showScrollbar)
            {
                // Group 1142: track #453324, thumb #997E68, 4px wide on the right.
                double trackX = panelX + panelWidth - scrollbarWidth;
                double thumbHeight = System.Math.Max(18 * fitScale, listHeight * (listHeight / contentHeight));
                double thumbTravel = System.Math.Max(1, listHeight - thumbHeight);
                double thumbY = listTop + ((adminBranchListScrollOffset / maxScroll) * thumbTravel);
                double radius = 2 * fitScale;
                FillRoundedRectangle(
                    ctx, trackX, listTop, scrollbarWidth, listHeight, radius,
                    QuestbookGuiLayout.SidebarScrollbarTrackColor);
                FillRoundedRectangle(
                    ctx, trackX, thumbY, scrollbarWidth, thumbHeight, radius,
                    QuestbookGuiLayout.SidebarScrollbarThumbColor);
            }

            ctx.Restore();
        }

        private double GetAdminBranchListRowStep(double fitScale)
        {
            return (QuestbookGuiLayout.SidebarCardHeight + QuestbookGuiLayout.SidebarCardGap) * fitScale;
        }

        private bool TryHandleAdminBranchListMouseWheel(MouseWheelEventArgs args)
        {
            int mouseX = capi.Input.MouseX;
            int mouseY = capi.Input.MouseY;
            if (!adminBranchListViewportHitArea.Contains(mouseX, mouseY))
                return false;

            float wheelDelta = args.deltaPrecise != 0 ? args.deltaPrecise : args.delta;
            if (wheelDelta == 0)
                return false;

            double listHeight = adminBranchListViewportHitArea.Height;
            double maxScroll = System.Math.Max(0,
                (categories.Length * GetAdminBranchListRowStep(currentFitScale))
                - (QuestbookGuiLayout.SidebarCardGap * currentFitScale)
                - listHeight);
            double scrollStep = GetAdminBranchListRowStep(currentFitScale) * 0.85;
            double direction = wheelDelta > 0 ? -1 : 1;
            adminBranchListScrollOffset = System.Math.Clamp(
                adminBranchListScrollOffset + (direction * scrollStep),
                0,
                maxScroll);
            ComposeDialog();
            return true;
        }

        private void DrawAdminSidebarToolbar(Cairo.Context ctx, double fitScale, double panelX, double panelY)
        {
            // Group 1143.svg: full-width 39px tool rows, then SAVE/EXIT at bottom of panel.
            double panelWidth = QuestbookGuiLayout.SidebarAdminPanelWidth * fitScale;
            double buttonHeight = QuestbookGuiLayout.SidebarAdminToolbarButtonHeight * fitScale;
            double gap = QuestbookGuiLayout.SidebarAdminToolbarButtonGap * fitScale;
            double stepY = buttonHeight + gap;
            // panelY here is contentTop (below mode switcher) — matches SVG y=82.5.
            double panelOriginY = panelY - (QuestbookGuiLayout.SidebarAdminQuestContentOffsetY * fitScale);

            LayoutRect Row(int index) => new(
                panelX,
                panelY + (index * stepY),
                panelWidth,
                buttonHeight);

            // SELECT, NEW QUEST, LINK, GRID, EDIT, DELETE
            adminToolSelectHitArea = Row(0);
            adminToolQuestHitArea = Row(1);
            adminToolLinkHitArea = Row(2);
            adminToolGridHitArea = Row(3);
            adminToolClearHitArea = Row(4); // EDIT (pencil) — opens selected quest
            adminToolDeleteHitArea = Row(5);

            DrawAdminTileButton(ctx, fitScale, adminToolSelectHitArea, AdminToolbarIcon.Select,
                adminData.ToolMode == AdminToolMode.Select, isAdminToolSelectHovered, null,
                GetAdminToolbarLabel(AdminToolbarIcon.Select), labelOnRight: true);
            DrawAdminTileButton(ctx, fitScale, adminToolQuestHitArea, AdminToolbarIcon.NewQuest,
                adminData.ToolMode == AdminToolMode.NewQuest, isAdminToolQuestHovered, null,
                GetAdminToolbarLabel(AdminToolbarIcon.NewQuest), labelOnRight: true);
            DrawAdminTileButton(ctx, fitScale, adminToolLinkHitArea, AdminToolbarIcon.Link,
                adminData.ToolMode == AdminToolMode.LinkQuests, isAdminToolLinkHovered, null,
                GetAdminToolbarLabel(AdminToolbarIcon.Link), labelOnRight: true);
            DrawAdminTileButton(ctx, fitScale, adminToolGridHitArea, AdminToolbarIcon.Grid,
                adminData.ShowGrid, isAdminToolGridHovered, null,
                GetAdminToolbarLabel(AdminToolbarIcon.Grid), labelOnRight: true);
            // EDIT — tool mode: click a node on the graph to open its editor (not drag).
            DrawAdminTileButton(ctx, fitScale, adminToolClearHitArea, AdminToolbarIcon.EditBranch,
                adminData.ToolMode == AdminToolMode.EditQuest, isAdminToolClearHovered, null,
                QuestbookLang.GetLocal("admin.icon.edit_quest"), labelOnRight: true);
            DrawAdminTileButton(ctx, fitScale, adminToolDeleteHitArea, AdminToolbarIcon.Delete,
                adminData.ToolMode == AdminToolMode.DeleteNode, isAdminToolDeleteHovered, null,
                GetAdminToolbarLabel(AdminToolbarIcon.Delete), labelOnRight: true);

            // SAVE (green) + EXIT (red) — bottom of panel, centered labels, no icons.
            double saveY = panelOriginY + (QuestbookGuiLayout.SidebarAdminQuestSaveOffsetY * fitScale);
            double exitY = panelOriginY + (QuestbookGuiLayout.SidebarAdminQuestExitOffsetY * fitScale);
            adminToolSaveHitArea = new LayoutRect(panelX, saveY, panelWidth, buttonHeight);
            adminToolCloseHitArea = new LayoutRect(panelX, exitY, panelWidth, buttonHeight);

            DrawAdminCenteredTextButton(
                ctx, fitScale, adminToolSaveHitArea,
                QuestbookLang.GetLocal("admin.icon.save"),
                isAdminToolSaveHovered,
                QuestbookGuiLayout.AdminTileActiveContentColor);
            DrawAdminCenteredTextButton(
                ctx, fitScale, adminToolCloseHitArea,
                QuestbookLang.GetLocal("admin.icon.close"),
                isAdminToolCloseHovered,
                QuestbookGuiLayout.AdminExitButtonColor);
        }

        private void DrawAdminStatusText(Cairo.Context ctx, double fitScale, double panelX, double toolbarTop)
        {
            // Group 1143.svg: single gray hint under DELETE, left-aligned (~y353, #555555).
            double panelOriginY = toolbarTop - (QuestbookGuiLayout.SidebarAdminQuestContentOffsetY * fitScale);
            double panelWidth = QuestbookGuiLayout.SidebarAdminPanelWidth * fitScale;
            double statusY = panelOriginY + (QuestbookGuiLayout.SidebarAdminQuestStatusOffsetY * fitScale);
            double statusHeight = QuestbookGuiLayout.SidebarAdminQuestStatusHeight * fitScale;
            double padX = 4 * fitScale;
            double maxTextWidth = panelWidth - (padX * 2);
            double fontSize = QuestbookGuiLayout.SidebarAdminQuestStatusFontSize * fitScale;
            string text = GetAdminStatusText();

            CairoFont font = CreateMontserratFont(fontSize, QuestbookGuiLayout.AdminBranchListHeaderColor);
            while (fontSize > 8 * fitScale && MeasureTextWidth(font, text) > maxTextWidth)
            {
                fontSize -= 0.5 * fitScale;
                font = CreateMontserratFont(fontSize, QuestbookGuiLayout.AdminBranchListHeaderColor);
            }

            while (text.Length > 3 && MeasureTextWidth(font, text) > maxTextWidth)
                text = text[..^4] + "...";

            DrawText(
                ctx,
                font,
                text,
                panelX + padX,
                GetTextBaselineY(font, statusY, statusHeight, statusHeight));
        }

        private string GetAdminStatusText()
        {
            return adminData.ToolMode switch
            {
                AdminToolMode.Select when adminData.HasSelectedNode =>
                    QuestbookLang.GetLocal("admin.status.selected", adminData.SelectedNodeId),
                AdminToolMode.Select =>
                    QuestbookLang.GetLocal("admin.status.pick_select"),
                AdminToolMode.EditQuest =>
                    QuestbookLang.GetLocal("admin.status.pick_edit"),
                AdminToolMode.NewQuest =>
                    QuestbookLang.GetLocal("admin.status.pick_new_position"),
                AdminToolMode.LinkQuests when adminData.LinkSourceNodeId == null =>
                    QuestbookLang.GetLocal("admin.status.pick_line_source"),
                AdminToolMode.LinkQuests =>
                    QuestbookLang.GetLocal("admin.status.pick_line_target"),
                AdminToolMode.DeleteNode =>
                    QuestbookLang.GetLocal("admin.status.pick_delete"),
                _ => QuestbookLang.GetLocal("admin.status.select_tool")
            };
        }

        private void DrawAdminEmptySelectionHint(Cairo.Context ctx, double fitScale, double panelX, double toolbarTop)
        {
            double hintY = toolbarTop
                + (QuestbookGuiLayout.SidebarAdminToolbarHeight * fitScale)
                + (QuestbookGuiLayout.SidebarAdminToolbarButtonGap * fitScale)
                + (QuestbookGuiLayout.SidebarAdminStatusHeight * fitScale)
                + (QuestbookGuiLayout.SidebarAdminSelectedHintY * fitScale);
            CairoFont font = CreateMontserratFont(12 * fitScale, QuestbookGuiLayout.AdminTitleColor);
            string text = QuestbookLang.GetLocal("admin.status.no_selection");
            DrawText(ctx, font, text, panelX + (4 * fitScale), hintY);
        }

        private void DrawAdminSelectedNodeHint(Cairo.Context ctx, double fitScale, double panelX, double toolbarTop)
        {
            double hintY = toolbarTop
                + (QuestbookGuiLayout.SidebarAdminToolbarHeight * fitScale)
                + (QuestbookGuiLayout.SidebarAdminToolbarButtonGap * fitScale)
                + (QuestbookGuiLayout.SidebarAdminStatusHeight * fitScale)
                + (QuestbookGuiLayout.SidebarAdminSelectedHintY * fitScale);
            CairoFont font = CreateMontserratFont(12 * fitScale, QuestbookGuiLayout.AdminTitleColor);
            string text = isQuestEditModalOpen
                ? QuestbookLang.GetLocal("admin.status.editing", adminData.SelectedNodeId)
                : QuestbookLang.GetLocal("admin.status.click_quest_edit", adminData.SelectedNodeId);
            DrawText(ctx, font, text, panelX + (4 * fitScale), hintY);
        }

        private void DrawQuestEditModal(Cairo.Context ctx, double fitScale, double screenX, double screenY)
        {
            if (!isQuestEditModalOpen || !adminData.HasSelectedNode)
                return;

            questEditModalOverlayHitArea = GetQuestbookDialogContentRect();

            // Group 1169.svg layout (1080×608) + modal_questedit.png frame.
            // Centered in the right-hand quest graph area (same as other floating modals).
            double S(double v) => v * fitScale;
            double designW = QuestbookGuiLayout.QuestEditModalWidth;
            double designH = QuestbookGuiLayout.QuestEditModalHeight;
            double modalWidth = S(designW);
            double modalHeight = S(designH);

            double rightX = QuestbookGuiLayout.GraphViewportX * fitScale;
            double rightY = QuestbookGuiLayout.GraphViewportY * fitScale;
            double rightW = QuestbookGuiLayout.GraphViewportWidth * fitScale;
            double rightH = QuestbookGuiLayout.GraphViewportHeight * fitScale;

            // If the modal is taller/wider than the graph pad, shrink uniformly to fit.
            double scaleW = rightW / modalWidth;
            double scaleH = rightH / modalHeight;
            double fit = System.Math.Min(1.0, System.Math.Min(scaleW, scaleH));
            if (fit < 0.999)
            {
                modalWidth *= fit;
                modalHeight *= fit;
            }

            double unit = modalWidth / designW;
            double D(double design) => design * unit;

            double modalX = rightX + ((rightW - modalWidth) / 2);
            double modalY = rightY + ((rightH - modalHeight) / 2);
            LayoutRect localPanelRect = new(modalX, modalY, modalWidth, modalHeight);
            questEditModalPanelHitArea = localPanelRect.Offset(screenX, screenY);

            ImageSurface? modalSurface = GetTextureSurface(QuestbookGuiLayout.QuestEditModalTexture);
            if (modalSurface != null)
                DrawImageSurface(ctx, modalSurface, modalX, modalY, modalWidth, modalHeight);
            else
                FillRectangle(ctx, modalX, modalY, modalWidth, modalHeight, QuestbookGuiLayout.ModalBorderColor);

            // Content origin — Group 1169 design coords relative to modal (0,0).
            double contentX = modalX + D(QuestbookGuiLayout.QuestEditModalPadX);
            double contentWidth = D(QuestbookGuiLayout.QuestEditModalContentWidth);

            bool isQuestType = adminData.IsQuestTypeEdited;
            bool isCheckpointLayout = adminData.EditedNodeType is QuestbookQuestNodeType.Checkpoint
                or QuestbookQuestNodeType.Start;

            // Header — Group 1214: left «Selecting A Quest» (white) + right «Quest #N» (#555).
            string headerLeft = QuestbookLang.GetLocal("admin.quest_edit.header");
            CairoFont headerLeftFont = CreateMontserratFont(
                D(QuestbookGuiLayout.QuestEditModalHeaderTitleFontSize),
                QuestbookGuiLayout.AdminModalTitleColor);
            double headerLeftY = modalY + D(QuestbookGuiLayout.QuestEditModalHeaderTitleY);
            double headerLeftH = D(QuestbookGuiLayout.QuestEditModalHeaderTitleHeight);
            DrawText(
                ctx,
                headerLeftFont,
                headerLeft,
                modalX + D(QuestbookGuiLayout.QuestEditModalHeaderTitleX),
                GetTextBaselineY(headerLeftFont, headerLeftY, headerLeftH, headerLeftH));

            string headerId = QuestbookLang.GetLocal("admin.quest_edit.title", adminData.SelectedNodeId);
            CairoFont headerIdFont = CreateMontserratFont(
                D(QuestbookGuiLayout.QuestEditModalHeaderIdFontSize),
                QuestbookGuiLayout.QuestEditModalHeaderIdColor);
            double headerIdW = MeasureTextWidth(headerIdFont, headerId);
            double headerIdY = modalY + D(QuestbookGuiLayout.QuestEditModalHeaderIdY);
            double headerIdH = D(QuestbookGuiLayout.QuestEditModalHeaderIdHeight);
            // Right-align inside content band (Group 1214 ~x863 in 950 content ≈ near content right).
            double headerIdX = contentX + contentWidth - headerIdW;
            DrawText(
                ctx,
                headerIdFont,
                headerId,
                headerIdX,
                GetTextBaselineY(headerIdFont, headerIdY, headerIdH, headerIdH));

            // Type bar START | QUEST | CHECKPOINT | KILL
            double typeY = modalY + D(QuestbookGuiLayout.QuestEditModalTypeBarY);
            DrawQuestEditModalTypeSelector(ctx, unit, contentX, typeY, contentWidth);
            List<LayoutRect> hitAreas = [];
            List<AdminFormFieldRef> fieldRefs = [];

            // Top legend only for Quest/Kill (flag chips apply to goal rows).
            if (isQuestType)
                DrawQuestEditTopLegend(ctx, unit, modalX, modalY);

            if (isQuestType)
            {
                double columnWidth = D(QuestbookGuiLayout.QuestEditModalListColumnWidth);
                double columnGap = D(QuestbookGuiLayout.QuestEditModalListColumnGap);
                double headerY = modalY + D(QuestbookGuiLayout.QuestEditModalListHeaderY);
                double headerH = D(QuestbookGuiLayout.QuestEditModalListHeaderHeight);
                CairoFont sectionFont = CreateMontserratFont(D(14), QuestbookGuiLayout.AdminModalSectionColor);
                double addSize = D(QuestbookGuiLayout.QuestEditModalAddButtonSize);
                double addY = modalY + D(QuestbookGuiLayout.QuestEditModalAddButtonY);

                string goalsHeaderKey = adminData.EditedNodeType == QuestbookQuestNodeType.Kill
                    ? "admin.quest_edit.kill_section"
                    : "admin.quest_edit.goals_section";
                DrawText(
                    ctx,
                    sectionFont,
                    QuestbookLang.GetLocal(goalsHeaderKey),
                    contentX,
                    GetTextBaselineY(sectionFont, headerY, headerH, headerH));

                // Goals + (cyan document-plus) — SVG absolute 482–498 × 165–181.
                goalsAddButtonHitArea = new LayoutRect(
                    modalX + D(QuestbookGuiLayout.QuestEditModalGoalsAddX),
                    addY,
                    addSize,
                    addSize);
                DrawDocumentPlusGlyph(
                    ctx, goalsAddButtonHitArea.X, goalsAddButtonHitArea.Y, addSize,
                    QuestbookGuiLayout.AdminModalSectionColor);

                double awardsX = contentX + columnWidth + columnGap;
                DrawText(
                    ctx,
                    sectionFont,
                    QuestbookLang.GetLocal("admin.quest_edit.awards_section"),
                    awardsX,
                    GetTextBaselineY(sectionFont, headerY, headerH, headerH));
                // Awards + (orange document-plus) — SVG absolute 974–990 × 165–181.
                awardsAddButtonHitArea = new LayoutRect(
                    modalX + D(QuestbookGuiLayout.QuestEditModalAwardsAddX),
                    addY,
                    addSize,
                    addSize);
                DrawDocumentPlusGlyph(
                    ctx, awardsAddButtonHitArea.X, awardsAddButtonHitArea.Y, addSize,
                    [1.0, 170.0 / 255.0, 0.0, 1.0]); // #FFAA00

                // List panels — y=191.5 h=111; scrollbar sits OUTSIDE right edge (+12.5).
                double listTop = modalY + D(QuestbookGuiLayout.QuestEditModalListPanelY);
                double listHeight = D(QuestbookGuiLayout.QuestEditModalListHeight);
                goalsListViewportHitArea = new LayoutRect(contentX, listTop, columnWidth, listHeight);
                awardsListViewportHitArea = new LayoutRect(awardsX, listTop, columnWidth, listHeight);

                FillRoundedRectangle(ctx, contentX, listTop, columnWidth, listHeight, D(5.5),
                    QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(ctx, contentX, listTop, columnWidth, listHeight, D(5.5), D(1),
                    QuestbookGuiLayout.AdminTileBorderColor);
                FillRoundedRectangle(ctx, awardsX, listTop, columnWidth, listHeight, D(5.5),
                    QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(ctx, awardsX, listTop, columnWidth, listHeight, D(5.5), D(1),
                    QuestbookGuiLayout.AdminTileBorderColor);

                // Wavy divider between GOALS and AWARDS (Group 1169 path at x≈540).
                DrawQuestEditWaveDivider(ctx, unit, modalX, listTop, listHeight);

                DrawQuestEditScrollableItemList(
                    ctx, unit, goalsListViewportHitArea, adminData.Goals, isGoals: true,
                    ref goalsListScrollOffset, ref goalsRemoveHitAreas, hitAreas, fieldRefs);
                DrawQuestEditScrollableItemList(
                    ctx, unit, awardsListViewportHitArea, adminData.Awards, isGoals: false,
                    ref awardsListScrollOffset, ref awardsRemoveHitAreas, hitAreas, fieldRefs);
            }
            else
            {
                goalsListViewportHitArea = new LayoutRect(0, 0, 0, 0);
                awardsListViewportHitArea = new LayoutRect(0, 0, 0, 0);
                goalsAddButtonHitArea = new LayoutRect(0, 0, 0, 0);
                awardsAddButtonHitArea = new LayoutRect(0, 0, 0, 0);
                goalsRemoveHitAreas = [];
                awardsRemoveHitAreas = [];
                goalsTakeToggleHitAreas = [];
                awardsTakeToggleHitAreas = [];
                goalsCraftToggleHitAreas = [];
                goalsKillToggleHitAreas = [];
                goalsMatchToggleHitAreas = [];
                awardsMatchToggleHitAreas = [];
            }

            // Language chips: under lists for Quest/Kill; under type bar for Checkpoint/Start (Group 1174).
            double langY = modalY + D(isCheckpointLayout
                ? QuestbookGuiLayout.QuestEditModalCheckpointLangY
                : QuestbookGuiLayout.QuestEditModalLangY);
            DrawQuestEditLanguageBar(
                ctx,
                unit,
                contentX,
                langY,
                contentWidth,
                D(QuestbookGuiLayout.QuestEditModalLangChipHeight),
                D(QuestbookGuiLayout.QuestEditModalLangRowGap));

            // Description between langs and SAVE. Repeat row sits under the text field (Quest/Kill).
            AdminFormFieldRef infoField = new(AdminFormFieldKind.Information);
            double langBottom = langY
                + D(QuestbookGuiLayout.QuestEditModalLangChipHeight * 2
                    + QuestbookGuiLayout.QuestEditModalLangRowGap);
            double saveTop = modalY + D(QuestbookGuiLayout.QuestEditModalSaveY);
            double infoY = langBottom + D(isCheckpointLayout ? 12 : 8);
            double repeatReserve = isQuestType
                ? D(QuestbookGuiLayout.QuestEditModalRepeatRowHeight + QuestbookGuiLayout.QuestEditModalRepeatRowGap + 4)
                : 0;
            double infoH = System.Math.Max(D(36), saveTop - infoY - D(10) - repeatReserve);
            if (infoY + infoH + repeatReserve > saveTop - D(6))
                infoH = System.Math.Max(D(28), saveTop - D(6) - infoY - repeatReserve);

            hitAreas.Add(new LayoutRect(contentX, infoY, contentWidth, infoH));
            fieldRefs.Add(infoField);

            string infoPlaceholder = QuestbookLang.GetLocal("admin.information_text")
                + $" [{adminData.EditorLanguage.ToUpperInvariant()}]";
            string infoValue = adminData.InformationText;
            bool infoFocused = adminData.FocusedField == infoField;

            // Description frame — pure Cairo rounded rect (no stretched texture).
            DrawQuestEditTextField(
                ctx, contentX, infoY, contentWidth, infoH, D(5.5), D(1), focused: infoFocused);

            string infoDisplay = string.IsNullOrWhiteSpace(infoValue) && !infoFocused
                ? infoPlaceholder
                : infoValue;
            double[] infoColor = infoFocused || !string.IsNullOrWhiteSpace(infoValue)
                ? QuestbookGuiLayout.AdminModalChipTextColor
                : QuestbookGuiLayout.AdminPanelPlaceholderColor;
            CairoFont infoFont = CreateMontserratFont(D(12), infoColor);
            // Checkpoint/Start: long description; Quest/Kill: shorter under lists.
            int infoMaxChars = isCheckpointLayout ? 900 : (isQuestType ? 220 : 624);
            double textPadX = D(10);
            double textPadY = D(isCheckpointLayout ? 10 : 6);
            List<string> infoLines = WrapText(
                infoFont,
                string.IsNullOrEmpty(infoDisplay) ? " " : infoDisplay,
                contentWidth - (textPadX * 2),
                infoMaxChars);
            if (string.IsNullOrEmpty(infoDisplay))
                infoLines = [string.Empty];

            double lineHeight = D(isCheckpointLayout ? 18 : 16);
            double contentTextY = infoY + textPadY;
            int maxLines = System.Math.Max(1, (int)((infoH - textPadY * 2) / lineHeight));
            if (infoLines.Count > maxLines)
            {
                infoLines = infoLines.Take(maxLines).ToList();
                if (infoLines[^1].Length > 3)
                    infoLines[^1] = infoLines[^1][..^3] + "...";
            }

            for (int li = 0; li < infoLines.Count; li++)
            {
                double lineY = contentTextY + (li * lineHeight);
                DrawText(ctx, infoFont, infoLines[li], contentX + textPadX,
                    GetTextBaselineY(infoFont, lineY, lineHeight, lineHeight));
            }

            if (infoFocused)
            {
                int last = System.Math.Max(0, infoLines.Count - 1);
                string lastLine = infoLines.Count > 0 ? infoLines[last] : string.Empty;
                DrawTextCaret(
                    ctx, infoFont, lastLine, contentX + textPadX,
                    contentTextY + (last * lineHeight), lineHeight, infoColor);
            }

            // Repeat row under description (Quest / Kill only).
            if (isQuestType)
            {
                double repeatY = infoY + infoH + D(QuestbookGuiLayout.QuestEditModalRepeatRowGap);
                DrawQuestEditRepeatRow(ctx, unit, contentX, repeatY, contentWidth, hitAreas, fieldRefs);
            }
            else
            {
                adminRepeatOnceHitArea = new LayoutRect(0, 0, 0, 0);
                adminRepeatCooldownHitArea = new LayoutRect(0, 0, 0, 0);
                adminRepeatInstantHitArea = new LayoutRect(0, 0, 0, 0);
                adminRepeatHoursHitArea = new LayoutRect(0, 0, 0, 0);
            }

            adminInputFieldHitAreas = hitAreas.ToArray();
            adminInputFieldRefs = fieldRefs.ToArray();

            // SAVE — full-width green label, Group 1169 y=499.5
            double saveY = modalY + D(QuestbookGuiLayout.QuestEditModalSaveY);
            double saveH = D(QuestbookGuiLayout.QuestEditModalCloseButtonHeight);
            questEditModalSaveButtonHitArea = new LayoutRect(contentX, saveY, contentWidth, saveH);
            DrawAdminCenteredTextButton(
                ctx,
                fitScale,
                questEditModalSaveButtonHitArea,
                QuestbookLang.GetLocal("admin.quest_edit.save"),
                isQuestEditModalSaveHovered,
                QuestbookGuiLayout.AdminTileActiveContentColor);

            if (adminItemPickerTarget != null)
                DrawAdminItemPicker(ctx, fitScale, localPanelRect);

            OffsetQuestEditModalHitAreas(screenX, screenY);
        }

        private static string[] GetRegisteredLanguageCodes()
        {
            try
            {
                if (Lang.AvailableLanguages is { Count: > 0 })
                {
                    return Lang.AvailableLanguages.Keys
                        .Select(static code => QuestbookLocalizedText.NormalizeLang(code))
                        .Where(static code => !string.IsNullOrWhiteSpace(code))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(static code => code, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
            }
            catch
            {
                // fall through
            }

            string current = QuestbookLocalizedText.NormalizeLang(Lang.CurrentLocale);
            return string.IsNullOrWhiteSpace(current)
                ? ["en", "ru"]
                : new[] { "en", "ru", current }
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static c => c, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
        }

        private void DrawQuestEditLanguageBar(
            Cairo.Context ctx,
            double unit,
            double x,
            double y,
            double width,
            double rowHeight,
            double rowGap)
        {
            string[] langs = GetRegisteredLanguageCodes();
            if (langs.Length == 0)
                langs = ["en"];

            // Always include languages that already have text, even if not registered right now.
            foreach (string existing in adminData.InformationByLang.Keys)
            {
                string code = QuestbookLocalizedText.NormalizeLang(existing);
                if (!langs.Contains(code, StringComparer.OrdinalIgnoreCase))
                    langs = langs.Append(code).OrderBy(static c => c, StringComparer.OrdinalIgnoreCase).ToArray();
            }

            questEditLangCodes = langs;
            questEditLangButtonHitAreas = new LayoutRect[langs.Length];

            // Group 1169: fixed 46×28 chips, step 49 (gap 3), left-to-right then next row.
            double chipW = QuestbookGuiLayout.QuestEditModalLangChipWidth * unit;
            double chipH = rowHeight > 0 ? rowHeight : QuestbookGuiLayout.QuestEditModalLangChipHeight * unit;
            double step = QuestbookGuiLayout.QuestEditModalLangChipStep * unit;
            int columns = System.Math.Max(1, (int)((width + (step - chipW)) / step));

            for (int i = 0; i < langs.Length; i++)
            {
                string lang = langs[i];
                int col = i % columns;
                int row = i / columns;
                double bx = x + (col * step);
                double by = y + (row * (chipH + rowGap));
                LayoutRect rect = new(bx, by, chipW, chipH);
                questEditLangButtonHitAreas[i] = rect;

                bool active = string.Equals(lang, adminData.EditorLanguage, StringComparison.OrdinalIgnoreCase);
                bool hasText = adminData.InformationByLang.TryGetValue(lang, out string? text)
                    && !string.IsNullOrWhiteSpace(text);

                double radius = 5.5 * unit;
                FillRoundedRectangle(ctx, rect.X, rect.Y, rect.Width, rect.Height, radius,
                    QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(
                    ctx,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height,
                    radius,
                    System.Math.Max(1.0, unit),
                    active
                        ? QuestbookGuiLayout.AdminTileActiveContentColor
                        : QuestbookGuiLayout.AdminTileBorderColor);

                string label = lang.ToUpperInvariant();
                if (hasText && !active)
                    label += "·";

                double[] color = active
                    ? QuestbookGuiLayout.AdminTileActiveContentColor
                    : QuestbookGuiLayout.AdminTileIdleContentColor;
                // Group 1169 chip labels #FAFFFD (idle) / green when active.
                if (!active)
                    color = QuestbookGuiLayout.AdminModalChipTextColor;
                CairoFont labelFont = CreateMontserratFont(System.Math.Clamp(chipH * 0.42, 9 * unit, 12 * unit), color);
                DrawCenteredText(ctx, labelFont, label, rect);
            }
        }

        private void OffsetQuestEditModalHitAreas(double screenX, double screenY)
        {
            adminTypeStartHitArea = adminTypeStartHitArea.Offset(screenX, screenY);
            adminTypeQuestHitArea = adminTypeQuestHitArea.Offset(screenX, screenY);
            adminTypeCheckpointHitArea = adminTypeCheckpointHitArea.Offset(screenX, screenY);
            adminTypeKillHitArea = adminTypeKillHitArea.Offset(screenX, screenY);
            adminRepeatOnceHitArea = adminRepeatOnceHitArea.Offset(screenX, screenY);
            adminRepeatCooldownHitArea = adminRepeatCooldownHitArea.Offset(screenX, screenY);
            adminRepeatInstantHitArea = adminRepeatInstantHitArea.Offset(screenX, screenY);
            adminRepeatHoursHitArea = adminRepeatHoursHitArea.Offset(screenX, screenY);
            goalsAddButtonHitArea = goalsAddButtonHitArea.Offset(screenX, screenY);
            awardsAddButtonHitArea = awardsAddButtonHitArea.Offset(screenX, screenY);
            goalsListViewportHitArea = goalsListViewportHitArea.Offset(screenX, screenY);
            awardsListViewportHitArea = awardsListViewportHitArea.Offset(screenX, screenY);
            questEditModalSaveButtonHitArea = questEditModalSaveButtonHitArea.Offset(screenX, screenY);
            for (int i = 0; i < questEditLangButtonHitAreas.Length; i++)
                questEditLangButtonHitAreas[i] = questEditLangButtonHitAreas[i].Offset(screenX, screenY);

            for (int i = 0; i < goalsRemoveHitAreas.Length; i++)
                goalsRemoveHitAreas[i] = goalsRemoveHitAreas[i].Offset(screenX, screenY);
            for (int i = 0; i < awardsRemoveHitAreas.Length; i++)
                awardsRemoveHitAreas[i] = awardsRemoveHitAreas[i].Offset(screenX, screenY);
            for (int i = 0; i < goalsItemPickHitAreas.Length; i++)
                goalsItemPickHitAreas[i] = goalsItemPickHitAreas[i].Offset(screenX, screenY);
            for (int i = 0; i < awardsItemPickHitAreas.Length; i++)
                awardsItemPickHitAreas[i] = awardsItemPickHitAreas[i].Offset(screenX, screenY);
            for (int i = 0; i < goalsMatchToggleHitAreas.Length; i++)
                goalsMatchToggleHitAreas[i] = goalsMatchToggleHitAreas[i].Offset(screenX, screenY);
            for (int i = 0; i < awardsMatchToggleHitAreas.Length; i++)
                awardsMatchToggleHitAreas[i] = awardsMatchToggleHitAreas[i].Offset(screenX, screenY);
            for (int i = 0; i < goalsCraftToggleHitAreas.Length; i++)
                goalsCraftToggleHitAreas[i] = goalsCraftToggleHitAreas[i].Offset(screenX, screenY);
            for (int i = 0; i < goalsKillToggleHitAreas.Length; i++)
                goalsKillToggleHitAreas[i] = goalsKillToggleHitAreas[i].Offset(screenX, screenY);
            for (int i = 0; i < goalsTakeToggleHitAreas.Length; i++)
                goalsTakeToggleHitAreas[i] = goalsTakeToggleHitAreas[i].Offset(screenX, screenY);
            for (int i = 0; i < awardsTakeToggleHitAreas.Length; i++)
                awardsTakeToggleHitAreas[i] = awardsTakeToggleHitAreas[i].Offset(screenX, screenY);

            LayoutRect[] offsetInputHitAreas = new LayoutRect[adminInputFieldHitAreas.Length];
            for (int i = 0; i < adminInputFieldHitAreas.Length; i++)
                offsetInputHitAreas[i] = adminInputFieldHitAreas[i].Offset(screenX, screenY);
            adminInputFieldHitAreas = offsetInputHitAreas;

            if (!adminItemPickerPanelHitArea.IsEmpty)
            {
                adminItemPickerPanelHitArea = adminItemPickerPanelHitArea.Offset(screenX, screenY);
                adminItemPickerCancelHitArea = adminItemPickerCancelHitArea.Offset(screenX, screenY);

            }
        }

        private bool IsAdminEntityPickerMode()
        {
            if (adminItemPickerTarget is not { } target || !target.IsGoals)
                return false;
            if (target.ListIndex < 0 || target.ListIndex >= adminData.Goals.Count)
                return adminData.EditedNodeType == QuestbookQuestNodeType.Kill;
            QuestbookAdminItemEntry entry = adminData.Goals[target.ListIndex];
            return entry.IsKillObjective || adminData.EditedNodeType == QuestbookQuestNodeType.Kill;
        }

        private void OpenAdminItemPicker(bool isGoals, int listIndex)
        {
            adminItemPickerTarget = new AdminItemPickerTarget(isGoals, listIndex);
            adminData.FocusedField = AdminFormFieldRef.None;
            adminEntityPickerScrollOffset = 0;
            adminEntityPickerSlots = [];
            adminItemPickerSlots = [];
            adminEntityPickerSearchText = string.Empty;
            adminEntityPickerSearchFocused = false;
            adminEntityPickerHoverLabel = null;
            adminEntityPickerHoverRect = new LayoutRect(0, 0, 0, 0);
        }

        private void CloseAdminItemPicker()
        {
            adminItemPickerTarget = null;
            adminItemPickerPanelHitArea = new LayoutRect(0, 0, 0, 0);
            adminItemPickerCancelHitArea = new LayoutRect(0, 0, 0, 0);
            adminItemPickerSlots = [];
            adminEntityPickerSlots = [];
            adminEntityPickerScrollOffset = 0;
            adminEntityPickerSearchText = string.Empty;
            adminEntityPickerSearchFocused = false;
            adminEntityPickerHoverLabel = null;
            adminEntityPickerHoverRect = new LayoutRect(0, 0, 0, 0);
            adminEntityPickerSearchHitArea = new LayoutRect(0, 0, 0, 0);
            adminEntityPickerViewportLocal = new LayoutRect(0, 0, 0, 0);
            entityPickerTooltipCachedText = null;
        }

        private bool TryAssignAdminPickerItem(ItemSlot slot, string? assignCode = null)
        {
            if (adminItemPickerTarget == null)
                return false;

            string code = assignCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
                code = slot.Itemstack?.Collectible?.Code?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
                return false;

            AdminItemPickerTarget target = adminItemPickerTarget.Value;
            List<QuestbookAdminItemEntry> list = target.IsGoals ? adminData.Goals : adminData.Awards;
            if (target.ListIndex < 0 || target.ListIndex >= list.Count)
                return false;

            QuestbookAdminItemEntry entry = list[target.ListIndex];
            entry.CollectibleCode = code;

            // Goals: default to all variants when supported. Awards: always exact item.
            if (target.IsGoals && QuestbookItemCodeHelper.SupportsVariantWildcard(entry.CollectibleCode))
                entry.MatchAllVariants = true;
            else if (!target.IsGoals)
                entry.MatchAllVariants = false;

            if (entry.Count <= 0)
                entry.Count = 1;

            CloseAdminItemPicker();
            SyncAdminFieldEdit();
            return true;
        }

        private bool TryAssignAdminPickerEntity(string entityCode)
        {
            if (adminItemPickerTarget == null || string.IsNullOrWhiteSpace(entityCode))
                return false;

            AdminItemPickerTarget target = adminItemPickerTarget.Value;
            if (!target.IsGoals || target.ListIndex < 0 || target.ListIndex >= adminData.Goals.Count)
                return false;

            QuestbookAdminItemEntry entry = adminData.Goals[target.ListIndex];
            entry.CollectibleCode = entityCode;
            entry.IsKillObjective = true;
            entry.IsCraftObjective = false;
            entry.ConsumeOnComplete = false;
            if (QuestbookItemCodeHelper.SupportsVariantWildcard(entityCode))
                entry.MatchAllVariants = true;
            if (entry.Count <= 0)
                entry.Count = 1;

            CloseAdminItemPicker();
            SyncAdminFieldEdit();
            return true;
        }

        private void DrawAdminItemPicker(Cairo.Context ctx, double fitScale, LayoutRect modalArea)
        {
            if (adminItemPickerTarget == null)
                return;

            if (IsAdminEntityPickerMode())
            {
                DrawAdminEntityPicker(ctx, fitScale, modalArea);
                return;
            }

            // Group 1172.svg — bottom catalog panel over modal content.
            double unit = modalArea.Width / QuestbookGuiLayout.QuestEditModalWidth;
            LayoutRect panel = BeginQuestEditPickerChrome(
                ctx,
                unit,
                modalArea,
                QuestbookLang.GetLocal("admin.quest_edit.item_search_placeholder"));

            double tileSize = QuestbookGuiLayout.QuestEditModalPickerSlotSize * unit;
            double tileStep = QuestbookGuiLayout.QuestEditModalPickerSlotStep * unit;
            double tileR = QuestbookGuiLayout.QuestEditModalChipRadius * unit;
            double borderW = System.Math.Max(1.0, unit);
            int columns = QuestbookGuiLayout.QuestEditModalPickerColumns;

            double listLeft = panel.X + QuestbookGuiLayout.QuestEditModalPickerSearchInsetX * unit;
            double listTop = panel.Y + QuestbookGuiLayout.QuestEditModalPickerGridY * unit;
            double listWidth = columns * tileStep - QuestbookGuiLayout.QuestEditModalPickerSlotGap * unit;
            double listHeight = System.Math.Max(tileSize, panel.Y + panel.Height - listTop - 4 * unit);
            adminEntityPickerViewportLocal = new LayoutRect(listLeft, listTop, listWidth, listHeight);

            IReadOnlyList<(string Code, string Label, DummySlot Slot)> catalog =
                GetItemCatalogEntries(adminEntityPickerSearchText);
            int rows = System.Math.Max(1, (int)System.Math.Ceiling(catalog.Count / (double)columns));
            double contentH = rows * tileStep;
            double maxScroll = System.Math.Max(0, contentH - listHeight);
            adminEntityPickerScrollOffset = System.Math.Clamp(adminEntityPickerScrollOffset, 0, maxScroll);

            int firstVisibleRow = System.Math.Max(0, (int)System.Math.Floor(adminEntityPickerScrollOffset / tileStep) - 1);
            int visibleRowCount = (int)System.Math.Ceiling(listHeight / tileStep) + 2;
            int firstIndex = firstVisibleRow * columns;
            int lastIndex = System.Math.Min(catalog.Count, (firstVisibleRow + visibleRowCount) * columns);

            ctx.Save();
            ctx.Rectangle(listLeft, listTop, listWidth, listHeight);
            ctx.Clip();

            var pickerSlots = new List<(ItemSlot Slot, LayoutRect HitArea, string Label, string AssignCode)>(
                System.Math.Max(16, lastIndex - firstIndex));

            for (int i = firstIndex; i < lastIndex; i++)
            {
                int col = i % columns;
                int row = i / columns;
                double cellX = listLeft + (col * tileStep);
                double cellY = listTop + (row * tileStep) - adminEntityPickerScrollOffset;
                if (cellY + tileSize < listTop || cellY > listTop + listHeight)
                    continue;

                LayoutRect tileRect = new(cellX, cellY, tileSize, tileSize);
                pickerSlots.Add((catalog[i].Slot, tileRect, catalog[i].Label, catalog[i].Code));

                FillRoundedRectangle(ctx, tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height, tileR,
                    QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(ctx, tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height, tileR,
                    borderW, QuestbookGuiLayout.AdminTileBorderColor);
            }

            ctx.Restore();
            DrawQuestEditPickerScrollbar(
                ctx, unit, panel, listTop, listHeight, contentH, maxScroll, adminEntityPickerScrollOffset);

            if (catalog.Count == 0)
            {
                CairoFont emptyFont = CreateMontserratFont(12 * unit, QuestbookGuiLayout.AdminPanelPlaceholderColor);
                DrawCenteredText(
                    ctx,
                    emptyFont,
                    QuestbookLang.GetLocal("admin.quest_edit.item_search_empty"),
                    new LayoutRect(listLeft, listTop, listWidth, listHeight));
            }

            adminItemPickerSlots = pickerSlots.ToArray();
            adminEntityPickerSlots = [];
        }

        /// <summary>
        /// Group 1172 chrome: 939×221 panel, search dual-bar + CANCEL, shared by item/entity pickers.
        /// </summary>
        private LayoutRect BeginQuestEditPickerChrome(
            Cairo.Context ctx,
            double unit,
            LayoutRect modalArea,
            string searchPlaceholder)
        {
            double padX = QuestbookGuiLayout.QuestEditModalPadX * unit;
            double panelW = QuestbookGuiLayout.QuestEditModalContentWidth * unit;
            double panelH = QuestbookGuiLayout.QuestEditModalPickerPanelHeight * unit;
            double panelX = modalArea.X + padX;
            // Pin to design Y when modal is full-size; clamp if modal is shorter.
            double panelY = modalArea.Y + QuestbookGuiLayout.QuestEditModalPickerPanelY * unit;
            if (panelY + panelH > modalArea.Y + modalArea.Height - 8 * unit)
                panelY = modalArea.Y + modalArea.Height - panelH - 8 * unit;

            adminItemPickerPanelHitArea = new LayoutRect(panelX, panelY, panelW, panelH);

            double radius = QuestbookGuiLayout.QuestEditModalChipRadius * unit;
            double borderW = System.Math.Max(1.0, unit);
            FillRoundedRectangle(ctx, panelX, panelY, panelW, panelH, radius,
                QuestbookGuiLayout.AdminTileBackgroundColor);
            StrokeRoundedRectangle(ctx, panelX, panelY, panelW, panelH, radius, borderW,
                QuestbookGuiLayout.AdminTileBorderColor);

            // Search field (left dual-stadium style / Group 1156) + CANCEL chip on the right.
            double searchX = panelX + QuestbookGuiLayout.QuestEditModalPickerSearchInsetX * unit;
            double searchY = panelY + QuestbookGuiLayout.QuestEditModalPickerSearchInsetY * unit;
            double searchW = QuestbookGuiLayout.QuestEditModalPickerSearchWidth * unit;
            double searchH = QuestbookGuiLayout.QuestEditModalPickerSearchHeight * unit;
            double cancelX = panelX + QuestbookGuiLayout.QuestEditModalPickerCancelX * unit;
            double cancelW = QuestbookGuiLayout.QuestEditModalPickerCancelWidth * unit;

            adminEntityPickerSearchHitArea = new LayoutRect(searchX, searchY, searchW, searchH);
            adminItemPickerCancelHitArea = new LayoutRect(cancelX, searchY, cancelW, searchH);

            // Search field — Cairo only (texture stretched and "swam" when scaled).
            DrawQuestEditTextField(
                ctx, searchX, searchY, searchW, searchH, radius, borderW,
                adminEntityPickerSearchFocused);

            CairoFont searchFont = CreateMontserratFont(12 * unit,
                string.IsNullOrEmpty(adminEntityPickerSearchText) && !adminEntityPickerSearchFocused
                    ? QuestbookGuiLayout.AdminPanelPlaceholderColor
                    : QuestbookGuiLayout.AdminModalChipTextColor);
            string searchDisplay = string.IsNullOrEmpty(adminEntityPickerSearchText) && !adminEntityPickerSearchFocused
                ? searchPlaceholder
                : adminEntityPickerSearchText;
            double textPad = 8 * unit;
            DrawText(ctx, searchFont, searchDisplay, searchX + textPad,
                GetTextBaselineY(searchFont, searchY, searchH, searchH));
            if (adminEntityPickerSearchFocused)
            {
                DrawTextCaret(ctx, searchFont, adminEntityPickerSearchText,
                    searchX + textPad, searchY, searchH, QuestbookGuiLayout.AdminModalChipTextColor);
            }

            // CANCEL button — rounded chip, muted label (#AEAEAE in SVG).
            FillRoundedRectangle(ctx, cancelX, searchY, cancelW, searchH, radius,
                QuestbookGuiLayout.AdminTileBackgroundColor);
            StrokeRoundedRectangle(ctx, cancelX, searchY, cancelW, searchH, radius, borderW,
                QuestbookGuiLayout.AdminTileBorderColor);
            CairoFont cancelFont = CreateMontserratFont(12 * unit, [0xAE / 255.0, 0xAE / 255.0, 0xAE / 255.0, 1.0]);
            DrawCenteredText(ctx, cancelFont, QuestbookLang.GetLocal("admin.quest_edit.picker_cancel"),
                adminItemPickerCancelHitArea);

            return adminItemPickerPanelHitArea;
        }

        private void DrawQuestEditPickerScrollbar(
            Cairo.Context ctx,
            double unit,
            LayoutRect panel,
            double listTop,
            double listHeight,
            double contentH,
            double maxScroll,
            double scrollOffset)
        {
            if (maxScroll <= 0)
                return;

            double trackW = QuestbookGuiLayout.QuestEditModalPickerScrollbarWidth * unit;
            // Scale scrollbar X with panel width (design: 929.5 on 939-wide panel).
            double trackX = panel.X
                + (QuestbookGuiLayout.QuestEditModalPickerScrollbarX
                    / QuestbookGuiLayout.QuestEditModalContentWidth) * panel.Width;
            double thumbH = System.Math.Max(18 * unit, listHeight * (listHeight / contentH));
            double thumbTravel = System.Math.Max(1, listHeight - thumbH);
            double thumbY = listTop + ((scrollOffset / maxScroll) * thumbTravel);
            FillRoundedRectangle(ctx, trackX, listTop, trackW, listHeight, trackW * 0.5,
                QuestbookGuiLayout.AdminTileBorderColor);
            FillRoundedRectangle(ctx, trackX, thumbY, trackW, thumbH, trackW * 0.5,
                [0x74 / 255.0, 0x74 / 255.0, 0x74 / 255.0, 1.0]);
        }

        /// <summary>
        /// Creative inventory entries only (same as creative menu), with search filter cache.
        /// </summary>
        private IReadOnlyList<(string Code, string Label, DummySlot Slot)> GetItemCatalogEntries(string? searchFilter = null)
        {
            EnsureItemCatalogCache();
            List<(string Code, string Label, DummySlot Slot)> source = adminItemCatalogCache!;
            string filter = (searchFilter ?? string.Empty).Trim();
            string cacheKey = "A|" + filter;
            if (string.Equals(cacheKey, adminItemCatalogFilterKey, StringComparison.Ordinal)
                && adminItemCatalogFiltered != null)
            {
                return adminItemCatalogFiltered;
            }

            adminItemCatalogFilterKey = cacheKey;
            if (filter.Length == 0)
            {
                adminItemCatalogFiltered = source;
                return source;
            }

            var filtered = new List<(string Code, string Label, DummySlot Slot)>(256);
            foreach ((string code, string label, DummySlot slot) in source)
            {
                if (label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || code.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filtered.Add((code, label, slot));
                }
            }

            adminItemCatalogFiltered = filtered;
            return filtered;
        }

        /// <summary>
        /// Builds the catalog once from collectibles that appear in the creative inventory
        /// (<see cref="CollectibleObject.CreativeInventoryTabs"/> / Stacks) — same source as the creative menu.
        /// </summary>
        private void EnsureItemCatalogCache()
        {
            if (adminItemCatalogCache != null)
                return;

            var result = new List<(string Code, string Label, DummySlot Slot)>(2048);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryAddStack(ItemStack? stack)
            {
                if (stack?.Collectible?.Code == null)
                    return;

                string code = stack.Collectible.Code.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
                    return;

                string path = stack.Collectible.Code.Path ?? string.Empty;
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
                    label = code;

                result.Add((code, label, new DummySlot(displayStack)));
            }

            void TryAddCollectible(CollectibleObject? collectible)
            {
                if (collectible?.Code == null || collectible.Id == 0)
                    return;

                // Only what creative menu shows.
                bool hasTabs = collectible.CreativeInventoryTabs is { Length: > 0 };
                bool hasStacks = collectible.CreativeInventoryStacks is { Length: > 0 };
                if (!hasTabs && !hasStacks)
                    return;

                if (hasStacks)
                {
                    foreach (CreativeTabAndStackList tabList in collectible.CreativeInventoryStacks!)
                    {
                        if (tabList?.Stacks == null)
                            continue;

                        foreach (JsonItemStack jstack in tabList.Stacks)
                        {
                            if (jstack == null)
                                continue;

                            try
                            {
                                if (jstack.ResolvedItemstack == null)
                                    jstack.Resolve(capi.World, "swixyquestbook item picker", collectible.Code);

                                ItemStack? resolved = jstack.ResolvedItemstack;
                                if (resolved != null)
                                    TryAddStack(resolved);
                            }
                            catch
                            {
                                // Skip unresolved creative stack variants.
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
                    // Skip broken collectibles.
                }
            }

            foreach (Item item in capi.World.Items)
                TryAddCollectible(item);
            foreach (Block block in capi.World.Blocks)
                TryAddCollectible(block);

            // Creative menu order is roughly tab order; alpha by name is fine for search UX.
            result.Sort(static (a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
            adminItemCatalogCache = result;
            adminItemCatalogFilterKey = "\u0001";
            adminItemCatalogFiltered = null;
        }

        /// <summary>
        /// Creature picker for kill goals — tile grid with 3D entity models (like creative creatures).
        /// </summary>
        private void DrawAdminEntityPicker(Cairo.Context ctx, double fitScale, LayoutRect modalArea)
        {
            // Same Group 1172 chrome as item picker; creature models in the grid.
            double unit = modalArea.Width / QuestbookGuiLayout.QuestEditModalWidth;
            LayoutRect panel = BeginQuestEditPickerChrome(
                ctx,
                unit,
                modalArea,
                QuestbookLang.GetLocal("admin.quest_edit.entity_search_placeholder"));

            double tileSize = QuestbookGuiLayout.QuestEditModalPickerSlotSize * unit;
            double tileStep = QuestbookGuiLayout.QuestEditModalPickerSlotStep * unit;
            double tileR = QuestbookGuiLayout.QuestEditModalChipRadius * unit;
            double borderW = System.Math.Max(1.0, unit);
            int columns = QuestbookGuiLayout.QuestEditModalPickerColumns;

            double listLeft = panel.X + QuestbookGuiLayout.QuestEditModalPickerSearchInsetX * unit;
            double listTop = panel.Y + QuestbookGuiLayout.QuestEditModalPickerGridY * unit;
            double listWidth = columns * tileStep - QuestbookGuiLayout.QuestEditModalPickerSlotGap * unit;
            double listHeight = System.Math.Max(tileSize, panel.Y + panel.Height - listTop - 4 * unit);
            adminEntityPickerViewportLocal = new LayoutRect(listLeft, listTop, listWidth, listHeight);

            var creatures = GetKillPickerCreatureEntries(adminEntityPickerSearchText);
            int rows = System.Math.Max(1, (int)System.Math.Ceiling(creatures.Count / (double)columns));
            double contentH = rows * tileStep;
            double maxScroll = System.Math.Max(0, contentH - listHeight);
            adminEntityPickerScrollOffset = System.Math.Clamp(adminEntityPickerScrollOffset, 0, maxScroll);

            int firstVisibleRow = System.Math.Max(0, (int)System.Math.Floor(adminEntityPickerScrollOffset / tileStep) - 1);
            int visibleRowCount = (int)System.Math.Ceiling(listHeight / tileStep) + 2;
            int firstIndex = firstVisibleRow * columns;
            int lastIndex = System.Math.Min(creatures.Count, (firstVisibleRow + visibleRowCount) * columns);

            ctx.Save();
            ctx.Rectangle(listLeft, listTop, listWidth, listHeight);
            ctx.Clip();

            var slots = new List<(string EntityCode, string Label, DummySlot Slot, LayoutRect HitArea)>(
                System.Math.Max(16, lastIndex - firstIndex));
            string? hoverLabel = null;
            LayoutRect hoverRect = new(0, 0, 0, 0);
            int mouseX = capi.Input.MouseX;
            int mouseY = capi.Input.MouseY;

            for (int i = firstIndex; i < lastIndex; i++)
            {
                int col = i % columns;
                int row = i / columns;
                double cellX = listLeft + (col * tileStep);
                double cellY = listTop + (row * tileStep) - adminEntityPickerScrollOffset;
                if (cellY + tileSize < listTop || cellY > listTop + listHeight)
                    continue;

                LayoutRect tileRect = new(cellX, cellY, tileSize, tileSize);
                DummySlot slot = new(creatures[i].Stack);
                slots.Add((creatures[i].EntityCode, creatures[i].Label, slot, tileRect));

                bool hovered = ToScreenRect(tileRect).Contains(mouseX, mouseY);
                if (hovered)
                {
                    hoverLabel = creatures[i].Label;
                    hoverRect = tileRect;
                }

                FillRoundedRectangle(ctx, tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height, tileR,
                    hovered
                        ? [0.18, 0.36, 0.20, 0.98]
                        : QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(ctx, tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height, tileR,
                    hovered ? borderW * 1.8 : borderW,
                    hovered
                        ? QuestbookGuiLayout.AdminTileActiveContentColor
                        : QuestbookGuiLayout.AdminTileBorderColor);
            }

            ctx.Restore();
            DrawQuestEditPickerScrollbar(
                ctx, unit, panel, listTop, listHeight, contentH, maxScroll, adminEntityPickerScrollOffset);

            if (creatures.Count == 0)
            {
                CairoFont emptyFont = CreateMontserratFont(12 * unit, QuestbookGuiLayout.AdminPanelPlaceholderColor);
                DrawCenteredText(
                    ctx,
                    emptyFont,
                    QuestbookLang.GetLocal("admin.quest_edit.entity_search_empty"),
                    new LayoutRect(listLeft, listTop, listWidth, listHeight));
            }

            adminEntityPickerHoverLabel = hoverLabel;
            adminEntityPickerHoverRect = hoverRect;
            adminEntityPickerSlots = slots.ToArray();
            adminItemPickerSlots = [];
        }

        /// <summary>
        /// Creative-style creatures: ItemCreature stacks (render via RenderItemstackToGui).
        /// EntityCode is what kill progress tracks (creature- prefix stripped).
        /// </summary>
        private List<(string EntityCode, string Label, ItemStack Stack)> GetKillPickerCreatureEntries(string? searchFilter = null)
        {
            var result = new List<(string EntityCode, string Label, ItemStack Stack)>(256);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string filter = (searchFilter ?? string.Empty).Trim();

            foreach (Item item in capi.World.Items)
            {
                if (item?.Code == null || item.Id == 0)
                    continue;

                string itemCode = item.Code.ToString() ?? string.Empty;
                string path = item.Code.Path ?? string.Empty;
                string cls = item.Class ?? item.GetType().Name;

                bool isCreatureItem =
                    string.Equals(cls, "ItemCreature", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("creature-", StringComparison.OrdinalIgnoreCase);

                if (!isCreatureItem)
                    continue;

                // Skip dead / inventory-only variants.
                if (path.Contains("-dead", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("inventory", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("armorstand", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("strawdummy", StringComparison.OrdinalIgnoreCase))
                    continue;

                string entityCode = CreatureItemCodeToEntityCode(itemCode);
                if (string.IsNullOrWhiteSpace(entityCode) || !seen.Add(entityCode))
                    continue;

                // Prefer only creatures that exist as live entity types (killable).
                if (capi.World.GetEntityType(new AssetLocation(entityCode)) == null)
                {
                    bool found = false;
                    foreach (EntityProperties t in capi.World.EntityTypes ?? [])
                    {
                        if (t?.Code != null
                            && string.Equals(t.Code.ToString(), entityCode, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        continue;
                }

                ItemStack stack = new(item, 1);
                string label = stack.GetName();
                if (string.IsNullOrWhiteSpace(label))
                    label = entityCode;

                if (filter.Length > 0
                    && label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && entityCode.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                result.Add((entityCode, label, stack));
            }

            result.Sort(static (a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        /// <summary>game:creature-drifter-normal → game:drifter-normal</summary>
        private static string CreatureItemCodeToEntityCode(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                return string.Empty;

            AssetLocation loc = new(itemCode);
            string path = loc.Path ?? itemCode;
            if (path.StartsWith("creature-", StringComparison.OrdinalIgnoreCase))
                path = path["creature-".Length..];

            return string.IsNullOrEmpty(loc.Domain) ? path : $"{loc.Domain}:{path}";
        }

        /// <summary>
        /// Top-of-modal flag legend from Group 1169 (y=70, 18×18 chips at 409 / 500 / 581).
        /// Soft translucent chips + caption — not the solid row toggles.
        /// </summary>
        private void DrawQuestEditTopLegend(Cairo.Context ctx, double unit, double modalX, double modalY)
        {
            double y = modalY + QuestbookGuiLayout.QuestEditModalLegendY * unit;
            double icon = QuestbookGuiLayout.QuestEditModalLegendIconSize * unit;
            double labelGap = QuestbookGuiLayout.QuestEditModalLegendLabelGap * unit;
            double radius = 4 * unit;
            // Compact captions so they never collide with the next chip.
            CairoFont font = CreateMontserratFont(11 * unit, QuestbookGuiLayout.AdminTileIdleContentColor);

            // Fixed slots with max text width = gap to next chip (no overlap).
            void Chip(double designX, double nextDesignX, AdminFlagIcon flag, string caption)
            {
                double x = modalX + designX * unit;
                double[] accent = GetAdminFlagAccent(flag);
                FillRoundedRectangle(ctx, x, y, icon, icon, radius,
                    [accent[0], accent[1], accent[2], 0.16]);
                StrokeRoundedRectangle(ctx, x, y, icon, icon, radius,
                    System.Math.Max(1.0, unit),
                    [accent[0], accent[1], accent[2], 0.32]);
                double pad = icon * 0.14;
                DrawAdminFlagGlyph(ctx, x + pad, y + pad, icon - (pad * 2), flag,
                    [accent[0], accent[1], accent[2], 0.7]);

                double textX = x + icon + labelGap;
                double maxTextW = System.Math.Max(8 * unit, (nextDesignX - designX) * unit - icon - labelGap - 4 * unit);
                string draw = caption;
                while (draw.Length > 1 && MeasureTextWidth(font, draw) > maxTextW)
                    draw = draw[..^1];
                if (draw.Length < caption.Length && draw.Length > 1)
                    draw = draw[..^1] + "…";
                DrawText(ctx, font, draw, textX, GetTextBaselineY(font, y, icon, icon));
            }

            // Group 896 legend (left→right): Create · Craft · All Types
            //   purple layers → take · cyan paperclip → craft · yellow tile → all variants
            Chip(QuestbookGuiLayout.QuestEditModalLegendTakeX,
                QuestbookGuiLayout.QuestEditModalLegendCraftX,
                AdminFlagIcon.Take,
                QuestbookLang.GetLocal("admin.quest_edit.flag.take"));
            Chip(QuestbookGuiLayout.QuestEditModalLegendCraftX,
                QuestbookGuiLayout.QuestEditModalLegendVariantsX,
                AdminFlagIcon.Craft,
                QuestbookLang.GetLocal("admin.quest_edit.flag.craft"));
            Chip(QuestbookGuiLayout.QuestEditModalLegendVariantsX,
                QuestbookGuiLayout.QuestEditModalLegendVariantsX + 120,
                AdminFlagIcon.AllVariants,
                QuestbookLang.GetLocal("admin.quest_edit.flag.variants"));
        }

        /// <summary>
        /// Vertical divider strip between GOALS and AWARDS — Line 31.png (7×143).
        /// Placed in the gap between the two list panels.
        /// </summary>
        private void DrawQuestEditWaveDivider(Cairo.Context ctx, double unit, double modalX, double listTop, double listHeight)
        {
            double designX = QuestbookGuiLayout.QuestEditModalWaveDividerX;
            double texW = QuestbookGuiLayout.QuestEditModalWaveDividerWidth * unit;
            // Texture is 143px tall; list is 111 — stretch to list height (or slightly beyond like SVG).
            double texH = listHeight;
            double x = modalX + designX * unit - texW / 2;
            double y = listTop;

            ImageSurface? strip = GetTextureSurface(QuestbookGuiLayout.QuestEditWaveDividerTexture);
            if (strip != null)
            {
                DrawImageSurface(ctx, strip, x, y, texW, texH);
                return;
            }

            // Fallback if texture missing.
            FillRoundedRectangle(
                ctx, x, y, texW, texH, texW * 0.5,
                QuestbookGuiLayout.AdminTileBorderColor);
        }

        /// <summary>
        /// Text field: dark fill + exact Group 767.svg border paths drawn in Cairo.
        /// </summary>
        private void DrawQuestEditTextField(
            Cairo.Context ctx,
            double x,
            double y,
            double width,
            double height,
            double radius,
            double borderWidth,
            bool focused)
        {
            if (width <= 0 || height <= 0)
                return;

            double r = System.Math.Min(radius, System.Math.Min(width, height) / 2);
            FillRoundedRectangle(ctx, x, y, width, height, r, QuestbookGuiLayout.AdminTileBackgroundColor);
            DrawGroup767BorderCairo(ctx, x, y, width, height);

            if (focused)
            {
                StrokeRoundedRectangle(ctx, x, y, width, height, r, System.Math.Max(1.5, borderWidth * 1.5),
                    QuestbookGuiLayout.AdminTileActiveContentColor);
            }
        }

        /// <summary>
        /// Dual name+qty: solid halves + Group 767 outer rim + straight join.
        /// </summary>
        private void DrawQuestEditNameQtyFrame(
            Cairo.Context ctx,
            double nameOuterX,
            double joinX,
            double qtyRight,
            double y,
            double height,
            double radius,
            double[] fill,
            double[] border,
            double borderWidth,
            bool nameFocused,
            bool qtyFocused)
        {
            if (qtyRight <= nameOuterX || height <= 0)
                return;

            double r = System.Math.Min(radius, height / 2);
            double width = qtyRight - nameOuterX;

            ctx.NewPath();
            ctx.MoveTo(joinX, y);
            ctx.LineTo(nameOuterX + r, y);
            ctx.ArcNegative(nameOuterX + r, y + r, r, -System.Math.PI / 2, System.Math.PI);
            ctx.LineTo(nameOuterX, y + height - r);
            ctx.ArcNegative(nameOuterX + r, y + height - r, r, System.Math.PI, System.Math.PI / 2);
            ctx.LineTo(joinX, y + height);
            ctx.ClosePath();
            ctx.SetSourceRGBA(fill[0], fill[1], fill[2], fill[3]);
            ctx.Fill();

            ctx.NewPath();
            ctx.MoveTo(joinX, y);
            ctx.LineTo(qtyRight - r, y);
            ctx.Arc(qtyRight - r, y + r, r, -System.Math.PI / 2, 0);
            ctx.LineTo(qtyRight, y + height - r);
            ctx.Arc(qtyRight - r, y + height - r, r, 0, System.Math.PI / 2);
            ctx.LineTo(joinX, y + height);
            ctx.ClosePath();
            ctx.SetSourceRGBA(fill[0], fill[1], fill[2], fill[3]);
            ctx.Fill();

            DrawGroup767BorderCairo(ctx, nameOuterX, y, width, height);

            ctx.SetSourceRGBA(border[0], border[1], border[2], border[3]);
            ctx.LineWidth = System.Math.Max(1.0, borderWidth);
            ctx.NewPath();
            ctx.MoveTo(joinX, y + 1);
            ctx.LineTo(joinX, y + height - 1);
            ctx.Stroke();

            if (nameFocused || qtyFocused)
            {
                double fx = nameFocused ? nameOuterX : joinX;
                double fw = nameFocused ? (joinX - nameOuterX) : (qtyRight - joinX);
                StrokeRoundedRectangle(ctx, fx, y, fw, height, r, System.Math.Max(1.5, borderWidth * 1.5),
                    QuestbookGuiLayout.AdminTileActiveContentColor);
            }
        }

        /// <summary>
        /// Draw exact Group 767.svg border polygons (#2D2D2D).
        /// Top/bottom: 9-slice stretch (length only) so thickness stays correct.
        /// Left/right: tile the side wave instead of stretching — tall Start fields keep waviness.
        /// <paramref name="thicknessMul"/> &gt; 1 makes the rim bolder (branch lang frame, etc.).
        /// </summary>
        private void DrawGroup767BorderCairo(
            Cairo.Context ctx,
            double x,
            double y,
            double width,
            double height,
            double thicknessMul = 1.0)
        {
            EnsureGroup767BorderPolys();
            if (group767BorderPolys == null || group767BorderPolys.Length == 0)
            {
                StrokeRoundedRectangle(ctx, x, y, width, height,
                    QuestbookGuiLayout.QuestEditModalChipRadius,
                    System.Math.Max(1.5, width * 0.004) * System.Math.Max(1.0, thicknessMul),
                    QuestbookGuiLayout.AdminTileBorderColor);
                return;
            }

            double srcW = group767BorderSrcW;
            double srcH = group767BorderSrcH;
            // Corner pocket in design space (rounded ends of the stadium).
            double cornerSrc = System.Math.Min(28.0, System.Math.Min(srcW, srcH) * 0.35);
            // Thickness scale never uses the long axis alone (avoids fat top/bottom on tall fields).
            double thickScale = System.Math.Min(width / srcW, height / srcH) * System.Math.Max(0.5, thicknessMul);
            // Keep corners from eating the whole frame.
            double maxThick = System.Math.Min(width, height) / (2.0 * cornerSrc + 4.0);
            thickScale = System.Math.Min(thickScale, maxThick);
            if (thickScale <= 1e-6)
                return;

            double cDx = System.Math.Min(cornerSrc * thickScale, width * 0.45);
            double cDy = System.Math.Min(cornerSrc * thickScale, height * 0.45);
            double midSrcW = System.Math.Max(1.0, srcW - (cornerSrc * 2));
            double midSrcH = System.Math.Max(1.0, srcH - (cornerSrc * 2));
            double midDstW = System.Math.Max(0.5, width - (cDx * 2));
            double midDstH = System.Math.Max(0.5, height - (cDy * 2));

            // One natural-height copy of the side wave (preserves frequency).
            double sideTileH = midSrcH * thickScale;
            int sideTiles = System.Math.Max(1, (int)System.Math.Ceiling(midDstH / sideTileH));
            // Slight overlap so seams between tiles don't gap.
            double sideTileStep = midDstH / sideTiles;

            // Horizontal: stretch middle, keep corner thickness.
            double MapX(double px)
            {
                if (px <= cornerSrc)
                    return (px / cornerSrc) * cDx;
                if (px >= srcW - cornerSrc)
                    return width - cDx + ((px - (srcW - cornerSrc)) / cornerSrc) * cDx;
                return cDx + ((px - cornerSrc) / midSrcW) * midDstW;
            }

            // Vertical for top/bottom bands only (no tall-field stretch of sides).
            double MapYCap(double py)
            {
                if (py <= cornerSrc)
                    return (py / cornerSrc) * cDy;
                if (py >= srcH - cornerSrc)
                    return height - cDy + ((py - (srcH - cornerSrc)) / cornerSrc) * cDy;
                // Mid of top/bottom paths rarely appears; keep in upper band.
                return cDy + ((py - cornerSrc) / midSrcH) * System.Math.Min(midDstH, sideTileH);
            }

            // Vertical for one side-tile copy (tile index 0..sideTiles-1).
            double MapYSide(double py, int tile)
            {
                if (py <= cornerSrc)
                    return (py / cornerSrc) * cDy; // only meaningful on first tile
                if (py >= srcH - cornerSrc)
                    return height - cDy + ((py - (srcH - cornerSrc)) / cornerSrc) * cDy;
                double local = (py - cornerSrc) / midSrcH; // 0..1 within source mid
                return cDy + (tile * sideTileStep) + (local * sideTileStep);
            }

            // Classify poly by centroid: top / bottom / left / right.
            static int Classify(float[] poly, double sw, double sh, double corner)
            {
                double sx = 0, sy = 0;
                int n = poly.Length / 2;
                for (int i = 0; i < poly.Length; i += 2)
                {
                    sx += poly[i];
                    sy += poly[i + 1];
                }

                sx /= n;
                sy /= n;
                if (sy < corner)
                    return 0; // top
                if (sy > sh - corner)
                    return 1; // bottom
                if (sx < corner)
                    return 2; // left
                if (sx > sw - corner)
                    return 3; // right
                // Fallback by nearer edge.
                double dt = sy, db = sh - sy, dl = sx, dr = sw - sx;
                double m = System.Math.Min(System.Math.Min(dt, db), System.Math.Min(dl, dr));
                if (m == dt) return 0;
                if (m == db) return 1;
                if (m == dl) return 2;
                return 3;
            }

            ctx.SetSourceRGBA(0x2D / 255.0, 0x2D / 255.0, 0x2D / 255.0, 1.0);

            void FillPolyTopBottom(float[] poly)
            {
                ctx.NewPath();
                ctx.MoveTo(x + MapX(poly[0]), y + MapYCap(poly[1]));
                for (int i = 2; i < poly.Length; i += 2)
                    ctx.LineTo(x + MapX(poly[i]), y + MapYCap(poly[i + 1]));
                ctx.ClosePath();
                ctx.Fill();
            }

            void FillPolySide(float[] poly, int tile)
            {
                ctx.NewPath();
                ctx.MoveTo(x + MapX(poly[0]), y + MapYSide(poly[1], tile));
                for (int i = 2; i < poly.Length; i += 2)
                    ctx.LineTo(x + MapX(poly[i]), y + MapYSide(poly[i + 1], tile));
                ctx.ClosePath();
                ctx.Fill();
            }

            // Clip side tiles to the vertical mid band so tiles don't spill over corners.
            for (int p = 0; p < group767BorderPolys.Length; p++)
            {
                float[] poly = group767BorderPolys[p];
                if (poly.Length < 6)
                    continue;

                int part = Classify(poly, srcW, srcH, cornerSrc);
                if (part is 0 or 1)
                {
                    FillPolyTopBottom(poly);
                    continue;
                }

                // Left / right: tile wave along height.
                ctx.Save();
                ctx.Rectangle(x, y + cDy, width, midDstH);
                ctx.Clip();
                for (int tile = 0; tile < sideTiles; tile++)
                    FillPolySide(poly, tile);
                ctx.Restore();
            }
        }

        private void EnsureGroup767BorderPolys()
        {
            if (group767BorderLoadAttempted)
                return;
            group767BorderLoadAttempted = true;

            try
            {
                IAsset? asset = capi.Assets.TryGet(
                    new AssetLocation("swixyquestbook", "textures/" + QuestbookGuiLayout.QuestEditTextFieldBorderPaths));
                if (asset?.Data == null || asset.Data.Length < 16)
                    return;

                using MemoryStream ms = new(asset.Data);
                using BinaryReader br = new(ms);
                group767BorderSrcW = br.ReadSingle();
                group767BorderSrcH = br.ReadSingle();
                int count = br.ReadInt32();
                if (count <= 0 || count > 20000)
                    return;

                float[][] polys = new float[count][];
                for (int i = 0; i < count; i++)
                {
                    int n = br.ReadInt32();
                    if (n < 3 || n > 64)
                    {
                        // Skip malformed entry: try to stay aligned if possible.
                        if (n > 0 && n <= 64)
                        {
                            for (int k = 0; k < n * 2; k++)
                                br.ReadSingle();
                        }

                        polys[i] = [];
                        continue;
                    }

                    float[] pts = new float[n * 2];
                    for (int k = 0; k < pts.Length; k++)
                        pts[k] = br.ReadSingle();
                    polys[i] = pts;
                }

                group767BorderPolys = polys;
            }
            catch
            {
                group767BorderPolys = null;
            }
        }


        private void DrawQuestEditModalTypeSelector(Cairo.Context ctx, double unit, double panelX, double panelY, double panelWidth)
        {
            // Group 1169: 4× (231×39) chips, gap 5, text-only labels.
            double typeHeight = QuestbookGuiLayout.QuestEditModalTypeBarHeight * unit;
            double gap = QuestbookGuiLayout.QuestEditModalTypeButtonGap * unit;
            double typeButtonWidth = QuestbookGuiLayout.QuestEditModalTypeButtonWidth * unit;
            // Fall back to equal split if content width differs after scale clamp.
            if (System.Math.Abs((typeButtonWidth * 4) + (gap * 3) - panelWidth) > 2 * unit)
                typeButtonWidth = (panelWidth - (gap * 3)) / 4;

            adminTypeStartHitArea = new LayoutRect(panelX, panelY, typeButtonWidth, typeHeight);
            adminTypeQuestHitArea = new LayoutRect(panelX + typeButtonWidth + gap, panelY, typeButtonWidth, typeHeight);
            adminTypeCheckpointHitArea = new LayoutRect(panelX + (typeButtonWidth + gap) * 2, panelY, typeButtonWidth, typeHeight);
            adminTypeKillHitArea = new LayoutRect(panelX + (typeButtonWidth + gap) * 3, panelY, typeButtonWidth, typeHeight);

            void DrawType(LayoutRect area, QuestbookQuestNodeType type, bool hovered, string label)
            {
                bool active = adminData.EditedNodeType == type;
                double radius = QuestbookGuiLayout.AdminTileCornerRadius * unit;
                FillRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius,
                    QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius,
                    System.Math.Max(1.0, unit),
                    active || hovered
                        ? QuestbookGuiLayout.AdminTileActiveContentColor
                        : QuestbookGuiLayout.AdminTileBorderColor);
                double[] color = active || hovered
                    ? QuestbookGuiLayout.AdminTileActiveContentColor
                    : QuestbookGuiLayout.AdminTileIdleContentColor;
                CairoFont font = CreateMontserratFont(System.Math.Clamp(typeHeight * 0.38, 11 * unit, 15 * unit), color);
                DrawCenteredText(ctx, font, label, area);
            }

            DrawType(adminTypeStartHitArea, QuestbookQuestNodeType.Start, isAdminTypeStartHovered,
                GetAdminToolbarLabel(AdminToolbarIcon.Start));
            DrawType(adminTypeQuestHitArea, QuestbookQuestNodeType.Quest, isAdminTypeQuestHovered,
                GetAdminToolbarLabel(AdminToolbarIcon.Quest));
            DrawType(adminTypeCheckpointHitArea, QuestbookQuestNodeType.Checkpoint, isAdminTypeCheckpointHovered,
                GetAdminToolbarLabel(AdminToolbarIcon.Checkpoint));
            DrawType(adminTypeKillHitArea, QuestbookQuestNodeType.Kill, isAdminTypeKillHovered,
                GetAdminToolbarLabel(AdminToolbarIcon.Kill));
        }

        /// <summary>
        /// Under description: full-width equal chips Once | Cooldown | Instant
        /// (+ minutes box flush-right when cooldown).
        /// </summary>
        private void DrawQuestEditRepeatRow(
            Cairo.Context ctx,
            double unit,
            double panelX,
            double rowY,
            double panelWidth,
            List<LayoutRect> hitAreas,
            List<AdminFormFieldRef> fieldRefs)
        {
            double u = unit;
            double rowH = QuestbookGuiLayout.QuestEditModalRepeatRowHeight * u;
            double gap = 5 * u;
            double borderW = System.Math.Max(1.0, u);
            double radius = 5 * u;

            CairoFont labelFont = CreateMontserratFont(11 * u, QuestbookGuiLayout.AdminTileIdleContentColor);
            string label = QuestbookLang.GetLocal("admin.quest_edit.repeat");
            double labelW = System.Math.Max(MeasureTextWidth(labelFont, label) + 6 * u, 56 * u);
            DrawText(ctx, labelFont, label, panelX, GetTextBaselineY(labelFont, rowY, rowH, rowH));

            string mode = QuestbookRepeatMode.Normalize(adminData.EditedRepeatMode);
            bool isCooldown = string.Equals(mode, QuestbookRepeatMode.Cooldown, StringComparison.OrdinalIgnoreCase);

            // Right cluster: [minutes box] + "min" label — only in cooldown mode.
            string minLabelText = QuestbookLang.GetLocal("admin.quest_edit.repeat_minutes");
            CairoFont minLabelFont = CreateMontserratFont(11 * u, QuestbookGuiLayout.AdminTileIdleContentColor);
            double minsBoxW = 52 * u;
            double minLabelW = isCooldown ? MeasureTextWidth(minLabelFont, minLabelText) : 0;
            double rightClusterW = isCooldown ? minsBoxW + 4 * u + minLabelW : 0;

            // Three equal chips fill everything between label and right cluster.
            const int chipCount = 3;
            double chipsLeft = panelX + labelW + gap;
            double chipsRight = panelX + panelWidth - rightClusterW;
            if (isCooldown)
                chipsRight -= gap; // breathing room before minutes box

            double chipAreaW = System.Math.Max(0, chipsRight - chipsLeft);
            double chipW = (chipAreaW - gap * (chipCount - 1)) / chipCount;
            chipW = System.Math.Max(chipW, 48 * u);

            double x = chipsLeft;

            void Chip(ref LayoutRect hit, string modeKey, string textKey)
            {
                hit = new LayoutRect(x, rowY, chipW, rowH);
                bool active = string.Equals(mode, modeKey, StringComparison.OrdinalIgnoreCase);
                FillRoundedRectangle(ctx, hit.X, hit.Y, hit.Width, hit.Height, radius,
                    QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(ctx, hit.X, hit.Y, hit.Width, hit.Height, radius,
                    borderW,
                    active
                        ? QuestbookGuiLayout.AdminTileActiveContentColor
                        : QuestbookGuiLayout.AdminTileBorderColor);
                CairoFont font = CreateMontserratFont(
                    System.Math.Clamp(rowH * 0.38, 10 * u, 13 * u),
                    active
                        ? QuestbookGuiLayout.AdminTileActiveContentColor
                        : QuestbookGuiLayout.AdminTileIdleContentColor);
                DrawCenteredText(ctx, font, QuestbookLang.GetLocal(textKey), hit);
                x += chipW + gap;
            }

            Chip(ref adminRepeatOnceHitArea, QuestbookRepeatMode.Once, "admin.quest_edit.repeat_once");
            Chip(ref adminRepeatCooldownHitArea, QuestbookRepeatMode.Cooldown, "admin.quest_edit.repeat_cooldown");
            Chip(ref adminRepeatInstantHitArea, QuestbookRepeatMode.Instant, "admin.quest_edit.repeat_instant");

            if (isCooldown)
            {
                // Flush-right minutes input + unit label.
                double minsX = panelX + panelWidth - rightClusterW;
                adminRepeatHoursHitArea = new LayoutRect(minsX, rowY, minsBoxW, rowH);
                bool focused = adminData.FocusedField.Kind == AdminFormFieldKind.CooldownMinutes;
                FillRoundedRectangle(ctx, adminRepeatHoursHitArea.X, adminRepeatHoursHitArea.Y,
                    adminRepeatHoursHitArea.Width, adminRepeatHoursHitArea.Height, radius,
                    QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(ctx, adminRepeatHoursHitArea.X, adminRepeatHoursHitArea.Y,
                    adminRepeatHoursHitArea.Width, adminRepeatHoursHitArea.Height, radius,
                    borderW,
                    focused
                        ? QuestbookGuiLayout.AdminTileActiveContentColor
                        : QuestbookGuiLayout.AdminTileBorderColor);

                AdminFormFieldRef minsField = new(AdminFormFieldKind.CooldownMinutes);
                string minsText = adminData.GetFieldValue(minsField);
                CairoFont minsFont = CreateMontserratFont(12 * u, QuestbookGuiLayout.AdminTileActiveContentColor);
                double minsTextW = MeasureTextWidth(minsFont, minsText);
                double minsTextX = adminRepeatHoursHitArea.X
                    + ((adminRepeatHoursHitArea.Width - minsTextW) * 0.5);
                DrawText(ctx, minsFont, minsText, minsTextX,
                    GetTextBaselineY(minsFont, adminRepeatHoursHitArea.Y, adminRepeatHoursHitArea.Height, adminRepeatHoursHitArea.Height));
                if (focused)
                {
                    DrawTextCaret(
                        ctx, minsFont, minsText,
                        minsTextX,
                        adminRepeatHoursHitArea.Y, adminRepeatHoursHitArea.Height,
                        QuestbookGuiLayout.AdminTileActiveContentColor);
                }

                hitAreas.Add(adminRepeatHoursHitArea);
                fieldRefs.Add(minsField);

                DrawText(ctx, minLabelFont, minLabelText,
                    minsX + minsBoxW + 4 * u,
                    GetTextBaselineY(minLabelFont, rowY, rowH, rowH));
            }
            else
            {
                adminRepeatHoursHitArea = new LayoutRect(0, 0, 0, 0);
            }
        }

        private void DrawQuestEditScrollableItemList(
            Cairo.Context ctx,
            double unit,
            LayoutRect viewport,
            List<QuestbookAdminItemEntry> items,
            bool isGoals,
            ref double scrollOffset,
            ref LayoutRect[] removeHitAreas,
            List<LayoutRect> hitAreas,
            List<AdminFormFieldRef> fieldRefs)
        {
            // Design-unit → screen scale for this modal instance.
            double u = unit;
            double chip = QuestbookGuiLayout.QuestEditModalChipSize * u;
            double chipR = QuestbookGuiLayout.QuestEditModalChipRadius * u;
            double rowStep = (QuestbookGuiLayout.QuestEditModalRowHeight + QuestbookGuiLayout.QuestEditModalRowGap) * u;
            double rowHeight = QuestbookGuiLayout.QuestEditModalRowHeight * u;
            // First row inset inside panel (197.5 − 191.5 = 6).
            double rowPadY = 6 * u;
            double borderW = System.Math.Max(1.0, u);

            // Group 1171 goals · 1171(1) awards · 1171(2) kill — different name width & chips.
            bool killGoalRows = isGoals && adminData.EditedNodeType == QuestbookQuestNodeType.Kill;
            bool showCraft = isGoals && !killGoalRows;
            // Kill: paperclip (all types) + delete only. Awards: name/qty/delete only.
            bool showVariants = isGoals;
            // Take/consume only on quest goals (not kill, not awards).
            bool showTake = isGoals && !killGoalRows;

            double pickX = viewport.X + QuestbookGuiLayout.QuestEditModalChipPadX * u;
            double nameOuterX = viewport.X + QuestbookGuiLayout.QuestEditModalNameFrameX * u;

            double joinDesign;
            double qtyRightDesign;
            if (killGoalRows)
            {
                joinDesign = QuestbookGuiLayout.QuestEditModalKillNameJoinX;
                qtyRightDesign = QuestbookGuiLayout.QuestEditModalKillQtyRightX;
            }
            else if (isGoals)
            {
                joinDesign = QuestbookGuiLayout.QuestEditModalNameJoinX;
                qtyRightDesign = QuestbookGuiLayout.QuestEditModalQtyRightX;
            }
            else
            {
                joinDesign = QuestbookGuiLayout.QuestEditModalAwardsNameJoinX;
                qtyRightDesign = QuestbookGuiLayout.QuestEditModalAwardsQtyRightX;
            }

            double joinX = viewport.X + joinDesign * u;
            double qtyRight = viewport.X + qtyRightDesign * u;
            // Group 896 goals: Create/take@288 · Craft@322 · All Types@356 · delete@390.
            // Kill: All Types@356 + delete. Awards: name/qty stretched, delete only.
            double takeX = viewport.X + QuestbookGuiLayout.QuestEditModalTakeX * u;
            double craftX = viewport.X + QuestbookGuiLayout.QuestEditModalCraftX * u;
            double variantsX = viewport.X + QuestbookGuiLayout.QuestEditModalVariantsX * u;
            double deleteX = viewport.X + QuestbookGuiLayout.QuestEditModalDeleteX * u;
            double nameTextX = nameOuterX + 6 * u;
            double nameTextW = System.Math.Max(12 * u, joinX - nameTextX - 4 * u);

            double contentHeight = items.Count * rowStep;
            double maxScroll = System.Math.Max(0, contentHeight - viewport.Height);
            scrollOffset = System.Math.Clamp(scrollOffset, 0, maxScroll);

            removeHitAreas = new LayoutRect[items.Count];
            LayoutRect[] pickHitAreas = new LayoutRect[items.Count];
            LayoutRect[] matchHitAreas = showVariants ? new LayoutRect[items.Count] : [];
            LayoutRect[] craftHitAreas = showCraft ? new LayoutRect[items.Count] : [];
            LayoutRect[] killHitAreas = [];
            LayoutRect[] takeHitAreas = showTake ? new LayoutRect[items.Count] : [];
            if (isGoals)
            {
                goalsItemPickHitAreas = pickHitAreas;
                goalsMatchToggleHitAreas = matchHitAreas;
                goalsCraftToggleHitAreas = craftHitAreas;
                goalsKillToggleHitAreas = killHitAreas;
                goalsTakeToggleHitAreas = takeHitAreas;
            }
            else
            {
                awardsItemPickHitAreas = pickHitAreas;
                awardsMatchToggleHitAreas = [];
                awardsTakeToggleHitAreas = []; // awards: no take chip — name/qty fill the row
            }

            ctx.Save();
            ctx.Rectangle(viewport.X, viewport.Y, viewport.Width, viewport.Height);
            ctx.Clip();

            if (items.Count == 0)
            {
                CairoFont emptyFont = CreateMontserratFont(11 * u, QuestbookGuiLayout.AdminTitleColor);
                DrawText(ctx, emptyFont, QuestbookLang.GetLocal("admin.quest_edit.empty_list"),
                    viewport.X + (8 * u), viewport.Y + (12 * u));
            }

            for (int index = 0; index < items.Count; index++)
            {
                double rowY = viewport.Y + rowPadY + (index * rowStep) - scrollOffset;
                if (rowY + rowHeight < viewport.Y || rowY > viewport.Y + viewport.Height)
                    continue;

                QuestbookAdminItemEntry entry = items[index];
                bool isKillRow = isGoals
                    && (entry.IsKillObjective || adminData.EditedNodeType == QuestbookQuestNodeType.Kill);

                AdminFormFieldRef countField = new(
                    isGoals ? AdminFormFieldKind.GoalCount : AdminFormFieldKind.AwardCount,
                    index);
                AdminFormFieldRef codeField = new(
                    isGoals ? AdminFormFieldKind.GoalId : AdminFormFieldKind.AwardId,
                    index);

                LayoutRect pickRect = new(pickX, rowY, chip, chip);
                LayoutRect nameRect = new(nameOuterX, rowY, joinX - nameOuterX, rowHeight);
                LayoutRect countRect = new(joinX, rowY, qtyRight - joinX, rowHeight);
                LayoutRect craftRect = showCraft
                    ? new LayoutRect(craftX, rowY, chip, chip)
                    : new LayoutRect(0, 0, 0, 0);
                LayoutRect matchRect = showVariants
                    ? new LayoutRect(variantsX, rowY, chip, chip)
                    : new LayoutRect(0, 0, 0, 0);
                LayoutRect takeRect = showTake
                    ? new LayoutRect(takeX, rowY, chip, chip)
                    : new LayoutRect(0, 0, 0, 0);
                LayoutRect removeRect = new(deleteX, rowY, chip, chip);

                pickHitAreas[index] = pickRect;
                if (showVariants)
                    matchHitAreas[index] = matchRect;
                if (showTake)
                    takeHitAreas[index] = takeRect;
                if (showCraft)
                    craftHitAreas[index] = craftRect;

                hitAreas.Add(nameRect);
                fieldRefs.Add(codeField);
                hitAreas.Add(countRect);
                fieldRefs.Add(countField);
                removeHitAreas[index] = removeRect;

                bool pickerActive = adminItemPickerTarget is { } pickerTarget
                    && pickerTarget.IsGoals == isGoals
                    && pickerTarget.ListIndex == index;
                string savedCode = entry.GetSavedCollectibleCode();
                string iconCode = QuestbookItemDisplayHelper.ResolveDisplayIconCode(capi, savedCode, isKillRow);
                bool hasCode = !string.IsNullOrWhiteSpace(savedCode);
                bool hasItem = !string.IsNullOrWhiteSpace(entry.CollectibleCode);
                bool codeFocused = adminData.FocusedField == codeField;
                bool countFocused = adminData.FocusedField == countField;

                // Pick chip 31×31.
                FillRoundedRectangle(ctx, pickRect.X, pickRect.Y, pickRect.Width, pickRect.Height, chipR,
                    QuestbookGuiLayout.AdminTileBackgroundColor);
                StrokeRoundedRectangle(ctx, pickRect.X, pickRect.Y, pickRect.Width, pickRect.Height, chipR,
                    borderW,
                    pickerActive
                        ? QuestbookGuiLayout.AdminTileActiveContentColor
                        : QuestbookGuiLayout.AdminTileBorderColor);

                if (hasCode)
                {
                    double iconSide = chip * 0.86;
                    LayoutRect iconRect = new(
                        pickRect.X + ((chip - iconSide) / 2),
                        pickRect.Y + ((chip - iconSide) / 2),
                        iconSide,
                        iconSide);
                    adminEditorIconRenderRequests.Add(new QuestItemIconRenderRequest(
                        iconCode,
                        iconRect,
                        false,
                        0,
                        QuestbookItemIconContext.Modal,
                        viewport));
                }
                else
                {
                    // Empty pick: crosshair glyph (#3C3C3C) like Group 1169.
                    DrawAdminToolbarIcon(
                        ctx,
                        AdminToolbarIcon.Select,
                        pickRect.X + chip * 0.12,
                        pickRect.Y + chip * 0.12,
                        chip * 0.76,
                        QuestbookGuiLayout.AdminModalMutedIconColor);
                }

                // Dual name+qty frame (not two separate full-rounded rects).
                DrawQuestEditNameQtyFrame(
                    ctx,
                    nameOuterX,
                    joinX,
                    qtyRight,
                    rowY,
                    rowHeight,
                    chipR,
                    QuestbookGuiLayout.AdminTileBackgroundColor,
                    QuestbookGuiLayout.AdminTileBorderColor,
                    borderW,
                    codeFocused,
                    countFocused);

                string itemLabel;
                if (codeFocused)
                {
                    itemLabel = adminData.GetFieldValue(codeField);
                }
                else if (!hasItem)
                {
                    itemLabel = isKillRow
                        ? QuestbookLang.GetLocal("admin.quest_edit.pick_entity")
                        : QuestbookLang.GetLocal("admin.quest_edit.pick_item");
                }
                else
                {
                    itemLabel = GetQuestItemSlot(iconCode)?.Itemstack?.GetName()
                        ?? GetQuestItemSlot(entry.CollectibleCode)?.Itemstack?.GetName()
                        ?? StripItemCodeForDisplay(entry.CollectibleCode);
                }

                CairoFont nameFont = CreateMontserratFont(11 * u,
                    !hasItem && !codeFocused
                        ? QuestbookGuiLayout.AdminPanelPlaceholderColor
                        : QuestbookGuiLayout.AdminModalChipTextColor);

                ctx.Save();
                ctx.Rectangle(nameTextX, rowY, nameTextW, rowHeight);
                ctx.Clip();
                DrawText(ctx, nameFont, itemLabel, nameTextX,
                    GetTextBaselineY(nameFont, rowY, rowHeight, 16 * u));
                if (codeFocused)
                {
                    DrawTextCaret(ctx, nameFont, itemLabel, nameTextX, rowY, rowHeight,
                        QuestbookGuiLayout.AdminModalChipTextColor);
                }
                ctx.Restore();

                string countPlaceholder = QuestbookLang.GetLocal("admin.num");
                string countDisplay = countFocused
                    ? (entry.Count > 0 ? entry.Count.ToString() : string.Empty)
                    : (entry.Count > 0 ? entry.Count.ToString() : countPlaceholder);
                double[] countColor = countFocused || entry.Count > 0
                    ? QuestbookGuiLayout.AdminModalChipTextColor
                    : QuestbookGuiLayout.AdminPanelPlaceholderColor;
                CairoFont countFont = CreateMontserratFont(12 * u, countColor);
                double countTextWidth = MeasureTextWidth(countFont, countDisplay);
                double countTextX = countRect.X + ((countRect.Width - countTextWidth) / 2);
                DrawText(ctx, countFont, countDisplay, countTextX,
                    GetTextBaselineY(countFont, countRect.Y, countRect.Height, countRect.Height));
                if (countFocused)
                {
                    DrawTextCaret(ctx, countFont, countDisplay, countTextX, countRect.Y, countRect.Height, countColor);
                }

                // Goals: take · craft · variants · delete. Awards: delete only (row stretched).
                if (showTake)
                {
                    DrawAdminFlagToggle(
                        ctx, u, takeRect, AdminFlagIcon.Take,
                        entry.ConsumeOnComplete, enabled: true, hovered: false);
                }

                if (showCraft)
                {
                    DrawAdminFlagToggle(
                        ctx, u, craftRect, AdminFlagIcon.Craft,
                        entry.IsCraftObjective, enabled: true, hovered: false);
                }

                if (showVariants)
                {
                    DrawAdminFlagToggle(
                        ctx, u, matchRect, AdminFlagIcon.AllVariants,
                        entry.MatchAllVariants, enabled: true, hovered: false);
                }

                // Delete chip — red trash (#FD5A53).
                DrawAdminFlagToggle(
                    ctx, u, removeRect, AdminFlagIcon.Delete,
                    isActive: true, enabled: true, hovered: false);
            }

            ctx.Restore();

            // Scrollbar OUTSIDE panel right edge (goals: panel right + 12.5 → track).
            if (maxScroll > 0)
            {
                double trackWidth = QuestbookGuiLayout.QuestEditModalListScrollbarWidth * u;
                double trackX = viewport.X + viewport.Width
                    + (QuestbookGuiLayout.QuestEditModalListScrollbarOutside * u);
                double thumbHeight = System.Math.Max(18 * u, viewport.Height * (viewport.Height / contentHeight));
                double thumbTravel = System.Math.Max(1, viewport.Height - thumbHeight);
                double thumbY = viewport.Y + ((scrollOffset / maxScroll) * thumbTravel);

                FillRoundedRectangle(ctx, trackX, viewport.Y, trackWidth, viewport.Height, trackWidth * 0.5,
                    QuestbookGuiLayout.AdminTileBorderColor); // #353432 track
                FillRoundedRectangle(ctx, trackX, thumbY, trackWidth, thumbHeight, trackWidth * 0.5,
                    [0x74 / 255.0, 0x74 / 255.0, 0x74 / 255.0, 1.0]); // #747474 thumb
            }
        }

        private double GetQuestEditListRowStep(double fitScale)
        {
            return (QuestbookGuiLayout.QuestEditModalRowHeight + QuestbookGuiLayout.QuestEditModalRowGap) * fitScale;
        }

        private static string StripItemCodeForDisplay(string collectibleCode)
        {
            int colonIndex = collectibleCode.IndexOf(':');
            return colonIndex >= 0 ? collectibleCode[(colonIndex + 1)..] : collectibleCode;
        }

        private void EnsureQuestEditFocusedRowVisible(AdminFormFieldRef field)
        {
            if (field.Kind == AdminFormFieldKind.GoalCount)
                EnsureQuestEditListRowVisible(goalsListViewportHitArea, field.ListIndex, ref goalsListScrollOffset);
            else if (field.Kind == AdminFormFieldKind.AwardCount)
                EnsureQuestEditListRowVisible(awardsListViewportHitArea, field.ListIndex, ref awardsListScrollOffset);
        }

        private void EnsureQuestEditListRowVisible(LayoutRect viewport, int rowIndex, ref double scrollOffset)
        {
            if (viewport.IsEmpty || rowIndex < 0)
                return;

            double rowStep = GetQuestEditListRowStep(currentFitScale);
            double rowTop = rowIndex * rowStep;
            double rowBottom = rowTop + (QuestbookGuiLayout.QuestEditModalRowHeight * currentFitScale);
            double maxScroll = System.Math.Max(0, (rowIndex + 1) * rowStep - viewport.Height);

            if (rowTop < scrollOffset)
                scrollOffset = rowTop;
            else if (rowBottom > scrollOffset + viewport.Height)
                scrollOffset = rowBottom - viewport.Height;

            scrollOffset = System.Math.Clamp(scrollOffset, 0, maxScroll);
        }

        private bool TryHandleQuestEditModalMouseWheel(MouseWheelEventArgs args)
        {
            int mouseX = capi.Input.MouseX;
            int mouseY = capi.Input.MouseY;
            float wheelDelta = args.deltaPrecise != 0 ? args.deltaPrecise : args.delta;
            if (wheelDelta == 0)
                return false;

            double scrollStep = GetQuestEditListRowStep(currentFitScale) * 0.85;
            double direction = wheelDelta > 0 ? -1 : 1;

            // Item / creature catalog picker scroll.
            if (adminItemPickerTarget != null
                && (adminEntityPickerSlots.Length > 0 || adminItemPickerSlots.Length > 0)
                && (adminItemPickerPanelHitArea.Contains(mouseX, mouseY)
                    || ToScreenRect(adminItemPickerPanelHitArea).Contains(mouseX, mouseY)))
            {
                adminEntityPickerScrollOffset = System.Math.Max(0,
                    adminEntityPickerScrollOffset + (direction * scrollStep * 1.4));
                RequestContentRefresh();
                return true;
            }

            if (goalsListViewportHitArea.Contains(mouseX, mouseY))
            {
                double maxScroll = System.Math.Max(0,
                    (adminData.Goals.Count * GetQuestEditListRowStep(currentFitScale)) - goalsListViewportHitArea.Height);
                goalsListScrollOffset = System.Math.Clamp(goalsListScrollOffset + (direction * scrollStep), 0, maxScroll);
                RequestContentRefresh();
                return true;
            }

            if (awardsListViewportHitArea.Contains(mouseX, mouseY))
            {
                double maxScroll = System.Math.Max(0,
                    (adminData.Awards.Count * GetQuestEditListRowStep(currentFitScale)) - awardsListViewportHitArea.Height);
                awardsListScrollOffset = System.Math.Clamp(awardsListScrollOffset + (direction * scrollStep), 0, maxScroll);
                RequestContentRefresh();
                return true;
            }

            return false;
        }

        private bool TryHandleQuestEditModalMouseDown(double mouseX, double mouseY)
        {
            if (!isQuestEditModalOpen)
                return false;

            if (adminItemPickerTarget != null)
            {
                if (adminItemPickerCancelHitArea.Contains(mouseX, mouseY))
                {
                    CloseAdminItemPicker();
                    ComposeDialog();
                    return true;
                }

                // Focus search box (item or creature catalog).
                if (adminEntityPickerSearchHitArea.Contains(mouseX, mouseY)
                    || ToScreenRect(adminEntityPickerSearchHitArea).Contains(mouseX, mouseY))
                {
                    adminEntityPickerSearchFocused = true;
                    adminData.FocusedField = AdminFormFieldRef.None;
                    RequestContentRefresh();
                    return true;
                }

                foreach ((string entityCode, string _, DummySlot _, LayoutRect hitArea) in adminEntityPickerSlots)
                {
                    if (!ToScreenRect(hitArea).Contains(mouseX, mouseY))
                        continue;

                    adminEntityPickerSearchFocused = false;
                    TryAssignAdminPickerEntity(entityCode);
                    ComposeDialog();
                    return true;
                }

                foreach ((ItemSlot slot, LayoutRect hitArea, string _, string assignCode) in adminItemPickerSlots)
                {
                    if (!ToScreenRect(hitArea).Contains(mouseX, mouseY))
                        continue;

                    adminEntityPickerSearchFocused = false;
                    if (TryAssignAdminPickerItem(slot, assignCode))
                        ComposeDialog();
                    else
                        ComposeDialog();
                    return true;
                }

                if (adminItemPickerPanelHitArea.Contains(mouseX, mouseY))
                {
                    // Click empty panel area: unfocus search.
                    if (adminEntityPickerSearchFocused)
                    {
                        adminEntityPickerSearchFocused = false;
                        RequestContentRefresh();
                    }

                    return true;
                }

                CloseAdminItemPicker();
                ComposeDialog();
                return true;
            }

            if (questEditModalSaveButtonHitArea.Contains(mouseX, mouseY))
            {
                SaveAndCloseQuestEditModal();
                ComposeDialog();
                return true;
            }

            for (int i = 0; i < questEditLangButtonHitAreas.Length && i < questEditLangCodes.Length; i++)
            {
                if (!questEditLangButtonHitAreas[i].Contains(mouseX, mouseY))
                    continue;

                adminData.SwitchEditorLanguage(questEditLangCodes[i]);
                adminData.FocusedField = new AdminFormFieldRef(AdminFormFieldKind.Information);
                RequestContentRefresh();
                return true;
            }

            if (adminTypeStartHitArea.Contains(mouseX, mouseY))
            {
                TrySetEditedNodeType(QuestbookQuestNodeType.Start);
                return true;
            }

            if (adminTypeQuestHitArea.Contains(mouseX, mouseY))
            {
                TrySetEditedNodeType(QuestbookQuestNodeType.Quest);
                return true;
            }

            if (adminTypeCheckpointHitArea.Contains(mouseX, mouseY))
            {
                TrySetEditedNodeType(QuestbookQuestNodeType.Checkpoint);
                return true;
            }

            if (adminTypeKillHitArea.Contains(mouseX, mouseY))
            {
                TrySetEditedNodeType(QuestbookQuestNodeType.Kill);
                return true;
            }

            if (adminRepeatOnceHitArea.Contains(mouseX, mouseY))
            {
                adminData.CommitCooldownMinutesDraft();
                adminData.EditedRepeatMode = QuestbookRepeatMode.Once;
                ApplyFormToSelectedNode();
                RequestContentRefresh();
                return true;
            }

            if (adminRepeatCooldownHitArea.Contains(mouseX, mouseY))
            {
                adminData.CommitCooldownMinutesDraft();
                adminData.EditedRepeatMode = QuestbookRepeatMode.Cooldown;
                if (adminData.EditedCooldownSeconds < 60)
                    adminData.EditedCooldownSeconds = 60; // default 1 minute
                ApplyFormToSelectedNode();
                RequestContentRefresh();
                return true;
            }

            // Explicit focus for minutes box (also in adminInputFieldHitAreas).
            if (adminRepeatHoursHitArea.Width > 0 && adminRepeatHoursHitArea.Contains(mouseX, mouseY))
            {
                // Start draft from current value so first backspace can clear it.
                if (adminData.CooldownMinutesDraft == null)
                    adminData.CooldownMinutesDraft = adminData.GetFieldValue(new AdminFormFieldRef(AdminFormFieldKind.CooldownMinutes));
                adminData.FocusedField = new AdminFormFieldRef(AdminFormFieldKind.CooldownMinutes);
                ResetTextCaretBlink();
                RequestContentRefresh();
                return true;
            }

            if (adminRepeatInstantHitArea.Contains(mouseX, mouseY))
            {
                adminData.CommitCooldownMinutesDraft();
                adminData.EditedRepeatMode = QuestbookRepeatMode.Instant;
                ApplyFormToSelectedNode();
                RequestContentRefresh();
                return true;
            }

            if (goalsAddButtonHitArea.Contains(mouseX, mouseY))
            {
                adminData.AddGoal();
                OpenAdminItemPicker(isGoals: true, adminData.Goals.Count - 1);
                EnsureQuestEditFocusedRowVisible(adminData.FocusedField);
                ComposeDialog();
                return true;
            }

            if (awardsAddButtonHitArea.Contains(mouseX, mouseY))
            {
                adminData.AddAward();
                OpenAdminItemPicker(isGoals: false, adminData.Awards.Count - 1);
                EnsureQuestEditFocusedRowVisible(adminData.FocusedField);
                ComposeDialog();
                return true;
            }

            for (int i = 0; i < adminInputFieldHitAreas.Length; i++)
            {
                if (!adminInputFieldHitAreas[i].Contains(mouseX, mouseY))
                    continue;

                AdminFormFieldRef field = adminInputFieldRefs[i];
                if (adminData.FocusedField.Kind == AdminFormFieldKind.CooldownMinutes
                    && field.Kind != AdminFormFieldKind.CooldownMinutes)
                    adminData.CommitCooldownMinutesDraft();

                if (field.Kind == AdminFormFieldKind.CooldownMinutes
                    && adminData.CooldownMinutesDraft == null)
                    adminData.CooldownMinutesDraft = adminData.GetFieldValue(field);

                if (field.IsCount && field.Kind != AdminFormFieldKind.CooldownMinutes
                    && adminData.GetFieldValue(field) == "0")
                    adminData.SetFieldValue(field, "0");

                CloseAdminItemPicker();
                adminData.FocusedField = field;
                ResetTextCaretBlink();
                EnsureQuestEditFocusedRowVisible(field);
                ComposeDialog();
                return true;
            }

            if (TryToggleQuestEditTakeOnComplete(mouseX, mouseY))
                return true;

            if (TryToggleQuestEditCraftObjective(mouseX, mouseY))
                return true;

            if (TryToggleQuestEditKillObjective(mouseX, mouseY))
                return true;

            if (TryToggleQuestEditMatchVariant(isGoals: true, mouseX, mouseY))
                return true;

            if (TryToggleQuestEditMatchVariant(isGoals: false, mouseX, mouseY))
                return true;

            for (int index = 0; index < goalsItemPickHitAreas.Length; index++)
            {
                if (!goalsItemPickHitAreas[index].Contains(mouseX, mouseY))
                    continue;

                adminData.FocusedField = AdminFormFieldRef.None;
                OpenAdminItemPicker(isGoals: true, index);
                ComposeDialog();
                return true;
            }

            for (int index = 0; index < awardsItemPickHitAreas.Length; index++)
            {
                if (!awardsItemPickHitAreas[index].Contains(mouseX, mouseY))
                    continue;

                adminData.FocusedField = AdminFormFieldRef.None;
                OpenAdminItemPicker(isGoals: false, index);
                ComposeDialog();
                return true;
            }

            for (int index = 0; index < goalsRemoveHitAreas.Length; index++)
            {
                if (!goalsRemoveHitAreas[index].Contains(mouseX, mouseY))
                    continue;

                adminData.RemoveGoal(index);
                ComposeDialog();
                return true;
            }

            for (int index = 0; index < awardsRemoveHitAreas.Length; index++)
            {
                if (!awardsRemoveHitAreas[index].Contains(mouseX, mouseY))
                    continue;

                adminData.RemoveAward(index);
                ComposeDialog();
                return true;
            }

            if (!questEditModalPanelHitArea.Contains(mouseX, mouseY))
            {
                SaveAndCloseQuestEditModal();
                ComposeDialog();
                return true;
            }

            adminData.FocusedField = AdminFormFieldRef.None;
            ComposeDialog();
            return true;
        }

        private bool TryToggleQuestEditTakeOnComplete(double mouseX, double mouseY)
        {
            // Awards no longer have a take chip — only quest goals.
            return TryToggleTakeOnList(goalsTakeToggleHitAreas, adminData.Goals, mouseX, mouseY);
        }

        private bool TryToggleTakeOnList(
            LayoutRect[] hitAreas,
            List<QuestbookAdminItemEntry> items,
            double mouseX,
            double mouseY)
        {
            for (int index = 0; index < hitAreas.Length; index++)
            {
                if (!hitAreas[index].Contains(mouseX, mouseY))
                    continue;

                if (index < 0 || index >= items.Count)
                    return true;

                QuestbookAdminItemEntry entry = items[index];
                if (entry.IsKillObjective)
                    return true;
                entry.ConsumeOnComplete = !entry.ConsumeOnComplete;
                ApplyFormToSelectedNode();
                RequestContentRefresh();
                return true;
            }

            return false;
        }

        private bool TryToggleQuestEditCraftObjective(double mouseX, double mouseY)
        {
            for (int index = 0; index < goalsCraftToggleHitAreas.Length; index++)
            {
                if (!goalsCraftToggleHitAreas[index].Contains(mouseX, mouseY))
                    continue;

                if (index < 0 || index >= adminData.Goals.Count)
                    return true;

                QuestbookAdminItemEntry entry = adminData.Goals[index];
                if (entry.IsKillObjective)
                    return true;
                // Independent of take — both can be on (craft then turn in).
                entry.IsCraftObjective = !entry.IsCraftObjective;
                ApplyFormToSelectedNode();
                RequestContentRefresh();
                return true;
            }

            return false;
        }

        private bool TryToggleQuestEditKillObjective(double mouseX, double mouseY)
        {
            for (int index = 0; index < goalsKillToggleHitAreas.Length; index++)
            {
                if (!goalsKillToggleHitAreas[index].Contains(mouseX, mouseY))
                    continue;

                if (index < 0 || index >= adminData.Goals.Count)
                    return true;

                QuestbookAdminItemEntry entry = adminData.Goals[index];
                entry.IsKillObjective = !entry.IsKillObjective;
                if (entry.IsKillObjective)
                {
                    // Kill is exclusive with craft / turn-in inventory modes.
                    entry.IsCraftObjective = false;
                    entry.ConsumeOnComplete = false;
                    entry.MatchAllVariants = true; // entity wildcards like game:drifter-*
                }

                ApplyFormToSelectedNode();
                RequestContentRefresh();
                return true;
            }

            return false;
        }

        private bool TryToggleQuestEditMatchVariant(bool isGoals, double mouseX, double mouseY)
        {
            LayoutRect[] hitAreas = isGoals ? goalsMatchToggleHitAreas : awardsMatchToggleHitAreas;
            List<QuestbookAdminItemEntry> items = isGoals ? adminData.Goals : adminData.Awards;

            for (int index = 0; index < hitAreas.Length; index++)
            {
                if (!hitAreas[index].Contains(mouseX, mouseY))
                    continue;

                if (index < 0 || index >= items.Count)
                    return true;

                QuestbookAdminItemEntry entry = items[index];
                entry.MatchAllVariants = !entry.MatchAllVariants;
                adminData.FocusedField = AdminFormFieldRef.None;
                SyncAdminFieldEdit();
                return true;
            }

            return false;
        }

        private void UpdateQuestEditModalHover(double mouseX, double mouseY)
        {
            if (!isQuestEditModalOpen)
                return;

            UpdateHover(ref isQuestEditModalSaveHovered, questEditModalSaveButtonHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isGoalsAddHovered, goalsAddButtonHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAwardsAddHovered, awardsAddButtonHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminTypeStartHovered, adminTypeStartHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminTypeQuestHovered, adminTypeQuestHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminTypeCheckpointHovered, adminTypeCheckpointHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminTypeKillHovered, adminTypeKillHitArea.Contains(mouseX, mouseY));

            // Catalog hover: update tooltip/highlight state only — no Cairo recompose (was the lag source).
            if (adminEntityPickerSlots.Length > 0 || adminItemPickerSlots.Length > 0)
            {
                // Search box / chrome above the grid must not activate partially scrolled tiles.
                if (IsAdminPickerSearchHovered(mouseX, mouseY)
                    || !IsAdminPickerViewportHovered(mouseX, mouseY))
                {
                    adminEntityPickerHoverLabel = null;
                    adminEntityPickerHoverRect = new LayoutRect(0, 0, 0, 0);
                    return;
                }

                string? nextHover = null;
                LayoutRect nextHoverRect = new(0, 0, 0, 0);

                foreach ((string _, string label, DummySlot _, LayoutRect hitArea) in adminEntityPickerSlots)
                {
                    if (!IsAdminPickerTileHovered(hitArea, mouseX, mouseY))
                        continue;
                    nextHover = label;
                    nextHoverRect = hitArea;
                    break;
                }

                if (nextHover == null)
                {
                    foreach ((ItemSlot _, LayoutRect hitArea, string label, string _) in adminItemPickerSlots)
                    {
                        if (!IsAdminPickerTileHovered(hitArea, mouseX, mouseY))
                            continue;
                        nextHover = label;
                        nextHoverRect = hitArea;
                        break;
                    }
                }

                adminEntityPickerHoverLabel = nextHover;
                adminEntityPickerHoverRect = nextHoverRect;
            }
        }

        private bool IsAdminPickerSearchHovered(double mouseX, double mouseY)
        {
            if (adminEntityPickerSearchHitArea.Width <= 0)
                return false;
            return adminEntityPickerSearchHitArea.Contains(mouseX, mouseY)
                || ToScreenRect(adminEntityPickerSearchHitArea).Contains(mouseX, mouseY);
        }

        private bool IsAdminPickerViewportHovered(double mouseX, double mouseY)
        {
            if (adminEntityPickerViewportLocal.Width <= 0)
                return false;
            return adminEntityPickerViewportLocal.Contains(mouseX, mouseY)
                || ToScreenRect(adminEntityPickerViewportLocal).Contains(mouseX, mouseY);
        }

        /// <summary>
        /// Tile hover only on the portion still inside the scroll viewport
        /// (ignores half-scrolled cells sticking under the search bar).
        /// </summary>
        private bool IsAdminPickerTileHovered(LayoutRect tileLocal, double mouseX, double mouseY)
        {
            LayoutRect tileScreen = ToScreenRect(tileLocal);
            LayoutRect viewportScreen = adminEntityPickerViewportLocal.Width > 0
                ? ToScreenRect(adminEntityPickerViewportLocal)
                : tileScreen;

            LayoutRect visible = tileScreen.Intersect(viewportScreen);
            if (visible.IsEmpty)
                return false;

            // Ignore thin slivers of partially scrolled rows/cols.
            if (visible.Height < tileScreen.Height * 0.45
                || visible.Width < tileScreen.Width * 0.45)
                return false;

            return visible.Contains(mouseX, mouseY);
        }

        private bool TryHandleAdminPanelMouseDown(double mouseX, double mouseY)
        {
            if (!adminData.IsAdminPanelOpen) return false;

            if (adminModeBranchesHitArea.Contains(mouseX, mouseY))
            {
                SwitchAdminEditorSection(AdminEditorSection.Branches);
                return true;
            }

            if (adminModeQuestsHitArea.Contains(mouseX, mouseY))
            {
                SwitchAdminEditorSection(AdminEditorSection.Quests);
                return true;
            }

            if (adminData.EditorSection == AdminEditorSection.Branches)
                return TryHandleAdminBranchPanelMouseDown(mouseX, mouseY);

            if (adminToolSelectHitArea.Contains(mouseX, mouseY))
            {
                adminData.ToggleToolMode(AdminToolMode.Select);
                ComposeDialog();
                return true;
            }
            if (adminToolQuestHitArea.Contains(mouseX, mouseY))
            {
                adminData.ToggleToolMode(AdminToolMode.NewQuest);
                ComposeDialog();
                return true;
            }
            if (adminToolLinkHitArea.Contains(mouseX, mouseY))
            {
                adminData.ToggleToolMode(AdminToolMode.LinkQuests);
                ComposeDialog();
                return true;
            }
            if (adminToolDeleteHitArea.Contains(mouseX, mouseY))
            {
                adminData.ToggleToolMode(AdminToolMode.DeleteNode);
                ComposeDialog();
                return true;
            }
            if (adminToolSaveHitArea.Contains(mouseX, mouseY))
            {
                ApplyFormToSelectedNode();
                HandleAdminSave();
                CommitAdminEditor();
                return true;
            }
            if (adminToolGridHitArea.Contains(mouseX, mouseY))
            {
                adminData.ShowGrid = !adminData.ShowGrid;
                ComposeDialog();
                return true;
            }
            if (adminToolClearHitArea.Contains(mouseX, mouseY))
            {
                // EDIT — click quests on the graph to open the editor.
                adminData.ToggleToolMode(AdminToolMode.EditQuest);
                ComposeDialog();
                return true;
            }
            if (adminToolCloseHitArea.Contains(mouseX, mouseY))
            {
                CloseAdminEditor(restoreSnapshot: true);
                ComposeDialog();
                return true;
            }

            return false;
        }

        private bool TryHandleAdminBranchPanelMouseDown(double mouseX, double mouseY)
        {
            if (adminBranchAddHitArea.Contains(mouseX, mouseY))
            {
                OpenBranchModal(BranchModalMode.Add);
                ComposeDialog();
                return true;
            }

            if (adminBranchRenameHitArea.Contains(mouseX, mouseY))
            {
                if (categories.Length == 0)
                    return true;

                OpenBranchModal(BranchModalMode.Rename);
                ComposeDialog();
                return true;
            }

            if (adminBranchDeleteHitArea.Contains(mouseX, mouseY))
            {
                if (categories.Length == 0)
                    return true;

                OpenBranchModal(BranchModalMode.DeleteConfirm);
                ComposeDialog();
                return true;
            }

            if (adminBranchCloseHitArea.Contains(mouseX, mouseY))
            {
                CloseAdminEditor(restoreSnapshot: true);
                ComposeDialog();
                return true;
            }

            for (int index = 0; index < adminBranchCardHitAreas.Length; index++)
            {
                if (!adminBranchCardHitAreas[index].Contains(mouseX, mouseY))
                    continue;

                if (selectedCategoryIndex != index)
                {
                    selectedCategoryIndex = index;
                    shouldCenterOnStartNode = true;
                    adminData.ClearSelection();
                    dataManager.EnsureCategoryContentLoaded(
                        categories[index].HeaderTitle,
                        includeI18n: adminData.IsAdminPanelOpen);
                    ComposeDialog();
                }

                return true;
            }

            return true;
        }

        private void UpdateAdminPanelHover(double mouseX, double mouseY)
        {
            if (!adminData.IsAdminPanelOpen)
            {
                UpdateHover(ref isAdminSettingsButtonHovered, adminSettingsButtonHitArea.Contains(mouseX, mouseY));
                return;
            }

            if (isAdminSettingsButtonHovered)
                UpdateHover(ref isAdminSettingsButtonHovered, false);

            UpdateHover(ref isAdminModeBranchesHovered, adminModeBranchesHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminModeQuestsHovered, adminModeQuestsHitArea.Contains(mouseX, mouseY));

            if (adminData.EditorSection == AdminEditorSection.Branches)
            {
                UpdateHover(ref isAdminBranchAddHovered, adminBranchAddHitArea.Contains(mouseX, mouseY));
                UpdateHover(ref isAdminBranchRenameHovered, adminBranchRenameHitArea.Contains(mouseX, mouseY));
                UpdateHover(ref isAdminBranchDeleteHovered, adminBranchDeleteHitArea.Contains(mouseX, mouseY));
                UpdateHover(ref isAdminBranchCloseHovered, adminBranchCloseHitArea.Contains(mouseX, mouseY));
                return;
            }

            UpdateHover(ref isAdminToolSelectHovered, adminToolSelectHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminToolQuestHovered, adminToolQuestHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminToolLinkHovered, adminToolLinkHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminToolDeleteHovered, adminToolDeleteHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminToolSaveHovered, adminToolSaveHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminToolClearHovered, adminToolClearHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminToolGridHovered, adminToolGridHitArea.Contains(mouseX, mouseY));
            UpdateHover(ref isAdminToolCloseHovered, adminToolCloseHitArea.Contains(mouseX, mouseY));

        }

        private void UpdateHover(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            RequestContentRefresh();
        }

        private bool TryHandleAdminGraphToolClick(double mouseX, double mouseY)
        {
            if (!adminData.IsAdminPanelOpen
                || adminData.EditorSection != AdminEditorSection.Quests
                || adminData.ToolMode == AdminToolMode.None)
                return false;

            if (!rightPanelViewportHitArea.Contains(mouseX, mouseY))
                return false;

            int? nodeId = GetNodeIdAtMouse(mouseX, mouseY);
            switch (adminData.ToolMode)
            {
                case AdminToolMode.Select:
                    // Select only — drag is handled on mouse-down; click selects without opening editor.
                    HandleSelectToolClick(nodeId);
                    return true;
                case AdminToolMode.EditQuest:
                    HandleEditToolClick(nodeId);
                    return true;
                case AdminToolMode.NewQuest:
                    HandleNewQuestToolClick(mouseX, mouseY, nodeId);
                    return true;
                case AdminToolMode.LinkQuests:
                    HandleLinkToolClick(nodeId);
                    return true;
                case AdminToolMode.DeleteNode:
                    HandleDeleteToolClick(nodeId);
                    return true;
            }

            return false;
        }

        private int? GetNodeIdAtMouse(double mouseX, double mouseY)
        {
            QuestbookCategoryDefinition category = GetSelectedCategory();
            for (int index = 0; index < questCardHitAreas.Length && index < category.Nodes.Length; index++)
            {
                if (questCardHitAreas[index].Contains(mouseX, mouseY))
                    return category.Nodes[index].Id;
            }
            return null;
        }

        private bool TryScreenToGraphCoords(double mouseX, double mouseY, QuestbookQuestNodeType nodeType,
            out double graphX, out double graphY)
        {
            graphX = 0;
            graphY = 0;
            if (!rightPanelViewportHitArea.Contains(mouseX, mouseY))
                return false;

            double graphScale = currentFitScale * graphZoom;
            double clickGraphX = (mouseX - rightPanelGraphBaseX - graphPanX) / graphScale;
            double clickGraphY = (mouseY - rightPanelGraphBaseY - graphPanY) / graphScale;
            double centerOffset = nodeType == QuestbookQuestNodeType.Start ? 0 : QuestbookGuiLayout.GraphNodeCenterOffset;
            double nodeSize = nodeType == QuestbookQuestNodeType.Start
                ? QuestbookGuiLayout.GraphStartNodeSize
                : QuestbookGuiLayout.GraphNodeSize;
            graphX = clickGraphX - centerOffset - (nodeSize / 2);
            graphY = clickGraphY - centerOffset - (nodeSize / 2);
            return true;
        }

        private void HandleSelectToolClick(int? nodeId)
        {
            // SELECT tool: pick / deselect only. Drag moves the node; never opens the editor.
            if (nodeId == null)
            {
                CloseQuestEditModal();
                adminData.ClearSelection();
                ComposeDialog();
                return;
            }

            SelectNode(nodeId.Value);
            ComposeDialog();
        }

        private void HandleEditToolClick(int? nodeId)
        {
            // EDIT tool: click a quest on the graph to open its property editor.
            if (nodeId == null)
            {
                CloseQuestEditModal();
                ComposeDialog();
                return;
            }

            SelectNode(nodeId.Value);
            OpenQuestEditModal();
            ComposeDialog();
        }

        private void HandleNewQuestToolClick(double mouseX, double mouseY, int? nodeId)
        {
            if (nodeId != null)
                return;

            if (!TryScreenToGraphCoords(mouseX, mouseY, QuestbookQuestNodeType.Quest, out double graphX, out double graphY))
                return;

            SnapGraphCoordsToGrid(ref graphX, ref graphY);

            var category = GetSelectedCategory();
            if (category == null)
                return;

            int id = GenerateNodeId(category);
            var nodes = category.Nodes.ToList();
            nodes.Add(CreateQuestNodeFromForm(id, graphX, graphY, QuestbookQuestNodeState.Available));
            ApplyCategoryUpdate(category, nodes, category.Connections.ToList());
            SelectNode(id);
            OpenQuestEditModal();
            adminData.SetToolMode(AdminToolMode.Select);
            ComposeDialog();
        }

        private void HandleLinkToolClick(int? nodeId)
        {
            if (nodeId == null)
                return;

            if (adminData.LinkSourceNodeId == null)
            {
                adminData.LinkSourceNodeId = nodeId;
                ComposeDialog();
                return;
            }

            if (adminData.LinkSourceNodeId == nodeId)
                return;

            HandleAddConnection(adminData.LinkSourceNodeId.Value, nodeId.Value);
            adminData.LinkSourceNodeId = null;
            ComposeDialog();
        }

        /// <summary>Cancel in-progress link rubber-band (first node already chosen).</summary>
        private void CancelLinkToolPending()
        {
            if (adminData.LinkSourceNodeId == null)
                return;

            adminData.LinkSourceNodeId = null;
            ComposeDialog();
        }

        private void HandleLinkToolRightClick(int nodeId)
        {
            // Only removes an existing connection when no pending link is active.
            var category = GetSelectedCategory();
            if (category == null)
                return;

            List<QuestbookQuestConnectionDefinition> connections = category.Connections.ToList();
            int removeIndex = connections.FindIndex(
                connection => connection.StartNodeId == nodeId || connection.EndNodeId == nodeId);
            if (removeIndex < 0)
                return;

            connections.RemoveAt(removeIndex);
            ApplyCategoryUpdate(category, category.Nodes.ToList(), connections);
            ComposeDialog();
        }

        private void HandleDeleteToolClick(int? nodeId)
        {
            if (nodeId == null)
                return;

            var category = GetSelectedCategory();
            var node = category?.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null || node.IsStartNode)
                return;

            HandleDeleteNodeById(nodeId.Value);
            ComposeDialog();
        }

        private void SelectNode(int nodeId)
        {
            var category = GetSelectedCategory();
            var node = category?.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
                return;

            adminData.EditorLanguage = QuestbookLocalizedText.NormalizeLang(Lang.CurrentLocale);
            adminData.LoadFromNode(node);
        }

        private void TrySetEditedNodeType(QuestbookQuestNodeType nodeType)
        {
            if (!adminData.HasSelectedNode)
                return;

            if (nodeType == QuestbookQuestNodeType.Start)
            {
                var category = GetSelectedCategory();
                if (category?.Nodes.Any(n => n.IsStartNode && n.Id != adminData.SelectedNodeId) == true)
                    return;
            }

            adminData.EditedNodeType = nodeType;
            adminData.FocusedField = AdminFormFieldRef.None;
            ApplyFormToSelectedNode();
            ComposeDialog();
        }

        private void ApplyFormToSelectedNode()
        {
            // Do NOT commit CooldownMinutesDraft here: SyncAdminFieldEdit calls this on every
            // keystroke. Committing would clear the draft and snap the minutes box back to "1".
            // Draft is finalized on save/close or when leaving the field.
            if (!adminData.HasSelectedNode)
                return;

            var category = GetSelectedCategory();
            if (category == null)
                return;

            var existing = category.Nodes.FirstOrDefault(n => n.Id == adminData.SelectedNodeId);
            if (existing == null)
                return;

            var nodes = category.Nodes.ToList();
            int index = nodes.FindIndex(n => n.Id == adminData.SelectedNodeId);
            if (index < 0)
                return;

            nodes[index] = BuildNodeFromForm(existing);
            ApplyCategoryUpdate(category, nodes, category.Connections.ToList());
        }

        private QuestbookQuestNodeDefinition BuildNodeFromForm(QuestbookQuestNodeDefinition existing)
        {
            if (adminData.IsQuestTypeEdited)
                return CreateQuestNodeFromForm(existing.Id, existing.X, existing.Y, existing.State);

            adminData.FlushInformationTextToLangMap();
            return new QuestbookQuestNodeDefinition(
                existing.Id, existing.X, existing.Y, existing.State,
                adminData.GetInformationTextForSave(),
                adminData.EditedNodeType,
                descriptionByLang: new Dictionary<string, string>(adminData.InformationByLang, StringComparer.OrdinalIgnoreCase),
                repeatMode: QuestbookRepeatMode.Once,
                cooldownSeconds: 0);
        }

        private QuestbookQuestNodeDefinition CreateQuestNodeFromForm(int id, double x, double y, QuestbookQuestNodeState state)
        {
            var requiredItems = new List<QuestbookQuestItemRequirement>();
            bool killNode = adminData.EditedNodeType == QuestbookQuestNodeType.Kill;
            foreach (QuestbookAdminItemEntry goal in adminData.Goals)
            {
                string code = goal.GetSavedCollectibleCode();
                if (!string.IsNullOrWhiteSpace(code) && goal.Count > 0)
                {
                    // Kill node: kill. Quest: full flags.
                    string objective;
                    if (killNode || goal.IsKillObjective)
                        objective = "kill";
                    else if (goal.IsCraftObjective)
                        objective = goal.ConsumeOnComplete ? "craft_have" : "craft";
                    else
                        objective = goal.ConsumeOnComplete ? "have" : "detect";
                    requiredItems.Add(new QuestbookQuestItemRequirement(code, goal.Count, objective));
                }
            }

            var rewardItems = new List<QuestbookQuestItemRequirement>();
            foreach (QuestbookAdminItemEntry award in adminData.Awards)
            {
                // Exact collectible only — never expand awards to game:item-* wildcards.
                string code = (award.CollectibleCode ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(code) && award.Count > 0)
                    rewardItems.Add(new QuestbookQuestItemRequirement(code, award.Count));
            }

            adminData.FlushInformationTextToLangMap();
            QuestbookQuestNodeType nodeType = adminData.EditedNodeType is QuestbookQuestNodeType.Kill
                ? QuestbookQuestNodeType.Kill
                : QuestbookQuestNodeType.Quest;
            return new QuestbookQuestNodeDefinition(
                id, x, y, state, adminData.GetInformationTextForSave(),
                nodeType,
                requiredItems.ToArray(), rewardItems.ToArray(),
                descriptionByLang: new Dictionary<string, string>(adminData.InformationByLang, StringComparer.OrdinalIgnoreCase),
                consumeRequiredItems: requiredItems.Any(static i => i.Consume),
                repeatMode: adminData.EditedRepeatMode,
                cooldownSeconds: adminData.EditedCooldownSeconds);
        }

        private void HandleAddConnection(int startNodeId, int endNodeId)
        {
            if (startNodeId == endNodeId)
                return;

            var category = GetSelectedCategory();
            if (category == null)
                return;

            if (category.Connections.Any(c => c.StartNodeId == startNodeId && c.EndNodeId == endNodeId))
                return;

            var nodes = category.Nodes.ToList();
            var connections = category.Connections.ToList();
            connections.Add(new QuestbookQuestConnectionDefinition(startNodeId, endNodeId));
            ApplyCategoryUpdate(category, nodes, connections);
        }

        private void HandleDeleteNodeById(int nodeId)
        {
            var category = GetSelectedCategory();
            if (category == null)
                return;

            // Keep id watermark so a new node never reuses this id (progress key).
            NoteCategoryNodeIdWatermark(category.HeaderTitle, nodeId);

            var nodes = category.Nodes.Where(n => n.Id != nodeId).ToList();
            var connections = category.Connections
                .Where(c => c.StartNodeId != nodeId && c.EndNodeId != nodeId)
                .ToList();
            ApplyCategoryUpdate(category, nodes, connections);

            // Drop local "completed" / craft / kill for this id immediately.
            dataManager.ClearLocalProgressForNode(category.HeaderTitle, nodeId);

            if (adminData.SelectedNodeId == nodeId)
            {
                CloseQuestEditModal();
                adminData.ClearSelection();
            }
            if (adminData.LinkSourceNodeId == nodeId)
                adminData.LinkSourceNodeId = null;
        }

        private void ApplyCategoryUpdate(
            QuestbookCategoryDefinition category,
            List<QuestbookQuestNodeDefinition> nodes,
            List<QuestbookQuestConnectionDefinition> connections)
        {
            var updatedCategory = new QuestbookCategoryDefinition(
                category.IconItemCode,
                category.Title,
                category.HeaderTitle,
                category.ProgressPercent,
                nodes.ToArray(),
                connections.ToArray(),
                category.HeaderDisplay,
                category.TitleByLang,
                category.HeaderByLang,
                isContentLoaded: true,
                totalNodeCount: nodes.Count,
                hasFullI18n: category.HasFullI18n);

            categories[selectedCategoryIndex] = updatedCategory;
            dataManager.UpdateCategory(selectedCategoryIndex, updatedCategory);
        }

        private int GenerateNodeId(QuestbookCategoryDefinition category)
        {
            string key = category.HeaderTitle ?? string.Empty;
            int maxExisting = category.Nodes.Length == 0 ? -1 : category.Nodes.Max(n => n.Id);
            if (adminCategoryNodeIdWatermark.TryGetValue(key, out int watermark))
                maxExisting = System.Math.Max(maxExisting, watermark);

            int next = maxExisting + 1;
            adminCategoryNodeIdWatermark[key] = next;
            return next;
        }

        private void NoteCategoryNodeIdWatermark(string? categoryHeaderTitle, int nodeId)
        {
            if (string.IsNullOrWhiteSpace(categoryHeaderTitle))
                return;

            if (adminCategoryNodeIdWatermark.TryGetValue(categoryHeaderTitle, out int watermark))
                adminCategoryNodeIdWatermark[categoryHeaderTitle] = System.Math.Max(watermark, nodeId);
            else
                adminCategoryNodeIdWatermark[categoryHeaderTitle] = nodeId;
        }

        private void HandleAdminClear()
        {
            var category = GetSelectedCategory();
            if (category == null) return;

            // Preserve id watermark across clear so recreated nodes get fresh ids.
            if (category.Nodes.Length > 0)
                NoteCategoryNodeIdWatermark(category.HeaderTitle, category.Nodes.Max(n => n.Id));

            int[] clearedIds = category.Nodes.Select(n => n.Id).ToArray();
            ApplyCategoryUpdate(category, [], []);
            foreach (int id in clearedIds)
                dataManager.ClearLocalProgressForNode(category.HeaderTitle, id);

            CloseQuestEditModal();
            adminData.ClearFields();
            adminData.SetToolMode(AdminToolMode.Select);
            ComposeDialog();
        }

        private void CommitAdminEditor()
        {
            preAdminSnapshot = null;
            adminData.IsAdminPanelOpen = false;
            adminData.ClearFields();
            isDraggingQuestNode = false;
            draggedQuestNodeId = -1;
            ComposeDialog();
        }

        private void BeginQuestNodeDrag(double mouseX, double mouseY, int nodeId)
        {
            var category = GetSelectedCategory();
            var node = category?.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
                return;

            SelectNode(nodeId);
            questNodePressMouseX = mouseX;
            questNodePressMouseY = mouseY;
            questNodePressMoved = false;
            isDraggingQuestNode = true;
            draggedQuestNodeId = nodeId;
            UpdateQuestNodeDrag(mouseX, mouseY);
        }

        private void UpdateQuestNodeDrag(double mouseX, double mouseY)
        {
            if (!isDraggingQuestNode || draggedQuestNodeId < 0)
                return;

            double dx = mouseX - questNodePressMouseX;
            double dy = mouseY - questNodePressMouseY;
            if (!questNodePressMoved
                && ((dx * dx) + (dy * dy)) > QuestNodeClickMoveThreshold * QuestNodeClickMoveThreshold)
            {
                questNodePressMoved = true;
            }

            var category = GetSelectedCategory();
            var node = category?.Nodes.FirstOrDefault(n => n.Id == draggedQuestNodeId);
            if (node == null || category == null)
                return;

            if (!TryScreenToGraphCoords(mouseX, mouseY, node.NodeType, out double graphX, out double graphY))
                return;

            SnapGraphCoordsToGrid(ref graphX, ref graphY);

            var nodes = category.Nodes.Select(n =>
                n.Id == draggedQuestNodeId ? CloneNodeAtPosition(n, graphX, graphY) : n).ToList();
            ApplyCategoryUpdate(category, nodes, category.Connections.ToList());
            SelectNode(draggedQuestNodeId);
        }

        private static QuestbookQuestNodeDefinition CloneNodeAtPosition(
            QuestbookQuestNodeDefinition node, double x, double y)
        {
            return new QuestbookQuestNodeDefinition(
                node.Id, x, y, node.State, node.Description, node.NodeType,
                node.RequiredItems, node.RewardItems,
                descriptionByLang: node.DescriptionByLang,
                consumeRequiredItems: node.ConsumeRequiredItems);
        }

        private void SnapGraphCoordsToGrid(ref double graphX, ref double graphY)
        {
            if (!adminData.ShowGrid)
                return;

            double step = QuestbookGuiLayout.AdminGraphGridStep;
            graphX = System.Math.Round(graphX / step) * step;
            graphY = System.Math.Round(graphY / step) * step;
        }

        private void DrawAdminGraphGrid(Cairo.Context ctx, LayoutRect viewport, double graphScale)
        {
            double gridStep = QuestbookGuiLayout.AdminGraphGridStep;
            double left = (-graphPanX) / graphScale;
            double top = (-graphPanY) / graphScale;
            double right = left + viewport.Width / graphScale;
            double bottom = top + viewport.Height / graphScale;
            double startX = System.Math.Floor(left / gridStep) * gridStep;
            double startY = System.Math.Floor(top / gridStep) * gridStep;

            ctx.Save();
            ctx.SetSourceRGBA(0.45, 0.5, 0.55, 0.28);
            ctx.LineWidth = 1;

            for (double gx = startX; gx <= right; gx += gridStep)
            {
                double sx = viewport.X + graphPanX + (gx * graphScale);
                ctx.MoveTo(sx, viewport.Y);
                ctx.LineTo(sx, viewport.Y + viewport.Height);
            }

            for (double gy = startY; gy <= bottom; gy += gridStep)
            {
                double sy = viewport.Y + graphPanY + (gy * graphScale);
                ctx.MoveTo(viewport.X, sy);
                ctx.LineTo(viewport.X + viewport.Width, sy);
            }

            ctx.Stroke();
            ctx.Restore();
        }

        private void HandleAdminSave()
        {
            ApplyFormToSelectedNode();

            var category = GetSelectedCategory();
            if (category == null) return;

            var syncCategory = new QuestbookSyncCategoryPacket
            {
                IconItemCode = category.IconItemCode,
                Title = category.Title,
                HeaderTitle = category.HeaderTitle,
                HeaderDisplay = category.HeaderDisplay,
                TitleI18n = ToLangPackets(category.TitleByLang),
                HeaderI18n = ToLangPackets(category.HeaderByLang),
                Nodes = category.Nodes.Select(n => new QuestbookSyncNodePacket
                {
                    Id = n.Id,
                    X = n.X,
                    Y = n.Y,
                    NodeType = n.NodeType switch
                    {
                        QuestbookQuestNodeType.Start => 0,
                        QuestbookQuestNodeType.Checkpoint => 2,
                        QuestbookQuestNodeType.Kill => 3,
                        _ => 1
                    },
                    Description = n.Description,
                    DescriptionI18n = ToLangPackets(n.DescriptionByLang),
                    RequiredItems = n.RequiredItems.Select(i => new QuestbookSyncItemPacket
                    {
                        CollectibleCode = i.CollectibleCode,
                        Count = i.Count,
                        Objective = i.Objective // have | detect | craft
                    }).ToArray(),
                    RewardItems = n.RewardItems.Select(i => new QuestbookSyncItemPacket
                    {
                        CollectibleCode = i.CollectibleCode,
                        Count = i.Count,
                        Objective = "have"
                    }).ToArray(),
                    ConsumeRequiredItems = n.ConsumeRequiredItems,
                    RepeatMode = n.RepeatMode,
                    CooldownSeconds = n.CooldownSeconds
                }).ToArray(),
                Connections = category.Connections.Select(c => new QuestbookSyncConnectionPacket
                {
                    StartNodeId = c.StartNodeId,
                    EndNodeId = c.EndNodeId
                }).ToArray()
            };

            QuestbookClientSystem.SendAdminSaveCategory(new QuestbookAdminSaveCategoryRequest
            {
                CategoryHeaderTitle = category.HeaderTitle,
                Category = syncCategory
            });
        }

        private static QuestbookLangTextPacket[] ToLangPackets(IReadOnlyDictionary<string, string>? map)
        {
            if (map == null || map.Count == 0)
                return [];

            return map
                .Where(static e => !string.IsNullOrWhiteSpace(e.Key) && !string.IsNullOrWhiteSpace(e.Value))
                .OrderBy(static e => e.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static e => new QuestbookLangTextPacket
                {
                    Lang = e.Key.Trim().ToLowerInvariant(),
                    Text = e.Value
                })
                .ToArray();
        }

        public void SyncAdminFieldEdit()
        {
            ApplyFormToSelectedNode();
            // Typing only needs content surface refresh, not full composer rebuild.
            RequestContentRefresh();
        }
    }
}