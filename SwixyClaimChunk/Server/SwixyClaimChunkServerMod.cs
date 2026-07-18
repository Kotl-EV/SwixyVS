// =============================================================================
// Серверный ModSystem. Partial-файлы: Server/*
// =============================================================================

using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SwixyClaimChunk;

/// <summary>Серверная логика LandClaim / use-filter / сетевые обработчики.</summary>
public sealed partial class SwixyClaimChunkServerMod : ModSystem
{
    private ICoreServerAPI? serverApi;
    private IServerNetworkChannel? serverChannel;
    private PlayerChatDelegate? serverPlayerChatHandler;
    private readonly Dictionary<string, HashSet<string>> coOwnerUidsByClaimKey = new(StringComparer.Ordinal);
    /// <summary>Серверное хранилище whitelist Use (и SP-источник истины).</summary>
    private readonly Dictionary<string, UseFilterRuleData> useFiltersByClaimKey = new(StringComparer.Ordinal);
    /// <summary>Активные фоновые сканы use-filter (не блокируем тик одним проходом).</summary>
    private readonly Dictionary<string, UseFilterScanJob> activeUseFilterScans = new(StringComparer.Ordinal);
    /// <summary>Флаги привата (PvP, защита животных) по storage-ключу.</summary>
    private readonly Dictionary<string, int> claimFlagsByClaimKey = new(StringComparer.Ordinal);
    /// <summary>Анти-спам сообщений о блокировке урона (uid → elapsed ms).</summary>
    private readonly Dictionary<string, long> damageNotifyCooldown = new(StringComparer.Ordinal);

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void Dispose()
    {
        if (serverApi != null)
        {
            serverApi.Event.SaveGameLoaded -= OnCoOwnersSaveGameLoaded;
            serverApi.Event.SaveGameLoaded -= OnUseFiltersSaveGameLoaded;
            serverApi.Event.SaveGameLoaded -= OnClaimFlagsSaveGameLoaded;
            serverApi.Event.GameWorldSave -= OnCoOwnersSaveGameSaving;
            serverApi.Event.GameWorldSave -= OnUseFiltersSaveGameSaving;
            serverApi.Event.GameWorldSave -= OnClaimFlagsSaveGameSaving;
            serverApi.Event.OnTestBlockAccess -= OnServerTestBlockAccess;
            serverApi.Event.OnTestBlockAccessClaim -= OnServerTestBlockAccessClaim;
            serverApi.Event.PlayerNowPlaying -= OnPlayerJoinSendUseFilters;
            serverApi.Event.OnEntitySpawn -= AttachClaimProtectBehavior;
            serverApi.Event.OnEntityLoaded -= AttachClaimProtectBehavior;
            if (serverPlayerChatHandler != null)
            {
                serverApi.Event.PlayerChat -= serverPlayerChatHandler;
            }
        }

        serverApi = null;
        serverPlayerChatHandler = null;
        serverChannel = null;
        base.Dispose();
    }
}
