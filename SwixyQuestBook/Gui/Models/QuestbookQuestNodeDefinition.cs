using SwixyQuestBook.Domain.Models;

namespace SwixyQuestBook.Gui
{
    public sealed class QuestbookQuestNodeDefinition
    {
        public int Id { get; }
        public double X { get; }
        public double Y { get; }
        public QuestbookQuestNodeState State { get; private set; }
        public QuestbookQuestNodeType NodeType { get; }
        public string Description { get; }
        public IReadOnlyDictionary<string, string> DescriptionByLang { get; }
        public QuestbookQuestItemRequirement[] RequiredItems { get; }
        public QuestbookQuestItemRequirement[] RewardItems { get; }
        public bool ConsumeRequiredItems { get; }

        public bool IsStartNode => NodeType == QuestbookQuestNodeType.Start;

        public bool UsesInfoModalLayout =>
            NodeType == QuestbookQuestNodeType.Start || NodeType == QuestbookQuestNodeType.Checkpoint;

        /// <summary>Quest and Kill nodes use the goals/rewards modal.</summary>
        public bool IsObjectiveNode =>
            NodeType is QuestbookQuestNodeType.Quest or QuestbookQuestNodeType.Kill;

        public bool SupportsItemIcon => IsObjectiveNode;

        public QuestbookQuestNodeDefinition(
            int id,
            double x,
            double y,
            QuestbookQuestNodeState state,
            string description = "",
            QuestbookQuestNodeType nodeType = QuestbookQuestNodeType.Quest,
            QuestbookQuestItemRequirement[]? requiredItems = null,
            QuestbookQuestItemRequirement[]? rewardItems = null,
            IReadOnlyDictionary<string, string>? descriptionByLang = null,
            bool consumeRequiredItems = true)
        {
            Id = id;
            X = x;
            Y = y;
            State = state;
            Description = description;
            NodeType = nodeType;
            RequiredItems = requiredItems?.Length > 0 ? requiredItems : [];
            RewardItems = rewardItems?.Length > 0 ? rewardItems : [];
            DescriptionByLang = CloneLangMap(descriptionByLang, description);
            bool anyItemConsume = RequiredItems.Any(static i => i.Consume);
            bool anyGoal = RequiredItems.Length > 0;
            ConsumeRequiredItems = anyGoal ? anyItemConsume : consumeRequiredItems;
        }

        private static IReadOnlyDictionary<string, string> CloneLangMap(
            IReadOnlyDictionary<string, string>? source,
            string fallbackDescription)
        {
            var map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            if (source != null)
            {
                foreach ((string lang, string text) in source)
                {
                    if (!string.IsNullOrWhiteSpace(lang) && !string.IsNullOrWhiteSpace(text))
                        map[lang.Trim().ToLowerInvariant()] = text;
                }
            }

            if (map.Count == 0 && !string.IsNullOrWhiteSpace(fallbackDescription))
                map["en"] = fallbackDescription;

            return map;
        }

        public void MarkCompleted()
        {
            State = QuestbookQuestNodeState.Completed;
        }
    }
}
