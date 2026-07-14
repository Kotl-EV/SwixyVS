namespace SwixyQuestBook.Gui
{
    public sealed class QuestbookQuestConnectionDefinition
    {
        public int StartNodeId { get; }
        public int EndNodeId { get; }

        public QuestbookQuestConnectionDefinition(int startNodeId, int endNodeId)
        {
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
        }
    }
}
