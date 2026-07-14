using System;
using System.Linq;
using System.Reflection;

var asm = Assembly.LoadFrom(@"E:\Vintagestory\Mods\VSSurvivalMod.dll");
var t = asm.GetType("Vintagestory.ServerMods.GenRockStrataNew")
    ?? asm.GetType("GenRockStrataNew");
if (t == null)
{
    foreach (var type in asm.GetTypes().Where(x => x.Name.Contains("RockStrata")))
    {
        Console.WriteLine(type.FullName);
    }
    return;
}
Console.WriteLine($"Type: {t.FullName}");
foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
{
    if (m.Name.Contains("Map", StringComparison.OrdinalIgnoreCase)
        || m.Name.Contains("Chunk", StringComparison.OrdinalIgnoreCase)
        || m.Name.Contains("init", StringComparison.OrdinalIgnoreCase)
        || m.Name.Contains("Rock", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine(m.Name);
    }
}

var storyType = asm.GetType("Vintagestory.GameContent.WorldGenStoryStructuresConfig");
if (storyType != null)
{
    Console.WriteLine($"\n{storyType.FullName} methods:");
    foreach (var m in storyType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
    {
        if (m.Name.Contains("Init", StringComparison.OrdinalIgnoreCase)
            || m.Name.Contains("Remap", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(m.Name);
        }
    }
}