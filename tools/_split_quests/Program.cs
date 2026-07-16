using System.Text.Json;
using System.Text.Json.Nodes;

var src = Path.GetFullPath(args[0]);
var outDir = Path.GetFullPath(args[1]);
var branchesDir = Path.Combine(outDir, "branches");
Directory.CreateDirectory(branchesDir);

using var doc = JsonDocument.Parse(File.ReadAllText(src));
var root = doc.RootElement;
string version = root.TryGetProperty("version", out var v) ? (v.GetString() ?? "1.0") : "1.0";

var manifestCats = new JsonArray();
foreach (var cat in root.GetProperty("categories").EnumerateArray())
{
    string headerTitle = cat.GetProperty("headerTitle").GetString() ?? "unknown";
    string safe = string.Concat(headerTitle.Select(c =>
        char.IsLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_'));
    string fileName = safe + ".json";
    string path = Path.Combine(branchesDir, fileName);
    string catJson = JsonSerializer.Serialize(cat, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(path, catJson + "\n");
    manifestCats.Add(new JsonObject
    {
        ["headerTitle"] = headerTitle,
        ["file"] = fileName
    });
    Console.WriteLine($"wrote {fileName} ({new FileInfo(path).Length} bytes)");
}

var manifest = new JsonObject
{
    ["version"] = version,
    ["categories"] = manifestCats
};
File.WriteAllText(Path.Combine(outDir, "manifest.json"),
    manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine("manifest written");
