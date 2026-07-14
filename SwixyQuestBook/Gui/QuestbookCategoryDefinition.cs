namespace SwixyQuestBook.Gui
{
    public sealed class QuestbookCategoryDefinition
    {
        public string IconItemCode { get; }
        public string Title { get; }
        public string HeaderTitle { get; }
        public int ProgressPercent { get; }
        public QuestbookQuestNodeDefinition[] Nodes { get; }
        public QuestbookQuestConnectionDefinition[] Connections { get; }

        public QuestbookCategoryDefinition(
            string iconItemCode,
            string title,
            string headerTitle,
            int progressPercent,
            QuestbookQuestNodeDefinition[] nodes,
            QuestbookQuestConnectionDefinition[] connections)
        {
            IconItemCode = iconItemCode;
            Title = title;
            HeaderTitle = headerTitle;
            ProgressPercent = progressPercent;
            Nodes = nodes;
            Connections = connections;
        }
    }
}