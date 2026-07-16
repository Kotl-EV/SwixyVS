using System.Text.Json.Serialization;

namespace SwixyQuestBook.Domain.Progress
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

    public sealed class QuestbookCraftProgressEntry
    {
        [JsonPropertyName("categoryHeaderTitle")]
        public string CategoryHeaderTitle { get; set; } = string.Empty;

        [JsonPropertyName("nodeId")]
        public int NodeId { get; set; }

        [JsonPropertyName("collectibleCode")]
        public string CollectibleCode { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public int Count { get; set; }

        public QuestbookCraftProgressEntry() { }

        public QuestbookCraftProgressEntry(string categoryHeaderTitle, int nodeId, string collectibleCode, int count)
        {
            CategoryHeaderTitle = categoryHeaderTitle;
            NodeId = nodeId;
            CollectibleCode = collectibleCode;
            Count = count;
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

        [JsonPropertyName("craftProgress")]
        public QuestbookCraftProgressEntry[] CraftProgress { get; set; } = [];

        [JsonPropertyName("killProgress")]
        public QuestbookCraftProgressEntry[] KillProgress { get; set; } = [];

        [JsonIgnore]
        public Dictionary<string, QuestbookCompletedQuestEntry> CompletedQuestsMap { get; set; } = new();

        [JsonIgnore]
        public Dictionary<string, QuestbookCraftProgressEntry> CraftProgressMap { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        [JsonIgnore]
        public Dictionary<string, QuestbookCraftProgressEntry> KillProgressMap { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public QuestbookPlayerProgressData() { }

        public QuestbookPlayerProgressData(string playerUid, string playerName)
        {
            PlayerUid = playerUid;
            PlayerName = playerName;
            TotalQuestsCompleted = 0;
            LastPlayedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            CompletedQuests = [];
            CompletedQuestsMap = new();
            CraftProgress = [];
            CraftProgressMap = new(StringComparer.OrdinalIgnoreCase);
            KillProgress = [];
            KillProgressMap = new(StringComparer.OrdinalIgnoreCase);
        }

        public static string CraftKey(string categoryHeaderTitle, int nodeId, string collectibleCode) =>
            $"{categoryHeaderTitle}:{nodeId}:{collectibleCode}".ToLowerInvariant();

        public static string KillKey(string categoryHeaderTitle, int nodeId, string entityCode) =>
            CraftKey(categoryHeaderTitle, nodeId, entityCode);

        public bool IsQuestCompleted(string categoryHeaderTitle, int nodeId) =>
            CompletedQuestsMap.ContainsKey($"{categoryHeaderTitle}:{nodeId}");

        public int GetCraftCount(string categoryHeaderTitle, int nodeId, string collectibleCode)
        {
            string key = CraftKey(categoryHeaderTitle, nodeId, collectibleCode);
            return CraftProgressMap.TryGetValue(key, out QuestbookCraftProgressEntry? e) ? e.Count : 0;
        }

        public void AddCraftCount(string categoryHeaderTitle, int nodeId, string collectibleCode, int amount)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(collectibleCode))
                return;

            string key = CraftKey(categoryHeaderTitle, nodeId, collectibleCode);
            if (CraftProgressMap.TryGetValue(key, out QuestbookCraftProgressEntry? existing))
                existing.Count += amount;
            else
                CraftProgressMap[key] = new QuestbookCraftProgressEntry(
                    categoryHeaderTitle, nodeId, collectibleCode, amount);

            CraftProgress = CraftProgressMap.Values.ToArray();
            LastPlayedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void ClearCraftProgressForNode(string categoryHeaderTitle, int nodeId)
        {
            string prefix = $"{categoryHeaderTitle}:{nodeId}:".ToLowerInvariant();
            var remove = CraftProgressMap.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray();
            if (remove.Length == 0)
                return;

            foreach (string k in remove)
                CraftProgressMap.Remove(k);

            CraftProgress = CraftProgressMap.Values.ToArray();
        }

        public void AddKillCount(string categoryHeaderTitle, int nodeId, string entityCode, int amount)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(entityCode))
                return;

            string key = KillKey(categoryHeaderTitle, nodeId, entityCode);
            if (KillProgressMap.TryGetValue(key, out QuestbookCraftProgressEntry? existing))
                existing.Count += amount;
            else
                KillProgressMap[key] = new QuestbookCraftProgressEntry(
                    categoryHeaderTitle, nodeId, entityCode, amount);

            KillProgress = KillProgressMap.Values.ToArray();
            LastPlayedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void ClearKillProgressForNode(string categoryHeaderTitle, int nodeId)
        {
            string prefix = $"{categoryHeaderTitle}:{nodeId}:".ToLowerInvariant();
            var remove = KillProgressMap.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray();
            if (remove.Length == 0)
                return;

            foreach (string k in remove)
                KillProgressMap.Remove(k);

            KillProgress = KillProgressMap.Values.ToArray();
        }

        public void RebuildCraftProgressMap()
        {
            CraftProgressMap = new Dictionary<string, QuestbookCraftProgressEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (QuestbookCraftProgressEntry entry in CraftProgress ?? [])
            {
                if (string.IsNullOrWhiteSpace(entry.CollectibleCode) || entry.Count <= 0)
                    continue;
                string key = CraftKey(entry.CategoryHeaderTitle, entry.NodeId, entry.CollectibleCode);
                if (CraftProgressMap.TryGetValue(key, out QuestbookCraftProgressEntry? existing))
                    existing.Count += entry.Count;
                else
                    CraftProgressMap[key] = entry;
            }

            CraftProgress = CraftProgressMap.Values.ToArray();
        }

        public void RebuildKillProgressMap()
        {
            KillProgressMap = new Dictionary<string, QuestbookCraftProgressEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (QuestbookCraftProgressEntry entry in KillProgress ?? [])
            {
                if (string.IsNullOrWhiteSpace(entry.CollectibleCode) || entry.Count <= 0)
                    continue;
                string key = KillKey(entry.CategoryHeaderTitle, entry.NodeId, entry.CollectibleCode);
                if (KillProgressMap.TryGetValue(key, out QuestbookCraftProgressEntry? existing))
                    existing.Count += entry.Count;
                else
                    KillProgressMap[key] = entry;
            }

            KillProgress = KillProgressMap.Values.ToArray();
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
            ClearCraftProgressForNode(categoryHeaderTitle, nodeId);
            ClearKillProgressForNode(categoryHeaderTitle, nodeId);
            LastPlayedAt = now;
        }

        public bool ClearAllProgressForNode(string categoryHeaderTitle, int nodeId)
        {
            bool changed = false;
            string completedKey = $"{categoryHeaderTitle}:{nodeId}";
            if (CompletedQuestsMap.Remove(completedKey))
                changed = true;

            int craftBefore = CraftProgressMap.Count;
            ClearCraftProgressForNode(categoryHeaderTitle, nodeId);
            if (CraftProgressMap.Count != craftBefore)
                changed = true;

            int killBefore = KillProgressMap.Count;
            ClearKillProgressForNode(categoryHeaderTitle, nodeId);
            if (KillProgressMap.Count != killBefore)
                changed = true;

            if (changed)
            {
                CompletedQuests = CompletedQuestsMap.Values
                    .OrderBy(e => e.CompletionOrder)
                    .ToArray();
                TotalQuestsCompleted = CompletedQuests.Length;
                LastPlayedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            return changed;
        }

        public void UpdateLastPlayed() =>
            LastPlayedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
