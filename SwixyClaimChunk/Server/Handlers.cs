using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Content;
using SwixyClaimChunk.Net;
using ProtoBuf;
using static SwixyClaimChunk.Content.ClaimVolumeUtil;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkMod"/> — сервер: сетевые обработчики.</summary>
public sealed partial class SwixyClaimChunkMod
{
    /// <summary>Клиент запросил снимок карты вокруг centerChunk и radius.</summary>
    private void OnMapRequest(IServerPlayer fromPlayer, ClaimMapRequestPacket packet)
    {
        try
        {
            serverApi?.Logger.Notification(
                "[SwixyClaimChunk] Server received map request from {0} center={1},{2} radius={3}",
                fromPlayer.PlayerName,
                packet.CenterChunkX,
                packet.CenterChunkZ,
                packet.Radius);
            SendState(fromPlayer, packet.CenterChunkX, packet.CenterChunkZ, packet.Radius, "", 0);
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error("Claim map request failed for {0}: {1}", fromPlayer.PlayerName, exception);
        }
    }

    /// <summary>Клик по одному чанку на карте — toggle claim/unclaim.</summary>
    private void OnChunkAction(IServerPlayer fromPlayer, ClaimChunkActionPacket packet)
    {
        serverApi?.Logger.Notification(
            "[SwixyClaimChunk] Server received ClaimChunkActionPacket from {0} chunk={1},{2}",
            fromPlayer.PlayerName,
            packet.ChunkX,
            packet.ChunkZ);

        try
        {
            var result = ToggleChunkClaim(fromPlayer, packet.ChunkX, packet.ChunkZ);
            SendState(fromPlayer, packet.CenterChunkX, packet.CenterChunkZ, packet.Radius, result);
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error("[SwixyClaimChunk] Claim action failed for {0}: {1}", fromPlayer.PlayerName, exception);
        }
    }

    /// <summary>Выделение нескольких чанков — пакетный claim и/или unclaim.</summary>
    private void OnChunksBatchAction(IServerPlayer fromPlayer, ClaimChunksBatchActionPacket packet)
    {
        serverApi?.Logger.Notification(
            "[SwixyClaimChunk] Server received ClaimChunksBatchActionPacket from {0} chunks={1}",
            fromPlayer.PlayerName,
            packet.Chunks?.Count ?? 0);

        try
        {
            var result = ProcessChunksBatch(fromPlayer, packet.Chunks ?? []);
            SendState(fromPlayer, packet.CenterChunkX, packet.CenterChunkZ, packet.Radius, result);
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error("[SwixyClaimChunk] Batch claim action failed for {0}: {1}", fromPlayer.PlayerName, exception);
        }
    }

    /// <summary>Клиент запросил список своих приватов.</summary>
    private void OnClaimListRequest(IServerPlayer fromPlayer, ClaimListRequestPacket packet)
    {
        SendClaimList(fromPlayer, "", 0);
    }

    /// <summary>Вкл/выкл подсветку границ привата в мире для игрока.</summary>
    private void OnClaimShowRequest(IServerPlayer fromPlayer, ClaimShowRequestPacket packet)
    {
        if (packet.Clear)
        {
            SendClaimShowCleared(fromPlayer, packet.ClaimId);
            return;
        }

        SendClaimShow(fromPlayer, packet.ClaimId);
    }

    /// <summary>Действия владельца: участники, переименование, удаление привата.</summary>
    private void OnClaimAccessAction(IServerPlayer fromPlayer, ClaimAccessActionPacket packet)
    {
        try
        {
            var result = ProcessClaimAccessAction(fromPlayer, packet);
            SendClaimList(fromPlayer, result);
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error("[SwixyClaimChunk] Claim access action failed for {0}: {1}", fromPlayer.PlayerName, exception);
            SendClaimList(fromPlayer, ClaimActionResult.Error("swixyclaimchunk:error-unknown"));
        }
    }

}
