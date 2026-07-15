using SwixyClaimChunk.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkMod"/> — регистрация типов сетевых пакетов.</summary>
public sealed partial class SwixyClaimChunkMod
{
    private static IClientNetworkChannel RegisterClaimPacketTypes(IClientNetworkChannel channel) =>
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

    private static IServerNetworkChannel RegisterClaimPacketTypes(IServerNetworkChannel channel) =>
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
