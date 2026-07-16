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
        /// <summary>
        /// False for sidebar stubs that have not received full tree data yet.
        /// </summary>
        public bool IsContentLoaded { get; }
        /// <summary>
        /// Total nodes in the branch (from server). Used for progress while content is not loaded.
        /// </summary>
        public int TotalNodeCount { get; }
        /// <summary>
        /// True when multi-language maps were delivered (admin editor). Display-only loads leave this false.
        /// </summary>
        public bool HasFullI18n { get; }

        public QuestbookCategoryDefinition(
            string iconItemCode,
            string title,
            string headerTitle,
            int progressPercent,
            QuestbookQuestNodeDefinition[] nodes,
            QuestbookQuestConnectionDefinition[] connections,
            string? headerDisplay = null,
            IReadOnlyDictionary<string, string>? titleByLang = null,
            IReadOnlyDictionary<string, string>? headerByLang = null,
            bool isContentLoaded = true,
            int totalNodeCount = -1,
            bool hasFullI18n = false)
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
            IsContentLoaded = isContentLoaded;
            TotalNodeCount = totalNodeCount >= 0
                ? totalNodeCount
                : (isContentLoaded ? nodes.Length : 0);
            HasFullI18n = hasFullI18n;
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
