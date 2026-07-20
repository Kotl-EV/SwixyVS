using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SwixyPermissionManager.Core;

/// <summary>
/// Каталог ванильных privilege codes + локализованные описания.
/// Источник кодов: <see cref="Privilege.AllCodes()"/> (+ worldedit, часто в admin).
/// </summary>
public static class PrivilegeCatalog
{
    /// <summary>Доп. коды, встречающиеся в serverconfig, но не в AllCodes().</summary>
    private static readonly string[] ExtraCodes =
    [
        "worldedit",
        "areamodify", // alias field claimland = areamodify already in AllCodes as areamodify via claimland field
    ];

    public static IReadOnlyList<string> AllCodes()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in Privilege.AllCodes())
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                set.Add(code);
            }
        }

        // Field name "buildblocks" stores code "build"; keep both paths covered.
        set.Add(Privilege.buildblocks);
        set.Add(Privilege.useblock);
        set.Add(Privilege.claimland);

        foreach (var extra in ExtraCodes)
        {
            set.Add(extra);
        }

        return set.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Короткое название (локализация или сам code).</summary>
    public static string GetTitle(string code, string? langCode = null)
    {
        var key = LangKey(code, "title");
        var text = langCode == null ? Lang.Get(key) : Lang.GetL(langCode, key);
        return string.IsNullOrWhiteSpace(text) || text == key ? code : text;
    }

    /// <summary>Пояснение, что даёт право.</summary>
    public static string GetDescription(string code, string? langCode = null)
    {
        var key = LangKey(code, "desc");
        var text = langCode == null ? Lang.Get(key) : Lang.GetL(langCode, key);
        if (string.IsNullOrWhiteSpace(text) || text == key)
        {
            // Fallback English descriptions baked in for unknown locales.
            return FallbackDescription(code);
        }

        return text;
    }

    public static string LangKey(string code, string part) =>
        $"swixypermissionmanager:priv-{Sanitize(code)}-{part}";

    private static string Sanitize(string code) =>
        (code ?? "").ToLowerInvariant().Replace('.', '-');

    private static string FallbackDescription(string code) => code switch
    {
        "build" or "buildblocks" => "Place or break blocks.",
        "useblock" => "Interact with blocks (doors, chests, etc.).",
        "buildblockseverywhere" => "Build anywhere ignoring claims (creative still required).",
        "useblockseverywhere" => "Use blocks anywhere ignoring claims (creative still required).",
        "attackplayers" => "Damage other players (PvP).",
        "attackcreatures" => "Damage non-player creatures.",
        "freemove" => "Fly / change movement speed.",
        "gamemode" => "Change own game mode.",
        "pickingrange" => "Change block reach distance.",
        "chat" => "Use chat.",
        "selfkill" => "Use /kill on yourself.",
        "kick" => "Kick players from the server.",
        "ban" => "Ban / unban players.",
        "whitelist" => "Add / remove players from whitelist.",
        "setwelcome" => "Set the server welcome message.",
        "announce" => "Make server-wide announcements.",
        "readlists" => "View client, group, ban and area lists.",
        "give" => "Give items/blocks via commands.",
        "areamodify" or "claimland" => "Claim and modify land areas.",
        "setspawn" => "Set the default world spawn.",
        "controlserver" => "Restart/shutdown server, reload mods, server config.",
        "tp" => "Teleport (self and related commands).",
        "time" => "Read and change world time.",
        "grantrevoke" => "Grant/revoke roles and privileges to others (same or lower level).",
        "root" => "Full access — everything is allowed.",
        "commandplayer" => "Run commands targeting other players.",
        "controlplayergroups" => "Join/leave/invite/op own player groups and group chat.",
        "manageplayergroups" => "Create and disband own player groups.",
        "manageotherplayergroups" => "Modify other players' groups.",
        "worldedit" => "Use WorldEdit tools and /we commands.",
        _ => $"Privilege code: {code}",
    };
}
