using ProtoBuf;
using SwixyQuestBook.Domain.Goals;

namespace SwixyQuestBook.Network
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncQuestsPacket
    {
        public QuestbookSyncCategoryPacket[] Categories = [];
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookRequestCategoryPacket
    {
        public string HeaderTitle = string.Empty;
        /// <summary>When true and the requester is admin, send full multi-lang maps.</summary>
        public bool IncludeI18n;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncCategoryUpdatePacket
    {
        public QuestbookSyncCategoryPacket? Category;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncCategoryMetaPacket
    {
        public string HeaderTitle = string.Empty;
        public string IconItemCode = string.Empty;
        public string Title = string.Empty;
        public string HeaderDisplay = string.Empty;
        public int TotalNodeCount;
        public bool IsNew;
        public bool Removed;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncCategoryPacket
    {
        public string IconItemCode = string.Empty;
        public string Title = string.Empty;
        public string HeaderTitle = string.Empty;
        public string HeaderDisplay = string.Empty;
        public QuestbookLangTextPacket[] TitleI18n = [];
        public QuestbookLangTextPacket[] HeaderI18n = [];
        public QuestbookSyncNodePacket[] Nodes = [];
        public QuestbookSyncConnectionPacket[] Connections = [];
        public bool IsStub;
        public int TotalNodeCount;
        /// <summary>True when TitleI18n/HeaderI18n/DescriptionI18n carry full multi-lang maps.</summary>
        public bool IncludesI18n;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncNodePacket
    {
        public int Id;
        public double X;
        public double Y;
        /// <summary>0=Start, 1=Quest, 2=Checkpoint, 3=Kill</summary>
        public int NodeType;
        public string Description = string.Empty;
        public QuestbookLangTextPacket[] DescriptionI18n = [];
        public QuestbookSyncItemPacket[] RequiredItems = [];
        public QuestbookSyncItemPacket[] RewardItems = [];
        public bool ConsumeRequiredItems = true;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookLangTextPacket
    {
        public string Lang = string.Empty;
        public string Text = string.Empty;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncConnectionPacket
    {
        public int StartNodeId;
        public int EndNodeId;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncItemPacket
    {
        public string CollectibleCode = string.Empty;
        public int Count;
        /// <summary>
        /// <c>have</c> | <c>detect</c> | <c>craft</c> | <c>craft_have</c> | <c>kill</c>.
        /// For kill goals, CollectibleCode is the entity code pattern.
        /// </summary>
        public string Objective = QuestbookGoalObjective.Have;
    }
}
