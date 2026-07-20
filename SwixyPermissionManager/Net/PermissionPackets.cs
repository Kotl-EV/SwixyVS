// =============================================================================
// Пакеты GUI-оболочки над ванильными Roles / Privilege / player role assign.
// =============================================================================

using System.Collections.Generic;
using ProtoBuf;

namespace SwixyPermissionManager.Net;

public static class PermissionActionType
{
    public const int CreateRole = 1;
    public const int RenameRole = 2;
    public const int DeleteRole = 3;
    public const int SetDescription = 4;
    public const int GrantPrivilege = 5;
    public const int RevokePrivilege = 6;
    public const int SetPrivilegeLevel = 7;
    public const int SetPlayerRole = 8;
    public const int SetLandClaimAllowance = 9;
    public const int SetLandClaimMaxAreas = 10;
    /// <summary>Min claim cuboid size: IntValue=X, IntValue2=Y, IntValue3=Z.</summary>
    public const int SetLandClaimMinSize = 11;
    /// <summary>Apply several claim-related numeric fields at once (TextValue JSON-ish or packed).</summary>
    public const int SetClaimSettings = 12;
    /// <summary>Clone RoleCode → new code/name in TextValue.</summary>
    public const int CloneRole = 13;
}

[ProtoContract]
public class PermissionStateRequestPacket
{
}

[ProtoContract]
public class PermissionOpenGuiPacket
{
}

[ProtoContract]
public class PermissionStatePacket
{
    [ProtoMember(1)]
    public List<RolePacket> Roles { get; set; } = [];

    [ProtoMember(2)]
    public List<PrivilegeInfoPacket> Privileges { get; set; } = [];

    [ProtoMember(3)]
    public List<PlayerInfoPacket> Players { get; set; } = [];

    [ProtoMember(4)]
    public string DefaultRoleCode { get; set; } = "";

    [ProtoMember(5)]
    public string StatusMessage { get; set; } = "";

    [ProtoMember(6)]
    public int MessageType { get; set; }

    [ProtoMember(7)]
    public string SelectedRoleCode { get; set; } = "";
}

[ProtoContract]
public class RolePacket
{
    [ProtoMember(1)]
    public string Code { get; set; } = "";

    [ProtoMember(2)]
    public string Name { get; set; } = "";

    [ProtoMember(3)]
    public string Description { get; set; } = "";

    [ProtoMember(4)]
    public int PrivilegeLevel { get; set; }

    [ProtoMember(5)]
    public List<string> Privileges { get; set; } = [];

    [ProtoMember(6)]
    public int LandClaimAllowance { get; set; }

    [ProtoMember(7)]
    public int LandClaimMaxAreas { get; set; }

    [ProtoMember(8)]
    public bool AutoGrant { get; set; }

    [ProtoMember(9)]
    public bool IsProtected { get; set; }

    [ProtoMember(10)]
    public int MemberCount { get; set; }

    [ProtoMember(11)]
    public int LandClaimMinX { get; set; }

    [ProtoMember(12)]
    public int LandClaimMinY { get; set; }

    [ProtoMember(13)]
    public int LandClaimMinZ { get; set; }
}

[ProtoContract]
public class PrivilegeInfoPacket
{
    [ProtoMember(1)]
    public string Code { get; set; } = "";

    [ProtoMember(2)]
    public string Title { get; set; } = "";

    [ProtoMember(3)]
    public string Description { get; set; } = "";
}

[ProtoContract]
public class PlayerInfoPacket
{
    [ProtoMember(1)]
    public string Uid { get; set; } = "";

    [ProtoMember(2)]
    public string Name { get; set; } = "";

    [ProtoMember(3)]
    public string RoleCode { get; set; } = "";

    [ProtoMember(4)]
    public bool Online { get; set; }
}

[ProtoContract]
public class PermissionActionPacket
{
    [ProtoMember(1)]
    public int Action { get; set; }

    /// <summary>Код роли (role code).</summary>
    [ProtoMember(2)]
    public string RoleCode { get; set; } = "";

    /// <summary>Имя / description / privilege code / player name — по действию.</summary>
    [ProtoMember(3)]
    public string TextValue { get; set; } = "";

    [ProtoMember(4)]
    public int IntValue { get; set; }

    /// <summary>Второй текст: для Create — name; для SetPlayerRole — player uid/name.</summary>
    [ProtoMember(5)]
    public string TextValue2 { get; set; } = "";

    [ProtoMember(6)]
    public int IntValue2 { get; set; }

    [ProtoMember(7)]
    public int IntValue3 { get; set; }

    /// <summary>Четвёртое int-поле (claim settings batch: max areas / level).</summary>
    [ProtoMember(8)]
    public int IntValue4 { get; set; }
}

[ProtoContract]
public class PermissionActionResultPacket
{
    [ProtoMember(1)]
    public bool Success { get; set; }

    [ProtoMember(2)]
    public string Message { get; set; } = "";

    [ProtoMember(3)]
    public PermissionStatePacket? State { get; set; }
}
