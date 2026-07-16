using System;
using SwixyClaimChunk.Core;
using SwixyClaimChunk.Net;
using Vintagestory.API.Common;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkClientMod"/> — client prediction whitelist Use.</summary>
public sealed partial class SwixyClaimChunkClientMod
{
    private EnumWorldAccessResponse OnClientTestBlockAccess(
        IPlayer player,
        BlockSelection blockSel,
        EnumBlockAccessFlags accessType,
        ref string claimant,
        EnumWorldAccessResponse response)
        => ApplyUseBlockFilter(player, blockSel, accessType, ref claimant, null, response);

    private EnumWorldAccessResponse OnClientTestBlockAccessClaim(
        IPlayer player,
        BlockSelection blockSel,
        EnumBlockAccessFlags accessType,
        ref string claimant,
        LandClaim claim,
        EnumWorldAccessResponse response)
        => ApplyUseBlockFilter(player, blockSel, accessType, ref claimant, claim, response);

    /// <summary>
    /// Клиентский prediction Use-фильтра.
    /// Со-владельцы на клиенте не синхронизируются — сервер остаётся авторитетом.
    /// </summary>
    private EnumWorldAccessResponse ApplyUseBlockFilter(
        IPlayer player,
        BlockSelection? blockSel,
        EnumBlockAccessFlags accessType,
        ref string claimant,
        LandClaim? claim,
        EnumWorldAccessResponse response)
    {
        return ClaimUseFilterLogic.ApplyUseBlockFilter(
            clientApi?.World,
            clientUseFiltersByClaimKey,
            player,
            blockSel,
            accessType,
            ref claimant,
            claim,
            response,
            isPrivileged: (activeClaim, playerUid) => ClaimUseFilterLogic.IsClaimOwner(activeClaim, playerUid),
            logError: msg => clientApi?.Logger.Error(msg));
    }

    /// <summary>
    /// Клиент: снимок фильтров только в <see cref="clientUseFiltersByClaimKey"/>.
    /// </summary>
    private void OnUseFiltersSyncPacket(ClaimUseFiltersSyncPacket packet)
    {
        clientUseFiltersByClaimKey.Clear();
        if (packet?.Entries == null)
        {
            clientApi?.Logger.Notification("[SwixyClaimChunk] Client use filters synced: 0 entries");
            return;
        }

        foreach (var entry in packet.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ClaimKey))
            {
                continue;
            }

            var codes = ClaimUseFilterLogic.NormalizeUseFilterCodes(ClaimUseFilterCodesCodec.Split(entry.CodesRaw));
            if (entry.Mode != ClaimUseFilterMode.Whitelist || codes.Count == 0)
            {
                continue;
            }

            clientUseFiltersByClaimKey[entry.ClaimKey] = new UseFilterRuleData
            {
                Mode = ClaimUseFilterMode.Whitelist,
                Codes = codes
            };
        }

        clientApi?.Logger.Notification(
            "[SwixyClaimChunk] Client use filters synced: {0} entries",
            clientUseFiltersByClaimKey.Count);
    }
}
