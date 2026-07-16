// =============================================================================
// ClaimVolumeUtil.cs
// -----------------------------------------------------------------------------
// Утилита пересчёта объёма привата из блоков в количество чанков.
// Используется при отображении лимитов и статистики: сервер и клиент могут
// оперировать блоками, а карта приватов — чанками фиксированного размера.
// =============================================================================

namespace SwixyClaimChunk.Core;

/// <summary>
/// Вспомогательные методы для пересчёта объёма привата между блоками и чанками.
/// </summary>
public static class ClaimVolumeUtil
{
    /// <summary>
    /// Переводит объём в блоках в эквивалентное количество чанков (округление вверх).
    /// </summary>
    /// <param name="blockVolume">Объём в блоках; неположительные значения дают 0 чанков.</param>
    /// <param name="chunkSize">Горизонтальный размер чанка (X и Z); при невалидном значении возвращается исходный blockVolume.</param>
    /// <param name="mapSizeY">Высота мира в блоках (ось Y); участвует в объёме одного чанка.</param>
    /// <returns>Минимальное целое число чанков, покрывающее заданный объём блоков.</returns>
    public static long BlocksToChunkCount(long blockVolume, int chunkSize, int mapSizeY)
    {
        if (blockVolume <= 0)
        {
            return 0;
        }

        if (chunkSize <= 0 || mapSizeY <= 0)
        {
            return blockVolume;
        }

        var chunkVolume = (long)chunkSize * chunkSize * mapSizeY;
        if (chunkVolume <= 0)
        {
            return blockVolume;
        }

        return (blockVolume + chunkVolume - 1) / chunkVolume;
    }
}
