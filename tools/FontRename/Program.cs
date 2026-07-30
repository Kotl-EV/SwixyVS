using System.Buffers.Binary;
using System.Text;

// Unique family names so Windows/Cairo never pick a stale "Minecraft Rus"
// face (missing/wrong capital Й). Glyph patch stays in FontPatchY (run first).
internal static class Program
{
    static int Main()
    {
        // ClaimChunk
        Rename(
            @"E:\рабочие файлы\GitHub\SwixyVS\SwixyClaimChunk\assets\swixyclaimchunk\fonts\minecraft.ttf",
            "swixyclaimchunk-v9-minecraft.ttf",
            "Minecraft Rus", "SwixyClaimBody",
            "Minecraft Rus Regular", "SwixyClaimBody",
            "Minecraft", "SwixyClaimBody");
        Rename(
            @"E:\рабочие файлы\GitHub\SwixyVS\SwixyClaimChunk\assets\swixyclaimchunk\fonts\MinecraftTitle.ttf",
            "swixyclaimchunk-v9-MinecraftTitle.ttf",
            "Minecraft Five", "SwixyClaimTitle",
            "Minecraft Five Regular", "SwixyClaimTitle",
            "MinecraftFive", "SwixyClaimTitle",
            "MinecraftFive-Regular", "SwixyClaimTitle");

        // QuestBook (same faces, separate family so each mod owns its install)
        Rename(
            @"E:\рабочие файлы\GitHub\SwixyVS\SwixyQuestBook\assets\swixyquestbook\fonts\minecraft.ttf",
            "swixyquestbook-v9-minecraft.ttf",
            "Minecraft Rus", "SwixyQuestBody",
            "Minecraft Rus Regular", "SwixyQuestBody",
            "Minecraft", "SwixyQuestBody");
        Rename(
            @"E:\рабочие файлы\GitHub\SwixyVS\SwixyQuestBook\assets\swixyquestbook\fonts\MinecraftTitle.ttf",
            "swixyquestbook-v9-MinecraftTitle.ttf",
            "Minecraft Five", "SwixyQuestTitle",
            "Minecraft Five Regular", "SwixyQuestTitle",
            "MinecraftFive", "SwixyQuestTitle",
            "MinecraftFive-Regular", "SwixyQuestTitle");

        return 0;
    }

    static void Rename(string path, string installName, params string[] pairs)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine("MISSING " + path);
            return;
        }

        var data = File.ReadAllBytes(path);
        var tables = ReadTables(data);
        if (!tables.ContainsKey("name")) throw new Exception("no name");
        var nameOff = tables["name"].off;
        var nameLen = tables["name"].len;
        var name = new byte[nameLen];
        Array.Copy(data, nameOff, name, 0, nameLen);

        ushort format = BinaryPrimitives.ReadUInt16BigEndian(name.AsSpan(0, 2));
        ushort count = BinaryPrimitives.ReadUInt16BigEndian(name.AsSpan(2, 2));
        ushort stringOffset = BinaryPrimitives.ReadUInt16BigEndian(name.AsSpan(4, 2));

        var recs = new List<(ushort p, ushort e, ushort l, ushort id, byte[] bytes)>();
        for (int i = 0; i < count; i++)
        {
            int o = 6 + i * 12;
            ushort p = BinaryPrimitives.ReadUInt16BigEndian(name.AsSpan(o, 2));
            ushort e = BinaryPrimitives.ReadUInt16BigEndian(name.AsSpan(o + 2, 2));
            ushort l = BinaryPrimitives.ReadUInt16BigEndian(name.AsSpan(o + 4, 2));
            ushort id = BinaryPrimitives.ReadUInt16BigEndian(name.AsSpan(o + 6, 2));
            ushort len = BinaryPrimitives.ReadUInt16BigEndian(name.AsSpan(o + 8, 2));
            ushort so = BinaryPrimitives.ReadUInt16BigEndian(name.AsSpan(o + 10, 2));
            var bytes = new byte[len];
            Array.Copy(name, stringOffset + so, bytes, 0, len);

            string text;
            try
            {
                text = p == 3 || p == 0
                    ? Encoding.BigEndianUnicode.GetString(bytes)
                    : Encoding.ASCII.GetString(bytes);
            }
            catch { text = Encoding.ASCII.GetString(bytes); }

            // Longest match first so "Minecraft Rus Regular" wins over "Minecraft Rus" / "Minecraft".
            var ordered = new List<(string from, string to)>();
            for (int k = 0; k + 1 < pairs.Length; k += 2)
                ordered.Add((pairs[k], pairs[k + 1]));
            ordered.Sort((a, b) => b.from.Length.CompareTo(a.from.Length));

            foreach (var (from, to) in ordered)
            {
                if (text.Equals(from, StringComparison.OrdinalIgnoreCase)
                    || text.Contains(from, StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Replace(from, to, StringComparison.OrdinalIgnoreCase);
                }
            }

            byte[] neu = p == 3 || p == 0
                ? Encoding.BigEndianUnicode.GetBytes(text)
                : Encoding.ASCII.GetBytes(text);
            recs.Add((p, e, l, id, neu));
        }

        int storage = 0;
        foreach (var r in recs) storage += r.bytes.Length;
        int header = 6 + recs.Count * 12;
        var neuName = new byte[header + storage];
        BinaryPrimitives.WriteUInt16BigEndian(neuName.AsSpan(0), format);
        BinaryPrimitives.WriteUInt16BigEndian(neuName.AsSpan(2), (ushort)recs.Count);
        BinaryPrimitives.WriteUInt16BigEndian(neuName.AsSpan(4), (ushort)header);
        int cur = 0;
        for (int i = 0; i < recs.Count; i++)
        {
            var r = recs[i];
            int o = 6 + i * 12;
            BinaryPrimitives.WriteUInt16BigEndian(neuName.AsSpan(o), r.p);
            BinaryPrimitives.WriteUInt16BigEndian(neuName.AsSpan(o + 2), r.e);
            BinaryPrimitives.WriteUInt16BigEndian(neuName.AsSpan(o + 4), r.l);
            BinaryPrimitives.WriteUInt16BigEndian(neuName.AsSpan(o + 6), r.id);
            BinaryPrimitives.WriteUInt16BigEndian(neuName.AsSpan(o + 8), (ushort)r.bytes.Length);
            BinaryPrimitives.WriteUInt16BigEndian(neuName.AsSpan(o + 10), (ushort)cur);
            Array.Copy(r.bytes, 0, neuName, header + cur, r.bytes.Length);
            cur += r.bytes.Length;
        }

        var newTables = new Dictionary<string, byte[]>();
        foreach (var kv in tables)
        {
            if (kv.Key == "name") newTables["name"] = neuName;
            else
            {
                var b = new byte[kv.Value.len];
                Array.Copy(data, kv.Value.off, b, 0, kv.Value.len);
                newTables[kv.Key] = b;
            }
        }
        var outBytes = Assemble(newTables);
        File.WriteAllBytes(path, outBytes);
        Console.WriteLine($"OK {Path.GetFileName(path)} nameLen {nameLen} -> {neuName.Length}");

        try
        {
            var user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Fonts");
            Directory.CreateDirectory(user);
            var dest = Path.Combine(user, installName);
            File.Copy(path, dest, true);
            Console.WriteLine("  installed " + installName);
        }
        catch (Exception ex) { Console.WriteLine("  " + ex.Message); }
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

    static byte[] Assemble(Dictionary<string, byte[]> tables)
    {
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
