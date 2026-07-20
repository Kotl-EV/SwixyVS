// =============================================================================
// Network hardening: rate limits, field sanitization, packet size caps.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Core;
using SwixyClaimChunk.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkServerMod"/> — защита и нормализация сетевых пакетов.</summary>
public sealed partial class SwixyClaimChunkServerMod
{
    /// <summary>uid:action → last ElapsedMilliseconds.</summary>
    private readonly Dictionary<string, long> packetRateByKey = new(StringComparer.Ordinal);

    /// <summary>
    /// True если действие разрешено (интервал прошёл). False = слишком часто (игнор/тихий отказ).
    /// </summary>
    private bool TryConsumePacketRate(IServerPlayer player, string action, int minIntervalMs)
    {
        if (player == null || serverApi == null || minIntervalMs <= 0)
        {
            return true;
        }

        var uid = player.PlayerUID ?? player.PlayerName ?? "?";
        var key = uid + ":" + action;
        var now = serverApi.World.ElapsedMilliseconds;
        if (packetRateByKey.TryGetValue(key, out var last) && now - last < minIntervalMs)
        {
            return false;
        }

        packetRateByKey[key] = now;

        // Bound dictionary size (disconnect / many actions).
        if (packetRateByKey.Count > 2048)
        {
            TrimPacketRateTable(now);
        }

        return true;
    }

    private void TrimPacketRateTable(long now)
    {
        // Drop entries older than 30s.
        var stale = packetRateByKey
            .Where(kv => now - kv.Value > 30_000)
            .Select(kv => kv.Key)
            .Take(512)
            .ToList();
        foreach (var k in stale)
        {
            packetRateByKey.Remove(k);
        }
    }

    private static string SanitizeShortString(string? value, int maxLen)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLen)
        {
            return trimmed;
        }

        return trimmed[..maxLen];
    }

    private static string SanitizePacketMessage(string? message)
        => SanitizeShortString(message, ClaimConstants.MaxPacketMessageLength);

    private static EnumBlockAccessFlags SanitizeAccessFlags(int flags)
    {
        const EnumBlockAccessFlags allowed =
            EnumBlockAccessFlags.Use | EnumBlockAccessFlags.BuildOrBreak;
        return (EnumBlockAccessFlags)flags & allowed;
    }

    private int ClampMapRadius(int radius)
        => Math.Clamp(radius <= 0 ? ClaimConstants.DefaultRadius : radius, 1, ClaimConstants.MaxRadius);

    /// <summary>Нормализует окно карты: radius + center в разумных границах мира.</summary>
    private void SanitizeMapWindow(ref int centerChunkX, ref int centerChunkZ, ref int radius)
    {
        radius = ClampMapRadius(radius);

        if (serverApi == null)
        {
            return;
        }

        var chunkSize = serverApi.WorldManager.ChunkSize;
        if (chunkSize <= 0)
        {
            return;
        }

        var maxChunkX = Math.Max(0, (serverApi.WorldManager.MapSizeX - 1) / chunkSize);
        var maxChunkZ = Math.Max(0, (serverApi.WorldManager.MapSizeZ - 1) / chunkSize);

        // Keep full window inside int-safe range relative to map.
        centerChunkX = Math.Clamp(centerChunkX, -radius, maxChunkX + radius);
        centerChunkZ = Math.Clamp(centerChunkZ, -radius, maxChunkZ + radius);
    }

    private static List<ClaimChunkCoordPacket> SanitizeBatchChunks(
        IReadOnlyList<ClaimChunkCoordPacket>? chunks,
        out bool truncated)
    {
        truncated = false;
        if (chunks == null || chunks.Count == 0)
        {
            return [];
        }

        var seen = new HashSet<long>();
        var list = new List<ClaimChunkCoordPacket>(Math.Min(chunks.Count, ClaimConstants.MaxBatchChunks));
        foreach (var c in chunks)
        {
            if (c == null)
            {
                continue;
            }

            // Reject absurd coordinates early (prevents huge loops elsewhere).
            if (c.ChunkX is < -1_000_000 or > 1_000_000
                || c.ChunkZ is < -1_000_000 or > 1_000_000)
            {
                continue;
            }

            var packed = ((long)c.ChunkX << 32) ^ (uint)c.ChunkZ;
            if (!seen.Add(packed))
            {
                continue;
            }

            if (list.Count >= ClaimConstants.MaxBatchChunks)
            {
                truncated = true;
                break;
            }

            list.Add(c);
        }

        return list;
    }

    private void SanitizeAccessActionPacket(ClaimAccessActionPacket packet)
    {
        packet.PlayerName = SanitizeShortString(packet.PlayerName, ClaimConstants.MaxPlayerNameLength);
        packet.PlayerUid = SanitizeShortString(packet.PlayerUid, ClaimConstants.MaxPlayerUidLength);
        packet.ClaimName = SanitizeShortString(packet.ClaimName, ClaimConstants.MaxClaimNameLength);
        packet.AccessFlags = (int)SanitizeAccessFlags(packet.AccessFlags);
        packet.ClaimFlags &= ClaimFlagBits.AllKnown;

        var raw = packet.UseFilterCodesRaw ?? "";
        if (raw.Length > ClaimConstants.MaxUseFilterCodesRawLength)
        {
            raw = raw[..ClaimConstants.MaxUseFilterCodesRawLength];
        }

        packet.UseFilterCodesRaw = raw;
    }

    private List<string> SanitizeUseFilterCodes(IEnumerable<string>? codes)
    {
        var normalized = ClaimUseFilterLogic.NormalizeUseFilterCodes(codes);
        if (normalized.Count > ClaimConstants.MaxUseFilterCodes)
        {
            normalized = normalized.Take(ClaimConstants.MaxUseFilterCodes).ToList();
        }

        // Drop absurdly long individual codes (path injection / bloat).
        return normalized
            .Where(static c => c.Length <= 128)
            .ToList();
    }
}
