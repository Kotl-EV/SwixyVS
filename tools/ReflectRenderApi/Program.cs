using System.Reflection;

string gameDir = args.Length > 0 ? args[0] : @"E:\Vintagestory";
AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    string name = new AssemblyName(e.Name).Name + ".dll";
    foreach (string dir in new[] { gameDir, Path.Combine(gameDir, "Lib") })
    {
        string path = Path.Combine(dir, name);
        if (File.Exists(path))
            return Assembly.LoadFrom(path);
    }
    return null;
};

var apiAsm = Assembly.LoadFrom(Path.Combine(gameDir, "VintagestoryAPI.dll"));
var passiveType = apiAsm.GetType("Vintagestory.API.Client.GuiElementPassiveItemSlot")!;
var gridBaseType = apiAsm.GetType("Vintagestory.API.Client.GuiElementItemSlotGridBase")!;

Console.WriteLine("=== GuiElement fields ===");
var guiElement = apiAsm.GetType("Vintagestory.API.Client.GuiElement")!;
foreach (var field in guiElement.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
    if (field.Name.Contains("Bound", StringComparison.OrdinalIgnoreCase) || field.Name == "api" || field.Name == "capi")
        Console.WriteLine($"{field.FieldType.Name} {field.Name}");

foreach (var prop in guiElement.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
    if (prop.Name.Contains("Bound", StringComparison.OrdinalIgnoreCase))
        Console.WriteLine($"prop {prop.PropertyType.Name} {prop.Name}");

Console.WriteLine("=== PassiveItemSlot RenderInteractiveElements IL size ===");
var renderMethod = passiveType.GetMethod("RenderInteractiveElements", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
Console.WriteLine(renderMethod?.GetMethodBody()?.GetILAsByteArray()?.Length);

Console.WriteLine("=== GridBase private methods ===");
foreach (var method in gridBaseType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
{
    if (method.Name.Contains("icon", StringComparison.OrdinalIgnoreCase)
        || method.Name.Contains("stack", StringComparison.OrdinalIgnoreCase)
        || method.Name.Contains("slot", StringComparison.OrdinalIgnoreCase))
        Console.WriteLine($"{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
}