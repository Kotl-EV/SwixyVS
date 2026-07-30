using System;
using System.Reflection;
using Vintagestory.API.Client;

class Probe {
  static void Main() {
    var t = typeof(GuiElement);
    Console.WriteLine("GuiElement: " + t.FullName);
    foreach (var p in t.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)) {
      if (p.Name.IndexOf("Cursor", StringComparison.OrdinalIgnoreCase) >= 0 || p.Name.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0) {
        var g = p.GetGetMethod(true); var s = p.GetSetMethod(true);
        Console.WriteLine($"PROP {p.Name} type={p.PropertyType.Name} get={(g==null?"-":$"{g.IsPublic}/{g.IsFamily}")} set={(s==null?"-":$"{s.IsPublic}/{s.IsFamily}")}");
      }
    }
    foreach (var m in t.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)) {
      if (m.Name.IndexOf("Cursor", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0) {
        Console.WriteLine($"METH {m.Name} pub={m.IsPublic} fam={m.IsFamily} virt={m.IsVirtual} params={m.GetParameters().Length}");
      }
    }
    var gd = typeof(GuiDialog);
    Console.WriteLine("GuiDialog: " + gd.FullName + " base=" + gd.BaseType.Name);
    foreach (var m in gd.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)) {
      if (m.Name.IndexOf("Cursor", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name == "OnRenderGUI" || m.Name == "OnFinalizeFrame") {
        Console.WriteLine($"GuiDialog.{m.Name} pub={m.IsPublic} virt={m.IsVirtual}");
      }
    }
    // ICoreClientAPI cursor methods
    foreach (var it in typeof(ICoreClientAPI).Assembly.GetTypes()) {
      if (!it.IsInterface && !it.IsClass) continue;
      if (it.Name.IndexOf("Input", StringComparison.OrdinalIgnoreCase) < 0 && it.Name.IndexOf("Gui", StringComparison.OrdinalIgnoreCase) < 0 && it.Name.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) < 0 && it.Name.IndexOf("Platform", StringComparison.OrdinalIgnoreCase) < 0 && it.Name != "ICoreClientAPI" && it.Name != "IClientWorldAccessor") continue;
      MethodInfo[] ms;
      try { ms = it.GetMethods(); } catch { continue; }
      foreach (var m in ms) {
        if (m.Name.IndexOf("Cursor", StringComparison.OrdinalIgnoreCase) >= 0) {
          Console.WriteLine($"{it.Name}.{m.Name}({string.Join(",", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name + " " + p.Name))})");
        }
      }
    }
  }
}
