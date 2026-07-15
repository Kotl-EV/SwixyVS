namespace SwixyQuestBook.Gui
{
    public sealed class QuestbookCategoryDefinition
    {
        public string IconItemCode { get; }
        /// <summary>Display title for the current client language (already resolved by server).</summary>
        public string Title { get; }
        /// <summary>Stable category key used for progress and network identity.</summary>
        public string HeaderTitle { get; }
        /// <summary>Display header for the current client language (already resolved by server).</summary>
        public string HeaderDisplay { get; }
        public IReadOnlyDictionary<string, string> TitleByLang { get; }
        public IReadOnlyDictionary<string, string> HeaderByLang { get; }
        public int ProgressPercent { get; }
        public QuestbookQuestNodeDefinition[] Nodes { get; }
        public QuestbookQuestConnectionDefinition[] Connections { get; }

        public QuestbookCategoryDefinition(
            string iconItemCode,
            string title,
            string headerTitle,
            int progressPercent,
            QuestbookQuestNodeDefinition[] nodes,
            QuestbookQuestConnectionDefinition[] connections,
            string? headerDisplay = null,
            IReadOnlyDictionary<string, string>? titleByLang = null,
            IReadOnlyDictionary<string, string>? headerByLang = null)
        {
            IconItemCode = iconItemCode;
            Title = title;
            HeaderTitle = headerTitle;
            HeaderDisplay = string.IsNullOrWhiteSpace(headerDisplay) ? title : headerDisplay;
            TitleByLang = CloneLangMap(titleByLang, title);
            HeaderByLang = CloneLangMap(headerByLang, HeaderDisplay);
            ProgressPercent = progressPercent;
            Nodes = nodes;
            Connections = connections;
        }

        private static IReadOnlyDictionary<string, string> CloneLangMap(
            IReadOnlyDictionary<string, string>? source,
            string fallback)
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

            if (map.Count == 0 && !string.IsNullOrWhiteSpace(fallback))
                map["en"] = fallback;

            return map;
        }
    }
}