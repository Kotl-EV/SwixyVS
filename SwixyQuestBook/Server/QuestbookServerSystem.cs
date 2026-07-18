using SwixyQuestBook.Domain.Goals;
using SwixyQuestBook.Domain.Localization;
using SwixyQuestBook.Domain.Models;
using SwixyQuestBook.Domain.Progress;
using SwixyQuestBook.Network;
using SwixyQuestBook.Server.Crafting;
using SwixyQuestBook.Server.Persistence;
using SwixyQuestBook.Server.Sync;
using SwixyQuestBook.Server.Validation;
using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
// GamePaths.ModConfig → %AppData%/VintagestoryData/ModConfig

namespace SwixyQuestBook.Server
{
    /// <summary>
    /// Server orchestrator: network handlers, progress events, admin mutations.
    /// Persistence / validation / packet mapping live in dedicated services under
    /// <c>Persistence/</c>, <c>Validation/</c>, <c>Sync/</c>.
    /// </summary>
    public sealed partial class QuestbookServerSystem : ModSystem
    {
        private ICoreServerAPI? sapi;
        private IServerNetworkChannel? serverChannel;
        private QuestbookQuestDatabase? questDatabase;
        private Dictionary<string, QuestbookPlayerProgressData> playerProgressMap = new();
        private string modDataPath = string.Empty;
        private string questsRootPath = string.Empty;
        private string questsManifestPath = string.Empty;
        private string questsBranchesPath = string.Empty;
        private string playersDataPath = string.Empty;

        private QuestbookQuestStore? questStore;
        private QuestbookProgressStore? progressStore;
        private QuestbookCollectibleSanitizer? collectibleSanitizer;

        /// <summary>lang → stub list (no i18n). Invalidated on any content change.</summary>
        private readonly Dictionary<string, QuestbookSyncCategoryPacket[]> stubListCacheByLang =
            new(StringComparer.Ordinal);

        /// <summary>lang\0headerTitle → full category without i18n.</summary>
        private readonly Dictionary<string, QuestbookSyncCategoryPacket> fullCategoryCacheByLang =
            new(StringComparer.Ordinal);

        /// <summary>In-flight submit keys playerUid (prevents double-consume races).</summary>
        private readonly HashSet<string> submitInFlight = new(StringComparer.Ordinal);

        /// <summary>playerUid → last submit UTC ms (rate limit).</summary>
        private readonly Dictionary<string, long> lastSubmitUtcMs = new(StringComparer.Ordinal);

        /// <summary>playerUid → last category request UTC ms.</summary>
        private readonly Dictionary<string, long> lastCategoryRequestUtcMs = new(StringComparer.Ordinal);

        private const string QuestsFolderName = "quests";
        private const string QuestsManifestFileName = "manifest.json";
        private const string QuestsBranchesFolderName = "branches";
        private const string PlayersFolderName = "players";
        private const string QuestbookDataFolder = "swixyquestbook";

        /// <summary>Server privilege required for all questbook admin mutations.</summary>
        private const string AdminPrivilegeCode = "controlserver";

        private const int MaxCategoryTitleLength = 80;
        private const int MaxNodeDescriptionLength = 2000;
        private const int MaxI18nLanguages = 48;
        private const int MaxLangCodeLength = 12;
        /// <summary>
        /// Max goals/rewards per quest node. Keep in sync with
        /// <see cref="QuestbookAdminData.MaxItemEntries"/> on the client editor.
        /// </summary>
        private const int MaxItemsPerList = 64;
        private const int MaxItemStackCount = 9999;
        private const int MaxNodesPerCategory = 500;
        private const int SubmitCooldownMs = 350;
        private const int CategoryRequestCooldownMs = 80;
        private const int MaxConnectionsPerCategory = 1000;
        private const int MaxCollectibleCodeLength = 128;
        private const int MaxCategories = 64;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;

            // Server-editable quest data + player progress:
            //   %AppData%/VintagestoryData/ModConfig/swixyquestbook/
            modDataPath = Path.Combine(GamePaths.ModConfig, QuestbookDataFolder);
            questStore = new QuestbookQuestStore(api, modDataPath);
            questsRootPath = questStore.QuestsRootPath;
            questsManifestPath = questStore.ManifestPath;
            questsBranchesPath = questStore.BranchesPath;
            playersDataPath = Path.Combine(modDataPath, PlayersFolderName);
            progressStore = new QuestbookProgressStore(api, playersDataPath);
            collectibleSanitizer = new QuestbookCollectibleSanitizer(
                api, MaxItemsPerList, MaxItemStackCount, MaxCollectibleCodeLength);

            Directory.CreateDirectory(modDataPath);
            progressStore.EnsureDirectory();
            questStore.EnsureDirectories();

            serverChannel = api.Network
                .RegisterChannel(QuestbookNetworkConstants.ChannelName)
                .RegisterMessageType<QuestbookSubmitQuestRequest>()
                .RegisterMessageType<QuestbookSubmitQuestResponse>()
                .RegisterMessageType<QuestbookSyncQuestsPacket>()
                .RegisterMessageType<QuestbookSyncProgressPacket>()
                .RegisterMessageType<QuestbookRequestCategoryPacket>()
                .RegisterMessageType<QuestbookSyncCategoryUpdatePacket>()
                .RegisterMessageType<QuestbookSyncCategoryMetaPacket>()
                .RegisterMessageType<QuestbookAdminCreateNodeRequest>()
                .RegisterMessageType<QuestbookAdminDeleteLastNodeRequest>()
                .RegisterMessageType<QuestbookAdminSaveCategoryRequest>()
                .RegisterMessageType<QuestbookAdminAddCategoryRequest>()
                .RegisterMessageType<QuestbookAdminRenameCategoryRequest>()
                .RegisterMessageType<QuestbookAdminDeleteCategoryRequest>()
                .RegisterMessageType<QuestbookAdminResponse>()
                .SetMessageHandler<QuestbookSubmitQuestRequest>(OnQuestSubmitRequest)
                .SetMessageHandler<QuestbookRequestCategoryPacket>(OnRequestCategory)
                .SetMessageHandler<QuestbookAdminCreateNodeRequest>(OnAdminCreateNode)
                .SetMessageHandler<QuestbookAdminDeleteLastNodeRequest>(OnAdminDeleteLastNode)
                .SetMessageHandler<QuestbookAdminSaveCategoryRequest>(OnAdminSaveCategory)
                .SetMessageHandler<QuestbookAdminAddCategoryRequest>(OnAdminAddCategory)
                .SetMessageHandler<QuestbookAdminRenameCategoryRequest>(OnAdminRenameCategory)
                .SetMessageHandler<QuestbookAdminDeleteCategoryRequest>(OnAdminDeleteCategory);

            LoadQuestData();
            LoadAllPlayerProgress();

            try
            {
                QuestbookCraftingHook.Apply(api, OnPlayerCraftedItem);
            }
            catch (Exception ex)
            {
                sapi?.Logger.Error("[SwixyQuestBook] Failed to register craft event listener: {0}", ex.Message);
            }

            api.Event.OnEntityDeath += OnEntityDeath;
            api.Event.PlayerJoin += OnPlayerJoin;
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
        }

        public override void Dispose()
        {
            if (sapi != null)
            {
                sapi.Event.OnEntityDeath -= OnEntityDeath;
                sapi.Event.PlayerJoin -= OnPlayerJoin;
                sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;
            }

            QuestbookCraftingHook.Unapply();

            SaveAllPlayerProgress();
            playerProgressMap.Clear();
            submitInFlight.Clear();
            lastSubmitUtcMs.Clear();
            lastCategoryRequestUtcMs.Clear();
            InvalidateLocalizedPacketCache();
            questDatabase = null;
            serverChannel = null;
            sapi = null;
            base.Dispose();
        }

        #region Загрузка/Сохранение данных квестов

        private static JsonSerializerOptions CreateJsonOptions(bool writeIndented = false) =>
            QuestbookJson.CreateOptions(writeIndented);

        private void LoadQuestData()
        {
            if (questStore == null)
                return;

            try
            {
                questDatabase = questStore.Load();

                if (questDatabase?.Categories == null || questDatabase.Categories.Length == 0)
                {
                    sapi?.Logger.Warning($"[SwixyQuestBook] No quest data found at {questsManifestPath}");
                    return;
                }

                // Expand lang-key placeholders into multi-lang maps if content still uses keys.
                bool mutated = ExpandLegacyLocalizedContent(questDatabase);
                if (QuestbookQuestStore.NormalizeItemConsumeFlags(questDatabase))
                    mutated = true;

                foreach (QuestbookCategoryData category in questDatabase.Categories)
                {
                    int before = category.NextNodeId;
                    category.EnsureNextNodeIdWatermark();
                    if (category.NextNodeId != before)
                        mutated = true;
                }

                if (mutated)
                    SaveQuestData();

                sapi?.Logger.Notification(
                    $"[SwixyQuestBook] Loaded {questDatabase.Categories.Length} quest categories (version {questDatabase.Version})");
            }
            catch (Exception ex)
            {
                sapi?.Logger.Error($"[SwixyQuestBook] Failed to load quest data: {ex.Message}");
            }
        }

        private void InvalidateLocalizedPacketCache(string? headerTitle = null)
        {
            stubListCacheByLang.Clear();
            if (headerTitle == null)
            {
                fullCategoryCacheByLang.Clear();
                return;
            }

            string suffix = "\0" + headerTitle;
            List<string>? remove = null;
            foreach (string key in fullCategoryCacheByLang.Keys)
            {
                if (!key.EndsWith(suffix, StringComparison.Ordinal))
                    continue;
                remove ??= [];
                remove.Add(key);
            }

            if (remove == null)
                return;

            foreach (string key in remove)
                fullCategoryCacheByLang.Remove(key);
        }

        private static string FullCategoryCacheKey(string lang, string headerTitle) => lang + "\0" + headerTitle;

        /// <summary>Writes all branches + manifest under ModConfig.</summary>
        private void SaveQuestData()
        {
            if (questDatabase == null || questStore == null)
                return;

            InvalidateLocalizedPacketCache();
            questStore.Save(questDatabase);
        }

        /// <summary>Writes a single branch file and refreshes manifest entry list.</summary>
        private void SaveCategoryData(QuestbookCategoryData category)
        {
            if (questDatabase == null || questStore == null)
                return;

            InvalidateLocalizedPacketCache(category.HeaderTitle);
            questStore.SaveCategory(questDatabase, category);
        }

        private static string GetBranchFileName(string headerTitle) =>
            QuestbookQuestStore.GetBranchFileName(headerTitle);

        #endregion

        #region Загрузка/Сохранение прогресса игроков

        private void LoadAllPlayerProgress()
        {
            if (!Directory.Exists(playersDataPath)) return;

            try
            {
                foreach (string file in Directory.GetFiles(playersDataPath, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var progress = JsonSerializer.Deserialize<QuestbookPlayerProgressData>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (progress != null && !string.IsNullOrEmpty(progress.PlayerUid))
                        {
                            progress.CompletedQuestsMap = progress.CompletedQuests
                                .ToDictionary(e => $"{e.CategoryHeaderTitle}:{e.NodeId}");
                            progress.RebuildCraftProgressMap();
                            progress.RebuildKillProgressMap();
                            playerProgressMap[progress.PlayerUid] = progress;
                        }
                    }
                    catch (Exception ex)
                    {
                        sapi?.Logger.Warning($"[SwixyQuestBook] Failed to load progress from {file}: {ex.Message}");
                    }
                }

                // Drop completion for quest nodes that no longer exist (deleted before id fix).
                PurgeOrphanNodeProgress();

                sapi?.Logger.Notification($"[SwixyQuestBook] Loaded progress for {playerProgressMap.Count} players");
            }
            catch (Exception ex)
            {
                sapi?.Logger.Error($"[SwixyQuestBook] Failed to load player progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes completed/craft/kill entries whose node id is not present in quest data.
        /// </summary>
        private void PurgeOrphanNodeProgress()
        {
            if (questDatabase?.Categories == null || playerProgressMap.Count == 0)
                return;

            var liveByCategory = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            foreach (QuestbookCategoryData category in questDatabase.Categories)
            {
                liveByCategory[category.HeaderTitle] = category.Nodes.Select(static n => n.Id).ToHashSet();
            }

            foreach (QuestbookPlayerProgressData progress in playerProgressMap.Values)
            {
                bool changed = false;

                foreach (QuestbookCompletedQuestEntry entry in progress.CompletedQuestsMap.Values.ToArray())
                {
                    if (liveByCategory.TryGetValue(entry.CategoryHeaderTitle, out HashSet<int>? live)
                        && live.Contains(entry.NodeId))
                        continue;

                    if (progress.ClearAllProgressForNode(entry.CategoryHeaderTitle, entry.NodeId))
                        changed = true;
                }

                // Craft/kill orphans without a completed entry.
                foreach (QuestbookCraftProgressEntry entry in progress.CraftProgressMap.Values.ToArray())
                {
                    if (liveByCategory.TryGetValue(entry.CategoryHeaderTitle, out HashSet<int>? live)
                        && live.Contains(entry.NodeId))
                        continue;
                    if (progress.ClearAllProgressForNode(entry.CategoryHeaderTitle, entry.NodeId))
                        changed = true;
                }

                foreach (QuestbookCraftProgressEntry entry in progress.KillProgressMap.Values.ToArray())
                {
                    if (liveByCategory.TryGetValue(entry.CategoryHeaderTitle, out HashSet<int>? live)
                        && live.Contains(entry.NodeId))
                        continue;
                    if (progress.ClearAllProgressForNode(entry.CategoryHeaderTitle, entry.NodeId))
                        changed = true;
                }

                if (changed)
                    SavePlayerProgress(progress);
            }
        }

        private void SaveAllPlayerProgress()
        {
            if (progressStore == null)
                return;
            progressStore.SaveAll(playerProgressMap.Values);
        }

        private void SavePlayerProgress(QuestbookPlayerProgressData progress)
        {
            progressStore?.Save(progress);
        }

        /// <summary>
        /// Player UIDs may contain '/' or other path-illegal characters (session-style IDs).
        /// </summary>
        private static string SanitizePlayerUidForFileName(string playerUid) =>
            QuestbookProgressStore.SanitizePlayerUidForFileName(playerUid);

        private QuestbookPlayerProgressData GetOrCreatePlayerProgress(IServerPlayer player)
        {
            if (!playerProgressMap.TryGetValue(player.PlayerUID, out var progress))
            {
                progress = new QuestbookPlayerProgressData(player.PlayerUID, player.PlayerName);
                playerProgressMap[player.PlayerUID] = progress;
            }
            else
            {
                progress.PlayerName = player.PlayerName;
                progress.UpdateLastPlayed();
            }
            return progress;
        }

        #endregion

        #region События игроков

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            var progress = GetOrCreatePlayerProgress(byPlayer);
            SendQuestsToPlayer(byPlayer);
            SendProgressToPlayer(byPlayer, progress);
            sapi?.Logger.Debug($"[SwixyQuestBook] Player {byPlayer.PlayerName} joined");
        }

        private void OnPlayerDisconnect(IServerPlayer byPlayer)
        {
            if (playerProgressMap.TryGetValue(byPlayer.PlayerUID, out var progress))
            {
                SavePlayerProgress(progress);
                sapi?.Logger.Debug($"[SwixyQuestBook] Saved progress for {byPlayer.PlayerName}");
            }
        }

        #endregion

        #region Отправка данных клиентам

        /// <summary>
        /// Join / list refresh: send lightweight stubs (no nodes, no i18n). Full trees are
        /// requested per branch via <see cref="OnRequestCategory"/>.
        /// </summary>
        private void SendQuestsToPlayer(IServerPlayer player)
        {
            if (questDatabase == null || serverChannel == null) return;

            string lang = GetPlayerLanguageCode(player);
            var packet = new QuestbookSyncQuestsPacket
            {
                Categories = GetOrBuildStubList(lang)
            };

            serverChannel.SendPacket(packet, player);
            sapi?.Logger.Debug(
                $"[SwixyQuestBook] Sent {questDatabase.Categories.Length} category stubs to {player.PlayerName} (lang={lang})");
        }

        private QuestbookSyncCategoryPacket[] GetOrBuildStubList(string lang)
        {
            if (questDatabase == null)
                return [];

            if (stubListCacheByLang.TryGetValue(lang, out QuestbookSyncCategoryPacket[]? cached))
                return cached;

            QuestbookSyncCategoryPacket[] built = questDatabase.Categories
                .Select(c => BuildLocalizedCategoryPacket(c, lang, includeAllLanguages: false, stubOnly: true))
                .ToArray();
            stubListCacheByLang[lang] = built;
            return built;
        }

        private void OnRequestCategory(IServerPlayer fromPlayer, QuestbookRequestCategoryPacket request)
        {
            if (questDatabase == null || serverChannel == null || fromPlayer == null)
                return;

            if (request == null)
                return;

            if (!TryConsumeRateLimit(lastCategoryRequestUtcMs, fromPlayer.PlayerUID, CategoryRequestCooldownMs))
                return;

            string headerTitle = request.HeaderTitle?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(headerTitle) || headerTitle.Length > MaxCategoryTitleLength)
                return;

            QuestbookCategoryData? category = questDatabase.Categories.FirstOrDefault(c =>
                string.Equals(c.HeaderTitle, headerTitle, StringComparison.Ordinal));
            if (category == null)
            {
                sapi?.Logger.Warning(
                    "[SwixyQuestBook] Category request miss '{0}' from {1}",
                    headerTitle, fromPlayer.PlayerName);
                return;
            }

            bool includeI18n = request.IncludeI18n && IsQuestbookAdmin(fromPlayer);
            SendCategoryToPlayer(fromPlayer, category, includeI18n);
        }

        private void SendCategoryToPlayer(
            IServerPlayer player,
            QuestbookCategoryData category,
            bool includeI18n = false)
        {
            if (serverChannel == null) return;

            string lang = GetPlayerLanguageCode(player);
            QuestbookSyncCategoryPacket categoryPacket = GetOrBuildFullCategoryPacket(category, lang, includeI18n);
            var packet = new QuestbookSyncCategoryUpdatePacket { Category = categoryPacket };
            serverChannel.SendPacket(packet, player);
            sapi?.Logger.Debug(
                $"[SwixyQuestBook] Sent category '{category.HeaderTitle}' to {player.PlayerName} ({category.Nodes.Length} nodes, lang={lang}, i18n={includeI18n})");
        }

        private QuestbookSyncCategoryPacket GetOrBuildFullCategoryPacket(
            QuestbookCategoryData category,
            string lang,
            bool includeI18n)
        {
            // i18n payloads are rare (admin editor) — always build fresh, never cache.
            if (includeI18n)
                return BuildLocalizedCategoryPacket(category, lang, includeAllLanguages: true, stubOnly: false);

            string key = FullCategoryCacheKey(lang, category.HeaderTitle);
            if (fullCategoryCacheByLang.TryGetValue(key, out QuestbookSyncCategoryPacket? cached))
                return cached;

            QuestbookSyncCategoryPacket built =
                BuildLocalizedCategoryPacket(category, lang, includeAllLanguages: false, stubOnly: false);
            fullCategoryCacheByLang[key] = built;
            return built;
        }

        private void BroadcastCategoryToAllPlayers(string headerTitle)
        {
            if (questDatabase == null || serverChannel == null || sapi?.World == null) return;

            QuestbookCategoryData? category = questDatabase.Categories.FirstOrDefault(c =>
                string.Equals(c.HeaderTitle, headerTitle, StringComparison.Ordinal));
            if (category == null) return;

            foreach (var player in sapi.World.AllPlayers)
            {
                if (player is not IServerPlayer serverPlayer)
                    continue;

                // Players get display-only; admins get full i18n so open editors stay editable.
                SendCategoryToPlayer(serverPlayer, category, includeI18n: IsQuestbookAdmin(serverPlayer));
            }
        }

        private void BroadcastCategoryMetaToAllPlayers(QuestbookCategoryData category, bool isNew = false)
        {
            if (serverChannel == null || sapi?.World == null) return;

            foreach (var player in sapi.World.AllPlayers)
            {
                if (player is not IServerPlayer serverPlayer)
                    continue;

                string lang = GetPlayerLanguageCode(serverPlayer);
                string title = category.Title?.Resolve(lang) ?? string.Empty;
                string headerDisplay = category.Header?.Resolve(lang) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(headerDisplay))
                    headerDisplay = title;

                serverChannel.SendPacket(new QuestbookSyncCategoryMetaPacket
                {
                    HeaderTitle = category.HeaderTitle,
                    Title = title,
                    HeaderDisplay = headerDisplay,
                    IconItemCode = category.IconItemCode ?? string.Empty,
                    TotalNodeCount = category.Nodes?.Length ?? 0,
                    Removed = false,
                    IsNew = isNew
                }, serverPlayer);
            }
        }

        private void BroadcastCategoryRemovedToAllPlayers(string headerTitle)
        {
            if (serverChannel == null || sapi?.World == null) return;

            foreach (var player in sapi.World.AllPlayers)
            {
                if (player is not IServerPlayer serverPlayer)
                    continue;

                serverChannel.SendPacket(new QuestbookSyncCategoryMetaPacket
                {
                    HeaderTitle = headerTitle,
                    Removed = true
                }, serverPlayer);
            }
        }

        private static bool IsQuestbookAdmin(IServerPlayer player)
        {
            return player.HasPrivilege(AdminPrivilegeCode) || player.HasPrivilege(Privilege.controlserver);
        }

        private static string GetPlayerLanguageCode(IServerPlayer player)
        {
            try
            {
                // VS exposes the client's UI language on the server player.
                string? code = player.LanguageCode;
                if (!string.IsNullOrWhiteSpace(code))
                {
                    return QuestbookLocalizedText.NormalizeLang(code);
                }
            }
            catch
            {
                // Older API shapes — fall through.
            }

            return QuestbookLocalizedText.DefaultLang;
        }

        private static QuestbookSyncCategoryPacket BuildLocalizedCategoryPacket(
            QuestbookCategoryData category,
            string lang,
            bool includeAllLanguages,
            bool stubOnly) =>
            QuestbookCategoryPacketBuilder.Build(category, lang, includeAllLanguages, stubOnly);

        private static QuestbookLangTextPacket[] ToLangPackets(QuestbookLocalizedText? text) =>
            QuestbookCategoryPacketBuilder.ToLangPackets(text);

        private static QuestbookLocalizedText FromLangPackets(QuestbookLangTextPacket[]? entries)
        {
            var text = new QuestbookLocalizedText();
            if (entries == null)
                return text;

            int accepted = 0;
            foreach (QuestbookLangTextPacket entry in entries)
            {
                if (accepted >= MaxI18nLanguages)
                    break;

                string lang = QuestbookLocalizedText.NormalizeLang(entry.Lang);
                if (string.IsNullOrWhiteSpace(lang) || lang.Length > MaxLangCodeLength)
                    continue;

                // Only basic language tags: letters and optional hyphen (already stripped by Normalize).
                bool langOk = true;
                foreach (char c in lang)
                {
                    if (!char.IsLetter(c))
                    {
                        langOk = false;
                        break;
                    }
                }

                if (!langOk)
                    continue;

                string value = SanitizeDescription(entry.Text);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                text.Set(lang, value);
                accepted++;
            }

            return text;
        }

        private void SendProgressToPlayer(
            IServerPlayer player,
            QuestbookPlayerProgressData progress,
            bool fullSync = true)
        {
            if (serverChannel == null) return;

            var packet = new QuestbookSyncProgressPacket
            {
                TotalQuestsCompleted = progress.TotalQuestsCompleted,
                IsFullSync = fullSync,
                CompletedQuests = progress.CompletedQuests.Select(q => new QuestbookSyncCompletedQuestPacket
                {
                    CategoryHeaderTitle = q.CategoryHeaderTitle,
                    NodeId = q.NodeId,
                    CompletedAt = q.CompletedAt,
                    CompletionOrder = q.CompletionOrder
                }).ToArray(),
                CraftProgress = progress.CraftProgress.Select(c => new QuestbookSyncCraftProgressPacket
                {
                    CategoryHeaderTitle = c.CategoryHeaderTitle,
                    NodeId = c.NodeId,
                    CollectibleCode = c.CollectibleCode,
                    Count = c.Count
                }).ToArray(),
                KillProgress = progress.KillProgress.Select(c => new QuestbookSyncCraftProgressPacket
                {
                    CategoryHeaderTitle = c.CategoryHeaderTitle,
                    NodeId = c.NodeId,
                    CollectibleCode = c.CollectibleCode,
                    Count = c.Count
                }).ToArray()
            };

            serverChannel.SendPacket(packet, player);
        }

        private void SendProgressDelta(
            IServerPlayer player,
            QuestbookPlayerProgressData progress,
            QuestbookCompletedQuestEntry entry)
        {
            if (serverChannel == null) return;

            // Include full craft/kill maps so client drops progress for the completed node.
            serverChannel.SendPacket(new QuestbookSyncProgressPacket
            {
                TotalQuestsCompleted = progress.TotalQuestsCompleted,
                IsFullSync = false,
                CompletedQuests =
                [
                    new QuestbookSyncCompletedQuestPacket
                    {
                        CategoryHeaderTitle = entry.CategoryHeaderTitle,
                        NodeId = entry.NodeId,
                        CompletedAt = entry.CompletedAt,
                        CompletionOrder = entry.CompletionOrder
                    }
                ],
                CraftProgress = progress.CraftProgress.Select(c => new QuestbookSyncCraftProgressPacket
                {
                    CategoryHeaderTitle = c.CategoryHeaderTitle,
                    NodeId = c.NodeId,
                    CollectibleCode = c.CollectibleCode,
                    Count = c.Count
                }).ToArray(),
                KillProgress = progress.KillProgress.Select(c => new QuestbookSyncCraftProgressPacket
                {
                    CategoryHeaderTitle = c.CategoryHeaderTitle,
                    NodeId = c.NodeId,
                    CollectibleCode = c.CollectibleCode,
                    Count = c.Count
                }).ToArray()
            }, player);
        }

        /// <summary>Called from event bus <c>onitemcrafted</c> when a player creates an item by crafting.</summary>
        private void OnPlayerCraftedItem(IPlayer player, string collectibleCode, int quantity)
        {
            if (questDatabase == null || player is not IServerPlayer serverPlayer)
                return;

            if (quantity <= 0 || string.IsNullOrWhiteSpace(collectibleCode))
                return;

            var progress = GetOrCreatePlayerProgress(serverPlayer);
            bool changed = false;

            foreach (QuestbookCategoryData category in questDatabase.Categories)
            {
                foreach (QuestbookQuestNodeData node in category.Nodes)
                {
                    if (progress.IsQuestCompleted(category.HeaderTitle, node.Id))
                        continue;

                    if (!IsNodeUnlockedForPlayer(category, node, progress))
                        continue;

                    foreach (QuestbookQuestItemData req in node.RequiredItems ?? [])
                    {
                        if (!QuestbookGoalObjective.IsCraft(req.Objective))
                            continue;

                        if (!QuestbookInventoryHelper.MatchesCollectibleCode(collectibleCode, req.CollectibleCode))
                            continue;

                        // Sum all crafted stacks matching this goal pattern (exact or wildcard).
                        int have = CountCraftProgressMatching(
                            progress, category.HeaderTitle, node.Id, req.CollectibleCode);
                        if (have >= req.Count)
                            continue;

                        int add = Math.Min(quantity, req.Count - have);
                        // Store the real crafted code (not the pattern).
                        progress.AddCraftCount(category.HeaderTitle, node.Id, collectibleCode, add);
                        changed = true;
                    }
                }
            }

            if (!changed)
            {
                sapi?.Logger.VerboseDebug(
                    "[SwixyQuestBook] Craft ignored (no matching open craft-goal): {0} x{1} by {2}",
                    collectibleCode, quantity, serverPlayer.PlayerName);
                return;
            }

            SavePlayerProgress(progress);
            SendProgressToPlayer(serverPlayer, progress, fullSync: true);
            sapi?.Logger.Notification(
                "[SwixyQuestBook] Craft progress +{0} {1} for {2}",
                quantity, collectibleCode, serverPlayer.PlayerName);
        }

        /// <summary>Entity died — if a player caused it, count toward open kill goals.</summary>
        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (questDatabase == null || entity == null || sapi == null)
                return;

            // Do not count player deaths as creature kills.
            if (entity is EntityPlayer)
                return;

            string? entityCode = entity.Code?.ToString();
            if (string.IsNullOrWhiteSpace(entityCode))
                return;

            Entity? cause = damageSource?.GetCauseEntity() ?? damageSource?.SourceEntity;
            if (cause is not EntityPlayer killerEp || killerEp.Player is not IServerPlayer serverPlayer)
                return;

            OnPlayerKilledEntity(serverPlayer, entityCode);
        }

        private void OnPlayerKilledEntity(IServerPlayer serverPlayer, string entityCode)
        {
            if (questDatabase == null)
                return;

            var progress = GetOrCreatePlayerProgress(serverPlayer);
            bool changed = false;

            foreach (QuestbookCategoryData category in questDatabase.Categories)
            {
                foreach (QuestbookQuestNodeData node in category.Nodes)
                {
                    if (progress.IsQuestCompleted(category.HeaderTitle, node.Id))
                        continue;

                    if (!IsNodeUnlockedForPlayer(category, node, progress))
                        continue;

                    foreach (QuestbookQuestItemData req in node.RequiredItems ?? [])
                    {
                        if (!QuestbookGoalObjective.IsKill(req.Objective))
                            continue;

                        if (!QuestbookInventoryHelper.MatchesCollectibleCode(entityCode, req.CollectibleCode))
                            continue;

                        int have = CountKillProgressMatching(
                            progress, category.HeaderTitle, node.Id, req.CollectibleCode);
                        if (have >= req.Count)
                            continue;

                        progress.AddKillCount(category.HeaderTitle, node.Id, entityCode, 1);
                        changed = true;
                    }
                }
            }

            if (!changed)
            {
                sapi?.Logger.VerboseDebug(
                    "[SwixyQuestBook] Kill ignored (no matching open kill-goal): {0} by {1}",
                    entityCode, serverPlayer.PlayerName);
                return;
            }

            SavePlayerProgress(progress);
            SendProgressToPlayer(serverPlayer, progress, fullSync: true);
            sapi?.Logger.Notification(
                "[SwixyQuestBook] Kill progress +1 {0} for {1}",
                entityCode, serverPlayer.PlayerName);
        }

        private void BroadcastProgressToAllPlayers()
        {
            if (serverChannel == null || sapi?.World == null)
                return;

            foreach (var player in sapi.World.AllPlayers)
            {
                if (player is not IServerPlayer serverPlayer)
                    continue;

                if (playerProgressMap.TryGetValue(serverPlayer.PlayerUID, out var progress))
                    SendProgressToPlayer(serverPlayer, progress, fullSync: true);
            }
        }

        private static int GetNodeTypeInt(string nodeType) =>
            QuestbookCategoryPacketBuilder.GetNodeTypeInt(nodeType);

        #endregion

        #region Обработка запросов от клиентов

        private void OnQuestSubmitRequest(IServerPlayer fromPlayer, QuestbookSubmitQuestRequest request)
        {
            if (questDatabase == null || fromPlayer == null)
            {
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            string playerUid = fromPlayer.PlayerUID ?? string.Empty;
            if (string.IsNullOrEmpty(playerUid))
            {
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            if (!TryConsumeRateLimit(lastSubmitUtcMs, playerUid, SubmitCooldownMs))
            {
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            // Single-flight: block concurrent submits from the same player (double-consume race).
            lock (submitInFlight)
            {
                if (!submitInFlight.Add(playerUid))
                {
                    SendQuestSubmitResponse(fromPlayer, request, false);
                    return;
                }
            }

            try
            {
                ProcessQuestSubmit(fromPlayer, request);
            }
            finally
            {
                lock (submitInFlight)
                {
                    submitInFlight.Remove(playerUid);
                }
            }
        }

        private void ProcessQuestSubmit(IServerPlayer fromPlayer, QuestbookSubmitQuestRequest request)
        {
            // Client payload is untrusted: only category + nodeId are used for lookup.
            string categoryHeader = request.CategoryHeaderTitle?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(categoryHeader)
                || categoryHeader.Length > MaxCategoryTitleLength
                || request.NodeId < 0)
            {
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            var category = questDatabase!.Categories.FirstOrDefault(c =>
                string.Equals(c.HeaderTitle, categoryHeader, StringComparison.Ordinal));
            if (category == null)
            {
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            var node = category.Nodes.FirstOrDefault(n => n.Id == request.NodeId);
            if (node == null)
            {
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            var progress = GetOrCreatePlayerProgress(fromPlayer);
            if (progress.IsQuestCompleted(category.HeaderTitle, node.Id))
            {
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            if (!IsNodeUnlockedForPlayer(category, node, progress))
            {
                sapi?.Logger.Warning(
                    "[SwixyQuestBook] Rejected submit from {0}: node {1}:{2} locked",
                    fromPlayer.PlayerName, category.HeaderTitle, node.Id);
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            // Authoritative requirements/rewards from server database — never from client packet.
            var requiredItems = SanitizeItemList(node.RequiredItems, allowWildcards: true);
            var rewardItems = SanitizeItemList(node.RewardItems, allowWildcards: false);

            string nodeType = (node.NodeType ?? "Quest").Trim();
            bool isInfoNode = string.Equals(nodeType, "Start", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nodeType, "Checkpoint", StringComparison.OrdinalIgnoreCase);

            if (!isInfoNode && requiredItems.Length == 0)
            {
                // Regular quests must define goals server-side.
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            // Craft axis: real craft progress (craft / craft_have).
            var craftGoals = requiredItems.Where(static i => i.IsCraftObjective).ToArray();
            // Kill axis: entity kill progress.
            var killGoals = requiredItems.Where(static i => i.IsKillObjective).ToArray();
            // Inventory axis: must be in bags at turn-in (have / detect / craft_have).
            var inventoryGoals = requiredItems.Where(static i => i.RequiresInventory).ToArray();

            foreach (QuestbookQuestItemRequirement craftGoal in craftGoals)
            {
                int crafted = CountCraftProgressMatching(
                    progress, category.HeaderTitle, node.Id, craftGoal.CollectibleCode);

                if (crafted < craftGoal.Count)
                {
                    sapi?.Logger.Debug(
                        "[SwixyQuestBook] Craft goal not met for {0}: {1}:{2} need {3}x {4} have {5}",
                        fromPlayer.PlayerName, category.HeaderTitle, node.Id,
                        craftGoal.Count, craftGoal.CollectibleCode, crafted);
                    SendQuestSubmitResponse(fromPlayer, request, false);
                    return;
                }
            }

            foreach (QuestbookQuestItemRequirement killGoal in killGoals)
            {
                int killed = CountKillProgressMatching(
                    progress, category.HeaderTitle, node.Id, killGoal.CollectibleCode);

                if (killed < killGoal.Count)
                {
                    sapi?.Logger.Debug(
                        "[SwixyQuestBook] Kill goal not met for {0}: {1}:{2} need {3}x {4} have {5}",
                        fromPlayer.PlayerName, category.HeaderTitle, node.Id,
                        killGoal.Count, killGoal.CollectibleCode, killed);
                    SendQuestSubmitResponse(fromPlayer, request, false);
                    return;
                }
            }

            bool success;
            if (isInfoNode && requiredItems.Length == 0 && rewardItems.Length == 0)
            {
                success = true;
            }
            else
            {
                // Inventory goals: detect = check only; have/craft_have = check + take.
                // Craft-only / kill goals already validated above.
                success = QuestbookInventoryHelper.TryCompleteQuestExchange(
                    fromPlayer,
                    inventoryGoals,
                    rewardItems,
                    out string? failReason);

                if (!success)
                {
                    sapi?.Logger.Debug(
                        "[SwixyQuestBook] Submit inventory fail for {0} on {1}:{2} ({3})",
                        fromPlayer.PlayerName, category.HeaderTitle, node.Id, failReason ?? "?");
                }
                else if (inventoryGoals.Any(static i => i.Consume) || rewardItems.Length > 0)
                {
                    fromPlayer.InventoryManager.BroadcastHotbarSlot();
                }
            }

            if (success)
            {
                sapi?.Logger.Debug(
                    "[SwixyQuestBook] Quest completed by {0}: {1}:{2}",
                    fromPlayer.PlayerName, category.HeaderTitle, node.Id);
                progress.AddCompletedQuest(category.HeaderTitle, node.Id);
                SavePlayerProgress(progress);

                string progressKey = $"{category.HeaderTitle}:{node.Id}";
                if (progress.CompletedQuestsMap.TryGetValue(progressKey, out QuestbookCompletedQuestEntry? entry))
                    SendProgressDelta(fromPlayer, progress, entry);
                else
                    SendProgressToPlayer(fromPlayer, progress, fullSync: true);
            }

            SendQuestSubmitResponse(fromPlayer, request, success);
        }

        private static bool TryConsumeRateLimit(
            Dictionary<string, long> map,
            string key,
            int cooldownMs)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            lock (map)
            {
                if (map.TryGetValue(key, out long last) && now - last < cooldownMs)
                    return false;

                map[key] = now;
                return true;
            }
        }

        /// <summary>
        /// Sum craft progress for a node whose stored codes match a (possibly wildcard) goal pattern.
        /// Exact key lookup is preferred; this handles pattern goals like <c>game:axe-*</c>.
        /// </summary>
        private static int CountCraftProgressMatching(
            QuestbookPlayerProgressData progress,
            string categoryHeaderTitle,
            int nodeId,
            string pattern)
        {
            int total = 0;
            string prefix = $"{categoryHeaderTitle}:{nodeId}:".ToLowerInvariant();
            foreach ((string key, QuestbookCraftProgressEntry entry) in progress.CraftProgressMap)
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                if (QuestbookInventoryHelper.MatchesCollectibleCode(entry.CollectibleCode, pattern))
                    total += entry.Count;
            }

            return total;
        }

        private static int CountKillProgressMatching(
            QuestbookPlayerProgressData progress,
            string categoryHeaderTitle,
            int nodeId,
            string pattern)
        {
            int total = 0;
            string prefix = $"{categoryHeaderTitle}:{nodeId}:".ToLowerInvariant();
            foreach ((string key, QuestbookCraftProgressEntry entry) in progress.KillProgressMap)
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                if (QuestbookInventoryHelper.MatchesCollectibleCode(entry.CollectibleCode, pattern))
                    total += entry.Count;
            }

            return total;
        }

        private void SendQuestSubmitResponse(IServerPlayer? player, QuestbookSubmitQuestRequest request, bool success)
        {
            if (player == null || serverChannel == null)
            {
                return;
            }

            serverChannel.SendPacket(
                new QuestbookSubmitQuestResponse
                {
                    CategoryHeaderTitle = request.CategoryHeaderTitle ?? string.Empty,
                    NodeId = request.NodeId,
                    Success = success
                },
                player
            );
        }

        private static bool IsNodeUnlockedForPlayer(
            QuestbookCategoryData category,
            QuestbookQuestNodeData node,
            QuestbookPlayerProgressData progress)
        {
            foreach (var connection in category.Connections)
            {
                if (connection.EndNodeId != node.Id)
                {
                    continue;
                }

                if (!progress.IsQuestCompleted(category.HeaderTitle, connection.StartNodeId))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Обработка админ-запросов

        private bool EnsureAdminAuthorized(IServerPlayer fromPlayer, string action)
        {
            if (fromPlayer == null)
            {
                return false;
            }

            if (IsQuestbookAdmin(fromPlayer))
            {
                return true;
            }

            sapi?.Logger.Warning(
                "[SwixyQuestBook] Unauthorized admin packet '{0}' from {1} ({2})",
                action, fromPlayer.PlayerName, fromPlayer.PlayerUID);
            SendAdminResponse(fromPlayer, false, "No permission");
            return false;
        }

        private void OnAdminCreateNode(IServerPlayer fromPlayer, QuestbookAdminCreateNodeRequest request)
        {
            if (!EnsureAdminAuthorized(fromPlayer, "CreateNode"))
            {
                return;
            }

            if (questDatabase == null)
            {
                SendAdminResponse(fromPlayer, false, "Quest database not loaded");
                return;
            }

            var category = questDatabase.Categories.FirstOrDefault(c =>
                string.Equals(c.HeaderTitle, request.CategoryHeaderTitle?.Trim(), StringComparison.Ordinal));
            if (category == null)
            {
                SendAdminResponse(fromPlayer, false, "Category not found");
                return;
            }

            if (category.Nodes.Length >= MaxNodesPerCategory)
            {
                SendAdminResponse(fromPlayer, false, "Category node limit reached");
                return;
            }

            string nodeType = NormalizeNodeTypeName(request.NodeType);
            if (nodeType == null)
            {
                SendAdminResponse(fromPlayer, false, "Invalid node type");
                return;
            }

            var parentNode = category.Nodes.FirstOrDefault(n => n.Id == request.ParentNodeId);
            if (parentNode == null && nodeType != "Start")
            {
                SendAdminResponse(fromPlayer, false, "Parent node not found");
                return;
            }

            if (nodeType == "Start" && category.Nodes.Any(n =>
                    string.Equals(n.NodeType, "Start", StringComparison.OrdinalIgnoreCase)))
            {
                SendAdminResponse(fromPlayer, false, "Category already has a Start node");
                return;
            }

            string lang = GetPlayerLanguageCode(fromPlayer);
            string description = SanitizeDescription(request.Description);
            var requiredItems = SanitizeItemList(request.RequiredItems, allowWildcards: true);
            var rewardItems = SanitizeItemList(request.RewardItems, allowWildcards: false);

            if (nodeType is "Start" or "Checkpoint")
            {
                requiredItems = [];
                rewardItems = [];
            }

            // Never reuse ids — progress is stored as category:nodeId.
            int newId = category.AllocateNodeId();

            double x = 0, y = 0;
            if (parentNode != null)
            {
                string direction = NormalizeDirection(request.Direction);
                (x, y) = CalculateNodePosition(parentNode, direction, nodeType,
                    request.IsSubQuest, request.SubQuestIndex, request.TotalSubQuests);
            }

            var descriptionText = new QuestbookLocalizedText();
            if (!string.IsNullOrWhiteSpace(description))
            {
                descriptionText.Set(lang, description);
            }

            var newNode = new QuestbookQuestNodeData
            {
                Id = newId,
                X = x,
                Y = y,
                NodeType = nodeType,
                Description = descriptionText,
                RequiredItems = requiredItems.Select(i => new QuestbookQuestItemData(
                    i.CollectibleCode, i.Count, i.Objective, i.Consume)).ToArray(),
                RewardItems = rewardItems.Select(i => new QuestbookQuestItemData(
                    i.CollectibleCode, i.Count, QuestbookGoalObjective.Have)).ToArray(),
                ConsumeRequiredItems = requiredItems.Any(static i => i.Consume)
            };

            var nodesList = category.Nodes.ToList();
            nodesList.Add(newNode);
            category.Nodes = nodesList.ToArray();

            if (parentNode != null)
            {
                if (category.Connections.Length >= MaxConnectionsPerCategory)
                {
                    // Roll back node add if connection cannot be stored.
                    category.Nodes = category.Nodes.Where(n => n.Id != newId).ToArray();
                    SendAdminResponse(fromPlayer, false, "Category connection limit reached");
                    return;
                }

                var connectionsList = category.Connections.ToList();
                connectionsList.Add(new QuestbookQuestConnectionData(parentNode.Id, newId));
                category.Connections = connectionsList.ToArray();
            }

            SaveCategoryData(category);
            BroadcastCategoryToAllPlayers(category.HeaderTitle);

            SendAdminResponse(fromPlayer, true, $"Node {newId} created");
            sapi?.Logger.Notification(
                "[SwixyQuestBook] Admin {0} created node {1} in {2}",
                fromPlayer.PlayerName, newId, category.HeaderTitle);
        }

        private void OnAdminDeleteLastNode(IServerPlayer fromPlayer, QuestbookAdminDeleteLastNodeRequest request)
        {
            if (!EnsureAdminAuthorized(fromPlayer, "DeleteLastNode"))
            {
                return;
            }

            if (questDatabase == null)
            {
                SendAdminResponse(fromPlayer, false, "Quest database not loaded");
                return;
            }

            var category = questDatabase.Categories.FirstOrDefault(c =>
                string.Equals(c.HeaderTitle, request.CategoryHeaderTitle?.Trim(), StringComparison.Ordinal));
            if (category == null)
            {
                SendAdminResponse(fromPlayer, false, "Category not found");
                return;
            }

            var lastNode = category.Nodes
                .Where(n => !string.Equals(n.NodeType, "Start", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.Id)
                .FirstOrDefault();

            if (lastNode == null)
            {
                SendAdminResponse(fromPlayer, false, "No nodes to delete");
                return;
            }

            int deletedId = lastNode.Id;
            category.Nodes = category.Nodes.Where(n => n.Id != deletedId).ToArray();
            category.Connections = category.Connections
                .Where(c => c.StartNodeId != deletedId && c.EndNodeId != deletedId)
                .ToArray();
            // Keep NextNodeId watermark so a recreated quest never reuses this id.
            category.EnsureNextNodeIdWatermark();
            if (category.NextNodeId <= deletedId)
                category.NextNodeId = deletedId + 1;

            SaveCategoryData(category);
            RemoveNodeProgressFromAllPlayers(category.HeaderTitle, [deletedId]);
            BroadcastCategoryToAllPlayers(category.HeaderTitle);

            SendAdminResponse(fromPlayer, true, $"Node {deletedId} deleted");
            sapi?.Logger.Notification(
                "[SwixyQuestBook] Admin {0} deleted node {1} from {2}",
                fromPlayer.PlayerName, deletedId, category.HeaderTitle);
        }

        private void OnAdminSaveCategory(IServerPlayer fromPlayer, QuestbookAdminSaveCategoryRequest request)
        {
            if (!EnsureAdminAuthorized(fromPlayer, "SaveCategory"))
            {
                return;
            }

            if (questDatabase == null)
            {
                SendAdminResponse(fromPlayer, false, "Quest database not loaded");
                return;
            }

            string categoryHeaderTitle = request.CategoryHeaderTitle?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(categoryHeaderTitle))
            {
                SendAdminResponse(fromPlayer, false, "Category not found");
                return;
            }

            if (!TrySanitizeCategoryPayload(request.Category, out QuestbookCategoryData? sanitized, out string error)
                || sanitized == null)
            {
                SendAdminResponse(fromPlayer, false, error);
                sapi?.Logger.Warning(
                    "[SwixyQuestBook] Rejected save from {0}: {1}",
                    fromPlayer.PlayerName, error);
                return;
            }

            // Keep stable identity of the category being edited.
            sanitized.HeaderTitle = categoryHeaderTitle;
            string lang = GetPlayerLanguageCode(fromPlayer);

            for (int i = 0; i < questDatabase.Categories.Length; i++)
            {
                if (!string.Equals(questDatabase.Categories[i].HeaderTitle, categoryHeaderTitle, StringComparison.Ordinal))
                {
                    continue;
                }

                QuestbookCategoryData previous = questDatabase.Categories[i];
                var previousIds = previous.Nodes.Select(static n => n.Id).ToHashSet();

                questDatabase.Categories[i] = MergeCategoryForLanguage(
                    previous,
                    sanitized,
                    lang,
                    request.Category);

                // Preserve / advance id watermark so deleted ids are not reused by future creates.
                questDatabase.Categories[i].NextNodeId = System.Math.Max(
                    previous.NextNodeId,
                    sanitized.NextNodeId);
                questDatabase.Categories[i].EnsureNextNodeIdWatermark();
                // Also raise watermark above any id still present (and any previously used).
                if (previousIds.Count > 0)
                {
                    int maxPrev = previousIds.Max();
                    if (questDatabase.Categories[i].NextNodeId <= maxPrev)
                        questDatabase.Categories[i].NextNodeId = maxPrev + 1;
                }

                var liveIds = questDatabase.Categories[i].Nodes.Select(static n => n.Id).ToHashSet();
                int[] removedIds = previousIds.Where(id => !liveIds.Contains(id)).ToArray();
                if (removedIds.Length > 0)
                    RemoveNodeProgressFromAllPlayers(categoryHeaderTitle, removedIds);

                SaveCategoryData(questDatabase.Categories[i]);
                BroadcastCategoryToAllPlayers(categoryHeaderTitle);

                SendAdminResponse(fromPlayer, true, "Category saved");
                sapi?.Logger.Notification(
                    "[SwixyQuestBook] Admin {0} saved category {1} ({2} nodes, lang={3})",
                    fromPlayer.PlayerName, categoryHeaderTitle, questDatabase.Categories[i].Nodes.Length, lang);
                return;
            }

            SendAdminResponse(fromPlayer, false, "Category not found");
        }

        /// <summary>
        /// When admin deletes a quest node, drop completion/craft/kill progress for that id
        /// so a new quest cannot inherit "completed" state via reused nodeId.
        /// </summary>
        private void RemoveNodeProgressFromAllPlayers(string categoryHeaderTitle, IReadOnlyCollection<int> nodeIds)
        {
            if (string.IsNullOrWhiteSpace(categoryHeaderTitle) || nodeIds.Count == 0)
                return;

            var changedUids = new List<string>();
            foreach (QuestbookPlayerProgressData progress in playerProgressMap.Values)
            {
                bool changed = false;
                foreach (int nodeId in nodeIds)
                {
                    if (progress.ClearAllProgressForNode(categoryHeaderTitle, nodeId))
                        changed = true;
                }

                if (!changed)
                    continue;

                SavePlayerProgress(progress);
                if (!string.IsNullOrEmpty(progress.PlayerUid))
                    changedUids.Add(progress.PlayerUid);
            }

            if (changedUids.Count == 0 || sapi?.World?.AllOnlinePlayers == null)
                return;

            foreach (IPlayer online in sapi.World.AllOnlinePlayers)
            {
                if (online is not IServerPlayer serverPlayer)
                    continue;
                if (!changedUids.Contains(serverPlayer.PlayerUID))
                    continue;
                if (!playerProgressMap.TryGetValue(serverPlayer.PlayerUID, out QuestbookPlayerProgressData? progress))
                    continue;
                SendProgressToPlayer(serverPlayer, progress, fullSync: true);
            }
        }

        /// <summary>
        /// Prefer full i18n maps from the client when provided; otherwise merge the
        /// active-language display string into the existing multi-lang maps.
        /// </summary>
        private static QuestbookCategoryData MergeCategoryForLanguage(
            QuestbookCategoryData existing,
            QuestbookCategoryData incoming,
            string lang,
            QuestbookSyncCategoryPacket? rawPacket = null)
        {
            QuestbookLocalizedText mergedTitle;
            if (rawPacket?.TitleI18n is { Length: > 0 })
            {
                mergedTitle = FromLangPackets(rawPacket.TitleI18n);
            }
            else
            {
                mergedTitle = existing.Title?.Clone() ?? new QuestbookLocalizedText();
                string resolvedTitle = incoming.Title?.Resolve(lang) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(resolvedTitle))
                    mergedTitle.Set(lang, resolvedTitle);
            }

            QuestbookLocalizedText mergedHeader;
            if (rawPacket?.HeaderI18n is { Length: > 0 })
            {
                mergedHeader = FromLangPackets(rawPacket.HeaderI18n);
            }
            else
            {
                mergedHeader = existing.Header?.Clone() ?? new QuestbookLocalizedText();
                string resolvedHeader = incoming.Header?.Resolve(lang)
                    ?? incoming.Title?.Resolve(lang)
                    ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(resolvedHeader))
                    mergedHeader.Set(lang, resolvedHeader);
            }

            var existingById = existing.Nodes.ToDictionary(n => n.Id);
            var rawNodesById = (rawPacket?.Nodes ?? [])
                .ToDictionary(n => n.Id);

            var mergedNodes = new List<QuestbookQuestNodeData>(incoming.Nodes.Length);
            foreach (QuestbookQuestNodeData node in incoming.Nodes)
            {
                QuestbookLocalizedText description;
                if (rawNodesById.TryGetValue(node.Id, out QuestbookSyncNodePacket? rawNode)
                    && rawNode.DescriptionI18n is { Length: > 0 })
                {
                    description = FromLangPackets(rawNode.DescriptionI18n);
                }
                else
                {
                    description = existingById.TryGetValue(node.Id, out QuestbookQuestNodeData? prev)
                        ? (prev.Description?.Clone() ?? new QuestbookLocalizedText())
                        : new QuestbookLocalizedText();

                    string text = node.Description?.Resolve(lang) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text))
                        description.Set(lang, text);
                }

                mergedNodes.Add(new QuestbookQuestNodeData
                {
                    Id = node.Id,
                    X = node.X,
                    Y = node.Y,
                    NodeType = node.NodeType,
                    Description = description,
                    RequiredItems = node.RequiredItems,
                    RewardItems = node.RewardItems,
                    ConsumeRequiredItems = node.ConsumeRequiredItems
                });
            }

            return new QuestbookCategoryData
            {
                IconItemCode = incoming.IconItemCode,
                Title = mergedTitle,
                HeaderTitle = existing.HeaderTitle,
                Header = mergedHeader,
                Nodes = mergedNodes.ToArray(),
                Connections = incoming.Connections
            };
        }

        private void OnAdminAddCategory(IServerPlayer fromPlayer, QuestbookAdminAddCategoryRequest request)
        {
            if (!EnsureAdminAuthorized(fromPlayer, "AddCategory"))
            {
                return;
            }

            if (questDatabase == null)
            {
                SendAdminResponse(fromPlayer, false, "Quest database not loaded");
                return;
            }

            if (questDatabase.Categories.Length >= MaxCategories)
            {
                SendAdminResponse(fromPlayer, false, "Category limit reached");
                return;
            }

            string lang = GetPlayerLanguageCode(fromPlayer);
            if (!TryBuildCategoryTitleMaps(
                    request.Title,
                    request.TitleI18n,
                    lang,
                    existingTitle: null,
                    existingHeader: null,
                    out QuestbookLocalizedText titleMap,
                    out QuestbookLocalizedText headerMap,
                    out string primaryTitle,
                    out string error))
            {
                SendAdminResponse(fromPlayer, false, error);
                return;
            }

            string headerKey = string.IsNullOrWhiteSpace(request.HeaderTitle)
                ? primaryTitle.ToUpperInvariant()
                : request.HeaderTitle.Trim();

            if (headerKey.Length > MaxCategoryTitleLength)
            {
                SendAdminResponse(fromPlayer, false, "Branch header is too long");
                return;
            }

            headerKey = EnsureUniqueHeaderTitle(headerKey, null);

            string iconItemCode = NormalizeIconItemCode(request.IconItemCode);
            if (!string.IsNullOrWhiteSpace(iconItemCode) && !IsValidIconItemCode(iconItemCode))
            {
                SendAdminResponse(fromPlayer, false, "Invalid branch item icon");
                return;
            }

            var newCategory = new QuestbookCategoryData
            {
                IconItemCode = iconItemCode,
                Title = titleMap,
                HeaderTitle = headerKey,
                Header = headerMap,
                Nodes =
                [
                    new QuestbookQuestNodeData
                    {
                        Id = 0,
                        X = 0,
                        Y = 0,
                        NodeType = "Start",
                        Description = new QuestbookLocalizedText()
                    }
                ],
                Connections = []
            };

            var categoriesList = questDatabase.Categories.ToList();
            categoriesList.Add(newCategory);
            questDatabase.Categories = categoriesList.ToArray();

            SaveQuestData();
            BroadcastCategoryMetaToAllPlayers(newCategory, isNew: true);
            BroadcastCategoryToAllPlayers(newCategory.HeaderTitle);

            SendAdminResponse(fromPlayer, true, "Category created", newCategory.HeaderTitle);
            sapi?.Logger.Notification(
                "[SwixyQuestBook] Admin {0} created category {1}",
                fromPlayer.PlayerName, newCategory.HeaderTitle);
        }

        private void OnAdminRenameCategory(IServerPlayer fromPlayer, QuestbookAdminRenameCategoryRequest request)
        {
            if (!EnsureAdminAuthorized(fromPlayer, "RenameCategory"))
            {
                return;
            }

            if (questDatabase == null)
            {
                SendAdminResponse(fromPlayer, false, "Quest database not loaded");
                return;
            }

            string categoryHeaderTitle = request.CategoryHeaderTitle?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(categoryHeaderTitle))
            {
                SendAdminResponse(fromPlayer, false, "Category not found");
                return;
            }

            int categoryIndex = Array.FindIndex(
                questDatabase.Categories,
                category => string.Equals(category.HeaderTitle, categoryHeaderTitle, StringComparison.Ordinal));

            if (categoryIndex < 0)
            {
                SendAdminResponse(fromPlayer, false, "Category not found");
                return;
            }

            var existingCategory = questDatabase.Categories[categoryIndex];
            string lang = GetPlayerLanguageCode(fromPlayer);

            string iconItemCode = string.IsNullOrWhiteSpace(request.IconItemCode)
                ? existingCategory.IconItemCode
                : NormalizeIconItemCode(request.IconItemCode);

            if (!string.IsNullOrWhiteSpace(iconItemCode) && !IsValidIconItemCode(iconItemCode))
            {
                SendAdminResponse(fromPlayer, false, "Invalid branch item icon");
                return;
            }

            // Keep stable HeaderTitle key; update multi-lang Title/Header maps.
            if (!TryBuildCategoryTitleMaps(
                    request.Title,
                    request.TitleI18n,
                    lang,
                    existingCategory.Title,
                    existingCategory.Header,
                    out QuestbookLocalizedText titleMap,
                    out QuestbookLocalizedText headerMap,
                    out _,
                    out string error))
            {
                SendAdminResponse(fromPlayer, false, error);
                return;
            }

            // Optional single-lang header override for the player's language (legacy field).
            if (!string.IsNullOrWhiteSpace(request.HeaderTitle)
                && (request.TitleI18n == null || request.TitleI18n.Length == 0))
            {
                string headerDisplay = request.HeaderTitle.Trim();
                if (headerDisplay.Length > MaxCategoryTitleLength)
                    headerDisplay = headerDisplay[..MaxCategoryTitleLength];
                headerMap.Set(lang, headerDisplay);
            }

            questDatabase.Categories[categoryIndex] = new QuestbookCategoryData
            {
                IconItemCode = iconItemCode,
                Title = titleMap,
                HeaderTitle = existingCategory.HeaderTitle,
                Header = headerMap,
                Nodes = existingCategory.Nodes,
                Connections = existingCategory.Connections
            };

            SaveCategoryData(questDatabase.Categories[categoryIndex]);
            // Sidebar titles/icons only — no full stub list resync.
            BroadcastCategoryMetaToAllPlayers(questDatabase.Categories[categoryIndex], isNew: false);

            SendAdminResponse(fromPlayer, true, "Category renamed", existingCategory.HeaderTitle);
            sapi?.Logger.Notification(
                "[SwixyQuestBook] Admin {0} renamed category display for {1} (langs={2})",
                fromPlayer.PlayerName, existingCategory.HeaderTitle, titleMap.Entries.Count);
        }

        private void OnAdminDeleteCategory(IServerPlayer fromPlayer, QuestbookAdminDeleteCategoryRequest request)
        {
            if (!EnsureAdminAuthorized(fromPlayer, "DeleteCategory"))
            {
                return;
            }

            if (questDatabase == null)
            {
                SendAdminResponse(fromPlayer, false, "Quest database not loaded");
                return;
            }

            string categoryHeaderTitle = request.CategoryHeaderTitle?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(categoryHeaderTitle))
            {
                SendAdminResponse(fromPlayer, false, "Category not found");
                return;
            }

            if (questDatabase.Categories.Length <= 1)
            {
                SendAdminResponse(fromPlayer, false, "Cannot delete the last category");
                return;
            }

            int categoryIndex = Array.FindIndex(
                questDatabase.Categories,
                category => string.Equals(category.HeaderTitle, categoryHeaderTitle, StringComparison.Ordinal));

            if (categoryIndex < 0)
            {
                SendAdminResponse(fromPlayer, false, "Category not found");
                return;
            }

            questDatabase.Categories = questDatabase.Categories
                .Where((_, index) => index != categoryIndex)
                .ToArray();

            RemoveCategoryProgressFromAllPlayers(categoryHeaderTitle);

            SaveQuestData();
            BroadcastCategoryRemovedToAllPlayers(categoryHeaderTitle);
            BroadcastProgressToAllPlayers();

            SendAdminResponse(fromPlayer, true, "Category deleted");
            sapi?.Logger.Notification(
                "[SwixyQuestBook] Admin {0} deleted category {1}",
                fromPlayer.PlayerName, categoryHeaderTitle);
        }

        private bool TrySanitizeCategoryPayload(
            QuestbookSyncCategoryPacket? packet,
            out QuestbookCategoryData? category,
            out string error)
        {
            category = null;
            error = string.Empty;

            if (packet == null)
            {
                error = "Empty category payload";
                return false;
            }

            // Incoming Title/HeaderDisplay/Description are single-language display strings
            // for the admin's current language. MergeCategoryForLanguage expands them.
            string title = packet.Title?.Trim() ?? string.Empty;
            if (title.Length > MaxCategoryTitleLength)
            {
                error = "Branch title is too long";
                return false;
            }

            string headerKey = packet.HeaderTitle?.Trim() ?? string.Empty;
            if (headerKey.Length > MaxCategoryTitleLength)
            {
                error = "Branch header is too long";
                return false;
            }

            string headerDisplay = packet.HeaderDisplay?.Trim() ?? string.Empty;
            if (headerDisplay.Length > MaxCategoryTitleLength)
            {
                headerDisplay = headerDisplay[..MaxCategoryTitleLength];
            }

            string iconItemCode = NormalizeIconItemCode(packet.IconItemCode);
            if (!string.IsNullOrWhiteSpace(iconItemCode) && !IsValidIconItemCode(iconItemCode))
            {
                error = "Invalid branch item icon";
                return false;
            }

            var rawNodes = packet.Nodes ?? [];
            if (rawNodes.Length == 0 || rawNodes.Length > MaxNodesPerCategory)
            {
                error = "Invalid node count";
                return false;
            }

            var rawConnections = packet.Connections ?? [];
            if (rawConnections.Length > MaxConnectionsPerCategory)
            {
                error = "Too many connections";
                return false;
            }

            var nodes = new List<QuestbookQuestNodeData>(rawNodes.Length);
            var seenIds = new HashSet<int>();
            int startCount = 0;

            foreach (var n in rawNodes)
            {
                if (!seenIds.Add(n.Id) || n.Id < 0)
                {
                    error = "Duplicate or invalid node id";
                    return false;
                }

                if (!IsFiniteCoordinate(n.X) || !IsFiniteCoordinate(n.Y))
                {
                    error = "Invalid node coordinates";
                    return false;
                }

                string? nodeType = n.NodeType switch
                {
                    0 => "Start",
                    1 => "Quest",
                    2 => "Checkpoint",
                    3 => "Kill",
                    4 => "Quest", // legacy Lore → Quest
                    _ => null
                };

                if (nodeType is null)
                {
                    error = "Invalid node type";
                    return false;
                }

                if (nodeType == "Start")
                {
                    startCount++;
                }

                var required = SanitizeItemList(n.RequiredItems, allowWildcards: true);
                var rewards = SanitizeItemList(n.RewardItems, allowWildcards: false);
                if (nodeType is "Start" or "Checkpoint")
                {
                    required = [];
                    rewards = [];
                }

                string description = SanitizeDescription(n.Description);
                // Temporary single-lang carrier; MergeCategoryForLanguage maps into the player language.
                var descriptionText = string.IsNullOrWhiteSpace(description)
                    ? new QuestbookLocalizedText()
                    : new QuestbookLocalizedText(description, QuestbookLocalizedText.DefaultLang);

                bool isObjectiveNode = nodeType is "Quest" or "Kill";
                nodes.Add(new QuestbookQuestNodeData
                {
                    Id = n.Id,
                    X = n.X,
                    Y = n.Y,
                    NodeType = nodeType,
                    Description = descriptionText,
                    RequiredItems = required.Select(i => new QuestbookQuestItemData(
                        i.CollectibleCode, i.Count, i.Objective, i.Consume)).ToArray(),
                    RewardItems = rewards.Select(i => new QuestbookQuestItemData(
                        i.CollectibleCode, i.Count, QuestbookGoalObjective.Have)).ToArray(),
                    ConsumeRequiredItems = !isObjectiveNode
                        || required.Any(static i => i.Consume)
                });
            }

            if (startCount != 1)
            {
                error = "Category must have exactly one Start node";
                return false;
            }

            var idSet = seenIds;
            var connections = new List<QuestbookQuestConnectionData>(rawConnections.Length);
            var connectionKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var c in rawConnections)
            {
                if (!idSet.Contains(c.StartNodeId) || !idSet.Contains(c.EndNodeId))
                {
                    error = "Connection references missing node";
                    return false;
                }

                if (c.StartNodeId == c.EndNodeId)
                {
                    error = "Self-connection is not allowed";
                    return false;
                }

                string key = $"{c.StartNodeId}->{c.EndNodeId}";
                if (!connectionKeys.Add(key))
                {
                    continue;
                }

                connections.Add(new QuestbookQuestConnectionData(c.StartNodeId, c.EndNodeId));
            }

            category = new QuestbookCategoryData
            {
                IconItemCode = iconItemCode,
                Title = new QuestbookLocalizedText(title, QuestbookLocalizedText.DefaultLang),
                HeaderTitle = headerKey,
                Header = new QuestbookLocalizedText(
                    string.IsNullOrWhiteSpace(headerDisplay) ? title : headerDisplay,
                    QuestbookLocalizedText.DefaultLang),
                Nodes = nodes.ToArray(),
                Connections = connections.ToArray()
            };
            return true;
        }

        private static bool IsFiniteCoordinate(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value)
                && value > -100000 && value < 100000;
        }

        private static string? NormalizeNodeTypeName(string? nodeType)
        {
            if (string.IsNullOrWhiteSpace(nodeType))
            {
                return "Quest";
            }

            return nodeType.Trim().ToLowerInvariant() switch
            {
                "start" => "Start",
                "checkpoint" => "Checkpoint",
                "quest" => "Quest",
                "kill" => "Kill",
                "lore" => "Quest", // legacy Lore → Quest
                _ => null
            };
        }

        private static string NormalizeDirection(string? direction)
        {
            return (direction?.Trim().ToUpperInvariant()) switch
            {
                "T" => "T",
                "B" => "B",
                "L" => "L",
                "R" => "R",
                _ => "R"
            };
        }

        private static string SanitizeDescription(string? description)
        {
            string text = description?.Trim() ?? string.Empty;
            if (text.Length > MaxNodeDescriptionLength)
            {
                text = text[..MaxNodeDescriptionLength];
            }

            // Strip control characters except newline/tab.
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                {
                    chars[i] = ' ';
                }
            }

            return new string(chars);
        }

        private QuestbookQuestItemRequirement[] SanitizeItemList(
            IEnumerable<QuestbookQuestItemData>? items,
            bool allowWildcards) =>
            collectibleSanitizer?.Sanitize(items, allowWildcards) ?? [];

        private QuestbookQuestItemRequirement[] SanitizeItemList(
            IEnumerable<QuestbookQuestItemStackPacket>? items,
            bool allowWildcards) =>
            collectibleSanitizer?.Sanitize(items, allowWildcards) ?? [];

        private QuestbookQuestItemRequirement[] SanitizeItemList(
            IEnumerable<QuestbookSyncItemPacket>? items,
            bool allowWildcards) =>
            collectibleSanitizer?.Sanitize(items, allowWildcards) ?? [];

        private QuestbookQuestItemRequirement[] SanitizeItemList(
            QuestbookQuestItemRequirement[] items,
            bool allowWildcards) =>
            collectibleSanitizer?.Sanitize(items, allowWildcards) ?? [];

        private string NormalizeCollectibleCode(string? code, bool allowWildcards) =>
            collectibleSanitizer?.NormalizeCollectibleCode(code, allowWildcards) ?? string.Empty;

        private string EnsureUniqueHeaderTitle(string headerTitle, string? excludeHeaderTitle)
        {
            string candidate = headerTitle;
            int suffix = 2;

            while (questDatabase?.Categories.Any(category =>
                       !string.Equals(category.HeaderTitle, excludeHeaderTitle, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(category.HeaderTitle, candidate, StringComparison.OrdinalIgnoreCase)) == true)
            {
                candidate = $"{headerTitle} ({suffix})";
                suffix++;
            }

            return candidate;
        }

        private void MigrateCategoryHeaderInAllProgress(string oldHeaderTitle, string newHeaderTitle)
        {
            foreach (var progress in playerProgressMap.Values)
            {
                bool changed = false;
                foreach (var entry in progress.CompletedQuestsMap.Values.ToArray())
                {
                    if (!string.Equals(entry.CategoryHeaderTitle, oldHeaderTitle, StringComparison.Ordinal))
                        continue;

                    progress.CompletedQuestsMap.Remove($"{oldHeaderTitle}:{entry.NodeId}");
                    entry.CategoryHeaderTitle = newHeaderTitle;
                    progress.CompletedQuestsMap[$"{newHeaderTitle}:{entry.NodeId}"] = entry;
                    changed = true;
                }

                if (changed)
                {
                    progress.CompletedQuests = progress.CompletedQuestsMap.Values
                        .OrderBy(entry => entry.CompletionOrder)
                        .ToArray();
                    SavePlayerProgress(progress);
                }
            }
        }

        private void RemoveCategoryProgressFromAllPlayers(string categoryHeaderTitle)
        {
            foreach (var progress in playerProgressMap.Values)
            {
                var keysToRemove = progress.CompletedQuestsMap.Keys
                    .Where(key => key.StartsWith($"{categoryHeaderTitle}:", StringComparison.Ordinal))
                    .ToArray();

                if (keysToRemove.Length == 0)
                    continue;

                foreach (string key in keysToRemove)
                    progress.CompletedQuestsMap.Remove(key);

                progress.CompletedQuests = progress.CompletedQuestsMap.Values
                    .OrderBy(entry => entry.CompletionOrder)
                    .ToArray();
                progress.TotalQuestsCompleted = progress.CompletedQuests.Length;
                SavePlayerProgress(progress);
            }
        }

        private (double x, double y) CalculateNodePosition(
            QuestbookQuestNodeData parent,
            string direction,
            string nodeType,
            bool isSubQuest,
            int subQuestIndex,
            int totalSubQuests)
        {
            double distance;
            bool isSide = false;

            if (isSubQuest)
            {
                distance = 18;
            }
            else if (nodeType == "Checkpoint")
            {
                distance = 136;
            }
            else if (totalSubQuests > 1 && subQuestIndex == 0)
            {
                distance = 88;
            }
            else
            {
                distance = parent.Id == 0 ? 64 : 36;
                isSide = totalSubQuests > 1 && subQuestIndex > 0;
            }

            if (isSide)
            {
                return direction switch
                {
                    "T" => (parent.X - distance, parent.Y),
                    "B" => (parent.X + distance, parent.Y),
                    "L" => (parent.X, parent.Y + distance),
                    "R" => (parent.X, parent.Y - distance),
                    _ => (parent.X, parent.Y)
                };
            }
            else
            {
                return direction switch
                {
                    "T" => (parent.X, parent.Y - distance),
                    "B" => (parent.X, parent.Y + distance),
                    "L" => (parent.X - distance, parent.Y),
                    "R" => (parent.X + distance, parent.Y),
                    _ => (parent.X, parent.Y)
                };
            }
        }

        private void SendAdminResponse(IServerPlayer player, bool success, string message, string categoryHeaderTitle = "")
        {
            serverChannel?.SendPacket(
                new QuestbookAdminResponse
                {
                    Success = success,
                    Message = message,
                    CategoryHeaderTitle = categoryHeaderTitle
                },
                player
            );
        }

        private void BroadcastQuestsToAllPlayers()
        {
            if (questDatabase == null || serverChannel == null || sapi?.World == null) return;

            foreach (var player in sapi.World.AllPlayers)
            {
                if (player is IServerPlayer serverPlayer)
                {
                    SendQuestsToPlayer(serverPlayer);
                }
            }
        }

        /// <summary>
        /// Expand legacy lang-key strings (category.x.title / quest.x.n.description) into
        /// multi-language maps using the game's loaded lang tables + bundled mod lang files.
        /// Returns true when any field changed (caller should re-save).
        /// </summary>
        private bool ExpandLegacyLocalizedContent(QuestbookQuestDatabase database)
        {
            Dictionary<string, Dictionary<string, string>>? bundled = null;
            try
            {
                bundled = LoadBundledModLangTables();
            }
            catch (Exception ex)
            {
                sapi?.Logger.Warning($"[SwixyQuestBook] Could not load bundled lang tables: {ex.Message}");
            }

            string[] langs = bundled?.Keys.ToArray() ?? ["en", "ru"];
            bool changed = false;

            foreach (QuestbookCategoryData category in database.Categories)
            {
                category.Title ??= new QuestbookLocalizedText();
                category.Header ??= new QuestbookLocalizedText();

                if (TryExpandLegacyText(category.Title, langs, bundled, ref changed))
                {
                    // expanded in place
                }

                // headerTitle is the stable key; if Header is empty, fill display from that key.
                if (category.Header.IsEmpty && !string.IsNullOrWhiteSpace(category.HeaderTitle))
                {
                    if (LooksLikeLangKey(category.HeaderTitle))
                    {
                        foreach (string lang in langs)
                        {
                            string resolved = ResolveLangKey(category.HeaderTitle, lang, bundled);
                            if (!string.IsNullOrWhiteSpace(resolved) &&
                                !string.Equals(resolved, category.HeaderTitle, StringComparison.Ordinal))
                            {
                                category.Header.Set(lang, resolved);
                                changed = true;
                            }
                        }
                    }
                    else
                    {
                        category.Header.Set(QuestbookLocalizedText.DefaultLang, category.HeaderTitle);
                        changed = true;
                    }
                }

                foreach (QuestbookQuestNodeData node in category.Nodes)
                {
                    node.Description ??= new QuestbookLocalizedText();
                    TryExpandLegacyText(node.Description, langs, bundled, ref changed);
                }
            }

            return changed;
        }

        private static bool TryExpandLegacyText(
            QuestbookLocalizedText text,
            string[] langs,
            Dictionary<string, Dictionary<string, string>>? bundled,
            ref bool changed)
        {
            if (text.Entries.Count != 1)
            {
                return false;
            }

            string single = text.Entries.Values.First();
            if (!LooksLikeLangKey(single))
            {
                return false;
            }

            bool any = false;
            foreach (string lang in langs)
            {
                string resolved = ResolveLangKey(single, lang, bundled);
                if (!string.IsNullOrWhiteSpace(resolved) &&
                    !string.Equals(resolved, single, StringComparison.Ordinal))
                {
                    text.Set(lang, resolved);
                    any = true;
                }
            }

            if (any)
            {
                // Drop the raw key if it was stored under a language entry.
                foreach (string lang in text.Entries.Keys.ToArray())
                {
                    if (string.Equals(text.Entries[lang], single, StringComparison.Ordinal))
                    {
                        text.Set(lang, null);
                    }
                }

                changed = true;
            }

            return any;
        }

        private static bool LooksLikeLangKey(string text)
        {
            return text.Contains('.') && !text.Contains(' ') && text.Length < 128;
        }

        private static string ResolveLangKey(
            string key,
            string lang,
            Dictionary<string, Dictionary<string, string>>? bundled)
        {
            string domainKey = key.StartsWith("swixyquestbook:", StringComparison.OrdinalIgnoreCase)
                ? key
                : $"swixyquestbook:{key}";

            try
            {
                string fromGame = Lang.GetL(lang, domainKey);
                if (!string.IsNullOrWhiteSpace(fromGame)
                    && !fromGame.StartsWith("swixyquestbook:", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(fromGame, domainKey, StringComparison.Ordinal)
                    && !string.Equals(fromGame, key, StringComparison.Ordinal))
                {
                    return fromGame;
                }
            }
            catch
            {
                // Lang table may be unavailable during early load.
            }

            if (bundled != null
                && bundled.TryGetValue(lang, out Dictionary<string, string>? table))
            {
                if (table.TryGetValue(key, out string? direct) && !string.IsNullOrWhiteSpace(direct))
                {
                    return direct;
                }

                string bare = key.StartsWith("swixyquestbook:", StringComparison.OrdinalIgnoreCase)
                    ? key["swixyquestbook:".Length..]
                    : key;
                if (table.TryGetValue(bare, out string? bareValue) && !string.IsNullOrWhiteSpace(bareValue))
                {
                    return bareValue;
                }
            }

            return string.Empty;
        }

        private Dictionary<string, Dictionary<string, string>> LoadBundledModLangTables()
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            string? baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                ?? AppContext.BaseDirectory;

            // Dev / packaged layouts.
            string[] candidates =
            [
                Path.Combine(baseDir, "assets", "swixyquestbook", "lang"),
                Path.Combine(baseDir, "swixyquestbook", "assets", "swixyquestbook", "lang"),
                Path.Combine(AppContext.BaseDirectory, "assets", "swixyquestbook", "lang")
            ];

            foreach (string dir in candidates)
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (string file in Directory.GetFiles(dir, "*.json"))
                {
                    string lang = Path.GetFileNameWithoutExtension(file);
                    try
                    {
                        string json = File.ReadAllText(file);
                        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (map != null && map.Count > 0)
                        {
                            result[QuestbookLocalizedText.NormalizeLang(lang)] = map;
                        }
                    }
                    catch
                    {
                        // skip broken lang files
                    }
                }

                if (result.Count > 0)
                {
                    break;
                }
            }

            return result;
        }

        private static string NormalizeIconItemCode(string? iconItemCode) =>
            QuestbookCollectibleSanitizer.NormalizeIconItemCode(iconItemCode);

        private bool IsValidIconItemCode(string iconItemCode) =>
            collectibleSanitizer?.IsValidIconItemCode(iconItemCode) == true;

        /// <summary>
        /// Builds multi-lang Title + Header maps for a branch.
        /// When <paramref name="titleI18n"/> is non-empty, it is the source of truth (merged onto existing).
        /// Otherwise falls back to a single <paramref name="primaryTitle"/> for the player language.
        /// Header entries are uppercased titles per language.
        /// </summary>
        private bool TryBuildCategoryTitleMaps(
            string? primaryTitle,
            QuestbookLangTextPacket[]? titleI18n,
            string playerLang,
            QuestbookLocalizedText? existingTitle,
            QuestbookLocalizedText? existingHeader,
            out QuestbookLocalizedText titleMap,
            out QuestbookLocalizedText headerMap,
            out string resolvedPrimary,
            out string error)
        {
            titleMap = existingTitle?.Clone() ?? new QuestbookLocalizedText();
            headerMap = existingHeader?.Clone() ?? new QuestbookLocalizedText();
            resolvedPrimary = string.Empty;
            error = string.Empty;

            bool hasI18n = titleI18n is { Length: > 0 };
            if (hasI18n)
            {
                // Replace with submitted map when full i18n is provided (admin UI).
                // Empty langs are removed so stale text can be cleared.
                var submitted = new QuestbookLocalizedText();
                foreach (QuestbookLangTextPacket entry in titleI18n!)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Lang))
                        continue;
                    string lang = QuestbookLocalizedText.NormalizeLang(entry.Lang);
                    string text = entry.Text?.Trim() ?? string.Empty;
                    if (text.Length > MaxCategoryTitleLength)
                    {
                        error = "Branch title is too long";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(text))
                        continue;
                    submitted.Set(lang, text);
                }

                if (submitted.IsEmpty)
                {
                    error = "Branch title is required";
                    return false;
                }

                titleMap = submitted;
                headerMap = new QuestbookLocalizedText();
                foreach ((string lang, string text) in titleMap.Entries)
                    headerMap.Set(lang, text.ToUpperInvariant());
            }
            else
            {
                string title = primaryTitle?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(title))
                {
                    error = "Branch title is required";
                    return false;
                }

                if (title.Length > MaxCategoryTitleLength)
                {
                    error = "Branch title is too long";
                    return false;
                }

                titleMap.Set(playerLang, title);
                headerMap.Set(playerLang, title.ToUpperInvariant());
            }

            resolvedPrimary = titleMap.Resolve(playerLang);
            if (string.IsNullOrWhiteSpace(resolvedPrimary))
            {
                error = "Branch title is required";
                return false;
            }

            return true;
        }

        #endregion
    }
}
