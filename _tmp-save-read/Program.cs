using System;
using System.Linq;
using System.Reflection;

class Program {
  static void Main() {
    var api = Assembly.LoadFrom(@"E:\Vintagestory\VintagestoryAPI.dll");
    var land = api.GetType("Vintagestory.API.Common.LandClaim") ?? api.GetTypes().FirstOrDefault(t => t.Name == "LandClaim");
    Console.WriteLine("LandClaim: " + (land?.FullName ?? "null"));
    if (land != null) {
      foreach (var f in land.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(x=>x.Name))
        Console.WriteLine($"  F {f.FieldType.Name} {f.Name}");
      foreach (var p in land.GetProperties(BindingFlags.Public|BindingFlags.Instance).OrderBy(x=>x.Name))
        Console.WriteLine($"  P {p.PropertyType.Name} {p.Name}");
      foreach (var mi in land.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(m=>!m.IsSpecialName).OrderBy(x=>x.Name))
        Console.WriteLine($"  M {mi.ReturnType.Name} {mi.Name}({string.Join(", ", mi.GetParameters().Select(x=>x.ParameterType.Name+" "+x.Name))})");
    }
    var flags = api.GetTypes().FirstOrDefault(t => t.Name == "EnumBlockAccessFlags");
    if (flags != null) {
      Console.WriteLine("EnumBlockAccessFlags:");
      foreach (var n in Enum.GetNames(flags)) Console.WriteLine("  " + n + " = " + Convert.ToInt64(Enum.Parse(flags, n)));
    }
    Console.WriteLine("--- methods with Access/Claim ---");
    foreach (var t in api.GetExportedTypes()) {
      foreach (var mi in t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).Where(m => {
        var n=m.Name; return n.Contains("BlockAccess") || n.Contains("TestAccess") || n.Contains("GetClaiming") || n.Contains("CanPlayer") || n=="TryAccess" || n.Contains("HasAccess");
      })) {
        Console.WriteLine($"{t.Name}.{mi.Name}({string.Join(", ", mi.GetParameters().Select(p=>p.ParameterType.Name+" "+p.Name))})");
      }
    }
  }
}
