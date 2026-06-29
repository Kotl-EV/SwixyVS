// =============================================================================
// SwixyClaimChunkMod.cs
// -----------------------------------------------------------------------------
// Точка входа мода приватов: клиент (GUI, горячая клавиша, команды) и сервер
// (клейм/анклейм чанков, управление участниками, подсветка в мире).
// Сетевой канал SwixyClaimChunk связывает ClaimMapDialog с серверной логикой LandClaim.
// Пакетные операции поддерживают прямоугольное выделение и связные области;
// соседние приваты одного игрока автоматически сливаются.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Content;
using SwixyClaimChunk.Net;
using ProtoBuf;
using static SwixyClaimChunk.Content.ClaimVolumeUtil;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

[assembly: ModDependency("game", "1.22.0")]
[assembly: ModInfo(
    "SwixyClaimChunk",
    "swixyclaimchunk",
    Website = "https://github.com/tehtelev/Swixy",
    Description = "Chunk claim map interface.",
    Version = "1.0.0",
    Authors =
    [
        "Tehtelev",
        "Kotl"
    ]
)]

namespace SwixyClaimChunk;

/// <summary>
/// ModSystem мода карт приватов: клиентский диалог и серверная логика LandClaim.
/// </summary>
public sealed class SwixyClaimChunkMod : ModSystem
{
    #region Константы и поля

    /// <summary>Имя сетевого канала для всех пакетов мода.</summary>
    private const string ChannelName = "SwixyClaimChunk";

    /// <summary>Код хоткея открытия GUI карты приватов; по нему игра сохраняет переназначение клавиши.</summary>
    public const string OpenMapHotkeyCode = "swixyclaimchunkopenmap";

    /// <summary>Привилегия для команд /claimmap и /privatemap.</summary>
    private static readonly string CommandPrivilege = Privilege.chat;

    /// <summary>Радиус окна карты по умолчанию (в чанках).</summary>
    private const int DefaultRadius = 10;

    /// <summary>Максимальный радиус, который сервер отдаёт клиенту.</summary>
    private const int MaxRadius = 32;

    /// <summary>Уровень защиты новых LandClaim (как в ванильном клейме).</summary>
    private const int ProtectionLevel = 1;

    /// <summary>Клиентский API; null на сервере.</summary>
    private ICoreClientAPI? clientApi;

    /// <summary>Серверный API; null на клиенте.</summary>
    private ICoreServerAPI? serverApi;

    /// <summary>Клиентский канал SwixyClaimChunk.</summary>
    private IClientNetworkChannel? clientChannel;

    /// <summary>Серверный канал SwixyClaimChunk.</summary>
    private IServerNetworkChannel? serverChannel;

    /// <summary>Единственный экземпляр диалога карты приватов.</summary>
    private ClaimMapDialog? dialog;

    /// <summary>Ключ в SaveGame для списков со-владельцев по приватам.</summary>
    private const string CoOwnersSaveKey = "swixyclaimchunk_coowners";

    /// <summary>UID со-владельцев по стабильному ключу привата (отдельно от Use/Build).</summary>
    private readonly Dictionary<string, HashSet<string>> coOwnerUidsByClaimKey = new(StringComparer.Ordinal);

    #endregion

    #region Клиент — инициализация и диалог

    /// <summary>Мод загружается и на клиенте, и на сервере.</summary>
    public override bool ShouldLoad(EnumAppSide forSide) => true;

    /// <summary>Регистрирует канал, горячую клавишу P и команды открытия карты.</summary>
    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        api.Logger.Notification("Swixy Claim Chunk client side starting.");

        clientApi = api;
        clientChannel = api.Network
            .RegisterChannel(ChannelName)
            .RegisterMessageType<ClaimMapRequestPacket>()
            .RegisterMessageType<ClaimChunkActionPacket>()
            .RegisterMessageType<ClaimChunksBatchActionPacket>()
            .RegisterMessageType<ClaimMapStatePacket>()
            .RegisterMessageType<ClaimListRequestPacket>()
            .RegisterMessageType<ClaimShowRequestPacket>()
            .RegisterMessageType<ClaimShowStatePacket>()
            .RegisterMessageType<ClaimAccessActionPacket>()
            .RegisterMessageType<ClaimListStatePacket>()
            .SetMessageHandler<ClaimMapStatePacket>(OnMapStatePacket)
            .SetMessageHandler<ClaimListStatePacket>(OnClaimListStatePacket)
            .SetMessageHandler<ClaimShowStatePacket>(OnClaimShowStatePacket);

        api.Logger.Notification("[SwixyClaimChunk] Client claim channel registered");

        api.Input.RegisterHotKey(
            OpenMapHotkeyCode,
            Lang.Get("swixyclaimchunk:open-map-hotkey"),
            GlKeys.P,
            HotkeyType.GUIOrOtherControls,
            false,
            false,
            false);
        api.Input.SetHotKeyHandler(OpenMapHotkeyCode, _ =>
        {
            ToggleDialog();
            return true;
        });

        api.ChatCommands.Create("claimmap")
            .WithDescription(Lang.Get("swixyclaimchunk:claim-map-command-desc"))
            .RequiresPrivilege(CommandPrivilege)
            .HandleWith(OpenClaimMapCommand);
        api.ChatCommands.Create("privatemap")
            .WithDescription(Lang.Get("swixyclaimchunk:claim-map-command-desc"))
            .RequiresPrivilege(CommandPrivilege)
            .HandleWith(OpenClaimMapCommand);
    }

    #endregion

    #region Сервер — инициализация

    /// <summary>Регистрирует серверный канал и обработчики пакетов от клиентов.</summary>
    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        api.Logger.Notification("Swixy Claim Chunk server side starting.");

        serverApi = api;
        serverChannel = api.Network
            .RegisterChannel(ChannelName)
            .RegisterMessageType<ClaimMapRequestPacket>()
            .RegisterMessageType<ClaimChunkActionPacket>()
            .RegisterMessageType<ClaimChunksBatchActionPacket>()
            .RegisterMessageType<ClaimMapStatePacket>()
            .RegisterMessageType<ClaimListRequestPacket>()
            .RegisterMessageType<ClaimShowRequestPacket>()
            .RegisterMessageType<ClaimShowStatePacket>()
            .RegisterMessageType<ClaimAccessActionPacket>()
            .RegisterMessageType<ClaimListStatePacket>()
            .SetMessageHandler<ClaimMapRequestPacket>(OnMapRequest)
            .SetMessageHandler<ClaimChunkActionPacket>(OnChunkAction)
            .SetMessageHandler<ClaimChunksBatchActionPacket>(OnChunksBatchAction)
            .SetMessageHandler<ClaimListRequestPacket>(OnClaimListRequest)
            .SetMessageHandler<ClaimShowRequestPacket>(OnClaimShowRequest)
            .SetMessageHandler<ClaimAccessActionPacket>(OnClaimAccessAction);

        api.Event.SaveGameLoaded += OnCoOwnersSaveGameLoaded;
        api.Event.GameWorldSave += OnCoOwnersSaveGameSaving;

        api.Logger.Notification("[SwixyClaimChunk] Server claim channel registered");
    }

    /// <summary>Загружает со-владельцев из SaveGame после старта мира.</summary>
    private void OnCoOwnersSaveGameLoaded()
    {
        coOwnerUidsByClaimKey.Clear();
        var data = serverApi?.WorldManager.SaveGame.GetData(CoOwnersSaveKey);
        if (data == null)
        {
            return;
        }

        var saved = SerializerUtil.Deserialize<CoOwnerSaveData>(data);
        if (saved?.Entries == null)
        {
            return;
        }

        foreach (var entry in saved.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null || entry.Value.Count == 0)
            {
                continue;
            }

            coOwnerUidsByClaimKey[entry.Key] = new HashSet<string>(entry.Value, StringComparer.Ordinal);
        }
    }

    /// <summary>Сохраняет со-владельцев в SaveGame.</summary>
    private void OnCoOwnersSaveGameSaving()
    {
        if (serverApi == null)
        {
            return;
        }

        var payload = new CoOwnerSaveData();
        foreach (var entry in coOwnerUidsByClaimKey)
        {
            if (entry.Value.Count == 0)
            {
                continue;
            }

            payload.Entries[entry.Key] = entry.Value.ToList();
        }

        serverApi.WorldManager.SaveGame.StoreData(CoOwnersSaveKey, SerializerUtil.Serialize(payload));
    }

    /// <summary>Освобождает диалог и ссылки на API при выгрузке мода.</summary>
    public override void Dispose()
    {
        dialog?.Dispose();
        dialog = null;
        clientApi = null;
        serverApi = null;
        clientChannel = null;
        serverChannel = null;
        base.Dispose();
    }

    /// <summary>Переключает открытие/закрытие диалога по горячей клавише.</summary>
    private void ToggleDialog()
    {
        if (dialog?.IsOpened() == true)
        {
            dialog.TryClose();
            return;
        }

        OpenDialog();
    }

    /// <summary>Создаёт или открывает ClaimMapDialog; при повторном вызове — RequestRefresh.</summary>
    private bool OpenDialog()
    {
        if (clientApi == null || clientChannel == null)
        {
            return false;
        }

        try
        {
            dialog ??= new ClaimMapDialog(clientApi, clientChannel);

            if (!dialog.IsOpened())
            {
                dialog.TryOpen();
            }
            else
            {
                dialog.RequestRefresh();
            }

            return dialog.IsOpened();
        }
        catch (Exception exception)
        {
            clientApi.Logger.Error(exception);
            clientApi.ShowChatMessage("Failed to open claim map. Check client log for details.");
            return false;
        }
    }

    /// <summary>Обработчик /claimmap и /privatemap.</summary>
    private TextCommandResult OpenClaimMapCommand(TextCommandCallingArgs args)
    {
        return OpenDialog()
            ? TextCommandResult.Success("Opening claim map.", null)
            : TextCommandResult.Error("Failed to open claim map.");
    }

    #endregion

    #region Клиент — обработчики входящих пакетов

    /// <summary>Применяет снимок карты к открытому диалогу.</summary>
    private void OnMapStatePacket(ClaimMapStatePacket packet)
    {
        clientApi?.Logger.Notification(
            "[SwixyClaimChunk] Received state: chunks={0} message='{1}'",
            packet.Chunks?.Count ?? 0,
            packet.Message ?? "");

        if (dialog == null || !dialog.IsOpened())
        {
            clientApi?.Logger.Warning("[SwixyClaimChunk] State packet ignored because dialog is closed");
            return;
        }

        dialog.ApplyState(packet);
    }

    /// <summary>Обновляет список приватов в открытом диалоге.</summary>
    private void OnClaimListStatePacket(ClaimListStatePacket packet)
    {
        if (dialog == null || !dialog.IsOpened())
        {
            return;
        }

        dialog.ApplyClaimList(packet);
    }

    /// <summary>Синхронизирует состояние подсветки привата в мире.</summary>
    private void OnClaimShowStatePacket(ClaimShowStatePacket packet)
    {
        if (dialog == null || !dialog.IsOpened())
        {
            return;
        }

        dialog.ApplyClaimShow(packet);
    }

    #endregion

    #region Сервер — обработчики входящих пакетов

    /// <summary>Клиент запросил снимок карты вокруг centerChunk и radius.</summary>
    private void OnMapRequest(IServerPlayer fromPlayer, ClaimMapRequestPacket packet)
    {
        try
        {
            serverApi?.Logger.Notification(
                "[SwixyClaimChunk] Server received map request from {0} center={1},{2} radius={3}",
                fromPlayer.PlayerName,
                packet.CenterChunkX,
                packet.CenterChunkZ,
                packet.Radius);
            SendState(fromPlayer, packet.CenterChunkX, packet.CenterChunkZ, packet.Radius, "", 0);
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error("Claim map request failed for {0}: {1}", fromPlayer.PlayerName, exception);
        }
    }

    /// <summary>Клик по одному чанку на карте — toggle claim/unclaim.</summary>
    private void OnChunkAction(IServerPlayer fromPlayer, ClaimChunkActionPacket packet)
    {
        serverApi?.Logger.Notification(
            "[SwixyClaimChunk] Server received ClaimChunkActionPacket from {0} chunk={1},{2}",
            fromPlayer.PlayerName,
            packet.ChunkX,
            packet.ChunkZ);

        try
        {
            var result = ToggleChunkClaim(fromPlayer, packet.ChunkX, packet.ChunkZ);
            serverApi?.Logger.Notification(
                "[SwixyClaimChunk] ToggleChunkClaim result for {0}: type={1} message='{2}'",
                fromPlayer.PlayerName,
                result.MessageType,
                result.Message);

            SendState(fromPlayer, packet.CenterChunkX, packet.CenterChunkZ, packet.Radius, result.Message, result.MessageType);
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error("[SwixyClaimChunk] Claim action failed for {0}: {1}", fromPlayer.PlayerName, exception);
        }
    }

    /// <summary>Выделение нескольких чанков — пакетный claim и/или unclaim.</summary>
    private void OnChunksBatchAction(IServerPlayer fromPlayer, ClaimChunksBatchActionPacket packet)
    {
        serverApi?.Logger.Notification(
            "[SwixyClaimChunk] Server received ClaimChunksBatchActionPacket from {0} chunks={1}",
            fromPlayer.PlayerName,
            packet.Chunks?.Count ?? 0);

        try
        {
            var result = ProcessChunksBatch(fromPlayer, packet.Chunks ?? []);
            serverApi?.Logger.Notification(
                "[SwixyClaimChunk] Batch claim result for {0}: type={1} message='{2}'",
                fromPlayer.PlayerName,
                result.MessageType,
                result.Message);

            SendState(fromPlayer, packet.CenterChunkX, packet.CenterChunkZ, packet.Radius, result.Message, result.MessageType);
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error("[SwixyClaimChunk] Batch claim action failed for {0}: {1}", fromPlayer.PlayerName, exception);
        }
    }

    /// <summary>Клиент запросил список своих приватов.</summary>
    private void OnClaimListRequest(IServerPlayer fromPlayer, ClaimListRequestPacket packet)
    {
        SendClaimList(fromPlayer, "", 0);
    }

    /// <summary>Вкл/выкл подсветку границ привата в мире для игрока.</summary>
    private void OnClaimShowRequest(IServerPlayer fromPlayer, ClaimShowRequestPacket packet)
    {
        if (packet.Clear)
        {
            SendClaimShowCleared(fromPlayer, packet.ClaimId);
            return;
        }

        SendClaimShow(fromPlayer, packet.ClaimId);
    }

    /// <summary>Действия владельца: участники, переименование, удаление привата.</summary>
    private void OnClaimAccessAction(IServerPlayer fromPlayer, ClaimAccessActionPacket packet)
    {
        try
        {
            var result = ProcessClaimAccessAction(fromPlayer, packet);
            SendClaimList(fromPlayer, result.Message, result.MessageType);
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error("[SwixyClaimChunk] Claim access action failed for {0}: {1}", fromPlayer.PlayerName, exception);
            SendClaimList(fromPlayer, Lang.Get("swixyclaimchunk:error-unknown"), 1);
        }
    }

    #endregion

    #region Управление приватами — доступ участников

    /// <summary>Маршрутизирует ClaimAccessActionPacket по типу действия.</summary>
    private ClaimActionResult ProcessClaimAccessAction(IServerPlayer player, ClaimAccessActionPacket packet)
    {
        if (serverApi == null)
        {
            return ClaimActionResult.Error("Server API is not ready.");
        }

        if (!TryGetClaimById(packet.ClaimId, out var claim) || !CanManageClaim(claim, player.PlayerUID))
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-not-found"));
        }

        switch (packet.Action)
        {
            case ClaimAccessActionType.Refresh:
                return ClaimActionResult.Success("");
            case ClaimAccessActionType.AddPlayer:
                return TryAddClaimMember(player, claim, packet.PlayerName, packet.PlayerUid, (EnumBlockAccessFlags)packet.AccessFlags);
            case ClaimAccessActionType.RemovePlayer:
                return TryRemoveClaimMember(claim, packet.PlayerName, packet.PlayerUid);
            case ClaimAccessActionType.RenameClaim:
                return TryRenameClaim(claim, packet.ClaimName);
            case ClaimAccessActionType.DeleteClaim:
                if (!IsClaimOwner(claim, player.PlayerUID))
                {
                    return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-coowner-cannot-delete"));
                }

                serverApi.World.Claims.Remove(claim);
                ClearCoOwners(claim);
                return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:claims-message-deleted"));
            case ClaimAccessActionType.UpdateMemberAccess:
                return TryUpdateClaimMemberAccess(claim, packet.PlayerName, packet.PlayerUid, (EnumBlockAccessFlags)packet.AccessFlags);
            case ClaimAccessActionType.GrantCoOwnership:
                if (!IsClaimOwner(claim, player.PlayerUID))
                {
                    return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-owner-only-crown"));
                }

                return TryToggleCoOwnership(claim, packet.PlayerName, packet.PlayerUid);
            default:
                return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-unknown"));
        }
    }

    /// <summary>Добавляет игрока в PermittedPlayerUids с заданными флагами доступа.</summary>
    private ClaimActionResult TryAddClaimMember(IServerPlayer owner, LandClaim claim, string playerName, string playerUid, EnumBlockAccessFlags accessFlags)
    {
        var playerData = ResolvePlayerData(playerName, playerUid);
        if (playerData == null)
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-player-not-found"));
        }

        if (playerData.PlayerUID == owner.PlayerUID)
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-owner-member"));
        }

        claim.PermittedPlayerUids ??= [];
        claim.PermittedPlayerUids[playerData.PlayerUID] = accessFlags;
        TouchClaim(claim);
        return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:claims-message-player-added", ResolvePlayerName(playerData.PlayerUID, playerName)));
    }

    /// <summary>Обновляет флаги Use/Build у существующего участника.</summary>
    private ClaimActionResult TryUpdateClaimMemberAccess(LandClaim claim, string playerName, string playerUid, EnumBlockAccessFlags accessFlags)
    {
        if (!TryResolveMemberUid(claim, playerName, playerUid, out var memberUid))
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-member-not-found"));
        }

        if (memberUid == claim.OwnedByPlayerUid)
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-owner-member"));
        }

        if (claim.PermittedPlayerUids == null || !claim.PermittedPlayerUids.ContainsKey(memberUid))
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-member-not-found"));
        }

        claim.PermittedPlayerUids[memberUid] = accessFlags;
        TouchClaim(claim);
        return ClaimActionResult.Success("");
    }

    /// <summary>Удаляет участника из PermittedPlayerUids.</summary>
    private ClaimActionResult TryRemoveClaimMember(LandClaim claim, string playerName, string playerUid)
    {
        if (!TryResolveMemberUid(claim, playerName, playerUid, out var memberUid))
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-member-not-found"));
        }

        if (memberUid == claim.OwnedByPlayerUid)
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-cannot-remove-owner"));
        }

        if (claim.PermittedPlayerUids == null || !claim.PermittedPlayerUids.Remove(memberUid))
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-member-not-found"));
        }

        RemoveCoOwner(claim, memberUid);
        TouchClaim(claim);
        return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:claims-message-player-removed", ResolvePlayerName(memberUid, playerName)));
    }

    /// <summary>Переключает статус со-владельца (корона); Use/Build не меняются.</summary>
    private ClaimActionResult TryToggleCoOwnership(LandClaim claim, string playerName, string playerUid)
    {
        if (!TryResolveMemberUid(claim, playerName, playerUid, out var memberUid))
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-member-not-found"));
        }

        if (memberUid == claim.OwnedByPlayerUid)
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-owner-member"));
        }

        if (IsCoOwner(claim, memberUid))
        {
            RemoveCoOwner(claim, memberUid);
            TouchClaim(claim);
            return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:claims-message-coowner-revoked", ResolvePlayerName(memberUid, playerName)));
        }

        AddCoOwner(claim, memberUid);
        TouchClaim(claim);
        return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:claims-message-coowner-granted", ResolvePlayerName(memberUid, playerName)));
    }

    /// <summary>Меняет Description привата (отображаемое имя).</summary>
    private ClaimActionResult TryRenameClaim(LandClaim claim, string claimName)
    {
        claimName = claimName.Trim();
        if (string.IsNullOrWhiteSpace(claimName))
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:claims-error-empty-name"));
        }

        claim.Description = claimName;
        TouchClaim(claim);
        return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:claims-message-renamed"));
    }

    /// <summary>Ищет IServerPlayerData по UID или нику (онлайн / last known name).</summary>
    private IServerPlayerData? ResolvePlayerData(string playerName, string playerUid = "")
    {
        playerName = playerName.Trim();
        playerUid = playerUid.Trim();

        if (serverApi == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(playerUid))
        {
            var byUid = serverApi.PlayerData.GetPlayerDataByUid(playerUid);
            if (byUid != null)
            {
                return byUid;
            }
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return null;
        }

        var onlinePlayer = serverApi.World.AllPlayers
            .FirstOrDefault(player => string.Equals(player.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
        if (onlinePlayer != null)
        {
            return serverApi.PlayerData.GetPlayerDataByUid(onlinePlayer.PlayerUID);
        }

        var byName = serverApi.PlayerData.GetPlayerDataByLastKnownName(playerName);
        if (byName != null)
        {
            return byName;
        }

        if (!string.IsNullOrWhiteSpace(playerUid))
        {
            return serverApi.PlayerData.GetPlayerDataByUid(playerUid);
        }

        return null;
    }

    /// <summary>Определяет UID участника привата по UID из пакета или нику.</summary>
    private bool TryResolveMemberUid(LandClaim claim, string playerName, string playerUid, out string memberUid)
    {
        memberUid = playerUid.Trim();
        if (!string.IsNullOrWhiteSpace(memberUid))
        {
            return claim.PermittedPlayerUids?.ContainsKey(memberUid) == true;
        }

        var playerData = ResolvePlayerData(playerName);
        if (playerData == null)
        {
            return false;
        }

        memberUid = playerData.PlayerUID;
        return claim.PermittedPlayerUids?.ContainsKey(memberUid) == true;
    }

    #endregion

    #region Пакетная клейм/анклейм

    /// <summary>Обрабатывает выделение чанков: свободные — claim, свои — unclaim; чужие — unclaim для админа.</summary>
    private ClaimActionResult ProcessChunksBatch(IServerPlayer player, IReadOnlyList<ClaimChunkCoordPacket> chunks)
    {
        if (chunks.Count == 0)
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-unknown"));
        }

        if (serverApi == null)
        {
            return ClaimActionResult.Error("Server API is not ready.");
        }

        if (!IsLandClaimingEnabled())
        {
            return ClaimActionResult.Error("Land claiming is disabled on this world.");
        }

        if (!CanClaimLand(player))
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-no-privilege"));
        }

        // Разделяем чанки по состоянию карты
        var freeChunks = new List<(int ChunkX, int ChunkZ)>();
        var ownChunks = new List<(int ChunkX, int ChunkZ)>();
        var otherChunks = new List<(int ChunkX, int ChunkZ)>();
        var seen = new HashSet<long>();
        var adminUnclaim = CanAdminUnclaimOthers(player);

        foreach (var chunk in chunks)
        {
            var packed = PackChunkCoord(chunk.ChunkX, chunk.ChunkZ);
            if (!seen.Add(packed))
            {
                continue;
            }

            switch (BuildCell(player, chunk.ChunkX, chunk.ChunkZ).State)
            {
                case ClaimChunkCellState.Free:
                    freeChunks.Add((chunk.ChunkX, chunk.ChunkZ));
                    break;
                case ClaimChunkCellState.Own:
                    ownChunks.Add((chunk.ChunkX, chunk.ChunkZ));
                    break;
                case ClaimChunkCellState.Other:
                    if (adminUnclaim)
                    {
                        otherChunks.Add((chunk.ChunkX, chunk.ChunkZ));
                    }
                    else
                    {
                        var ownerName = BuildCell(player, chunk.ChunkX, chunk.ChunkZ).OwnerName;
                        return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-owned-by-other", ownerName ?? "?"));
                    }
                    break;
                default:
                    return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-out-of-world"));
            }
        }

        var claimed = 0;
        var unclaimed = 0;
        var adminUnclaimed = 0;
        string? lastError = null;

        if (freeChunks.Count > 0)
        {
            var claimResult = TryClaimFreeChunksBatch(player, freeChunks);
            if (claimResult.MessageType != 0)
            {
                return claimResult;
            }

            claimed = freeChunks.Count;
        }

        if (ownChunks.Count > 0)
        {
            var unclaimResult = TryUnclaimChunksBatch(player, ownChunks, allowOtherPlayersClaims: false);
            if (unclaimResult.MessageType != 0 && claimed == 0 && otherChunks.Count == 0)
            {
                return unclaimResult;
            }

            if (unclaimResult.MessageType != 0)
            {
                lastError = unclaimResult.Message;
            }
            else
            {
                unclaimed = ownChunks.Count;
            }
        }

        if (otherChunks.Count > 0)
        {
            var adminResult = TryUnclaimChunksBatch(player, otherChunks, allowOtherPlayersClaims: true);
            if (adminResult.MessageType != 0 && claimed == 0 && unclaimed == 0)
            {
                return adminResult;
            }

            if (adminResult.MessageType != 0)
            {
                lastError = adminResult.Message;
            }
            else
            {
                adminUnclaimed = otherChunks.Count;
                serverApi.Logger.Notification(
                    "[SwixyClaimChunk] Admin {0} unclaimed {1} chunks from other players' claims",
                    player.PlayerName,
                    adminUnclaimed);
            }
        }

        if (claimed == 0 && unclaimed == 0 && adminUnclaimed == 0)
        {
            return ClaimActionResult.Error(lastError ?? Lang.Get("swixyclaimchunk:error-unknown"));
        }

        var message = BuildBatchResultMessage(claimed, unclaimed, adminUnclaimed);
        if (!string.IsNullOrWhiteSpace(lastError))
        {
            message = $"{message} {lastError}";
        }

        return ClaimActionResult.Success(message);
    }

    /// <summary>
    /// Клеймит свободные чанки: один чанк, сплошной прямоугольник или связная область.
    /// </summary>
    private ClaimActionResult TryClaimFreeChunksBatch(IServerPlayer player, IReadOnlyList<(int ChunkX, int ChunkZ)> chunks)
    {
        if (chunks.Count == 1)
        {
            if (!TryBuildChunkArea(chunks[0].ChunkX, chunks[0].ChunkZ, out var singleArea))
            {
                return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-out-of-world"));
            }

            return TryAddChunkClaim(player, singleArea);
        }

        if (TryBuildSolidSelectionRectangle(chunks, out var rectangleArea))
        {
            serverApi?.Logger.Notification(
                "[SwixyClaimChunk] Claiming solid rectangle for {0}: {1},{2},{3} to {4},{5},{6}",
                player.PlayerName,
                rectangleArea.X1, rectangleArea.Y1, rectangleArea.Z1,
                rectangleArea.X2, rectangleArea.Y2, rectangleArea.Z2);
            return TryAddChunkClaim(player, rectangleArea);
        }

        return TryAddConnectedChunkAreas(player, chunks);
    }

    /// <summary>
    /// Снимает клейм с чанков; для прямоугольника — одна операция по bounding area.
    /// </summary>
    private ClaimActionResult TryUnclaimChunksBatch(
        IServerPlayer player,
        IReadOnlyList<(int ChunkX, int ChunkZ)> chunks,
        bool allowOtherPlayersClaims)
    {
        if (chunks.Count == 0)
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-unknown"));
        }

        var successMessageKey = allowOtherPlayersClaims
            ? "swixyclaimchunk:message-batch-admin-unclaimed"
            : "swixyclaimchunk:message-batch-unclaimed";

        if (TryBuildSolidSelectionRectangle(chunks, out var rectangleArea))
        {
            var claim = FindIntersectingClaim(rectangleArea);
            if (claim != null && CanUnclaimFromClaim(claim, player.PlayerUID, allowOtherPlayersClaims))
            {
                var result = TryRemoveAreaFromClaim(claim, rectangleArea);
                if (result.MessageType == 0)
                {
                    return ClaimActionResult.Success(Lang.Get(successMessageKey, chunks.Count));
                }
            }
        }

        foreach (var (chunkX, chunkZ) in chunks)
        {
            if (!TryBuildChunkArea(chunkX, chunkZ, out var chunkArea))
            {
                return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-out-of-world"));
            }

            var claim = FindIntersectingClaim(chunkArea);
            if (claim == null || !CanUnclaimFromClaim(claim, player.PlayerUID, allowOtherPlayersClaims))
            {
                return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-cannot-remove"));
            }

            var result = TryRemoveAreaFromClaim(claim, chunkArea);
            if (result.MessageType != 0)
            {
                return result;
            }
        }

        return ClaimActionResult.Success(Lang.Get(successMessageKey, chunks.Count));
    }

    /// <summary>Снимает клейм с собственных чанков.</summary>
    private ClaimActionResult TryUnclaimOwnChunksBatch(IServerPlayer player, IReadOnlyList<(int ChunkX, int ChunkZ)> chunks)
    {
        return TryUnclaimChunksBatch(player, chunks, allowOtherPlayersClaims: false);
    }

    /// <summary>Проверяет, что выделение — сплошной прямоугольник чанков без дыр.</summary>
    private bool TryBuildSolidSelectionRectangle(IReadOnlyList<(int ChunkX, int ChunkZ)> chunks, out Cuboidi area)
    {
        area = null!;
        if (chunks.Count == 0)
        {
            return false;
        }

        var minChunkX = chunks[0].ChunkX;
        var maxChunkX = chunks[0].ChunkX;
        var minChunkZ = chunks[0].ChunkZ;
        var maxChunkZ = chunks[0].ChunkZ;
        var selected = new HashSet<long>(chunks.Count);

        foreach (var (chunkX, chunkZ) in chunks)
        {
            minChunkX = Math.Min(minChunkX, chunkX);
            maxChunkX = Math.Max(maxChunkX, chunkX);
            minChunkZ = Math.Min(minChunkZ, chunkZ);
            maxChunkZ = Math.Max(maxChunkZ, chunkZ);
            selected.Add(PackChunkCoord(chunkX, chunkZ));
        }

        for (var chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
        {
            for (var chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
            {
                if (!selected.Contains(PackChunkCoord(chunkX, chunkZ)))
                {
                    return false;
                }
            }
        }

        return TryBuildChunksBoundingArea(minChunkX, minChunkZ, maxChunkX, maxChunkZ, out area);
    }

    /// <summary>Строит Cuboidi по углам прямоугольника чанков (min/max chunk coords).</summary>
    private bool TryBuildChunksBoundingArea(int minChunkX, int minChunkZ, int maxChunkX, int maxChunkZ, out Cuboidi area)
    {
        area = null!;
        if (!TryBuildChunkArea(minChunkX, minChunkZ, out var minCorner)
            || !TryBuildChunkArea(maxChunkX, maxChunkZ, out var maxCorner))
        {
            return false;
        }

        area = new Cuboidi(
            Math.Min(minCorner.X1, maxCorner.X1),
            Math.Min(minCorner.Y1, maxCorner.Y1),
            Math.Min(minCorner.Z1, maxCorner.Z1),
            Math.Max(minCorner.X2, maxCorner.X2),
            Math.Max(minCorner.Y2, maxCorner.Y2),
            Math.Max(minCorner.Z2, maxCorner.Z2));
        return true;
    }

    /// <summary>
    /// Клеймит несвязный прямоугольник: итеративно добавляет чанки к соседнему привату
    /// или создаёт новый; после — MergeTouchingOwnClaims.
    /// </summary>
    private ClaimActionResult TryAddConnectedChunkAreas(IServerPlayer player, IReadOnlyList<(int ChunkX, int ChunkZ)> chunks)
    {
        var remaining = new HashSet<long>(chunks.Count);
        var areasByChunk = new Dictionary<long, Cuboidi>(chunks.Count);

        foreach (var (chunkX, chunkZ) in chunks)
        {
            if (!TryBuildChunkArea(chunkX, chunkZ, out var area))
            {
                return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-out-of-world"));
            }

            var existing = FindIntersectingClaim(area);
            if (existing != null && existing.OwnedByPlayerUid != player.PlayerUID)
            {
                return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-owned-by-other", ResolveClaimOwnerName(existing)));
            }

            var packed = PackChunkCoord(chunkX, chunkZ);
            remaining.Add(packed);
            areasByChunk[packed] = area;
        }

        var ownClaims = GetOwnClaims(player.PlayerUID).ToList();
        LandClaim? targetClaim = null;
        foreach (var packed in remaining)
        {
            targetClaim = FindAdjacentOwnClaim(ownClaims, areasByChunk[packed]);
            if (targetClaim != null)
            {
                break;
            }
        }

        var createdNewClaim = targetClaim == null;
        if (createdNewClaim)
        {
            var usedVolume = ownClaims.Sum(static claim => (long)claim.SizeXYZ);
            var totalVolume = remaining.Sum(packed => (long)areasByChunk[packed].SizeXYZ);
            var allowance = GetLandClaimAllowance(player);
            if (allowance > 0 && usedVolume + totalVolume > allowance)
            {
                return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-allowance"));
            }

            var usedAreas = ownClaims.Sum(static claim => claim.Areas?.Count ?? 0);
            var maxAreas = GetLandClaimMaxAreas(player);
            if (maxAreas > 0 && usedAreas + 1 > maxAreas)
            {
                return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-areas"));
            }

            var claimIndex = GetNextClaimIndex(player, ownClaims);
            targetClaim = LandClaim.CreateClaim(player, ProtectionLevel);
            targetClaim.Description = BuildClaimName(player, claimIndex);
        }

        // Пока есть чанки — расширяем приват или добавляем отдельные области
        while (remaining.Count > 0)
        {
            var madeProgress = false;
            foreach (var packed in remaining.ToList())
            {
                var area = areasByChunk[packed];
                if (createdNewClaim && targetClaim!.Areas!.Count == 0)
                {
                    var firstError = targetClaim.AddArea(area);
                    if (firstError != EnumClaimError.NoError)
                    {
                        return ClaimActionResult.Error(ClaimErrorText(firstError));
                    }

                    remaining.Remove(packed);
                    madeProgress = true;
                    continue;
                }

                if (TryExpandTouchingArea(targetClaim!, area))
                {
                    remaining.Remove(packed);
                    madeProgress = true;
                    continue;
                }

                if (WouldOverlapAnotherClaim(targetClaim!, area, player.PlayerUID))
                {
                    continue;
                }

                var addError = targetClaim!.AddArea(area);
                if (addError == EnumClaimError.NoError)
                {
                    remaining.Remove(packed);
                    madeProgress = true;
                }
            }

            if (!madeProgress)
            {
                break;
            }

            ConsolidateClaimAreas(targetClaim!);
        }

        if (remaining.Count > 0)
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-batch-not-connected"));
        }

        if (createdNewClaim)
        {
            serverApi!.World.Claims.Add(targetClaim!);
            serverApi.Logger.Notification(
                "[SwixyClaimChunk] Added connected land claim '{0}' for {1} with {2} chunk areas",
                targetClaim!.Description,
                player.PlayerName,
                chunks.Count);
        }
        else
        {
            TouchClaim(targetClaim!);
        }

        MergeTouchingOwnClaims(player, targetClaim!);
        return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:message-batch-claimed", chunks.Count));
    }

    #endregion

    #region Вспомогательные методы — координаты и сообщения

    /// <summary>Упаковывает пару chunkX/chunkZ в long для HashSet.</summary>
    private static long PackChunkCoord(int chunkX, int chunkZ)
    {
        return ((long)chunkX << 32) ^ (uint)chunkZ;
    }

    /// <summary>Локализованное сообщение по итогам пакетной операции.</summary>
    private static string BuildBatchResultMessage(int claimed, int unclaimed, int adminUnclaimed = 0)
    {
        var parts = new List<string>();
        if (claimed > 0)
        {
            parts.Add(Lang.Get("swixyclaimchunk:message-batch-claimed", claimed));
        }

        if (unclaimed > 0)
        {
            parts.Add(Lang.Get("swixyclaimchunk:message-batch-unclaimed", unclaimed));
        }

        if (adminUnclaimed > 0)
        {
            parts.Add(Lang.Get("swixyclaimchunk:message-batch-admin-unclaimed", adminUnclaimed));
        }

        return parts.Count > 0
            ? string.Join(" ", parts)
            : Lang.Get("swixyclaimchunk:error-unknown");
    }

    #endregion

    #region Построение и отправка пакетов

    /// <summary>Отправляет ClaimMapStatePacket после клейма или запроса карты.</summary>
    private void SendState(IServerPlayer player, int centerChunkX, int centerChunkZ, int radius, string message, int messageType)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        radius = Math.Clamp(radius <= 0 ? DefaultRadius : radius, 1, MaxRadius);
        var packet = BuildStatePacket(player, centerChunkX, centerChunkZ, radius, message, messageType);
        if (packet.Chunks.Count == 0)
        {
            serverApi.Logger.Warning("[SwixyClaimChunk] State packet has 0 chunks, not sending to {0}", player.PlayerName);
            return;
        }

        try
        {
            serverApi.Logger.Notification(
                "[SwixyClaimChunk] Sending state to {0}: {1} chunks, message='{2}'",
                player.PlayerName,
                packet.Chunks.Count,
                message);
            serverChannel.SendPacket(packet, [player]);
        }
        catch (Exception exception)
        {
            serverApi.Logger.Error("Failed to send claim map state to {0}: {1}", player.PlayerName, exception);
        }
    }

    /// <summary>Отправляет ClaimListStatePacket со списком приватов игрока.</summary>
    private void SendClaimList(IServerPlayer player, string message, int messageType)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        try
        {
            serverChannel.SendPacket(BuildClaimListPacket(player, message, messageType), [player]);
        }
        catch (Exception exception)
        {
            serverApi.Logger.Error("Failed to send claim list to {0}: {1}", player.PlayerName, exception);
        }
    }

    /// <summary>Включает подсветку привата и отправляет ClaimShowStatePacket.</summary>
    private void SendClaimShow(IServerPlayer player, int claimId)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        try
        {
            serverChannel.SendPacket(BuildClaimShowPacket(player, claimId), [player]);
        }
        catch (Exception exception)
        {
            serverApi.Logger.Error("Failed to send claim highlight to {0}: {1}", player.PlayerName, exception);
        }
    }

    /// <summary>Собирает пакет подсветки; вызывает HighlightClaim на сервере.</summary>
    private ClaimShowStatePacket BuildClaimShowPacket(IServerPlayer player, int claimId)
    {
        var packet = new ClaimShowStatePacket
        {
            ClaimId = claimId,
            Active = true
        };

        if (!TryGetClaimById(claimId, out var claim) || !CanManageClaim(claim, player.PlayerUID))
        {
            packet.Active = false;
            packet.Message = Lang.Get("swixyclaimchunk:claims-error-not-found");
            packet.MessageType = 1;
            return packet;
        }

        HighlightClaim(player, claim);

        foreach (var area in claim.Areas ?? [])
        {
            packet.Areas.Add(new ClaimAreaPacket
            {
                X1 = area.X1,
                Y1 = area.Y1,
                Z1 = area.Z1,
                X2 = area.X2,
                Y2 = area.Y2,
                Z2 = area.Z2
            });
        }

        return packet;
    }

    /// <summary>Снимает подсветку и уведомляет клиент (Active=false).</summary>
    private void SendClaimShowCleared(IServerPlayer player, int claimId)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        ClearClaimHighlight(player);

        try
        {
            serverChannel.SendPacket(new ClaimShowStatePacket
            {
                ClaimId = claimId,
                Active = false
            }, [player]);
        }
        catch (Exception exception)
        {
            serverApi.Logger.Error("Failed to clear claim highlight for {0}: {1}", player.PlayerName, exception);
        }
    }

    #endregion

    #region Подсветка приватов в мире

    /// <summary>Убирает кубы подсветки LandClaim у игрока.</summary>
    private void ClearClaimHighlight(IServerPlayer player)
    {
        serverApi?.World.HighlightBlocks(
            player,
            (int)EnumHighlightSlot.LandClaim,
            [],
            [],
            EnumHighlightBlocksMode.Absolute,
            EnumHighlightShape.Cubes,
            1f);
    }

    /// <summary>Рисует полупрозрачные кубы по всем Areas привата через HighlightBlocks.</summary>
    private void HighlightClaim(IServerPlayer player, LandClaim claim)
    {
        var areas = claim.Areas;
        if (serverApi == null || areas == null)
        {
            return;
        }

        var blocks = new List<BlockPos>(areas.Count * 2);
        var colors = new List<int>(areas.Count);
        var color = ColorUtil.ToRgba(64, 100, 255, 100);

        foreach (var area in areas)
        {
            blocks.Add(new BlockPos(area.X1, area.Y1, area.Z1));
            blocks.Add(new BlockPos(area.X2, area.Y2, area.Z2));
            colors.Add(color);
        }

        serverApi.World.HighlightBlocks(
            player,
            (int)EnumHighlightSlot.LandClaim,
            blocks,
            colors,
            EnumHighlightBlocksMode.Absolute,
            EnumHighlightShape.Cubes,
            1f);
    }

    /// <summary>Собирает список приватов игрока (ClaimId = индекс+1 в World.Claims.All).</summary>
    private ClaimListStatePacket BuildClaimListPacket(IServerPlayer player, string message, int messageType)
    {
        var packet = new ClaimListStatePacket
        {
            Message = message ?? "",
            MessageType = messageType
        };

        var allClaims = serverApi?.World.Claims?.All;
        if (allClaims == null)
        {
            return packet;
        }

        for (var i = 0; i < allClaims.Count; i++)
        {
            var claim = allClaims[i];
            if (!CanManageClaim(claim, player.PlayerUID))
            {
                continue;
            }

            packet.Claims.Add(BuildClaimInfo(i + 1, claim, player.PlayerUID));
        }

        return packet;
    }

    /// <summary>Локализованная строка прав Use/Build для пакета участника.</summary>
    private static string FormatMemberAccessName(EnumBlockAccessFlags flags, bool isOwner = false)
    {
        if (isOwner)
        {
            return Lang.Get("swixyclaimchunk:claims-owner-role");
        }

        var parts = new List<string>();
        if (flags.HasFlag(EnumBlockAccessFlags.Use))
        {
            parts.Add(Lang.Get("swixyclaimchunk:claims-access-use"));
        }

        if (flags.HasFlag(EnumBlockAccessFlags.BuildOrBreak))
        {
            parts.Add(Lang.Get("swixyclaimchunk:claims-access-build"));
        }

        return parts.Count > 0
            ? string.Join(", ", parts)
            : Lang.Get("swixyclaimchunk:claims-access-none");
    }

    /// <summary>Локализованная подпись прав участника с учётом статуса со-владельца.</summary>
    private string FormatMemberAccessName(EnumBlockAccessFlags flags, bool isOwner, bool isCoOwner)
    {
        if (isOwner)
        {
            return Lang.Get("swixyclaimchunk:claims-owner-role");
        }

        if (isCoOwner)
        {
            return Lang.Get("swixyclaimchunk:claims-coowner-role");
        }

        return FormatMemberAccessName(flags);
    }

    /// <summary>Конвертирует LandClaim в ClaimInfoPacket с владельцем и участниками.</summary>
    private ClaimInfoPacket BuildClaimInfo(int claimId, LandClaim claim, string viewerPlayerUid)
    {
        var ownerName = ResolveClaimOwnerName(claim);
        var info = new ClaimInfoPacket
        {
            ClaimId = claimId,
            Name = string.IsNullOrWhiteSpace(claim.Description) ? ownerName : claim.Description,
            OwnerName = ownerName,
            ViewerIsCoOwner = IsCoOwner(claim, viewerPlayerUid),
            AreaCount = claim.Areas?.Count ?? 0,
            Volume = claim.SizeXYZ,
            ChunkCount = BlocksToChunkCount(
                claim.SizeXYZ,
                serverApi!.WorldManager.ChunkSize,
                serverApi.WorldManager.MapSizeY)
        };

        info.Members.Add(new ClaimMemberPacket
        {
            PlayerUid = claim.OwnedByPlayerUid,
            PlayerName = info.OwnerName,
            AccessFlags = (int)(EnumBlockAccessFlags.Use | EnumBlockAccessFlags.BuildOrBreak),
            AccessName = Lang.Get("swixyclaimchunk:claims-owner-role"),
            IsOwner = true
        });

        if (claim.PermittedPlayerUids != null)
        {
            foreach (var entry in claim.PermittedPlayerUids.OrderBy(pair => ResolvePlayerName(pair.Key), StringComparer.OrdinalIgnoreCase))
            {
                info.Members.Add(new ClaimMemberPacket
                {
                    PlayerUid = entry.Key,
                    PlayerName = ResolvePlayerName(entry.Key),
                    AccessFlags = (int)entry.Value,
                    AccessName = FormatMemberAccessName(entry.Value, isOwner: false, IsCoOwner(claim, entry.Key)),
                    IsOwner = false,
                    IsCoOwner = IsCoOwner(claim, entry.Key)
                });
            }
        }

        return info;
    }

    /// <summary>Имя игрока по UID через онлайн-список или PlayerData.LastKnownPlayername.</summary>
    private string ResolvePlayerName(string? playerUid, string? fallbackName = null)
    {
        if (string.IsNullOrWhiteSpace(playerUid))
        {
            return GetNonEmptyPlayerName(fallbackName, "?");
        }

        if (serverApi == null)
        {
            return GetDisplayNameFallback(playerUid, fallbackName);
        }

        var onlinePlayer = serverApi.World.AllPlayers.FirstOrDefault(player => player.PlayerUID == playerUid);
        if (onlinePlayer != null)
        {
            return onlinePlayer.PlayerName;
        }

        var playerDataName = serverApi.PlayerData.GetPlayerDataByUid(playerUid)?.LastKnownPlayername;
        if (!string.IsNullOrWhiteSpace(playerDataName))
        {
            return playerDataName.Trim();
        }

        return GetDisplayNameFallback(playerUid, fallbackName);
    }

    /// <summary>Последний fallback для отображаемого имени без сырого UID в UI.</summary>
    private string GetDisplayNameFallback(string playerUid, string? fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(fallbackName) && !string.Equals(fallbackName, playerUid, StringComparison.Ordinal))
        {
            return fallbackName.Trim();
        }

        return Lang.Get("swixyclaimchunk:claims-unknown-player");
    }

    /// <summary>Имя владельца привата с запасным вариантом из LastKnownOwnerName.</summary>
    private string ResolveClaimOwnerName(LandClaim claim)
    {
        return ResolvePlayerName(claim.OwnedByPlayerUid, claim.LastKnownOwnerName);
    }

    /// <summary>Возвращает первое непустое имя; UID используется последним fallback.</summary>
    private static string GetNonEmptyPlayerName(string? playerName, string fallback)
    {
        return string.IsNullOrWhiteSpace(playerName)
            ? fallback
            : playerName.Trim();
    }

    /// <summary>ClaimId в пакетах — 1-based индекс в World.Claims.All.</summary>
    private bool TryGetClaimById(int claimId, out LandClaim claim)
    {
        claim = null!;
        var allClaims = serverApi?.World.Claims?.All;
        if (allClaims == null || claimId <= 0 || claimId > allClaims.Count)
        {
            return false;
        }

        claim = allClaims[claimId - 1];
        return true;
    }

    /// <summary>Строит сетку чанков вокруг центра и квоты игрока.</summary>
    private ClaimMapStatePacket BuildStatePacket(IServerPlayer player, int centerChunkX, int centerChunkZ, int radius, string message, int messageType)
    {
        var sapi = serverApi!;
        var chunkSize = sapi.WorldManager.ChunkSize;
        if (chunkSize <= 0)
        {
            throw new InvalidOperationException("World chunk size is not available.");
        }

        if (!TryGetPlayerChunk(player, out var playerChunkX, out var playerChunkZ))
        {
            playerChunkX = centerChunkX;
            playerChunkZ = centerChunkZ;
        }

        var ownClaims = GetOwnClaims(player.PlayerUID).ToList();
        var allClaims = sapi.World.Claims?.All?.ToList() ?? [];
        var packet = new ClaimMapStatePacket
        {
            CenterChunkX = centerChunkX,
            CenterChunkZ = centerChunkZ,
            PlayerChunkX = playerChunkX,
            PlayerChunkZ = playerChunkZ,
            Radius = radius,
            ChunkSize = chunkSize,
            MapSizeX = sapi.WorldManager.MapSizeX,
            MapSizeZ = sapi.WorldManager.MapSizeZ,
            MapSizeY = sapi.WorldManager.MapSizeY,
            UsedVolume = ownClaims.Sum(static claim => (long)claim.SizeXYZ),
            MaxVolume = GetLandClaimAllowance(player),
            UsedAreas = ownClaims.Sum(static claim => claim.Areas?.Count ?? 0),
            MaxAreas = GetLandClaimMaxAreas(player),
            CanAdminUnclaimOthers = CanAdminUnclaimOthers(player),
            Message = message ?? "",
            MessageType = messageType
        };

        for (var z = centerChunkZ - radius; z <= centerChunkZ + radius; z++)
        {
            for (var x = centerChunkX - radius; x <= centerChunkX + radius; x++)
            {
                packet.Chunks.Add(BuildCell(player, x, z, allClaims));
            }
        }

        return packet;
    }

    /// <summary>Координаты чанка, в котором стоит игрок (для маркера на карте).</summary>
    private bool TryGetPlayerChunk(IServerPlayer player, out int chunkX, out int chunkZ)
    {
        chunkX = 0;
        chunkZ = 0;

        if (serverApi == null)
        {
            return false;
        }

        var entity = player.Entity;
        var chunkSize = serverApi.WorldManager.ChunkSize;
        if (entity == null || chunkSize <= 0)
        {
            return false;
        }

        var blockPos = entity.Pos.AsBlockPos;
        chunkX = FloorDiv(blockPos.X, chunkSize);
        chunkZ = FloorDiv(blockPos.Z, chunkSize);
        return true;
    }

    /// <summary>Состояние одного чанка для карты (обёртка с загрузкой всех claims).</summary>
    private ClaimChunkCellPacket BuildCell(IServerPlayer player, int chunkX, int chunkZ)
    {
        return BuildCell(player, chunkX, chunkZ, serverApi?.World.Claims?.All?.ToList() ?? []);
    }

    /// <summary>Free / Own / Other / OutOfWorld для чанка по пересечению с LandClaim.</summary>
    private ClaimChunkCellPacket BuildCell(IServerPlayer player, int chunkX, int chunkZ, IReadOnlyList<LandClaim> allClaims)
    {
        if (!TryBuildChunkArea(chunkX, chunkZ, out var area))
        {
            return new ClaimChunkCellPacket
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                State = ClaimChunkCellState.OutOfWorld
            };
        }

        var claimIndex = FindIntersectingClaimIndex(area, allClaims);
        if (claimIndex < 0)
        {
            return new ClaimChunkCellPacket
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                State = ClaimChunkCellState.Free
            };
        }

        var claim = allClaims[claimIndex];
        var ownerName = ResolveClaimOwnerName(claim);
        return new ClaimChunkCellPacket
        {
            ChunkX = chunkX,
            ChunkZ = chunkZ,
            State = PlayerTreatsClaimAsOwn(claim, player.PlayerUID) ? ClaimChunkCellState.Own : ClaimChunkCellState.Other,
            OwnerName = ownerName,
            ClaimId = claimIndex + 1,
            ClaimName = string.IsNullOrWhiteSpace(claim.Description) ? ownerName : claim.Description
        };
    }

    #endregion

    #region Клейм/анклейм одного чанка

    /// <summary>Переключает клейм чанка: добавить или снять с собственного привата.</summary>
    private ClaimActionResult ToggleChunkClaim(IServerPlayer player, int chunkX, int chunkZ)
    {
        if (serverApi == null)
        {
            return ClaimActionResult.Error("Server API is not ready.");
        }

        if (!IsLandClaimingEnabled())
        {
            return ClaimActionResult.Error("Land claiming is disabled on this world.");
        }

        if (!CanClaimLand(player))
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-no-privilege"));
        }

        if (!TryBuildChunkArea(chunkX, chunkZ, out var area))
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-out-of-world"));
        }

        var existing = FindIntersectingClaim(area);
        if (existing != null)
        {
            var adminUnclaim = CanAdminUnclaimOthers(player);
            if (!CanUnclaimFromClaim(existing, player.PlayerUID, adminUnclaim))
            {
                return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-owned-by-other", ResolveClaimOwnerName(existing)));
            }

            if (adminUnclaim && !CanManageClaim(existing, player.PlayerUID))
            {
                serverApi.Logger.Notification(
                    "[SwixyClaimChunk] Admin {0} unclaimed chunk {1},{2} from claim '{3}'",
                    player.PlayerName,
                    chunkX,
                    chunkZ,
                    existing.Description);
            }

            return TryRemoveChunkFromClaim(existing, area);
        }

        return TryAddChunkClaim(player, area);
    }

    /// <summary>
    /// Добавляет область: расширение соседнего, новая area в соседнем привате или новый LandClaim.
    /// Проверяет квоты volume/areas; после — MergeTouchingOwnClaims.
    /// </summary>
    private ClaimActionResult TryAddChunkClaim(IServerPlayer player, Cuboidi area)
    {
        var ownClaims = GetOwnClaims(player.PlayerUID).ToList();
        var usedVolume = ownClaims.Sum(static claim => (long)claim.SizeXYZ);
        var allowance = GetLandClaimAllowance(player);
        if (allowance > 0 && usedVolume + area.SizeXYZ > allowance)
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-allowance"));
        }

        if (TryExpandExistingArea(ownClaims, area, out var expandedClaim, player.PlayerUID))
        {
            MergeTouchingOwnClaims(player, expandedClaim);
            TouchClaim(expandedClaim);
            return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:message-claimed"));
        }

        var usedAreas = ownClaims.Sum(static claim => claim.Areas?.Count ?? 0);
        var maxAreas = GetLandClaimMaxAreas(player);
        var adjacentClaim = FindAdjacentOwnClaim(ownClaims, area);
        if (adjacentClaim != null)
        {
            if (TryExpandTouchingArea(adjacentClaim, area))
            {
                ConsolidateClaimAreas(adjacentClaim);
                MergeTouchingOwnClaims(player, adjacentClaim);
                TouchClaim(adjacentClaim);
                return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:message-claimed"));
            }

            if (maxAreas > 0 && usedAreas + 1 > maxAreas)
            {
                return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-areas"));
            }

            var addAdjacentError = adjacentClaim.AddArea(area);
            if (addAdjacentError != EnumClaimError.NoError)
            {
                return ClaimActionResult.Error(ClaimErrorText(addAdjacentError));
            }

            ConsolidateClaimAreas(adjacentClaim);
            MergeTouchingOwnClaims(player, adjacentClaim);
            TouchClaim(adjacentClaim);
            return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:message-claimed"));
        }

        if (maxAreas > 0 && usedAreas + 1 > maxAreas)
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-areas"));
        }

        // Новый отдельный приват с именем «Claim {ник} {индекс}»
        var claimIndex = GetNextClaimIndex(player, ownClaims);
        var newClaim = LandClaim.CreateClaim(player, ProtectionLevel);
        newClaim.Description = BuildClaimName(player, claimIndex);
        var addError = newClaim.AddArea(area);
        if (addError != EnumClaimError.NoError)
        {
            return ClaimActionResult.Error(ClaimErrorText(addError));
        }

        serverApi!.World.Claims.Add(newClaim);
        MergeTouchingOwnClaims(player, newClaim);
        serverApi.Logger.Notification(
            "[SwixyClaimChunk] Added land claim '{0}' for {1} area={2},{3},{4} to {5},{6},{7}",
            newClaim.Description,
            player.PlayerName,
            area.X1, area.Y1, area.Z1,
            area.X2, area.Y2, area.Z2);
        return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:message-claimed"));
    }

    /// <summary>Ищет свой приват с областью, смежной с chunkArea.</summary>
    private static LandClaim? FindAdjacentOwnClaim(IEnumerable<LandClaim> ownClaims, Cuboidi area)
    {
        foreach (var claim in ownClaims)
        {
            if (claim.Areas == null)
            {
                continue;
            }

            foreach (var existing in claim.Areas)
            {
                if (AreAdjacent(existing, area))
                {
                    return claim;
                }
            }
        }

        return null;
    }

    #endregion

    #region Слияние приватов

    /// <summary>
    /// Пока есть соприкасающиеся приваты того же игрока — поглощает их в anchorClaim.
    /// </summary>
    private void MergeTouchingOwnClaims(IServerPlayer player, LandClaim anchorClaim)
    {
        if (anchorClaim.Areas == null || anchorClaim.Areas.Count == 0)
        {
            return;
        }

        var mergedAny = true;
        while (mergedAny)
        {
            mergedAny = false;
            foreach (var otherClaim in GetOwnClaims(player.PlayerUID).ToList())
            {
                if (ReferenceEquals(otherClaim, anchorClaim) || !ClaimsTouch(anchorClaim, otherClaim))
                {
                    continue;
                }

                AbsorbClaimInto(player, anchorClaim, otherClaim);
                mergedAny = true;
            }
        }

        ConsolidateClaimAreas(anchorClaim);
    }

    /// <summary>
    /// Переносит области other в primary, удаляет other; сохраняет меньший индекс в имени.
    /// </summary>
    private void AbsorbClaimInto(IServerPlayer player, LandClaim primary, LandClaim other)
    {
        if (other.Areas == null || primary.Areas == null)
        {
            return;
        }

        // При слиянии оставляем имя с меньшим номером (Claim Player 1 поглощает Claim Player 3)
        var primaryIndex = TryParseClaimIndex(primary.Description, player.PlayerName);
        var otherIndex = TryParseClaimIndex(other.Description, player.PlayerName);
        if (otherIndex > 0 && (primaryIndex == 0 || otherIndex < primaryIndex))
        {
            primary.Description = BuildClaimName(player, otherIndex);
        }

        foreach (var otherArea in other.Areas.ToList())
        {
            if (TryExpandExistingArea(new[] { primary }, otherArea, out _, player.PlayerUID)
                || TryExpandTouchingArea(primary, otherArea))
            {
                continue;
            }

            if (primary.Areas.Any(existing => existing.Equals(otherArea) || existing.Intersects(otherArea)))
            {
                continue;
            }

            primary.AddArea(otherArea);
        }

        serverApi!.World.Claims.Remove(other);
        MergeCoOwners(primary, other);
        serverApi.Logger.Notification(
            "[SwixyClaimChunk] Merged claim '{0}' into '{1}' for {2}",
            other.Description,
            primary.Description,
            player.PlayerName);
    }

    /// <summary>Объединяет смежные Areas внутри одного LandClaim в один Cuboidi.</summary>
    private void ConsolidateClaimAreas(LandClaim claim)
    {
        if (claim.Areas == null || claim.Areas.Count <= 1)
        {
            return;
        }

        var mergedAny = true;
        while (mergedAny)
        {
            mergedAny = false;
            for (var i = 0; i < claim.Areas.Count; i++)
            {
                for (var j = i + 1; j < claim.Areas.Count; j++)
                {
                    var first = claim.Areas[i];
                    var second = claim.Areas[j];
                    if (!TryCreateExpandedArea(first, second, out var expandedArea)
                        && !TryCreateExpandedArea(second, first, out expandedArea))
                    {
                        continue;
                    }

                    first.Set(expandedArea.X1, expandedArea.Y1, expandedArea.Z1, expandedArea.X2, expandedArea.Y2, expandedArea.Z2);
                    claim.Areas.RemoveAt(j);
                    mergedAny = true;
                    break;
                }

                if (mergedAny)
                {
                    break;
                }
            }
        }
    }

    /// <summary>True, если у двух приватов есть пересекающиеся или смежные области.</summary>
    private static bool ClaimsTouch(LandClaim first, LandClaim second)
    {
        if (first.Areas == null || second.Areas == null)
        {
            return false;
        }

        foreach (var firstArea in first.Areas)
        {
            foreach (var secondArea in second.Areas)
            {
                if (firstArea.Intersects(secondArea) || AreAdjacent(firstArea, secondArea))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Шаблон имени по умолчанию: «Claim {ник} {индекс}».</summary>
    private static string BuildClaimName(IServerPlayer player, int index)
    {
        return $"Claim {player.PlayerName} {index}";
    }

    /// <summary>Следующий свободный индекс по максимуму среди существующих имён игрока.</summary>
    private static int GetNextClaimIndex(IServerPlayer player, IEnumerable<LandClaim> ownClaims)
    {
        var maxIndex = 0;
        foreach (var claim in ownClaims)
        {
            maxIndex = Math.Max(maxIndex, TryParseClaimIndex(claim.Description, player.PlayerName));
        }

        return maxIndex + 1;
    }

    /// <summary>
    /// Извлекает числовой индекс из Description; поддерживает старый формат «{ник} N».
    /// </summary>
    private static int TryParseClaimIndex(string? description, string playerName)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return 0;
        }

        // Новый префикс «Claim {ник} » и legacy «{ник} »
        foreach (var prefix in new[] { $"Claim {playerName} ", $"{playerName} " })
        {
            if (!description.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (int.TryParse(description.AsSpan(prefix.Length), out var index))
            {
                return index;
            }
        }

        return 0;
    }

    #endregion

    #region Геометрия приватов — удаление и расширение

    /// <summary>Проверяет world config allowLandClaiming.</summary>
    private bool IsLandClaimingEnabled()
    {
        return serverApi?.World.Config.GetAsBool("allowLandClaiming", true) != false;
    }

    /// <summary>
    /// Вычитает removeArea из привата: полное совпадение, shrink или разбиение на куски.
    /// </summary>
    private ClaimActionResult TryRemoveAreaFromClaim(LandClaim claim, Cuboidi removeArea)
    {
        if (claim.Areas == null)
        {
            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-cannot-remove"));
        }

        for (var i = 0; i < claim.Areas.Count; i++)
        {
            var area = claim.Areas[i];
            if (!area.Intersects(removeArea))
            {
                continue;
            }

            if (area.Equals(removeArea))
            {
                claim.Areas.RemoveAt(i);
                if (claim.Areas.Count == 0)
                {
                    serverApi!.World.Claims.Remove(claim);
                }
                else
                {
                    ConsolidateClaimAreas(claim);
                    TouchClaim(claim);
                }

                return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:message-unclaimed"));
            }

            if (TryShrinkArea(area, removeArea))
            {
                ConsolidateClaimAreas(claim);
                TouchClaim(claim);
                return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:message-unclaimed"));
            }

            if (TrySubtractAreaFromArea(area, removeArea, out var remainingPieces))
            {
                claim.Areas.RemoveAt(i);
                foreach (var piece in remainingPieces)
                {
                    claim.Areas.Add(piece);
                }

                if (claim.Areas.Count == 0)
                {
                    serverApi!.World.Claims.Remove(claim);
                }
                else
                {
                    ConsolidateClaimAreas(claim);
                    TouchClaim(claim);
                }

                return ClaimActionResult.Success(Lang.Get("swixyclaimchunk:message-unclaimed"));
            }

            return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-cannot-remove"));
        }

        return ClaimActionResult.Error(Lang.Get("swixyclaimchunk:error-cannot-remove"));
    }

    /// <summary>Алиас TryRemoveAreaFromClaim для одного чанка.</summary>
    private ClaimActionResult TryRemoveChunkFromClaim(LandClaim claim, Cuboidi chunkArea)
    {
        return TryRemoveAreaFromClaim(claim, chunkArea);
    }

    /// <summary>
    /// Вырезает removeArea из area (одинаковая высота Y); до четырёх оставшихся прямоугольников.
    /// </summary>
    private static bool TrySubtractAreaFromArea(Cuboidi area, Cuboidi removeArea, out List<Cuboidi> remainingPieces)
    {
        remainingPieces = [];

        if (!area.Intersects(removeArea))
        {
            return false;
        }

        if (area.Y1 != removeArea.Y1 || area.Y2 != removeArea.Y2)
        {
            return false;
        }

        if (removeArea.X1 < area.X1 || removeArea.X2 > area.X2 || removeArea.Z1 < area.Z1 || removeArea.Z2 > area.Z2)
        {
            return false;
        }

        if (removeArea.X1 > area.X1)
        {
            remainingPieces.Add(new Cuboidi(area.X1, area.Y1, area.Z1, removeArea.X1, area.Y2, area.Z2));
        }

        if (removeArea.X2 < area.X2)
        {
            remainingPieces.Add(new Cuboidi(removeArea.X2, area.Y1, area.Z1, area.X2, area.Y2, area.Z2));
        }

        var overlapX1 = Math.Max(area.X1, removeArea.X1);
        var overlapX2 = Math.Min(area.X2, removeArea.X2);

        if (removeArea.Z1 > area.Z1)
        {
            remainingPieces.Add(new Cuboidi(overlapX1, area.Y1, area.Z1, overlapX2, area.Y2, removeArea.Z1));
        }

        if (removeArea.Z2 < area.Z2)
        {
            remainingPieces.Add(new Cuboidi(overlapX1, area.Y1, removeArea.Z2, overlapX2, area.Y2, area.Z2));
        }

        remainingPieces.RemoveAll(static piece => piece.X1 >= piece.X2 || piece.Z1 >= piece.Z2);
        return true;
    }

    /// <summary>Расширяет существующую смежную область вместо добавления новой.</summary>
    private bool TryExpandExistingArea(IEnumerable<LandClaim> ownClaims, Cuboidi chunkArea, out LandClaim expandedClaim, string? ownerPlayerUid = null)
    {
        foreach (var claim in ownClaims)
        {
            if (TryExpandTouchingArea(claim, chunkArea, out var expandedArea, out var expandedExisting)
                && !WouldOverlapAnotherAreaInSameClaim(claim, expandedExisting, expandedArea, chunkArea)
                && !WouldOverlapAnotherClaim(claim, expandedArea, ownerPlayerUid))
            {
                expandedExisting.Set(expandedArea.X1, expandedArea.Y1, expandedArea.Z1, expandedArea.X2, expandedArea.Y2, expandedArea.Z2);
                ConsolidateClaimAreas(claim);
                expandedClaim = claim;
                return true;
            }
        }

        expandedClaim = null!;
        return false;
    }

    /// <summary>Расширяет первую подходящую area в claim и применяет к ней.</summary>
    private static bool TryExpandTouchingArea(LandClaim claim, Cuboidi chunkArea)
    {
        if (!TryExpandTouchingArea(claim, chunkArea, out var expandedArea, out var existing))
        {
            return false;
        }

        existing.Set(expandedArea.X1, expandedArea.Y1, expandedArea.Z1, expandedArea.X2, expandedArea.Y2, expandedArea.Z2);
        return true;
    }

    /// <summary>Ищет area в claim, которую можно расширить до chunkArea по стороне.</summary>
    private static bool TryExpandTouchingArea(LandClaim claim, Cuboidi chunkArea, out Cuboidi expandedArea, out Cuboidi expandedExisting)
    {
        expandedArea = null!;
        expandedExisting = null!;

        if (claim.Areas == null)
        {
            return false;
        }

        foreach (var existing in claim.Areas)
        {
            if (!TryCreateExpandedArea(existing, chunkArea, out var candidate))
            {
                continue;
            }

            expandedArea = candidate;
            expandedExisting = existing;
            return true;
        }

        return false;
    }

    /// <summary>Проверяет смежность по X или Z (одинаковая Y) и строит объединённый Cuboidi.</summary>
    private static bool TryCreateExpandedArea(Cuboidi existing, Cuboidi chunkArea, out Cuboidi expanded)
    {
        expanded = null!;

        if (existing.Y1 != chunkArea.Y1 || existing.Y2 != chunkArea.Y2)
        {
            return false;
        }

        if (existing.Z1 == chunkArea.Z1 && existing.Z2 == chunkArea.Z2)
        {
            if (existing.X2 == chunkArea.X1)
            {
                expanded = new Cuboidi(existing.X1, existing.Y1, existing.Z1, chunkArea.X2, existing.Y2, existing.Z2);
                return true;
            }

            if (chunkArea.X2 == existing.X1)
            {
                expanded = new Cuboidi(chunkArea.X1, existing.Y1, existing.Z1, existing.X2, existing.Y2, existing.Z2);
                return true;
            }
        }

        if (existing.X1 == chunkArea.X1 && existing.X2 == chunkArea.X2)
        {
            if (existing.Z2 == chunkArea.Z1)
            {
                expanded = new Cuboidi(existing.X1, existing.Y1, existing.Z1, existing.X2, existing.Y2, chunkArea.Z2);
                return true;
            }

            if (chunkArea.Z2 == existing.Z1)
            {
                expanded = new Cuboidi(existing.X1, existing.Y1, chunkArea.Z1, existing.X2, existing.Y2, existing.Z2);
                return true;
            }
        }

        return false;
    }

    /// <summary>Уменьшает existing, отрезая chunkArea с края (unclaim одного чанка).</summary>
    private bool TryShrinkArea(Cuboidi existing, Cuboidi chunkArea)
    {
        if (existing.Y1 != chunkArea.Y1 || existing.Y2 != chunkArea.Y2)
        {
            return false;
        }

        if (existing.Z1 == chunkArea.Z1 && existing.Z2 == chunkArea.Z2)
        {
            if (existing.X1 == chunkArea.X1 && existing.X2 > chunkArea.X2)
            {
                existing.X1 = chunkArea.X2;
                return true;
            }

            if (existing.X2 == chunkArea.X2 && existing.X1 < chunkArea.X1)
            {
                existing.X2 = chunkArea.X1;
                return true;
            }
        }

        if (existing.X1 == chunkArea.X1 && existing.X2 == chunkArea.X2)
        {
            if (existing.Z1 == chunkArea.Z1 && existing.Z2 > chunkArea.Z2)
            {
                existing.Z1 = chunkArea.Z2;
                return true;
            }

            if (existing.Z2 == chunkArea.Z2 && existing.Z1 < chunkArea.Z1)
            {
                existing.Z2 = chunkArea.Z1;
                return true;
            }
        }

        return false;
    }

    /// <summary>Проверяет пересечение с чужими приватами (свои другого claim — пропуск при merge).</summary>
    private bool WouldOverlapAnotherClaim(LandClaim ownClaim, Cuboidi area, string? ownerPlayerUid = null)
    {
        foreach (var claim in serverApi!.World.Claims.All)
        {
            if (ReferenceEquals(claim, ownClaim))
            {
                continue;
            }

            if (ownerPlayerUid != null && claim.OwnedByPlayerUid == ownerPlayerUid)
            {
                continue;
            }

            if (claim.Intersects(area))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Расширение не должно наехать на несмежную area того же привата.</summary>
    private static bool WouldOverlapAnotherAreaInSameClaim(LandClaim claim, Cuboidi originalArea, Cuboidi expandedArea, Cuboidi chunkArea)
    {
        if (claim.Areas == null)
        {
            return false;
        }

        foreach (var area in claim.Areas)
        {
            if (ReferenceEquals(area, originalArea) || !area.Intersects(expandedArea))
            {
                continue;
            }

            if (AreAdjacent(area, chunkArea) || AreAdjacent(area, originalArea))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>Две области на одной высоте Y соприкасаются по грани X или Z.</summary>
    private static bool AreAdjacent(Cuboidi first, Cuboidi second)
    {
        if (first.Y1 != second.Y1 || first.Y2 != second.Y2)
        {
            return false;
        }

        var touchesX = first.X2 == second.X1 || second.X2 == first.X1;
        var overlapsZ = first.Z1 < second.Z2 && second.Z1 < first.Z2;
        if (touchesX && overlapsZ)
        {
            return true;
        }

        var touchesZ = first.Z2 == second.Z1 || second.Z2 == first.Z1;
        var overlapsX = first.X1 < second.X2 && second.X1 < first.X2;
        return touchesZ && overlapsX;
    }

    /// <summary>Перемещает claim в конец списка World.Claims (обновление порядка/сохранения).</summary>
    private void TouchClaim(LandClaim claim)
    {
        var claims = serverApi!.World.Claims;
        if (claims.Remove(claim))
        {
            claims.Add(claim);
        }
    }

    /// <summary>Все LandClaim, принадлежащие игроку по UID.</summary>
    private IEnumerable<LandClaim> GetOwnClaims(string playerUid)
    {
        var claims = serverApi?.World.Claims?.All;
        if (claims == null)
        {
            return [];
        }

        return claims.Where(claim => claim.OwnedByPlayerUid == playerUid);
    }

    /// <summary>Первый приват, пересекающий area; битые claims пропускаются с warning.</summary>
    private LandClaim? FindIntersectingClaim(Cuboidi area)
    {
        var claims = serverApi?.World.Claims?.All;
        if (claims == null)
        {
            return null;
        }

        foreach (var claim in claims)
        {
            try
            {
                if (claim.Intersects(area))
                {
                    return claim;
                }
            }
            catch (Exception exception)
            {
                serverApi?.Logger.Warning("Skipped invalid land claim while building map: {0}", exception.Message);
            }
        }

        return null;
    }

    /// <summary>Индекс в allClaims для BuildCell (быстрее, чем повторный перебор).</summary>
    private int FindIntersectingClaimIndex(Cuboidi area, IReadOnlyList<LandClaim> claims)
    {
        for (var i = 0; i < claims.Count; i++)
        {
            try
            {
                if (claims[i].Intersects(area))
                {
                    return i;
                }
            }
            catch (Exception exception)
            {
                serverApi?.Logger.Warning("Skipped invalid land claim while building map: {0}", exception.Message);
            }
        }

        return -1;
    }

    /// <summary>Cuboidi чанка от y=0 до mapSizeY с учётом границ мира.</summary>
    private bool TryBuildChunkArea(int chunkX, int chunkZ, out Cuboidi area)
    {
        var sapi = serverApi!;
        var chunkSize = sapi.WorldManager.ChunkSize;
        if (chunkSize <= 0)
        {
            area = null!;
            return false;
        }

        long x1 = (long)chunkX * chunkSize;
        long z1 = (long)chunkZ * chunkSize;
        if (x1 < 0 || z1 < 0 || x1 > int.MaxValue || z1 > int.MaxValue)
        {
            area = null!;
            return false;
        }

        var mapSizeX = sapi.WorldManager.MapSizeX;
        var mapSizeZ = sapi.WorldManager.MapSizeZ;
        var mapSizeY = sapi.WorldManager.MapSizeY;
        if (mapSizeX <= 0 || mapSizeZ <= 0 || mapSizeY <= 0)
        {
            area = null!;
            return false;
        }

        var ix1 = (int)x1;
        var iz1 = (int)z1;
        if (ix1 >= mapSizeX || iz1 >= mapSizeZ)
        {
            area = null!;
            return false;
        }

        var x2 = Math.Min(ix1 + chunkSize, mapSizeX);
        var z2 = Math.Min(iz1 + chunkSize, mapSizeZ);
        area = new Cuboidi(ix1, 0, iz1, x2, mapSizeY, z2);
        return true;
    }

    #endregion

    #region Вспомогательные методы — права и квоты

    /// <summary>Сериализуемые данные со-владельцев для SaveGame.</summary>
    [ProtoContract]
    private sealed class CoOwnerSaveData
    {
        [ProtoMember(1)]
        public Dictionary<string, List<string>> Entries { get; set; } = [];
    }

    /// <summary>Стабильный ключ привата для хранения со-владельцев между сохранениями.</summary>
    private static string BuildClaimStorageKey(LandClaim claim)
    {
        if (string.IsNullOrWhiteSpace(claim.OwnedByPlayerUid))
        {
            return "";
        }

        var areas = claim.Areas;
        if (areas == null || areas.Count == 0)
        {
            return claim.OwnedByPlayerUid + ":noarea";
        }

        var minX = areas.Min(static area => area.X1);
        var minY = areas.Min(static area => area.Y1);
        var minZ = areas.Min(static area => area.Z1);
        return $"{claim.OwnedByPlayerUid}:{minX}:{minY}:{minZ}";
    }

    private HashSet<string> GetOrCreateCoOwners(LandClaim claim)
    {
        var key = BuildClaimStorageKey(claim);
        if (!coOwnerUidsByClaimKey.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            coOwnerUidsByClaimKey[key] = set;
        }

        return set;
    }

    private void AddCoOwner(LandClaim claim, string playerUid)
    {
        if (string.IsNullOrWhiteSpace(playerUid))
        {
            return;
        }

        GetOrCreateCoOwners(claim).Add(playerUid);
    }

    private void RemoveCoOwner(LandClaim claim, string playerUid)
    {
        var key = BuildClaimStorageKey(claim);
        if (!coOwnerUidsByClaimKey.TryGetValue(key, out var set))
        {
            return;
        }

        set.Remove(playerUid);
        if (set.Count == 0)
        {
            coOwnerUidsByClaimKey.Remove(key);
        }
    }

    private void ClearCoOwners(LandClaim claim)
    {
        var key = BuildClaimStorageKey(claim);
        coOwnerUidsByClaimKey.Remove(key);
    }

    private void MergeCoOwners(LandClaim primary, LandClaim other)
    {
        var otherKey = BuildClaimStorageKey(other);
        if (!coOwnerUidsByClaimKey.TryGetValue(otherKey, out var otherSet) || otherSet.Count == 0)
        {
            return;
        }

        var primarySet = GetOrCreateCoOwners(primary);
        foreach (var uid in otherSet)
        {
            primarySet.Add(uid);
        }

        coOwnerUidsByClaimKey.Remove(otherKey);
    }

    /// <summary>Игрок — официальный владелец привата.</summary>
    private static bool IsClaimOwner(LandClaim claim, string playerUid)
    {
        return claim.OwnedByPlayerUid == playerUid;
    }

    /// <summary>Игрок — со-владелец (назначен короной, не зависит от Use/Build).</summary>
    private bool IsCoOwner(LandClaim claim, string playerUid)
    {
        if (string.IsNullOrWhiteSpace(playerUid) || IsClaimOwner(claim, playerUid))
        {
            return false;
        }

        var key = BuildClaimStorageKey(claim);
        return coOwnerUidsByClaimKey.TryGetValue(key, out var set) && set.Contains(playerUid);
    }

    /// <summary>Владелец или со-владелец может управлять приватом в GUI.</summary>
    private bool CanManageClaim(LandClaim claim, string playerUid)
    {
        return IsClaimOwner(claim, playerUid) || IsCoOwner(claim, playerUid);
    }

    /// <summary>Чанки привата отображаются как «свои» на карте.</summary>
    private bool PlayerTreatsClaimAsOwn(LandClaim claim, string playerUid)
    {
        return CanManageClaim(claim, playerUid);
    }

    /// <summary>Можно снять клейм с чанка: владелец, со-владелец или админ.</summary>
    private bool CanUnclaimFromClaim(LandClaim claim, string playerUid, bool adminBypass)
    {
        if (adminBypass)
        {
            return true;
        }

        return CanManageClaim(claim, playerUid);
    }

    /// <summary>claimland, controlserver или одиночная игра.</summary>
    private bool CanClaimLand(IServerPlayer player)
    {
        return player.HasPrivilege(Privilege.claimland)
            || player.HasPrivilege(Privilege.controlserver)
            || serverApi?.Server?.IsDedicated == false;
    }

    /// <summary>Снимать приват с чужих чанков на карте — только controlserver.</summary>
    private static bool CanAdminUnclaimOthers(IServerPlayer player)
    {
        return player.HasPrivilege(Privilege.controlserver);
    }

    /// <summary>Лимит объёма приватов (роль + extra).</summary>
    private static long GetLandClaimAllowance(IServerPlayer player)
    {
        var roleAllowance = player.Role?.LandClaimAllowance ?? 0;
        var extraAllowance = player.ServerData?.ExtraLandClaimAllowance ?? 0;
        return (long)roleAllowance + extraAllowance;
    }

    /// <summary>Максимум отдельных областей (роль + extra).</summary>
    private static int GetLandClaimMaxAreas(IServerPlayer player)
    {
        var roleAreas = player.Role?.LandClaimMaxAreas ?? 0;
        var extraAreas = player.ServerData?.ExtraLandClaimAreas ?? 0;
        return roleAreas + extraAreas;
    }

    /// <summary>Деление с округлением вниз для отрицательных координат чанков.</summary>
    private static int FloorDiv(int value, int divisor)
    {
        return (int)Math.Floor((double)value / divisor);
    }

    /// <summary>Локализация ошибок LandClaim.AddArea.</summary>
    private static string ClaimErrorText(EnumClaimError error)
    {
        return error switch
        {
            EnumClaimError.NotAdjacent => Lang.Get("swixyclaimchunk:error-not-adjacent"),
            EnumClaimError.Overlapping => Lang.Get("swixyclaimchunk:error-overlap"),
            _ => Lang.Get("swixyclaimchunk:error-unknown")
        };
    }

    #endregion

    #region ClaimActionResult

    /// <summary>Результат серверной операции: текст и тип (0 — успех, 1 — ошибка) для UI.</summary>
    private readonly struct ClaimActionResult
    {
        /// <summary>Сообщение для клиента (локализованное).</summary>
        public readonly string Message;

        /// <summary>0 — информация/успех, 1 — ошибка (цвет в UI).</summary>
        public readonly int MessageType;

        private ClaimActionResult(string message, int messageType)
        {
            Message = message;
            MessageType = messageType;
        }

        /// <summary>Успешный результат с опциональным сообщением.</summary>
        public static ClaimActionResult Success(string message) => new(message, 0);

        /// <summary>Ошибка с текстом для игрока.</summary>
        public static ClaimActionResult Error(string message) => new(message, 1);
    }

    #endregion
}
