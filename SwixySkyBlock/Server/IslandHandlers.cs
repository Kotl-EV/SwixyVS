using System;
using System.Collections.Generic;
using System.Linq;
using SwixySkyBlock.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixySkyBlock;

public sealed partial class SwixySkyBlockMod
{
    private void OnHubRequest(IServerPlayer player, IslandHubRequestPacket _)
    {
        serverApi?.Logger.Notification(
            "[SwixySkyBlock][Hub] Hub request from {0}",
            player.PlayerName);
        SendHubState(player, "", 0);
    }

    private void OnIslandAction(IServerPlayer player, IslandActionPacket packet)
    {
        switch (packet.Action)
        {
            case IslandHubActionType.Create:
                BeginCreatePlayerIsland(player, packet.TemplateName);
                return;
        }

        var result = ProcessIslandAction(player, packet);
        SendHubState(player, result);
    }

    private void OnClaimListRequest(IServerPlayer player, IslandClaimListRequestPacket _)
    {
        SendClaimList(player, "", 0);
    }

    private const int MaxIslandTopEntries = 50;

    private void OnTopRequest(IServerPlayer player, IslandTopRequestPacket _)
    {
        if (serverChannel == null)
        {
            return;
        }

        serverChannel.SendPacket(BuildIslandTopStatePacket(player), [player]);
    }

    private IslandTopStatePacket BuildIslandTopStatePacket(IServerPlayer viewer)
    {
        var langCode = viewer.LanguageCode;
        var entries = islandRegistry.All
            .OrderByDescending(static record => record.GeneratorLevel)
            .ThenBy(
                record => ResolvePlayerName(record.PlayerUid, langCode: langCode),
                StringComparer.OrdinalIgnoreCase)
            .Take(MaxIslandTopEntries)
            .Select((record, index) => new IslandTopEntryPacket
            {
                Rank = index + 1,
                PlayerName = ResolvePlayerName(record.PlayerUid, langCode: langCode),
                GeneratorLevel = record.GeneratorLevel,
                TemplateName = record.TemplateName,
                IsViewer = record.PlayerUid == viewer.PlayerUID
            })
            .ToList();

        return new IslandTopStatePacket { Entries = entries };
    }

    private void OnClaimAccessAction(IServerPlayer player, IslandClaimAccessActionPacket packet)
    {
        var result = ProcessClaimAccessAction(player, packet);
        SendClaimList(player, result);
    }

    private void OnClaimShowRequest(IServerPlayer player, IslandClaimShowRequestPacket packet)
    {
        if (packet.Clear)
        {
            SendClaimShowCleared(player, packet.ClaimId);
            return;
        }

        SendClaimShow(player, packet.ClaimId);
    }

    private void SendClaimShow(IServerPlayer player, int claimId)
    {
        if (serverChannel == null)
        {
            return;
        }

        serverChannel.SendPacket(BuildClaimShowPacket(player, claimId), [player]);
    }

    private void SendClaimShowCleared(IServerPlayer player, int claimId)
    {
        if (serverChannel == null)
        {
            return;
        }

        ClearClaimHighlight(player);
        serverChannel.SendPacket(new IslandClaimShowStatePacket
        {
            ClaimId = claimId,
            Active = false
        }, [player]);
    }

    private IslandClaimShowStatePacket BuildClaimShowPacket(IServerPlayer player, int claimId)
    {
        var packet = new IslandClaimShowStatePacket
        {
            ClaimId = claimId,
            Active = true
        };

        if (!TryGetClaimById(claimId, out var claim) || !CanViewClaimInList(claim, player.PlayerUID))
        {
            packet.Active = false;
            packet.Message = Lang.GetL(player.LanguageCode, "swixyskyblock:island-claims-error-not-found");
            packet.MessageType = 1;
            return packet;
        }

        HighlightClaim(player, claim);

        foreach (var area in claim.Areas ?? [])
        {
            packet.Areas.Add(new IslandClaimAreaPacket
            {
                X1 = area.X1,
                Y1 = area.Y1,
                Z1 = area.Z1,
                X2 = area.X2,
                Y2 = area.Y2,
                Z2 = area.Z2
            });
        }

        return packet;
    }

    private void ClearClaimHighlight(IServerPlayer player)
    {
        serverApi?.World.HighlightBlocks(
            player,
            (int)EnumHighlightSlot.LandClaim,
            [],
            [],
            EnumHighlightBlocksMode.Absolute,
            EnumHighlightShape.Cubes,
            1f);
    }

    private void HighlightClaim(IServerPlayer player, LandClaim claim)
    {
        var areas = claim.Areas;
        if (serverApi == null || areas == null)
        {
            return;
        }

        var blocks = new List<BlockPos>(areas.Count * 2);
        var colors = new List<int>(areas.Count);
        var color = ColorUtil.ToRgba(64, 100, 255, 100);

        foreach (var area in areas)
        {
            blocks.Add(new BlockPos(area.X1, area.Y1, area.Z1));
            blocks.Add(new BlockPos(area.X2, area.Y2, area.Z2));
            colors.Add(color);
        }

        serverApi.World.HighlightBlocks(
            player,
            (int)EnumHighlightSlot.LandClaim,
            blocks,
            colors,
            EnumHighlightBlocksMode.Absolute,
            EnumHighlightShape.Cubes,
            1f);
    }
}
