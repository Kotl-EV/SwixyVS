using ProtoBuf;

namespace SwixyQuestBook.Network
{
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
        /// <summary>Primary/fallback title (usually the editor's active language).</summary>
        public string Title = string.Empty;
        public string HeaderTitle = string.Empty;
        public string IconItemCode = string.Empty;
        /// <summary>Multi-language branch titles (same shape as quest description i18n).</summary>
        public QuestbookLangTextPacket[] TitleI18n = [];
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookAdminRenameCategoryRequest
    {
        public string CategoryHeaderTitle = string.Empty;
        /// <summary>Primary/fallback title (usually the editor's active language).</summary>
        public string Title = string.Empty;
        public string HeaderTitle = string.Empty;
        public string IconItemCode = string.Empty;
        /// <summary>Full multi-language title map; empty = legacy single-lang update via Title.</summary>
        public QuestbookLangTextPacket[] TitleI18n = [];
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
