// =============================================================================
// Claim flags: PvP allow + animal protection — SaveGame + damage checks.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Core;
using SwixyClaimChunk.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SwixyClaimChunk;

/// <summary>Часть <see cref="SwixyClaimChunkServerMod"/> — флаги привата (PvP, животные).</summary>
public sealed partial class SwixyClaimChunkServerMod
{
    private int GetClaimFlags(LandClaim claim)
    {
        foreach (var key in ClaimStorageKeys.EnumerateClaimStorageKeys(claim))
        {
            if (claimFlagsByClaimKey.TryGetValue(key, out var flags))
            {
                return flags;
            }
        }

        return 0;
    }

    private void FillClaimFlagsInfo(ClaimInfoPacket info, LandClaim claim)
    {
        info.ClaimFlags = GetClaimFlags(claim);
    }

    private ClaimActionResult TrySetClaimFlags(LandClaim claim, int flags)
    {
        // Keep only known bits.
        flags &= ClaimFlagBits.AllowPvp | ClaimFlagBits.ProtectAnimals;

        var keys = ClaimStorageKeys.EnumerateClaimStorageKeys(claim).ToList();
        if (keys.Count == 0)
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-unknown");
        }

        if (flags == 0)
        {
            foreach (var key in keys)
            {
                claimFlagsByClaimKey.Remove(key);
            }
        }
        else
        {
            foreach (var key in keys)
            {
                claimFlagsByClaimKey[key] = flags;
            }
        }

        PersistClaimFlagsNow();
        return ClaimActionResult.Success("swixyclaimchunk:claim-flags-saved");
    }

    private void ClearClaimFlags(LandClaim claim)
    {
        foreach (var key in ClaimStorageKeys.EnumerateClaimStorageKeys(claim))
        {
            claimFlagsByClaimKey.Remove(key);
        }

        PersistClaimFlagsNow();
    }

    private void ClearClaimFlagsKeysOnly(LandClaim claim)
    {
        foreach (var key in ClaimStorageKeys.EnumerateClaimStorageKeys(claim))
        {
            claimFlagsByClaimKey.Remove(key);
        }
    }

    private void MergeClaimFlags(LandClaim primary, LandClaim other)
    {
        var merged = GetClaimFlags(primary) | GetClaimFlags(other);
        ClearClaimFlagsKeysOnly(other);
        if (merged == 0)
        {
            ClearClaimFlagsKeysOnly(primary);
        }
        else
        {
            foreach (var key in ClaimStorageKeys.EnumerateClaimStorageKeys(primary))
            {
                claimFlagsByClaimKey[key] = merged;
            }
        }

        PersistClaimFlagsNow();
    }

    private void RebindClaimFlagsKeys(LandClaim claim)
    {
        var flags = GetClaimFlags(claim);
        // Drop old keys for this claim identity and rewrite to current keys.
        // Safer: rewrite known current keys only.
        if (flags == 0)
        {
            return;
        }

        foreach (var key in ClaimStorageKeys.EnumerateClaimStorageKeys(claim))
        {
            claimFlagsByClaimKey[key] = flags;
        }

        PersistClaimFlagsNow();
    }

    private void OnClaimFlagsSaveGameLoaded()
    {
        claimFlagsByClaimKey.Clear();
        var data = serverApi?.WorldManager.SaveGame.GetData(ClaimConstants.ClaimFlagsSaveKey);
        if (data != null && data.Length > 0)
        {
            try
            {
                var saved = SerializerUtil.Deserialize<ClaimFlagsSaveData>(data);
                ImportClaimFlagsSaveData(saved);
            }
            catch (Exception exception)
            {
                serverApi?.Logger.Error("[SwixyClaimChunk] Failed to load claim flags: {0}", exception);
            }
        }

        if (claimFlagsByClaimKey.Count == 0 && serverApi != null)
        {
            try
            {
                var generic = serverApi.WorldManager.SaveGame.GetData<ClaimFlagsSaveData>(
                    ClaimConstants.ClaimFlagsSaveKey + "_obj",
                    null!);
                ImportClaimFlagsSaveData(generic);
            }
            catch
            {
                // ignore
            }
        }

        serverApi?.Logger.Notification(
            "[SwixyClaimChunk] Loaded claim flags: {0} entries",
            claimFlagsByClaimKey.Count);
    }

    private void ImportClaimFlagsSaveData(ClaimFlagsSaveData? saved)
    {
        if (saved?.Entries == null)
        {
            return;
        }

        foreach (var entry in saved.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == 0)
            {
                continue;
            }

            claimFlagsByClaimKey[entry.Key] = entry.Value
                & (ClaimFlagBits.AllowPvp | ClaimFlagBits.ProtectAnimals);
        }
    }

    private void OnClaimFlagsSaveGameSaving() => PersistClaimFlagsNow();

    private void PersistClaimFlagsNow()
    {
        if (serverApi == null)
        {
            return;
        }

        var payload = new ClaimFlagsSaveData();
        foreach (var entry in claimFlagsByClaimKey)
        {
            if (entry.Value == 0)
            {
                continue;
            }

            payload.Entries[entry.Key] = entry.Value;
        }

        try
        {
            serverApi.WorldManager.SaveGame.StoreData(
                ClaimConstants.ClaimFlagsSaveKey,
                SerializerUtil.Serialize(payload));
            serverApi.WorldManager.SaveGame.StoreData(
                ClaimConstants.ClaimFlagsSaveKey + "_obj",
                payload);
        }
        catch (Exception exception)
        {
            serverApi.Logger.Error("[SwixyClaimChunk] Failed to save claim flags: {0}", exception);
        }
    }

    /// <summary>Attach lightweight damage filter behavior to every living entity.</summary>
    private void AttachClaimProtectBehavior(Entity entity)
    {
        if (entity == null || !entity.Alive)
        {
            return;
        }

        if (entity.HasBehavior(ClaimConstants.ClaimProtectBehaviorCode))
        {
            return;
        }

        try
        {
            entity.AddBehavior(new EntityBehaviorClaimProtect(entity));
        }
        catch
        {
            // ignore entities that reject behaviors
        }
    }

    /// <summary>
    /// Server authority for claim flags on damage.
    /// Returns true if damage should be cancelled (set to 0).
    /// </summary>
    internal bool ShouldCancelDamageInClaim(Entity target, DamageSource damageSource, ref float damage)
    {
        if (serverApi == null || damage <= 0 || target?.World == null)
        {
            return false;
        }

        var attacker = ResolveAttackingEntity(damageSource);
        // Environmental damage (fall, fire without source entity) — leave alone.
        if (attacker == null)
        {
            return false;
        }

        var pos = target.Pos?.AsBlockPos;
        if (pos == null)
        {
            return false;
        }

        var claims = serverApi.World.Claims.Get(pos);
        if (claims == null || claims.Length == 0)
        {
            return false;
        }

        IPlayer? attackerPlayer = (attacker as EntityPlayer)?.Player;

        foreach (var claim in claims)
        {
            if (claim == null)
            {
                continue;
            }

            var flags = GetClaimFlags(claim);

            // --- PvP ---
            if ((flags & ClaimFlagBits.AllowPvp) == 0
                && target is EntityPlayer
                && attacker is EntityPlayer)
            {
                // Friendly fire / self: still block? Allow self-damage (void, etc. has no attacker).
                // PvP: cancel player→player.
                if (!ReferenceEquals(target, attacker))
                {
                    NotifyDamageBlocked(attackerPlayer, "swixyclaimchunk:claim-flags-pvp-blocked");
                    return true;
                }
            }

            // --- Animals ---
            if ((flags & ClaimFlagBits.ProtectAnimals) != 0
                && IsProtectableAnimal(target)
                && attacker is EntityPlayer)
            {
                var uid = attackerPlayer?.PlayerUID;
                // Owner / co-owner / Build may still kill animals (farming).
                if (!IsClaimOwner(claim, uid)
                    && !IsCoOwner(claim, uid)
                    && !ClaimUseFilterLogic.HasBuildAccess(claim, uid))
                {
                    NotifyDamageBlocked(attackerPlayer, "swixyclaimchunk:claim-flags-animals-blocked");
                    return true;
                }
            }
        }

        return false;
    }

    private void NotifyDamageBlocked(IPlayer? player, string langKey)
    {
        if (player is not IServerPlayer sp)
        {
            return;
        }

        // Throttle chat spam: only every 2s per player.
        var now = serverApi?.World.ElapsedMilliseconds ?? 0;
        if (damageNotifyCooldown.TryGetValue(sp.PlayerUID, out var last) && now - last < 2000)
        {
            return;
        }

        damageNotifyCooldown[sp.PlayerUID] = now;
        sp.SendMessage(
            GlobalConstants.GeneralChatGroup,
            Lang.GetL(sp.LanguageCode, langKey),
            EnumChatType.Notification);
    }

    private static Entity? ResolveAttackingEntity(DamageSource damageSource)
    {
        if (damageSource == null)
        {
            return null;
        }

        // Melee: SourceEntity is attacker. Projectiles: CauseEntity is thrower.
        if (damageSource.CauseEntity != null)
        {
            return damageSource.CauseEntity;
        }

        return damageSource.SourceEntity;
    }

    /// <summary>Passive / farm creatures — not players, not common hostiles.</summary>
    internal static bool IsProtectableAnimal(Entity entity)
    {
        if (entity is EntityPlayer || entity is not EntityAgent)
        {
            return false;
        }

        var path = entity.Code?.Path ?? "";
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Utility / non-animals.
        if (path.Contains("trader", StringComparison.OrdinalIgnoreCase)
            || path.Contains("armorstand", StringComparison.OrdinalIgnoreCase)
            || path.Contains("strawdummy", StringComparison.OrdinalIgnoreCase)
            || path.Contains("boat", StringComparison.OrdinalIgnoreCase)
            || path.Contains("humanoid", StringComparison.OrdinalIgnoreCase)
            || path.Contains("echochamber", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Known hostiles / temporal.
        if (path.Contains("drifter", StringComparison.OrdinalIgnoreCase)
            || path.Contains("locust", StringComparison.OrdinalIgnoreCase)
            || path.Contains("bell", StringComparison.OrdinalIgnoreCase)
            || path.Contains("shiver", StringComparison.OrdinalIgnoreCase)
            || path.Contains("bowtorn", StringComparison.OrdinalIgnoreCase)
            || path.Contains("eidolon", StringComparison.OrdinalIgnoreCase)
            || path.Contains("boreworm", StringComparison.OrdinalIgnoreCase)
            || path.Contains("wolf", StringComparison.OrdinalIgnoreCase) // wild predators
            || path.Contains("bear", StringComparison.OrdinalIgnoreCase)
            || path.Contains("hyena", StringComparison.OrdinalIgnoreCase)
            || path.Contains("fox", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// Lightweight behavior: filters damage using claim flags (server only).
/// </summary>
public sealed class EntityBehaviorClaimProtect : EntityBehavior
{
    public EntityBehaviorClaimProtect(Entity entity)
        : base(entity)
    {
    }

    public override string PropertyName() => ClaimConstants.ClaimProtectBehaviorCode;

    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
        if (damage <= 0 || entity.World?.Side != EnumAppSide.Server)
        {
            return;
        }

        var mod = entity.Api?.ModLoader?.GetModSystem<SwixyClaimChunkServerMod>();
        if (mod == null)
        {
            return;
        }

        if (mod.ShouldCancelDamageInClaim(entity, damageSource, ref damage))
        {
            damage = 0;
        }
    }
}
