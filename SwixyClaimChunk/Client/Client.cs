using System;
using SwixyClaimChunk.Content;
using SwixyClaimChunk.Core;
using SwixyClaimChunk.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkClientMod"/> — клиент: GUI и входящие пакеты.</summary>
public sealed partial class SwixyClaimChunkClientMod
{
    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        api.Logger.Notification("Swixy Claim Chunk client side starting.");

        clientApi = api;
        clientChannel = ClaimPacketChannel.Register(api.Network.RegisterChannel(ClaimConstants.ChannelName))
            .SetMessageHandler<ClaimMapStatePacket>(OnMapStatePacket)
            .SetMessageHandler<ClaimListStatePacket>(OnClaimListStatePacket)
            .SetMessageHandler<ClaimShowStatePacket>(OnClaimShowStatePacket)
            .SetMessageHandler<ClaimOpenGuiPacket>(_ => OpenDialog())
            .SetMessageHandler<ClaimUseFiltersSyncPacket>(OnUseFiltersSyncPacket)
            .SetMessageHandler<ClaimUseFilterScanResultPacket>(OnUseFilterScanResultPacket);

        // Отдельные client-handlers: иначе prediction открывает GUI костра при deny на сервере.
        api.Event.OnTestBlockAccess += OnClientTestBlockAccess;
        api.Event.OnTestBlockAccessClaim += OnClientTestBlockAccessClaim;

        // Запрос whitelist сразу после захода в мир (PlayerNowPlaying на сервере может быть рано).
        api.Event.LevelFinalize += RequestUseFiltersFromServer;
        api.Event.RegisterCallback(_ => RequestUseFiltersFromServer(), 1500);

        api.Logger.Notification("[SwixyClaimChunk] Client claim channel registered");

        api.Input.RegisterHotKey(
            ClaimConstants.OpenMapHotkeyCode,
            Lang.Get("swixyclaimchunk:open-map-hotkey"),
            GlKeys.P,
            HotkeyType.GUIOrOtherControls,
            false,
            false,
            false);
        api.Input.SetHotKeyHandler(ClaimConstants.OpenMapHotkeyCode, _ =>
        {
            ToggleDialog();
            return true;
        });
    }

    /// <summary>Переключает открытие/закрытие диалога по горячей клавише.</summary>
    private void ToggleDialog()
    {
        if (dialog?.IsOpened() == true)
        {
            dialog.TryClose();
            return;
        }

        OpenDialog();
    }

    /// <summary>Создаёт или открывает ClaimMapDialog; при повторном вызове — RequestRefresh.</summary>
    private bool OpenDialog()
    {
        if (clientApi == null || clientChannel == null)
        {
            return false;
        }

        try
        {
            dialog ??= new ClaimMapDialog(clientApi, clientChannel);

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
            clientApi.ShowChatMessage(Lang.Get("swixyclaimchunk:error-open-map-failed"));
            return false;
        }
    }

    /// <summary>Применяет снимок карты к открытому диалогу.</summary>
    private void OnMapStatePacket(ClaimMapStatePacket packet)
    {
        clientApi?.Logger.Notification(
            "[SwixyClaimChunk] Received state: chunks={0} message='{1}'",
            packet.Chunks?.Count ?? 0,
            packet.Message ?? "");

        if (dialog == null || !dialog.IsOpened())
        {
            clientApi?.Logger.Warning("[SwixyClaimChunk] State packet ignored because dialog is closed");
            return;
        }

        dialog.ApplyState(packet);
    }

    /// <summary>Обновляет список приватов в открытом диалоге.</summary>
    private void OnClaimListStatePacket(ClaimListStatePacket packet)
    {
        if (dialog == null || !dialog.IsOpened())
        {
            return;
        }

        dialog.ApplyClaimList(packet);
    }

    /// <summary>Синхронизирует состояние подсветки привата в мире.</summary>
    private void OnClaimShowStatePacket(ClaimShowStatePacket packet)
    {
        if (dialog == null || !dialog.IsOpened())
        {
            return;
        }

        dialog.ApplyClaimShow(packet);
    }

    private void RequestUseFiltersFromServer()
    {
        try
        {
            clientChannel?.SendPacket(new ClaimUseFiltersRequestPacket());
        }
        catch (Exception exception)
        {
            clientApi?.Logger.Warning("[SwixyClaimChunk] Use filter request failed: {0}", exception.Message);
        }
    }

    private void OnUseFilterScanResultPacket(ClaimUseFilterScanResultPacket packet)
    {
        dialog?.ApplyUseFilterScanResult(packet);
    }
}
