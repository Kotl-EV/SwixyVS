namespace SwixyQuestBook.Domain.Models
{
    /// <summary>
    /// How a quest behaves after a successful claim.
    /// </summary>
    public static class QuestbookRepeatMode
    {
        /// <summary>Completed forever (default).</summary>
        public const string Once = "once";

        /// <summary>Can be claimed again after <c>cooldownSeconds</c>.</summary>
        public const string Cooldown = "cooldown";

        /// <summary>Can be claimed again immediately after rewards are given.</summary>
        public const string Instant = "instant";

        public static string Normalize(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
                return Once;

            string m = mode.Trim().ToLowerInvariant();
            return m switch
            {
                Cooldown or "cd" or "repeat" or "daily" => Cooldown,
                Instant or "always" or "infinite" => Instant,
                _ => Once
            };
        }

        public static bool IsRepeatable(string? mode)
        {
            string m = Normalize(mode);
            return m is Cooldown or Instant;
        }

        /// <summary>
        /// When the player may claim again.
        /// <c>0</c> = never (once). Instant = <paramref name="completedAtMs"/> (already available).
        /// </summary>
        public static long ComputeAvailableAgainAt(string? mode, int cooldownSeconds, long completedAtMs)
        {
            string m = Normalize(mode);
            if (m == Once)
                return 0;

            if (m == Instant)
                return completedAtMs;

            int sec = System.Math.Max(60, cooldownSeconds); // min 1 minute
            return completedAtMs + (sec * 1000L);
        }

        /// <summary>
        /// True while the quest should look/act completed (blocks submit).
        /// </summary>
        public static bool IsCurrentlyCompleted(long availableAgainAt, long nowMs)
        {
            // 0 = permanent once-complete
            if (availableAgainAt == 0)
                return true;

            return nowMs < availableAgainAt;
        }

        public static int ClampCooldownSeconds(int seconds)
        {
            // 1 minute … 365 days
            return System.Math.Clamp(seconds, 60, 365 * 24 * 3600);
        }

        public static int MinutesToSeconds(int minutes) =>
            ClampCooldownSeconds(System.Math.Max(1, minutes) * 60);

        public static int SecondsToMinutes(int seconds) =>
            System.Math.Max(1, (int)System.Math.Round(seconds / 60.0));

        /// <summary>Legacy helpers (hours).</summary>
        public static int HoursToSeconds(int hours) => MinutesToSeconds(System.Math.Max(1, hours) * 60);

        public static int SecondsToHours(int seconds) =>
            System.Math.Max(1, (int)System.Math.Round(seconds / 3600.0));
    }
}
