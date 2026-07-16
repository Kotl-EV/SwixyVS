using SwixyQuestBook.Domain.Goals;
using SwixyQuestBook.Domain.Localization;
using SwixyQuestBook.Domain.Models;
using SwixyQuestBook.Network;

namespace SwixyQuestBook.Server.Sync
{
    /// <summary>
    /// Builds localized category packets for client sync (stubs, full trees, admin i18n).
    /// </summary>
    public static class QuestbookCategoryPacketBuilder
    {
        public static QuestbookSyncCategoryPacket Build(
            QuestbookCategoryData category,
            string lang,
            bool includeAllLanguages,
            bool stubOnly)
        {
            string title = category.Title?.Resolve(lang) ?? string.Empty;
            string headerDisplay = category.Header?.Resolve(lang) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(headerDisplay))
                headerDisplay = title;

            if (stubOnly)
            {
                return new QuestbookSyncCategoryPacket
                {
                    IconItemCode = category.IconItemCode,
                    Title = title,
                    HeaderTitle = category.HeaderTitle,
                    HeaderDisplay = headerDisplay,
                    TitleI18n = [],
                    HeaderI18n = [],
                    Nodes = [],
                    Connections = [],
                    IsStub = true,
                    TotalNodeCount = category.Nodes?.Length ?? 0,
                    IncludesI18n = false
                };
            }

            return new QuestbookSyncCategoryPacket
            {
                IconItemCode = category.IconItemCode,
                Title = title,
                HeaderTitle = category.HeaderTitle,
                HeaderDisplay = headerDisplay,
                TitleI18n = includeAllLanguages ? ToLangPackets(category.Title) : [],
                HeaderI18n = includeAllLanguages ? ToLangPackets(category.Header) : [],
                Nodes = category.Nodes.Select(n => new QuestbookSyncNodePacket
                {
                    Id = n.Id,
                    X = n.X,
                    Y = n.Y,
                    NodeType = GetNodeTypeInt(n.NodeType),
                    Description = n.Description?.Resolve(lang) ?? string.Empty,
                    DescriptionI18n = includeAllLanguages ? ToLangPackets(n.Description) : [],
                    RequiredItems = n.RequiredItems.Select(i => new QuestbookSyncItemPacket
                    {
                        CollectibleCode = i.CollectibleCode,
                        Count = i.Count,
                        Objective = QuestbookGoalObjective.Resolve(i.Objective, i.Consume)
                    }).ToArray(),
                    RewardItems = n.RewardItems.Select(i => new QuestbookSyncItemPacket
                    {
                        CollectibleCode = i.CollectibleCode,
                        Count = i.Count,
                        Objective = QuestbookGoalObjective.Have
                    }).ToArray(),
                    ConsumeRequiredItems = (n.RequiredItems ?? []).Any(i =>
                        QuestbookGoalObjective.ShouldConsume(i.Objective))
                        || ((n.RequiredItems == null || n.RequiredItems.Length == 0) && n.ConsumeRequiredItems)
                }).ToArray(),
                Connections = category.Connections.Select(conn => new QuestbookSyncConnectionPacket
                {
                    StartNodeId = conn.StartNodeId,
                    EndNodeId = conn.EndNodeId
                }).ToArray(),
                IsStub = false,
                TotalNodeCount = category.Nodes?.Length ?? 0,
                IncludesI18n = includeAllLanguages
            };
        }

        public static QuestbookLangTextPacket[] ToLangPackets(QuestbookLocalizedText? text)
        {
            if (text == null || text.IsEmpty)
                return [];

            return text.Entries
                .Where(static e => !string.IsNullOrWhiteSpace(e.Key) && !string.IsNullOrWhiteSpace(e.Value))
                .OrderBy(static e => e.Key, StringComparer.Ordinal)
                .Select(static e => new QuestbookLangTextPacket
                {
                    Lang = e.Key,
                    Text = e.Value
                })
                .ToArray();
        }

        public static QuestbookLocalizedText FromLangPackets(
            QuestbookLangTextPacket[]? entries,
            int maxLanguages = 48,
            int maxLangCodeLength = 12)
        {
            var text = new QuestbookLocalizedText();
            if (entries == null)
                return text;

            int accepted = 0;
            foreach (QuestbookLangTextPacket entry in entries)
            {
                if (accepted >= maxLanguages)
                    break;

                string lang = QuestbookLocalizedText.NormalizeLang(entry.Lang);
                if (string.IsNullOrWhiteSpace(lang) || lang.Length > maxLangCodeLength)
                    continue;

                bool langOk = true;
                foreach (char c in lang)
                {
                    if (!char.IsLetter(c))
                    {
                        langOk = false;
                        break;
                    }
                }

                if (!langOk)
                    continue;

                string value = entry.Text ?? string.Empty;
                if (value.Length > 2000)
                    value = value[..2000];

                text.Set(lang, value);
                accepted++;
            }

            return text;
        }

        public static int GetNodeTypeInt(string? nodeType)
        {
            if (string.Equals(nodeType, "Start", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(nodeType, "Checkpoint", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (string.Equals(nodeType, "Kill", StringComparison.OrdinalIgnoreCase))
                return 3;
            // Legacy lore nodes were type 4; treat as Quest.
            return 1;
        }
    }
}
