using SwixySkyBlock.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace SwixySkyBlock.Core;

/// <summary>Регистрация protobuf-типов сетевого канала островов.</summary>
public static class IslandPacketChannel
{
    public static IClientNetworkChannel Register(IClientNetworkChannel channel) =>
        channel
            .RegisterMessageType<IslandHubRequestPacket>()
            .RegisterMessageType<IslandHubStatePacket>()
            .RegisterMessageType<IslandActionPacket>()
            .RegisterMessageType<IslandGeneratorLabelsRequestPacket>()
            .RegisterMessageType<IslandGeneratorLabelsPacket>()
            .RegisterMessageType<IslandGeneratorStateRequestPacket>()
            .RegisterMessageType<IslandGeneratorStatePacket>()
            .RegisterMessageType<IslandGeneratorUpgradeRequestPacket>()
            .RegisterMessageType<IslandTopRequestPacket>()
            .RegisterMessageType<IslandTopStatePacket>()
            .RegisterMessageType<StoryDungeonStateRequestPacket>()
            .RegisterMessageType<StoryDungeonStatePacket>()
            .RegisterMessageType<StoryDungeonTeleportRequestPacket>()
            .RegisterMessageType<IslandClaimListRequestPacket>()
            .RegisterMessageType<IslandClaimAccessActionPacket>()
            .RegisterMessageType<IslandClaimListStatePacket>()
            .RegisterMessageType<IslandClaimShowRequestPacket>()
            .RegisterMessageType<IslandClaimShowStatePacket>()
            .RegisterMessageType<IslandClaimListDeltaPacket>()
            .RegisterMessageType<IslandClaimListFilterPacket>();

    public static IServerNetworkChannel Register(IServerNetworkChannel channel) =>
        channel
            .RegisterMessageType<IslandHubRequestPacket>()
            .RegisterMessageType<IslandHubStatePacket>()
            .RegisterMessageType<IslandActionPacket>()
            .RegisterMessageType<IslandGeneratorLabelsRequestPacket>()
            .RegisterMessageType<IslandGeneratorLabelsPacket>()
            .RegisterMessageType<IslandGeneratorStateRequestPacket>()
            .RegisterMessageType<IslandGeneratorStatePacket>()
            .RegisterMessageType<IslandGeneratorUpgradeRequestPacket>()
            .RegisterMessageType<IslandTopRequestPacket>()
            .RegisterMessageType<IslandTopStatePacket>()
            .RegisterMessageType<StoryDungeonStateRequestPacket>()
            .RegisterMessageType<StoryDungeonStatePacket>()
            .RegisterMessageType<StoryDungeonTeleportRequestPacket>()
            .RegisterMessageType<IslandClaimListRequestPacket>()
            .RegisterMessageType<IslandClaimAccessActionPacket>()
            .RegisterMessageType<IslandClaimListStatePacket>()
            .RegisterMessageType<IslandClaimShowRequestPacket>()
            .RegisterMessageType<IslandClaimShowStatePacket>()
            .RegisterMessageType<IslandClaimListDeltaPacket>()
            .RegisterMessageType<IslandClaimListFilterPacket>();
}
