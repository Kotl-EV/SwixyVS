// =============================================================================
// ClaimVolumeUtil.cs
// -----------------------------------------------------------------------------
// Пересчёт объёма привата: блоки ↔ чанки.
//
// В serverconfig (PlayerRole.LandClaimAllowance / ExtraLandClaimAllowance)
// SwixyClaimChunk хранит и читает КОЛИЧЕСТВО ЧАНКОВ, не блоков.
// При проверке квоты чанки умножаются на объём столбца (size² × MapSizeY).
// =============================================================================

namespace SwixyClaimChunk.Core;

/// <summary>
/// Вспомогательные методы для пересчёта объёма привата между блоками и чанками.
/// </summary>
public static class ClaimVolumeUtil
{
    public const int DefaultChunkSize = 32;
    public const int DefaultMapSizeY = 256;

    /// <summary>
    /// Порог: значения LandClaimAllowance ≥ этого числа считаются «старыми» (в блоках)
    /// и один раз конвертируются в чанки при загрузке мира.
    /// </summary>
    public const int LegacyBlocksThreshold = 10_000;

    /// <summary>Объём одного чанк-столбца в блоках: chunkSize² × mapSizeY.</summary>
    public static long ChunkColumnVolume(int chunkSize, int mapSizeY)
    {
        if (chunkSize <= 0)
        {
            chunkSize = DefaultChunkSize;
        }

        if (mapSizeY <= 0)
        {
            mapSizeY = DefaultMapSizeY;
        }

        return (long)chunkSize * chunkSize * mapSizeY;
    }

    /// <summary>
    /// Переводит объём в блоках в эквивалентное количество чанков (округление вверх).
    /// </summary>
    public static long BlocksToChunkCount(long blockVolume, int chunkSize, int mapSizeY)
    {
        if (blockVolume <= 0)
        {
            return 0;
        }

        var chunkVolume = ChunkColumnVolume(chunkSize, mapSizeY);
        if (chunkVolume <= 0)
        {
            return blockVolume;
        }

        return (blockVolume + chunkVolume - 1) / chunkVolume;
    }

    /// <summary>Чанки → объём в блоках для сравнения с claim.SizeXYZ.</summary>
    public static long ChunksToBlockVolume(long chunks, int chunkSize, int mapSizeY)
    {
        if (chunks <= 0)
        {
            return 0;
        }

        var chunkVolume = ChunkColumnVolume(chunkSize, mapSizeY);
        if (chunkVolume <= 0)
        {
            return chunks;
        }

        // защита от overflow
        if (chunks > long.MaxValue / chunkVolume)
        {
            return long.MaxValue;
        }

        return chunks * chunkVolume;
    }
}
