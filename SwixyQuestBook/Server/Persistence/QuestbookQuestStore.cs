using System.Text.Json;
using SwixyQuestBook.Domain.Goals;
using SwixyQuestBook.Domain.Models;
using Vintagestory.API.Server;

namespace SwixyQuestBook.Server.Persistence
{
    /// <summary>
    /// Loads / saves split quest databases (manifest + branch JSON files) under ModConfig.
    /// </summary>
    public sealed class QuestbookQuestStore
    {
        public const string QuestsFolderName = "quests";
        public const string ManifestFileName = "manifest.json";
        public const string BranchesFolderName = "branches";

        private readonly ICoreServerAPI sapi;
        private readonly string questsRootPath;
        private readonly string manifestPath;
        private readonly string branchesPath;

        public string QuestsRootPath => questsRootPath;
        public string ManifestPath => manifestPath;
        public string BranchesPath => branchesPath;

        public QuestbookQuestStore(ICoreServerAPI sapi, string modDataPath)
        {
            this.sapi = sapi;
            questsRootPath = Path.Combine(modDataPath, QuestsFolderName);
            manifestPath = Path.Combine(questsRootPath, ManifestFileName);
            branchesPath = Path.Combine(questsRootPath, BranchesFolderName);
        }

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(questsRootPath);
            Directory.CreateDirectory(branchesPath);
        }

        public static string GetPackagedQuestsRoot()
        {
            string assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                ?? AppContext.BaseDirectory;
            return Path.Combine(assemblyDir, "swixyquestbook", QuestsFolderName);
        }

        public void EnsureRuntimeFromPackagedMod()
        {
            string packagedRoot = GetPackagedQuestsRoot();
            string packagedManifest = Path.Combine(packagedRoot, ManifestFileName);
            if (!File.Exists(packagedManifest))
                return;

            if (!File.Exists(manifestPath))
            {
                sapi.Logger.Notification($"[SwixyQuestBook] Copying default quests from mod: {packagedRoot}");
                CopyDirectory(packagedRoot, questsRootPath);
                return;
            }

            string? runtimeVersion = TryReadQuestVersion(manifestPath);
            string? packagedVersion = TryReadQuestVersion(packagedManifest);

            if (string.IsNullOrWhiteSpace(packagedVersion) ||
                string.Equals(runtimeVersion, packagedVersion, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                if (Directory.Exists(questsRootPath))
                    Directory.Delete(questsRootPath, recursive: true);

                Directory.CreateDirectory(questsRootPath);
                Directory.CreateDirectory(branchesPath);
                CopyDirectory(packagedRoot, questsRootPath);

                sapi.Logger.Notification(
                    $"[SwixyQuestBook] Updated quest data {runtimeVersion ?? "?"} → {packagedVersion}");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[SwixyQuestBook] Failed to update quest data from mod package: {ex.Message}");
            }
        }

        public QuestbookQuestDatabase? Load()
        {
            EnsureRuntimeFromPackagedMod();
            return TryLoadSplit(manifestPath, branchesPath);
        }

        public void Save(QuestbookQuestDatabase database)
        {
            try
            {
                Directory.CreateDirectory(questsRootPath);
                Directory.CreateDirectory(branchesPath);

                var keepFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var manifestEntries = new List<QuestbookQuestManifestEntry>(database.Categories.Length);

                foreach (QuestbookCategoryData category in database.Categories)
                {
                    string fileName = GetBranchFileName(category.HeaderTitle);
                    keepFiles.Add(fileName);
                    WriteBranchFile(category, fileName);
                    manifestEntries.Add(new QuestbookQuestManifestEntry
                    {
                        HeaderTitle = category.HeaderTitle,
                        File = fileName
                    });
                }

                foreach (string existing in Directory.GetFiles(branchesPath, "*.json"))
                {
                    string name = Path.GetFileName(existing);
                    if (!keepFiles.Contains(name))
                    {
                        try { File.Delete(existing); }
                        catch { /* best-effort */ }
                    }
                }

                var manifest = new QuestbookQuestManifest
                {
                    Version = database.Version,
                    Categories = manifestEntries.ToArray()
                };

                File.WriteAllText(
                    manifestPath,
                    JsonSerializer.Serialize(manifest, QuestbookJson.CreateOptions(writeIndented: true)));
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[SwixyQuestBook] Failed to save quest data: {ex.Message}");
            }
        }

        public void SaveCategory(QuestbookQuestDatabase database, QuestbookCategoryData category)
        {
            try
            {
                Directory.CreateDirectory(branchesPath);
                string fileName = GetBranchFileName(category.HeaderTitle);
                WriteBranchFile(category, fileName);

                var manifest = new QuestbookQuestManifest
                {
                    Version = database.Version,
                    Categories = database.Categories.Select(c => new QuestbookQuestManifestEntry
                    {
                        HeaderTitle = c.HeaderTitle,
                        File = GetBranchFileName(c.HeaderTitle)
                    }).ToArray()
                };

                File.WriteAllText(
                    manifestPath,
                    JsonSerializer.Serialize(manifest, QuestbookJson.CreateOptions(writeIndented: true)));
            }
            catch (Exception ex)
            {
                sapi.Logger.Error(
                    $"[SwixyQuestBook] Failed to save category {category.HeaderTitle}: {ex.Message}");
            }
        }

        public static bool NormalizeItemConsumeFlags(QuestbookQuestDatabase database)
        {
            bool changed = false;
            foreach (QuestbookCategoryData category in database.Categories ?? [])
            {
                foreach (QuestbookQuestNodeData node in category.Nodes ?? [])
                {
                    var items = node.RequiredItems ?? [];
                    if (items.Length == 0)
                        continue;

                    foreach (QuestbookQuestItemData item in items)
                    {
                        bool consumeHint = node.ConsumeRequiredItems && item.Consume;
                        if (QuestbookGoalObjective.IsCraft(item.Objective))
                            consumeHint = false;
                        else if (QuestbookGoalObjective.IsDetect(item.Objective))
                            consumeHint = false;
                        else if (!node.ConsumeRequiredItems)
                            consumeHint = false;

                        string resolved = QuestbookGoalObjective.Resolve(item.Objective, consumeHint);
                        bool consume = QuestbookGoalObjective.ShouldConsume(resolved);
                        if (!string.Equals(item.Objective, resolved, StringComparison.Ordinal)
                            || item.Consume != consume)
                        {
                            item.Objective = resolved;
                            item.Consume = consume;
                            changed = true;
                        }
                    }

                    bool derived = items.Any(i => QuestbookGoalObjective.ShouldConsume(i.Objective));
                    if (node.ConsumeRequiredItems != derived)
                    {
                        node.ConsumeRequiredItems = derived;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        public static string GetBranchFileName(string headerTitle)
        {
            string safe = headerTitle.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');

            if (string.IsNullOrWhiteSpace(safe))
                safe = "category";

            if (safe.Length > 120)
                safe = safe[..120];

            return safe + ".json";
        }

        public static bool TryResolveSafeBranchPath(string branchesPath, string fileName, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            string name = fileName.Trim().Replace('\\', '/');
            if (name.Contains("..", StringComparison.Ordinal)
                || name.Contains('/')
                || name.Contains(':')
                || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || name.Length > 160)
                return false;

            string root = Path.GetFullPath(branchesPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(root, name));
            string rootPrefix = root + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(Path.GetDirectoryName(candidate), root, StringComparison.OrdinalIgnoreCase))
                return false;

            fullPath = candidate;
            return true;
        }

        private QuestbookQuestDatabase? TryLoadSplit(string manifestPath, string branchesPath)
        {
            if (!File.Exists(manifestPath))
                return null;

            string manifestJson = File.ReadAllText(manifestPath);
            QuestbookQuestManifest? manifest = JsonSerializer.Deserialize<QuestbookQuestManifest>(
                manifestJson, QuestbookJson.CreateOptions());

            if (manifest?.Categories == null || manifest.Categories.Length == 0)
                return null;

            var categories = new List<QuestbookCategoryData>(manifest.Categories.Length);
            foreach (QuestbookQuestManifestEntry entry in manifest.Categories)
            {
                if (string.IsNullOrWhiteSpace(entry.File))
                    continue;

                if (!TryResolveSafeBranchPath(branchesPath, entry.File, out string branchPath))
                {
                    sapi.Logger.Warning(
                        "[SwixyQuestBook] Rejected unsafe branch path in manifest: {0}",
                        entry.File);
                    continue;
                }

                if (!File.Exists(branchPath))
                {
                    sapi.Logger.Warning($"[SwixyQuestBook] Missing branch file: {branchPath}");
                    continue;
                }

                string branchJson = File.ReadAllText(branchPath);
                QuestbookCategoryData? category = JsonSerializer.Deserialize<QuestbookCategoryData>(
                    branchJson, QuestbookJson.CreateOptions());
                if (category == null)
                    continue;

                if (string.IsNullOrWhiteSpace(category.HeaderTitle) && !string.IsNullOrWhiteSpace(entry.HeaderTitle))
                    category.HeaderTitle = entry.HeaderTitle;

                categories.Add(category);
            }

            if (categories.Count == 0)
                return null;

            return new QuestbookQuestDatabase
            {
                Version = manifest.Version ?? "1.0",
                Categories = categories.ToArray()
            };
        }

        private void WriteBranchFile(QuestbookCategoryData category, string fileName)
        {
            if (!TryResolveSafeBranchPath(branchesPath, fileName, out string path))
            {
                sapi.Logger.Error(
                    "[SwixyQuestBook] Refusing to write branch with unsafe file name: {0}",
                    fileName);
                return;
            }

            File.WriteAllText(
                path,
                JsonSerializer.Serialize(category, QuestbookJson.CreateOptions(writeIndented: true)));
        }

        private static string? TryReadQuestVersion(string path)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("version", out var versionProp))
                    return versionProp.GetString();
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string target = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, target, overwrite: true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }
}
