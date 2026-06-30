using System;
using System.IO;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixySkyBlock;

/// <summary>
/// Старые сейвы с WorldType=skyblock ломают hover-текст в меню Singleplayer.
/// Патчим protobuf-поле SaveGame.WorldType (field 30) на ванильный empty.
/// Не добавляйте worldConfigAttributes без поля default — getHoverText упадёт с NRE на всех сейвах.
/// </summary>
internal static class LegacySaveFixup
{
    // SaveGame.WorldType = "skyblock" (field tag 0xF2 0x01, len 8)
    private static readonly byte[] WorldTypeSkyblock =
        [0xF2, 0x01, 0x08, (byte)'s', (byte)'k', (byte)'y', (byte)'b', (byte)'l', (byte)'o', (byte)'c', (byte)'k'];

    // SaveGame.WorldType = "empty" (field tag 0xF2 0x01, len 5)
    private static readonly byte[] WorldTypeEmpty =
        [0xF2, 0x01, 0x05, (byte)'e', (byte)'m', (byte)'p', (byte)'t', (byte)'y'];

    // Старый PlayStyleLangCode из serverconfig: swixyskyblock:worldtype-skyblock
    private static readonly byte[] PlayStyleLangSuffixBad = Encoding.UTF8.GetBytes("worldtype-skyblock");

    // Та же длина (17), чтобы не ломать protobuf.
    private static readonly byte[] PlayStyleLangSuffixGood = Encoding.UTF8.GetBytes("preset-skyblock    ");

    public static void MigrateAllSaves(ILogger logger)
    {
        var savesDir = GamePaths.Saves;
        if (!Directory.Exists(savesDir))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(savesDir, "*.vcdbs", SearchOption.TopDirectoryOnly))
        {
            try
            {
                TryPatchSaveFile(path, logger);
            }
            catch (Exception ex)
            {
                logger.Warning("[SwixySkyBlock] Could not patch save {0}: {1}", Path.GetFileName(path), ex.Message);
            }
        }
    }

    public static bool TryPatchSaveFile(string path, ILogger logger)
    {
        var bytes = File.ReadAllBytes(path);
        var worldTypeIndex = IndexOf(bytes, WorldTypeSkyblock);
        var langCodeIndex = IndexOf(bytes, PlayStyleLangSuffixBad);
        if (worldTypeIndex < 0 && langCodeIndex < 0)
        {
            return false;
        }

        var patched = (byte[])bytes.Clone();
        if (worldTypeIndex >= 0)
        {
            Array.Copy(WorldTypeEmpty, 0, patched, worldTypeIndex, WorldTypeEmpty.Length);
        }

        if (langCodeIndex >= 0)
        {
            Array.Copy(PlayStyleLangSuffixGood, 0, patched, langCodeIndex, PlayStyleLangSuffixGood.Length);
        }

        var backup = path + ".skyblock-worldtype.bak";
        if (!File.Exists(backup))
        {
            File.Copy(path, backup, overwrite: false);
        }

        File.WriteAllBytes(path, patched);
        logger.Notification(
            "[SwixySkyBlock] Patched legacy save metadata in {0}",
            Path.GetFileName(path));
        return true;
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return -1;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }
}
