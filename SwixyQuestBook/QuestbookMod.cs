using SwixyQuestBook.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SwixyQuestBook
{
    /// <summary>
    /// Client-side network send helpers (compiled only into SwixyQuestBook.Client.dll).
    /// </summary>
    public sealed class QuestbookMod : ModSystem
    {
        private static IClientNetworkChannel? clientChannel;

        public static event Action<QuestbookSubmitQuestResponse>? QuestSubmitResponseReceived;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api) { }

        public static void SetClientChannel(IClientNetworkChannel channel)
        {
            clientChannel = channel;
        }

        public override void Dispose()
        {
            clientChannel = null;
            QuestSubmitResponseReceived = null;
            base.Dispose();
        }

        public static bool TrySendQuestSubmit(QuestbookSubmitQuestRequest request)
        {
            if (clientChannel?.Connected != true) return false;
            clientChannel.SendPacket(request);
            return true;
        }

        public static bool TrySendAdminCreateNode(QuestbookAdminCreateNodeRequest request)
        {
            if (clientChannel?.Connected != true) return false;
            clientChannel.SendPacket(request);
            return true;
        }

        public static bool TrySendAdminDeleteLastNode(QuestbookAdminDeleteLastNodeRequest request)
        {
            if (clientChannel?.Connected != true) return false;
            clientChannel.SendPacket(request);
            return true;
        }

        public static bool TrySendAdminSaveCategory(QuestbookAdminSaveCategoryRequest request)
        {
            if (clientChannel?.Connected != true) return false;
            clientChannel.SendPacket(request);
            return true;
        }

        public static bool TrySendAdminAddCategory(QuestbookAdminAddCategoryRequest request)
        {
            if (clientChannel?.Connected != true) return false;
            clientChannel.SendPacket(request);
            return true;
        }

        public static bool TrySendAdminRenameCategory(QuestbookAdminRenameCategoryRequest request)
        {
            if (clientChannel?.Connected != true) return false;
            clientChannel.SendPacket(request);
            return true;
        }

        public static bool TrySendAdminDeleteCategory(QuestbookAdminDeleteCategoryRequest request)
        {
            if (clientChannel?.Connected != true) return false;
            clientChannel.SendPacket(request);
            return true;
        }

        public static bool TrySendRequestCategory(QuestbookRequestCategoryPacket request)
        {
            if (clientChannel?.Connected != true) return false;
            clientChannel.SendPacket(request);
            return true;
        }

        public static void NotifyQuestSubmitResponse(QuestbookSubmitQuestResponse response)
        {
            QuestSubmitResponseReceived?.Invoke(response);
        }
    }
}
