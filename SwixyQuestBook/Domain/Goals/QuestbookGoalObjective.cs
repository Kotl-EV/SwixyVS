namespace SwixyQuestBook.Domain.Goals
{
    /// <summary>
    /// How a goal is validated. Modes are encoded in one string
    /// (so protobuf/json never drop a false bool):
    /// <list type="bullet">
    /// <item><c>have</c> — inventory + take on claim</item>
    /// <item><c>detect</c> — inventory only (no take)</item>
    /// <item><c>craft</c> — craft progress only</item>
    /// <item><c>craft_have</c> — craft progress + inventory take on claim</item>
    /// <item><c>kill</c> — kill entity progress (code = entity type, e.g. game:drifter-*)</item>
    /// </list>
    /// </summary>
    public static class QuestbookGoalObjective
    {
        public const string Have = "have";
        public const string Detect = "detect";
        public const string Craft = "craft";
        /// <summary>Craft progress required and item is taken from inventory on claim.</summary>
        public const string CraftHave = "craft_have";
        public const string Kill = "kill";

        public static string Normalize(string? value)
        {
            if (string.Equals(value, CraftHave, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "craft_turnin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "craft_submit", StringComparison.OrdinalIgnoreCase))
                return CraftHave;
            if (string.Equals(value, Craft, StringComparison.OrdinalIgnoreCase))
                return Craft;
            if (string.Equals(value, Detect, StringComparison.OrdinalIgnoreCase))
                return Detect;
            if (string.Equals(value, Kill, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "slay", StringComparison.OrdinalIgnoreCase))
                return Kill;
            return Have;
        }

        public static bool IsCraft(string? value)
        {
            string n = Normalize(value);
            return n == Craft || n == CraftHave;
        }

        public static bool IsKill(string? value) =>
            string.Equals(Normalize(value), Kill, StringComparison.Ordinal);

        public static bool IsDetect(string? value) =>
            string.Equals(Normalize(value), Detect, StringComparison.Ordinal);

        /// <summary>Item is removed from inventory on claim (<c>have</c> or <c>craft_have</c>).</summary>
        public static bool ShouldConsume(string? value)
        {
            string n = Normalize(value);
            return n == Have || n == CraftHave;
        }

        /// <summary>Must be present in inventory at turn-in (not pure craft / kill).</summary>
        public static bool NeedsInventory(string? value)
        {
            string n = Normalize(value);
            return n is Have or Detect or CraftHave;
        }

        /// <summary>Build objective from independent admin flags.</summary>
        public static string FromFlags(bool isCraft, bool isKill, bool consumeOnComplete)
        {
            if (isKill)
                return Kill;
            if (isCraft && consumeOnComplete)
                return CraftHave;
            if (isCraft)
                return Craft;
            if (consumeOnComplete)
                return Have;
            return Detect;
        }

        /// <summary>
        /// Resolve objective from stored objective + legacy consume bool.
        /// </summary>
        public static string Resolve(string? objective, bool consume)
        {
            string n = Normalize(objective);
            if (n is CraftHave or Detect or Kill)
                return n;
            if (n == Craft)
                return consume ? CraftHave : Craft;
            return consume ? Have : Detect;
        }
    }
}
