namespace SwixyPermissionManager.Core;

/// <summary>Константы мода — оболочка над ванильными Roles / Privilege.</summary>
public static class PermissionConstants
{
    public const string ChannelName = "SwixyPermissionManager";
    public const string OpenGuiHotkeyCode = "swixypermissionmanageropen";
    public const string ChatCommand = "perms";

    /// <summary>Роли, которые нельзя удалить (системные).</summary>
    public static readonly string[] ProtectedRoleCodes =
    [
        "admin",
        "suvisitor",
        "crvisitor",
        "limitedsuplayer",
        "suplayer",
        "sumod",
        "crmod",
    ];
}
