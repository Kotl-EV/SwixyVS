using System.Text.Json;
using System.Text.Json.Nodes;

static Dictionary<string, string> LoadLang(string path)
{
    string json = File.ReadAllText(path);
    return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
        ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

static JsonObject? Expand(string? key, Dictionary<string, string> en, Dictionary<string, string> ru)
{
    if (string.IsNullOrWhiteSpace(key) || !key.Contains('.') || key.Contains(' '))
        return null;

    var obj = new JsonObject();
    if (en.TryGetValue(key, out string? enVal) && !string.IsNullOrWhiteSpace(enVal))
        obj["en"] = enVal;
    if (ru.TryGetValue(key, out string? ruVal) && !string.IsNullOrWhiteSpace(ruVal))
        obj["ru"] = ruVal;
    return obj.Count > 0 ? obj : null;
}

string root = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SwixyQuestBook"));

string questsPath = Path.Combine(root, "Data", "quests.json");
string enPath = Path.Combine(root, "assets", "swixyquestbook", "lang", "en.json");
string ruPath = Path.Combine(root, "assets", "swixyquestbook", "lang", "ru.json");

var en = LoadLang(enPath);
var ru = LoadLang(ruPath);
var node = JsonNode.Parse(File.ReadAllText(questsPath))!.AsObject();
int changed = 0;

foreach (JsonNode? catNode in node["categories"]!.AsArray())
{
    var cat = catNode!.AsObject();
    if (cat["title"] is JsonValue tv && tv.TryGetValue<string>(out string? titleKey))
    {
        JsonObject? expanded = Expand(titleKey, en, ru);
        if (expanded != null)
        {
            cat["title"] = expanded;
            changed++;
        }
    }

    if (cat["header"] is null && cat["headerTitle"] is JsonValue hv && hv.TryGetValue<string>(out string? headerKey))
    {
        JsonObject? expanded = Expand(headerKey, en, ru);
        if (expanded != null)
        {
            cat["header"] = expanded;
            changed++;
        }
        else if (!string.IsNullOrWhiteSpace(headerKey))
        {
            cat["header"] = new JsonObject { ["en"] = headerKey, ["ru"] = headerKey };
            changed++;
        }
    }

    foreach (JsonNode? n in cat["nodes"]!.AsArray())
    {
        var nodeObj = n!.AsObject();
        if (nodeObj["description"] is JsonValue dv && dv.TryGetValue<string>(out string? descKey))
        {
            JsonObject? expanded = Expand(descKey, en, ru);
            if (expanded != null)
            {
                nodeObj["description"] = expanded;
                changed++;
            }
            else if (!string.IsNullOrWhiteSpace(descKey))
            {
                nodeObj["description"] = new JsonObject { ["en"] = descKey, ["ru"] = descKey };
                changed++;
            }
        }
    }
}

string version = node["version"]?.GetValue<string>() ?? "1.0";
if (!version.Contains("+i18n", StringComparison.Ordinal))
{
    node["version"] = version + "+i18n";
}

var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
File.WriteAllText(questsPath, node.ToJsonString(options) + Environment.NewLine);
Console.WriteLine($"Migrated fields: {changed}");
Console.WriteLine($"Wrote {questsPath}");
