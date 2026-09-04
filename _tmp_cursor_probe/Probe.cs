using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;

class Probe {
  static void Main() {
    var t = typeof(GuiElementListMenu);
    Console.WriteLine("GuiElementListMenu " + t.FullName);
    foreach (var m in t.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)) {
      if (m.Name.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase)>=0
          || m.Name.IndexOf("Open", StringComparison.OrdinalIgnoreCase)>=0
          || m.Name.IndexOf("Close", StringComparison.OrdinalIgnoreCase)>=0
          || m.Name.IndexOf("Select", StringComparison.OrdinalIgnoreCase)>=0
          || m.Name.IndexOf("Inside", StringComparison.OrdinalIgnoreCase)>=0
          || m.Name.IndexOf("Click", StringComparison.OrdinalIgnoreCase)>=0) {
        Console.WriteLine($"  {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name+" "+p.Name))}) pub={m.IsPublic}");
      }
    }
    var tb = t.Assembly.GetTypes().FirstOrDefault(x => x.Name == "GuiElementDialogTitleBar");
    if (tb != null) {
      Console.WriteLine("TitleBar found");
      foreach (var m in tb.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)) {
        if (m.Name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase)>=0
            || m.Name.IndexOf("Select", StringComparison.OrdinalIgnoreCase)>=0
            || m.Name.IndexOf("Auto", StringComparison.OrdinalIgnoreCase)>=0
            || m.Name.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase)>=0) {
          Console.WriteLine($"  TB.{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name+" "+p.Name))})");
        }
      }
      foreach (var f in tb.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)) {
        Console.WriteLine($"  F {f.FieldType.Name} {f.Name}");
      }
    }
  }
}
