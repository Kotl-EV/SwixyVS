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
        flags &= ClaimFlagBits.AllKnown;

        var keys = ClaimStorageKeys.EnumerateClaimStorageKeys(claim).ToList();
        if (keys.Count == 0)
        {
            return ClaimActionResult.Error("swixyclaimchunk:error-unknown");
        }

        // flags==0 = safe defaults (no PvP, animals protected) — can drop keys.
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

        // v0: bit1 = ProtectAnimals (opt-in protect). v1: bit1 = AllowAnimalDamage (opt-in hurt).
        var legacy = saved.Version < 1;

        foreach (var entry in saved.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == 0)
            {
                continue;
            }

            var flags = entry.Value & ClaimFlagBits.AllKnown;
            if (legacy)
            {
                // Invert animal bit: was "protect when set" → now "allow damage when set".
                if ((flags & ClaimFlagBits.AllowAnimalDamage) != 0)
                {
                    flags &= ~ClaimFlagBits.AllowAnimalDamage; // was protect → still protect
                }
                else
                {
                    flags |= ClaimFlagBits.AllowAnimalDamage; // was unprotected → allow damage
                }
            }

            if (flags != 0)
            {
                claimFlagsByClaimKey[entry.Key] = flags;
            }
        }
    }

    private void OnClaimFlagsSaveGameSaving() => PersistClaimFlagsNow();

    private void PersistClaimFlagsNow()
    {
        if (serverApi == null)
        {
            return;
        }

        var payload = new ClaimFlagsSaveData { Version = 1 };
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

    /// <summary>
    /// Attach damage filter — MUST run before EntityBehaviorHealth
    /// (health applies Health -= damage inside its own OnEntityReceiveDamage).
    /// </summary>
    private void AttachClaimProtectBehavior(Entity entity)
    {
        if (entity == null || entity.World?.Side != EnumAppSide.Server)
        {
            return;
        }

        var list = entity.SidedProperties?.Behaviors;
        if (list == null)
        {
            return;
        }

        try
        {
            var existing = entity.GetBehavior(ClaimConstants.ClaimProtectBehaviorCode);
            if (existing != null)
            {
                // Already before health?
                var idx = list.IndexOf(existing);
                var healthIdx = IndexOfBehavior(list, "health");
                if (idx >= 0 && (healthIdx < 0 || idx < healthIdx))
                {
                    return;
                }

                entity.RemoveBehavior(existing);
            }

            var behavior = new EntityBehaviorClaimProtect(entity);
            var insertAt = IndexOfBehavior(list, "health");
            if (insertAt < 0)
            {
                insertAt = 0;
            }

            list.Insert(insertAt, behavior);
            entity.CacheServerBehaviors();
        }
        catch
        {
            // ignore entities that reject behaviors
        }
    }

    private static int IndexOfBehavior(IList<EntityBehavior> list, string propertyName)
    {
        for (var i = 0; i < list.Count; i++)
        {
            try
            {
                if (string.Equals(list[i]?.PropertyName(), propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            catch
            {
                // next
            }
        }

        return -1;
    }

    /// <summary>Ensure online players always have protect behavior (join / already online).</summary>
    private void EnsurePlayersHaveClaimProtect(IServerPlayer? player = null)
    {
        if (serverApi == null)
        {
            return;
        }

        if (player?.Entity != null)
        {
            AttachClaimProtectBehavior(player.Entity);
            return;
        }

        foreach (var p in serverApi.World.AllOnlinePlayers)
        {
            if (p?.Entity != null)
            {
                AttachClaimProtectBehavior(p.Entity);
            }
        }
    }

    private void OnPlayerJoinAttachClaimProtect(IServerPlayer byPlayer)
        => EnsurePlayersHaveClaimProtect(byPlayer);

    private void OnSaveGameLoadedAttachClaimProtectToPlayers()
        => EnsurePlayersHaveClaimProtect();

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

        if (damageSource == null)
        {
            return false;
        }

        // Heal never blocked.
        if (damageSource.Type == EnumDamageType.Heal)
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

        IPlayer? attackerPlayer = null;
        if (attacker is EntityPlayer ep)
        {
            attackerPlayer = ep.Player ?? serverApi.World.PlayerByUid(ep.PlayerUID);
        }

        var targetIsPlayer = target is EntityPlayer;
        var attackerIsPlayer = attacker is EntityPlayer || attackerPlayer != null;

        foreach (var claim in claims)
        {
            if (claim == null)
            {
                continue;
            }

            var flags = GetClaimFlags(claim);

            // --- PvP (default OFF when AllowPvp bit is clear) ---
            if ((flags & ClaimFlagBits.AllowPvp) == 0
                && targetIsPlayer
                && attackerIsPlayer
                && !ReferenceEquals(target, attacker))
            {
                if (target is EntityPlayer tp
                    && attacker is EntityPlayer ap
                    && !string.IsNullOrEmpty(tp.PlayerUID)
                    && string.Equals(tp.PlayerUID, ap.PlayerUID, StringComparison.Ordinal))
                {
                    continue;
                }

                NotifyDamageBlocked(attackerPlayer, "swixyclaimchunk:claim-flags-pvp-blocked");
                return true;
            }

            // --- Animals (default ON: protected unless AllowAnimalDamage) ---
            if (ClaimFlagBits.AreAnimalsProtected(flags)
                && IsProtectableAnimal(target)
                && attackerIsPlayer)
            {
                var uid = attackerPlayer?.PlayerUID
                          ?? (attacker as EntityPlayer)?.PlayerUID;
                // Owner / co-owner / Build may still kill (farming). Strangers cannot.
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

        // Official helper: projectile → thrower; melee → SourceEntity.
        var cause = damageSource.GetCauseEntity();
        if (cause != null)
        {
            return cause;
        }

        return damageSource.SourceEntity ?? damageSource.CauseEntity;
    }

    /// <summary>
    /// Creatures worth protecting in a claim: farm/passive animals, pets.
    /// Not players, hostiles, traders, or utility entities.
    /// </summary>
    internal static bool IsProtectableAnimal(Entity entity)
    {
        if (entity is EntityPlayer || entity is not EntityAgent)
        {
            return false;
        }

        // Projectiles / items / falling blocks are not agents with meaningful codes usually.
        var path = entity.Code?.Path ?? "";
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Utility / non-animals.
        if (path.Contains("trader", StringComparison.OrdinalIgnoreCase)
            || path.Contains("armorstand", StringComparison.OrdinalIgnoreCase)
            || path.Contains("strawdummy", StringComparison.OrdinalIgnoreCase)
            || path.Contains("mannequin", StringComparison.OrdinalIgnoreCase)
            || path.Contains("boat", StringComparison.OrdinalIgnoreCase)
            || path.Contains("raft", StringComparison.OrdinalIgnoreCase)
            || path.Contains("humanoid", StringComparison.OrdinalIgnoreCase)
            || path.Contains("echochamber", StringComparison.OrdinalIgnoreCase)
            || path.Contains("projectile", StringComparison.OrdinalIgnoreCase)
            || path.Contains("item", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("thrown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Hostiles / temporal / predators — always killable in claim.
        if (path.Contains("drifter", StringComparison.OrdinalIgnoreCase)
            || path.Contains("locust", StringComparison.OrdinalIgnoreCase)
            || path.Contains("bell", StringComparison.OrdinalIgnoreCase)
            || path.Contains("shiver", StringComparison.OrdinalIgnoreCase)
            || path.Contains("bowtorn", StringComparison.OrdinalIgnoreCase)
            || path.Contains("eidolon", StringComparison.OrdinalIgnoreCase)
            || path.Contains("boreworm", StringComparison.OrdinalIgnoreCase)
            || path.Contains("wolf", StringComparison.OrdinalIgnoreCase)
            || path.Contains("bear", StringComparison.OrdinalIgnoreCase)
            || path.Contains("hyena", StringComparison.OrdinalIgnoreCase)
            || path.Contains("fox", StringComparison.OrdinalIgnoreCase)
            || path.Contains("shark", StringComparison.OrdinalIgnoreCase)
            || path.Contains("piranha", StringComparison.OrdinalIgnoreCase)
            || path.Contains("moose", StringComparison.OrdinalIgnoreCase) // aggressive wild
            || path.Contains("eidolon", StringComparison.OrdinalIgnoreCase)
            || path.Contains("drifter", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Everything else living (chicken, pig, sheep, goat, hare, deer, bee?, butterfly…) is protected.
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

        // Zero damage BEFORE EntityBehaviorHealth runs (this behavior is inserted at index 0).
        if (mod.ShouldCancelDamageInClaim(entity, damageSource, ref damage))
        {
            damage = 0;
        }
    }
}
