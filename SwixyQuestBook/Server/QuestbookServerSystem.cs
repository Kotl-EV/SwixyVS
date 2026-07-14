using SwixyQuestBook.Gui;
using SwixyQuestBook.Helpers;
using SwixyQuestBook.Network;
using SwixyQuestBook.Server;
using System.Text.Json;
using Vintagestory.API.Common;
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
            if (!File.Exists(questsDataPath))
            {
                string modQuestsPath = Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory,
                    "swixyquestbook", QuestsFileName);

                if (File.Exists(modQuestsPath))
                {
                    sapi?.Logger.Notification($"[SwixyQuestBook] Copying default quests from mod: {modQuestsPath}");
                    File.Copy(modQuestsPath, questsDataPath);
                }
                else
                {
                    sapi?.Logger.Warning($"[SwixyQuestBook] No quests.json found at {questsDataPath} or {modQuestsPath}");
                    return;
                }
            }

            try
            {
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

                sapi?.Logger.Notification($"[SwixyQuestBook] Loaded {questDatabase?.Categories.Length ?? 0} quest categories");
            }
            catch (Exception ex)
            {
                sapi?.Logger.Error($"[SwixyQuestBook] Failed to load quest data: {ex.Message}");
            }
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
                string filePath = Path.Combine(playersDataPath, $"{progress.PlayerUid}.json");
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

            var packet = new QuestbookSyncQuestsPacket
            {
                Categories = questDatabase.Categories.Select(c => new QuestbookSyncCategoryPacket
                {
                    IconItemCode = c.IconItemCode,
                    Title = c.Title,
                    HeaderTitle = c.HeaderTitle,
                    Nodes = c.Nodes.Select(n => new QuestbookSyncNodePacket
                    {
                        Id = n.Id,
                        X = n.X,
                        Y = n.Y,
                        NodeType = GetNodeTypeInt(n.NodeType),
                        Description = n.Description,
                        RequiredItems = n.RequiredItems.Select(i => new QuestbookSyncItemPacket { CollectibleCode = i.CollectibleCode, Count = i.Count }).ToArray(),
                        RewardItems = n.RewardItems.Select(i => new QuestbookSyncItemPacket { CollectibleCode = i.CollectibleCode, Count = i.Count }).ToArray()
                    }).ToArray(),
                    Connections = c.Connections.Select(c => new QuestbookSyncConnectionPacket { StartNodeId = c.StartNodeId, EndNodeId = c.EndNodeId }).ToArray()
                }).ToArray()
            };

            serverChannel.SendPacket(packet, player);
            sapi?.Logger.Debug($"[SwixyQuestBook] Sent {questDatabase.Categories.Length} categories to {player.PlayerName}");
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
            if (questDatabase == null)
            {
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            var category = questDatabase.Categories.FirstOrDefault(c => c.HeaderTitle == request.CategoryHeaderTitle);
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
            if (progress.IsQuestCompleted(request.CategoryHeaderTitle, request.NodeId))
            {
                SendQuestSubmitResponse(fromPlayer, request, false);
                return;
            }

            var requiredItems = request.RequiredItems
                .Select(i => new QuestbookQuestItemRequirement(i.CollectibleCode, i.Count))
                .ToArray();
            var rewardItems = request.RewardItems
                .Select(i => new QuestbookQuestItemRequirement(i.CollectibleCode, i.Count))
                .ToArray();

            bool success = QuestbookInventoryHelper.TryConsumeCollectibles(fromPlayer, requiredItems);
            if (success)
            {
                success = QuestbookInventoryHelper.TryGiveCollectibles(fromPlayer, rewardItems);
                fromPlayer.InventoryManager.BroadcastHotbarSlot();

                if (success)
                {
                    sapi?.Logger.Debug($"[SwixyQuestBook] Quest completed: {request.CategoryHeaderTitle}:{request.NodeId}");
                    progress.AddCompletedQuest(request.CategoryHeaderTitle, request.NodeId);
                    SavePlayerProgress(progress);
                    SendProgressToPlayer(fromPlayer, progress);
                }
            }

            SendQuestSubmitResponse(fromPlayer, request, success);
        }

        private void SendQuestSubmitResponse(IServerPlayer player, QuestbookSubmitQuestRequest request, bool success)
        {
            serverChannel?.SendPacket(
                new QuestbookSubmitQuestResponse
                {
                    CategoryHeaderTitle = request.CategoryHeaderTitle,
                    NodeId = request.NodeId,
                    Success = success
                },
                player
            );
        }

        #endregion

        #region Обработка админ-запросов

        private void OnAdminCreateNode(IServerPlayer fromPlayer, QuestbookAdminCreateNodeRequest request)
        {
            if (questDatabase == null)
            {
                SendAdminResponse(fromPlayer, false, "Quest database not loaded");
                return;
            }

            var category = questDatabase.Categories.FirstOrDefault(c => c.HeaderTitle == request.CategoryHeaderTitle);
            if (category == null)
            {
                SendAdminResponse(fromPlayer, false, "Category not found");
                return;
            }

            var parentNode = category.Nodes.FirstOrDefault(n => n.Id == request.ParentNodeId);
            if (parentNode == null && request.NodeType != "Start")
            {
                SendAdminResponse(fromPlayer, false, "Parent node not found");
                return;
            }

            int newId = category.Nodes.Length == 0 ? 0 : category.Nodes.Max(n => n.Id) + 1;

            double x = 0, y = 0;
            if (parentNode != null)
            {
                (x, y) = CalculateNodePosition(parentNode, request.Direction, request.NodeType,
                    request.IsSubQuest, request.SubQuestIndex, request.TotalSubQuests);
            }

            var newNode = new QuestbookQuestNodeData
            {
                Id = newId,
                X = x,
                Y = y,
                NodeType = request.NodeType,
                Description = request.Description,
                RequiredItems = request.RequiredItems.Select(i => new QuestbookQuestItemData(i.CollectibleCode, i.Count)).ToArray(),
                RewardItems = request.RewardItems.Select(i => new QuestbookQuestItemData(i.CollectibleCode, i.Count)).ToArray()
            };

            var nodesList = category.Nodes.ToList();
            nodesList.Add(newNode);
            category.Nodes = nodesList.ToArray();

            if (parentNode != null)
            {
                var connectionsList = category.Connections.ToList();
                connectionsList.Add(new QuestbookQuestConnectionData(parentNode.Id, newId));
                category.Connections = connectionsList.ToArray();
            }

            SaveQuestData();

            BroadcastQuestsToAllPlayers();

            SendAdminResponse(fromPlayer, true, $"Node {newId} created");
            sapi?.Logger.Debug($"[SwixyQuestBook] Admin {fromPlayer.PlayerName} created node {newId} in {request.CategoryHeaderTitle}");
        }

        private void OnAdminDeleteLastNode(IServerPlayer fromPlayer, QuestbookAdminDeleteLastNodeRequest request)
        {
            if (questDatabase == null)
            {
                SendAdminResponse(fromPlayer, false, "Quest database not loaded");
                return;
            }

            var category = questDatabase.Categories.FirstOrDefault(c => c.HeaderTitle == request.CategoryHeaderTitle);
            if (category == null)
            {
                SendAdminResponse(fromPlayer, false, "Category not found");
                return;
            }

            var lastNode = category.Nodes
                .Where(n => n.NodeType != "Start")
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
            sapi?.Logger.Debug($"[SwixyQuestBook] Admin {fromPlayer.PlayerName} deleted node {lastNode.Id} from {request.CategoryHeaderTitle}");
        }

        private void OnAdminSaveCategory(IServerPlayer fromPlayer, QuestbookAdminSaveCategoryRequest request)
        {
            if (questDatabase == null)
            {
                SendAdminResponse(fromPlayer, false, "Quest database not loaded");
                return;
            }

            for (int i = 0; i < questDatabase.Categories.Length; i++)
            {
                if (questDatabase.Categories[i].HeaderTitle == request.CategoryHeaderTitle)
                {
                    questDatabase.Categories[i] = new QuestbookCategoryData
                    {
                        IconItemCode = request.Category.IconItemCode,
                        Title = request.Category.Title,
                        HeaderTitle = request.Category.HeaderTitle,
                        Nodes = request.Category.Nodes.Select(n => new QuestbookQuestNodeData
                        {
                            Id = n.Id,
                            X = n.X,
                            Y = n.Y,
                            NodeType = n.NodeType switch { 0 => "Start", 2 => "Checkpoint", _ => "Quest" },
                            Description = n.Description,
                            RequiredItems = n.RequiredItems.Select(i => new QuestbookQuestItemData(i.CollectibleCode, i.Count)).ToArray(),
                            RewardItems = n.RewardItems.Select(i => new QuestbookQuestItemData(i.CollectibleCode, i.Count)).ToArray()
                        }).ToArray(),
                        Connections = request.Category.Connections.Select(c => new QuestbookQuestConnectionData(c.StartNodeId, c.EndNodeId)).ToArray()
                    };

                    SaveQuestData();
                    BroadcastQuestsToAllPlayers();

                    SendAdminResponse(fromPlayer, true, "Category saved");
                    sapi?.Logger.Debug($"[SwixyQuestBook] Admin {fromPlayer.PlayerName} saved category {request.CategoryHeaderTitle}");
                    return;
                }
            }

            SendAdminResponse(fromPlayer, false, "Category not found");
        }

        private void OnAdminAddCategory(IServerPlayer fromPlayer, QuestbookAdminAddCategoryRequest request)
        {
            if (questDatabase == null)
            {
                SendAdminResponse(fromPlayer, false, "Quest database not loaded");
                return;
            }

            string title = request.Title?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                SendAdminResponse(fromPlayer, false, "Branch title is required");
                return;
            }

            if (title.Length > 80)
            {
                SendAdminResponse(fromPlayer, false, "Branch title is too long");
                return;
            }

            string headerTitle = string.IsNullOrWhiteSpace(request.HeaderTitle)
                ? title.ToUpperInvariant()
                : request.HeaderTitle.Trim();

            if (headerTitle.Length > 80)
            {
                SendAdminResponse(fromPlayer, false, "Branch header is too long");
                return;
            }

            headerTitle = EnsureUniqueHeaderTitle(headerTitle, null);

            string iconItemCode = NormalizeIconItemCode(request.IconItemCode);
            if (!string.IsNullOrWhiteSpace(iconItemCode) && !IsValidIconItemCode(iconItemCode))
            {
                SendAdminResponse(fromPlayer, false, "Invalid branch item icon");
                return;
            }

            var newCategory = new QuestbookCategoryData
            {
                IconItemCode = iconItemCode,
                Title = title,
                HeaderTitle = headerTitle,
                Nodes =
                [
                    new QuestbookQuestNodeData
                    {
                        Id = 0,
                        X = 0,
                        Y = 0,
                        NodeType = "Start",
                        Description = string.Empty
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
            sapi?.Logger.Debug($"[SwixyQuestBook] Admin {fromPlayer.PlayerName} created category {newCategory.HeaderTitle}");
        }

        private void OnAdminRenameCategory(IServerPlayer fromPlayer, QuestbookAdminRenameCategoryRequest request)
        {
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

            if (title.Length > 80)
            {
                SendAdminResponse(fromPlayer, false, "Branch title is too long");
                return;
            }

            string headerTitle = string.IsNullOrWhiteSpace(request.HeaderTitle)
                ? title.ToUpperInvariant()
                : request.HeaderTitle.Trim();

            if (headerTitle.Length > 80)
            {
                SendAdminResponse(fromPlayer, false, "Branch header is too long");
                return;
            }

            var existingCategory = questDatabase.Categories[categoryIndex];
            string oldHeaderTitle = existingCategory.HeaderTitle;
            headerTitle = EnsureUniqueHeaderTitle(headerTitle, oldHeaderTitle);

            string iconItemCode = string.IsNullOrWhiteSpace(request.IconItemCode)
                ? existingCategory.IconItemCode
                : NormalizeIconItemCode(request.IconItemCode);

            if (!string.IsNullOrWhiteSpace(iconItemCode) && !IsValidIconItemCode(iconItemCode))
            {
                SendAdminResponse(fromPlayer, false, "Invalid branch item icon");
                return;
            }

            questDatabase.Categories[categoryIndex] = new QuestbookCategoryData
            {
                IconItemCode = iconItemCode,
                Title = title,
                HeaderTitle = headerTitle,
                Nodes = existingCategory.Nodes,
                Connections = existingCategory.Connections
            };

            bool progressMigrated = false;
            if (!string.Equals(oldHeaderTitle, headerTitle, StringComparison.Ordinal))
            {
                MigrateCategoryHeaderInAllProgress(oldHeaderTitle, headerTitle);
                progressMigrated = true;
            }

            SaveQuestData();
            BroadcastQuestsToAllPlayers();
            if (progressMigrated)
                BroadcastProgressToAllPlayers();

            SendAdminResponse(fromPlayer, true, "Category renamed", headerTitle);
            sapi?.Logger.Debug($"[SwixyQuestBook] Admin {fromPlayer.PlayerName} renamed category {oldHeaderTitle} -> {headerTitle}");
        }

        private void OnAdminDeleteCategory(IServerPlayer fromPlayer, QuestbookAdminDeleteCategoryRequest request)
        {
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
            sapi?.Logger.Debug($"[SwixyQuestBook] Admin {fromPlayer.PlayerName} deleted category {categoryHeaderTitle}");
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
