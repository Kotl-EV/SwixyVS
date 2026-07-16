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

        private bool TryHandleAdminSidebarEditClick(double mouseX, double mouseY)
        {
            if (!IsPlayerAdmin() || adminData.IsAdminPanelOpen) return false;
            if (!adminSidebarEditButtonHitArea.Contains(mouseX, mouseY)) return false;

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
            if (isBranchModalTitleFocused)
                ResetTextCaretBlink();
            branchModalTargetHeaderTitle = categories.Length > 0 ? GetSelectedCategory().HeaderTitle : string.Empty;

            branchModalTitleText = mode switch
            {
                BranchModalMode.Rename => GetEditableCategoryTitle(GetSelectedCategory()),
                _ => string.Empty
            };

            branchModalSelectedIconItemCode = mode switch
            {
                BranchModalMode.Rename when categories.Length > 0 => GetSelectedCategory().IconItemCode ?? string.Empty,
                _ => string.Empty
            };
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
            ApplyFormToSelectedNode();
            HandleAdminSave();
            CloseQuestEditModal();
        }

        private void DismissQuestEditModal()
        {
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
            CloseAdminItemPicker();
            adminInputFieldHitAreas = [];
            adminInputFieldRefs = [];
            adminTypeStartHitArea = new LayoutRect(0, 0, 0, 0);
            adminTypeQuestHitArea = new LayoutRect(0, 0, 0, 0);
            adminTypeCheckpointHitArea = new LayoutRect(0, 0, 0, 0);
            adminTypeKillHitArea = new LayoutRect(0, 0, 0, 0);

        }

        private void CloseBranchModal()
        {
            branchModalMode = BranchModalMode.None;
            isBranchModalOpen = false;
            isBranchModalTitleFocused = false;
            branchModalTitleText = string.Empty;
            branchModalTargetHeaderTitle = string.Empty;
            isBranchModalPrimaryHovered = false;
            isBranchModalCancelHovered = false;
            branchModalOverlayHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalPanelHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalTitleInputHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalPrimaryButtonHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalCancelButtonHitArea = new LayoutRect(0, 0, 0, 0);
            branchModalSelectedIconItemCode = string.Empty;
            branchModalItemPickerSlots = [];
        }

        private string GetEditableCategoryTitle(QuestbookCategoryDefinition category)
        {
            // Already language-resolved by the server.
            return category.Title;
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
            string title = branchModalTitleText.Trim();
            if (string.IsNullOrWhiteSpace(title))
                return;

            pendingOpenAdminEditor = true;
            adminData.EditorSection = AdminEditorSection.Quests;
            MarkBranchMetadataDirty();
            QuestbookClientSystem.SendAdminAddCategory(new QuestbookAdminAddCategoryRequest
            {
                Title = title,
                HeaderTitle = title.ToUpperInvariant(),
                IconItemCode = branchModalSelectedIconItemCode
            });
            CloseBranchModal();
        }

        private void SubmitRenameBranchModal()
        {
            string title = branchModalTitleText.Trim();
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(branchModalTargetHeaderTitle))
                return;

            QuestbookCategoryDefinition category = GetSelectedCategory();
            bool titleUnchanged = string.Equals(
                title,
                GetEditableCategoryTitle(category),
                StringComparison.Ordinal);

            pendingOpenAdminEditor = false;
            MarkBranchMetadataDirty();
            QuestbookClientSystem.SendAdminRenameCategory(new QuestbookAdminRenameCategoryRequest
            {
                CategoryHeaderTitle = branchModalTargetHeaderTitle,
                Title = titleUnchanged ? category.Title : title,
                HeaderTitle = titleUnchanged ? category.HeaderTitle : title.ToUpperInvariant(),
                IconItemCode = branchModalSelectedIconItemCode
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

            if (branchModalTitleInputHitArea.Contains(mouseX, mouseY))
            {
                isBranchModalTitleFocused = true;
                ResetTextCaretBlink();
                RequestContentRefresh();
                return true;
            }

            foreach ((ItemSlot slot, LayoutRect hitArea, string collectibleCode) in branchModalItemPickerSlots)
            {
                if (!ToScreenRect(hitArea).Contains(mouseX, mouseY) || slot.Itemstack == null)
                    continue;

                isBranchModalTitleFocused = false;
                branchModalSelectedIconItemCode = collectibleCode;
                ComposeDialog();
                return true;
            }

            if (!branchModalPanelHitArea.Contains(mouseX, mouseY))
            {
                CloseBranchModal();
                ComposeDialog();
                return true;
            }

            // Click elsewhere inside the modal unfocuses the name field.
            if (isBranchModalTitleFocused)
            {
                isBranchModalTitleFocused = false;
                RequestContentRefresh();
            }

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
                CloseBranchModal();
                ComposeDialog();
                args.Handled = true;
                return true;
            }

            if (key == GlKeys.Enter || key == GlKeys.KeypadEnter)
            {
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

            if (isBranchModalTitleFocused && TryApplyBranchModalTextKey(args))
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

            if (!isBranchModalTitleFocused)
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

            if (key == GlKeys.Back)
            {
                if (branchModalTitleText.Length > 0)
                    branchModalTitleText = branchModalTitleText[..^1];
                ResetTextCaretBlink();
                RequestContentRefresh();
                return true;
            }

            if (key == GlKeys.V && args.CtrlPressed)
            {
                string clipboard = capi.Input.ClipboardText?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(clipboard))
                {
                    branchModalTitleText = TruncateBranchTitle(clipboard);
                    ResetTextCaretBlink();
                    RequestContentRefresh();
                    return true;
                }
            }

            if (args.KeyChar != '\0' && args.KeyChar != '\t' && args.KeyChar != '\n' && args.KeyChar != '\r')
            {
                if (branchModalTitleText.Length < 80)
                    branchModalTitleText += args.KeyChar;
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

            double modalWidth = QuestbookGuiLayout.AddBranchModalWidth * fitScale;
            double modalHeight = QuestbookGuiLayout.AddBranchModalHeight * fitScale;
            double modalX = ((QuestbookGuiLayout.BackgroundWidth * fitScale) - modalWidth) / 2;
            double modalY = (((QuestbookGuiLayout.BackgroundOffsetY + QuestbookGuiLayout.BackgroundHeight) * fitScale) - modalHeight) / 2;
            branchModalPanelHitArea = new LayoutRect(modalX, modalY, modalWidth, modalHeight).Offset(screenX, screenY);

            ImageSurface? modalSurface = GetTextureSurface("modal.png");
            if (modalSurface != null)
                DrawImageSurface(ctx, modalSurface, modalX, modalY, modalWidth, modalHeight);
            else
                FillRectangle(ctx, modalX, modalY, modalWidth, modalHeight, QuestbookGuiLayout.ModalBorderColor);

            // Extra inset from the modal frame on all sides (texture border is thick).
            double padX = QuestbookGuiLayout.AddBranchModalPadX * fitScale;
            double padTop = QuestbookGuiLayout.AddBranchModalPadTop * fitScale;
            double padBottom = QuestbookGuiLayout.AddBranchModalPadBottom * fitScale;
            double contentX = modalX + padX;
            double contentWidth = modalWidth - (padX * 2);
            double currentY = modalY + padTop;

            CairoFont titleFont = CreateMontserratFont(18 * fitScale, QuestbookGuiLayout.TopMenuTitleColor);
            string modalTitle = branchModalMode switch
            {
                BranchModalMode.Rename => QuestbookLang.GetLocal("admin.rename_branch.title"),
                BranchModalMode.DeleteConfirm => QuestbookLang.GetLocal("admin.delete_branch.title"),
                _ => QuestbookLang.GetLocal("admin.add_branch.title")
            };
            DrawText(ctx, titleFont, modalTitle, contentX, currentY);
            currentY += (28 * fitScale);

            if (branchModalMode == BranchModalMode.DeleteConfirm)
            {
                branchModalTitleInputHitArea = new LayoutRect(0, 0, 0, 0);
                branchModalItemPickerSlots = [];
                CairoFont messageFont = CreateMontserratFont(13 * fitScale, QuestbookGuiLayout.AdminPanelTextColor);
                string branchName = GetEditableCategoryTitle(GetSelectedCategory());
                string message = QuestbookLang.GetLocal("admin.delete_branch.confirm", branchName);
                DrawText(ctx, messageFont, message, contentX, currentY);
            }
            else
            {
                CairoFont labelFont = CreateMontserratFont(12 * fitScale, QuestbookGuiLayout.AdminTitleColor);
                DrawText(ctx, labelFont, QuestbookLang.GetLocal("admin.add_branch.name_label"), contentX, currentY);
                currentY += (20 * fitScale);

                double inputHeight = QuestbookGuiLayout.AddBranchModalInputHeight * fitScale;
                branchModalTitleInputHitArea = new LayoutRect(contentX, currentY, contentWidth, inputHeight);
                ImageSurface? inputSurface = GetTextureSurface(QuestbookGuiLayout.AdminQvestBoxModalBoxTexture);
                if (inputSurface != null)
                    DrawImageSurface(ctx, inputSurface, contentX, currentY, contentWidth, inputHeight);

                bool titleEmpty = string.IsNullOrWhiteSpace(branchModalTitleText);
                // While focused, do not show placeholder under the caret.
                string displayText = titleEmpty && !isBranchModalTitleFocused
                    ? QuestbookLang.GetLocal("admin.add_branch.name_placeholder")
                    : branchModalTitleText;
                double[] inputColor = titleEmpty && !isBranchModalTitleFocused
                    ? QuestbookGuiLayout.AdminPanelPlaceholderColor
                    : QuestbookGuiLayout.AdminPanelTextColor;
                CairoFont inputFont = CreateMontserratFont(13 * fitScale, inputColor);
                double textX = contentX + (8 * fitScale);
                DrawText(ctx, inputFont, displayText, textX,
                    GetTextBaselineY(inputFont, currentY, inputHeight, 18 * fitScale));

                if (isBranchModalTitleFocused)
                {
                    StrokeRoundedRectangle(
                        ctx,
                        contentX,
                        currentY,
                        contentWidth,
                        inputHeight,
                        4 * fitScale,
                        1.5 * fitScale,
                        QuestbookGuiLayout.AdminSaveButtonColor);
                    DrawTextCaret(ctx, inputFont, branchModalTitleText, textX, currentY, inputHeight, inputColor);
                }

                currentY += inputHeight + (12 * fitScale);

                currentY = DrawBranchModalItemPicker(ctx, fitScale, contentX, contentWidth, currentY, screenX, screenY);
            }

            double buttonHeight = QuestbookGuiLayout.AddBranchModalButtonHeight * fitScale;
            currentY = modalY + modalHeight - padBottom - buttonHeight;
            double buttonGap = QuestbookGuiLayout.AddBranchModalButtonGap * fitScale;
            double buttonWidth = (contentWidth - buttonGap) / 2;
            ImageSurface? barSurface = GetTextureSurface(QuestbookGuiLayout.AdminBarTexture);

            branchModalPrimaryButtonHitArea = new LayoutRect(contentX, currentY, buttonWidth, buttonHeight);
            branchModalCancelButtonHitArea = new LayoutRect(contentX + buttonWidth + buttonGap, currentY, buttonWidth, buttonHeight);

            string primaryLabel = branchModalMode switch
            {
                BranchModalMode.DeleteConfirm => QuestbookLang.GetLocal("admin.delete_branch.confirm_button"),
                BranchModalMode.Rename => QuestbookLang.GetLocal("admin.rename_branch.save"),
                _ => QuestbookLang.GetLocal("admin.add_branch.create")
            };
            double[]? primaryAccent = branchModalMode == BranchModalMode.DeleteConfirm
                ? QuestbookGuiLayout.AdminClearButtonColor
                : QuestbookGuiLayout.AdminSaveButtonColor;

            DrawBranchModalButton(ctx, fitScale, barSurface, branchModalPrimaryButtonHitArea, primaryLabel,
                isBranchModalPrimaryHovered, primaryAccent);
            DrawBranchModalButton(ctx, fitScale, barSurface, branchModalCancelButtonHitArea,
                QuestbookLang.GetLocal("admin.add_branch.cancel"), isBranchModalCancelHovered,
                QuestbookGuiLayout.AdminClearButtonColor);

            if (!branchModalTitleInputHitArea.IsEmpty)
                branchModalTitleInputHitArea = branchModalTitleInputHitArea.Offset(screenX, screenY);
            branchModalPrimaryButtonHitArea = branchModalPrimaryButtonHitArea.Offset(screenX, screenY);
            branchModalCancelButtonHitArea = branchModalCancelButtonHitArea.Offset(screenX, screenY);

        }

        private double DrawBranchModalItemPicker(
            Cairo.Context ctx,
            double fitScale,
            double contentX,
            double contentWidth,
            double startY,
            double screenX,
            double screenY)
        {
            // Label (left) + selected-icon preview (right) on one row.
            double previewSize = QuestbookGuiLayout.AddBranchModalItemSlotSize * fitScale;
            double rowHeight = System.Math.Max(previewSize, 20 * fitScale);
            double previewX = contentX + contentWidth - previewSize;
            double previewY = startY + ((rowHeight - previewSize) / 2);

            CairoFont labelFont = CreateMontserratFont(12 * fitScale, QuestbookGuiLayout.AdminTitleColor);
            string iconLabel = QuestbookLang.GetLocal("admin.branch_icon.label");
            DrawText(
                ctx,
                labelFont,
                iconLabel,
                contentX,
                GetTextBaselineY(labelFont, startY, rowHeight, rowHeight));

            LayoutRect previewRect = new(previewX, previewY, previewSize, previewSize);
            FillRoundedRectangle(
                ctx,
                previewX,
                previewY,
                previewSize,
                previewSize,
                QuestbookGuiLayout.AdminTileCornerRadius * fitScale,
                QuestbookGuiLayout.AdminTileBackgroundColor);
            StrokeRoundedRectangle(
                ctx,
                previewX,
                previewY,
                previewSize,
                previewSize,
                QuestbookGuiLayout.AdminTileCornerRadius * fitScale,
                1.5 * fitScale,
                QuestbookGuiLayout.AdminSaveButtonColor);

            if (!string.IsNullOrWhiteSpace(branchModalSelectedIconItemCode))
            {
                branchModalIconRenderRequests.Add(new QuestItemIconRenderRequest(
                    branchModalSelectedIconItemCode,
                    previewRect,
                    false,
                    0,
                    QuestbookItemIconContext.Modal));
            }

            double slotSize = QuestbookGuiLayout.AddBranchModalItemSlotSize * fitScale;
            double slotGap = QuestbookGuiLayout.AddBranchModalItemSlotGap * fitScale;
            int columns = QuestbookGuiLayout.AddBranchModalItemColumns;
            double gridY = startY + rowHeight + (10 * fitScale);
            double gridHeight = QuestbookGuiLayout.AddBranchModalItemGridHeight * fitScale;
            double gridBottom = gridY + gridHeight;

            List<(ItemSlot Slot, LayoutRect HitArea, string CollectibleCode)> pickerSlots = [];
            IPlayer? player = capi.World.Player;
            int slotIndex = 0;

            foreach (InventoryBase inventory in QuestbookInventoryHelper.GetPlayerQuestInventories(player))
            {
                for (int i = 0; i < inventory.Count; i++)
                {
                    ItemSlot slot = inventory[i];
                    if (slot.Itemstack?.Collectible?.Code == null)
                        continue;

                    int col = slotIndex % columns;
                    int row = slotIndex / columns;
                    double slotX = contentX + (col * (slotSize + slotGap));
                    double slotY = gridY + (row * (slotSize + slotGap));
                    if (slotY + slotSize > gridBottom)
                        break;

                    string collectibleCode = slot.Itemstack.Collectible.Code.ToString() ?? string.Empty;
                    LayoutRect slotRect = new(slotX, slotY, slotSize, slotSize);
                    bool selected = string.Equals(collectibleCode, branchModalSelectedIconItemCode, StringComparison.OrdinalIgnoreCase);
                    double[] background = selected
                        ? QuestbookGuiLayout.AdminTileActiveBackgroundColor
                        : [0.14, 0.16, 0.18, 0.95];
                    double[] border = selected
                        ? QuestbookGuiLayout.AdminSaveButtonColor
                        : QuestbookGuiLayout.AdminTileBorderColor;

                    FillRoundedRectangle(ctx, slotX, slotY, slotSize, slotSize, 4 * fitScale, background);
                    StrokeRoundedRectangle(ctx, slotX, slotY, slotSize, slotSize, 4 * fitScale, (selected ? 2.0 : 1.0) * fitScale, border);

                    pickerSlots.Add((slot, slotRect, collectibleCode));
                    slotIndex++;
                }
            }

            branchModalItemPickerSlots = pickerSlots.ToArray();
            return gridBottom;
        }

        private void DrawBranchModalButton(
            Cairo.Context ctx,
            double fitScale,
            ImageSurface? barSurface,
            LayoutRect area,
            string label,
            bool hovered,
            double[]? accent)
        {
            if (barSurface != null)
                DrawImageSurface(ctx, barSurface, area.X, area.Y, area.Width, area.Height);

            double[] color = hovered
                ? accent ?? QuestbookGuiLayout.AdminSaveButtonColor
                : QuestbookGuiLayout.AdminPanelTextColor;
            CairoFont font = CreateMontserratFont(12 * fitScale, color);
            double textWidth = MeasureTextWidth(font, label);
            DrawText(ctx, font, label, area.X + ((area.Width - textWidth) / 2),
                GetTextBaselineY(font, area.Y, area.Height, 18 * fitScale));
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
                    adminData.EditorSection = AdminEditorSection.Quests;
                }

                ComposeDialog();
                return;
            }

            pendingSelectCategoryHeaderTitle = headerTitle;
            pendingOpenAdminEditor = openEditor;
        }

        private double GetSidebarAdminButtonsOffset(double fitScale)
        {
            if (!IsPlayerAdmin() || adminData.IsAdminPanelOpen)
                return 0;

            return (QuestbookGuiLayout.SidebarEditButtonHeight + QuestbookGuiLayout.SidebarCardGap) * fitScale;
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

        private void DrawAdminSidebarEditButton(Cairo.Context ctx, double fitScale, double screenX, double screenY)
        {
            if (!IsPlayerAdmin()) return;

            double buttonX = QuestbookGuiLayout.SidebarCardOffsetX * fitScale;
            double buttonWidth = QuestbookGuiLayout.SidebarCardWidth * fitScale;
            double buttonHeight = QuestbookGuiLayout.SidebarEditButtonHeight * fitScale;
            LayoutRect localButtonRect = new(
                buttonX,
                QuestbookGuiLayout.SidebarCardOffsetY * fitScale,
                buttonWidth,
                buttonHeight);
            adminSidebarEditButtonHitArea = localButtonRect.Offset(screenX, screenY);

            DrawAdminTileButton(
                ctx,
                fitScale,
                localButtonRect,
                AdminToolbarIcon.Editor,
                adminData.IsAdminPanelOpen,
                isAdminSidebarEditHovered,
                null,
                GetAdminToolbarLabel(AdminToolbarIcon.Editor),
                labelOnRight: true);
        }

        private void DrawAdminPanel(Cairo.Context ctx, double fitScale)
        {
            if (!adminData.IsAdminPanelOpen) return;

            adminSidebarEditButtonHitArea = new LayoutRect(0, 0, 0, 0);

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
                DrawAdminStatusText(ctx, fitScale, panelX, contentTop);

                if (adminData.HasSelectedNode)
                    DrawAdminSelectedNodeHint(ctx, fitScale, panelX, contentTop);
                else
                    DrawAdminEmptySelectionHint(ctx, fitScale, panelX, contentTop);
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
            double panelWidth = QuestbookGuiLayout.SidebarAdminPanelWidth * fitScale;
            double buttonHeight = QuestbookGuiLayout.SidebarAdminModeBarHeight * fitScale;
            double gap = QuestbookGuiLayout.SidebarAdminToolbarButtonGap * fitScale;
            double buttonWidth = (panelWidth - gap) / 2;

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
            double buttonHeight = QuestbookGuiLayout.SidebarAdminToolbarButtonHeight * fitScale;
            double gap = QuestbookGuiLayout.SidebarAdminToolbarButtonGap * fitScale;

            adminBranchAddHitArea = new LayoutRect(panelX, contentTop, panelWidth, buttonHeight);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminBranchAddHitArea,
                AdminToolbarIcon.Add,
                false,
                isAdminBranchAddHovered,
                QuestbookGuiLayout.AdminSaveButtonColor,
                GetAdminToolbarLabel(AdminToolbarIcon.Add));

            double row2Y = contentTop + buttonHeight + gap;
            double halfWidth = (panelWidth - gap) / 2;
            adminBranchRenameHitArea = new LayoutRect(panelX, row2Y, halfWidth, buttonHeight);
            adminBranchDeleteHitArea = new LayoutRect(panelX + halfWidth + gap, row2Y, halfWidth, buttonHeight);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminBranchRenameHitArea,
                AdminToolbarIcon.EditBranch,
                false,
                isAdminBranchRenameHovered,
                null,
                GetAdminToolbarLabel(AdminToolbarIcon.Rename));
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminBranchDeleteHitArea,
                AdminToolbarIcon.Delete,
                false,
                isAdminBranchDeleteHovered,
                QuestbookGuiLayout.AdminClearButtonColor,
                GetAdminToolbarLabel(AdminToolbarIcon.Delete));

            double row3Y = row2Y + buttonHeight + gap;
            double statusY = row3Y;
            double statusHeight = QuestbookGuiLayout.SidebarAdminStatusHeight * fitScale;
            CairoFont statusFont = CreateMontserratFont(11 * fitScale, QuestbookGuiLayout.AdminTitleColor);
            string statusText = categories.Length > 0
                ? QuestbookLang.GetLocal("admin.branch.status", GetEditableCategoryTitle(GetSelectedCategory()))
                : QuestbookLang.GetLocal("admin.branch.hint");
            DrawText(ctx, statusFont, statusText, panelX + (4 * fitScale),
                GetTextBaselineY(statusFont, statusY, statusHeight, 16 * fitScale));

            double listTop = panelY + (QuestbookGuiLayout.SidebarAdminBranchListOffsetY * fitScale);
            double closeHeight = buttonHeight;
            double closeY = panelY + panelHeight - (QuestbookGuiLayout.SidebarAdminBranchCloseOffsetFromBottom * fitScale);
            double listHeight = System.Math.Max(0, closeY - listTop - gap);
            adminBranchCloseHitArea = new LayoutRect(panelX, closeY, panelWidth, closeHeight);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminBranchCloseHitArea,
                AdminToolbarIcon.Close,
                false,
                isAdminBranchCloseHovered,
                null,
                GetAdminToolbarLabel(AdminToolbarIcon.Close));

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
            double scrollbarWidth = maxScroll > 0
                ? QuestbookGuiLayout.QuestEditModalListScrollbarWidth * fitScale
                : 0;
            double listContentWidth = panelWidth - scrollbarWidth;

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

            if (maxScroll > 0)
            {
                double trackX = panelX + panelWidth - scrollbarWidth;
                double thumbHeight = System.Math.Max(18 * fitScale, listHeight * (listHeight / contentHeight));
                double thumbTravel = System.Math.Max(1, listHeight - thumbHeight);
                double thumbY = listTop + ((adminBranchListScrollOffset / maxScroll) * thumbTravel);
                FillRoundedRectangle(ctx, trackX, listTop, scrollbarWidth, listHeight, 3 * fitScale,
                    [0.22, 0.24, 0.27, 0.7]);
                FillRoundedRectangle(ctx, trackX, thumbY, scrollbarWidth, thumbHeight, 3 * fitScale,
                    QuestbookGuiLayout.AdminTileBorderColor);
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
            double panelWidth = QuestbookGuiLayout.SidebarAdminPanelWidth * fitScale;
            double buttonHeight = QuestbookGuiLayout.SidebarAdminToolbarButtonHeight * fitScale;
            double gap = QuestbookGuiLayout.SidebarAdminToolbarButtonGap * fitScale;
            int columns = QuestbookGuiLayout.SidebarAdminToolbarColumns;
            double buttonWidth = (panelWidth - (gap * (columns - 1))) / columns;
            double stepX = buttonWidth + gap;
            double stepY = buttonHeight + gap;

            LayoutRect Cell(int index) => new(
                panelX + ((index % columns) * stepX),
                panelY + ((index / columns) * stepY),
                buttonWidth,
                buttonHeight);

            adminToolSelectHitArea = Cell(0);
            adminToolQuestHitArea = Cell(1);
            adminToolLinkHitArea = Cell(2);
            adminToolDeleteHitArea = Cell(3);
            adminToolSaveHitArea = Cell(4);
            adminToolClearHitArea = Cell(5);
            adminToolGridHitArea = Cell(6);
            adminToolCloseHitArea = Cell(7);

            DrawAdminWideToolbarButton(
                ctx,
                fitScale,
                adminToolSelectHitArea,
                AdminToolbarIcon.Select,
                adminData.ToolMode == AdminToolMode.Select,
                isAdminToolSelectHovered,
                null);
            DrawAdminWideToolbarButton(
                ctx,
                fitScale,
                adminToolQuestHitArea,
                AdminToolbarIcon.NewQuest,
                adminData.ToolMode == AdminToolMode.NewQuest,
                isAdminToolQuestHovered,
                null);
            DrawAdminWideToolbarButton(
                ctx,
                fitScale,
                adminToolLinkHitArea,
                AdminToolbarIcon.Link,
                adminData.ToolMode == AdminToolMode.LinkQuests,
                isAdminToolLinkHovered,
                null);
            DrawAdminWideToolbarButton(
                ctx,
                fitScale,
                adminToolDeleteHitArea,
                AdminToolbarIcon.Delete,
                adminData.ToolMode == AdminToolMode.DeleteNode,
                isAdminToolDeleteHovered,
                QuestbookGuiLayout.AdminClearButtonColor);
            DrawAdminWideToolbarButton(
                ctx,
                fitScale,
                adminToolSaveHitArea,
                AdminToolbarIcon.Save,
                false,
                isAdminToolSaveHovered,
                QuestbookGuiLayout.AdminSaveButtonColor);
            DrawAdminWideToolbarButton(
                ctx,
                fitScale,
                adminToolClearHitArea,
                AdminToolbarIcon.Clear,
                false,
                isAdminToolClearHovered,
                QuestbookGuiLayout.AdminClearButtonColor);
            DrawAdminWideToolbarButton(
                ctx,
                fitScale,
                adminToolGridHitArea,
                AdminToolbarIcon.Grid,
                adminData.ShowGrid,
                isAdminToolGridHovered,
                null);
            DrawAdminWideToolbarButton(
                ctx,
                fitScale,
                adminToolCloseHitArea,
                AdminToolbarIcon.Close,
                false,
                isAdminToolCloseHovered,
                null);
        }

        /// <summary>
        /// Full-width editor tool: icon + label side-by-side (one control ≈ old 4-tile row).
        /// </summary>
        private void DrawAdminWideToolbarButton(
            Cairo.Context ctx,
            double fitScale,
            LayoutRect area,
            AdminToolbarIcon icon,
            bool active,
            bool hovered,
            double[]? accentColor)
        {
            double radius = QuestbookGuiLayout.AdminTileCornerRadius * fitScale;
            double borderWidth = (active ? 2.0 : 1.5) * fitScale;
            double[] accent = accentColor ?? QuestbookGuiLayout.AdminSaveButtonColor;

            double[] background = active
                ? QuestbookGuiLayout.AdminTileActiveBackgroundColor
                : hovered
                    ? QuestbookGuiLayout.AdminTileHoverBackgroundColor
                    : QuestbookGuiLayout.AdminTileBackgroundColor;

            double[] border = active || hovered
                ? accent
                : QuestbookGuiLayout.AdminTileBorderColor;

            FillRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius, background);
            StrokeRoundedRectangle(ctx, area.X, area.Y, area.Width, area.Height, radius, borderWidth, border);

            string label = GetAdminToolbarLabel(icon);
            double[] iconColor = active || hovered
                ? accent
                : QuestbookGuiLayout.AdminPanelTextColor;

            double pad = 10 * fitScale;
            double iconSize = System.Math.Min(area.Height - (pad * 1.4), 40 * fitScale);
            double iconX = area.X + pad;
            double iconY = area.Y + ((area.Height - iconSize) / 2);
            DrawAdminToolbarIcon(ctx, icon, iconX, iconY, iconSize, iconColor);

            if (string.IsNullOrWhiteSpace(label))
                return;

            double fontSize = System.Math.Clamp(area.Height * 0.34, 13 * fitScale, 18 * fitScale);
            CairoFont font = CreateMontserratFont(fontSize, iconColor);
            string text = label.Trim();
            double textX = iconX + iconSize + (10 * fitScale);
            double textMaxW = area.X + area.Width - pad - textX;
            if (textMaxW > 8 * fitScale)
            {
                while (text.Length > 1 && MeasureTextWidth(font, text) > textMaxW)
                    text = text[..^1];
            }

            DrawText(
                ctx,
                font,
                text,
                textX,
                GetTextBaselineY(font, area.Y, area.Height, area.Height * 0.7));
        }

        private void DrawAdminStatusText(Cairo.Context ctx, double fitScale, double panelX, double toolbarTop)
        {
            // Below the full-width tool stack (not overlaid on the last button).
            double statusY = toolbarTop
                + (QuestbookGuiLayout.SidebarAdminToolbarHeight * fitScale)
                + (QuestbookGuiLayout.SidebarAdminToolbarButtonGap * fitScale);
            double statusHeight = QuestbookGuiLayout.SidebarAdminStatusHeight * fitScale;
            CairoFont font = CreateMontserratFont(11 * fitScale, QuestbookGuiLayout.AdminTitleColor);
            DrawText(ctx, font, GetAdminStatusText(), panelX + (4 * fitScale),
                GetTextBaselineY(font, statusY, statusHeight, 16 * fitScale));
        }

        private string GetAdminStatusText()
        {
            return adminData.ToolMode switch
            {
                AdminToolMode.Select when adminData.HasSelectedNode =>
                    QuestbookLang.GetLocal("admin.status.editing", adminData.SelectedNodeId),
                AdminToolMode.Select =>
                    QuestbookLang.GetLocal("admin.status.pick_select"),
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

            // Wider + taller so goal rows, language tabs, info text and Save do not collide.
            // Sit over the graph (right of the left admin sidebar), not dead-center of the book.
            double modalWidth = QuestbookGuiLayout.QuestEditModalWidth * fitScale;
            double bookW = QuestbookGuiLayout.BackgroundWidth * fitScale;
            double bookTop = QuestbookGuiLayout.BackgroundOffsetY * fitScale;
            double bookBottom = (QuestbookGuiLayout.BackgroundOffsetY + QuestbookGuiLayout.BackgroundHeight) * fitScale;
            double bookH = bookBottom - bookTop;
            double margin = 12 * fitScale;
            double modalHeight = System.Math.Min(
                System.Math.Max(QuestbookGuiLayout.QuestEditModalHeight * fitScale, 640 * fitScale),
                bookH - (margin * 2));
            double modalX = ((bookW - modalWidth) / 2) + (QuestbookGuiLayout.QuestEditModalOffsetX * fitScale);
            modalX = System.Math.Clamp(modalX, margin, System.Math.Max(margin, bookW - modalWidth - margin));
            double modalY = bookTop + System.Math.Max(margin, (bookH - modalHeight) / 2);
            LayoutRect localPanelRect = new(modalX, modalY, modalWidth, modalHeight);
            questEditModalPanelHitArea = localPanelRect.Offset(screenX, screenY);

            ImageSurface? modalSurface = GetTextureSurface("modal.png");
            if (modalSurface != null)
                DrawImageSurface(ctx, modalSurface, modalX, modalY, modalWidth, modalHeight);
            else
                FillRectangle(ctx, modalX, modalY, modalWidth, modalHeight, QuestbookGuiLayout.ModalBorderColor);

            // Extra inset from the modal frame on all sides (texture border is thick).
            double padX = 36 * fitScale;
            double padTop = 36 * fitScale;
            double padBottom = 40 * fitScale;
            double contentX = modalX + padX;
            double contentWidth = modalWidth - (padX * 2);
            double currentY = modalY + padTop;
            double sectionGap = QuestbookGuiLayout.QuestEditModalSectionGap * fitScale;

            double closeHeight = QuestbookGuiLayout.QuestEditModalCloseButtonHeight * fitScale;
            double closeY = modalY + modalHeight - padBottom - closeHeight;

            CairoFont titleFont = CreateMontserratFont(18 * fitScale, QuestbookGuiLayout.TopMenuTitleColor);
            DrawText(
                ctx,
                titleFont,
                QuestbookLang.GetLocal("admin.quest_edit.title", adminData.SelectedNodeId),
                contentX,
                GetTextBaselineY(titleFont, currentY, 24 * fitScale, 24 * fitScale));
            currentY += (32 * fitScale);

            DrawQuestEditModalTypeSelector(ctx, fitScale, contentX, currentY, contentWidth);
            currentY += (QuestbookGuiLayout.QuestEditModalTypeBarHeight * fitScale) + sectionGap;

            bool isQuestType = adminData.IsQuestTypeEdited;
            List<LayoutRect> hitAreas = [];
            List<AdminFormFieldRef> fieldRefs = [];
            double listsBottomY;

            // Reserve bottom stack: language tabs + info box + gap + save button.
            // listsMaxBottom is the lowest Y lists may use before that stack.
            double langRowHeight = 32 * fitScale;
            double langRowGap = 5 * fitScale;
            double langBarHeight = (langRowHeight * 2) + langRowGap;
            double langBarGap = 10 * fitScale;
            double minInfoH = 72 * fitScale;
            double maxInfoH = QuestbookGuiLayout.QuestEditModalInfoHeight * fitScale;
            double infoGap = 12 * fitScale;
            // Space from end of lists to top of save button.
            double reservedAboveSave = langBarHeight + langBarGap + minInfoH + infoGap + sectionGap;
            double listsMaxBottom = closeY - reservedAboveSave;

            if (isQuestType)
            {
                double columnGap = QuestbookGuiLayout.QuestEditModalListColumnGap * fitScale;
                double columnWidth = (contentWidth - columnGap) / 2;
                double headerH = 26 * fitScale;
                double legendH = 20 * fitScale;
                double headerY = currentY;
                CairoFont sectionFont = CreateMontserratFont(14 * fitScale, QuestbookGuiLayout.AdminSaveButtonColor);
                double addSize = QuestbookGuiLayout.QuestEditModalAddButtonSize * fitScale;

                // Goals header row
                string goalsHeaderKey = adminData.EditedNodeType == QuestbookQuestNodeType.Kill
                    ? "admin.quest_edit.kill_section"
                    : "admin.quest_edit.goals_section";
                DrawText(
                    ctx,
                    sectionFont,
                    QuestbookLang.GetLocal(goalsHeaderKey),
                    contentX,
                    GetTextBaselineY(sectionFont, headerY, headerH, headerH));
                goalsAddButtonHitArea = new LayoutRect(
                    contentX + columnWidth - addSize,
                    headerY + ((headerH - addSize) / 2),
                    addSize,
                    addSize);
                DrawAdminTileButton(ctx, fitScale, goalsAddButtonHitArea, AdminToolbarIcon.Add, false, isGoalsAddHovered,
                    QuestbookGuiLayout.AdminSaveButtonColor);

                // Legend by node type.
                double legendY = headerY + headerH + (2 * fitScale);
                double legendX = contentX;
                bool killNode = adminData.EditedNodeType == QuestbookQuestNodeType.Kill;
                if (!killNode)
                {
                    DrawAdminFlagLegendChip(
                        ctx, fitScale, legendX, legendY, legendH,
                        AdminFlagIcon.Take,
                        QuestbookLang.GetLocal("admin.quest_edit.flag.take"),
                        out double takeChipW);
                    legendX += takeChipW + (8 * fitScale);
                    DrawAdminFlagLegendChip(
                        ctx, fitScale, legendX, legendY, legendH,
                        AdminFlagIcon.Craft,
                        QuestbookLang.GetLocal("admin.quest_edit.flag.craft"),
                        out double craftChipW);
                    legendX += craftChipW + (8 * fitScale);
                }

                DrawAdminFlagLegendChip(
                    ctx, fitScale, legendX, legendY, legendH,
                    AdminFlagIcon.AllVariants,
                    QuestbookLang.GetLocal("admin.quest_edit.flag.variants"),
                    out _);

                // Awards header row
                double awardsX = contentX + columnWidth + columnGap;
                DrawText(
                    ctx,
                    sectionFont,
                    QuestbookLang.GetLocal("admin.quest_edit.awards_section"),
                    awardsX,
                    GetTextBaselineY(sectionFont, headerY, headerH, headerH));
                awardsAddButtonHitArea = new LayoutRect(
                    awardsX + columnWidth - addSize,
                    headerY + ((headerH - addSize) / 2),
                    addSize,
                    addSize);
                DrawAdminTileButton(ctx, fitScale, awardsAddButtonHitArea, AdminToolbarIcon.Add, false, isAwardsAddHovered,
                    QuestbookGuiLayout.AdminSaveButtonColor);

                // Awards legend: all variants only.
                DrawAdminFlagLegendChip(
                    ctx, fitScale, awardsX, legendY, legendH,
                    AdminFlagIcon.AllVariants,
                    QuestbookLang.GetLocal("admin.quest_edit.flag.variants"),
                    out _);

                double listTop = legendY + legendH + (6 * fitScale);
                double listHeight = System.Math.Clamp(
                    listsMaxBottom - listTop,
                    72 * fitScale,
                    QuestbookGuiLayout.QuestEditModalListHeight * fitScale);

                goalsListViewportHitArea = new LayoutRect(contentX, listTop, columnWidth, listHeight);
                awardsListViewportHitArea = new LayoutRect(awardsX, listTop, columnWidth, listHeight);

                DrawQuestEditScrollableItemList(
                    ctx, fitScale, goalsListViewportHitArea, adminData.Goals, isGoals: true,
                    ref goalsListScrollOffset, ref goalsRemoveHitAreas, hitAreas, fieldRefs);
                DrawQuestEditScrollableItemList(
                    ctx, fitScale, awardsListViewportHitArea, adminData.Awards, isGoals: false,
                    ref awardsListScrollOffset, ref awardsRemoveHitAreas, hitAreas, fieldRefs);

                listsBottomY = listTop + listHeight + sectionGap;
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
                goalsCraftToggleHitAreas = [];
                goalsKillToggleHitAreas = [];
                listsBottomY = currentY;
            }

            // Language tabs — always above the description box with a hard floor so Save never collides.
            double langBarY = System.Math.Min(listsBottomY, closeY - infoGap - minInfoH - langBarGap - langBarHeight);
            langBarY = System.Math.Max(listsBottomY > 0 ? System.Math.Min(listsBottomY, langBarY) : langBarY, modalY + padTop);
            // Prefer placing lang bar right after lists when there is room.
            if (listsBottomY + langBarHeight + langBarGap + minInfoH + infoGap <= closeY)
                langBarY = listsBottomY;

            DrawQuestEditLanguageBar(ctx, fitScale, contentX, langBarY, contentWidth, langRowHeight, langRowGap);

            double infoY = langBarY + langBarHeight + langBarGap;
            double infoWidth = contentWidth;
            double maxInfoByButton = closeY - infoGap - infoY;
            double infoHeight = System.Math.Clamp(maxInfoH, minInfoH, System.Math.Max(minInfoH, maxInfoByButton));
            // If still overlapping save, shrink info and push it up is not needed — clamp height only.

            AdminFormFieldRef infoField = new(AdminFormFieldKind.Information);
            hitAreas.Add(new LayoutRect(contentX, infoY, infoWidth, infoHeight));
            fieldRefs.Add(infoField);
            adminInputFieldHitAreas = hitAreas.ToArray();
            adminInputFieldRefs = fieldRefs.ToArray();

            ImageSurface? infoBox = GetTextureSurface(QuestbookGuiLayout.AdminQvestBoxModalBoxTexture);
            if (infoBox != null)
                DrawImageSurface(ctx, infoBox, contentX, infoY, infoWidth, infoHeight);
            else
                FillRectangle(ctx, contentX, infoY, infoWidth, infoHeight, [0.12, 0.13, 0.15, 0.9]);

            string infoPlaceholder = QuestbookLang.GetLocal("admin.information_text")
                + $" [{adminData.EditorLanguage.ToUpperInvariant()}]";
            string infoValue = adminData.InformationText;
            bool infoFocused = adminData.FocusedField == infoField;
            // Focused empty field: no placeholder under the caret.
            string infoDisplay = string.IsNullOrWhiteSpace(infoValue) && !infoFocused
                ? infoPlaceholder
                : infoValue;
            double[] infoColor = infoFocused || !string.IsNullOrWhiteSpace(infoValue)
                ? QuestbookGuiLayout.AdminPanelTextColor
                : QuestbookGuiLayout.AdminPanelPlaceholderColor;
            CairoFont infoFont = CreateMontserratFont(12 * fitScale, infoColor);
            int infoMaxChars = isQuestType ? 220 : 624;
            double textPadX = 8 * fitScale;
            List<string> infoLines = WrapText(
                infoFont,
                string.IsNullOrEmpty(infoDisplay) ? " " : infoDisplay,
                infoWidth - (textPadX * 2),
                infoMaxChars);
            if (string.IsNullOrEmpty(infoDisplay))
                infoLines = [string.Empty];

            double lineHeight = 18 * fitScale;
            double contentTextY = infoY + (8 * fitScale);
            int maxLines = System.Math.Max(1, (int)((infoHeight - (16 * fitScale)) / lineHeight));
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
                StrokeRoundedRectangle(
                    ctx,
                    contentX,
                    infoY,
                    infoWidth,
                    infoHeight,
                    4 * fitScale,
                    1.5 * fitScale,
                    QuestbookGuiLayout.AdminSaveButtonColor);

                // Caret at end of last visible line.
                int last = System.Math.Max(0, infoLines.Count - 1);
                string lastLine = infoLines.Count > 0 ? infoLines[last] : string.Empty;
                double caretLineY = contentTextY + (last * lineHeight);
                DrawTextCaret(
                    ctx,
                    infoFont,
                    lastLine,
                    contentX + textPadX,
                    caretLineY,
                    lineHeight,
                    infoColor);
            }

            ImageSurface? barSurface = GetTextureSurface(QuestbookGuiLayout.AdminBarTexture);
            questEditModalSaveButtonHitArea = new LayoutRect(contentX, closeY, contentWidth, closeHeight);
            DrawBranchModalButton(
                ctx,
                fitScale,
                barSurface,
                questEditModalSaveButtonHitArea,
                QuestbookLang.GetLocal("admin.quest_edit.save"),
                isQuestEditModalSaveHovered,
                QuestbookGuiLayout.AdminSaveButtonColor);

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
            double fitScale,
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

            // Two rows → fewer buttons per row → larger chips.
            const int rowCount = 2;
            int columns = System.Math.Max(1, (int)System.Math.Ceiling(langs.Length / (double)rowCount));
            double gap = 8 * fitScale;
            double buttonWidth = (width - (gap * System.Math.Max(0, columns - 1))) / columns;

            for (int i = 0; i < langs.Length; i++)
            {
                string lang = langs[i];
                int col = i % columns;
                int row = i / columns;
                double bx = x + (col * (buttonWidth + gap));
                double by = y + (row * (rowHeight + rowGap));
                LayoutRect rect = new(bx, by, buttonWidth, rowHeight);
                questEditLangButtonHitAreas[i] = rect;

                bool active = string.Equals(lang, adminData.EditorLanguage, StringComparison.OrdinalIgnoreCase);
                bool hasText = adminData.InformationByLang.TryGetValue(lang, out string? text)
                    && !string.IsNullOrWhiteSpace(text);

                double[] bg = active
                    ? QuestbookGuiLayout.AdminTileActiveBackgroundColor
                    : QuestbookGuiLayout.AdminTileBackgroundColor;
                double[] border = active
                    ? QuestbookGuiLayout.AdminSaveButtonColor
                    : QuestbookGuiLayout.AdminTileBorderColor;

                FillRoundedRectangle(ctx, rect.X, rect.Y, rect.Width, rect.Height, 6 * fitScale, bg);
                StrokeRoundedRectangle(
                    ctx,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height,
                    6 * fitScale,
                    active ? 2.0 * fitScale : 1.3 * fitScale,
                    border);

                string label = lang.ToUpperInvariant();
                if (hasText && !active)
                    label += " ·";

                double[] color = active
                    ? QuestbookGuiLayout.AdminSaveButtonColor
                    : QuestbookGuiLayout.AdminPanelTextColor;
                CairoFont labelFont = CreateMontserratFont(14 * fitScale, color);
                DrawCenteredText(ctx, labelFont, label, rect);
            }
        }

        private void OffsetQuestEditModalHitAreas(double screenX, double screenY)
        {
            adminTypeStartHitArea = adminTypeStartHitArea.Offset(screenX, screenY);
            adminTypeQuestHitArea = adminTypeQuestHitArea.Offset(screenX, screenY);
            adminTypeCheckpointHitArea = adminTypeCheckpointHitArea.Offset(screenX, screenY);
            adminTypeKillHitArea = adminTypeKillHitArea.Offset(screenX, screenY);
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

            if (QuestbookItemCodeHelper.SupportsVariantWildcard(entry.CollectibleCode))
                entry.MatchAllVariants = true;

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

            // Full creative-style catalog (all items + blocks).
            double padding = 36 * fitScale;
            double contentPadTop = 18 * fitScale;
            double panelHeight = System.Math.Min(modalArea.Height * 0.62, 280 * fitScale);
            double panelWidth = modalArea.Width - (padding * 2);
            double panelX = modalArea.X + padding;
            double panelY = modalArea.Y + modalArea.Height - panelHeight - (44 * fitScale);
            adminItemPickerPanelHitArea = new LayoutRect(panelX, panelY, panelWidth, panelHeight);

            FillRoundedRectangle(ctx, panelX, panelY, panelWidth, panelHeight, 8 * fitScale,
                QuestbookGuiLayout.AdminTileBackgroundColor);
            StrokeRoundedRectangle(ctx, panelX, panelY, panelWidth, panelHeight, 8 * fitScale, 1.5 * fitScale,
                QuestbookGuiLayout.AdminSaveButtonColor);

            CairoFont titleFont = CreateMontserratFont(13 * fitScale, QuestbookGuiLayout.TopMenuTitleColor);
            DrawText(ctx, titleFont, QuestbookLang.GetLocal("admin.quest_edit.picker_title"),
                panelX + (12 * fitScale), panelY + contentPadTop);

            double cancelWidth = 88 * fitScale;
            double cancelHeight = 28 * fitScale;
            adminItemPickerCancelHitArea = new LayoutRect(
                panelX + panelWidth - cancelWidth - (10 * fitScale),
                panelY + contentPadTop - (4 * fitScale),
                cancelWidth,
                cancelHeight);
            CairoFont cancelFont = CreateMontserratFont(11 * fitScale, QuestbookGuiLayout.AdminPanelTextColor);
            DrawCenteredText(ctx, cancelFont, QuestbookLang.GetLocal("admin.quest_edit.picker_cancel"),
                adminItemPickerCancelHitArea);

            // Search field under title (shared state with creature picker — only one open at a time).
            double searchH = 28 * fitScale;
            double searchY = panelY + contentPadTop + (26 * fitScale);
            double searchX = panelX + (12 * fitScale);
            double searchW = panelWidth - cancelWidth - (36 * fitScale);
            adminEntityPickerSearchHitArea = new LayoutRect(searchX, searchY, searchW, searchH);
            FillRoundedRectangle(ctx, searchX, searchY, searchW, searchH, 4 * fitScale,
                [0.10, 0.11, 0.13, 0.95]);
            StrokeRoundedRectangle(
                ctx, searchX, searchY, searchW, searchH, 4 * fitScale,
                adminEntityPickerSearchFocused ? 1.6 * fitScale : 1.1 * fitScale,
                adminEntityPickerSearchFocused
                    ? QuestbookGuiLayout.AdminSaveButtonColor
                    : QuestbookGuiLayout.AdminTileBorderColor);

            CairoFont searchFont = CreateMontserratFont(12 * fitScale,
                string.IsNullOrEmpty(adminEntityPickerSearchText) && !adminEntityPickerSearchFocused
                    ? QuestbookGuiLayout.AdminPanelPlaceholderColor
                    : QuestbookGuiLayout.AdminPanelTextColor);
            string searchDisplay = string.IsNullOrEmpty(adminEntityPickerSearchText) && !adminEntityPickerSearchFocused
                ? QuestbookLang.GetLocal("admin.quest_edit.item_search_placeholder")
                : adminEntityPickerSearchText;
            DrawText(ctx, searchFont, searchDisplay, searchX + (8 * fitScale),
                GetTextBaselineY(searchFont, searchY, searchH, searchH));
            if (adminEntityPickerSearchFocused)
            {
                DrawTextCaret(ctx, searchFont, adminEntityPickerSearchText,
                    searchX + (8 * fitScale), searchY, searchH, QuestbookGuiLayout.AdminPanelTextColor);
            }

            double tileSize = 48 * fitScale;
            double tileGap = 6 * fitScale;
            double listTop = searchY + searchH + (10 * fitScale);
            double listBottom = panelY + panelHeight - (10 * fitScale);
            double listHeight = System.Math.Max(tileSize, listBottom - listTop);
            double listLeft = panelX + (12 * fitScale);
            double listWidth = panelWidth - (28 * fitScale);
            adminEntityPickerViewportLocal = new LayoutRect(listLeft, listTop, listWidth, listHeight);

            int columns = System.Math.Max(1, (int)System.Math.Floor((listWidth + tileGap) / (tileSize + tileGap)));
            IReadOnlyList<(string Code, string Label, DummySlot Slot)> catalog =
                GetItemCatalogEntries(adminEntityPickerSearchText);
            int rows = System.Math.Max(1, (int)System.Math.Ceiling(catalog.Count / (double)System.Math.Max(1, columns)));
            double contentH = (rows * tileSize) + (System.Math.Max(0, rows - 1) * tileGap);
            double maxScroll = System.Math.Max(0, contentH - listHeight);
            adminEntityPickerScrollOffset = System.Math.Clamp(adminEntityPickerScrollOffset, 0, maxScroll);

            // Only lay out visible rows (creative menu does the same).
            int firstVisibleRow = System.Math.Max(0, (int)System.Math.Floor(adminEntityPickerScrollOffset / (tileSize + tileGap)) - 1);
            int visibleRowCount = (int)System.Math.Ceiling(listHeight / (tileSize + tileGap)) + 2;
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
                double cellX = listLeft + (col * (tileSize + tileGap));
                double cellY = listTop + (row * (tileSize + tileGap)) - adminEntityPickerScrollOffset;
                if (cellY + tileSize < listTop || cellY > listTop + listHeight)
                    continue;

                LayoutRect tileRect = new(cellX, cellY, tileSize, tileSize);
                // AssignCode = journal:piece for lore, or item code for normal catalog.
                pickerSlots.Add((catalog[i].Slot, tileRect, catalog[i].Label, catalog[i].Code));

                // Static cell chrome only — hover is drawn in GL each frame (no recompose).
                FillRoundedRectangle(
                    ctx,
                    tileRect.X,
                    tileRect.Y,
                    tileRect.Width,
                    tileRect.Height,
                    4 * fitScale,
                    [0.12, 0.14, 0.16, 0.96]);
                StrokeRoundedRectangle(
                    ctx,
                    tileRect.X,
                    tileRect.Y,
                    tileRect.Width,
                    tileRect.Height,
                    4 * fitScale,
                    1.0 * fitScale,
                    QuestbookGuiLayout.AdminTileBorderColor);
            }

            ctx.Restore();

            if (maxScroll > 0)
            {
                double trackW = 6 * fitScale;
                double trackX = listLeft + listWidth - trackW + (4 * fitScale);
                double thumbH = System.Math.Max(16 * fitScale, listHeight * (listHeight / contentH));
                double thumbY = listTop + ((adminEntityPickerScrollOffset / maxScroll) * (listHeight - thumbH));
                FillRoundedRectangle(ctx, trackX, listTop, trackW, listHeight, 3 * fitScale, [0.22, 0.24, 0.27, 0.7]);
                FillRoundedRectangle(ctx, trackX, thumbY, trackW, thumbH, 3 * fitScale,
                    QuestbookGuiLayout.AdminTileBorderColor);
            }

            if (catalog.Count == 0)
            {
                CairoFont emptyFont = CreateMontserratFont(12 * fitScale, QuestbookGuiLayout.AdminPanelPlaceholderColor);
                DrawCenteredText(
                    ctx,
                    emptyFont,
                    QuestbookLang.GetLocal("admin.quest_edit.item_search_empty"),
                    new LayoutRect(listLeft, listTop, listWidth, listHeight));
            }

            adminItemPickerSlots = pickerSlots.ToArray();
            adminEntityPickerSlots = [];
            // Hover is tracked live in UpdateQuestEditModalHover / OnRenderGUI — don't stomp it here.
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
            double padding = 36 * fitScale;
            double contentPadTop = 18 * fitScale;
            double panelHeight = System.Math.Min(
                modalArea.Height * 0.62,
                280 * fitScale);
            double panelWidth = modalArea.Width - (padding * 2);
            double panelX = modalArea.X + padding;
            double panelY = modalArea.Y + modalArea.Height - panelHeight - (44 * fitScale);
            adminItemPickerPanelHitArea = new LayoutRect(panelX, panelY, panelWidth, panelHeight);

            FillRoundedRectangle(ctx, panelX, panelY, panelWidth, panelHeight, 8 * fitScale,
                QuestbookGuiLayout.AdminTileBackgroundColor);
            StrokeRoundedRectangle(ctx, panelX, panelY, panelWidth, panelHeight, 8 * fitScale, 1.5 * fitScale,
                QuestbookGuiLayout.AdminSaveButtonColor);

            CairoFont titleFont = CreateMontserratFont(13 * fitScale, QuestbookGuiLayout.TopMenuTitleColor);
            DrawText(ctx, titleFont, QuestbookLang.GetLocal("admin.quest_edit.entity_picker_title"),
                panelX + (12 * fitScale), panelY + contentPadTop);

            double cancelWidth = 88 * fitScale;
            double cancelHeight = 28 * fitScale;
            adminItemPickerCancelHitArea = new LayoutRect(
                panelX + panelWidth - cancelWidth - (10 * fitScale),
                panelY + contentPadTop - (4 * fitScale),
                cancelWidth,
                cancelHeight);
            CairoFont cancelFont = CreateMontserratFont(11 * fitScale, QuestbookGuiLayout.AdminPanelTextColor);
            DrawCenteredText(ctx, cancelFont, QuestbookLang.GetLocal("admin.quest_edit.picker_cancel"),
                adminItemPickerCancelHitArea);

            // Search field under title.
            double searchH = 28 * fitScale;
            double searchY = panelY + contentPadTop + (26 * fitScale);
            double searchX = panelX + (12 * fitScale);
            double searchW = panelWidth - cancelWidth - (36 * fitScale);
            adminEntityPickerSearchHitArea = new LayoutRect(searchX, searchY, searchW, searchH);
            FillRoundedRectangle(ctx, searchX, searchY, searchW, searchH, 4 * fitScale,
                [0.10, 0.11, 0.13, 0.95]);
            StrokeRoundedRectangle(
                ctx, searchX, searchY, searchW, searchH, 4 * fitScale,
                adminEntityPickerSearchFocused ? 1.6 * fitScale : 1.1 * fitScale,
                adminEntityPickerSearchFocused
                    ? QuestbookGuiLayout.AdminSaveButtonColor
                    : QuestbookGuiLayout.AdminTileBorderColor);

            CairoFont searchFont = CreateMontserratFont(12 * fitScale,
                string.IsNullOrEmpty(adminEntityPickerSearchText) && !adminEntityPickerSearchFocused
                    ? QuestbookGuiLayout.AdminPanelPlaceholderColor
                    : QuestbookGuiLayout.AdminPanelTextColor);
            string searchDisplay = string.IsNullOrEmpty(adminEntityPickerSearchText) && !adminEntityPickerSearchFocused
                ? QuestbookLang.GetLocal("admin.quest_edit.entity_search_placeholder")
                : adminEntityPickerSearchText;
            DrawText(ctx, searchFont, searchDisplay, searchX + (8 * fitScale),
                GetTextBaselineY(searchFont, searchY, searchH, searchH));
            if (adminEntityPickerSearchFocused)
            {
                DrawTextCaret(ctx, searchFont, adminEntityPickerSearchText,
                    searchX + (8 * fitScale), searchY, searchH, QuestbookGuiLayout.AdminPanelTextColor);
            }

            // Square tiles — name only as hover tooltip (drawn after 3D models).
            double tileSize = 48 * fitScale;
            double tileGap = 6 * fitScale;
            double listTop = searchY + searchH + (10 * fitScale);
            double listBottom = panelY + panelHeight - (10 * fitScale);
            double listHeight = System.Math.Max(tileSize, listBottom - listTop);
            double listLeft = panelX + (12 * fitScale);
            double listWidth = panelWidth - (28 * fitScale);
            adminEntityPickerViewportLocal = new LayoutRect(listLeft, listTop, listWidth, listHeight);

            int columns = System.Math.Max(1, (int)System.Math.Floor((listWidth + tileGap) / (tileSize + tileGap)));
            var creatures = GetKillPickerCreatureEntries(adminEntityPickerSearchText);
            int rows = System.Math.Max(1, (int)System.Math.Ceiling(creatures.Count / (double)System.Math.Max(1, columns)));
            double contentH = (rows * tileSize) + (System.Math.Max(0, rows - 1) * tileGap);
            double maxScroll = System.Math.Max(0, contentH - listHeight);
            adminEntityPickerScrollOffset = System.Math.Clamp(adminEntityPickerScrollOffset, 0, maxScroll);

            ctx.Save();
            ctx.Rectangle(listLeft, listTop, listWidth, listHeight);
            ctx.Clip();

            var slots = new List<(string EntityCode, string Label, DummySlot Slot, LayoutRect HitArea)>(creatures.Count);
            string? hoverLabel = null;
            LayoutRect hoverRect = new(0, 0, 0, 0);
            int mouseX = capi.Input.MouseX;
            int mouseY = capi.Input.MouseY;

            for (int i = 0; i < creatures.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                double cellX = listLeft + (col * (tileSize + tileGap));
                double cellY = listTop + (row * (tileSize + tileGap)) - adminEntityPickerScrollOffset;
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

                // Hover highlight — brighter fill + green glow stroke.
                FillRoundedRectangle(
                    ctx,
                    tileRect.X,
                    tileRect.Y,
                    tileRect.Width,
                    tileRect.Height,
                    4 * fitScale,
                    hovered
                        ? [0.18, 0.36, 0.20, 0.98]
                        : [0.12, 0.14, 0.16, 0.96]);
                StrokeRoundedRectangle(
                    ctx,
                    tileRect.X,
                    tileRect.Y,
                    tileRect.Width,
                    tileRect.Height,
                    4 * fitScale,
                    hovered ? 2.4 * fitScale : 1.0 * fitScale,
                    hovered
                        ? QuestbookGuiLayout.AdminSaveButtonColor
                        : QuestbookGuiLayout.AdminTileBorderColor);

                // Extra outer glow ring on hover so the cell is obvious under the 3D model.
                if (hovered)
                {
                    StrokeRoundedRectangle(
                        ctx,
                        tileRect.X - (1.5 * fitScale),
                        tileRect.Y - (1.5 * fitScale),
                        tileRect.Width + (3 * fitScale),
                        tileRect.Height + (3 * fitScale),
                        5 * fitScale,
                        1.2 * fitScale,
                        [0.35, 0.95, 0.40, 0.55]);
                }
            }

            ctx.Restore();

            if (maxScroll > 0)
            {
                double trackW = 6 * fitScale;
                double trackX = listLeft + listWidth - trackW + (4 * fitScale);
                double thumbH = System.Math.Max(16 * fitScale, listHeight * (listHeight / contentH));
                double thumbY = listTop + ((adminEntityPickerScrollOffset / maxScroll) * (listHeight - thumbH));
                FillRoundedRectangle(ctx, trackX, listTop, trackW, listHeight, 3 * fitScale, [0.22, 0.24, 0.27, 0.7]);
                FillRoundedRectangle(ctx, trackX, thumbY, trackW, thumbH, 3 * fitScale,
                    QuestbookGuiLayout.AdminTileBorderColor);
            }

            if (creatures.Count == 0)
            {
                CairoFont emptyFont = CreateMontserratFont(12 * fitScale, QuestbookGuiLayout.AdminPanelPlaceholderColor);
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

        private void DrawQuestEditModalTypeSelector(Cairo.Context ctx, double fitScale, double panelX, double panelY, double panelWidth)
        {
            double typeHeight = QuestbookGuiLayout.QuestEditModalTypeBarHeight * fitScale;
            double gap = QuestbookGuiLayout.SidebarAdminToolbarButtonGap * fitScale;
            // Four types: Start | Quest | Checkpoint | Kill
            double typeButtonWidth = (panelWidth - (gap * 3)) / 4;

            adminTypeStartHitArea = new LayoutRect(panelX, panelY, typeButtonWidth, typeHeight);
            adminTypeQuestHitArea = new LayoutRect(panelX + typeButtonWidth + gap, panelY, typeButtonWidth, typeHeight);
            adminTypeCheckpointHitArea = new LayoutRect(panelX + (typeButtonWidth + gap) * 2, panelY, typeButtonWidth, typeHeight);
            adminTypeKillHitArea = new LayoutRect(panelX + (typeButtonWidth + gap) * 3, panelY, typeButtonWidth, typeHeight);

            DrawAdminTileButton(
                ctx,
                fitScale,
                adminTypeStartHitArea,
                AdminToolbarIcon.Start,
                adminData.EditedNodeType == QuestbookQuestNodeType.Start,
                isAdminTypeStartHovered,
                null,
                GetAdminToolbarLabel(AdminToolbarIcon.Start));
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminTypeQuestHitArea,
                AdminToolbarIcon.Quest,
                adminData.EditedNodeType == QuestbookQuestNodeType.Quest,
                isAdminTypeQuestHovered,
                null,
                GetAdminToolbarLabel(AdminToolbarIcon.Quest));
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminTypeCheckpointHitArea,
                AdminToolbarIcon.Checkpoint,
                adminData.EditedNodeType == QuestbookQuestNodeType.Checkpoint,
                isAdminTypeCheckpointHovered,
                null,
                GetAdminToolbarLabel(AdminToolbarIcon.Checkpoint));
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminTypeKillHitArea,
                AdminToolbarIcon.Kill,
                adminData.EditedNodeType == QuestbookQuestNodeType.Kill,
                isAdminTypeKillHovered,
                null,
                GetAdminToolbarLabel(AdminToolbarIcon.Kill));
        }

        private void DrawQuestEditScrollableItemList(
            Cairo.Context ctx,
            double fitScale,
            LayoutRect viewport,
            List<QuestbookAdminItemEntry> items,
            bool isGoals,
            ref double scrollOffset,
            ref LayoutRect[] removeHitAreas,
            List<LayoutRect> hitAreas,
            List<AdminFormFieldRef> fieldRefs)
        {
            double rowHeight = QuestbookGuiLayout.QuestEditModalRowHeight * fitScale;
            double rowGap = QuestbookGuiLayout.QuestEditModalRowGap * fitScale;
            double rowStep = rowHeight + rowGap;
            double pickSize = QuestbookGuiLayout.QuestEditModalPickSlotSize * fitScale;
            double flagSize = QuestbookGuiLayout.QuestEditModalMatchToggleSize * fitScale;
            double numWidth = QuestbookGuiLayout.QuestEditModalNumInputWidth * fitScale;
            double removeWidth = QuestbookGuiLayout.QuestEditModalRemoveButtonWidth * fitScale;
            // Kill: no take/craft. Quest: take + craft.
            bool killGoalRows = isGoals && adminData.EditedNodeType == QuestbookQuestNodeType.Kill;
            double takeSize = isGoals && !killGoalRows ? flagSize : 0;
            double craftSize = isGoals && !killGoalRows ? flagSize : 0;
            double innerGap = 3 * fitScale;
            int gapCount = isGoals
                ? (killGoalRows ? 4 : 6)
                : 4;
            // Leave a little horizontal pad so the last control does not sit on the edge.
            double hPad = 4 * fitScale;
            double nameWidth = System.Math.Max(
                36 * fitScale,
                viewport.Width - hPad - pickSize - takeSize - craftSize - flagSize - numWidth - removeWidth - (innerGap * gapCount));
            double contentHeight = items.Count * rowStep;
            double maxScroll = System.Math.Max(0, contentHeight - viewport.Height);
            scrollOffset = System.Math.Clamp(scrollOffset, 0, maxScroll);

            removeHitAreas = new LayoutRect[items.Count];
            LayoutRect[] pickHitAreas = new LayoutRect[items.Count];
            LayoutRect[] matchHitAreas = new LayoutRect[items.Count];
            LayoutRect[] craftHitAreas = isGoals && craftSize > 0 ? new LayoutRect[items.Count] : [];
            LayoutRect[] killHitAreas = [];
            LayoutRect[] takeHitAreas = isGoals && takeSize > 0 ? new LayoutRect[items.Count] : [];
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
                awardsMatchToggleHitAreas = matchHitAreas;
            }

            ImageSurface? box = GetTextureSurface(QuestbookGuiLayout.AdminQvestBoxModalBoxTexture);

            FillRoundedRectangle(
                ctx,
                viewport.X,
                viewport.Y,
                viewport.Width,
                viewport.Height,
                4 * fitScale,
                [0.10, 0.11, 0.13, 0.55]);

            ctx.Save();
            ctx.Rectangle(viewport.X, viewport.Y, viewport.Width, viewport.Height);
            ctx.Clip();

            if (items.Count == 0)
            {
                CairoFont emptyFont = CreateMontserratFont(11 * fitScale, QuestbookGuiLayout.AdminTitleColor);
                DrawText(ctx, emptyFont, QuestbookLang.GetLocal("admin.quest_edit.empty_list"),
                    viewport.X + (8 * fitScale), viewport.Y + (12 * fitScale));
            }

            for (int index = 0; index < items.Count; index++)
            {
                double rowY = viewport.Y + (index * rowStep) - scrollOffset;
                if (rowY + rowHeight < viewport.Y || rowY > viewport.Y + viewport.Height)
                    continue;

                QuestbookAdminItemEntry entry = items[index];
                bool isKillRow = isGoals
                    && (entry.IsKillObjective || adminData.EditedNodeType == QuestbookQuestNodeType.Kill);
                double pickX = viewport.X + hPad;
                double nameX = pickX + pickSize + innerGap;
                double takeX = nameX + nameWidth + innerGap;
                double craftX = takeSize > 0 ? takeX + takeSize + innerGap : takeX;
                double matchX = craftSize > 0
                    ? craftX + craftSize + innerGap
                    : takeSize > 0
                        ? takeX + takeSize + innerGap
                        : takeX;
                double numX = matchX + flagSize + innerGap;
                double removeX = numX + numWidth + innerGap;

                AdminFormFieldRef countField = new(
                    isGoals ? AdminFormFieldKind.GoalCount : AdminFormFieldKind.AwardCount,
                    index);
                AdminFormFieldRef codeField = new(
                    isGoals ? AdminFormFieldKind.GoalId : AdminFormFieldKind.AwardId,
                    index);

                LayoutRect pickRect = new(pickX, rowY, pickSize, rowHeight);
                LayoutRect nameRect = new(nameX, rowY, nameWidth, rowHeight);
                LayoutRect takeRect = takeSize > 0
                    ? new LayoutRect(takeX, rowY + ((rowHeight - takeSize) / 2), takeSize, takeSize)
                    : new LayoutRect(0, 0, 0, 0);
                LayoutRect craftRect = craftSize > 0
                    ? new LayoutRect(craftX, rowY + ((rowHeight - craftSize) / 2), craftSize, craftSize)
                    : new LayoutRect(0, 0, 0, 0);
                LayoutRect matchRect = new(matchX, rowY + ((rowHeight - flagSize) / 2), flagSize, flagSize);
                LayoutRect countRect = new(numX, rowY, numWidth, rowHeight);
                LayoutRect removeRect = new(removeX, rowY, removeWidth, rowHeight);
                pickHitAreas[index] = pickRect;
                matchHitAreas[index] = matchRect;
                if (takeSize > 0)
                    takeHitAreas[index] = takeRect;
                if (craftSize > 0)
                    craftHitAreas[index] = craftRect;
                // Name / entity-code field is editable (needed for kill entity codes).
                hitAreas.Add(nameRect);
                fieldRefs.Add(codeField);
                hitAreas.Add(countRect);
                fieldRefs.Add(countField);
                removeHitAreas[index] = removeRect;

                bool pickerActive = adminItemPickerTarget is { } pickerTarget
                    && pickerTarget.IsGoals == isGoals
                    && pickerTarget.ListIndex == index;
                // Kill goals store entity codes — resolve to ItemCreature for the slot icon.
                string savedCode = entry.GetSavedCollectibleCode();
                string iconCode = QuestbookItemDisplayHelper.ResolveDisplayIconCode(capi, savedCode, isKillRow);
                bool hasCode = !string.IsNullOrWhiteSpace(savedCode);

                if (hasCode)
                {
                    // Selected item: plain slot background only — no Select glyph under the item mesh.
                    FillRoundedRectangle(
                        ctx,
                        pickRect.X,
                        pickRect.Y,
                        pickRect.Width,
                        pickRect.Height,
                        4 * fitScale,
                        QuestbookGuiLayout.AdminTileBackgroundColor);
                    if (pickerActive)
                    {
                        StrokeRoundedRectangle(
                            ctx,
                            pickRect.X,
                            pickRect.Y,
                            pickRect.Width,
                            pickRect.Height,
                            4 * fitScale,
                            1.5 * fitScale,
                            QuestbookGuiLayout.AdminSaveButtonColor);
                    }

                    double iconSide = System.Math.Min(pickSize, rowHeight) * 0.86;
                    LayoutRect iconRect = new(
                        pickX + ((pickSize - iconSide) / 2),
                        rowY + ((rowHeight - iconSide) / 2),
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
                    // Empty slot: show the pick/select affordance.
                    DrawAdminTileButton(
                        ctx,
                        fitScale,
                        pickRect,
                        AdminToolbarIcon.Select,
                        pickerActive,
                        pickerActive,
                        QuestbookGuiLayout.AdminSaveButtonColor);
                }

                if (box != null)
                {
                    DrawImageSurface(ctx, box, nameX, rowY, nameWidth, rowHeight);
                    DrawImageSurface(ctx, box, numX, rowY, numWidth, rowHeight);
                }

                bool hasItem = !string.IsNullOrWhiteSpace(entry.CollectibleCode);
                bool codeFocused = adminData.FocusedField == codeField;
                // Prefer creature/item display name; fall back to stripped code.
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
                    // ResolveDisplayIconCode + CreateDisplayStack map kill entity codes to ItemCreature names.
                    itemLabel = GetQuestItemSlot(iconCode)?.Itemstack?.GetName()
                        ?? GetQuestItemSlot(entry.CollectibleCode)?.Itemstack?.GetName()
                        ?? StripItemCodeForDisplay(entry.CollectibleCode);
                }

                CairoFont nameFont = CreateMontserratFont(12 * fitScale,
                    !hasItem && !codeFocused
                        ? QuestbookGuiLayout.AdminPanelPlaceholderColor
                        : QuestbookGuiLayout.AdminPanelTextColor);
                if (codeFocused)
                {
                    StrokeRoundedRectangle(
                        ctx, nameRect.X, nameRect.Y, nameRect.Width, nameRect.Height,
                        4 * fitScale, 1.5 * fitScale, QuestbookGuiLayout.AdminSaveButtonColor);
                }

                ctx.Save();
                ctx.Rectangle(nameX + (4 * fitScale), rowY, nameWidth - (8 * fitScale), rowHeight);
                ctx.Clip();
                DrawText(ctx, nameFont, itemLabel, nameX + (6 * fitScale),
                    GetTextBaselineY(nameFont, rowY, rowHeight, 16 * fitScale));
                if (codeFocused)
                {
                    DrawTextCaret(ctx, nameFont, itemLabel, nameX + (6 * fitScale), rowY, rowHeight,
                        QuestbookGuiLayout.AdminPanelTextColor);
                }
                ctx.Restore();

                if (takeSize > 0)
                {
                    DrawAdminFlagToggle(
                        ctx,
                        fitScale,
                        takeRect,
                        AdminFlagIcon.Take,
                        entry.ConsumeOnComplete,
                        enabled: true,
                        hovered: false);
                }

                if (craftSize > 0)
                {
                    DrawAdminFlagToggle(
                        ctx,
                        fitScale,
                        craftRect,
                        AdminFlagIcon.Craft,
                        entry.IsCraftObjective,
                        enabled: true,
                        hovered: false);
                }

                // All variants — wildcard types for this item code.
                DrawAdminFlagToggle(
                    ctx,
                    fitScale,
                    matchRect,
                    AdminFlagIcon.AllVariants,
                    entry.MatchAllVariants,
                    enabled: true,
                    hovered: false);

                string countPlaceholder = QuestbookLang.GetLocal("admin.num");
                bool countFocused = adminData.FocusedField == countField;
                string countDisplay = countFocused
                    ? (entry.Count > 0 ? entry.Count.ToString() : string.Empty)
                    : (entry.Count > 0 ? entry.Count.ToString() : countPlaceholder);
                double[] countColor = countFocused || entry.Count > 0
                    ? QuestbookGuiLayout.AdminPanelTextColor
                    : QuestbookGuiLayout.AdminPanelPlaceholderColor;
                CairoFont countFont = CreateMontserratFont(12 * fitScale, countColor);
                if (countFocused)
                {
                    StrokeRoundedRectangle(
                        ctx,
                        countRect.X,
                        countRect.Y,
                        countRect.Width,
                        countRect.Height,
                        4 * fitScale,
                        1.5 * fitScale,
                        QuestbookGuiLayout.AdminSaveButtonColor);
                }

                double countTextWidth = MeasureTextWidth(countFont, countDisplay);
                double countTextX = numX + ((numWidth - countTextWidth) / 2);
                DrawText(ctx, countFont, countDisplay, countTextX,
                    GetTextBaselineY(countFont, rowY, rowHeight, 16 * fitScale));

                if (countFocused)
                {
                    DrawTextCaret(ctx, countFont, countDisplay, countTextX, rowY, rowHeight, countColor);
                }

                DrawAdminTileButton(
                    ctx,
                    fitScale,
                    removeRect,
                    AdminToolbarIcon.Delete,
                    false,
                    false,
                    QuestbookGuiLayout.AdminClearButtonColor);
            }

            ctx.Restore();

            if (maxScroll > 0)
            {
                double trackX = viewport.X + viewport.Width - (QuestbookGuiLayout.QuestEditModalListScrollbarWidth * fitScale);
                double trackWidth = QuestbookGuiLayout.QuestEditModalListScrollbarWidth * fitScale;
                double thumbHeight = System.Math.Max(18 * fitScale, viewport.Height * (viewport.Height / contentHeight));
                double thumbTravel = System.Math.Max(1, viewport.Height - thumbHeight);
                double thumbY = viewport.Y + ((scrollOffset / maxScroll) * thumbTravel);
                FillRoundedRectangle(ctx, trackX, viewport.Y, trackWidth, viewport.Height, 3 * fitScale,
                    [0.22, 0.24, 0.27, 0.7]);
                FillRoundedRectangle(ctx, trackX, thumbY, trackWidth, thumbHeight, 3 * fitScale,
                    QuestbookGuiLayout.AdminTileBorderColor);
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
                if (field.IsCount && adminData.GetFieldValue(field) == "0")
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
            for (int index = 0; index < goalsTakeToggleHitAreas.Length; index++)
            {
                if (!goalsTakeToggleHitAreas[index].Contains(mouseX, mouseY))
                    continue;

                if (index < 0 || index >= adminData.Goals.Count)
                    return true;

                QuestbookAdminItemEntry entry = adminData.Goals[index];
                if (entry.IsKillObjective)
                    return true;
                // Independent of craft: can craft-only, craft+turn-in, have, or detect.
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
                HandleAdminClear();
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
                UpdateHover(ref isAdminSidebarEditHovered, adminSidebarEditButtonHitArea.Contains(mouseX, mouseY));
                return;
            }

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
                    HandleSelectToolClick(nodeId);
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
            if (nodeId == null)
            {
                CloseQuestEditModal();
                adminData.ClearSelection();
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

        private void HandleLinkToolRightClick(int nodeId)
        {
            var category = GetSelectedCategory();
            if (category == null)
                return;

            List<QuestbookQuestConnectionDefinition> connections = category.Connections.ToList();
            int removeIndex = connections.FindIndex(
                connection => connection.StartNodeId == nodeId || connection.EndNodeId == nodeId);
            if (removeIndex < 0)
                return;

            QuestbookQuestConnectionDefinition removed = connections[removeIndex];
            connections.RemoveAt(removeIndex);
            ApplyCategoryUpdate(category, category.Nodes.ToList(), connections);

            if (adminData.LinkSourceNodeId == nodeId
                || adminData.LinkSourceNodeId == removed.StartNodeId
                || adminData.LinkSourceNodeId == removed.EndNodeId)
            {
                adminData.LinkSourceNodeId = null;
            }

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
                descriptionByLang: new Dictionary<string, string>(adminData.InformationByLang, StringComparer.OrdinalIgnoreCase));
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
                string code = award.GetSavedCollectibleCode();
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
                consumeRequiredItems: requiredItems.Any(static i => i.Consume));
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
                    ConsumeRequiredItems = n.ConsumeRequiredItems
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