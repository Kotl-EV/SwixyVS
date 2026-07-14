using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

var lib = Assembly.LoadFrom(@"E:\Vintagestory\VintagestoryLib.dll");
var reqType = lib.GetType("Vintagestory.Server.ChunkColumnGenerateRequest");
Console.WriteLine($"ChunkColumnGenerateRequest: {reqType?.FullName}");
if (reqType != null)
{
    foreach (var ctor in reqType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
    {
        Console.WriteLine($"  ctor({string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
    }
    foreach (var p in reqType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
    {
        Console.WriteLine($"  prop {p.Name}: {p.PropertyType.Name}");
    }
}

var genChunk = typeof(GenRockStrataNew).GetMethod("GenChunkColumn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
Console.WriteLine($"GenRockStrataNew.GenChunkColumn: {genChunk}");
if (genChunk != null)
{
    foreach (var p in genChunk.GetParameters())
    {
        Console.WriteLine($"  param {p.Name}: {p.ParameterType.Name}");
    }
}

var blockLayerChunk = typeof(GenBlockLayers).GetMethod("OnChunkColumnGeneration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
Console.WriteLine($"GenBlockLayers.OnChunkColumnGeneration: {blockLayerChunk}");
if (blockLayerChunk != null)
{
    foreach (var p in blockLayerChunk.GetParameters())
    {
        Console.WriteLine($"  param {p.Name}: {p.ParameterType.Name}");
    }
}

var standard = Enum.GetValues<EnumWorldGenPass>().Length;
Console.WriteLine($"\nEnumWorldGenPass count: {standard}");

// List mod types that register map region on standard - scan survival mod systems
var asm = typeof(GenMaps).Assembly;
foreach (var t in asm.GetTypes().Where(t => t.Namespace?.Contains("ServerMods") == true && !t.IsAbstract))
{
    var start = t.GetMethod("StartServerSide", BindingFlags.Public | BindingFlags.Instance);
    if (start == null) continue;
    var body = start.GetMethodBody()?.GetILAsByteArray();
    if (body == null) continue;
    var hasMapRegion = false;
    for (var i = 0; i < body.Length; i++)
    {
        if (body[i] is 0x6F or 0x28 && i + 4 < body.Length)
        {
            try
            {
                if (start.Module.ResolveMethod(BitConverter.ToInt32(body, i + 1)) is MethodInfo m
                    && m.Name == "MapRegionGeneration")
                    hasMapRegion = true;
            }
            catch { }
        }
    }
    if (hasMapRegion) Console.WriteLine(t.Name);
}