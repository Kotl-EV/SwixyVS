using ProtoBuf;
using SwixyQuestBook.Server;

namespace SwixyQuestBook.Network
{
    public static class QuestbookNetworkConstants
    {
        public const string ChannelName = "swixyquestbook:main";
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookQuestItemStackPacket
    {
        public string CollectibleCode = string.Empty;
        public int Count;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSubmitQuestRequest
    {
        public string CategoryHeaderTitle = string.Empty;
        public int NodeId;
        public QuestbookQuestItemStackPacket[] RequiredItems = [];
        public QuestbookQuestItemStackPacket[] RewardItems = [];
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSubmitQuestResponse
    {
        public string CategoryHeaderTitle = string.Empty;
        public int NodeId;
        public bool Success;
    }

    // ===== Админ-пакеты =====

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookAdminCreateNodeRequest
    {
        public string CategoryHeaderTitle = string.Empty;
        public string NodeType = string.Empty;
        public string Description = string.Empty;
        public int ParentNodeId;
        public string Direction = "R";
        public bool IsSubQuest;
        public int SubQuestIndex;
        public int TotalSubQuests;
        public QuestbookQuestItemStackPacket[] RequiredItems = [];
        public QuestbookQuestItemStackPacket[] RewardItems = [];
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookAdminDeleteLastNodeRequest
    {
        public string CategoryHeaderTitle = string.Empty;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookAdminSaveCategoryRequest
    {
        public string CategoryHeaderTitle = string.Empty;
        public QuestbookSyncCategoryPacket Category = new();
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookAdminAddCategoryRequest
    {
        public string Title = string.Empty;
        public string HeaderTitle = string.Empty;
        public string IconItemCode = string.Empty;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookAdminRenameCategoryRequest
    {
        public string CategoryHeaderTitle = string.Empty;
        public string Title = string.Empty;
        public string HeaderTitle = string.Empty;
        public string IconItemCode = string.Empty;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookAdminDeleteCategoryRequest
    {
        public string CategoryHeaderTitle = string.Empty;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookAdminResponse
    {
        public bool Success;
        public string Message = string.Empty;
        public string CategoryHeaderTitle = string.Empty;
    }
}
