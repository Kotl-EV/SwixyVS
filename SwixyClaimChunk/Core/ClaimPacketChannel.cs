using SwixyClaimChunk.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace SwixyClaimChunk.Core;

/// <summary>Регистрация protobuf-типов сетевого канала приватов.</summary>
public static class ClaimPacketChannel
{
    /// <summary>Регистрация типов пакетов на клиенте.</summary>
    public static IClientNetworkChannel Register(IClientNetworkChannel channel) =>
        channel
            .RegisterMessageType<ClaimMapRequestPacket>()
            .RegisterMessageType<ClaimChunkActionPacket>()
            .RegisterMessageType<ClaimChunksBatchActionPacket>()
            .RegisterMessageType<ClaimMapStatePacket>()
            .RegisterMessageType<ClaimListRequestPacket>()
            .RegisterMessageType<ClaimShowRequestPacket>()
            .RegisterMessageType<ClaimShowStatePacket>()
            .RegisterMessageType<ClaimAccessActionPacket>()
            .RegisterMessageType<ClaimListStatePacket>()
            .RegisterMessageType<ClaimOpenGuiPacket>()
            .RegisterMessageType<ClaimUseFiltersRequestPacket>()
            .RegisterMessageType<ClaimUseFiltersSyncPacket>()
            .RegisterMessageType<ClaimUseFilterScanRequestPacket>()
            .RegisterMessageType<ClaimUseFilterScanResultPacket>();

    /// <summary>Регистрация типов пакетов на сервере.</summary>
    public static IServerNetworkChannel Register(IServerNetworkChannel channel) =>
        channel
            .RegisterMessageType<ClaimMapRequestPacket>()
            .RegisterMessageType<ClaimChunkActionPacket>()
            .RegisterMessageType<ClaimChunksBatchActionPacket>()
            .RegisterMessageType<ClaimMapStatePacket>()
            .RegisterMessageType<ClaimListRequestPacket>()
            .RegisterMessageType<ClaimShowRequestPacket>()
            .RegisterMessageType<ClaimShowStatePacket>()
            .RegisterMessageType<ClaimAccessActionPacket>()
            .RegisterMessageType<ClaimListStatePacket>()
            .RegisterMessageType<ClaimOpenGuiPacket>()
            .RegisterMessageType<ClaimUseFiltersRequestPacket>()
            .RegisterMessageType<ClaimUseFiltersSyncPacket>()
            .RegisterMessageType<ClaimUseFilterScanRequestPacket>()
            .RegisterMessageType<ClaimUseFilterScanResultPacket>();
}