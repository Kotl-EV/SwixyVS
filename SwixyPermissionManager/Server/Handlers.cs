using System;
using System.Collections.Generic;
using System.Linq;
using SwixyPermissionManager.Core;
using SwixyPermissionManager.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SwixyPermissionManager;

public sealed partial class SwixyPermissionManagerServerMod
{
    private void OnStateRequest(IServerPlayer fromPlayer, PermissionStateRequestPacket packet)
    {
        if (!CanManage(fromPlayer))
        {
            SendError(fromPlayer, "error-no-manage");
            return;
        }

        SendStateTo(fromPlayer);
    }

    private void OnAction(IServerPlayer fromPlayer, PermissionActionPacket packet)
    {
        if (!CanManage(fromPlayer))
        {
            SendError(fromPlayer, "error-no-manage");
            return;
        }

        if (serverApi == null || packet == null)
        {
            return;
        }

        serverApi.Logger.Notification(
            "[SwixyPermissionManager] Action={0} role='{1}' text='{2}' from {3}",
            packet.Action, packet.RoleCode, packet.TextValue, fromPlayer.PlayerName);

        bool success;
        string resultKey;
        object[]? args;

        try
        {
            (success, resultKey, args) = packet.Action switch
            {
                PermissionActionType.CreateRole => CreateRole(packet.RoleCode, packet.TextValue),
                PermissionActionType.RenameRole => RenameRole(packet.RoleCode, packet.TextValue),
                PermissionActionType.CloneRole => CloneRole(packet.RoleCode, packet.TextValue),
                PermissionActionType.DeleteRole => DeleteRole(packet.RoleCode),
                PermissionActionType.SetDescription => SetRoleDescription(packet.RoleCode, packet.TextValue),
                PermissionActionType.GrantPrivilege => GrantPrivilege(packet.RoleCode, packet.TextValue),
                PermissionActionType.RevokePrivilege => RevokePrivilege(packet.RoleCode, packet.TextValue),
                PermissionActionType.SetPrivilegeLevel => SetPrivilegeLevel(packet.RoleCode, packet.IntValue),
                PermissionActionType.SetPlayerRole => SetPlayerRole(packet.TextValue, packet.RoleCode),
                PermissionActionType.SetLandClaimAllowance => SetLandClaimAllowance(packet.RoleCode, packet.IntValue),
                PermissionActionType.SetLandClaimMaxAreas => SetLandClaimMaxAreas(packet.RoleCode, packet.IntValue),
                PermissionActionType.SetLandClaimMinSize => SetLandClaimMinSize(
                    packet.RoleCode, packet.IntValue, packet.IntValue2, packet.IntValue3),
                PermissionActionType.SetClaimSettings => SetClaimSettings(
                    packet.RoleCode,
                    packet.IntValue,
                    packet.IntValue2,
                    packet.IntValue3,
                    ParseMinAxis(packet.TextValue, 0, packet.IntValue4 > 0 ? packet.IntValue4 : 6),
                    ParseMinAxis(packet.TextValue, 1, 6),
                    ParseMinAxis(packet.TextValue, 2, 6)),
                _ => (false, "error-unknown-action", null),
            };
        }
        catch (Exception ex)
        {
            serverApi.Logger.Error("[SwixyPermissionManager] Action failed: {0}", ex);
            success = false;
            resultKey = "error-unknown";
            args = null;
        }

        var message = string.IsNullOrEmpty(resultKey)
            ? ""
            : Lang.GetL(fromPlayer.LanguageCode, "swixypermissionmanager:" + resultKey, args ?? []);

        // After create/clone the sanitized code is the last lang arg — use it for selection.
        var selected = packet.RoleCode ?? "";
        if (success
            && packet.Action is PermissionActionType.CreateRole or PermissionActionType.CloneRole
            && args is { Length: > 0 }
            && args[^1] is string createdCode
            && !string.IsNullOrWhiteSpace(createdCode))
        {
            selected = createdCode;
        }

        var state = BuildStatePacket(fromPlayer, message, success ? 0 : 1, selected);

        serverApi.Logger.Notification(
            "[SwixyPermissionManager] Action {0} success={1} selected='{2}' rolesInState={3} msg='{4}'",
            packet.Action, success, selected, state.Roles?.Count ?? 0, message);

        // Result (with embedded state) + standalone state for clients that only listen to one type.
        serverChannel?.SendPacket(new PermissionActionResultPacket
        {
            Success = success,
            Message = message,
            State = state,
        }, fromPlayer);
        serverChannel?.SendPacket(state, fromPlayer);

        if (!success)
        {
            serverApi.Logger.Warning(
                "[SwixyPermissionManager] Action {0} failed: {1}",
                packet.Action, message);
        }
    }

    private void SendStateTo(IServerPlayer player, string status = "", int messageType = 0, string selected = "")
    {
        serverChannel?.SendPacket(BuildStatePacket(player, status, messageType, selected), player);
    }

    private void SendError(IServerPlayer player, string langKey)
    {
        var msg = Lang.GetL(player.LanguageCode, "swixypermissionmanager:" + langKey);
        serverChannel?.SendPacket(new PermissionActionResultPacket
        {
            Success = false,
            Message = msg,
            State = null,
        }, player);
    }

    private PermissionStatePacket BuildStatePacket(
        IServerPlayer forPlayer,
        string status = "",
        int messageType = 0,
        string selectedRole = "")
    {
        var lang = forPlayer.LanguageCode;
        var roles = GetRoles();

        // Count members online + known offline by role code
        var memberCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var players = new List<PlayerInfoPacket>();

        if (serverApi != null)
        {
            var onlineUids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in serverApi.World.AllOnlinePlayers.OfType<IServerPlayer>())
            {
                onlineUids.Add(p.PlayerUID);
                var roleCode = p.Role?.Code ?? "";
                if (!string.IsNullOrEmpty(roleCode))
                {
                    memberCounts[roleCode] = memberCounts.GetValueOrDefault(roleCode) + 1;
                }

                players.Add(new PlayerInfoPacket
                {
                    Uid = p.PlayerUID,
                    Name = p.PlayerName ?? p.PlayerUID,
                    RoleCode = roleCode,
                    Online = true,
                });
            }

            // Offline known players (PlayerDataByUid)
            try
            {
                foreach (var pair in serverApi.PlayerData.PlayerDataByUid)
                {
                    var data = pair.Value;
                    if (data == null || onlineUids.Contains(data.PlayerUID))
                    {
                        continue;
                    }

                    var roleCode = GetOfflineRoleCode(data);
                    if (!string.IsNullOrEmpty(roleCode))
                    {
                        memberCounts[roleCode] = memberCounts.GetValueOrDefault(roleCode) + 1;
                    }

                    players.Add(new PlayerInfoPacket
                    {
                        Uid = data.PlayerUID,
                        Name = data.LastKnownPlayername ?? data.PlayerUID,
                        RoleCode = roleCode,
                        Online = false,
                    });
                }
            }
            catch
            {
                // PlayerData dictionary shape may vary
            }
        }

        players = players
            .OrderByDescending(p => p.Online)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();

        var rolePackets = roles.Select(r =>
        {
            var min = r.LandClaimMinSize;
            return new RolePacket
            {
                Code = r.Code ?? "",
                Name = r.Name ?? r.Code ?? "",
                Description = r.Description ?? "",
                PrivilegeLevel = r.PrivilegeLevel,
                Privileges = r.Privileges?.ToList() ?? [],
                // LandClaimAllowance = чанки (семантика SwixyClaimChunk / serverconfig).
                LandClaimAllowance = r.LandClaimAllowance,
                LandClaimMaxAreas = r.LandClaimMaxAreas,
                LandClaimMinX = min?.X ?? 6,
                LandClaimMinY = min?.Y ?? 6,
                LandClaimMinZ = min?.Z ?? 6,
                AutoGrant = r.AutoGrant,
                IsProtected = IsProtectedRole(r.Code),
                MemberCount = memberCounts.GetValueOrDefault(r.Code ?? ""),
            };
        }).ToList();

        var privPackets = PrivilegeCatalog.AllCodes()
            .Select(code => new PrivilegeInfoPacket
            {
                Code = code,
                Title = PrivilegeCatalog.GetTitle(code, lang),
                Description = PrivilegeCatalog.GetDescription(code, lang),
            })
            .ToList();

        // Include custom privileges already on roles but not in catalog
        var known = new HashSet<string>(privPackets.Select(p => p.Code), StringComparer.OrdinalIgnoreCase);
        foreach (var r in roles)
        {
            if (r.Privileges == null)
            {
                continue;
            }

            foreach (var code in r.Privileges)
            {
                if (string.IsNullOrWhiteSpace(code) || !known.Add(code))
                {
                    continue;
                }

                privPackets.Add(new PrivilegeInfoPacket
                {
                    Code = code,
                    Title = PrivilegeCatalog.GetTitle(code, lang),
                    Description = PrivilegeCatalog.GetDescription(code, lang),
                });
            }
        }

        privPackets = privPackets.OrderBy(p => p.Code, StringComparer.OrdinalIgnoreCase).ToList();

        return new PermissionStatePacket
        {
            Roles = rolePackets,
            Privileges = privPackets,
            Players = players,
            DefaultRoleCode = Config?.DefaultRoleCode ?? "suplayer",
            StatusMessage = status ?? "",
            MessageType = messageType,
            SelectedRoleCode = selectedRole ?? "",
        };
    }

    /// <summary>Парсит "x,y,z" или "x y z" для min claim size.</summary>
    private static int ParseMinAxis(string? packed, int index, int fallback)
    {
        if (string.IsNullOrWhiteSpace(packed))
        {
            return fallback;
        }

        var parts = packed.Split([',', ' ', ';', 'x', 'X'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (index < 0 || index >= parts.Length)
        {
            return fallback;
        }

        return int.TryParse(parts[index], out var v) ? v : fallback;
    }

    private static string GetOfflineRoleCode(IServerPlayerData data)
    {
        try
        {
            var prop = data.GetType().GetProperty("RoleCode");
            if (prop?.GetValue(data) is string code && !string.IsNullOrEmpty(code))
            {
                return code;
            }

            prop = data.GetType().GetProperty("Role");
            var role = prop?.GetValue(data);
            if (role is IPlayerRole ir)
            {
                return ir.Code ?? "";
            }

            var codeProp = role?.GetType().GetProperty("Code");
            if (codeProp?.GetValue(role) is string c2)
            {
                return c2;
            }
        }
        catch
        {
            // ignore
        }

        return "";
    }
}
