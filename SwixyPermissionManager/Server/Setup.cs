using SwixyPermissionManager.Core;
using SwixyPermissionManager.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SwixyPermissionManager;

public sealed partial class SwixyPermissionManagerServerMod
{
    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        api.Logger.Notification("[SwixyPermissionManager] Server side starting (vanilla roles shell).");

        serverApi = api;
        serverChannel = PermissionPacketChannel.Register(api.Network.RegisterChannel(PermissionConstants.ChannelName))
            .SetMessageHandler<PermissionStateRequestPacket>(OnStateRequest)
            .SetMessageHandler<PermissionActionPacket>(OnAction);

        api.ChatCommands
            .Create(PermissionConstants.ChatCommand)
            .WithDescription("swixypermissionmanager:command-desc")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(OnPermsCommand);

        api.ChatCommands
            .Create("permissions")
            .WithDescription("swixypermissionmanager:command-desc")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(OnPermsCommand);

        api.ChatCommands
            .Create("roles")
            .WithDescription("swixypermissionmanager:command-desc")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(OnPermsCommand);

        api.Logger.Notification("[SwixyPermissionManager] Server channel registered");
    }

    private TextCommandResult OnPermsCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player)
        {
            return TextCommandResult.Error("Player only.");
        }

        if (!CanManage(player))
        {
            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                Lang.GetL(player.LanguageCode, "swixypermissionmanager:error-no-manage"),
                EnumChatType.CommandError);
            return TextCommandResult.Success();
        }

        serverChannel?.SendPacket(new PermissionOpenGuiPacket(), player);
        SendStateTo(player);
        return TextCommandResult.Success();
    }
}
