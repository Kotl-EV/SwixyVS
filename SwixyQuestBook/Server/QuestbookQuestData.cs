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

        /// <summary>Multi-language quest text (en/ru/…).</summary>
        [JsonPropertyName("description")]
        public QuestbookLocalizedText Description { get; set; } = new();

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

        /// <summary>Multi-language sidebar branch name.</summary>
        [JsonPropertyName("title")]
        public QuestbookLocalizedText Title { get; set; } = new();

        /// <summary>
        /// Stable category identity used for progress / network identity.
        /// Not a display string (display is <see cref="Header"/>).
        /// </summary>
        [JsonPropertyName("headerTitle")]
        public string HeaderTitle { get; set; } = string.Empty;

        /// <summary>Multi-language top-menu / start-node header display text.</summary>
        [JsonPropertyName("header")]
        public QuestbookLocalizedText Header { get; set; } = new();

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
        /// <summary>Display title resolved for the receiving client's language.</summary>
        public string Title = string.Empty;
        /// <summary>Stable category key (progress / identity).</summary>
        public string HeaderTitle = string.Empty;
        /// <summary>Display header resolved for the receiving client's language.</summary>
        public string HeaderDisplay = string.Empty;
        /// <summary>All language variants for admin editing / re-resolve.</summary>
        public QuestbookLangTextPacket[] TitleI18n = [];
        public QuestbookLangTextPacket[] HeaderI18n = [];
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
        /// <summary>Description resolved for the receiving client's language.</summary>
        public string Description = string.Empty;
        /// <summary>All language variants for admin editing.</summary>
        public QuestbookLangTextPacket[] DescriptionI18n = [];
        public QuestbookSyncItemPacket[] RequiredItems = [];
        public QuestbookSyncItemPacket[] RewardItems = [];
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
    }
}
