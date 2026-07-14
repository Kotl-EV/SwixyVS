using ProtoBuf;
using System.Text.Json.Serialization;

namespace SwixyQuestBook.Server
{
    public sealed class QuestbookCompletedQuestEntry
    {
        [JsonPropertyName("categoryHeaderTitle")]
        public string CategoryHeaderTitle { get; set; } = string.Empty;

        [JsonPropertyName("nodeId")]
        public int NodeId { get; set; }

        [JsonPropertyName("completedAt")]
        public long CompletedAt { get; set; }

        [JsonPropertyName("completionOrder")]
        public int CompletionOrder { get; set; }

        public QuestbookCompletedQuestEntry() { }

        public QuestbookCompletedQuestEntry(string categoryHeaderTitle, int nodeId, long completedAt, int completionOrder)
        {
            CategoryHeaderTitle = categoryHeaderTitle;
            NodeId = nodeId;
            CompletedAt = completedAt;
            CompletionOrder = completionOrder;
        }
    }

    public sealed class QuestbookPlayerProgressData
    {
        [JsonPropertyName("playerUid")]
        public string PlayerUid { get; set; } = string.Empty;

        [JsonPropertyName("playerName")]
        public string PlayerName { get; set; } = string.Empty;

        [JsonPropertyName("totalQuestsCompleted")]
        public int TotalQuestsCompleted { get; set; }

        [JsonPropertyName("lastPlayedAt")]
        public long LastPlayedAt { get; set; }

        [JsonPropertyName("completedQuests")]
        public QuestbookCompletedQuestEntry[] CompletedQuests { get; set; } = [];

        [JsonIgnore]
        public Dictionary<string, QuestbookCompletedQuestEntry> CompletedQuestsMap { get; set; } = new();

        public QuestbookPlayerProgressData() { }

        public QuestbookPlayerProgressData(string playerUid, string playerName)
        {
            PlayerUid = playerUid;
            PlayerName = playerName;
            TotalQuestsCompleted = 0;
            LastPlayedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            CompletedQuests = [];
            CompletedQuestsMap = new();
        }

        public bool IsQuestCompleted(string categoryHeaderTitle, int nodeId)
        {
            return CompletedQuestsMap.ContainsKey($"{categoryHeaderTitle}:{nodeId}");
        }

        public void AddCompletedQuest(string categoryHeaderTitle, int nodeId)
        {
            string key = $"{categoryHeaderTitle}:{nodeId}";
            if (CompletedQuestsMap.ContainsKey(key))
                return;

            TotalQuestsCompleted++;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var entry = new QuestbookCompletedQuestEntry(categoryHeaderTitle, nodeId, now, TotalQuestsCompleted);
            CompletedQuestsMap[key] = entry;
            CompletedQuests = CompletedQuestsMap.Values
                .OrderBy(e => e.CompletionOrder)
                .ToArray();
            LastPlayedAt = now;
        }

        public void UpdateLastPlayed()
        {
            LastPlayedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    // Сетевые пакеты для синхронизации прогресса

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncProgressPacket
    {
        public int TotalQuestsCompleted;
        public QuestbookSyncCompletedQuestPacket[] CompletedQuests = [];
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class QuestbookSyncCompletedQuestPacket
    {
        public string CategoryHeaderTitle = string.Empty;
        public int NodeId;
        public long CompletedAt;
        public int CompletionOrder;
    }
}
