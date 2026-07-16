using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Core;
using SwixyClaimChunk.Net;
using ProtoBuf;
using static SwixyClaimChunk.Core.ClaimVolumeUtil;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkServerMod"/> — сервер: инициализация и /land.</summary>
public sealed partial class SwixyClaimChunkServerMod
{
    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        api.Logger.Notification("Swixy Claim Chunk server side starting.");

        serverApi = api;
        serverChannel = ClaimPacketChannel.Register(api.Network.RegisterChannel(ClaimConstants.ChannelName))
            .SetMessageHandler<ClaimMapRequestPacket>(OnMapRequest)
            .SetMessageHandler<ClaimChunkActionPacket>(OnChunkAction)
            .SetMessageHandler<ClaimChunksBatchActionPacket>(OnChunksBatchAction)
            .SetMessageHandler<ClaimListRequestPacket>(OnClaimListRequest)
            .SetMessageHandler<ClaimShowRequestPacket>(OnClaimShowRequest)
            .SetMessageHandler<ClaimAccessActionPacket>(OnClaimAccessAction)
            .SetMessageHandler<ClaimUseFiltersRequestPacket>(OnUseFiltersRequestPacket)
            .SetMessageHandler<ClaimUseFilterScanRequestPacket>(OnUseFilterScanRequestPacket);

        OverrideLandCommand(api.ChatCommands, OpenClaimMapServerCommand);
        api.Event.RegisterCallback(_ => OverrideLandCommand(api.ChatCommands, OpenClaimMapServerCommand), 0);

        api.Event.SaveGameLoaded += OnCoOwnersSaveGameLoaded;
        api.Event.SaveGameLoaded += OnUseFiltersSaveGameLoaded;
        api.Event.GameWorldSave += OnCoOwnersSaveGameSaving;
        api.Event.GameWorldSave += OnUseFiltersSaveGameSaving;
        api.Event.OnTestBlockAccess += OnServerTestBlockAccess;
        api.Event.OnTestBlockAccessClaim += OnServerTestBlockAccessClaim;
        // После полной загрузки клиента — иначе пакет фильтра может потеряться.
        api.Event.PlayerNowPlaying += OnPlayerJoinSendUseFilters;

        serverPlayerChatHandler = (IServerPlayer byPlayer, int channelId, ref string message, ref string data, BoolRef consumed) =>
        {
            if (!IsLandChatMessage(message))
            {
                return;
            }

            consumed.value = true;
            SendOpenGuiPacket(byPlayer);
        };
        api.Event.PlayerChat += serverPlayerChatHandler;

        api.Logger.Notification("[SwixyClaimChunk] Server claim channel registered");
    }

    /// <summary>Подменяет ванильную /land и все её субкоманды: любой вариант открывает GUI.</summary>
    private static void OverrideLandCommand(IChatCommandApi commands, OnCommandDelegate handler)
    {
        var landCommand = commands.Get("land");
        if (landCommand == null)
        {
            return;
        }

        landCommand.WithDescription(Lang.Get("swixyclaimchunk:claim-map-command-desc"));
        OverrideLandCommandTree(landCommand, handler);
    }

    private static void OverrideLandCommandTree(IChatCommand command, OnCommandDelegate handler)
    {
        command
            .HandleWith(handler)
            .IgnoreAdditionalArgs();

        foreach (var subcommand in command.AllSubcommands.Values)
        {
            OverrideLandCommandTree(subcommand, handler);
        }
    }

    private static bool IsLandChatMessage(string message)
    {
        var trimmed = message.TrimStart();
        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        var commandLine = trimmed[1..].TrimStart();
        var commandEnd = commandLine.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        var command = commandEnd >= 0 ? commandLine[..commandEnd] : commandLine;

        if (!command.Equals("land", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private void SendOpenGuiPacket(IServerPlayer player)
    {
        serverChannel?.SendPacket(new ClaimOpenGuiPacket(), player);
    }

    private TextCommandResult OpenClaimMapServerCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is IServerPlayer serverPlayer)
        {
            SendOpenGuiPacket(serverPlayer);
        }

        return TextCommandResult.Success();
    }

    /// <summary>Загружает со-владельцев из SaveGame после старта мира.</summary>
    private void OnCoOwnersSaveGameLoaded()
    {
        coOwnerUidsByClaimKey.Clear();
        var data = serverApi?.WorldManager.SaveGame.GetData(ClaimConstants.CoOwnersSaveKey);
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

        serverApi.WorldManager.SaveGame.StoreData(ClaimConstants.CoOwnersSaveKey, SerializerUtil.Serialize(payload));
    }

}
