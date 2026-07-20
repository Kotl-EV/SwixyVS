using System;
using SwixyPermissionManager.Content;
using SwixyPermissionManager.Core;
using SwixyPermissionManager.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixyPermissionManager;

public sealed partial class SwixyPermissionManagerClientMod
{
    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        api.Logger.Notification("[SwixyPermissionManager] Client side starting.");

        clientApi = api;
        clientChannel = PermissionPacketChannel.Register(api.Network.RegisterChannel(PermissionConstants.ChannelName))
            .SetMessageHandler<PermissionStatePacket>(OnStatePacket)
            .SetMessageHandler<PermissionOpenGuiPacket>(_ => OpenDialog())
            .SetMessageHandler<PermissionActionResultPacket>(OnActionResultPacket);

        api.Input.RegisterHotKey(
            PermissionConstants.OpenGuiHotkeyCode,
            Lang.Get("swixypermissionmanager:open-gui-hotkey"),
            GlKeys.F7,
            HotkeyType.GUIOrOtherControls,
            false,
            true,
            false);
        api.Input.SetHotKeyHandler(PermissionConstants.OpenGuiHotkeyCode, _ =>
        {
            ToggleDialog();
            return true;
        });

        api.Logger.Notification("[SwixyPermissionManager] Client channel registered");
    }

    private void ToggleDialog()
    {
        if (dialog?.IsOpened() == true)
        {
            dialog.TryClose();
            return;
        }

        OpenDialog();
    }

    private bool OpenDialog()
    {
        if (clientApi == null || clientChannel == null)
        {
            return false;
        }

        try
        {
            dialog ??= new PermissionManagerDialog(clientApi, clientChannel);

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
            clientApi.ShowChatMessage(Lang.Get("swixypermissionmanager:error-open-failed"));
            return false;
        }
    }

    private void OnStatePacket(PermissionStatePacket packet)
    {
        if (dialog == null || !dialog.IsOpened())
        {
            OpenDialog();
        }

        dialog?.ApplyState(packet);
    }

    private void OnActionResultPacket(PermissionActionResultPacket packet)
    {
        clientApi?.Logger.Notification(
            "[SwixyPermissionManager] ← ActionResult success={0} msg='{1}' hasState={2}",
            packet.Success, packet.Message, packet.State != null);

        if (packet.State != null)
        {
            if (dialog == null || !dialog.IsOpened())
            {
                OpenDialog();
            }

            dialog?.ApplyState(packet.State);
        }

        if (!string.IsNullOrEmpty(packet.Message))
        {
            dialog?.SetStatus(packet.Message, packet.Success ? 0 : 1);
            if (!packet.Success)
            {
                clientApi?.ShowChatMessage(packet.Message);
            }
        }
    }
}
