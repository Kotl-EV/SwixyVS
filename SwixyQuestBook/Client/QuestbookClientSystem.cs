using SwixyQuestBook.Gui;
using SwixyQuestBook.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixyQuestBook.Client
{
    public sealed class QuestbookClientSystem : ModSystem
    {
        public const string ToggleHotkeyCode = "swixyquestbook-toggle";

        private ICoreClientAPI? capi;
        private QuestbookDialog? dialog;
        private QuestbookClientDataManager? dataManager;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            dataManager = new QuestbookClientDataManager(api);
            dialog = new QuestbookDialog(api, dataManager);

            dataManager.QuestDataUpdated += OnQuestDataUpdated;
            dataManager.ProgressUpdated += OnProgressUpdated;

            var channel = api.Network
                .RegisterChannel(QuestbookNetworkConstants.ChannelName)
                .RegisterMessageType<QuestbookSubmitQuestRequest>()
                .RegisterMessageType<QuestbookSubmitQuestResponse>()
                .RegisterMessageType<QuestbookSyncQuestsPacket>()
                .RegisterMessageType<QuestbookSyncProgressPacket>()
                .RegisterMessageType<QuestbookRequestCategoryPacket>()
                .RegisterMessageType<QuestbookSyncCategoryUpdatePacket>()
                .RegisterMessageType<QuestbookSyncCategoryMetaPacket>()
                .RegisterMessageType<QuestbookAdminCreateNodeRequest>()
                .RegisterMessageType<QuestbookAdminDeleteLastNodeRequest>()
                .RegisterMessageType<QuestbookAdminSaveCategoryRequest>()
                .RegisterMessageType<QuestbookAdminAddCategoryRequest>()
                .RegisterMessageType<QuestbookAdminRenameCategoryRequest>()
                .RegisterMessageType<QuestbookAdminDeleteCategoryRequest>()
                .RegisterMessageType<QuestbookAdminResponse>()
                .SetMessageHandler<QuestbookSyncQuestsPacket>(OnQuestsPacket)
                .SetMessageHandler<QuestbookSyncProgressPacket>(OnProgressPacket)
                .SetMessageHandler<QuestbookSyncCategoryUpdatePacket>(OnCategoryUpdatePacket)
                .SetMessageHandler<QuestbookSyncCategoryMetaPacket>(OnCategoryMetaPacket)
                .SetMessageHandler<QuestbookSubmitQuestResponse>(OnQuestSubmitResponse)
                .SetMessageHandler<QuestbookAdminResponse>(OnAdminResponse);

            QuestbookMod.SetClientChannel(channel);

            api.Input.RegisterHotKey(
                ToggleHotkeyCode,
                QuestbookLang.GetLocal("hotkey_name"),
                GlKeys.K,
                HotkeyType.GUIOrOtherControls,
                false,
                false,
                false);
            api.Input.SetHotKeyHandler(ToggleHotkeyCode, OnToggleQuestbook);
        }

        public override void Dispose()
        {
            if (dataManager != null)
            {
                dataManager.QuestDataUpdated -= OnQuestDataUpdated;
                dataManager.ProgressUpdated -= OnProgressUpdated;
                dataManager = null;
            }

            dialog?.TryClose();
            dialog?.Dispose();
            dialog = null;
            capi = null;
            base.Dispose();
        }

        private bool OnToggleQuestbook(KeyCombination keyCombination)
        {
            if (dialog == null) return false;

            bool wasOpen = dialog.IsOpened();
            if (wasOpen && dialog.TryDismissOpenSubDialogOnToggle())
                return true;

            dialog.Toggle();

            if (!wasOpen)
            {
                QuestbookSoundHelper.PlayBookOpening(capi);
                dialog?.EnsureSelectedCategoryContentLoaded();
            }

            return true;
        }

        private void OnQuestsPacket(QuestbookSyncQuestsPacket packet)
        {
            dataManager?.HandleQuestsPacket(packet);
            dialog?.EnsureSelectedCategoryContentLoaded();
        }

        private void OnCategoryUpdatePacket(QuestbookSyncCategoryUpdatePacket packet)
        {
            dataManager?.HandleCategoryUpdatePacket(packet);
        }

        private void OnCategoryMetaPacket(QuestbookSyncCategoryMetaPacket packet)
        {
            dataManager?.HandleCategoryMetaPacket(packet);
        }

        private void OnProgressPacket(QuestbookSyncProgressPacket packet)
        {
            dataManager?.HandleProgressPacket(packet);
        }

        private void OnQuestSubmitResponse(QuestbookSubmitQuestResponse response)
        {
            dataManager?.HandleQuestSubmitResponse(response);
            dialog?.ApplyQuestSubmitResponse(response);
            QuestbookMod.NotifyQuestSubmitResponse(response);
        }

        private void OnQuestDataUpdated() => dialog?.RefreshData();
        private void OnProgressUpdated() => dialog?.RefreshData();

        private void OnAdminResponse(QuestbookAdminResponse response)
        {
            capi?.Logger.Notification($"[Questbook Admin] {(response.Success ? "OK" : "Error")}: {response.Message}");
            dialog?.HandleAdminResponse(response);
        }

        public static bool SendAdminCreateNode(QuestbookAdminCreateNodeRequest request)
        {
            return QuestbookMod.TrySendAdminCreateNode(request);
        }

        public static bool SendAdminDeleteLastNode(QuestbookAdminDeleteLastNodeRequest request)
        {
            return QuestbookMod.TrySendAdminDeleteLastNode(request);
        }

        public static bool SendAdminSaveCategory(QuestbookAdminSaveCategoryRequest request)
        {
            return QuestbookMod.TrySendAdminSaveCategory(request);
        }

        public static bool SendAdminAddCategory(QuestbookAdminAddCategoryRequest request)
        {
            return QuestbookMod.TrySendAdminAddCategory(request);
        }

        public static bool SendAdminRenameCategory(QuestbookAdminRenameCategoryRequest request)
        {
            return QuestbookMod.TrySendAdminRenameCategory(request);
        }

        public static bool SendAdminDeleteCategory(QuestbookAdminDeleteCategoryRequest request)
        {
            return QuestbookMod.TrySendAdminDeleteCategory(request);
        }
    }
}
