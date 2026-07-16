using ProtoBuf;

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
}
