using System.Linq;
using System.Text.Json.Serialization;
using SwixyQuestBook.Domain.Goals;
using SwixyQuestBook.Domain.Localization;

namespace SwixyQuestBook.Domain.Models
{
    public sealed class QuestbookQuestItemData
    {
        [JsonPropertyName("collectibleCode")]
        public string CollectibleCode { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public int Count { get; set; }

        /// <summary>
        /// <c>have</c> | <c>detect</c> | <c>craft</c> | <c>craft_have</c> | <c>kill</c>.
        /// For <c>kill</c>, <see cref="CollectibleCode"/> holds the entity code pattern.
        /// </summary>
        [JsonPropertyName("objective")]
        public string Objective { get; set; } = QuestbookGoalObjective.Have;

        /// <summary>
        /// Legacy mirror of <see cref="QuestbookGoalObjective.ShouldConsume"/>.
        /// Source of truth is <see cref="Objective"/>.
        /// </summary>
        [JsonPropertyName("consume")]
        public bool Consume { get; set; } = true;

        public QuestbookQuestItemData() { }

        public QuestbookQuestItemData(
            string collectibleCode,
            int count,
            string objective = QuestbookGoalObjective.Have,
            bool consume = true)
        {
            CollectibleCode = collectibleCode;
            Count = count;
            Objective = QuestbookGoalObjective.Resolve(objective, consume);
            Consume = QuestbookGoalObjective.ShouldConsume(Objective);
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
        public QuestbookLocalizedText Description { get; set; } = new();

        [JsonPropertyName("requiredItems")]
        public QuestbookQuestItemData[] RequiredItems { get; set; } = [];

        [JsonPropertyName("rewardItems")]
        public QuestbookQuestItemData[] RewardItems { get; set; } = [];

        [JsonPropertyName("consumeRequiredItems")]
        public bool ConsumeRequiredItems { get; set; } = true;

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
        public QuestbookLocalizedText Title { get; set; } = new();

        [JsonPropertyName("headerTitle")]
        public string HeaderTitle { get; set; } = string.Empty;

        [JsonPropertyName("header")]
        public QuestbookLocalizedText Header { get; set; } = new();

        [JsonPropertyName("nodes")]
        public QuestbookQuestNodeData[] Nodes { get; set; } = [];

        [JsonPropertyName("connections")]
        public QuestbookQuestConnectionData[] Connections { get; set; } = [];

        /// <summary>
        /// Monotonic id allocator so deleted node ids are never reused
        /// (player progress is keyed by category + nodeId).
        /// </summary>
        [JsonPropertyName("nextNodeId")]
        public int NextNodeId { get; set; }

        public QuestbookCategoryData() { }

        public void EnsureNextNodeIdWatermark()
        {
            int maxExisting = Nodes.Length == 0 ? -1 : Nodes.Max(static n => n.Id);
            if (NextNodeId <= maxExisting)
                NextNodeId = maxExisting + 1;
        }

        public int AllocateNodeId()
        {
            EnsureNextNodeIdWatermark();
            int id = NextNodeId;
            NextNodeId = id + 1;
            return id;
        }
    }

    public sealed class QuestbookQuestDatabase
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("categories")]
        public QuestbookCategoryData[] Categories { get; set; } = [];

        public QuestbookQuestDatabase() { }
    }

    public sealed class QuestbookQuestManifest
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("categories")]
        public QuestbookQuestManifestEntry[] Categories { get; set; } = [];
    }

    public sealed class QuestbookQuestManifestEntry
    {
        [JsonPropertyName("headerTitle")]
        public string HeaderTitle { get; set; } = string.Empty;

        [JsonPropertyName("file")]
        public string File { get; set; } = string.Empty;
    }
}
