// =============================================================================
// LandClaimAllowance / ExtraLandClaimAllowance в serverconfig = ЧАНКИ.
// При старте: reflection по Roles и player data — миграция старых значений (блоки → чанки)
// и запись обратно в конфиг.
// =============================================================================

using System;
using System.Collections;
using System.Reflection;
using SwixyClaimChunk.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SwixyClaimChunk;

public sealed partial class SwixyClaimChunkServerMod
{
    private bool landClaimAllowanceMigrated;

    /// <summary>
    /// После загрузки мира: LandClaimAllowance трактуем как чанки.
    /// Если в конфиге ещё «ванильные» блоки (большие числа) — конвертируем и Save.
    /// </summary>
    private void OnLandClaimAllowanceSaveGameLoaded()
    {
        MigrateLandClaimAllowanceToChunks(persist: true);
    }

    /// <summary>
    /// Читает LandClaimAllowance ролей/игроков как чанки.
    /// Значения ≥ <see cref="ClaimVolumeUtil.LegacyBlocksThreshold"/> считаются блоками
    /// (legacy) и переводятся в чанки через reflection.
    /// </summary>
    private void MigrateLandClaimAllowanceToChunks(bool persist)
    {
        if (serverApi == null || landClaimAllowanceMigrated)
        {
            return;
        }

        landClaimAllowanceMigrated = true;

        var (chunkSize, mapSizeY) = GetWorldChunkDims();
        var columnVol = ClaimVolumeUtil.ChunkColumnVolume(chunkSize, mapSizeY);
        var changed = 0;

        try
        {
            changed += MigrateRolesAllowanceViaReflection(chunkSize, mapSizeY, columnVol);
            changed += MigratePlayersExtraAllowance(chunkSize, mapSizeY, columnVol);

            if (changed > 0 && persist)
            {
                PersistServerConfigAfterAllowanceMigration();
                serverApi.Logger.Notification(
                    "[SwixyClaimChunk] LandClaimAllowance now in CHUNKS. Migrated {0} value(s). " +
                    "Column volume={1} blocks (chunkSize={2}, mapSizeY={3}).",
                    changed, columnVol, chunkSize, mapSizeY);
            }
            else
            {
                serverApi.Logger.Notification(
                    "[SwixyClaimChunk] LandClaimAllowance unit = CHUNKS " +
                    "(1 chunk column = {0} blocks; chunkSize={1}, mapSizeY={2}).",
                    columnVol, chunkSize, mapSizeY);
            }
        }
        catch (Exception ex)
        {
            serverApi.Logger.Error("[SwixyClaimChunk] LandClaimAllowance migration failed: {0}", ex);
        }
    }

    private (int chunkSize, int mapSizeY) GetWorldChunkDims()
    {
        var chunkSize = serverApi?.WorldManager?.ChunkSize ?? 0;
        var mapSizeY = serverApi?.WorldManager?.MapSizeY ?? 0;
        if (chunkSize <= 0)
        {
            chunkSize = ClaimVolumeUtil.DefaultChunkSize;
        }

        if (mapSizeY <= 0)
        {
            mapSizeY = ClaimVolumeUtil.DefaultMapSizeY;
        }

        return (chunkSize, mapSizeY);
    }

    /// <summary>
    /// Если raw выглядит как старый объём в блоках — вернуть чанки; иначе raw уже чанки.
    /// </summary>
    private static int NormalizeAllowanceToChunks(int raw, int chunkSize, int mapSizeY, long columnVol)
    {
        if (raw <= 0)
        {
            return 0;
        }

        // Уже чанки (разумный диапазон)
        if (raw < ClaimVolumeUtil.LegacyBlocksThreshold)
        {
            return raw;
        }

        // Legacy: блоки → чанки (ceil)
        var chunks = ClaimVolumeUtil.BlocksToChunkCount(raw, chunkSize, mapSizeY);
        if (chunks > int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)chunks;
    }

    private int MigrateRolesAllowanceViaReflection(int chunkSize, int mapSizeY, long columnVol)
    {
        if (serverApi == null)
        {
            return 0;
        }

        var changed = 0;
        var configObj = serverApi.Server.Config;
        if (configObj == null)
        {
            return 0;
        }

        // Concrete List<PlayerRole> через property/field Roles (не IServerConfig-копию)
        var rolesList = GetConcreteRolesListObject(configObj);
        if (rolesList is not IEnumerable enumerable)
        {
            // fallback interface
            enumerable = serverApi.Server.Config.Roles;
        }

        foreach (var item in enumerable)
        {
            if (item == null)
            {
                continue;
            }

            if (!TryGetIntProp(item, "LandClaimAllowance", out var raw))
            {
                if (item is IPlayerRole roleIface)
                {
                    raw = roleIface.LandClaimAllowance;
                }
                else
                {
                    continue;
                }
            }

            var chunks = NormalizeAllowanceToChunks(raw, chunkSize, mapSizeY, columnVol);
            if (chunks == raw)
            {
                continue;
            }

            if (TrySetIntProp(item, "LandClaimAllowance", chunks))
            {
                changed++;
                var code = TryGetStringProp(item, "Code") ?? "?";
                serverApi.Logger.Notification(
                    "[SwixyClaimChunk] Role '{0}' LandClaimAllowance: {1} blocks → {2} chunks",
                    code, raw, chunks);
            }
        }

        return changed;
    }

    private int MigratePlayersExtraAllowance(int chunkSize, int mapSizeY, long columnVol)
    {
        if (serverApi == null)
        {
            return 0;
        }

        var changed = 0;
        try
        {
            foreach (var pair in serverApi.PlayerData.PlayerDataByUid)
            {
                var data = pair.Value;
                if (data == null)
                {
                    continue;
                }

                // ExtraLandClaimAllowance на IServerPlayerData
                int raw;
                try
                {
                    raw = data.ExtraLandClaimAllowance;
                }
                catch
                {
                    if (!TryGetIntProp(data, "ExtraLandClaimAllowance", out raw))
                    {
                        continue;
                    }
                }

                var chunks = NormalizeAllowanceToChunks(raw, chunkSize, mapSizeY, columnVol);
                if (chunks == raw)
                {
                    continue;
                }

                try
                {
                    data.ExtraLandClaimAllowance = chunks;
                    changed++;
                    serverApi.Logger.Notification(
                        "[SwixyClaimChunk] Player '{0}' ExtraLandClaimAllowance: {1} → {2} chunks",
                        data.LastKnownPlayername ?? data.PlayerUID, raw, chunks);
                }
                catch
                {
                    if (TrySetIntProp(data, "ExtraLandClaimAllowance", chunks))
                    {
                        changed++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            serverApi.Logger.Warning("[SwixyClaimChunk] ExtraLandClaimAllowance migration: {0}", ex.Message);
        }

        return changed;
    }

    private void PersistServerConfigAfterAllowanceMigration()
    {
        if (serverApi == null)
        {
            return;
        }

        try
        {
            var configObj = serverApi.Server.Config;
            var save = configObj.GetType().GetMethod(
                "Save",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            save?.Invoke(configObj, null);
            serverApi.Server.MarkConfigDirty();
        }
        catch (Exception ex)
        {
            serverApi.Logger.Warning("[SwixyClaimChunk] Failed to Save config after allowance migration: {0}", ex.Message);
        }
    }

    /// <summary>Реальный List&lt;PlayerRole&gt; ServerConfig (не ConvertAll-копия IServerConfig).</summary>
    private static object? GetConcreteRolesListObject(object configObj)
    {
        var t = configObj.GetType();
        var field = t.GetField("<Roles>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            return field.GetValue(configObj);
        }

        foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop.Name != "Roles" || !prop.PropertyType.IsGenericType)
            {
                continue;
            }

            if (prop.PropertyType.GetGenericTypeDefinition() != typeof(System.Collections.Generic.List<>))
            {
                continue;
            }

            var elem = prop.PropertyType.GetGenericArguments()[0];
            if (elem == typeof(IPlayerRole))
            {
                continue;
            }

            return prop.GetValue(configObj);
        }

        return null;
    }

    private static bool TryGetIntProp(object target, string name, out int value)
    {
        value = 0;
        try
        {
            var prop = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null)
            {
                return false;
            }

            var raw = prop.GetValue(target);
            if (raw is int i)
            {
                value = i;
                return true;
            }

            if (raw != null && int.TryParse(raw.ToString(), out var parsed))
            {
                value = parsed;
                return true;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static bool TrySetIntProp(object target, string name, int value)
    {
        try
        {
            var prop = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanWrite)
            {
                // try field
                var field = target.GetType().GetField(
                    $"<{name}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return true;
                }

                return false;
            }

            prop.SetValue(target, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryGetStringProp(object target, string name)
    {
        try
        {
            var prop = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return prop?.GetValue(target)?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
