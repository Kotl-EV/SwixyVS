using System.Text.Json;
using System.Text.RegularExpressions;

const string questsPath = @"E:\рабочие файлы\GitHub\SwixyVS\SwixyQuestBook\Data\quests.json";
const string assetsPath = @"E:\Vintagestory\assets";

string[] scanRoots =
[
    Path.Combine(assetsPath, "survival", "itemtypes"),
    Path.Combine(assetsPath, "survival", "blocktypes"),
    Path.Combine(assetsPath, "survival", "recipes"),
    Path.Combine(assetsPath, "survival", "config"),
    Path.Combine(assetsPath, "game", "config", "remaps.json"),
];

var quests = JsonDocument.Parse(File.ReadAllText(questsPath));
var usages = new List<Usage>();

foreach (var category in quests.RootElement.GetProperty("categories").EnumerateArray())
{
    string categoryTitle = category.TryGetProperty("headerTitle", out var header)
        ? header.GetString() ?? ""
        : category.GetProperty("title").GetString() ?? "";

    foreach (var node in category.GetProperty("nodes").EnumerateArray())
    {
        int nodeId = node.GetProperty("id").GetInt32();
        string description = node.TryGetProperty("description", out var desc)
            ? desc.GetString() ?? ""
            : "";

        foreach (string field in new[] { "requiredItems", "rewardItems" })
        {
            if (!node.TryGetProperty(field, out var items))
                continue;

            foreach (var item in items.EnumerateArray())
            {
                string code = item.GetProperty("collectibleCode").GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(code))
                    usages.Add(new Usage(categoryTitle, nodeId, field, code, description));
            }
        }
    }
}

var uniqueCodes = usages.Select(u => u.Code).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
var cache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
var invalid = new Dictionary<string, List<Usage>>(StringComparer.OrdinalIgnoreCase);

foreach (string code in uniqueCodes)
{
    bool exists = CodeExists(code, scanRoots, cache);
    if (!exists)
        invalid[code] = usages.Where(u => string.Equals(u.Code, code, StringComparison.OrdinalIgnoreCase)).ToList();
}

Console.WriteLine($"Checked {usages.Count} references, {uniqueCodes.Count} unique codes");
Console.WriteLine($"Invalid unique codes: {invalid.Count}");
Console.WriteLine();

foreach (var pair in invalid.OrderBy(p => p.Key))
{
    Console.WriteLine(pair.Key);
    foreach (var usage in pair.Value.Take(4))
        Console.WriteLine($"  node {usage.NodeId} | {usage.Field} | {usage.Category} | {usage.Description}");
    if (pair.Value.Count > 4)
        Console.WriteLine($"  ... +{pair.Value.Count - 4} more");
    Console.WriteLine();
}

static bool CodeExists(string fullCode, string[] scanRoots, Dictionary<string, bool> cache)
{
    if (cache.TryGetValue(fullCode, out bool cached))
        return cached;

    string path = fullCode.Contains(':') ? fullCode[(fullCode.IndexOf(':') + 1)..] : fullCode;
    bool exists;

    if (path.Contains('*'))
    {
        string prefix = path.TrimEnd('*').TrimEnd('-');
        if (prefix == "stone")
            prefix = "stone-";
        exists = ScanAssets(prefix, scanRoots, partial: true);
    }
    else
    {
        exists = ScanAssets(path, scanRoots, partial: false)
            || ScanRemapTarget(path, scanRoots);
    }

    cache[fullCode] = exists;
    return exists;
}

static bool ScanRemapTarget(string path, string[] scanRoots)
{
    string remapsFile = Path.Combine(scanRoots[^1]);
    if (!File.Exists(remapsFile))
        return false;

    string text = File.ReadAllText(remapsFile);
    foreach (Match match in Regex.Matches(text, $@"remapq\s+{Regex.Escape(path)}\s+(\S+)"))
    {
        string target = match.Groups[1].Value;
        if (target.Contains(':'))
            target = target[(target.IndexOf(':') + 1)..];

        if (ScanAssets(target, scanRoots, partial: false))
            return true;
    }

    return false;
}

static bool ScanAssets(string needle, string[] scanRoots, bool partial)
{
    string pattern = partial
        ? Regex.Escape(needle)
        : $@"(?<![A-Za-z0-9_\-]){Regex.Escape(needle)}(?![A-Za-z0-9_\-])";

    foreach (string root in scanRoots)
    {
        if (!File.Exists(root) && !Directory.Exists(root))
            continue;

        IEnumerable<string> files = File.Exists(root)
            ? new[] { root }
            : Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}lang{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || file.Contains($"{Path.DirectorySeparatorChar}worldgen{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Regex.IsMatch(File.ReadAllText(file), pattern))
                return true;
        }
    }

    return false;
}

sealed record Usage(string Category, int NodeId, string Field, string Code, string Description);