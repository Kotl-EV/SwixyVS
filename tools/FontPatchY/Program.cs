using System.Buffers.Binary;
using System.Text;

// Restore working fonts: keep original name tables (Minecraft Rus / Minecraft Five),
// only replace capital Й glyph with full И + 2 vertical pixels (with gap).
internal static class Program
{
    static int Main()
    {
        // ClaimChunk + QuestBook share the same Minecraft faces; patch both packs.
        string[][] targets =
        [
            [
                @"E:\рабочие файлы\GitHub\SwixyVS\SwixyClaimChunk\assets\swixyclaimchunk\fonts\minecraft.ttf",
                @"E:\рабочие файлы\GitHub\SwixyVS\SwixyClaimChunk\assets\swixyclaimchunk\fonts\minecraft.ttf.bak"
            ],
            [
                @"E:\рабочие файлы\GitHub\SwixyVS\SwixyClaimChunk\assets\swixyclaimchunk\fonts\MinecraftTitle.ttf",
                @"E:\рабочие файлы\GitHub\SwixyVS\SwixyClaimChunk\assets\swixyclaimchunk\fonts\MinecraftTitle.ttf.bak"
            ],
            [
                @"E:\рабочие файлы\GitHub\SwixyVS\SwixyQuestBook\assets\swixyquestbook\fonts\minecraft.ttf",
                @"E:\рабочие файлы\GitHub\SwixyVS\SwixyQuestBook\assets\swixyquestbook\fonts\minecraft.ttf.bak"
            ],
            [
                @"E:\рабочие файлы\GitHub\SwixyVS\SwixyQuestBook\assets\swixyquestbook\fonts\MinecraftTitle.ttf",
                @"E:\рабочие файлы\GitHub\SwixyVS\SwixyQuestBook\assets\swixyquestbook\fonts\MinecraftTitle.ttf.bak"
            ],
        ];

        foreach (var pair in targets)
        {
            var dest = pair[0];
            var bak = pair[1];
            if (!File.Exists(bak))
            {
                // First run: backup current as source if no .bak yet.
                if (File.Exists(dest))
                {
                    File.Copy(dest, bak, overwrite: false);
                    Console.WriteLine("created bak " + bak);
                }
                else
                {
                    Console.WriteLine("skip missing " + dest);
                    continue;
                }
            }

            Patch(dest, bak);
        }

        return 0;
    }

    static void Patch(string dest, string bak)
    {
        var data = File.ReadAllBytes(bak);
        var tables = ReadTables(data);
        var cmap = ReadCmap(data, tables["cmap"]);
        int gI = cmap[0x0418], gY = cmap[0x0419];
        var gi = Parse(GetGlyf(data, tables, gI));

        var body = gi.Contours.Select(c => c.ToList()).ToList();
        int xMin = gi.XMin, xMax = gi.XMax, yMin = gi.YMin, yMax = gi.YMax;
        int midX = (xMin + xMax) / 2;
        int px = Math.Max(32, ((yMax - yMin) / 7 / 16) * 16);
        if (px < 32) px = 32;
        int colX = midX - px / 2;
        int yUpper = yMax - px;
        int yLower = yMax - 2 * px; // raised +1px vs previous (was 3*px gap layout)
        if (yLower < yMin) yLower = yMin;
        body.Add(Sq(colX, yLower, px));
        body.Add(Sq(colX, yUpper, px));

        var g = new G { Contours = body };
        g.Recalc();
        var enc = Encode(g);
        BinaryPrimitives.WriteInt16BigEndian(enc.AsSpan(2), gi.XMin);
        BinaryPrimitives.WriteInt16BigEndian(enc.AsSpan(4), gi.YMin);
        BinaryPrimitives.WriteInt16BigEndian(enc.AsSpan(6), (short)Math.Max(gi.XMax, colX + px));
        BinaryPrimitives.WriteInt16BigEndian(enc.AsSpan(8), gi.YMax);

        // Keep original name/OS/2/etc — only glyf/loca/head indexToLocFormat.
        data = ReplaceGlyphKeepNames(data, tables, gY, enc);
        File.WriteAllBytes(dest, data);
        Console.WriteLine($"OK {Path.GetFileName(dest)} size={data.Length} px={px} colX={colX}");

        try
        {
            var user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Fonts");
            Directory.CreateDirectory(user);
            var prefix = dest.Contains("QuestBook", StringComparison.OrdinalIgnoreCase)
                ? "swixyquestbook-v8-"
                : "swixyclaimchunk-v8-";
            var outName = prefix + Path.GetFileName(dest);
            File.Copy(dest, Path.Combine(user, outName), true);
            Console.WriteLine("  installed " + outName);
        }
        catch (Exception ex) { Console.WriteLine("  " + ex.Message); }
    }

    static List<(int x, int y)> Sq(int x, int y, int s) =>
        new() { (x, y + s), (x + s, y + s), (x + s, y), (x, y) };

    sealed class G
    {
        public List<List<(int x, int y)>> Contours = new();
        public short XMin, YMin, XMax, YMax;
        public void Recalc()
        {
            var p = Contours.SelectMany(c => c).ToList();
            XMin = (short)p.Min(t => t.x); YMin = (short)p.Min(t => t.y);
            XMax = (short)p.Max(t => t.x); YMax = (short)p.Max(t => t.y);
        }
    }

    static G Parse(byte[] glyf)
    {
        var g = new G();
        if (glyf.Length < 10) return g;
        short n = BinaryPrimitives.ReadInt16BigEndian(glyf.AsSpan(0, 2));
        g.XMin = BinaryPrimitives.ReadInt16BigEndian(glyf.AsSpan(2, 2));
        g.YMin = BinaryPrimitives.ReadInt16BigEndian(glyf.AsSpan(4, 2));
        g.XMax = BinaryPrimitives.ReadInt16BigEndian(glyf.AsSpan(6, 2));
        g.YMax = BinaryPrimitives.ReadInt16BigEndian(glyf.AsSpan(8, 2));
        if (n <= 0) return g;
        int o = 10;
        var end = new int[n];
        for (int i = 0; i < n; i++) { end[i] = BinaryPrimitives.ReadUInt16BigEndian(glyf.AsSpan(o, 2)); o += 2; }
        int nPts = end[^1] + 1;
        ushort instr = BinaryPrimitives.ReadUInt16BigEndian(glyf.AsSpan(o, 2)); o += 2 + instr;
        var flags = new byte[nPts];
        for (int i = 0; i < nPts;)
        {
            byte f = glyf[o++]; flags[i++] = f;
            if ((f & 8) != 0) { int r = glyf[o++]; for (int k = 0; k < r; k++) flags[i++] = f; }
        }
        var xs = new int[nPts]; var ys = new int[nPts];
        int x = 0, y = 0;
        for (int i = 0; i < nPts; i++)
        {
            byte f = flags[i];
            if ((f & 2) != 0) { int dx = glyf[o++]; x += ((f & 16) != 0) ? dx : -dx; }
            else if ((f & 16) == 0) { x += (short)BinaryPrimitives.ReadUInt16BigEndian(glyf.AsSpan(o, 2)); o += 2; }
            xs[i] = x;
        }
        for (int i = 0; i < nPts; i++)
        {
            byte f = flags[i];
            if ((f & 4) != 0) { int dy = glyf[o++]; y += ((f & 32) != 0) ? dy : -dy; }
            else if ((f & 32) == 0) { y += (short)BinaryPrimitives.ReadUInt16BigEndian(glyf.AsSpan(o, 2)); o += 2; }
            ys[i] = y;
        }
        int s = 0;
        for (int c = 0; c < n; c++)
        {
            int e = end[c];
            var cont = new List<(int, int)>();
            for (int i = s; i <= e; i++) cont.Add((xs[i], ys[i]));
            g.Contours.Add(cont);
            s = e + 1;
        }
        return g;
    }

    static byte[] Encode(G g)
    {
        g.Recalc();
        var all = g.Contours;
        int nPts = all.Sum(c => c.Count);
        var endPts = new List<int>();
        int acc = -1;
        foreach (var c in all) { acc += c.Count; endPts.Add(acc); }
        var xs = all.SelectMany(c => c.Select(p => p.x)).ToArray();
        var ys = all.SelectMany(c => c.Select(p => p.y)).ToArray();
        var flags = new byte[nPts];
        var xB = new List<byte>(); var yB = new List<byte>();
        int px = 0, py = 0;
        for (int i = 0; i < nPts; i++)
        {
            int dx = xs[i] - px, dy = ys[i] - py;
            byte f = 1;
            if (dx == 0) f |= 16;
            else if (dx >= 0 && dx <= 255) { f |= 2 | 16; xB.Add((byte)dx); }
            else if (dx < 0 && dx >= -255) { f |= 2; xB.Add((byte)(-dx)); }
            else { xB.Add((byte)((dx >> 8) & 0xFF)); xB.Add((byte)(dx & 0xFF)); }
            if (dy == 0) f |= 32;
            else if (dy >= 0 && dy <= 255) { f |= 4 | 32; yB.Add((byte)dy); }
            else if (dy < 0 && dy >= -255) { f |= 4; yB.Add((byte)(-dy)); }
            else { yB.Add((byte)((dy >> 8) & 0xFF)); yB.Add((byte)(dy & 0xFF)); }
            flags[i] = f; px = xs[i]; py = ys[i];
        }
        using var ms = new MemoryStream();
        void W16(int v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)v); ms.Write(b); }
        void W16s(int v) { var b = new byte[2]; BinaryPrimitives.WriteInt16BigEndian(b, (short)v); ms.Write(b); }
        W16s(all.Count); W16s(g.XMin); W16s(g.YMin); W16s(g.XMax); W16s(g.YMax);
        foreach (var e in endPts) W16(e);
        W16(0); ms.Write(flags); ms.Write(xB.ToArray()); ms.Write(yB.ToArray());
        return ms.ToArray();
    }

    static Dictionary<string, (int off, int len)> ReadTables(byte[] data)
    {
        int n = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4, 2));
        var d = new Dictionary<string, (int, int)>();
        for (int i = 0; i < n; i++)
        {
            int o = 12 + i * 16;
            d[Encoding.ASCII.GetString(data, o, 4)] = (
                (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(o + 8, 4)),
                (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(o + 12, 4)));
        }
        return d;
    }

    static Dictionary<int, int> ReadCmap(byte[] data, (int off, int len) t)
    {
        var map = new Dictionary<int, int>();
        int b = t.off, num = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(b + 2, 2));
        for (int i = 0; i < num; i++)
        {
            int e = b + 4 + i * 8;
            int sub = b + (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(e + 4, 4));
            if (BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(sub, 2)) != 4) continue;
            int seg = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(sub + 6, 2)) / 2;
            int endC = sub + 14, startC = endC + seg * 2 + 2, idD = startC + seg * 2, idR = idD + seg * 2;
            for (int s = 0; s < seg; s++)
            {
                int end = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(endC + s * 2, 2));
                int start = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(startC + s * 2, 2));
                short delta = (short)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(idD + s * 2, 2));
                ushort ro = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(idR + s * 2, 2));
                for (int c = start; c <= end; c++)
                {
                    int g;
                    if (ro == 0) g = (c + delta) & 0xFFFF;
                    else
                    {
                        int addr = idR + s * 2 + ro + (c - start) * 2;
                        int gg = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(addr, 2));
                        g = gg == 0 ? 0 : (gg + delta) & 0xFFFF;
                    }
                    if (g != 0) map[c] = g;
                }
            }
        }
        return map;
    }

    static byte[] GetGlyf(byte[] data, Dictionary<string, (int off, int len)> tables, int glyphId)
    {
        var head = tables["head"];
        short idx = (short)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(head.off + 50, 2));
        var loca = tables["loca"]; var glyf = tables["glyf"];
        int start, end;
        if (idx == 0)
        {
            start = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(loca.off + glyphId * 2, 2)) * 2;
            end = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(loca.off + (glyphId + 1) * 2, 2)) * 2;
        }
        else
        {
            start = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(loca.off + glyphId * 4, 4));
            end = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(loca.off + (glyphId + 1) * 4, 4));
        }
        var bytes = new byte[Math.Max(0, end - start)];
        Array.Copy(data, glyf.off + start, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>Rebuild font tables, preserving original name/OS/2/cmap/etc bytes.</summary>
    static byte[] ReplaceGlyphKeepNames(byte[] data, Dictionary<string, (int off, int len)> tables, int glyphId, byte[] newGlyf)
    {
        var head = tables["head"];
        var maxp = tables["maxp"];
        var loca = tables["loca"];
        var glyf = tables["glyf"];
        short idx = (short)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(head.off + 50, 2));
        int num = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(maxp.off + 4, 2));
        var offsets = new int[num + 1];
        for (int i = 0; i <= num; i++)
        {
            if (idx == 0) offsets[i] = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(loca.off + i * 2, 2)) * 2;
            else offsets[i] = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(loca.off + i * 4, 4));
        }
        var old = new byte[glyf.len];
        Array.Copy(data, glyf.off, old, 0, glyf.len);
        using var ms = new MemoryStream();
        var no = new int[num + 1];
        for (int g = 0; g < num; g++)
        {
            no[g] = (int)ms.Position;
            int s = offsets[g], e = offsets[g + 1], l = Math.Max(0, e - s);
            if (g == glyphId) ms.Write(newGlyf);
            else if (l > 0) ms.Write(old, s, l);
            while (ms.Position % 4 != 0) ms.WriteByte(0);
        }
        no[num] = (int)ms.Position;
        var ng = ms.ToArray();
        byte[] nl; short nidx;
        if (no[^1] > 0x1FFFE)
        {
            nidx = 1; nl = new byte[(num + 1) * 4];
            for (int i = 0; i <= num; i++) BinaryPrimitives.WriteUInt32BigEndian(nl.AsSpan(i * 4), (uint)no[i]);
        }
        else
        {
            nidx = 0; nl = new byte[(num + 1) * 2];
            for (int i = 0; i <= num; i++) BinaryPrimitives.WriteUInt16BigEndian(nl.AsSpan(i * 2), (ushort)(no[i] / 2));
        }
        var nh = new byte[head.len];
        Array.Copy(data, head.off, nh, 0, head.len);
        BinaryPrimitives.WriteUInt16BigEndian(nh.AsSpan(50), (ushort)nidx);

        var d = new Dictionary<string, byte[]>();
        foreach (var name in tables.Keys)
        {
            if (name == "head") d[name] = nh;
            else if (name == "loca") d[name] = nl;
            else if (name == "glyf") d[name] = ng;
            else
            {
                var b = new byte[tables[name].len];
                Array.Copy(data, tables[name].off, b, 0, b.Length);
                d[name] = b;
            }
        }
        return Assemble(d);
    }

    static byte[] Assemble(Dictionary<string, byte[]> tables)
    {
        // Preserve a stable order similar to original (not alphabetical) for better loader compat.
        string[] prefer = ["head", "hhea", "maxp", "OS/2", "hmtx", "cmap", "loca", "glyf", "name", "post"];
        var tags = prefer.Where(tables.ContainsKey).Concat(tables.Keys.Where(k => !prefer.Contains(k))).ToList();
        int n = tags.Count, ot = 12 + n * 16;
        var al = new Dictionary<string, byte[]>();
        foreach (var tag in tags)
        {
            var raw = tables[tag];
            int pad = (4 - (raw.Length % 4)) % 4;
            if (pad == 0) al[tag] = raw;
            else { var p = new byte[raw.Length + pad]; Array.Copy(raw, p, raw.Length); al[tag] = p; }
        }
        int cur = ot;
        var rec = new List<(string tag, uint sum, int off, int len)>();
        foreach (var tag in tags)
        {
            var p = al[tag];
            uint sum = 0;
            for (int i = 0; i + 3 < p.Length; i += 4) sum += BinaryPrimitives.ReadUInt32BigEndian(p.AsSpan(i, 4));
            rec.Add((tag, sum, cur, tables[tag].Length));
            cur += p.Length;
        }
        var output = new byte[cur];
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(0), 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4), (ushort)n);
        int mp = 1, ex = 0;
        while (mp * 2 <= n) { mp *= 2; ex++; }
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(6), (ushort)(mp * 16));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(8), (ushort)ex);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(10), (ushort)((n - mp) * 16));
        for (int i = 0; i < rec.Count; i++)
        {
            var r = rec[i];
            int o = 12 + i * 16;
            Encoding.ASCII.GetBytes(r.tag).CopyTo(output.AsSpan(o, 4));
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(o + 4), r.sum);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(o + 8), (uint)r.off);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(o + 12), (uint)r.len);
            Array.Copy(al[r.tag], 0, output, r.off, al[r.tag].Length);
        }
        var hr = rec.First(r => r.tag == "head");
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(hr.off + 8), 0);
        uint total = 0;
        for (int i = 0; i + 3 < output.Length; i += 4) total += BinaryPrimitives.ReadUInt32BigEndian(output.AsSpan(i, 4));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(hr.off + 8), unchecked(0xB1B0AFBA - total));
        return output;
    }
}
