using SwixyPermissionManager.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace SwixyPermissionManager.Core;

/// <summary>Регистрация protobuf-типов канала.</summary>
public static class PermissionPacketChannel
{
    public static IClientNetworkChannel Register(IClientNetworkChannel channel) =>
        channel
            .RegisterMessageType<PermissionStateRequestPacket>()
            .RegisterMessageType<PermissionStatePacket>()
            .RegisterMessageType<PermissionOpenGuiPacket>()
            .RegisterMessageType<PermissionActionPacket>()
            .RegisterMessageType<PermissionActionResultPacket>();

    public static IServerNetworkChannel Register(IServerNetworkChannel channel) =>
        channel
            .RegisterMessageType<PermissionStateRequestPacket>()
            .RegisterMessageType<PermissionStatePacket>()
            .RegisterMessageType<PermissionOpenGuiPacket>()
            .RegisterMessageType<PermissionActionPacket>()
            .RegisterMessageType<PermissionActionResultPacket>();
}
