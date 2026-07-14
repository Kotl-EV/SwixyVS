using System;
using System.Linq;
using System.Reflection;
var asm = Assembly.LoadFrom("VSSurvivalMod.dll");
var t = asm.GetType("Vintagestory.GameContent.GenStoryStructures");
if (t == null) { foreach (var et in asm.GetExportedTypes().Where(x => x.Name.Contains("Story"))) Console.WriteLine(et.FullName); }
else {
  foreach (var m in t.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)) {
    if (m.Name.Contains("Place") || m.Name.Contains("Generate") || m.Name.Contains("Story"))
      Console.WriteLine(m.Name + "(" + string.Join(", ", m.GetParameters().Select(p=>p.ParameterType.Name+" "+p.Name))+")");
  }
}
