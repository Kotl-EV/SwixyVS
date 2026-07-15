using SwixyQuestBook.Gui;
using SwixyQuestBook.Helpers;
using SwixyQuestBook.Network;
using SwixyQuestBook.Server;
using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SwixyQuestBook.Server
{
    public sealed class QuestbookServerSystem : ModSystem
    {
        private ICoreServerAPI? sapi;
        private IServerNetworkChannel? serverChannel;
        private QuestbookQuestDatabase? questDatabase;
        private Dictionary<string, QuestbookPlayerProgressData> playerProgressMap = new();
        private string questsDataPath = string.Empty;
        private string playersDataPath = string.Empty;

        private const string QuestsFileName = "quests.json";
        private const string PlayersFolderName = "players";
        private const string QuestbookDataFolder = "swixyquestbook";

        /// <summary>Server privilege required for all questbook admin mutations.</summary>
        private const string AdminPrivilegeCode = "controlserver";

        private const int MaxCategoryTitleLength = 80;
        private const int MaxNodeDescriptionLength = 2000;
        /// <summary>
        /// Max goals/rewards per quest node. Keep in sync with
        /// <see cref="QuestbookAdminData.MaxItemEntries"/> on the client editor.
        /// </summary>
        private const int MaxItemsPerList = 64;
        private const int MaxItemStackCount = 9999;
        private const int MaxNodesPerCategory = 500;
        private const int MaxConnectionsPerCategory = 1000;
        private const int MaxCollectibleCodeLength = 128;
        private const int MaxCategories = 64;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;

            string modDataPath = Path.Combine(api.GetOrCreateDataPath("swixyquestbook"), QuestbookDataFolder);
            questsDataPath = Path.Combine(modDataPath, QuestsFileName);
            playersDataPath = Path.Combine(modDataPath, PlayersFolderName);
            Directory.CreateDirectory(modDataPath);
            Directory.CreateDirectory(playersDataPath);

            serverChannel = api.Network
                .RegisterChannel(QuestbookNetworkConstants.ChannelName)
                .RegisterMessageType<QuestbookSubmitQuestRequest>()
                .RegisterMessageType<QuestbookSubmitQuestResponse>()
                .RegisterMessageType<QuestbookSyncQuestsPacket>()
                .RegisterMessageType<QuestbookSyncProgressPacket>()
                .RegisterMessageType<QuestbookAdminCreateNodeRequest>()
                .RegisterMessageType<QuestbookAdminDeleteLastNodeRequest>()
                .RegisterMessageType<QuestbookAdminSaveCategoryRequest>()
                .RegisterMessageType<QuestbookAdminAddCategoryRequest>()
                .RegisterMessageType<QuestbookAdminRenameCategoryRequest>()
                .RegisterMessageType<QuestbookAdminDeleteCategoryRequest>()
                .RegisterMessageType<QuestbookAdminResponse>()
                .SetMessageHandler<QuestbookSubmitQuestRequest>(OnQuestSubmitRequest)
                .SetMessageHandler<QuestbookAdminCreateNodeRequest>(OnAdminCreateNode)
                .SetMessageHandler<QuestbookAdminDeleteLastNodeRequest>(OnAdminDeleteLastNode)
                .SetMessageHandler<QuestbookAdminSaveCategoryRequest>(OnAdminSaveCategory)
                .SetMessageHandler<QuestbookAdminAddCategoryRequest>(OnAdminAddCategory)
                .SetMessageHandler<QuestbookAdminRenameCategoryRequest>(OnAdminRenameCategory)
                .SetMessageHandler<QuestbookAdminDeleteCategoryRequest>(OnAdminDeleteCategory);

            LoadQuestData();
            LoadAllPlayerProgress();

            api.Event.PlayerJoin += OnPlayerJoin;
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
        }

        public override void Dispose()
        {
            if (sapi != null)
            {
                sapi.Event.PlayerJoin -= OnPlayerJoin;
                sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;
            }

            SaveAllPlayerProgress();
            playerProgressMap.Clear();
            questDatabase = null;
            serverChannel = null;
            sapi = null;
            base.Dispose();
        }

        #region Загрузка/Сохранение данных квестов

        private void LoadQuestData()
        {
            string modQuestsPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory,
                "swixyquestbook", QuestsFileName);

            try
            {
                EnsureRuntimeQuestsFromMod(modQuestsPath);

                if (!File.Exists(questsDataPath))
                {
                    sapi?.Logger.Warning($"[SwixyQuestBook] No quests.json found at {questsDataPath} or {modQuestsPath}");
                    return;
                }

                string json = File.ReadAllText(questsDataPath);
                questDatabase = JsonSerializer.Deserialize<QuestbookQuestDatabase>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

                if (questDatabase?.Categories == null || questDatabase.Categories.Length == 0)
                {
                    sapi?.Logger.Warning("[SwixyQuestBook] No categories in quest data");
                }
                else
                {
                    bool expanded = ExpandLegacyLocalizedContent(questDatabase);
                    if (expanded)
                    {
                        SaveQuestData();
                        sapi?.Logger.Notification("[SwixyQuestBook] Migrated quest titles/descriptions into multi-language maps");
                    }
                }

                sapi?.Logger.Notification(
                    $"[SwixyQuestBook] Loaded {questDatabase?.Categories.Length ?? 0} quest categories (version {questDatabase?.Version ?? "?"})");
            }
            catch (Exception ex)
            {
                sapi?.Logger.Error($"[SwixyQuestBook] Failed to load quest data: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies packaged defaults on first run. When the mod ships a new content version,
        /// backs up the old runtime file and replaces it so progression updates apply.
        /// </summary>
        private void EnsureRuntimeQuestsFromMod(string modQuestsPath)
        {
            if (!File.Exists(modQuestsPath))
            {
                return;
            }

            if (!File.Exists(questsDataPath))
            {
                sapi?.Logger.Notification($"[SwixyQuestBook] Copying default quests from mod: {modQuestsPath}");
                File.Copy(modQuestsPath, questsDataPath);
                return;
            }

            string? runtimeVersion = TryReadQuestVersion(questsDataPath);
            string? packagedVersion = TryReadQuestVersion(modQuestsPath);
            if (string.IsNullOrWhiteSpace(packagedVersion) ||
                string.Equals(runtimeVersion, packagedVersion, StringComparison.Ordinal))
            {
                return;
            }

            string backupPath = questsDataPath + $".bak-{SanitizeFileToken(runtimeVersion ?? "unknown")}";
            try
            {
                File.Copy(questsDataPath, backupPath, overwrite: true);
            }
            catch (Exception ex)
            {
                sapi?.Logger.Warning($"[SwixyQuestBook] Could not backup old quests.json: {ex.Message}");
            }

            File.Copy(modQuestsPath, questsDataPath, overwrite: true);
            sapi?.Logger.Notification(
                $"[SwixyQuestBook] Updated quests.json {runtimeVersion ?? "?"} → {packagedVersion} (backup: {Path.GetFileName(backupPath)})");
        }

        private static string? TryReadQuestVersion(string path)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("version", out var versionProp))
                {
                    return versionProp.GetString();
                }
            }
            catch
            {
                // ignore parse errors — caller will load full file later
            }

            return null;
        }

        private static string SanitizeFileToken(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return value.Length > 48 ? value[..48] : value;
        }

        private void SaveQuestData()
        {
            if (questDatabase == null) return;

            try
            {
                string json = JsonSerializer.Serialize(questDatabase, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                File.WriteAllText(questsDataPath, json);
            }
            catch (Exception ex)
            {
                sapi?.Logger.Error($"[SwixyQuestBook] Failed to save quest data: {ex.Message}");
            }
        }

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
                            playerProgressMap[progress.PlayerUid] = progress;
                        }
                    }
                    catch (Exception ex)
                    {
                        sapi?.Logger.Warning($"[SwixyQuestBook] Failed to load progress from {file}: {ex.Message}");
                    }
                }

                sapi?.Logger.Notification($"[SwixyQuestBook] Loaded progress for {playerProgressMap.Count} players");
            }
            catch (Exception ex)
            {
                sapi?.Logger.Error($"[SwixyQuestBook] Failed to load player progress: {ex.Message}");
            }
        }

        private void SaveAllPlayerProgress()
        {
            foreach (var kvp in playerProgressMap)
                SavePlayerProgress(kvp.Value);
        }

        private void SavePlayerProgress(QuestbookPlayerProgressData progress)
        {
            if (string.IsNullOrEmpty(progress.PlayerUid)) return;

            try
            {
                Directory.CreateDirectory(playersDataPath);
                string fileName = SanitizePlayerUidForFileName(progress.PlayerUid) + ".json";
                string filePath = Path.Combine(playersDataPath, fileName);
                string json = JsonSerializer.Serialize(progress, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                sapi?.Logger.Error($"[SwixyQuestBook] Failed to save progress for {progress.PlayerUid}: {ex.Message}");
            }
        }

        /// <summary>
        /// Player UIDs may contain '/' or other path-illegal characters (session-style IDs).
        /// </summary>
        private static string SanitizePlayerUidForFileName(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return "unknown";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = playerUid.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c == '/' || c == '\\' || Array.IndexOf(invalid, c) >= 0)
                {
                    chars[i] = '_';
                }
            }

            string sanitized = new string(chars).Trim('.', ' ');
            return string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
        }

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

        private void SendQuestsToPlayer(IServerPlayer player)
        {
            if (questDatabase == null || serverChannel == null) return;

            string lang = GetPlayerLanguageCode(player);
            var packet = new QuestbookSyncQuestsPacket
            {
                Categories = questDatabase.Categories.Select(c => BuildLocalizedCategoryPacket(c, lang)).ToArray()
            };

            serverChannel.SendPacket(packet, player);
            sapi?.Logger.Debug(
                $"[SwixyQuestBook] Sent {questDatabase.Categories.Length} categories to {player.PlayerName} (lang={lang})");
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
            string lang)
        {
            string title = category.Title?.Resolve(lang) ?? string.Empty;
            string headerDisplay = category.Header?.Resolve(lang) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(headerDisplay))
            {
                headerDisplay = title;
            }

            return new QuestbookSyncCategoryPacket
            {
                IconItemCode = category.IconItemCode,
                Title = title,
                HeaderTitle = category.HeaderTitle,
                HeaderDisplay = headerDisplay,
                TitleI18n = ToLangPackets(category.Title),
                HeaderI18n = ToLangPackets(category.Header),
                Nodes = category.Nodes.Select(n => new QuestbookSyncNodePacket
                {
                    Id = n.Id,
                    X = n.X,
                    Y = n.Y,
                    NodeType = GetNodeTypeInt(n.NodeType),
                    Description = n.Description?.Resolve(lang) ?? string.Empty,
                    DescriptionI18n = ToLangPackets(n.Description),
                    RequiredItems = n.RequiredItems.Select(i => new QuestbookSyncItemPacket
                    {
                        CollectibleCode = i.CollectibleCode,
                        Count = i.Count
                    }).ToArray(),
                    RewardItems = n.RewardItems.Select(i => new QuestbookSyncItemPacket
                    {
                        CollectibleCode = i.CollectibleCode,
                        Count = i.Count
                    }).ToArray()
                }).ToArray(),
                Connections = category.Connections.Select(conn => new QuestbookSyncConnectionPacket
                {
                    StartNodeId = conn.StartNodeId,
                    EndNodeId = conn.EndNodeId
                }).ToArray()
            };
        }

        private static QuestbookLangTextPacket[] ToLangPackets(QuestbookLocalizedText? text)
        {
            if (text == null || text.IsEmpty)
                return [];

            return text.Entries
                .Where(static e => !string.IsNullOrWhiteSpace(e.Key) && !string.IsNullOrWhiteSpace(e.Value))
                .OrderBy(static e => e.Key, StringComparer.Ordinal)
                .Select(static e => new QuestbookLangTextPacket
                {
                    Lang = e.Key,
                    Text = e.Value
                })
                .ToArray();
        }

        private static QuestbookLocalizedText FromLangPackets(QuestbookLangTextPacket[]? entries)
        {
            var text = new QuestbookLocalizedText();
            if (entries == null)
                return text;

            foreach (QuestbookLangTextPacket entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Lang) && !string.IsNullOrWhiteSpace(entry.Text))
                    text.Set(entry.Lang, entry.Text);
            }

            return text;
        }

        private void SendProgressToPlayer(IServerPlayer player, QuestbookPlayerProgressData progress)
        {
            if (serverChannel == null) return;

            var packet = new QuestbookSyncProgressPacket
            {
                TotalQuestsCompleted = progress.TotalQuestsCompleted,
                CompletedQuests = progress.CompletedQuests.Select(q => new QuestbookSyncCompletedQuestPacket
                {
                    CategoryHeaderTitle = q.CategoryHeaderTitle,
                    NodeId = q.NodeId,
                    CompletedAt = q.CompletedAt,
                    CompletionOrder = q.CompletionOrder
                }).ToArray()
            };

            serverChannel.SendPacket(packet, player);
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
                    SendProgressToPlayer(serverPlayer, progress);
            }
        }

        private static int GetNodeTypeInt(string nodeType)
        {
            return nodeType.ToLowerInvariant() switch
            {
                "start" => 0,
                "checkpoint" => 2,
                _ => 1
            };
        }

        #endregion

        #region Обработка запросов от клиентов

        private void OnQuestSubmitRequest(IServerPlayer fromPlayer, QuestbookSubmitQuestRequest request)
        {
            if (questDatabase == null || fromPlayer == null)
            {
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            // Client payload is untrusted: only category + nodeId are used for lookup.
            string categoryHeader = request.CategoryHeaderTitle?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(categoryHeader) || request.NodeId < 0)
            {
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            var category = questDatabase.Categories.FirstOrDefault(c =>
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

            bool success;
            if (isInfoNode && requiredItems.Length == 0)
            {
                success = true;
            }
            else
            {
                success = QuestbookInventoryHelper.TryConsumeCollectibles(fromPlayer, requiredItems);
                if (success)
                {
                    success = QuestbookInventoryHelper.TryGiveCollectibles(fromPlayer, rewardItems);
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
                SendProgressToPlayer(fromPlayer, progress);
            }

            SendQuestSubmitResponse(fromPlayer, request, success);
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

            if (fromPlayer.HasPrivilege(AdminPrivilegeCode) || fromPlayer.HasPrivilege(Privilege.controlserver))
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

            int newId = category.Nodes.Length == 0 ? 0 : category.Nodes.Max(n => n.Id) + 1;

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
                RequiredItems = requiredItems.Select(i => new QuestbookQuestItemData(i.CollectibleCode, i.Count)).ToArray(),
                RewardItems = rewardItems.Select(i => new QuestbookQuestItemData(i.CollectibleCode, i.Count)).ToArray()
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

            SaveQuestData();
            BroadcastQuestsToAllPlayers();

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

            category.Nodes = category.Nodes.Where(n => n.Id != lastNode.Id).ToArray();
            category.Connections = category.Connections
                .Where(c => c.StartNodeId != lastNode.Id && c.EndNodeId != lastNode.Id)
                .ToArray();

            SaveQuestData();
            BroadcastQuestsToAllPlayers();

            SendAdminResponse(fromPlayer, true, $"Node {lastNode.Id} deleted");
            sapi?.Logger.Notification(
                "[SwixyQuestBook] Admin {0} deleted node {1} from {2}",
                fromPlayer.PlayerName, lastNode.Id, category.HeaderTitle);
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

                questDatabase.Categories[i] = MergeCategoryForLanguage(
                    questDatabase.Categories[i],
                    sanitized,
                    lang,
                    request.Category);

                SaveQuestData();
                BroadcastQuestsToAllPlayers();

                SendAdminResponse(fromPlayer, true, "Category saved");
                sapi?.Logger.Notification(
                    "[SwixyQuestBook] Admin {0} saved category {1} ({2} nodes, lang={3})",
                    fromPlayer.PlayerName, categoryHeaderTitle, questDatabase.Categories[i].Nodes.Length, lang);
                return;
            }

            SendAdminResponse(fromPlayer, false, "Category not found");
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
                    RewardItems = node.RewardItems
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

            string title = request.Title?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                SendAdminResponse(fromPlayer, false, "Branch title is required");
                return;
            }

            if (title.Length > MaxCategoryTitleLength)
            {
                SendAdminResponse(fromPlayer, false, "Branch title is too long");
                return;
            }

            string lang = GetPlayerLanguageCode(fromPlayer);
            string headerKey = string.IsNullOrWhiteSpace(request.HeaderTitle)
                ? title.ToUpperInvariant()
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
                Title = new QuestbookLocalizedText(title, lang),
                HeaderTitle = headerKey,
                Header = new QuestbookLocalizedText(title.ToUpperInvariant(), lang),
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
            BroadcastQuestsToAllPlayers();

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

            string title = request.Title?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                SendAdminResponse(fromPlayer, false, "Branch title is required");
                return;
            }

            if (title.Length > MaxCategoryTitleLength)
            {
                SendAdminResponse(fromPlayer, false, "Branch title is too long");
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

            // Keep stable HeaderTitle key; only update language-specific display strings.
            var titleMap = existingCategory.Title?.Clone() ?? new QuestbookLocalizedText();
            titleMap.Set(lang, title);
            var headerMap = existingCategory.Header?.Clone() ?? new QuestbookLocalizedText();
            string headerDisplay = string.IsNullOrWhiteSpace(request.HeaderTitle)
                ? title.ToUpperInvariant()
                : request.HeaderTitle.Trim();
            if (headerDisplay.Length > MaxCategoryTitleLength)
            {
                headerDisplay = headerDisplay[..MaxCategoryTitleLength];
            }

            headerMap.Set(lang, headerDisplay);

            questDatabase.Categories[categoryIndex] = new QuestbookCategoryData
            {
                IconItemCode = iconItemCode,
                Title = titleMap,
                HeaderTitle = existingCategory.HeaderTitle,
                Header = headerMap,
                Nodes = existingCategory.Nodes,
                Connections = existingCategory.Connections
            };

            SaveQuestData();
            BroadcastQuestsToAllPlayers();

            SendAdminResponse(fromPlayer, true, "Category renamed", existingCategory.HeaderTitle);
            sapi?.Logger.Notification(
                "[SwixyQuestBook] Admin {0} renamed category display for {1} (lang={2})",
                fromPlayer.PlayerName, existingCategory.HeaderTitle, lang);
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
            BroadcastQuestsToAllPlayers();
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
                    2 => "Checkpoint",
                    1 => "Quest",
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

                nodes.Add(new QuestbookQuestNodeData
                {
                    Id = n.Id,
                    X = n.X,
                    Y = n.Y,
                    NodeType = nodeType,
                    Description = descriptionText,
                    RequiredItems = required.Select(i => new QuestbookQuestItemData(i.CollectibleCode, i.Count)).ToArray(),
                    RewardItems = rewards.Select(i => new QuestbookQuestItemData(i.CollectibleCode, i.Count)).ToArray()
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
            bool allowWildcards)
        {
            if (items == null)
            {
                return [];
            }

            return SanitizeItemList(
                items.Select(i => new QuestbookQuestItemRequirement(i.CollectibleCode ?? string.Empty, i.Count)).ToArray(),
                allowWildcards);
        }

        private QuestbookQuestItemRequirement[] SanitizeItemList(
            IEnumerable<QuestbookQuestItemStackPacket>? items,
            bool allowWildcards)
        {
            if (items == null)
            {
                return [];
            }

            return SanitizeItemList(
                items.Select(i => new QuestbookQuestItemRequirement(i.CollectibleCode ?? string.Empty, i.Count)).ToArray(),
                allowWildcards);
        }

        private QuestbookQuestItemRequirement[] SanitizeItemList(
            IEnumerable<QuestbookSyncItemPacket>? items,
            bool allowWildcards)
        {
            if (items == null)
            {
                return [];
            }

            return SanitizeItemList(
                items.Select(i => new QuestbookQuestItemRequirement(i.CollectibleCode ?? string.Empty, i.Count)).ToArray(),
                allowWildcards);
        }

        private QuestbookQuestItemRequirement[] SanitizeItemList(
            QuestbookQuestItemRequirement[] items,
            bool allowWildcards)
        {
            var result = new List<QuestbookQuestItemRequirement>(Math.Min(items.Length, MaxItemsPerList));
            foreach (var item in items)
            {
                if (result.Count >= MaxItemsPerList)
                {
                    break;
                }

                string code = NormalizeCollectibleCode(item.CollectibleCode, allowWildcards);
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                int count = item.Count;
                if (count < 1)
                {
                    count = 1;
                }
                else if (count > MaxItemStackCount)
                {
                    count = MaxItemStackCount;
                }

                // Rewards must resolve to a real item/block (no wildcards).
                if (!allowWildcards && !IsValidIconItemCode(code))
                {
                    continue;
                }

                result.Add(new QuestbookQuestItemRequirement(code, count));
            }

            return result.ToArray();
        }

        private static string NormalizeCollectibleCode(string? code, bool allowWildcards)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            string trimmed = code.Trim();
            if (trimmed.Length > MaxCollectibleCodeLength)
            {
                return string.Empty;
            }

            // Reject path tricks / injection-ish payloads.
            if (trimmed.Contains("..", StringComparison.Ordinal)
                || trimmed.Contains('\\', StringComparison.Ordinal)
                || trimmed.Contains('\0'))
            {
                return string.Empty;
            }

            if (!allowWildcards && trimmed.Contains('*', StringComparison.Ordinal))
            {
                return string.Empty;
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                bool ok = char.IsLetterOrDigit(c)
                    || c is ':' or '-' or '_' or '*' or '/' or '.';
                if (!ok)
                {
                    return string.Empty;
                }
            }

            return trimmed;
        }

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

        private static string NormalizeIconItemCode(string? iconItemCode)
        {
            return iconItemCode?.Trim() ?? string.Empty;
        }

        private bool IsValidIconItemCode(string iconItemCode)
        {
            if (sapi?.World == null || string.IsNullOrWhiteSpace(iconItemCode))
                return false;

            var location = new AssetLocation(iconItemCode);
            return sapi.World.GetItem(location) != null || sapi.World.GetBlock(location) != null;
        }

        #endregion
    }
}
