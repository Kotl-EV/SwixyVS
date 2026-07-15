using SwixyQuestBook.Helpers;

namespace SwixyQuestBook.Gui
{
    public enum AdminToolMode
    {
        None,
        Select,
        NewQuest,
        LinkQuests,
        DeleteNode
    }

    public enum AdminEditorSection
    {
        Branches,
        Quests
    }

    public enum AdminFormFieldKind
    {
        None,
        Information,
        GoalId,
        GoalCount,
        AwardId,
        AwardCount
    }

    public readonly record struct AdminItemPickerTarget(bool IsGoals, int ListIndex);

    public readonly record struct AdminFormFieldRef(AdminFormFieldKind Kind, int ListIndex = 0)
    {
        public static AdminFormFieldRef None => default;

        public bool IsNone => Kind == AdminFormFieldKind.None;

        public bool IsCount => Kind is AdminFormFieldKind.GoalCount or AdminFormFieldKind.AwardCount;
    }

    public sealed class QuestbookAdminItemEntry
    {
        public string CollectibleCode { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool MatchAllVariants { get; set; }

        public string GetSavedCollectibleCode()
        {
            return QuestbookItemCodeHelper.GetEffectiveCollectibleCode(CollectibleCode, MatchAllVariants);
        }

        public bool CanToggleVariantMatch =>
            !string.IsNullOrWhiteSpace(CollectibleCode) && QuestbookItemCodeHelper.SupportsVariantWildcard(CollectibleCode);
    }

    public sealed class QuestbookAdminData
    {
        public const int MaxItemEntries = 64;

        public string InformationText { get; set; } = string.Empty;
        /// <summary>Active language tab in the description editor (e.g. en / ru).</summary>
        public string EditorLanguage { get; set; } = "en";
        /// <summary>Description text per language code.</summary>
        public Dictionary<string, string> InformationByLang { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<QuestbookAdminItemEntry> Goals { get; } = [];
        public List<QuestbookAdminItemEntry> Awards { get; } = [];

        public int SelectedCategoryIndex { get; set; } = -1;
        public int SelectedNodeId { get; set; } = -1;
        public QuestbookQuestNodeType EditedNodeType { get; set; } = QuestbookQuestNodeType.Quest;

        public AdminToolMode ToolMode { get; set; } = AdminToolMode.None;
        public int? LinkSourceNodeId { get; set; }

        public bool IsAdminPanelOpen { get; set; }
        public AdminEditorSection EditorSection { get; set; } = AdminEditorSection.Quests;
        public bool ShowGrid { get; set; }
        public AdminFormFieldRef FocusedField { get; set; } = AdminFormFieldRef.None;

        public QuestbookAdminData()
        {
            ClearFormFields();
        }

        public bool HasSelectedNode => SelectedNodeId >= 0;

        public bool IsQuestTypeEdited => EditedNodeType == QuestbookQuestNodeType.Quest;

        public void ResetToolState()
        {
            ToolMode = AdminToolMode.None;
            LinkSourceNodeId = null;
        }

        public void SetToolMode(AdminToolMode mode)
        {
            ToolMode = mode;
            LinkSourceNodeId = null;
        }

        public void ToggleToolMode(AdminToolMode mode)
        {
            if (ToolMode == mode)
                SetToolMode(AdminToolMode.None);
            else
                SetToolMode(mode);
        }

        public void ClearSelection()
        {
            SelectedNodeId = -1;
            EditedNodeType = QuestbookQuestNodeType.Quest;
            ClearFormFields();
        }

        public void ClearFormFields()
        {
            InformationText = string.Empty;
            InformationByLang.Clear();
            EditorLanguage = "en";
            FocusedField = AdminFormFieldRef.None;
            Goals.Clear();
            Awards.Clear();
        }

        public void ClearFields()
        {
            ResetToolState();
            ClearSelection();
        }

        public void LoadFromNode(QuestbookQuestNodeDefinition node)
        {
            SelectedNodeId = node.Id;
            EditedNodeType = node.NodeType;
            FocusedField = AdminFormFieldRef.None;
            Goals.Clear();
            Awards.Clear();
            InformationByLang.Clear();

            foreach ((string lang, string text) in node.DescriptionByLang)
            {
                if (!string.IsNullOrWhiteSpace(lang) && !string.IsNullOrWhiteSpace(text))
                    InformationByLang[lang.Trim().ToLowerInvariant()] = text;
            }

            if (InformationByLang.Count == 0 && !string.IsNullOrWhiteSpace(node.Description))
                InformationByLang["en"] = node.Description;

            if (string.IsNullOrWhiteSpace(EditorLanguage))
                EditorLanguage = "en";

            InformationText = InformationByLang.TryGetValue(EditorLanguage, out string? current)
                ? current
                : string.Empty;

            foreach (QuestbookQuestItemRequirement item in node.RequiredItems)
            {
                Goals.Add(CreateItemEntryFromSaved(item.CollectibleCode, item.Count));
            }

            foreach (QuestbookQuestItemRequirement item in node.RewardItems)
            {
                Awards.Add(CreateItemEntryFromSaved(item.CollectibleCode, item.Count));
            }
        }

        public void FlushInformationTextToLangMap()
        {
            string lang = string.IsNullOrWhiteSpace(EditorLanguage) ? "en" : EditorLanguage.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(InformationText))
                InformationByLang.Remove(lang);
            else
                InformationByLang[lang] = InformationText;
        }

        public void SwitchEditorLanguage(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
                return;

            string next = lang.Trim().ToLowerInvariant();
            FlushInformationTextToLangMap();
            EditorLanguage = next;
            InformationText = InformationByLang.TryGetValue(next, out string? text) ? text : string.Empty;
        }

        public string GetInformationTextForSave()
        {
            FlushInformationTextToLangMap();
            if (InformationByLang.TryGetValue(EditorLanguage, out string? current) && !string.IsNullOrWhiteSpace(current))
                return current;
            if (InformationByLang.TryGetValue("en", out string? en) && !string.IsNullOrWhiteSpace(en))
                return en;
            return InformationByLang.Values.FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }

        public void AddGoal()
        {
            if (Goals.Count >= MaxItemEntries)
                return;

            Goals.Add(new QuestbookAdminItemEntry());
            FocusedField = AdminFormFieldRef.None;
        }

        public void AddAward()
        {
            if (Awards.Count >= MaxItemEntries)
                return;

            Awards.Add(new QuestbookAdminItemEntry());
            FocusedField = AdminFormFieldRef.None;
        }

        public void RemoveGoal(int index)
        {
            if (index < 0 || index >= Goals.Count)
                return;

            Goals.RemoveAt(index);
            FocusedField = AdjustFocusAfterRemove(FocusedField, isGoal: true, removedIndex: index);
        }

        public void RemoveAward(int index)
        {
            if (index < 0 || index >= Awards.Count)
                return;

            Awards.RemoveAt(index);
            FocusedField = AdjustFocusAfterRemove(FocusedField, isGoal: false, removedIndex: index);
        }

        public AdminFormFieldRef GetNextFieldRef(AdminFormFieldRef current)
        {
            List<AdminFormFieldRef> order = BuildFieldOrder();
            if (order.Count == 0)
                return AdminFormFieldRef.None;

            int index = order.FindIndex(field => field == current);
            if (index < 0)
                return order[0];

            return order[(index + 1) % order.Count];
        }

        public List<AdminFormFieldRef> BuildFieldOrder()
        {
            List<AdminFormFieldRef> order = [];
            if (!IsQuestTypeEdited)
            {
                order.Add(new AdminFormFieldRef(AdminFormFieldKind.Information));
                return order;
            }

            for (int i = 0; i < Goals.Count; i++)
                order.Add(new AdminFormFieldRef(AdminFormFieldKind.GoalCount, i));

            for (int i = 0; i < Awards.Count; i++)
                order.Add(new AdminFormFieldRef(AdminFormFieldKind.AwardCount, i));

            order.Add(new AdminFormFieldRef(AdminFormFieldKind.Information));
            return order;
        }

        public string GetFieldValue(AdminFormFieldRef field)
        {
            return field.Kind switch
            {
                AdminFormFieldKind.GoalId when field.ListIndex >= 0 && field.ListIndex < Goals.Count
                    => StripIdPrefix(Goals[field.ListIndex].CollectibleCode),
                AdminFormFieldKind.GoalCount when field.ListIndex >= 0 && field.ListIndex < Goals.Count
                    => Goals[field.ListIndex].Count.ToString(),
                AdminFormFieldKind.AwardId when field.ListIndex >= 0 && field.ListIndex < Awards.Count
                    => StripIdPrefix(Awards[field.ListIndex].CollectibleCode),
                AdminFormFieldKind.AwardCount when field.ListIndex >= 0 && field.ListIndex < Awards.Count
                    => Awards[field.ListIndex].Count.ToString(),
                AdminFormFieldKind.Information => InformationText,
                _ => string.Empty
            };
        }

        public void SetFieldValue(AdminFormFieldRef field, string value)
        {
            switch (field.Kind)
            {
                case AdminFormFieldKind.GoalId when field.ListIndex >= 0 && field.ListIndex < Goals.Count:
                    Goals[field.ListIndex].CollectibleCode = EnsureIdPrefix(value);
                    break;
                case AdminFormFieldKind.GoalCount when field.ListIndex >= 0 && field.ListIndex < Goals.Count:
                    if (int.TryParse(value, out int goalCount) && goalCount >= 0 && goalCount <= 9999)
                        Goals[field.ListIndex].Count = goalCount;
                    break;
                case AdminFormFieldKind.AwardId when field.ListIndex >= 0 && field.ListIndex < Awards.Count:
                    Awards[field.ListIndex].CollectibleCode = EnsureIdPrefix(value);
                    break;
                case AdminFormFieldKind.AwardCount when field.ListIndex >= 0 && field.ListIndex < Awards.Count:
                    if (int.TryParse(value, out int awardCount) && awardCount >= 0 && awardCount <= 9999)
                        Awards[field.ListIndex].Count = awardCount;
                    break;
                case AdminFormFieldKind.Information:
                    InformationText = value;
                    FlushInformationTextToLangMap();
                    break;
            }
        }

        public void AppendToField(AdminFormFieldRef field, char c)
        {
            if (field.IsCount)
            {
                if (!char.IsDigit(c))
                    return;

                string current = GetFieldValue(field);
                string newStr = current == "0" ? c.ToString() : current + c;
                if (int.TryParse(newStr, out int val) && val >= 1 && val <= 9999)
                    SetFieldValue(field, newStr);
                return;
            }

            int maxLength = field.Kind == AdminFormFieldKind.Information
                ? (EditedNodeType == QuestbookQuestNodeType.Quest ? 165 : 624)
                : 100;
            string currentText = GetFieldValue(field);
            if (currentText.Length < maxLength)
                SetFieldValue(field, currentText + c);
        }

        public void BackspaceField(AdminFormFieldRef field)
        {
            if (field.IsCount)
            {
                string current = GetFieldValue(field);
                if (current.Length > 1)
                    SetFieldValue(field, current[..^1]);
                else
                    SetFieldValue(field, "0");
                return;
            }

            string currentText = GetFieldValue(field);
            if (currentText.Length > 0)
                SetFieldValue(field, currentText[..^1]);
        }

        private static AdminFormFieldRef AdjustFocusAfterRemove(AdminFormFieldRef focus, bool isGoal, int removedIndex)
        {
            AdminFormFieldKind idKind = isGoal ? AdminFormFieldKind.GoalId : AdminFormFieldKind.AwardId;
            AdminFormFieldKind countKind = isGoal ? AdminFormFieldKind.GoalCount : AdminFormFieldKind.AwardCount;

            if (focus.Kind is AdminFormFieldKind.None or AdminFormFieldKind.Information)
                return focus;

            bool focusIsGoal = focus.Kind is AdminFormFieldKind.GoalId or AdminFormFieldKind.GoalCount;
            bool focusIsAward = focus.Kind is AdminFormFieldKind.AwardId or AdminFormFieldKind.AwardCount;
            if (isGoal && !focusIsGoal)
                return focus;
            if (!isGoal && !focusIsAward)
                return focus;

            if (focus.ListIndex == removedIndex)
                return AdminFormFieldRef.None;

            if (focus.ListIndex > removedIndex)
                return focus with { ListIndex = focus.ListIndex - 1 };

            return focus;
        }

        private static string EnsureIdPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            value = value.Trim();
            return value.Contains(':') ? value : "game:" + value;
        }

        private static QuestbookAdminItemEntry CreateItemEntryFromSaved(string collectibleCode, int count)
        {
            bool isWildcard = QuestbookItemCodeHelper.IsVariantWildcardCode(collectibleCode);
            return new QuestbookAdminItemEntry
            {
                CollectibleCode = collectibleCode,
                Count = count,
                MatchAllVariants = isWildcard
            };
        }

        private static string StripIdPrefix(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            int colonIndex = value.IndexOf(':');
            return colonIndex >= 0 ? value[(colonIndex + 1)..] : value;
        }
    }
}