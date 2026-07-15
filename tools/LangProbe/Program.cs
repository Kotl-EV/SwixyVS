using System.Reflection;
using Vintagestory.API.Config;
var t = typeof(Lang);
foreach (var m in t.GetMembers(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance))
  if (m.Name.Contains("Lang", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Avail", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Current", StringComparison.OrdinalIgnoreCase))
    Console.WriteLine(m.MemberType + " " + m);
