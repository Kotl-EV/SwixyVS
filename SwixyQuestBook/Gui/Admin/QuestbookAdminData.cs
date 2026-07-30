
namespace SwixyQuestBook.Gui
{
    public enum AdminToolMode
    {
        None,
        /// <summary>Select and drag quest nodes (does not open the editor).</summary>
        Select,
        NewQuest,
        LinkQuests,
        /// <summary>Click a quest node to open its editor.</summary>
        EditQuest,
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
        AwardCount,
        /// <summary>Repeat cooldown length in minutes (integer).</summary>
        CooldownMinutes
    }

    public readonly record struct AdminItemPickerTarget(bool IsGoals, int ListIndex);

    public readonly record struct AdminFormFieldRef(AdminFormFieldKind Kind, int ListIndex = 0)
    {
        public static AdminFormFieldRef None => default;

        public bool IsNone => Kind == AdminFormFieldKind.None;

        public bool IsCount => Kind is AdminFormFieldKind.GoalCount
            or AdminFormFieldKind.AwardCount
            or AdminFormFieldKind.CooldownMinutes;
    }

    public sealed class QuestbookAdminItemEntry
    {
        public string CollectibleCode { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool MatchAllVariants { get; set; }
        /// <summary>When true (goals only), objective is craft-detection instead of inventory have.</summary>
        public bool IsCraftObjective { get; set; }
        /// <summary>When true (goals only), objective is kill entity (code = entity type).</summary>
        public bool IsKillObjective { get; set; }
        /// <summary>When true (have/craft_have), take item from inventory on claim.</summary>
        public bool ConsumeOnComplete { get; set; } = true;

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
        /// <summary><see cref="Domain.Models.QuestbookRepeatMode"/> for the edited node.</summary>
        public string EditedRepeatMode { get; set; } = "once";
        /// <summary>Cooldown length in seconds (used when mode is cooldown).</summary>
        public int EditedCooldownSeconds { get; set; } = 60; // default 1 minute
        /// <summary>
        /// Draft text for the minutes box while typing (null = show from <see cref="EditedCooldownSeconds"/>).
        /// Allows clearing the field without snapping back to "1".
        /// </summary>
        public string? CooldownMinutesDraft { get; set; }

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

        /// <summary>Quest and Kill share the goals/rewards editor layout.</summary>
        public bool IsQuestTypeEdited =>
            EditedNodeType is QuestbookQuestNodeType.Quest or QuestbookQuestNodeType.Kill;

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
            EditedRepeatMode = "once";
            EditedCooldownSeconds = 60;
            CooldownMinutesDraft = null;
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
            EditedRepeatMode = "once";
            EditedCooldownSeconds = 60;
            CooldownMinutesDraft = null;
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
            EditedRepeatMode = node.RepeatMode ?? "once";
            EditedCooldownSeconds = node.CooldownSeconds > 0 ? node.CooldownSeconds : 60;
            CooldownMinutesDraft = null;
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
                QuestbookAdminItemEntry entry = CreateItemEntryFromSaved(item.CollectibleCode, item.Count);
                entry.IsCraftObjective = item.IsCraftObjective;
                entry.IsKillObjective = item.IsKillObjective;
                // Take only when objective is have / craft_have.
                entry.ConsumeOnComplete = item.Consume;
                Goals.Add(entry);
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

            // Kill → kill goals; Quest → craft by default.
            bool killNode = EditedNodeType == QuestbookQuestNodeType.Kill;
            Goals.Add(new QuestbookAdminItemEntry
            {
                Count = 1,
                MatchAllVariants = true,
                IsCraftObjective = !killNode,
                IsKillObjective = killNode,
                ConsumeOnComplete = false
            });
            FocusedField = AdminFormFieldRef.None;
        }

        public void AddAward()
        {
            if (Awards.Count >= MaxItemEntries)
                return;

            // Rewards always use the exact picked variant (no "all types").
            Awards.Add(new QuestbookAdminItemEntry
            {
                Count = 1,
                MatchAllVariants = false
            });
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

            if (IsQuestTypeEdited
                && string.Equals(EditedRepeatMode, Domain.Models.QuestbookRepeatMode.Cooldown, StringComparison.OrdinalIgnoreCase))
            {
                order.Add(new AdminFormFieldRef(AdminFormFieldKind.CooldownMinutes));
            }

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
                AdminFormFieldKind.CooldownMinutes =>
                    CooldownMinutesDraft ?? Domain.Models.QuestbookRepeatMode.SecondsToMinutes(EditedCooldownSeconds).ToString(),
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
                case AdminFormFieldKind.CooldownMinutes:
                    // Keep draft so the box can be empty while typing.
                    CooldownMinutesDraft = value ?? string.Empty;
                    if (int.TryParse(CooldownMinutesDraft, out int minutes)
                        && minutes >= 1
                        && minutes <= 365 * 24 * 60)
                    {
                        EditedCooldownSeconds = Domain.Models.QuestbookRepeatMode.MinutesToSeconds(minutes);
                    }
                    break;
                case AdminFormFieldKind.Information:
                    InformationText = value;
                    FlushInformationTextToLangMap();
                    break;
            }
        }

        /// <summary>Flush minutes draft into seconds (min 1) before save / apply.</summary>
        public void CommitCooldownMinutesDraft()
        {
            if (CooldownMinutesDraft == null)
                return;

            if (int.TryParse(CooldownMinutesDraft.Trim(), out int minutes) && minutes >= 1)
                EditedCooldownSeconds = Domain.Models.QuestbookRepeatMode.MinutesToSeconds(minutes);
            else if (EditedCooldownSeconds < 60)
                EditedCooldownSeconds = 60;

            CooldownMinutesDraft = null;
        }

        public void AppendToField(AdminFormFieldRef field, char c)
        {
            if (field.Kind == AdminFormFieldKind.CooldownMinutes)
            {
                if (!char.IsDigit(c))
                    return;
                string current = GetFieldValue(field);
                string newStr = current + c;
                if (newStr.Length <= 6 && int.TryParse(newStr, out int val) && val <= 525600)
                    SetFieldValue(field, newStr);
                return;
            }

            if (field.IsCount)
            {
                if (!char.IsDigit(c))
                    return;

                string current = GetFieldValue(field);
                string newStr = current == "0" ? c.ToString() : current + c;
                if (int.TryParse(newStr, out int val) && val >= 0 && val <= 9999)
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
            if (field.Kind == AdminFormFieldKind.CooldownMinutes)
            {
                string current = GetFieldValue(field);
                SetFieldValue(field, current.Length > 0 ? current[..^1] : string.Empty);
                return;
            }

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