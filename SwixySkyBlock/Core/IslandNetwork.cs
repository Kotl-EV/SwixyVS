using System;
using System.Collections.Generic;
using SwixySkyBlock.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

public sealed partial class SwixySkyBlockMod
{
    private const string ChannelName = "SwixySkyBlock";
    private const int ProtectionLevel = 1;

    private IClientNetworkChannel? clientChannel;
    private IServerNetworkChannel? serverChannel;
    private readonly PlayerIslandRegistry islandRegistry = new();
    private readonly IslandResidencyRegistry islandResidency = new();
    private readonly Dictionary<string, HashSet<string>> coOwnerUidsByClaimKey = new(StringComparer.Ordinal);

    private bool UsePerPlayerIslands =>
        serverApi?.Server?.IsDedicated == true;

    private static IClientNetworkChannel RegisterIslandPacketTypes(IClientNetworkChannel channel) =>
        channel
            .RegisterMessageType<IslandHubRequestPacket>()
            .RegisterMessageType<IslandHubStatePacket>()
            .RegisterMessageType<IslandActionPacket>()
            .RegisterMessageType<IslandClaimListRequestPacket>()
            .RegisterMessageType<IslandClaimAccessActionPacket>()
            .RegisterMessageType<IslandClaimListStatePacket>()
            .RegisterMessageType<IslandClaimShowRequestPacket>()
            .RegisterMessageType<IslandClaimShowStatePacket>()
            .RegisterMessageType<IslandClaimListDeltaPacket>()
            .RegisterMessageType<IslandClaimListFilterPacket>();

    private static IServerNetworkChannel RegisterIslandPacketTypes(IServerNetworkChannel channel) =>
        channel
            .RegisterMessageType<IslandHubRequestPacket>()
            .RegisterMessageType<IslandHubStatePacket>()
            .RegisterMessageType<IslandActionPacket>()
            .RegisterMessageType<IslandClaimListRequestPacket>()
            .RegisterMessageType<IslandClaimAccessActionPacket>()
            .RegisterMessageType<IslandClaimListStatePacket>()
            .RegisterMessageType<IslandClaimShowRequestPacket>()
            .RegisterMessageType<IslandClaimShowStatePacket>()
            .RegisterMessageType<IslandClaimListDeltaPacket>()
            .RegisterMessageType<IslandClaimListFilterPacket>();
}
