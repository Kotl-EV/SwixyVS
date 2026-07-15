using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// Expand every multi-lang map in quests.json to all Vintage Story language codes.
// - Keep existing en/ru (and any other already-authored texts)
// - uk/be fall back to ru when missing (closer for Cyrillic players)
// - all other missing codes fall back to en

string root = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SwixyQuestBook"));

string questsPath = Path.Combine(root, "Data", "quests.json");
string languagesPath = args.Length > 1
    ? args[1]
    : @"E:\Vintagestory\assets\game\lang\languages.json";

if (!File.Exists(questsPath))
{
    Console.Error.WriteLine("quests.json not found: " + questsPath);
    return 1;
}

string[] allLangs = LoadLanguageCodes(languagesPath);
Console.WriteLine($"Language codes: {allLangs.Length} ({string.Join(", ", allLangs)})");

var node = JsonNode.Parse(File.ReadAllText(questsPath))!.AsObject();
int mapsTouched = 0;
int keysAdded = 0;

Walk(node);

string version = node["version"]?.GetValue<string>() ?? "1.0";
if (!version.Contains("+alllangs", StringComparison.Ordinal))
{
    node["version"] = version.Replace("+i18n", "", StringComparison.Ordinal) + "+i18n+alllangs";
}

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};
File.WriteAllText(questsPath, node.ToJsonString(options) + Environment.NewLine);
Console.WriteLine($"Maps updated: {mapsTouched}, keys added: {keysAdded}");
Console.WriteLine($"Wrote {questsPath}");
return 0;

void Walk(JsonNode? n)
{
    if (n is JsonObject obj)
    {
        if (IsLangMap(obj))
        {
            ExpandMap(obj);
            return;
        }

        foreach (var prop in obj.ToList())
            Walk(prop.Value);
    }
    else if (n is JsonArray arr)
    {
        foreach (JsonNode? child in arr)
            Walk(child);
    }
}

bool IsLangMap(JsonObject obj)
{
    if (obj.Count == 0)
        return false;

    // Multi-lang maps only contain language-code keys and string values.
    foreach (var prop in obj)
    {
        if (prop.Value is not JsonValue jv || jv.GetValueKind() != JsonValueKind.String)
            return false;
        if (!LooksLikeLangCode(prop.Key))
            return false;
    }

    // Must look like i18n (has en or ru or short codes only), not a random object.
    return obj.ContainsKey("en") || obj.ContainsKey("ru") || obj.Count <= 8;
}

bool LooksLikeLangCode(string key)
{
    // en, ru, zh-cn, es-es, pt-br, ...
    return Regex.IsMatch(key, @"^[a-z]{2}(-[a-z0-9]{2,8})?$", RegexOptions.IgnoreCase);
}

void ExpandMap(JsonObject map)
{
    string? en = Get(map, "en");
    string? ru = Get(map, "ru");
    if (string.IsNullOrWhiteSpace(en) && string.IsNullOrWhiteSpace(ru))
        return;

    // Prefer existing non-empty en; else any value as base.
    en ??= FirstNonEmpty(map) ?? string.Empty;
    ru ??= en;

    bool changed = false;
    foreach (string lang in allLangs)
    {
        string code = lang.ToLowerInvariant();
        string? existing = Get(map, code);
        if (!string.IsNullOrWhiteSpace(existing))
            continue;

        string fill = code switch
        {
            "en" => en,
            "ru" => ru,
            "uk" or "be" => ru, // Cyrillic family: better temporary fill than English
            _ => en
        };

        if (string.IsNullOrWhiteSpace(fill))
            continue;

        map[code] = fill;
        keysAdded++;
        changed = true;
    }

    if (changed)
        mapsTouched++;
}

static string? Get(JsonObject map, string key)
{
    if (!map.TryGetPropertyValue(key, out JsonNode? n) || n is null)
        return null;
    return n.GetValue<string>();
}

static string? FirstNonEmpty(JsonObject map)
{
    foreach (var prop in map)
    {
        if (prop.Value is JsonValue jv && jv.GetValueKind() == JsonValueKind.String)
        {
            string? s = jv.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(s))
                return s;
        }
    }

    return null;
}

static string[] LoadLanguageCodes(string languagesPath)
{
    var codes = new List<string>();
    if (File.Exists(languagesPath))
    {
        // languages.json is non-strict JSON (unquoted keys). Extract code: "xx"
        string raw = File.ReadAllText(languagesPath);
        foreach (Match m in Regex.Matches(raw, @"code\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase))
        {
            string code = m.Groups[1].Value.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(code) && !codes.Contains(code, StringComparer.OrdinalIgnoreCase))
                codes.Add(code);
        }
    }

    // Always ensure core pair exists.
    if (!codes.Contains("en", StringComparer.OrdinalIgnoreCase)) codes.Insert(0, "en");
    if (!codes.Contains("ru", StringComparer.OrdinalIgnoreCase)) codes.Add("ru");

    // Also scan game lang folder if available.
    string? langDir = Path.GetDirectoryName(languagesPath);
    if (!string.IsNullOrWhiteSpace(langDir) && Directory.Exists(langDir))
    {
        foreach (string file in Directory.GetFiles(langDir, "*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (string.Equals(name, "languages", StringComparison.OrdinalIgnoreCase))
                continue;
            string code = name.ToLowerInvariant();
            if (!codes.Contains(code, StringComparer.OrdinalIgnoreCase))
                codes.Add(code);
        }
    }

    codes.Sort(StringComparer.OrdinalIgnoreCase);
    return codes.ToArray();
}
