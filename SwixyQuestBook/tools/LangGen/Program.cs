using System.Text;
using System.Text.Json;

/// <summary>
/// Builds swixyquestbook lang/*.json for every Vintage Story language.
/// Base content (quest.*) from en.json (ru.json for Russian).
/// UI + category keys overlaid with translations.
/// </summary>
static class Program
{
    static readonly string[] VsLanguages =
    [
        "ar", "be", "cs", "da", "de", "en", "eo", "es-419", "es-es", "fi", "fr",
        "hu", "is", "it", "ja", "ko", "lt", "nl", "no", "pl", "pt-br", "pt-pt",
        "ro", "ru", "sk", "sr", "sv-se", "th", "tr", "uk", "vi", "zh-cn", "zh-tw"
    ];

    static int Main(string[] args)
    {
        string root = args.Length > 0
            ? args[0]
            : FindLangDir();

        if (root == null || !Directory.Exists(root))
        {
            Console.Error.WriteLine("Lang directory not found. Pass path to assets/swixyquestbook/lang");
            return 1;
        }

        string enPath = Path.Combine(root, "en.json");
        string ruPath = Path.Combine(root, "ru.json");
        if (!File.Exists(enPath) || !File.Exists(ruPath))
        {
            Console.Error.WriteLine("en.json / ru.json required in " + root);
            return 1;
        }

        var en = LoadJson(enPath);
        var ru = LoadJson(ruPath);
        var allOverlays = UiTranslations.All; // lang -> (key -> value)

        int written = 0;
        foreach (string lang in VsLanguages)
        {
            Dictionary<string, string> dest;
            if (lang == "en")
            {
                dest = new Dictionary<string, string>(en, StringComparer.Ordinal);
            }
            else if (lang == "ru")
            {
                dest = new Dictionary<string, string>(ru, StringComparer.Ordinal);
            }
            else
            {
                // Full key set from English (quest lore stays EN until translated).
                dest = new Dictionary<string, string>(en, StringComparer.Ordinal);
                if (allOverlays.TryGetValue(lang, out var overlay))
                {
                    foreach (var kv in overlay)
                        dest[kv.Key] = kv.Value;
                }
            }

            // Always re-apply overlay for en if present (noop), and ensure ru keeps overlay extras.
            if (lang != "en" && lang != "ru" && allOverlays.TryGetValue(lang, out var o2))
            {
                foreach (var kv in o2)
                    dest[kv.Key] = kv.Value;
            }

            string outPath = Path.Combine(root, lang + ".json");
            WriteJson(outPath, dest, en.Keys.ToList());
            written++;
            Console.WriteLine("Wrote " + lang + ".json (" + dest.Count + " keys)");
        }

        Console.WriteLine("Done: " + written + " language files in " + root);
        return 0;
    }

    static string? FindLangDir()
    {
        string? dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 8 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "assets", "swixyquestbook", "lang");
            if (Directory.Exists(candidate))
                return candidate;
            candidate = Path.Combine(dir, "SwixyQuestBook", "assets", "swixyquestbook", "lang");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    static Dictionary<string, string> LoadJson(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
            map[prop.Name] = prop.Value.GetString() ?? "";
        return map;
    }

    static void WriteJson(string path, Dictionary<string, string> map, List<string> keyOrder)
    {
        // Preserve en key order, then any extras.
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string k in keyOrder)
        {
            if (map.ContainsKey(k) && seen.Add(k))
                ordered.Add(k);
        }
        foreach (string k in map.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (seen.Add(k))
                ordered.Add(k);
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");
        for (int i = 0; i < ordered.Count; i++)
        {
            string k = ordered[i];
            string v = map[k];
            sb.Append("  ");
            sb.Append(JsonSerializer.Serialize(k));
            sb.Append(": ");
            sb.Append(JsonSerializer.Serialize(v));
            if (i < ordered.Count - 1)
                sb.Append(',');
            sb.AppendLine();
        }
        sb.AppendLine("}");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
