using SwixyQuestBook.Domain.Models;
using SwixyQuestBook.Gui;
using SwixyQuestBook.Network;
using System.Linq;
using Vintagestory.API.Client;

namespace SwixyQuestBook.Client
{
    public sealed class QuestbookClientDataManager
    {
        private ICoreClientAPI? capi;
        private readonly HashSet<string> pendingCategoryRequests = new(StringComparer.Ordinal);

        public QuestbookCategoryDefinition[] Categories { get; private set; } = [];
        public int TotalQuestsCompleted { get; private set; }
        public Dictionary<string, QuestbookSyncCompletedQuestPacket> CompletedQuestsMap { get; private set; } = new();
        /// <summary>Key: category:nodeId:code (lower) → crafted count toward craft goals.</summary>
        public Dictionary<string, int> CraftProgressMap { get; private set; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Key: category:nodeId:entityCode (lower) → kills toward kill goals.</summary>
        public Dictionary<string, int> KillProgressMap { get; private set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public event Action? QuestDataUpdated;
        public event Action? ProgressUpdated;

        public QuestbookClientDataManager(ICoreClientAPI api)
        {
            capi = api;
        }

        public void HandleQuestsPacket(QuestbookSyncQuestsPacket packet)
        {
            if (packet?.Categories == null) return;

            // Preserve already-loaded full trees when server re-sends the stub list
            // (structural list refresh), so open graphs do not blank out.
            var previousByKey = Categories.ToDictionary(
                c => c.HeaderTitle,
                c => c,
                StringComparer.Ordinal);

            Categories = packet.Categories.Select(p =>
            {
                QuestbookCategoryDefinition converted = ConvertSyncCategoryToDefinition(p);
                if (!converted.IsContentLoaded
                    && previousByKey.TryGetValue(converted.HeaderTitle, out QuestbookCategoryDefinition? prev)
                    && prev.IsContentLoaded)
                {
                    return MergePreservingContent(converted, prev);
                }

                return converted;
            }).ToArray();

            pendingCategoryRequests.Clear();
            QuestDataUpdated?.Invoke();
            capi?.Logger.Notification($"[SwixyQuestBook] Received {Categories.Length} categories (list/stubs)");
        }

        public void HandleCategoryUpdatePacket(QuestbookSyncCategoryUpdatePacket packet)
        {
            if (packet?.Category == null) return;

            QuestbookCategoryDefinition updated = ConvertSyncCategoryToDefinition(packet.Category);
            pendingCategoryRequests.Remove(updated.HeaderTitle);
            pendingCategoryRequests.Remove(I18nRequestKey(updated.HeaderTitle));

            bool found = false;
            for (int i = 0; i < Categories.Length; i++)
            {
                if (!string.Equals(Categories[i].HeaderTitle, updated.HeaderTitle, StringComparison.Ordinal))
                    continue;

                // Keep full i18n maps if a non-i18n broadcast arrives while editor has them.
                if (!updated.HasFullI18n && Categories[i].HasFullI18n && updated.IsContentLoaded)
                    Categories[i] = MergePreservingI18n(updated, Categories[i]);
                else
                    Categories[i] = updated;

                found = true;
                break;
            }

            if (!found)
            {
                Categories = Categories.Append(updated).ToArray();
            }

            QuestDataUpdated?.Invoke();
            capi?.Logger.Notification(
                $"[SwixyQuestBook] Category loaded: {updated.HeaderTitle} ({updated.Nodes.Length} nodes, i18n={updated.HasFullI18n})");
        }

        public void HandleCategoryMetaPacket(QuestbookSyncCategoryMetaPacket packet)
        {
            if (packet == null || string.IsNullOrWhiteSpace(packet.HeaderTitle))
                return;

            if (packet.Removed)
            {
                Categories = Categories
                    .Where(c => !string.Equals(c.HeaderTitle, packet.HeaderTitle, StringComparison.Ordinal))
                    .ToArray();
                QuestDataUpdated?.Invoke();
                return;
            }

            for (int i = 0; i < Categories.Length; i++)
            {
                if (!string.Equals(Categories[i].HeaderTitle, packet.HeaderTitle, StringComparison.Ordinal))
                    continue;

                QuestbookCategoryDefinition prev = Categories[i];
                Categories[i] = new QuestbookCategoryDefinition(
                    string.IsNullOrWhiteSpace(packet.IconItemCode) ? prev.IconItemCode : packet.IconItemCode,
                    string.IsNullOrWhiteSpace(packet.Title) ? prev.Title : packet.Title,
                    prev.HeaderTitle,
                    prev.ProgressPercent,
                    prev.Nodes,
                    prev.Connections,
                    string.IsNullOrWhiteSpace(packet.HeaderDisplay) ? prev.HeaderDisplay : packet.HeaderDisplay,
                    prev.TitleByLang,
                    prev.HeaderByLang,
                    isContentLoaded: prev.IsContentLoaded,
                    totalNodeCount: packet.TotalNodeCount > 0 ? packet.TotalNodeCount : prev.TotalNodeCount,
                    hasFullI18n: prev.HasFullI18n);
                QuestDataUpdated?.Invoke();
                return;
            }

            if (packet.IsNew)
            {
                Categories = Categories.Append(new QuestbookCategoryDefinition(
                    packet.IconItemCode,
                    packet.Title,
                    packet.HeaderTitle,
                    0,
                    [],
                    [],
                    packet.HeaderDisplay,
                    isContentLoaded: false,
                    totalNodeCount: packet.TotalNodeCount,
                    hasFullI18n: false)).ToArray();
                QuestDataUpdated?.Invoke();
            }
        }

        public void HandleProgressPacket(QuestbookSyncProgressPacket packet)
        {
            if (packet == null) return;

            TotalQuestsCompleted = packet.TotalQuestsCompleted;

            if (packet.IsFullSync || CompletedQuestsMap.Count == 0)
            {
                CompletedQuestsMap = (packet.CompletedQuests ?? [])
                    .ToDictionary(q => $"{q.CategoryHeaderTitle}:{q.NodeId}");
            }
            else if (packet.CompletedQuests is { Length: > 0 })
            {
                foreach (QuestbookSyncCompletedQuestPacket entry in packet.CompletedQuests)
                {
                    string key = $"{entry.CategoryHeaderTitle}:{entry.NodeId}";
                    CompletedQuestsMap[key] = entry;
                }
            }

            // Server always sends full craft/kill snapshots on full sync and after progress updates.
            if (packet.IsFullSync || packet.CraftProgress != null)
            {
                CraftProgressMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (QuestbookSyncCraftProgressPacket c in packet.CraftProgress ?? [])
                {
                    if (string.IsNullOrWhiteSpace(c.CollectibleCode) || c.Count <= 0)
                        continue;
                    string key = $"{c.CategoryHeaderTitle}:{c.NodeId}:{c.CollectibleCode}".ToLowerInvariant();
                    CraftProgressMap[key] = CraftProgressMap.TryGetValue(key, out int existing)
                        ? existing + c.Count
                        : c.Count;
                }
            }

            if (packet.IsFullSync || packet.KillProgress != null)
            {
                KillProgressMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (QuestbookSyncCraftProgressPacket c in packet.KillProgress ?? [])
                {
                    if (string.IsNullOrWhiteSpace(c.CollectibleCode) || c.Count <= 0)
                        continue;
                    string key = $"{c.CategoryHeaderTitle}:{c.NodeId}:{c.CollectibleCode}".ToLowerInvariant();
                    KillProgressMap[key] = KillProgressMap.TryGetValue(key, out int existing)
                        ? existing + c.Count
                        : c.Count;
                }
            }

            UpdateNodeStatesFromProgress();
            ProgressUpdated?.Invoke();
            capi?.Logger.Debug(
                $"[SwixyQuestBook] Progress sync: total={TotalQuestsCompleted}, full={packet.IsFullSync}, delta={packet.CompletedQuests?.Length ?? 0}, crafts={CraftProgressMap.Count}, kills={KillProgressMap.Count}");
        }

        public int GetCraftProgress(string categoryHeaderTitle, int nodeId, string pattern)
        {
            return SumProgressMatching(CraftProgressMap, categoryHeaderTitle, nodeId, pattern);
        }

        public int GetKillProgress(string categoryHeaderTitle, int nodeId, string pattern)
        {
            return SumProgressMatching(KillProgressMap, categoryHeaderTitle, nodeId, pattern);
        }

        private static int SumProgressMatching(
            Dictionary<string, int> map,
            string categoryHeaderTitle,
            int nodeId,
            string pattern)
        {
            int total = 0;
            string prefix = $"{categoryHeaderTitle}:{nodeId}:".ToLowerInvariant();
            foreach ((string key, int count) in map)
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                string code = key[prefix.Length..];
                if (QuestbookInventoryHelper.MatchesCollectibleCode(code, pattern))
                    total += count;
            }

            return total;
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

        /// <summary>
        /// Local admin preview: drop completion/craft/kill for a deleted node so a new quest
        /// at the same coordinates does not inherit "completed" until server save.
        /// </summary>
        public void ClearLocalProgressForNode(string categoryHeaderTitle, int nodeId)
        {
            if (string.IsNullOrWhiteSpace(categoryHeaderTitle))
                return;

            string completedKey = $"{categoryHeaderTitle}:{nodeId}";
            bool changed = CompletedQuestsMap.Remove(completedKey);

            string prefix = $"{categoryHeaderTitle}:{nodeId}:".ToLowerInvariant();
            string[] craftKeys = CraftProgressMap.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray();
            foreach (string k in craftKeys)
            {
                CraftProgressMap.Remove(k);
                changed = true;
            }

            string[] killKeys = KillProgressMap.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray();
            foreach (string k in killKeys)
            {
                KillProgressMap.Remove(k);
                changed = true;
            }

            if (!changed)
                return;

            TotalQuestsCompleted = CompletedQuestsMap.Count;
            UpdateNodeStatesFromProgress();
            ProgressUpdated?.Invoke();
        }

        public QuestbookSyncCompletedQuestPacket? GetCompletedQuestInfo(string categoryHeaderTitle, int nodeId)
        {
            return CompletedQuestsMap.TryGetValue($"{categoryHeaderTitle}:{nodeId}", out var info) ? info : null;
        }

        public QuestbookSyncCompletedQuestPacket[] GetCompletedQuestsInOrder()
        {
            return CompletedQuestsMap.Values.OrderBy(q => q.CompletionOrder).ToArray();
        }

        public int CountCompletedInCategory(string categoryHeaderTitle)
        {
            int count = 0;
            foreach (QuestbookSyncCompletedQuestPacket entry in CompletedQuestsMap.Values)
            {
                if (string.Equals(entry.CategoryHeaderTitle, categoryHeaderTitle, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Requests full branch content from the server if not yet loaded.
        /// <paramref name="includeI18n"/> is for the admin editor (full language maps).
        /// </summary>
        public bool EnsureCategoryContentLoaded(string? headerTitle, bool includeI18n = false)
        {
            if (string.IsNullOrWhiteSpace(headerTitle))
                return false;

            QuestbookCategoryDefinition? category = Categories.FirstOrDefault(c =>
                string.Equals(c.HeaderTitle, headerTitle, StringComparison.Ordinal));

            if (category == null)
                return false;

            if (category.IsContentLoaded && (!includeI18n || category.HasFullI18n))
                return false;

            string requestKey = includeI18n ? I18nRequestKey(headerTitle) : headerTitle;
            if (!pendingCategoryRequests.Add(requestKey))
                return false;

            return QuestbookMod.TrySendRequestCategory(new QuestbookRequestCategoryPacket
            {
                HeaderTitle = headerTitle,
                IncludeI18n = includeI18n
            });
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

        private static string I18nRequestKey(string headerTitle) => headerTitle + "\0i18n";

        private static QuestbookCategoryDefinition MergePreservingContent(
            QuestbookCategoryDefinition stubMeta,
            QuestbookCategoryDefinition previous)
        {
            return new QuestbookCategoryDefinition(
                stubMeta.IconItemCode,
                stubMeta.Title,
                stubMeta.HeaderTitle,
                CalculateProgressPercentStatic(previous.Nodes, stubMeta.HeaderTitle, previous.TotalNodeCount, null),
                previous.Nodes,
                previous.Connections,
                stubMeta.HeaderDisplay,
                previous.HasFullI18n ? previous.TitleByLang : stubMeta.TitleByLang,
                previous.HasFullI18n ? previous.HeaderByLang : stubMeta.HeaderByLang,
                isContentLoaded: true,
                totalNodeCount: previous.TotalNodeCount,
                hasFullI18n: previous.HasFullI18n);
        }

        private static QuestbookCategoryDefinition MergePreservingI18n(
            QuestbookCategoryDefinition updated,
            QuestbookCategoryDefinition previousWithI18n)
        {
            // Rebuild nodes with previous description maps when ids match.
            var prevById = previousWithI18n.Nodes.ToDictionary(n => n.Id);
            var mergedNodes = updated.Nodes.Select(n =>
            {
                if (!prevById.TryGetValue(n.Id, out QuestbookQuestNodeDefinition? prev))
                    return n;

                return new QuestbookQuestNodeDefinition(
                    n.Id,
                    n.X,
                    n.Y,
                    n.State,
                    n.Description,
                    n.NodeType,
                    n.RequiredItems,
                    n.RewardItems,
                    descriptionByLang: prev.DescriptionByLang,
                    consumeRequiredItems: n.ConsumeRequiredItems);
            }).ToArray();

            return new QuestbookCategoryDefinition(
                updated.IconItemCode,
                updated.Title,
                updated.HeaderTitle,
                updated.ProgressPercent,
                mergedNodes,
                updated.Connections,
                updated.HeaderDisplay,
                previousWithI18n.TitleByLang,
                previousWithI18n.HeaderByLang,
                isContentLoaded: true,
                totalNodeCount: updated.TotalNodeCount,
                hasFullI18n: true);
        }

        private void UpdateNodeStatesFromProgress()
        {
            for (int i = 0; i < Categories.Length; i++)
            {
                QuestbookCategoryDefinition category = Categories[i];
                if (!category.IsContentLoaded)
                    continue;

                foreach (var node in category.Nodes)
                {
                    if (IsQuestCompleted(category.HeaderTitle, node.Id))
                        node.MarkCompleted();
                }
            }
        }

        private QuestbookCategoryDefinition ConvertSyncCategoryToDefinition(QuestbookSyncCategoryPacket packet)
        {
            bool isStub = packet.IsStub
                || (packet.Nodes.Length == 0 && packet.TotalNodeCount > 0);

            var nodes = isStub
                ? []
                : packet.Nodes.Select(n =>
                {
                    // Objective string is the source of truth: have | detect | craft.
                    var required = n.RequiredItems.Select(i => new QuestbookQuestItemRequirement(
                        i.CollectibleCode,
                        i.Count,
                        i.Objective)).ToArray();

                    return new QuestbookQuestNodeDefinition(
                        n.Id,
                        n.X,
                        n.Y,
                        QuestbookQuestNodeState.Available,
                        n.Description,
                        requiredItems: required,
                        rewardItems: n.RewardItems.Select(i => new QuestbookQuestItemRequirement(
                            i.CollectibleCode, i.Count)).ToArray(),
                        nodeType: ConvertIntToNodeType(n.NodeType),
                        descriptionByLang: ToLangMap(n.DescriptionI18n, n.Description),
                        consumeRequiredItems: n.ConsumeRequiredItems);
                }).ToArray();

            if (!isStub)
            {
                foreach (var node in nodes)
                {
                    if (IsQuestCompleted(packet.HeaderTitle, node.Id))
                        node.MarkCompleted();
                }
            }

            var connections = isStub
                ? []
                : packet.Connections.Select(c => new QuestbookQuestConnectionDefinition(c.StartNodeId, c.EndNodeId)).ToArray();

            int totalNodeCount = packet.TotalNodeCount > 0
                ? packet.TotalNodeCount
                : nodes.Length;

            return new QuestbookCategoryDefinition(
                packet.IconItemCode,
                packet.Title,
                packet.HeaderTitle,
                CalculateProgressPercent(nodes, packet.HeaderTitle, totalNodeCount),
                nodes,
                connections,
                string.IsNullOrWhiteSpace(packet.HeaderDisplay) ? packet.Title : packet.HeaderDisplay,
                ToLangMap(packet.TitleI18n, packet.Title),
                ToLangMap(packet.HeaderI18n, packet.HeaderDisplay),
                isContentLoaded: !isStub,
                totalNodeCount: totalNodeCount,
                hasFullI18n: packet.IncludesI18n
            );
        }

        private int CalculateProgressPercent(
            QuestbookQuestNodeDefinition[] nodes,
            string categoryHeaderTitle,
            int totalNodeCount)
        {
            return CalculateProgressPercentStatic(nodes, categoryHeaderTitle, totalNodeCount, CountCompletedInCategory);
        }

        private static int CalculateProgressPercentStatic(
            QuestbookQuestNodeDefinition[] nodes,
            string categoryHeaderTitle,
            int totalNodeCount,
            Func<string, int>? countCompleted)
        {
            int total = totalNodeCount > 0 ? totalNodeCount : nodes.Length;
            if (total <= 0) return 0;

            int completedCount = nodes.Length > 0
                ? nodes.Count(n => n.State == QuestbookQuestNodeState.Completed)
                : (countCompleted?.Invoke(categoryHeaderTitle) ?? 0);

            return (int)Math.Round((double)completedCount / total * 100, MidpointRounding.AwayFromZero);
        }

        private static Dictionary<string, string> ToLangMap(
            QuestbookLangTextPacket[]? entries,
            string fallback)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (entries != null)
            {
                foreach (QuestbookLangTextPacket entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Lang) || string.IsNullOrWhiteSpace(entry.Text))
                        continue;
                    map[entry.Lang.Trim().ToLowerInvariant()] = entry.Text;
                }
            }

            if (map.Count == 0 && !string.IsNullOrWhiteSpace(fallback))
                map["en"] = fallback;

            return map;
        }

        private static QuestbookQuestNodeType ConvertIntToNodeType(int nodeType)
        {
            return nodeType switch
            {
                0 => QuestbookQuestNodeType.Start,
                2 => QuestbookQuestNodeType.Checkpoint,
                3 => QuestbookQuestNodeType.Kill,
                // Legacy type 4 was Lore — treat as Quest.
                _ => QuestbookQuestNodeType.Quest
            };
        }
    }
}
