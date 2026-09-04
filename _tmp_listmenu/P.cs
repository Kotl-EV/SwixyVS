using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

class P {
  static void Main() {
    var asm = Assembly.LoadFrom(System.IO.Path.Combine(AppContext.BaseDirectory, "VintagestoryAPI.dll"));
    DumpIL(asm.GetType("Vintagestory.API.Client.GuiElementListMenu"), "Open");
    DumpIL(asm.GetType("Vintagestory.API.Client.GuiElementListMenu"), "OnMouseDown");
    DumpIL(asm.GetType("Vintagestory.API.Client.GuiElementDialogTitleBar"), "SetUpMovableState");
    DumpIL(asm.GetType("Vintagestory.API.Client.GuiElementDialogTitleBar"), "OnMouseDown");
  }
  static void DumpIL(Type t, string name) {
    Console.WriteLine("\n##### " + t.Name + "." + name);
    foreach (var m in t.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(x => x.Name == name)) {
      var body = m.GetMethodBody();
      if (body == null) { Console.WriteLine("no body"); continue; }
      var il = body.GetILAsByteArray();
      Console.WriteLine("len="+il.Length+" params="+m.GetParameters().Length);
      var module = t.Module;
      var ops = typeof(OpCodes).GetFields().Select(f => (OpCode)f.GetValue(null)).ToArray();
      int i=0;
      int nlines=0;
      while (i < il.Length && nlines < 120) {
        int start = i;
        byte b = il[i++];
        OpCode op;
        if (b == 0xFE) {
          byte b2 = il[i++];
          op = ops.FirstOrDefault(o => o.Size==2 && o.Value == (short)((0xFE<<8)|b2));
        } else {
          op = ops.FirstOrDefault(o => o.Size==1 && (byte)o.Value == b);
        }
        string extra = "";
        switch (op.OperandType) {
          case OperandType.InlineString:
            extra = " \"" + module.ResolveString(BitConverter.ToInt32(il, i)) + "\"";
            i += 4; break;
          case OperandType.InlineMethod:
          case OperandType.InlineField:
          case OperandType.InlineTok:
          case OperandType.InlineType:
            try {
              var mb = module.ResolveMethod(BitConverter.ToInt32(il, i));
              extra = " " + (mb.DeclaringType?.Name ?? "") + "." + mb.Name;
            } catch {
              try { extra = " field " + module.ResolveField(BitConverter.ToInt32(il, i)).Name; }
              catch {
                try { extra = " type " + module.ResolveType(BitConverter.ToInt32(il, i)).Name; } catch { extra = " tok"; }
              }
            }
            i += 4; break;
          case OperandType.ShortInlineBrTarget:
          case OperandType.ShortInlineI:
          case OperandType.ShortInlineVar:
            extra = " " + (sbyte)il[i]; i += 1; break;
          case OperandType.InlineI:
          case OperandType.InlineBrTarget:
            extra = " " + BitConverter.ToInt32(il, i); i += 4; break;
          case OperandType.InlineI8: i += 8; break;
          case OperandType.ShortInlineR: i += 4; break;
          case OperandType.InlineR: i += 8; break;
          case OperandType.InlineSwitch:
            int n = BitConverter.ToInt32(il, i); i += 4 + 4*n; extra=" switch"; break;
          default: break;
        }
        Console.WriteLine($"  IL_{start:X4}: {op.Name}{extra}");
        nlines++;
      }
    }
  }
}
