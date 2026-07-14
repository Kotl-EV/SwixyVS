using SwixyQuestBook.Gui;
using SwixyQuestBook.Network;
using SwixyQuestBook.Server;
using Vintagestory.API.Client;

namespace SwixyQuestBook.Client
{
    public sealed class QuestbookClientDataManager
    {
        private ICoreClientAPI? capi;

        public QuestbookCategoryDefinition[] Categories { get; private set; } = [];
        public int TotalQuestsCompleted { get; private set; }
        public Dictionary<string, QuestbookSyncCompletedQuestPacket> CompletedQuestsMap { get; private set; } = new();

        public event Action? QuestDataUpdated;
        public event Action? ProgressUpdated;

        public QuestbookClientDataManager(ICoreClientAPI api)
        {
            capi = api;
        }

        public void HandleQuestsPacket(QuestbookSyncQuestsPacket packet)
        {
            if (packet?.Categories == null) return;

            Categories = packet.Categories.Select(ConvertSyncCategoryToDefinition).ToArray();
            QuestDataUpdated?.Invoke();
            capi?.Logger.Notification($"[SwixyQuestBook] Received {Categories.Length} categories");
        }

        public void HandleProgressPacket(QuestbookSyncProgressPacket packet)
        {
            if (packet == null) return;

            TotalQuestsCompleted = packet.TotalQuestsCompleted;
            CompletedQuestsMap = packet.CompletedQuests
                .ToDictionary(q => $"{q.CategoryHeaderTitle}:{q.NodeId}");

            UpdateNodeStatesFromProgress();
            ProgressUpdated?.Invoke();
            capi?.Logger.Notification($"[SwixyQuestBook] Progress: {TotalQuestsCompleted} quests completed");
        }

        public void HandleQuestSubmitResponse(QuestbookSubmitQuestResponse response)
        {
            if (response == null || !response.Success) return;

            string key = $"{response.CategoryHeaderTitle}:{response.NodeId}";
            if (!CompletedQuestsMap.ContainsKey(key))
            {
                CompletedQuestsMap[key] = new QuestbookSyncCompletedQuestPacket
                {
                    CategoryHeaderTitle = response.CategoryHeaderTitle,
                    NodeId = response.NodeId,
                    CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    CompletionOrder = TotalQuestsCompleted + 1
                };
                TotalQuestsCompleted++;
                ProgressUpdated?.Invoke();
            }
        }

        public bool IsQuestCompleted(string categoryHeaderTitle, int nodeId)
        {
            return CompletedQuestsMap.ContainsKey($"{categoryHeaderTitle}:{nodeId}");
        }

        public QuestbookSyncCompletedQuestPacket? GetCompletedQuestInfo(string categoryHeaderTitle, int nodeId)
        {
            return CompletedQuestsMap.TryGetValue($"{categoryHeaderTitle}:{nodeId}", out var info) ? info : null;
        }

        public QuestbookSyncCompletedQuestPacket[] GetCompletedQuestsInOrder()
        {
            return CompletedQuestsMap.Values.OrderBy(q => q.CompletionOrder).ToArray();
        }

        public void UpdateCategory(int index, QuestbookCategoryDefinition category)
        {
            if (index >= 0 && index < Categories.Length)
            {
                Categories[index] = category;
                QuestDataUpdated?.Invoke();
            }
        }

        public void UpdateCategories(QuestbookCategoryDefinition[] newCategories)
        {
            Categories = newCategories;
            QuestDataUpdated?.Invoke();
        }

        private void UpdateNodeStatesFromProgress()
        {
            foreach (var category in Categories)
            {
                foreach (var node in category.Nodes)
                {
                    if (IsQuestCompleted(category.HeaderTitle, node.Id))
                        node.MarkCompleted();
                }
            }
        }

        private QuestbookCategoryDefinition ConvertSyncCategoryToDefinition(QuestbookSyncCategoryPacket packet)
        {
            var nodes = packet.Nodes.Select(n => new QuestbookQuestNodeDefinition(
                n.Id,
                n.X,
                n.Y,
                QuestbookQuestNodeState.Available,
                n.Description,
                requiredItems: n.RequiredItems.Select(i => new QuestbookQuestItemRequirement(i.CollectibleCode, i.Count)).ToArray(),
                rewardItems: n.RewardItems.Select(i => new QuestbookQuestItemRequirement(i.CollectibleCode, i.Count)).ToArray(),
                nodeType: ConvertIntToNodeType(n.NodeType)
            )).ToArray();

            foreach (var node in nodes)
            {
                if (IsQuestCompleted(packet.HeaderTitle, node.Id))
                    node.MarkCompleted();
            }

            var connections = packet.Connections.Select(c => new QuestbookQuestConnectionDefinition(c.StartNodeId, c.EndNodeId)).ToArray();

            return new QuestbookCategoryDefinition(
                packet.IconItemCode,
                packet.Title,
                packet.HeaderTitle,
                CalculateProgressPercent(nodes),
                nodes,
                connections
            );
        }

        private static int CalculateProgressPercent(QuestbookQuestNodeDefinition[] nodes)
        {
            if (nodes.Length == 0) return 0;
            int completedCount = nodes.Count(n => n.State == QuestbookQuestNodeState.Completed);
            return (int)Math.Round((double)completedCount / nodes.Length * 100, MidpointRounding.AwayFromZero);
        }

        private static QuestbookQuestNodeType ConvertIntToNodeType(int nodeType)
        {
            return nodeType switch
            {
                0 => QuestbookQuestNodeType.Start,
                2 => QuestbookQuestNodeType.Checkpoint,
                _ => QuestbookQuestNodeType.Quest
            };
        }
    }
}
