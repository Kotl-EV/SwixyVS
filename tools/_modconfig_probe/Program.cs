using System.Reflection;
AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
  var name = new AssemblyName(e.Name).Name + ".dll";
  foreach (var dir in new[]{@"E:\Vintagestory", @"E:\Vintagestory\Lib"}) {
    var p = Path.Combine(dir, name);
    if (File.Exists(p)) return Assembly.LoadFrom(p);
  }
  return null;
};
var api = Assembly.LoadFrom(@"E:\Vintagestory\VintagestoryAPI.dll");
var t = api.GetType("Vintagestory.API.Datastructures.TreeAttribute")!;
foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.Instance).Where(m => m.Name.StartsWith("Get")))
  if (m.Name is "GetItemstack" or "GetLong" or "GetInt" or "GetString" or "GetTreeAttribute")
    Console.WriteLine(m);
