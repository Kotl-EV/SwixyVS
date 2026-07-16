using System.Text.Json;
using SwixyQuestBook.Domain.Progress;
using Vintagestory.API.Server;

namespace SwixyQuestBook.Server.Persistence
{
    /// <summary>Loads / saves per-player quest progress JSON files.</summary>
    public sealed class QuestbookProgressStore
    {
        private readonly ICoreServerAPI sapi;
        private readonly string playersDataPath;

        public string PlayersDataPath => playersDataPath;

        public QuestbookProgressStore(ICoreServerAPI sapi, string playersDataPath)
        {
            this.sapi = sapi;
            this.playersDataPath = playersDataPath;
        }

        public void EnsureDirectory() => Directory.CreateDirectory(playersDataPath);

        public Dictionary<string, QuestbookPlayerProgressData> LoadAll()
        {
            var map = new Dictionary<string, QuestbookPlayerProgressData>(StringComparer.Ordinal);
            if (!Directory.Exists(playersDataPath))
                return map;

            try
            {
                foreach (string file in Directory.GetFiles(playersDataPath, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        QuestbookPlayerProgressData? progress = JsonSerializer.Deserialize<QuestbookPlayerProgressData>(
                            json, QuestbookJson.CreateOptions());
                        if (progress == null || string.IsNullOrWhiteSpace(progress.PlayerUid))
                            continue;

                        progress.CompletedQuestsMap = (progress.CompletedQuests ?? [])
                            .GroupBy(q => $"{q.CategoryHeaderTitle}:{q.NodeId}")
                            .ToDictionary(g => g.Key, g => g.First());
                        progress.RebuildCraftProgressMap();
                        progress.RebuildKillProgressMap();
                        map[progress.PlayerUid] = progress;
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning(
                            "[SwixyQuestBook] Failed to load progress file {0}: {1}",
                            file, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[SwixyQuestBook] Failed to load player progress: {ex.Message}");
            }

            return map;
        }

        public void Save(QuestbookPlayerProgressData progress)
        {
            if (string.IsNullOrWhiteSpace(progress.PlayerUid))
                return;

            try
            {
                Directory.CreateDirectory(playersDataPath);
                string path = Path.Combine(
                    playersDataPath,
                    SanitizePlayerUidForFileName(progress.PlayerUid) + ".json");

                progress.CompletedQuests = progress.CompletedQuestsMap.Values
                    .OrderBy(e => e.CompletionOrder)
                    .ToArray();
                progress.CraftProgress = progress.CraftProgressMap.Values.ToArray();
                progress.KillProgress = progress.KillProgressMap.Values.ToArray();

                File.WriteAllText(
                    path,
                    JsonSerializer.Serialize(progress, QuestbookJson.CreateOptions(writeIndented: true)));
            }
            catch (Exception ex)
            {
                sapi.Logger.Error(
                    $"[SwixyQuestBook] Failed to save progress for {progress.PlayerUid}: {ex.Message}");
            }
        }

        public void SaveAll(IEnumerable<QuestbookPlayerProgressData> all)
        {
            foreach (QuestbookPlayerProgressData progress in all)
                Save(progress);
        }

        public static string SanitizePlayerUidForFileName(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
                return "unknown";

            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = playerUid.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c == '/' || c == '\\' || Array.IndexOf(invalid, c) >= 0)
                    chars[i] = '_';
            }

            string sanitized = new string(chars).Trim('.', ' ');
            return string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
        }
    }
}
