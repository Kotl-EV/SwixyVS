using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
var alc=new AssemblyLoadContext("w2",true);
alc.Resolving+=(c,n)=>{foreach(var d in new[]{@"E:\Vintagestory",@"E:\Vintagestory\Lib"}){var p=Path.Combine(d,n.Name+".dll");if(File.Exists(p))return c.LoadFromAssemblyPath(p);}return null;};
var api=alc.LoadFromAssemblyPath(@"E:\Vintagestory\VintagestoryAPI.dll");
var t=api.GetTypes().First(x=>x.Name.StartsWith("GuiElementCellList"));
var m=t.GetMethod("OnMouseUpOnElement", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
Console.WriteLine(m);
var il=m!.GetMethodBody()!.GetILAsByteArray()!;
var module=m.Module;
int i=0;
while(i<il.Length){
  var op=il[i];
  if(op==0x28||op==0x6F){
    int token=BitConverter.ToInt32(il,i+1);
    try{ var mem=module.ResolveMethod(token); Console.WriteLine($"{i:X3} call {mem}");}catch{}
    i+=5;continue;
  }
  if(op==0x7B||op==0x6F){i+=5;continue;}
  if(op==0x28){i+=5;continue;}
  // ldfld get_Button?
  if(op==0x6F){i+=5;continue;}
  if(op==0x7B){
    int token=BitConverter.ToInt32(il,i+1);
    try{var f=module.ResolveField(token);Console.WriteLine($"{i:X3} ldfld {f}");}catch{
      try{var mem=module.ResolveMember(token);Console.WriteLine($"{i:X3} member {mem}");}catch{}
    }
    i+=5;continue;
  }
  if(op==0x6A||op==0x02||op==0x03||op==0x04||op==0x05||op==0x06||op==0x07||op==0x08||op==0x0A||op==0x2A||op==0x16||op==0x17||op==0x18){i++;continue;}
  // callvirt get_Button
  if(op==0x6F){
    int token=BitConverter.ToInt32(il,i+1);
    try{Console.WriteLine($"{i:X3} callvirt {module.ResolveMethod(token)}");}catch{}
    i+=5;continue;
  }
  i++;
}
// simpler: print all method resolves
i=0;
while(i<il.Length-4){
  if(il[i]==0x28||il[i]==0x6F||il[i]==0x73){
    int token=BitConverter.ToInt32(il,i+1);
    try{Console.WriteLine($"{i:X3}: {module.ResolveMethod(token)}");}catch{}
  }
  if(il[i]==0x7B){
    int token=BitConverter.ToInt32(il,i+1);
    try{Console.WriteLine($"{i:X3}: field {module.ResolveField(token)}");}catch{}
  }
  i++;
}
Console.WriteLine("IL len "+il.Length);
