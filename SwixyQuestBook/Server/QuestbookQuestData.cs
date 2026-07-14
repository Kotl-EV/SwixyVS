using ProtoBuf;
using System.Text.Json.Serialization;

namespace SwixyQuestBook.Server
{
    // ===== JSON-модели для хранения данных квестов =====

    public sealed class QuestbookQuestItemData
    {
        [JsonPropertyName("collectibleCode")]
        public string CollectibleCode { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public int Count { get; set; }

        public QuestbookQuestItemData() { }

        public QuestbookQuestItemData(string collectibleCode, int count)
        {
            CollectibleCode = collectibleCode;
            Count = count;
        }
    }

    public sealed class QuestbookQuestNodeData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("nodeType")]
        public string NodeType { get; set; } = "Quest";

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("requiredItems")]
        public QuestbookQuestItemData[] RequiredItems { get; set; } = [];

        [JsonPropertyName("rewardItems")]
        public QuestbookQuestItemData[] RewardItems { get; set; } = [];

        public QuestbookQuestNodeData() { }
    }

    public sealed class QuestbookQuestConnectionData
    {
        [JsonPropertyName("startNodeId")]
        public int StartNodeId { get; set; }

        [JsonPropertyName("endNodeId")]
        public int EndNodeId { get; set; }

        public QuestbookQuestConnectionData() { }

        public QuestbookQuestConnectionData(int startNodeId, int endNodeId)
        {
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
        }
    }

    public sealed class QuestbookCategoryData
    {
        [JsonPropertyName("iconItemCode")]
        public string IconItemCode { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("headerTitle")]
        public string HeaderTitle { get; set; } = string.Empty;

        [JsonPropertyName("nodes")]
        public QuestbookQuestNodeData[] Nodes { get; set; } = [];

        [JsonPropertyName("connections")]
        public QuestbookQuestConnectionData[] Connections { get; set; } = [];

        public QuestbookCategoryData() { }
    }

    public sealed class QuestbookQuestDatabase
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("categories")]
        public QuestbookCategoryData[] Categories { get; set; } = [];

        public QuestbookQuestDatabase() { }
    }

    // ===== Сетевые пакеты для синхронизации квестов =====

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncQuestsPacket
    {
        public QuestbookSyncCategoryPacket[] Categories = [];
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncCategoryPacket
    {
        public string IconItemCode = string.Empty;
        public string Title = string.Empty;
        public string HeaderTitle = string.Empty;
        public QuestbookSyncNodePacket[] Nodes = [];
        public QuestbookSyncConnectionPacket[] Connections = [];
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncNodePacket
    {
        public int Id;
        public double X;
        public double Y;
        public int NodeType; // 0=Start, 1=Quest, 2=Checkpoint
        public string Description = string.Empty;
        public QuestbookSyncItemPacket[] RequiredItems = [];
        public QuestbookSyncItemPacket[] RewardItems = [];
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
    }
}
