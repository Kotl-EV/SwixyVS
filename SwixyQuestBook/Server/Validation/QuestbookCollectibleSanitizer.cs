using SwixyQuestBook.Domain.Goals;
using SwixyQuestBook.Domain.Models;
using SwixyQuestBook.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SwixyQuestBook.Server.Validation
{
    /// <summary>
    /// Normalizes and validates collectible / entity codes for goals and rewards.
    /// </summary>
    public sealed class QuestbookCollectibleSanitizer
    {
        private readonly ICoreServerAPI? sapi;
        private readonly int maxItemsPerList;
        private readonly int maxItemStackCount;
        private readonly int maxCollectibleCodeLength;

        public QuestbookCollectibleSanitizer(
            ICoreServerAPI? sapi,
            int maxItemsPerList = 64,
            int maxItemStackCount = 9999,
            int maxCollectibleCodeLength = 128)
        {
            this.sapi = sapi;
            this.maxItemsPerList = maxItemsPerList;
            this.maxItemStackCount = maxItemStackCount;
            this.maxCollectibleCodeLength = maxCollectibleCodeLength;
        }

        public QuestbookQuestItemRequirement[] Sanitize(
            IEnumerable<Domain.Models.QuestbookQuestItemData>? items,
            bool allowWildcards)
        {
            if (items == null)
                return [];

            return Sanitize(
                items.Select(i => new QuestbookQuestItemRequirement(
                    i.CollectibleCode ?? string.Empty,
                    i.Count,
                    QuestbookGoalObjective.Resolve(i.Objective, i.Consume))).ToArray(),
                allowWildcards);
        }

        public QuestbookQuestItemRequirement[] Sanitize(
            IEnumerable<QuestbookQuestItemStackPacket>? items,
            bool allowWildcards)
        {
            if (items == null)
                return [];

            return Sanitize(
                items.Select(i => new QuestbookQuestItemRequirement(i.CollectibleCode ?? string.Empty, i.Count)).ToArray(),
                allowWildcards);
        }

        public QuestbookQuestItemRequirement[] Sanitize(
            IEnumerable<QuestbookSyncItemPacket>? items,
            bool allowWildcards)
        {
            if (items == null)
                return [];

            return Sanitize(
                items.Select(i => new QuestbookQuestItemRequirement(
                    i.CollectibleCode ?? string.Empty,
                    i.Count,
                    QuestbookGoalObjective.Normalize(i.Objective))).ToArray(),
                allowWildcards);
        }

        public QuestbookQuestItemRequirement[] Sanitize(
            QuestbookQuestItemRequirement[] items,
            bool allowWildcards)
        {
            var result = new List<QuestbookQuestItemRequirement>(Math.Min(items.Length, maxItemsPerList));
            foreach (var item in items)
            {
                if (result.Count >= maxItemsPerList)
                    break;

                string code = NormalizeCollectibleCode(item.CollectibleCode, allowWildcards);
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                int count = item.Count;
                if (count < 1)
                    count = 1;
                else if (count > maxItemStackCount)
                    count = maxItemStackCount;

                if (!allowWildcards && !IsValidIconItemCode(code))
                    continue;

                string objective = QuestbookGoalObjective.Normalize(item.Objective);
                result.Add(new QuestbookQuestItemRequirement(code, count, objective));
            }

            return result.ToArray();
        }

        public string NormalizeCollectibleCode(string? code, bool allowWildcards)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            string trimmed = code.Trim();
            if (trimmed.Length > maxCollectibleCodeLength)
                return string.Empty;

            if (trimmed.Contains("..", StringComparison.Ordinal)
                || trimmed.Contains('\\', StringComparison.Ordinal)
                || trimmed.Contains('\0'))
            {
                return string.Empty;
            }

            if (trimmed.Contains('*', StringComparison.Ordinal))
            {
                if (!allowWildcards || !QuestbookInventoryHelper.IsSafeWildcardPattern(trimmed))
                    return string.Empty;
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                bool ok = char.IsLetterOrDigit(c)
                    || c is ':' or '-' or '_' or '*' or '/' or '.';
                if (!ok)
                    return string.Empty;
            }

            return trimmed;
        }

        public static string NormalizeIconItemCode(string? iconItemCode)
        {
            if (string.IsNullOrWhiteSpace(iconItemCode))
                return string.Empty;
            return iconItemCode.Trim();
        }

        public bool IsValidIconItemCode(string iconItemCode)
        {
            if (sapi?.World == null || string.IsNullOrWhiteSpace(iconItemCode))
                return false;

            var location = new AssetLocation(iconItemCode);
            return sapi.World.GetItem(location) != null || sapi.World.GetBlock(location) != null;
        }
    }
}
