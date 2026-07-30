using System.Buffers.Binary; using System.Text;
var path=@"E:\נאבמקטו פאיכû\GitHub\SwixyVS\SwixyClaimChunk\assets\swixyclaimchunk\fonts\minecraft.ttf";
var data=File.ReadAllBytes(path);
// parse glyph 216 contours
int n=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4,2));
int glyfOff=0,locaOff=0,headOff=0;
for(int i=0;i<n;i++){int o=12+i*16; var t=Encoding.ASCII.GetString(data,o,4); int off=(int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(o+8,4));
 if(t=="glyf")glyfOff=off; if(t=="loca")locaOff=off; if(t=="head")headOff=off;}
short idx=(short)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(headOff+50,2));
int g=216; int start,end;
if(idx==0){start=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(locaOff+g*2,2))*2; end=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(locaOff+(g+1)*2,2))*2;}
else{start=(int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(locaOff+g*4,4)); end=(int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(locaOff+(g+1)*4,4));}
var glyf=new byte[end-start]; Array.Copy(data,glyfOff+start,glyf,0,glyf.Length);
short nCont=BinaryPrimitives.ReadInt16BigEndian(glyf.AsSpan(0,2));
int o2=10; var endPts=new int[nCont]; for(int i=0;i<nCont;i++){endPts[i]=BinaryPrimitives.ReadUInt16BigEndian(glyf.AsSpan(o2,2)); o2+=2;}
int nPts=endPts[^1]+1; ushort instr=BinaryPrimitives.ReadUInt16BigEndian(glyf.AsSpan(o2,2)); o2+=2+instr;
var flags=new byte[nPts]; for(int i=0;i<nPts;){byte f=glyf[o2++]; flags[i++]=f; if((f&8)!=0){int r=glyf[o2++]; for(int k=0;k<r;k++)flags[i++]=f;}}
var xs=new int[nPts]; var ys=new int[nPts]; int x=0,y=0;
for(int i=0;i<nPts;i++){byte f=flags[i]; if((f&2)!=0){int dx=glyf[o2++]; x+=((f&16)!=0)?dx:-dx;} else if((f&16)==0){x+=(short)BinaryPrimitives.ReadUInt16BigEndian(glyf.AsSpan(o2,2)); o2+=2;} xs[i]=x;}
for(int i=0;i<nPts;i++){byte f=flags[i]; if((f&4)!=0){int dy=glyf[o2++]; y+=((f&32)!=0)?dy:-dy;} else if((f&32)==0){y+=(short)BinaryPrimitives.ReadUInt16BigEndian(glyf.AsSpan(o2,2)); o2+=2;} ys[i]=y;}
int st=0; for(int c=0;c<nCont;c++){int e=endPts[c]; int minx=xs.Skip(st).Take(e-st+1).Min(); int maxx=xs.Skip(st).Take(e-st+1).Max(); int miny=ys.Skip(st).Take(e-st+1).Min(); int maxy=ys.Skip(st).Take(e-st+1).Max();
 Console.WriteLine($"contour {c} pts {st}-{e} box=({minx},{miny})-({maxx},{maxy})"); st=e+1;}
