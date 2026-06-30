using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Content;
using SwixyClaimChunk.Net;
using ProtoBuf;
using static SwixyClaimChunk.Content.ClaimVolumeUtil;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkMod"/> — сервер: карта, пакеты и подсветка.</summary>
public sealed partial class SwixyClaimChunkMod
{
    /// <summary>Отправляет ClaimMapStatePacket после клейма или запроса карты.</summary>
    private void SendState(IServerPlayer player, int centerChunkX, int centerChunkZ, int radius, ClaimActionResult result)
    {
        SendState(player, centerChunkX, centerChunkZ, radius, result.Resolve(player), result.MessageType);
    }

    /// <summary>Отправляет ClaimMapStatePacket после клейма или запроса карты.</summary>
    private void SendState(IServerPlayer player, int centerChunkX, int centerChunkZ, int radius, string message, int messageType)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        radius = Math.Clamp(radius <= 0 ? DefaultRadius : radius, 1, MaxRadius);
        var packet = BuildStatePacket(player, centerChunkX, centerChunkZ, radius, message, messageType);
        if (packet.Chunks.Count == 0)
        {
            serverApi.Logger.Warning("[SwixyClaimChunk] State packet has 0 chunks, not sending to {0}", player.PlayerName);
            return;
        }

        try
        {
            serverApi.Logger.Notification(
                "[SwixyClaimChunk] Sending state to {0}: {1} chunks, message='{2}'",
                player.PlayerName,
                packet.Chunks.Count,
                message);
            serverChannel.SendPacket(packet, [player]);
        }
        catch (Exception exception)
        {
            serverApi.Logger.Error("Failed to send claim map state to {0}: {1}", player.PlayerName, exception);
        }
    }

    /// <summary>Отправляет ClaimListStatePacket со списком приватов игрока.</summary>
    private void SendClaimList(IServerPlayer player, ClaimActionResult result)
    {
        SendClaimList(player, result.Resolve(player), result.MessageType);
    }

    /// <summary>Отправляет ClaimListStatePacket со списком приватов игрока.</summary>
    private void SendClaimList(IServerPlayer player, string message, int messageType)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        try
        {
            serverChannel.SendPacket(BuildClaimListPacket(player, message, messageType), [player]);
        }
        catch (Exception exception)
        {
            serverApi.Logger.Error("Failed to send claim list to {0}: {1}", player.PlayerName, exception);
        }
    }

    /// <summary>Включает подсветку привата и отправляет ClaimShowStatePacket.</summary>
    private void SendClaimShow(IServerPlayer player, int claimId)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        try
        {
            serverChannel.SendPacket(BuildClaimShowPacket(player, claimId), [player]);
        }
        catch (Exception exception)
        {
            serverApi.Logger.Error("Failed to send claim highlight to {0}: {1}", player.PlayerName, exception);
        }
    }

    /// <summary>Собирает пакет подсветки; вызывает HighlightClaim на сервере.</summary>
    private ClaimShowStatePacket BuildClaimShowPacket(IServerPlayer player, int claimId)
    {
        var packet = new ClaimShowStatePacket
        {
            ClaimId = claimId,
            Active = true
        };

        if (!TryGetClaimById(claimId, out var claim) || !CanManageClaim(claim, player.PlayerUID))
        {
            packet.Active = false;
            packet.Message = Lang.GetL(player.LanguageCode, "swixyclaimchunk:claims-error-not-found");
            packet.MessageType = 1;
            return packet;
        }

        HighlightClaim(player, claim);

        foreach (var area in claim.Areas ?? [])
        {
            packet.Areas.Add(new ClaimAreaPacket
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

    /// <summary>Снимает подсветку и уведомляет клиент (Active=false).</summary>
    private void SendClaimShowCleared(IServerPlayer player, int claimId)
    {
        if (serverApi == null || serverChannel == null)
        {
            return;
        }

        ClearClaimHighlight(player);

        try
        {
            serverChannel.SendPacket(new ClaimShowStatePacket
            {
                ClaimId = claimId,
                Active = false
            }, [player]);
        }
        catch (Exception exception)
        {
            serverApi.Logger.Error("Failed to clear claim highlight for {0}: {1}", player.PlayerName, exception);
        }
    }


    /// <summary>Убирает кубы подсветки LandClaim у игрока.</summary>
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

    /// <summary>Рисует полупрозрачные кубы по всем Areas привата через HighlightBlocks.</summary>
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

    /// <summary>Собирает список приватов игрока (ClaimId = индекс+1 в World.Claims.All).</summary>
    private ClaimListStatePacket BuildClaimListPacket(IServerPlayer player, string message, int messageType)
    {
        var packet = new ClaimListStatePacket
        {
            Message = message ?? "",
            MessageType = messageType
        };

        var allClaims = serverApi?.World.Claims?.All;
        if (allClaims == null)
        {
            return packet;
        }

        for (var i = 0; i < allClaims.Count; i++)
        {
            var claim = allClaims[i];
            if (!CanManageClaim(claim, player.PlayerUID))
            {
                continue;
            }

            packet.Claims.Add(BuildClaimInfo(i + 1, claim, player));
        }

        return packet;
    }

    /// <summary>Локализованная строка прав Use/Build для пакета участника.</summary>
    private static string FormatMemberAccessName(string langCode, EnumBlockAccessFlags flags, bool isOwner = false)
    {
        if (isOwner)
        {
            return Lang.GetL(langCode, "swixyclaimchunk:claims-owner-role");
        }

        var parts = new List<string>();
        if (flags.HasFlag(EnumBlockAccessFlags.Use))
        {
            parts.Add(Lang.GetL(langCode, "swixyclaimchunk:claims-access-use"));
        }

        if (flags.HasFlag(EnumBlockAccessFlags.BuildOrBreak))
        {
            parts.Add(Lang.GetL(langCode, "swixyclaimchunk:claims-access-build"));
        }

        return parts.Count > 0
            ? string.Join(", ", parts)
            : Lang.GetL(langCode, "swixyclaimchunk:claims-access-none");
    }

    /// <summary>Локализованная подпись прав участника с учётом статуса со-владельца.</summary>
    private static string FormatMemberAccessName(string langCode, EnumBlockAccessFlags flags, bool isOwner, bool isCoOwner)
    {
        if (isOwner)
        {
            return Lang.GetL(langCode, "swixyclaimchunk:claims-owner-role");
        }

        if (isCoOwner)
        {
            return Lang.GetL(langCode, "swixyclaimchunk:claims-coowner-role");
        }

        return FormatMemberAccessName(langCode, flags);
    }

    /// <summary>Конвертирует LandClaim в ClaimInfoPacket с владельцем и участниками.</summary>
    private ClaimInfoPacket BuildClaimInfo(int claimId, LandClaim claim, IServerPlayer viewer)
    {
        var langCode = viewer.LanguageCode;
        var ownerName = ResolveClaimOwnerName(claim, langCode);
        var info = new ClaimInfoPacket
        {
            ClaimId = claimId,
            Name = string.IsNullOrWhiteSpace(claim.Description) ? ownerName : claim.Description,
            OwnerName = ownerName,
            ViewerIsCoOwner = IsCoOwner(claim, viewer.PlayerUID),
            AreaCount = claim.Areas?.Count ?? 0,
            Volume = claim.SizeXYZ,
            ChunkCount = BlocksToChunkCount(
                claim.SizeXYZ,
                serverApi!.WorldManager.ChunkSize,
                serverApi.WorldManager.MapSizeY)
        };

        info.Members.Add(new ClaimMemberPacket
        {
            PlayerUid = claim.OwnedByPlayerUid,
            PlayerName = info.OwnerName,
            AccessFlags = (int)(EnumBlockAccessFlags.Use | EnumBlockAccessFlags.BuildOrBreak),
            AccessName = Lang.GetL(langCode, "swixyclaimchunk:claims-owner-role"),
            IsOwner = true
        });

        if (claim.PermittedPlayerUids != null)
        {
            foreach (var entry in claim.PermittedPlayerUids.OrderBy(pair => ResolvePlayerName(pair.Key, langCode: langCode), StringComparer.OrdinalIgnoreCase))
            {
                info.Members.Add(new ClaimMemberPacket
                {
                    PlayerUid = entry.Key,
                    PlayerName = ResolvePlayerName(entry.Key, langCode: langCode),
                    AccessFlags = (int)entry.Value,
                    AccessName = FormatMemberAccessName(langCode, entry.Value, isOwner: false, IsCoOwner(claim, entry.Key)),
                    IsOwner = false,
                    IsCoOwner = IsCoOwner(claim, entry.Key)
                });
            }
        }

        return info;
    }

    /// <summary>Имя игрока по UID через онлайн-список или PlayerData.LastKnownPlayername.</summary>
    private string ResolvePlayerName(string? playerUid, string? fallbackName = null, string? langCode = null)
    {
        if (string.IsNullOrWhiteSpace(playerUid))
        {
            return GetNonEmptyPlayerName(fallbackName, "?");
        }

        if (serverApi == null)
        {
            return GetDisplayNameFallback(playerUid, fallbackName, langCode);
        }

        var onlinePlayer = serverApi.World.AllPlayers.FirstOrDefault(player => player.PlayerUID == playerUid);
        if (onlinePlayer != null)
        {
            return onlinePlayer.PlayerName;
        }

        var playerDataName = serverApi.PlayerData.GetPlayerDataByUid(playerUid)?.LastKnownPlayername;
        if (!string.IsNullOrWhiteSpace(playerDataName))
        {
            return playerDataName.Trim();
        }

        return GetDisplayNameFallback(playerUid, fallbackName, langCode);
    }

    /// <summary>Последний fallback для отображаемого имени без сырого UID в UI.</summary>
    private static string GetDisplayNameFallback(string playerUid, string? fallbackName, string? langCode = null)
    {
        if (!string.IsNullOrWhiteSpace(fallbackName) && !string.Equals(fallbackName, playerUid, StringComparison.Ordinal))
        {
            return fallbackName.Trim();
        }

        var code = string.IsNullOrWhiteSpace(langCode) ? Lang.CurrentLocale : langCode;
        return Lang.GetL(code, "swixyclaimchunk:claims-unknown-player");
    }

    /// <summary>Имя владельца привата с запасным вариантом из LastKnownOwnerName.</summary>
    private string ResolveClaimOwnerName(LandClaim claim, string? langCode = null)
    {
        return ResolvePlayerName(claim.OwnedByPlayerUid, claim.LastKnownOwnerName, langCode);
    }

    /// <summary>Возвращает первое непустое имя; UID используется последним fallback.</summary>
    private static string GetNonEmptyPlayerName(string? playerName, string fallback)
    {
        return string.IsNullOrWhiteSpace(playerName)
            ? fallback
            : playerName.Trim();
    }

    /// <summary>ClaimId в пакетах — 1-based индекс в World.Claims.All.</summary>
    private bool TryGetClaimById(int claimId, out LandClaim claim)
    {
        claim = null!;
        var allClaims = serverApi?.World.Claims?.All;
        if (allClaims == null || claimId <= 0 || claimId > allClaims.Count)
        {
            return false;
        }

        claim = allClaims[claimId - 1];
        return true;
    }

    /// <summary>Строит сетку чанков вокруг центра и квоты игрока.</summary>
    private ClaimMapStatePacket BuildStatePacket(IServerPlayer player, int centerChunkX, int centerChunkZ, int radius, string message, int messageType)
    {
        var sapi = serverApi!;
        var chunkSize = sapi.WorldManager.ChunkSize;
        if (chunkSize <= 0)
        {
            throw new InvalidOperationException("World chunk size is not available.");
        }

        if (!TryGetPlayerChunk(player, out var playerChunkX, out var playerChunkZ))
        {
            playerChunkX = centerChunkX;
            playerChunkZ = centerChunkZ;
        }

        var ownClaims = GetOwnClaims(player.PlayerUID).ToList();
        var allClaims = sapi.World.Claims?.All?.ToList() ?? [];
        var packet = new ClaimMapStatePacket
        {
            CenterChunkX = centerChunkX,
            CenterChunkZ = centerChunkZ,
            PlayerChunkX = playerChunkX,
            PlayerChunkZ = playerChunkZ,
            Radius = radius,
            ChunkSize = chunkSize,
            MapSizeX = sapi.WorldManager.MapSizeX,
            MapSizeZ = sapi.WorldManager.MapSizeZ,
            MapSizeY = sapi.WorldManager.MapSizeY,
            UsedVolume = ownClaims.Sum(static claim => (long)claim.SizeXYZ),
            MaxVolume = GetLandClaimAllowance(player),
            UsedAreas = ownClaims.Sum(static claim => claim.Areas?.Count ?? 0),
            MaxAreas = GetLandClaimMaxAreas(player),
            CanAdminUnclaimOthers = CanAdminUnclaimOthers(player),
            Message = message ?? "",
            MessageType = messageType
        };

        for (var z = centerChunkZ - radius; z <= centerChunkZ + radius; z++)
        {
            for (var x = centerChunkX - radius; x <= centerChunkX + radius; x++)
            {
                packet.Chunks.Add(BuildCell(player, x, z, allClaims));
            }
        }

        return packet;
    }

    /// <summary>Координаты чанка, в котором стоит игрок (для маркера на карте).</summary>
    private bool TryGetPlayerChunk(IServerPlayer player, out int chunkX, out int chunkZ)
    {
        chunkX = 0;
        chunkZ = 0;

        if (serverApi == null)
        {
            return false;
        }

        var entity = player.Entity;
        var chunkSize = serverApi.WorldManager.ChunkSize;
        if (entity == null || chunkSize <= 0)
        {
            return false;
        }

        var blockPos = entity.Pos.AsBlockPos;
        chunkX = FloorDiv(blockPos.X, chunkSize);
        chunkZ = FloorDiv(blockPos.Z, chunkSize);
        return true;
    }

    /// <summary>Состояние одного чанка для карты (обёртка с загрузкой всех claims).</summary>
    private ClaimChunkCellPacket BuildCell(IServerPlayer player, int chunkX, int chunkZ)
    {
        return BuildCell(player, chunkX, chunkZ, serverApi?.World.Claims?.All?.ToList() ?? []);
    }

    /// <summary>Free / Own / Other / OutOfWorld для чанка по пересечению с LandClaim.</summary>
    private ClaimChunkCellPacket BuildCell(IServerPlayer player, int chunkX, int chunkZ, IReadOnlyList<LandClaim> allClaims)
    {
        if (!TryBuildChunkArea(chunkX, chunkZ, out var area))
        {
            return new ClaimChunkCellPacket
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                State = ClaimChunkCellState.OutOfWorld
            };
        }

        var claimIndex = FindIntersectingClaimIndex(area, allClaims);
        if (claimIndex < 0)
        {
            return new ClaimChunkCellPacket
            {
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                State = ClaimChunkCellState.Free
            };
        }

        var claim = allClaims[claimIndex];
        var ownerName = ResolveClaimOwnerName(claim);
        return new ClaimChunkCellPacket
        {
            ChunkX = chunkX,
            ChunkZ = chunkZ,
            State = PlayerTreatsClaimAsOwn(claim, player.PlayerUID) ? ClaimChunkCellState.Own : ClaimChunkCellState.Other,
            OwnerName = ownerName,
            ClaimId = claimIndex + 1,
            ClaimName = string.IsNullOrWhiteSpace(claim.Description) ? ownerName : claim.Description
        };
    }

}
