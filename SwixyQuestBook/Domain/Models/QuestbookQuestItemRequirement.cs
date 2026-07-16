using SwixyQuestBook.Domain.Goals;

namespace SwixyQuestBook.Domain.Models
{
    /// <summary>
    /// Runtime goal/reward requirement used by server submit and client UI.
    /// Shared between both sides.
    /// </summary>
    public sealed class QuestbookQuestItemRequirement
    {
        public string CollectibleCode { get; }
        public int Count { get; }

        /// <summary>
        /// <c>have</c> | <c>detect</c> | <c>craft</c> | <c>craft_have</c> | <c>kill</c>.
        /// For kill, CollectibleCode is the entity code pattern.
        /// </summary>
        public string Objective { get; }

        public bool IsCraftObjective => QuestbookGoalObjective.IsCraft(Objective);
        public bool IsKillObjective => QuestbookGoalObjective.IsKill(Objective);
        public bool IsDetectObjective => QuestbookGoalObjective.IsDetect(Objective);
        public bool Consume => QuestbookGoalObjective.ShouldConsume(Objective);
        public bool RequiresInventory => QuestbookGoalObjective.NeedsInventory(Objective);

        public QuestbookQuestItemRequirement(
            string collectibleCode,
            int count,
            string objective = QuestbookGoalObjective.Have,
            bool consume = true)
        {
            CollectibleCode = collectibleCode;
            Count = count;
            Objective = QuestbookGoalObjective.Resolve(objective, consume);
        }
    }
}
