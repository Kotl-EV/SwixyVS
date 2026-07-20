using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SwixyPermissionManager.Core;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SwixyPermissionManager;

/// <summary>
/// Работа с ванильными <see cref="IPlayerRole"/> в serverconfig (Roles).
/// Create/Delete через reflection на Vintagestory.Common.PlayerRole + MarkConfigDirty.
/// </summary>
public sealed partial class SwixyPermissionManagerServerMod
{
    private static readonly Regex RoleCodeSanitize = new(@"[^a-z0-9_\-]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private IServerConfig? Config => serverApi?.Server?.Config;

    private IReadOnlyList<IPlayerRole> GetRoles()
    {
        // Use concrete ServerConfig.Roles (List<PlayerRole>) — interface getter can wrap/copy.
        var roles = EnumerateConcreteRoles().ToList();
        if (roles.Count == 0 && Config?.Roles != null)
        {
            roles = Config.Roles.ToList();
        }

        return roles
            .OrderBy(r => r.PrivilegeLevel)
            .ThenBy(r => r.Name ?? r.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IPlayerRole? FindRole(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || serverApi == null)
        {
            return null;
        }

        // Prefer Permissions.GetRole — same instances as RolesByCode.
        try
        {
            var byPerm = serverApi.Permissions.GetRole(code);
            if (byPerm != null)
            {
                return byPerm;
            }
        }
        catch
        {
            // ignore
        }

        foreach (var role in EnumerateConcreteRoles())
        {
            if (string.Equals(role.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return role;
            }
        }

        return Config?.Roles?.FirstOrDefault(r =>
            string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Реальный List&lt;PlayerRole&gt; в ServerConfig.
    /// Нельзя использовать IServerConfig.Roles — explicit getter отдаёт ConvertAll-копию,
    /// Add в неё не попадает в serverconfig (create «успешен», роль не видна).
    /// Берём backing field / public property, никогда explicit IServerConfig.Roles.
    /// </summary>
    private object? GetConcreteRolesList()
    {
        if (serverApi == null)
        {
            return null;
        }

        try
        {
            var configObj = serverApi.Server.Config;
            var t = configObj.GetType();

            // 1) Auto-property field — однозначная ссылка на List<PlayerRole>
            var field = t.GetField("<Roles>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!f.Name.Contains("Roles", StringComparison.OrdinalIgnoreCase)
                        || !f.FieldType.IsGenericType
                        || f.FieldType.GetGenericTypeDefinition() != typeof(List<>))
                    {
                        continue;
                    }

                    // Skip List<IPlayerRole> (interface copy); keep List<PlayerRole>
                    if (f.FieldType.GetGenericArguments()[0] == typeof(IPlayerRole))
                    {
                        continue;
                    }

                    field = f;
                    break;
                }
            }

            if (field != null)
            {
                var viaField = field.GetValue(configObj);
                if (viaField != null)
                {
                    return viaField;
                }
            }

            // 2) Public Roles property whose T is not IPlayerRole
            foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.Name != "Roles"
                    || !prop.PropertyType.IsGenericType
                    || prop.PropertyType.GetGenericTypeDefinition() != typeof(List<>))
                {
                    continue;
                }

                if (prop.PropertyType.GetGenericArguments()[0] == typeof(IPlayerRole))
                {
                    continue;
                }

                var value = prop.GetValue(configObj);
                if (value != null)
                {
                    return value;
                }
            }

            serverApi.Logger.Error(
                "[SwixyPermissionManager] GetConcreteRolesList: no concrete Roles list on {0}",
                t.FullName);
            return null;
        }
        catch (Exception ex)
        {
            serverApi.Logger.Warning("[SwixyPermissionManager] GetConcreteRolesList: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>Конкретный List&lt;PlayerRole&gt; из ServerConfig (не interface wrapper).</summary>
    private IEnumerable<IPlayerRole> EnumerateConcreteRoles()
    {
        var listObj = GetConcreteRolesList() ?? (object?)Config?.Roles;
        if (listObj is not System.Collections.IEnumerable enumerable)
        {
            yield break;
        }

        foreach (var item in enumerable)
        {
            if (item is IPlayerRole role)
            {
                yield return role;
            }
        }
    }

    private bool TryAddRoleToConfig(object roleObj)
    {
        if (serverApi == null)
        {
            return false;
        }

        var list = GetConcreteRolesList();
        if (list == null)
        {
            serverApi.Logger.Error("[SwixyPermissionManager] Concrete Roles list is null");
            return false;
        }

        try
        {
            var before = list is System.Collections.ICollection col ? col.Count : -1;

            // Prefer IList.Add — works for List<PlayerRole>
            if (list is System.Collections.IList ilist)
            {
                ilist.Add(roleObj);
            }
            else
            {
                var add = list.GetType().GetMethod("Add", [roleObj.GetType()])
                          ?? list.GetType().GetMethods()
                              .FirstOrDefault(m => m.Name == "Add"
                                                   && m.GetParameters().Length == 1
                                                   && m.GetParameters()[0].ParameterType.IsInstanceOfType(roleObj));
                if (add == null)
                {
                    serverApi.Logger.Error(
                        "[SwixyPermissionManager] No Add method on Roles list type {0}", list.GetType());
                    return false;
                }

                add.Invoke(list, [roleObj]);
            }

            var after = list is System.Collections.ICollection col2 ? col2.Count : -1;
            serverApi.Logger.Notification(
                "[SwixyPermissionManager] TryAddRoleToConfig type={0} count {1}→{2}",
                list.GetType().FullName, before, after);
            return after < 0 || after > before || before < 0;
        }
        catch (Exception ex)
        {
            serverApi.Logger.Error("[SwixyPermissionManager] TryAddRoleToConfig failed: {0}", ex);
            return false;
        }
    }

    private bool TryRemoveRoleFromConfig(IPlayerRole role)
    {
        var list = GetConcreteRolesList();
        if (list == null)
        {
            return false;
        }

        try
        {
            if (list is System.Collections.IList ilist)
            {
                // Remove by reference or by matching code
                for (var i = ilist.Count - 1; i >= 0; i--)
                {
                    if (ilist[i] is IPlayerRole r
                        && string.Equals(r.Code, role.Code, StringComparison.OrdinalIgnoreCase))
                    {
                        ilist.RemoveAt(i);
                        return true;
                    }
                }
            }

            var remove = list.GetType().GetMethod("Remove", [role.GetType()]);
            if (remove != null)
            {
                return remove.Invoke(list, [role]) as bool? ?? false;
            }
        }
        catch (Exception ex)
        {
            serverApi?.Logger.Error("[SwixyPermissionManager] TryRemoveRoleFromConfig: {0}", ex);
        }

        return false;
    }

    private void PersistConfig()
    {
        if (serverApi == null)
        {
            return;
        }

        try
        {
            var configObj = serverApi.Server.Config;

            // IMPORTANT: do NOT call InitializeRoles() here.
            // It re-seeds default roles and for AutoGrant roles does:
            //   Privileges = Privileges.Union(Privilege.AllCodes())
            // which undoes every Revoke on admin / AutoGrant groups.

            RebuildRolesByCode(configObj);

            var save = configObj.GetType().GetMethod(
                "Save",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            save?.Invoke(configObj, null);
        }
        catch (Exception ex)
        {
            serverApi.Logger.Warning("[SwixyPermissionManager] PersistConfig failed: {0}", ex.Message);
        }

        serverApi.Server.MarkConfigDirty();
    }

    /// <summary>Обновить RolesByCode без пересборки привилегий (в отличие от InitializeRoles).</summary>
    private void RebuildRolesByCode(object configObj)
    {
        try
        {
            var field = configObj.GetType().GetField(
                "RolesByCode",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            var dict = field.GetValue(configObj);
            if (dict == null)
            {
                // Create Dictionary<string, PlayerRole> if missing
                var playerRoleType = Type.GetType("Vintagestory.Common.PlayerRole, VintagestoryLib");
                if (playerRoleType == null)
                {
                    return;
                }

                var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), playerRoleType);
                dict = Activator.CreateInstance(dictType);
                if (dict == null)
                {
                    return;
                }

                field.SetValue(configObj, dict);
            }

            if (dict == null)
            {
                return;
            }

            var clear = dict.GetType().GetMethod("Clear");
            clear?.Invoke(dict, null);

            var indexer = dict.GetType().GetProperty("Item");
            foreach (var role in EnumerateConcreteRoles())
            {
                if (string.IsNullOrEmpty(role.Code))
                {
                    continue;
                }

                indexer?.SetValue(dict, role, [role.Code]);
            }

            // DefaultRole field
            var defaultCode = Config?.DefaultRoleCode ?? "suplayer";
            var defaultRoleField = configObj.GetType().GetField(
                "DefaultRole",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (defaultRoleField != null)
            {
                var def = FindRole(defaultCode);
                if (def != null)
                {
                    defaultRoleField.SetValue(configObj, def);
                }
            }
        }
        catch (Exception ex)
        {
            serverApi?.Logger.Warning("[SwixyPermissionManager] RebuildRolesByCode: {0}", ex.Message);
        }
    }

    /// <summary>После смены прав роли — переназначить роль онлайн-игрокам (кэш привилегий).</summary>
    private void RefreshPlayersWithRole(string? roleCode)
    {
        if (serverApi == null || string.IsNullOrWhiteSpace(roleCode))
        {
            return;
        }

        foreach (var p in serverApi.World.AllOnlinePlayers.OfType<IServerPlayer>())
        {
            if (!string.Equals(p.Role?.Code, roleCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                serverApi.Permissions.SetRole(p, roleCode);
            }
            catch (Exception ex)
            {
                serverApi.Logger.Warning(
                    "[SwixyPermissionManager] Failed to refresh role for {0}: {1}",
                    p.PlayerName, ex.Message);
            }
        }
    }

    private (bool ok, string key, object[]? args) CreateRole(string? codeRaw, string? nameRaw)
    {
        if (Config?.Roles == null || serverApi == null)
        {
            return (false, "error-server-not-ready", null);
        }

        var name = (nameRaw ?? codeRaw ?? "").Trim();
        if (name.Length is < 1 or > 48)
        {
            return (false, "error-invalid-name", null);
        }

        var code = RoleCodeSanitize.Replace((codeRaw ?? name).Trim().ToLowerInvariant(), "");
        code = code.Trim('-', '_');
        if (string.IsNullOrEmpty(code))
        {
            return (false, "error-invalid-code", null);
        }

        if (FindRole(code) != null)
        {
            return (false, "error-role-exists", [code]);
        }

        var roleObj = CreatePlayerRoleInstance();
        if (roleObj is not IPlayerRole)
        {
            return (false, "error-create-role-type", null);
        }

        // Set via concrete properties (Name/Code/Description setters on PlayerRole).
        SetProp(roleObj, "Code", code);
        SetProp(roleObj, "Name", name);
        SetProp(roleObj, "Description", "");
        SetProp(roleObj, "PrivilegeLevel", 0);
        SetProp(roleObj, "DefaultGameMode", EnumGameMode.Survival);
        SetProp(roleObj, "Color", Color.White);
        SetProp(roleObj, "LandClaimAllowance", 0);
        SetProp(roleObj, "LandClaimMaxAreas", 3);
        SetProp(roleObj, "LandClaimMinSize", new Vec3i(6, 6, 6));
        SetProp(roleObj, "AutoGrant", false);
        SetProp(roleObj, "Privileges", new List<string> { Privilege.chat });
        SetProp(roleObj, "RuntimePrivileges", new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        if (!TryAddRoleToConfig(roleObj))
        {
            return (false, "error-unknown", null);
        }

        // Сразу в RolesByCode — иначе Permissions.GetRole не видит роль до PersistConfig.
        try
        {
            RebuildRolesByCode(serverApi.Server.Config);
        }
        catch (Exception ex)
        {
            serverApi.Logger.Warning("[SwixyPermissionManager] RebuildRolesByCode after create: {0}", ex.Message);
        }

        // Verify role is visible through the same path as GetRoles/FindRole
        if (FindRole(code) == null)
        {
            var concreteCount = EnumerateConcreteRoles().Count();
            var ifaceCount = Config.Roles?.Count ?? -1;
            serverApi.Logger.Error(
                "[SwixyPermissionManager] CreateRole '{0}' added but FindRole cannot see it. concrete={1} iface={2} listType={3}",
                code, concreteCount, ifaceCount, GetConcreteRolesList()?.GetType().FullName ?? "null");
            return (false, "error-unknown", null);
        }

        PersistConfig();
        serverApi.Logger.Notification(
            "[SwixyPermissionManager] Created role '{0}' ({1}). Total roles: {2}",
            name, code, EnumerateConcreteRoles().Count());
        // args: display name, code — Handlers uses last arg as SelectedRoleCode for create/clone
        return (true, "message-role-created", [name, code]);
    }

    private (bool ok, string key, object[]? args) CloneRole(string? sourceCode, string? newNameRaw)
    {
        var source = FindRole(sourceCode);
        if (source == null)
        {
            return (false, "error-role-not-found", null);
        }

        if (Config?.Roles == null || serverApi == null)
        {
            return (false, "error-server-not-ready", null);
        }

        var baseName = (newNameRaw ?? "").Trim();
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = (source.Name ?? source.Code ?? "role") + " copy";
        }

        if (baseName.Length > 48)
        {
            baseName = baseName[..48];
        }

        var code = RoleCodeSanitize.Replace(baseName.ToLowerInvariant(), "").Trim('-', '_');
        if (string.IsNullOrEmpty(code))
        {
            code = "role_" + Guid.NewGuid().ToString("N")[..8];
        }

        // Ensure unique code
        var unique = code;
        var n = 2;
        while (FindRole(unique) != null)
        {
            unique = code + n;
            n++;
            if (n > 99)
            {
                unique = code + "_" + Guid.NewGuid().ToString("N")[..4];
                break;
            }
        }

        code = unique;

        var roleObj = CreatePlayerRoleInstance();
        if (roleObj is not IPlayerRole)
        {
            return (false, "error-create-role-type", null);
        }

        var min = source.LandClaimMinSize ?? new Vec3i(6, 6, 6);
        var privs = source.Privileges?.ToList() ?? [Privilege.chat];

        SetProp(roleObj, "Code", code);
        SetProp(roleObj, "Name", baseName);
        SetProp(roleObj, "Description", source.Description ?? "");
        SetProp(roleObj, "PrivilegeLevel", source.PrivilegeLevel);
        SetProp(roleObj, "DefaultGameMode", source.DefaultGameMode);
        SetProp(roleObj, "Color", source.Color);
        SetProp(roleObj, "LandClaimAllowance", source.LandClaimAllowance);
        SetProp(roleObj, "LandClaimMaxAreas", source.LandClaimMaxAreas);
        SetProp(roleObj, "LandClaimMinSize", new Vec3i(min.X, min.Y, min.Z));
        SetProp(roleObj, "AutoGrant", source.AutoGrant);
        SetProp(roleObj, "Privileges", new List<string>(privs));
        SetProp(roleObj, "RuntimePrivileges", new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        if (!TryAddRoleToConfig(roleObj))
        {
            return (false, "error-unknown", null);
        }

        try
        {
            RebuildRolesByCode(serverApi.Server.Config);
        }
        catch (Exception ex)
        {
            serverApi.Logger.Warning("[SwixyPermissionManager] RebuildRolesByCode after clone: {0}", ex.Message);
        }

        if (FindRole(code) == null)
        {
            serverApi.Logger.Error(
                "[SwixyPermissionManager] CloneRole '{0}' added but FindRole cannot see it", code);
            return (false, "error-unknown", null);
        }

        PersistConfig();
        serverApi.Logger.Notification(
            "[SwixyPermissionManager] Cloned role → '{0}' ({1}). Total roles: {2}",
            baseName, code, EnumerateConcreteRoles().Count());
        return (true, "message-role-cloned", [source.Name ?? source.Code ?? "", baseName, code]);
    }

    private (bool ok, string key, object[]? args) RenameRole(string? code, string? newName)
    {
        var role = FindRole(code);
        if (role == null)
        {
            return (false, "error-role-not-found", null);
        }

        var name = (newName ?? "").Trim();
        if (name.Length is < 1 or > 48)
        {
            return (false, "error-invalid-name", null);
        }

        // IPlayerRole.Name is get-only on interface — set on concrete.
        if (!SetProp(role, "Name", name))
        {
            return (false, "error-rename-failed", null);
        }

        PersistConfig();
        return (true, "message-role-renamed", [name]);
    }

    private (bool ok, string key, object[]? args) SetRoleDescription(string? code, string? text)
    {
        var role = FindRole(code);
        if (role == null)
        {
            return (false, "error-role-not-found", null);
        }

        var desc = (text ?? "").Trim();
        if (desc.Length > 240)
        {
            desc = desc[..240];
        }

        if (!SetProp(role, "Description", desc))
        {
            return (false, "error-unknown", null);
        }

        PersistConfig();
        return (true, "message-description-set", null);
    }

    private (bool ok, string key, object[]? args) DeleteRole(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || Config?.Roles == null)
        {
            return (false, "error-role-not-found", null);
        }

        if (IsProtectedRole(code))
        {
            return (false, "error-role-protected", [code]);
        }

        if (string.Equals(Config.DefaultRoleCode, code, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "error-role-is-default", [code]);
        }

        var role = FindRole(code);
        if (role == null)
        {
            return (false, "error-role-not-found", null);
        }

        // Reassign online players with this role to default.
        var defaultCode = Config.DefaultRoleCode ?? "suplayer";
        if (serverApi != null)
        {
            foreach (var p in serverApi.World.AllOnlinePlayers.OfType<IServerPlayer>())
            {
                if (string.Equals(p.Role?.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        serverApi.Permissions.SetRole(p, defaultCode);
                    }
                    catch
                    {
                        // ignore individual failures
                    }
                }
            }
        }

        if (!TryRemoveRoleFromConfig(role))
        {
            // Fallback interface list
            try
            {
                Config.Roles.Remove(role);
            }
            catch
            {
                return (false, "error-delete-failed", null);
            }
        }

        if (FindRole(code) != null)
        {
            return (false, "error-delete-failed", null);
        }

        PersistConfig();
        return (true, "message-role-deleted", [code]);
    }

    private (bool ok, string key, object[]? args) GrantPrivilege(string? roleCode, string? privilege)
    {
        var role = FindRole(roleCode);
        if (role == null)
        {
            serverApi?.Logger.Warning("[SwixyPermissionManager] Grant: role not found '{0}'", roleCode);
            return (false, "error-role-not-found", null);
        }

        var code = NormalizePrivilege(privilege);
        if (code == null)
        {
            serverApi?.Logger.Warning("[SwixyPermissionManager] Grant: invalid privilege '{0}'", privilege);
            return (false, "error-invalid-privilege", null);
        }

        var list = EnsurePrivilegesList(role);

        if (list.Any(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase)))
        {
            return (false, "error-privilege-already", [code]);
        }

        list.Add(code);
        // Keep list unique / stable
        SetProp(role, "Privileges", list);

        try
        {
            // List.Contains is case-sensitive — API may add again; we already checked.
            role.GrantPrivilege(code);
        }
        catch (Exception ex)
        {
            serverApi?.Logger.Warning("[SwixyPermissionManager] role.GrantPrivilege: {0}", ex.Message);
        }

        // Re-read after API
        list = EnsurePrivilegesList(role);
        if (!list.Any(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(code);
            SetProp(role, "Privileges", list);
        }

        PersistConfig();
        RefreshPlayersWithRole(role.Code);
        serverApi?.Logger.Notification(
            "[SwixyPermissionManager] Granted '{0}' → role '{1}'. Privileges({2}): [{3}]",
            code, role.Code, role.Privileges?.Count ?? 0, string.Join(", ", role.Privileges ?? []));
        return (true, "message-privilege-granted", [code, role.Name ?? role.Code]);
    }

    private (bool ok, string key, object[]? args) RevokePrivilege(string? roleCode, string? privilege)
    {
        var role = FindRole(roleCode);
        if (role == null)
        {
            serverApi?.Logger.Warning("[SwixyPermissionManager] Revoke: role not found '{0}'", roleCode);
            return (false, "error-role-not-found", null);
        }

        var code = NormalizePrivilege(privilege);
        if (code == null)
        {
            serverApi?.Logger.Warning("[SwixyPermissionManager] Revoke: invalid privilege '{0}'", privilege);
            return (false, "error-invalid-privilege", null);
        }

        // AutoGrant roles (admin) re-fill AllCodes() on InitializeRoles.
        // Even without that, AutoGrant means "has everything" — turn it off so a revoke sticks.
        if (role.AutoGrant)
        {
            role.AutoGrant = false;
            // Expand to explicit full set, then remove the one privilege.
            var all = Privilege.AllCodes().ToList();
            // Keep any extra custom codes already on the role
            foreach (var existing in role.Privileges ?? [])
            {
                if (!all.Any(a => string.Equals(a, existing, StringComparison.OrdinalIgnoreCase)))
                {
                    all.Add(existing);
                }
            }

            all.RemoveAll(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase));
            SetProp(role, "Privileges", all);
            serverApi?.Logger.Notification(
                "[SwixyPermissionManager] AutoGrant disabled on '{0}' so revoke of '{1}' can persist.",
                role.Code, code);
        }
        else
        {
            var list = EnsurePrivilegesList(role);
            var before = list.Count;
            // List.Remove is case-sensitive — remove all case variants
            var removed = list.RemoveAll(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                // Try exact API remove + case variants of stored codes
                try
                {
                    role.RevokePrivilege(code);
                }
                catch
                {
                    // ignore
                }

                list = EnsurePrivilegesList(role);
                removed = list.RemoveAll(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase));
            }

            if (removed == 0 && before == (role.Privileges?.Count ?? 0)
                && !(role.Privileges?.Any(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase)) ?? false))
            {
                return (false, "error-privilege-missing", [code]);
            }

            SetProp(role, "Privileges", list);

            try
            {
                role.RevokePrivilege(code);
            }
            catch (Exception ex)
            {
                serverApi?.Logger.Warning("[SwixyPermissionManager] role.RevokePrivilege: {0}", ex.Message);
            }

            // Final sweep — API uses case-sensitive Remove
            list = EnsurePrivilegesList(role);
            list.RemoveAll(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase));
            SetProp(role, "Privileges", list);
        }

        // Verify gone
        if (role.Privileges?.Any(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase)) == true)
        {
            serverApi?.Logger.Error(
                "[SwixyPermissionManager] Revoke failed to remove '{0}' from '{1}'",
                code, role.Code);
            return (false, "error-unknown", null);
        }

        PersistConfig();
        RefreshPlayersWithRole(role.Code);
        serverApi?.Logger.Notification(
            "[SwixyPermissionManager] Revoked '{0}' ← role '{1}'. Privileges({2}): [{3}]",
            code, role.Code, role.Privileges?.Count ?? 0, string.Join(", ", role.Privileges ?? []));
        return (true, "message-privilege-revoked", [code, role.Name ?? role.Code]);
    }

    private static List<string> EnsurePrivilegesList(IPlayerRole role)
    {
        var list = role.Privileges;
        if (list != null)
        {
            return list;
        }

        list = [];
        // interface may not allow set — use reflection on concrete
        var prop = role.GetType().GetProperty("Privileges");
        prop?.SetValue(role, list);
        return role.Privileges ?? list;
    }

    private (bool ok, string key, object[]? args) SetPrivilegeLevel(string? roleCode, int level)
    {
        var role = FindRole(roleCode);
        if (role == null)
        {
            return (false, "error-role-not-found", null);
        }

        if (IsProtectedRole(role.Code) && string.Equals(role.Code, "admin", StringComparison.OrdinalIgnoreCase))
        {
            // allow but clamp
        }

        level = Math.Clamp(level, -1, 99999);
        if (!SetProp(role, "PrivilegeLevel", level))
        {
            return (false, "error-unknown", null);
        }

        PersistConfig();
        return (true, "message-level-set", [role.Name ?? role.Code, level]);
    }

    /// <param name="value">Лимит в <b>чанках</b> (как хранит SwixyClaimChunk в serverconfig).</param>
    private (bool ok, string key, object[]? args) SetLandClaimAllowance(string? roleCode, int value)
    {
        var role = FindRole(roleCode);
        if (role == null)
        {
            return (false, "error-role-not-found", null);
        }

        // Пишем как есть: LandClaimAllowance = число чанков (не блоков).
        value = Math.Clamp(value, 0, 50_000_000);
        role.LandClaimAllowance = value;
        PersistConfig();
        serverApi?.Logger.Notification(
            "[SwixyPermissionManager] LandClaimAllowance role={0}: {1} chunks",
            role.Code, value);
        return (true, "message-claim-allowance-set", [role.Name ?? role.Code, value]);
    }

    private (bool ok, string key, object[]? args) SetLandClaimMaxAreas(string? roleCode, int value)
    {
        var role = FindRole(roleCode);
        if (role == null)
        {
            return (false, "error-role-not-found", null);
        }

        value = Math.Clamp(value, 0, 9999);
        role.LandClaimMaxAreas = value;
        PersistConfig();
        return (true, "message-claim-areas-set", [role.Name ?? role.Code, value]);
    }

    private (bool ok, string key, object[]? args) SetLandClaimMinSize(string? roleCode, int x, int y, int z)
    {
        var role = FindRole(roleCode);
        if (role == null)
        {
            return (false, "error-role-not-found", null);
        }

        x = Math.Clamp(x, 1, 1024);
        y = Math.Clamp(y, 1, 1024);
        z = Math.Clamp(z, 1, 1024);
        role.LandClaimMinSize = new Vec3i(x, y, z);
        PersistConfig();
        return (true, "message-claim-minsize-set", [role.Name ?? role.Code, x, y, z]);
    }

    /// <summary>
    /// Пакетно: level + allowance(чанки) + maxAreas + minSize XYZ(блоки).
    /// LandClaimAllowance в serverconfig = чанки (семантика SwixyClaimChunk).
    /// </summary>
    private (bool ok, string key, object[]? args) SetClaimSettings(
        string? roleCode,
        int level,
        int allowanceChunks,
        int maxAreas,
        int minX,
        int minY,
        int minZ)
    {
        var role = FindRole(roleCode);
        if (role == null)
        {
            return (false, "error-role-not-found", null);
        }

        level = Math.Clamp(level, -1, 99999);
        allowanceChunks = Math.Clamp(allowanceChunks, 0, 50_000_000);
        maxAreas = Math.Clamp(maxAreas, 0, 9999);
        minX = Math.Clamp(minX, 1, 1024);
        minY = Math.Clamp(minY, 1, 1024);
        minZ = Math.Clamp(minZ, 1, 1024);

        SetProp(role, "PrivilegeLevel", level);
        role.LandClaimAllowance = allowanceChunks;
        role.LandClaimMaxAreas = maxAreas;
        role.LandClaimMinSize = new Vec3i(minX, minY, minZ);

        PersistConfig();
        serverApi?.Logger.Notification(
            "[SwixyPermissionManager] Claim settings role={0}: level={1} chunks={2} areas={3} min={4},{5},{6}",
            role.Code, level, allowanceChunks, maxAreas, minX, minY, minZ);
        return (true, "message-claim-settings-set",
            [role.Name ?? role.Code, level, allowanceChunks, maxAreas, minX, minY, minZ]);
    }

    private (bool ok, string key, object[]? args) SetPlayerRole(string? playerNameOrUid, string? roleCode)
    {
        if (serverApi == null)
        {
            return (false, "error-server-not-ready", null);
        }

        var role = FindRole(roleCode);
        if (role == null)
        {
            return (false, "error-role-not-found", null);
        }

        if (!TryResolvePlayer(playerNameOrUid, out var uid, out var displayName))
        {
            return (false, "error-player-not-found", [playerNameOrUid ?? ""]);
        }

        // Online path
        var online = serverApi.World.AllOnlinePlayers
            .OfType<IServerPlayer>()
            .FirstOrDefault(p => p.PlayerUID == uid);

        if (online != null)
        {
            serverApi.Permissions.SetRole(online, role.Code);
            return (true, "message-player-role-set", [displayName, role.Name ?? role.Code]);
        }

        // Offline: mutate player data role code
        try
        {
            var data = serverApi.PlayerData.GetPlayerDataByUid(uid);
            if (data == null)
            {
                return (false, "error-player-not-found", [playerNameOrUid ?? ""]);
            }

            SetProp(data, "RoleCode", role.Code);
            // Some builds use PlayerRole property
            SetProp(data, "Role", role);

            return (true, "message-player-role-set", [displayName, role.Name ?? role.Code]);
        }
        catch (Exception ex)
        {
            serverApi.Logger.Error("[SwixyPermissionManager] Set offline role failed: {0}", ex);
            return (false, "error-player-offline-role", [displayName]);
        }
    }

    private bool TryResolvePlayer(string? nameOrUid, out string uid, out string displayName)
    {
        uid = "";
        displayName = "";
        if (serverApi == null || string.IsNullOrWhiteSpace(nameOrUid))
        {
            return false;
        }

        var key = nameOrUid.Trim();

        foreach (var p in serverApi.World.AllOnlinePlayers)
        {
            if (string.Equals(p.PlayerUID, key, StringComparison.Ordinal)
                || string.Equals(p.PlayerName, key, StringComparison.OrdinalIgnoreCase))
            {
                uid = p.PlayerUID;
                displayName = p.PlayerName ?? uid;
                return true;
            }
        }

        var byUid = serverApi.PlayerData.GetPlayerDataByUid(key);
        if (byUid != null)
        {
            uid = byUid.PlayerUID;
            displayName = byUid.LastKnownPlayername ?? uid;
            return true;
        }

        var byName = serverApi.PlayerData.GetPlayerDataByLastKnownName(key);
        if (byName != null)
        {
            uid = byName.PlayerUID;
            displayName = byName.LastKnownPlayername ?? uid;
            return true;
        }

        return false;
    }

    private static bool IsProtectedRole(string? code) =>
        !string.IsNullOrEmpty(code)
        && PermissionConstants.ProtectedRoleCodes.Any(p =>
            string.Equals(p, code, StringComparison.OrdinalIgnoreCase));

    private static string? NormalizePrivilege(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var code = raw.Trim().ToLowerInvariant();
        // Accept friendly aliases
        if (code is "buildblocks" or "buildblock")
        {
            code = Privilege.buildblocks; // "build"
        }

        if (code is "claimland")
        {
            code = Privilege.claimland; // "areamodify"
        }

        if (code.Length is < 2 or > 64)
        {
            return null;
        }

        return code;
    }

    private static object? CreatePlayerRoleInstance()
    {
        var type = Type.GetType("Vintagestory.Common.PlayerRole, VintagestoryLib")
                   ?? AppDomain.CurrentDomain.GetAssemblies()
                       .Select(a => a.GetType("Vintagestory.Common.PlayerRole"))
                       .FirstOrDefault(t => t != null);

        if (type == null)
        {
            return null;
        }

        return Activator.CreateInstance(type);
    }

    private static bool SetProp(object target, string name, object? value)
    {
        try
        {
            var prop = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanWrite)
            {
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
}
