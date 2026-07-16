using ProtoBuf;

namespace SwixyQuestBook.Network
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncProgressPacket
    {
        public int TotalQuestsCompleted;
        /// <summary>
        /// True = replace client map with <see cref="CompletedQuests"/>.
        /// False = merge entries into existing map.
        /// </summary>
        public bool IsFullSync;
        public QuestbookSyncCompletedQuestPacket[] CompletedQuests = [];
        public QuestbookSyncCraftProgressPacket[] CraftProgress = [];
        public QuestbookSyncCraftProgressPacket[] KillProgress = [];
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncCompletedQuestPacket
    {
        public string CategoryHeaderTitle = string.Empty;
        public int NodeId;
        public long CompletedAt;
        public int CompletionOrder;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncCraftProgressPacket
    {
        public string CategoryHeaderTitle = string.Empty;
        public int NodeId;
        public string CollectibleCode = string.Empty;
        public int Count;
    }
}
