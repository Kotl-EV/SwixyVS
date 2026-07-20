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

/// <summary>Часть <see cref="SwixyClaimChunkServerMod"/> — сервер: сетевые обработчики.</summary>
public sealed partial class SwixyClaimChunkServerMod
{
    /// <summary>Клиент запросил снимок карты вокруг centerChunk и radius.</summary>
    private void OnMapRequest(IServerPlayer fromPlayer, ClaimMapRequestPacket packet)
    {
        try
        {
            if (!TryConsumePacketRate(fromPlayer, "map", ClaimConstants.RateMapRequestMs))
            {
                return;
            }

            var cx = packet.CenterChunkX;
            var cz = packet.CenterChunkZ;
            var r = packet.Radius;
            SanitizeMapWindow(ref cx, ref cz, ref r);
            SendState(fromPlayer, cx, cz, r, "", 0);
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error("Claim map request failed for {0}: {1}", fromPlayer.PlayerName, exception);
        }
    }

    /// <summary>Клик по одному чанку на карте — toggle claim/unclaim.</summary>
    private void OnChunkAction(IServerPlayer fromPlayer, ClaimChunkActionPacket packet)
    {
        try
        {
            if (!TryConsumePacketRate(fromPlayer, "chunk", ClaimConstants.RateChunkActionMs))
            {
                return;
            }

            var cx = packet.CenterChunkX;
            var cz = packet.CenterChunkZ;
            var r = packet.Radius;
            SanitizeMapWindow(ref cx, ref cz, ref r);

            var result = ToggleChunkClaim(fromPlayer, packet.ChunkX, packet.ChunkZ);
            SendState(fromPlayer, cx, cz, r, result);
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error("[SwixyClaimChunk] Claim action failed for {0}: {1}", fromPlayer.PlayerName, exception);
        }
    }

    /// <summary>Выделение нескольких чанков — пакетный claim и/или unclaim.</summary>
    private void OnChunksBatchAction(IServerPlayer fromPlayer, ClaimChunksBatchActionPacket packet)
    {
        try
        {
            if (!TryConsumePacketRate(fromPlayer, "batch", ClaimConstants.RateBatchMs))
            {
                return;
            }

            var chunks = SanitizeBatchChunks(packet.Chunks, out var truncated);
            var cx = packet.CenterChunkX;
            var cz = packet.CenterChunkZ;
            var r = packet.Radius;
            SanitizeMapWindow(ref cx, ref cz, ref r);

            var result = ProcessChunksBatch(fromPlayer, chunks);
            if (truncated && result.MessageType == 0)
            {
                // Soft note: only first MaxBatchChunks processed.
                result = ClaimActionResult.SuccessComposite(
                    result.Resolve(fromPlayer) + " "
                    + Lang.GetL(fromPlayer.LanguageCode, "swixyclaimchunk:error-batch-truncated", ClaimConstants.MaxBatchChunks));
            }

            SendState(fromPlayer, cx, cz, r, result);
        }
        catch (Exception exception)
        {
            serverApi?.Logger.Error("[SwixyClaimChunk] Batch claim action failed for {0}: {1}", fromPlayer.PlayerName, exception);
        }
    }

    /// <summary>Клиент запросил список своих приватов.</summary>
    private void OnClaimListRequest(IServerPlayer fromPlayer, ClaimListRequestPacket packet)
    {
        if (!TryConsumePacketRate(fromPlayer, "list", ClaimConstants.RateClaimListMs))
        {
            return;
        }

        SendClaimList(fromPlayer, "", 0);
    }

    /// <summary>Вкл/выкл подсветку границ привата в мире для игрока.</summary>
    private void OnClaimShowRequest(IServerPlayer fromPlayer, ClaimShowRequestPacket packet)
    {
        if (!TryConsumePacketRate(fromPlayer, "show", ClaimConstants.RateShowMs))
        {
            return;
        }

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
            if (!TryConsumePacketRate(fromPlayer, "access", ClaimConstants.RateAccessActionMs))
            {
                return;
            }

            if (packet == null)
            {
                return;
            }

            SanitizeAccessActionPacket(packet);
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
