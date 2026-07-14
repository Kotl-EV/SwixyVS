using Cairo;
using SwixyQuestBook.Client;
using SwixyQuestBook.Helpers;
using SwixyQuestBook.Network;
using SwixyQuestBook.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

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
        }

        private void CloseBranchModal()
        {
            branchModalMode = BranchModalMode.None;
            isBranchModalOpen = false;
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
            return GetDisplayCategoryText(category.Title);
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
                return true;

            foreach ((ItemSlot slot, LayoutRect hitArea, string collectibleCode) in branchModalItemPickerSlots)
            {
                if (!ToScreenRect(hitArea).Contains(mouseX, mouseY) || slot.Itemstack == null)
                    continue;

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

            if (TryApplyBranchModalTextKey(args))
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
                ComposeDialog();
                return true;
            }

            if (key == GlKeys.V && args.CtrlPressed)
            {
                string clipboard = capi.Input.ClipboardText?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(clipboard))
                {
                    branchModalTitleText = TruncateBranchTitle(clipboard);
                    ComposeDialog();
                    return true;
                }
            }

            if (args.KeyChar != '\0' && args.KeyChar != '\t' && args.KeyChar != '\n' && args.KeyChar != '\r')
            {
                if (branchModalTitleText.Length < 80)
                    branchModalTitleText += args.KeyChar;
                ComposeDialog();
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
            FillQuestbookDialogDimming(ctx, fitScale);

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

            double padding = QuestbookGuiLayout.AddBranchModalPadding * fitScale;
            double contentX = modalX + padding;
            double contentWidth = modalWidth - (padding * 2);
            double currentY = modalY + padding;

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

                string displayText = string.IsNullOrWhiteSpace(branchModalTitleText)
                    ? QuestbookLang.GetLocal("admin.add_branch.name_placeholder")
                    : branchModalTitleText;
                double[] inputColor = string.IsNullOrWhiteSpace(branchModalTitleText)
                    ? QuestbookGuiLayout.AdminPanelPlaceholderColor
                    : QuestbookGuiLayout.AdminPanelTextColor;
                CairoFont inputFont = CreateMontserratFont(13 * fitScale, inputColor);
                DrawText(ctx, inputFont, displayText, contentX + (8 * fitScale),
                    GetTextBaselineY(inputFont, currentY, inputHeight, 18 * fitScale));
                currentY += inputHeight + (8 * fitScale);

                string headerPreview = QuestbookLang.GetLocal(
                    "admin.add_branch.header_hint",
                    string.IsNullOrWhiteSpace(branchModalTitleText) ? "..." : branchModalTitleText.ToUpperInvariant());
                CairoFont hintFont = CreateMontserratFont(11 * fitScale, QuestbookGuiLayout.AdminTitleColor);
                DrawText(ctx, hintFont, headerPreview, contentX, currentY);
                currentY += (22 * fitScale);

                currentY = DrawBranchModalItemPicker(ctx, fitScale, contentX, contentWidth, currentY, screenX, screenY);
            }

            currentY = modalY + modalHeight - padding - (QuestbookGuiLayout.AddBranchModalButtonHeight * fitScale);
            double buttonGap = QuestbookGuiLayout.AddBranchModalButtonGap * fitScale;
            double buttonHeight = QuestbookGuiLayout.AddBranchModalButtonHeight * fitScale;
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
            CairoFont labelFont = CreateMontserratFont(12 * fitScale, QuestbookGuiLayout.AdminTitleColor);
            DrawText(ctx, labelFont, QuestbookLang.GetLocal("admin.branch_icon.label"), contentX, startY);

            double previewSize = QuestbookGuiLayout.AddBranchModalItemSlotSize * fitScale;
            double previewX = contentX + contentWidth - previewSize;
            double previewY = startY - (2 * fitScale);
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
            double gridY = startY + (22 * fitScale);
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
                        snapshotCategory.Connections));
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
                isAdminSidebarEditHovered);
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
                isAdminModeBranchesHovered);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminModeQuestsHitArea,
                AdminToolbarIcon.Quests,
                adminData.EditorSection == AdminEditorSection.Quests,
                isAdminModeQuestsHovered);
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
                QuestbookGuiLayout.AdminSaveButtonColor);

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
                isAdminBranchRenameHovered);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminBranchDeleteHitArea,
                AdminToolbarIcon.Delete,
                false,
                isAdminBranchDeleteHovered,
                QuestbookGuiLayout.AdminClearButtonColor);

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
                isAdminBranchCloseHovered);

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
                DrawSidebarCard(ctx, entry, cardRect, fitScale);
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
            double toolButtonWidth = (panelWidth - (gap * 3)) / 4;

            double row1Y = panelY;
            adminToolSelectHitArea = new LayoutRect(panelX, row1Y, toolButtonWidth, buttonHeight);
            adminToolQuestHitArea = new LayoutRect(panelX + toolButtonWidth + gap, row1Y, toolButtonWidth, buttonHeight);
            adminToolLinkHitArea = new LayoutRect(panelX + (toolButtonWidth + gap) * 2, row1Y, toolButtonWidth, buttonHeight);
            adminToolDeleteHitArea = new LayoutRect(panelX + (toolButtonWidth + gap) * 3, row1Y, toolButtonWidth, buttonHeight);

            DrawAdminTileButton(
                ctx,
                fitScale,
                adminToolSelectHitArea,
                AdminToolbarIcon.Select,
                adminData.ToolMode == AdminToolMode.Select,
                isAdminToolSelectHovered);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminToolQuestHitArea,
                AdminToolbarIcon.NewQuest,
                adminData.ToolMode == AdminToolMode.NewQuest,
                isAdminToolQuestHovered);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminToolLinkHitArea,
                AdminToolbarIcon.Link,
                adminData.ToolMode == AdminToolMode.LinkQuests,
                isAdminToolLinkHovered);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminToolDeleteHitArea,
                AdminToolbarIcon.Delete,
                adminData.ToolMode == AdminToolMode.DeleteNode,
                isAdminToolDeleteHovered,
                QuestbookGuiLayout.AdminClearButtonColor);

            double row2Y = row1Y + buttonHeight + gap;
            double actionButtonWidth = (panelWidth - (gap * 3)) / 4;
            adminToolSaveHitArea = new LayoutRect(panelX, row2Y, actionButtonWidth, buttonHeight);
            adminToolClearHitArea = new LayoutRect(panelX + actionButtonWidth + gap, row2Y, actionButtonWidth, buttonHeight);
            adminToolGridHitArea = new LayoutRect(panelX + (actionButtonWidth + gap) * 2, row2Y, actionButtonWidth, buttonHeight);
            adminToolCloseHitArea = new LayoutRect(panelX + (actionButtonWidth + gap) * 3, row2Y, actionButtonWidth, buttonHeight);

            DrawAdminTileButton(
                ctx,
                fitScale,
                adminToolSaveHitArea,
                AdminToolbarIcon.Save,
                false,
                isAdminToolSaveHovered,
                QuestbookGuiLayout.AdminSaveButtonColor);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminToolClearHitArea,
                AdminToolbarIcon.Clear,
                false,
                isAdminToolClearHovered,
                QuestbookGuiLayout.AdminClearButtonColor);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminToolGridHitArea,
                AdminToolbarIcon.Grid,
                adminData.ShowGrid,
                isAdminToolGridHovered);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminToolCloseHitArea,
                AdminToolbarIcon.Close,
                false,
                isAdminToolCloseHovered);
        }

        private void DrawAdminStatusText(Cairo.Context ctx, double fitScale, double panelX, double toolbarTop)
        {
            double statusY = toolbarTop + ((QuestbookGuiLayout.SidebarAdminToolbarHeight - QuestbookGuiLayout.SidebarAdminStatusHeight) * fitScale);
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
            double hintY = toolbarTop + (QuestbookGuiLayout.SidebarAdminToolbarHeight * fitScale)
                + (QuestbookGuiLayout.SidebarAdminSelectedHintY * fitScale);
            CairoFont font = CreateMontserratFont(12 * fitScale, QuestbookGuiLayout.AdminTitleColor);
            string text = QuestbookLang.GetLocal("admin.status.no_selection");
            DrawText(ctx, font, text, panelX + (4 * fitScale), hintY);
        }

        private void DrawAdminSelectedNodeHint(Cairo.Context ctx, double fitScale, double panelX, double toolbarTop)
        {
            double hintY = toolbarTop + (QuestbookGuiLayout.SidebarAdminToolbarHeight * fitScale)
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
            FillQuestbookDialogDimming(ctx, fitScale);

            double modalWidth = QuestbookGuiLayout.QuestEditModalWidth * fitScale;
            double modalHeight = QuestbookGuiLayout.QuestEditModalHeight * fitScale;
            double modalX = ((QuestbookGuiLayout.BackgroundWidth * fitScale) - modalWidth) / 2;
            double modalY = (((QuestbookGuiLayout.BackgroundOffsetY + QuestbookGuiLayout.BackgroundHeight) * fitScale) - modalHeight) / 2;
            LayoutRect localPanelRect = new(modalX, modalY, modalWidth, modalHeight);
            questEditModalPanelHitArea = localPanelRect.Offset(screenX, screenY);

            ImageSurface? modalSurface = GetTextureSurface("modal.png");
            if (modalSurface != null)
                DrawImageSurface(ctx, modalSurface, modalX, modalY, modalWidth, modalHeight);
            else
                FillRectangle(ctx, modalX, modalY, modalWidth, modalHeight, QuestbookGuiLayout.ModalBorderColor);

            double padding = QuestbookGuiLayout.QuestEditModalPadding * fitScale;
            double contentX = modalX + padding;
            double contentWidth = modalWidth - (padding * 2);
            double currentY = modalY + padding;

            CairoFont titleFont = CreateMontserratFont(18 * fitScale, QuestbookGuiLayout.TopMenuTitleColor);
            DrawText(ctx, titleFont, QuestbookLang.GetLocal("admin.quest_edit.title", adminData.SelectedNodeId), contentX, currentY);
            currentY += (30 * fitScale);

            DrawQuestEditModalTypeSelector(ctx, fitScale, contentX, currentY, contentWidth);
            currentY += (QuestbookGuiLayout.QuestEditModalTypeBarHeight * fitScale) + (QuestbookGuiLayout.QuestEditModalSectionGap * fitScale);

            bool isQuestType = adminData.IsQuestTypeEdited;
            List<LayoutRect> hitAreas = [];
            List<AdminFormFieldRef> fieldRefs = [];

            double closeHeight = QuestbookGuiLayout.QuestEditModalCloseButtonHeight * fitScale;
            double closeY = modalY + modalHeight - padding - closeHeight;
            double listsBottomY;

            if (isQuestType)
            {
                double columnGap = QuestbookGuiLayout.QuestEditModalListColumnGap * fitScale;
                double columnWidth = (contentWidth - columnGap) / 2;
                double listHeight = QuestbookGuiLayout.QuestEditModalListHeight * fitScale;
                double headerY = currentY;
                CairoFont sectionFont = CreateMontserratFont(13 * fitScale, QuestbookGuiLayout.AdminSaveButtonColor);
                double addSize = QuestbookGuiLayout.QuestEditModalAddButtonSize * fitScale;

                DrawText(ctx, sectionFont, QuestbookLang.GetLocal("admin.quest_edit.goals_section"), contentX, headerY);
                goalsAddButtonHitArea = new LayoutRect(contentX + columnWidth - addSize, headerY - (4 * fitScale), addSize, addSize);
                DrawAdminTileButton(ctx, fitScale, goalsAddButtonHitArea, AdminToolbarIcon.Add, false, isGoalsAddHovered,
                    QuestbookGuiLayout.AdminSaveButtonColor);

                double awardsX = contentX + columnWidth + columnGap;
                DrawText(ctx, sectionFont, QuestbookLang.GetLocal("admin.quest_edit.awards_section"), awardsX, headerY);
                awardsAddButtonHitArea = new LayoutRect(awardsX + columnWidth - addSize, headerY - (4 * fitScale), addSize, addSize);
                DrawAdminTileButton(ctx, fitScale, awardsAddButtonHitArea, AdminToolbarIcon.Add, false, isAwardsAddHovered,
                    QuestbookGuiLayout.AdminSaveButtonColor);

                double listTop = headerY + (24 * fitScale);
                goalsListViewportHitArea = new LayoutRect(contentX, listTop, columnWidth, listHeight);
                awardsListViewportHitArea = new LayoutRect(awardsX, listTop, columnWidth, listHeight);

                DrawQuestEditScrollableItemList(
                    ctx, fitScale, goalsListViewportHitArea, adminData.Goals, isGoals: true,
                    ref goalsListScrollOffset, ref goalsRemoveHitAreas, hitAreas, fieldRefs);
                DrawQuestEditScrollableItemList(
                    ctx, fitScale, awardsListViewportHitArea, adminData.Awards, isGoals: false,
                    ref awardsListScrollOffset, ref awardsRemoveHitAreas, hitAreas, fieldRefs);

                listsBottomY = listTop + listHeight + (QuestbookGuiLayout.QuestEditModalSectionGap * fitScale);
            }
            else
            {
                goalsListViewportHitArea = new LayoutRect(0, 0, 0, 0);
                awardsListViewportHitArea = new LayoutRect(0, 0, 0, 0);
                goalsAddButtonHitArea = new LayoutRect(0, 0, 0, 0);
                awardsAddButtonHitArea = new LayoutRect(0, 0, 0, 0);
                goalsRemoveHitAreas = [];
                awardsRemoveHitAreas = [];
                listsBottomY = modalY + padding
                    + ((QuestbookGuiLayout.QuestEditModalTypeBarHeight + QuestbookGuiLayout.QuestEditModalSectionGap) * fitScale);
            }

            double infoHeight = QuestbookGuiLayout.QuestEditModalInfoHeight * fitScale;
            double infoY = listsBottomY;
            double infoWidth = contentWidth;
            if (infoY + infoHeight > closeY - (8 * fitScale))
                infoHeight = System.Math.Max(48 * fitScale, closeY - infoY - (8 * fitScale));

            AdminFormFieldRef infoField = new(AdminFormFieldKind.Information);
            hitAreas.Add(new LayoutRect(contentX, infoY, infoWidth, infoHeight));
            fieldRefs.Add(infoField);
            adminInputFieldHitAreas = hitAreas.ToArray();
            adminInputFieldRefs = fieldRefs.ToArray();

            ImageSurface? infoBox = GetTextureSurface(QuestbookGuiLayout.AdminQvestBoxModalBoxTexture);
            if (infoBox != null)
                DrawImageSurface(ctx, infoBox, contentX, infoY, infoWidth, infoHeight);

            string infoPlaceholder = QuestbookLang.GetLocal("admin.information_text");
            string infoValue = adminData.InformationText;
            string infoDisplay = string.IsNullOrWhiteSpace(infoValue) ? infoPlaceholder : infoValue;
            bool infoFocused = adminData.FocusedField == infoField;
            double[] infoColor = infoFocused || !string.IsNullOrWhiteSpace(infoValue)
                ? QuestbookGuiLayout.AdminPanelTextColor
                : QuestbookGuiLayout.AdminPanelPlaceholderColor;
            CairoFont infoFont = CreateMontserratFont(12 * fitScale, infoColor);
            int infoMaxChars = isQuestType ? 165 : 624;
            List<string> infoLines = WrapText(infoFont, infoDisplay, infoWidth - (12 * fitScale), infoMaxChars);
            double lineHeight = 18 * fitScale;
            double contentTextY = infoY + (6 * fitScale);
            int maxLines = System.Math.Max(1, (int)((infoHeight - (12 * fitScale)) / lineHeight));
            if (infoLines.Count > maxLines)
            {
                infoLines = infoLines.Take(maxLines).ToList();
                if (infoLines[^1].Length > 3)
                    infoLines[^1] = infoLines[^1][..^3] + "...";
            }

            for (int li = 0; li < infoLines.Count; li++)
            {
                double lineY = contentTextY + (li * lineHeight);
                DrawText(ctx, infoFont, infoLines[li], contentX + (6 * fitScale),
                    GetTextBaselineY(infoFont, lineY, lineHeight, lineHeight));
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

        private void OffsetQuestEditModalHitAreas(double screenX, double screenY)
        {
            adminTypeStartHitArea = adminTypeStartHitArea.Offset(screenX, screenY);
            adminTypeQuestHitArea = adminTypeQuestHitArea.Offset(screenX, screenY);
            adminTypeCheckpointHitArea = adminTypeCheckpointHitArea.Offset(screenX, screenY);
            goalsAddButtonHitArea = goalsAddButtonHitArea.Offset(screenX, screenY);
            awardsAddButtonHitArea = awardsAddButtonHitArea.Offset(screenX, screenY);
            goalsListViewportHitArea = goalsListViewportHitArea.Offset(screenX, screenY);
            awardsListViewportHitArea = awardsListViewportHitArea.Offset(screenX, screenY);
            questEditModalSaveButtonHitArea = questEditModalSaveButtonHitArea.Offset(screenX, screenY);

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

        private void OpenAdminItemPicker(bool isGoals, int listIndex)
        {
            adminItemPickerTarget = new AdminItemPickerTarget(isGoals, listIndex);
            adminData.FocusedField = AdminFormFieldRef.None;
        }

        private void CloseAdminItemPicker()
        {
            adminItemPickerTarget = null;
            adminItemPickerPanelHitArea = new LayoutRect(0, 0, 0, 0);
            adminItemPickerCancelHitArea = new LayoutRect(0, 0, 0, 0);
            adminItemPickerSlots = [];
        }

        private bool TryAssignAdminPickerItem(ItemSlot slot)
        {
            if (adminItemPickerTarget == null || slot.Itemstack?.Collectible?.Code == null)
                return false;

            AdminItemPickerTarget target = adminItemPickerTarget.Value;
            List<QuestbookAdminItemEntry> list = target.IsGoals ? adminData.Goals : adminData.Awards;
            if (target.ListIndex < 0 || target.ListIndex >= list.Count)
                return false;

            QuestbookAdminItemEntry entry = list[target.ListIndex];
            entry.CollectibleCode = slot.Itemstack.Collectible.Code.ToString() ?? string.Empty;
            entry.MatchAllVariants = false;
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

            FillRectangle(ctx, modalArea.X, modalArea.Y, modalArea.Width, modalArea.Height,
                [0.02, 0.02, 0.03, 0.45]);

            double padding = QuestbookGuiLayout.QuestEditModalPadding * fitScale;
            double panelHeight = QuestbookGuiLayout.QuestEditModalPickerPanelHeight * fitScale;
            double panelWidth = modalArea.Width - (padding * 2);
            double panelX = modalArea.X + padding;
            double panelY = modalArea.Y + modalArea.Height - panelHeight - padding;
            adminItemPickerPanelHitArea = new LayoutRect(panelX, panelY, panelWidth, panelHeight);

            FillRoundedRectangle(ctx, panelX, panelY, panelWidth, panelHeight, 8 * fitScale,
                QuestbookGuiLayout.AdminTileBackgroundColor);
            StrokeRoundedRectangle(ctx, panelX, panelY, panelWidth, panelHeight, 8 * fitScale, 1.5 * fitScale,
                QuestbookGuiLayout.AdminSaveButtonColor);

            CairoFont titleFont = CreateMontserratFont(13 * fitScale, QuestbookGuiLayout.TopMenuTitleColor);
            DrawText(ctx, titleFont, QuestbookLang.GetLocal("admin.quest_edit.picker_title"),
                panelX + (10 * fitScale), panelY + (10 * fitScale));

            double cancelWidth = 88 * fitScale;
            double cancelHeight = 28 * fitScale;
            adminItemPickerCancelHitArea = new LayoutRect(
                panelX + panelWidth - cancelWidth - (8 * fitScale),
                panelY + (6 * fitScale),
                cancelWidth,
                cancelHeight);
            CairoFont cancelFont = CreateMontserratFont(11 * fitScale, QuestbookGuiLayout.AdminPanelTextColor);
            DrawCenteredText(ctx, cancelFont, QuestbookLang.GetLocal("admin.quest_edit.picker_cancel"),
                adminItemPickerCancelHitArea);

            double slotSize = QuestbookGuiLayout.QuestEditModalPickerSlotSize * fitScale;
            double slotGap = QuestbookGuiLayout.QuestEditModalPickerSlotGap * fitScale;
            double slotsTop = panelY + (36 * fitScale);
            double slotsLeft = panelX + (10 * fitScale);
            int columns = QuestbookGuiLayout.QuestEditModalPickerColumns;

            List<(ItemSlot Slot, LayoutRect HitArea)> pickerSlots = [];
            IPlayer? player = capi.World.Player;
            int slotIndex = 0;

            foreach (InventoryBase inventory in QuestbookInventoryHelper.GetPlayerQuestInventories(player))
            {
                for (int i = 0; i < inventory.Count; i++)
                {
                    ItemSlot slot = inventory[i];
                    int col = slotIndex % columns;
                    int row = slotIndex / columns;
                    double slotX = slotsLeft + (col * (slotSize + slotGap));
                    double slotY = slotsTop + (row * (slotSize + slotGap));
                    if (slotY + slotSize > panelY + panelHeight - (8 * fitScale))
                        break;

                    LayoutRect slotRect = new(slotX, slotY, slotSize, slotSize);
                    pickerSlots.Add((slot, slotRect));
                    FillRoundedRectangle(ctx, slotX, slotY, slotSize, slotSize, 4 * fitScale,
                        [0.14, 0.16, 0.18, 0.95]);
                    StrokeRoundedRectangle(ctx, slotX, slotY, slotSize, slotSize, 4 * fitScale, 1 * fitScale,
                        QuestbookGuiLayout.AdminTileBorderColor);
                    slotIndex++;
                }
            }

            adminItemPickerSlots = pickerSlots.ToArray();
        }

        private void DrawQuestEditModalTypeSelector(Cairo.Context ctx, double fitScale, double panelX, double panelY, double panelWidth)
        {
            double typeHeight = QuestbookGuiLayout.QuestEditModalTypeBarHeight * fitScale;
            double gap = QuestbookGuiLayout.SidebarAdminToolbarButtonGap * fitScale;
            double typeButtonWidth = (panelWidth - (gap * 2)) / 3;

            adminTypeStartHitArea = new LayoutRect(panelX, panelY, typeButtonWidth, typeHeight);
            adminTypeQuestHitArea = new LayoutRect(panelX + typeButtonWidth + gap, panelY, typeButtonWidth, typeHeight);
            adminTypeCheckpointHitArea = new LayoutRect(panelX + (typeButtonWidth + gap) * 2, panelY, typeButtonWidth, typeHeight);

            DrawAdminTileButton(
                ctx,
                fitScale,
                adminTypeStartHitArea,
                AdminToolbarIcon.Start,
                adminData.EditedNodeType == QuestbookQuestNodeType.Start,
                isAdminTypeStartHovered);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminTypeQuestHitArea,
                AdminToolbarIcon.Quest,
                adminData.EditedNodeType == QuestbookQuestNodeType.Quest,
                isAdminTypeQuestHovered);
            DrawAdminTileButton(
                ctx,
                fitScale,
                adminTypeCheckpointHitArea,
                AdminToolbarIcon.Checkpoint,
                adminData.EditedNodeType == QuestbookQuestNodeType.Checkpoint,
                isAdminTypeCheckpointHovered);
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
            double matchSize = QuestbookGuiLayout.QuestEditModalMatchToggleSize * fitScale;
            double numWidth = QuestbookGuiLayout.QuestEditModalNumInputWidth * fitScale;
            double removeWidth = QuestbookGuiLayout.QuestEditModalRemoveButtonWidth * fitScale;
            double innerGap = 3 * fitScale;
            double nameWidth = viewport.Width - pickSize - matchSize - numWidth - removeWidth - (innerGap * 4);
            double contentHeight = items.Count * rowStep;
            double maxScroll = System.Math.Max(0, contentHeight - viewport.Height);
            scrollOffset = System.Math.Clamp(scrollOffset, 0, maxScroll);

            removeHitAreas = new LayoutRect[items.Count];
            LayoutRect[] pickHitAreas = new LayoutRect[items.Count];
            LayoutRect[] matchHitAreas = new LayoutRect[items.Count];
            if (isGoals)
            {
                goalsItemPickHitAreas = pickHitAreas;
                goalsMatchToggleHitAreas = matchHitAreas;
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
                double pickX = viewport.X;
                double nameX = pickX + pickSize + innerGap;
                double matchX = nameX + nameWidth + innerGap;
                double numX = matchX + matchSize + innerGap;
                double removeX = numX + numWidth + innerGap;

                AdminFormFieldRef countField = new(
                    isGoals ? AdminFormFieldKind.GoalCount : AdminFormFieldKind.AwardCount,
                    index);

                LayoutRect pickRect = new(pickX, rowY, pickSize, rowHeight);
                LayoutRect matchRect = new(matchX, rowY + ((rowHeight - matchSize) / 2), matchSize, matchSize);
                LayoutRect countRect = new(numX, rowY, numWidth, rowHeight);
                LayoutRect removeRect = new(removeX, rowY, removeWidth, rowHeight);
                pickHitAreas[index] = pickRect;
                matchHitAreas[index] = matchRect;
                hitAreas.Add(countRect);
                fieldRefs.Add(countField);
                removeHitAreas[index] = removeRect;

                bool pickerActive = adminItemPickerTarget is { } pickerTarget
                    && pickerTarget.IsGoals == isGoals
                    && pickerTarget.ListIndex == index;
                DrawAdminTileButton(
                    ctx,
                    fitScale,
                    pickRect,
                    AdminToolbarIcon.Select,
                    pickerActive,
                    pickerActive,
                    QuestbookGuiLayout.AdminSaveButtonColor);

                string iconCode = entry.GetSavedCollectibleCode();
                if (!string.IsNullOrWhiteSpace(iconCode))
                {
                    adminEditorIconRenderRequests.Add(new QuestItemIconRenderRequest(
                        iconCode,
                        pickRect,
                        false,
                        0,
                        QuestbookItemIconContext.Modal));
                }

                if (box != null)
                {
                    DrawImageSurface(ctx, box, nameX, rowY, nameWidth, rowHeight);
                    DrawImageSurface(ctx, box, numX, rowY, numWidth, rowHeight);
                }

                bool hasItem = !string.IsNullOrWhiteSpace(entry.CollectibleCode);
                string itemLabel = !hasItem
                    ? QuestbookLang.GetLocal("admin.quest_edit.pick_item")
                    : GetQuestItemSlot(entry.CollectibleCode)?.Itemstack?.GetName()
                        ?? StripItemCodeForDisplay(entry.CollectibleCode);
                if (hasItem && entry.MatchAllVariants)
                {
                    itemLabel += " · " + QuestbookLang.GetLocal("admin.quest_edit.match_all_variants");
                }
                CairoFont nameFont = CreateMontserratFont(11 * fitScale,
                    !hasItem
                        ? QuestbookGuiLayout.AdminPanelPlaceholderColor
                        : QuestbookGuiLayout.AdminPanelTextColor);
                ctx.Save();
                ctx.Rectangle(nameX + (4 * fitScale), rowY, nameWidth - (8 * fitScale), rowHeight);
                ctx.Clip();
                DrawText(ctx, nameFont, itemLabel, nameX + (6 * fitScale),
                    GetTextBaselineY(nameFont, rowY, rowHeight, 16 * fitScale));
                ctx.Restore();

                bool matchEnabled = entry.CanToggleVariantMatch;
                bool isWildcardLoaded = QuestbookItemCodeHelper.IsVariantWildcardCode(entry.CollectibleCode);
                DrawAdminCheckbox(
                    ctx,
                    fitScale,
                    matchRect,
                    entry.MatchAllVariants,
                    hasItem && (matchEnabled || isWildcardLoaded),
                    false);

                string countPlaceholder = QuestbookLang.GetLocal("admin.num");
                bool countFocused = adminData.FocusedField == countField;
                string countDisplay = countFocused
                    ? (entry.Count > 0 ? entry.Count.ToString() : string.Empty)
                    : (entry.Count > 0 ? entry.Count.ToString() : countPlaceholder);
                double[] countColor = countFocused || entry.Count > 0
                    ? QuestbookGuiLayout.AdminPanelTextColor
                    : QuestbookGuiLayout.AdminPanelPlaceholderColor;
                CairoFont countFont = CreateMontserratFont(11 * fitScale, countColor);
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

                DrawText(ctx, countFont, countDisplay, numX + ((numWidth - MeasureTextWidth(countFont, countDisplay)) / 2),
                    GetTextBaselineY(countFont, rowY, rowHeight, 16 * fitScale));

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

            if (goalsListViewportHitArea.Contains(mouseX, mouseY))
            {
                double maxScroll = System.Math.Max(0,
                    (adminData.Goals.Count * GetQuestEditListRowStep(currentFitScale)) - goalsListViewportHitArea.Height);
                goalsListScrollOffset = System.Math.Clamp(goalsListScrollOffset + (direction * scrollStep), 0, maxScroll);
                ComposeDialog();
                return true;
            }

            if (awardsListViewportHitArea.Contains(mouseX, mouseY))
            {
                double maxScroll = System.Math.Max(0,
                    (adminData.Awards.Count * GetQuestEditListRowStep(currentFitScale)) - awardsListViewportHitArea.Height);
                awardsListScrollOffset = System.Math.Clamp(awardsListScrollOffset + (direction * scrollStep), 0, maxScroll);
                ComposeDialog();
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

                foreach ((ItemSlot slot, LayoutRect hitArea) in adminItemPickerSlots)
                {
                    if (!ToScreenRect(hitArea).Contains(mouseX, mouseY))
                        continue;

                    if (TryAssignAdminPickerItem(slot))
                        ComposeDialog();
                    else
                        ComposeDialog();
                    return true;
                }

                if (adminItemPickerPanelHitArea.Contains(mouseX, mouseY))
                    return true;

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
                EnsureQuestEditFocusedRowVisible(field);
                ComposeDialog();
                return true;
            }

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
                bool canToggle = !string.IsNullOrWhiteSpace(entry.CollectibleCode)
                    && (entry.CanToggleVariantMatch
                        || QuestbookItemCodeHelper.IsVariantWildcardCode(entry.CollectibleCode));
                if (!canToggle)
                    return true;

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
            ComposeDialog();
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
            if (adminData.EditedNodeType == QuestbookQuestNodeType.Quest)
                return CreateQuestNodeFromForm(existing.Id, existing.X, existing.Y, existing.State);

            return new QuestbookQuestNodeDefinition(
                existing.Id, existing.X, existing.Y, existing.State,
                adminData.InformationText, adminData.EditedNodeType);
        }

        private QuestbookQuestNodeDefinition CreateQuestNodeFromForm(int id, double x, double y, QuestbookQuestNodeState state)
        {
            var requiredItems = new List<QuestbookQuestItemRequirement>();
            foreach (QuestbookAdminItemEntry goal in adminData.Goals)
            {
                string code = goal.GetSavedCollectibleCode();
                if (!string.IsNullOrWhiteSpace(code) && goal.Count > 0)
                    requiredItems.Add(new QuestbookQuestItemRequirement(code, goal.Count));
            }

            var rewardItems = new List<QuestbookQuestItemRequirement>();
            foreach (QuestbookAdminItemEntry award in adminData.Awards)
            {
                string code = award.GetSavedCollectibleCode();
                if (!string.IsNullOrWhiteSpace(code) && award.Count > 0)
                    rewardItems.Add(new QuestbookQuestItemRequirement(code, award.Count));
            }

            return new QuestbookQuestNodeDefinition(
                id, x, y, state, adminData.InformationText,
                QuestbookQuestNodeType.Quest,
                requiredItems.ToArray(), rewardItems.ToArray());
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

            var nodes = category.Nodes.Where(n => n.Id != nodeId).ToList();
            var connections = category.Connections
                .Where(c => c.StartNodeId != nodeId && c.EndNodeId != nodeId)
                .ToList();
            ApplyCategoryUpdate(category, nodes, connections);

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
                category.IconItemCode, category.Title, category.HeaderTitle,
                category.ProgressPercent, nodes.ToArray(), connections.ToArray());

            categories[selectedCategoryIndex] = updatedCategory;
            dataManager.UpdateCategory(selectedCategoryIndex, updatedCategory);
        }

        private static int GenerateNodeId(QuestbookCategoryDefinition category)
        {
            if (category.Nodes.Length == 0) return 0;
            return category.Nodes.Max(n => n.Id) + 1;
        }

        private void HandleAdminClear()
        {
            var category = GetSelectedCategory();
            if (category == null) return;

            ApplyCategoryUpdate(category, [], []);
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
                node.RequiredItems, node.RewardItems);
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
                Nodes = category.Nodes.Select(n => new QuestbookSyncNodePacket
                {
                    Id = n.Id,
                    X = n.X,
                    Y = n.Y,
                    NodeType = n.NodeType switch
                    {
                        QuestbookQuestNodeType.Start => 0,
                        QuestbookQuestNodeType.Checkpoint => 2,
                        _ => 1
                    },
                    Description = n.Description,
                    RequiredItems = n.RequiredItems.Select(i => new QuestbookSyncItemPacket
                    {
                        CollectibleCode = i.CollectibleCode,
                        Count = i.Count
                    }).ToArray(),
                    RewardItems = n.RewardItems.Select(i => new QuestbookSyncItemPacket
                    {
                        CollectibleCode = i.CollectibleCode,
                        Count = i.Count
                    }).ToArray()
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

        public void SyncAdminFieldEdit()
        {
            ApplyFormToSelectedNode();
            ComposeDialog();
        }
    }
}