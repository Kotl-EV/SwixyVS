using System;
using System.Reflection;
using System.Runtime.Loader;
using System.IO;
var path = @"E:\Vintagestory\VintagestoryAPI.dll";
var alc = new AssemblyLoadContext("probe7", true);
alc.Resolving += (c, n) => {
  foreach (var dir in new[]{@"E:\Vintagestory", @"E:\Vintagestory\Lib", @"E:\Vintagestory\Mods"}) {
    var p = Path.Combine(dir, n.Name + ".dll");
    if (File.Exists(p)) return c.LoadFromAssemblyPath(p);
  }
  return null;
};
var asm = alc.LoadFromAssemblyPath(path);
var eb = asm.GetType("Vintagestory.API.Client.ElementBounds")!;
foreach(var f in eb.GetFields(BindingFlags.Public|BindingFlags.Instance))
  Console.WriteLine("F "+f.Name+" "+f.FieldType.Name+" public");
foreach(var p in eb.GetProperties(BindingFlags.Public|BindingFlags.Instance))
  if(p.Name.Contains("Child")||p.Name.Contains("Parent"))
    Console.WriteLine("P "+p.Name);
