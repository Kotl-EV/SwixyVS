namespace SwixyQuestBook.Gui
{
    public sealed class QuestbookQuestItemRequirement
    {
        public string CollectibleCode { get; }
        public int Count { get; }

        public QuestbookQuestItemRequirement(string collectibleCode, int count)
        {
            CollectibleCode = collectibleCode;
            Count = count;
        }
    }

    public sealed class QuestbookQuestNodeDefinition
    {
        public int Id { get; }
        public double X { get; }
        public double Y { get; }
        public QuestbookQuestNodeState State { get; private set; }
        public QuestbookQuestNodeType NodeType { get; }
        public string Description { get; }
        public QuestbookQuestItemRequirement[] RequiredItems { get; }
        public QuestbookQuestItemRequirement[] RewardItems { get; }

        public bool IsStartNode => NodeType == QuestbookQuestNodeType.Start;

        public bool UsesInfoModalLayout =>
            NodeType == QuestbookQuestNodeType.Start || NodeType == QuestbookQuestNodeType.Checkpoint;

        public bool SupportsItemIcon => NodeType == QuestbookQuestNodeType.Quest;

        public QuestbookQuestNodeDefinition(
            int id,
            double x,
            double y,
            QuestbookQuestNodeState state,
            string description = "",
            QuestbookQuestNodeType nodeType = QuestbookQuestNodeType.Quest,
            QuestbookQuestItemRequirement[]? requiredItems = null,
            QuestbookQuestItemRequirement[]? rewardItems = null)
        {
            Id = id;
            X = x;
            Y = y;
            State = state;
            Description = description;
            NodeType = nodeType;
            RequiredItems = requiredItems?.Length > 0 ? requiredItems : [];
            RewardItems = rewardItems?.Length > 0 ? rewardItems : [];
        }

        public void MarkCompleted()
        {
            State = QuestbookQuestNodeState.Completed;
        }
    }
}
