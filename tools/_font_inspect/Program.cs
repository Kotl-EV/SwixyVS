using System.Buffers.Binary;
using System.Text;
foreach (var path in args) {
  if (!File.Exists(path)) { Console.WriteLine("MISSING "+path); continue; }
  var data = File.ReadAllBytes(path);
  int n = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4,2));
  int nameOff=0,nameLen=0,glyfOff=0,locaOff=0,headOff=0,cmapOff=0;
  for (int i=0;i<n;i++) {
    int o=12+i*16; var t=Encoding.ASCII.GetString(data,o,4);
    int off=(int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(o+8,4));
    int len=(int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(o+12,4));
    if (t=="name"){nameOff=off;nameLen=len;}
    if (t=="glyf")glyfOff=off; if (t=="loca")locaOff=off; if (t=="head")headOff=off; if (t=="cmap")cmapOff=off;
  }
  Console.WriteLine("=== "+Path.GetFileName(path)+" size="+data.Length+" ===");
  ushort count=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(nameOff+2,2));
  ushort so=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(nameOff+4,2));
  for (int i=0;i<count;i++) {
    int o=nameOff+6+i*12;
    ushort p=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(o,2));
    ushort e=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(o+2,2));
    ushort l=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(o+4,2));
    ushort id=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(o+6,2));
    ushort len=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(o+8,2));
    ushort off=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(o+10,2));
    if (id!=1 && id!=4 && id!=6) continue;
    var bytes=data.AsSpan(nameOff+so+off,len);
    string text = (p==3||p==0)? Encoding.BigEndianUnicode.GetString(bytes): Encoding.ASCII.GetString(bytes);
    Console.WriteLine($"  name id={id} p={p} e={e} l={l}: {text}");
  }
  // cmap find 0x0419
  // simplified: use loca index for glyph 216 as before if present
  short idxFmt=(short)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(headOff+50,2));
  // try parse cmap format 4 for 0x0418 and 0x0419
  int numTables=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(cmapOff+2,2));
  int gI=-1,gY=-1;
  for (int ti=0;ti<numTables;ti++) {
    int to=cmapOff+4+ti*8;
    ushort plat=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(to,2));
    ushort enc=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(to+2,2));
    int sub=(int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(to+4,4))+cmapOff;
    ushort fmt=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(sub,2));
    if (fmt!=4) continue;
    ushort segCountX2=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(sub+6,2));
    int segCount=segCountX2/2;
    int endOff=sub+14;
    int startOff=endOff+segCountX2+2;
    int idDeltaOff=startOff+segCountX2;
    int idRangeOff=idDeltaOff+segCountX2;
    int GlyphFor(int cp) {
      for (int s=0;s<segCount;s++) {
        int end=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(endOff+s*2,2));
        int start=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(startOff+s*2,2));
        if (cp<start||cp>end) continue;
        short idDelta=(short)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(idDeltaOff+s*2,2));
        ushort idRange=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(idRangeOff+s*2,2));
        if (idRange==0) return (cp+idDelta)&0xFFFF;
        int glyphIndexAddress=idRangeOff+s*2+idRange+(cp-start)*2;
        int gi=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(glyphIndexAddress,2));
        if (gi==0) return 0;
        return (gi+idDelta)&0xFFFF;
      }
      return 0;
    }
    gI=GlyphFor(0x0418); gY=GlyphFor(0x0419);
    if (gI>0||gY>0) { Console.WriteLine($"  cmap plat={plat} enc={enc} И={gI} Й={gY}"); break; }
  }
  if (gY>0) {
    int start,end;
    if (idxFmt==0){start=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(locaOff+gY*2,2))*2; end=BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(locaOff+(gY+1)*2,2))*2;}
    else{start=(int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(locaOff+gY*4,4)); end=(int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(locaOff+(gY+1)*4,4));}
    var glyf=data.AsSpan(glyfOff+start,end-start);
    short nCont=BinaryPrimitives.ReadInt16BigEndian(glyf.Slice(0,2));
    short xMin=BinaryPrimitives.ReadInt16BigEndian(glyf.Slice(2,2));
    short yMin=BinaryPrimitives.ReadInt16BigEndian(glyf.Slice(4,2));
    short xMax=BinaryPrimitives.ReadInt16BigEndian(glyf.Slice(6,2));
    short yMax=BinaryPrimitives.ReadInt16BigEndian(glyf.Slice(8,2));
    Console.WriteLine($"  glyph Й contours={nCont} box=({xMin},{yMin})-({xMax},{yMax}) bytes={end-start}");
  }
}
