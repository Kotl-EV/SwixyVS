// =============================================================================
// Точка входа ModSystem. Partial-файлы: Core/, Client/, Server/
// =============================================================================

using System;
using System.Collections.Generic;
using SwixyClaimChunk.Content;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

[assembly: ModDependency("game", "1.22.0")]
[assembly: ModInfo(
    "SwixyClaimChunk",
    "swixyclaimchunk",
    Website = "https://github.com/tehtelev/Swixy",
    Description = "Chunk claim map interface.",
    Version = "1.0.4",
    Authors =
    [
        "Tehtelev",
        "Kotl"
    ]
)]

namespace SwixyClaimChunk;

/// <summary>ModSystem мода карт приватов: клиентский диалог и серверная логика LandClaim.</summary>
public sealed partial class SwixyClaimChunkMod : ModSystem
{
    private const string ChannelName = "SwixyClaimChunk";
    public const string OpenMapHotkeyCode = "swixyclaimchunkopenmap";
    private const int DefaultRadius = 10;
    private const int MaxRadius = 32;
    private const int ProtectionLevel = 1;
    private const string CoOwnersSaveKey = "swixyclaimchunk_coowners";
    private const string UseFiltersSaveKey = "swixyclaimchunk_use_filters";
    private ICoreClientAPI? clientApi;
    private ICoreServerAPI? serverApi;
    private IClientNetworkChannel? clientChannel;
    private IServerNetworkChannel? serverChannel;
    private ClaimMapDialog? dialog;
    private PlayerChatDelegate? serverPlayerChatHandler;
    private readonly Dictionary<string, HashSet<string>> coOwnerUidsByClaimKey = new(StringComparer.Ordinal);
    /// <summary>Серверное хранилище whitelist Use (и SP-источник истины).</summary>
    private readonly Dictionary<string, UseFilterRuleData> useFiltersByClaimKey = new(StringComparer.Ordinal);
    /// <summary>Клиентская копия фильтров (MP prediction). Не трогает server dict.</summary>
    private readonly Dictionary<string, UseFilterRuleData> clientUseFiltersByClaimKey = new(StringComparer.Ordinal);

    /// <summary>Активные фоновые сканы use-filter (не блокируем тик одним проходом).</summary>
    private readonly Dictionary<string, UseFilterScanJob> activeUseFilterScans = new(StringComparer.Ordinal);

    public override bool ShouldLoad(EnumAppSide forSide) => true;

    public override void Dispose()
    {
        dialog?.Dispose();
        if (clientApi != null)
        {
            clientApi.Event.OnTestBlockAccess -= OnClientTestBlockAccess;
            clientApi.Event.OnTestBlockAccessClaim -= OnClientTestBlockAccessClaim;
            clientApi.Event.LevelFinalize -= RequestUseFiltersFromServer;
        }

        if (serverApi != null)
        {
            serverApi.Event.SaveGameLoaded -= OnCoOwnersSaveGameLoaded;
            serverApi.Event.SaveGameLoaded -= OnUseFiltersSaveGameLoaded;
            serverApi.Event.GameWorldSave -= OnCoOwnersSaveGameSaving;
            serverApi.Event.GameWorldSave -= OnUseFiltersSaveGameSaving;
            serverApi.Event.OnTestBlockAccess -= OnServerTestBlockAccess;
            serverApi.Event.OnTestBlockAccessClaim -= OnServerTestBlockAccessClaim;
            serverApi.Event.PlayerNowPlaying -= OnPlayerJoinSendUseFilters;
            if (serverPlayerChatHandler != null)
            {
                serverApi.Event.PlayerChat -= serverPlayerChatHandler;
            }
        }

        dialog = null;
        clientApi = null;
        serverApi = null;
        serverPlayerChatHandler = null;
        clientChannel = null;
        serverChannel = null;
        base.Dispose();
    }
}
