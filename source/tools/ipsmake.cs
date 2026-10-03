// Makes an IPS patch from an original file and a patched file of the same size, then checks it by
// applying it to the original and comparing with the patched file.
// IPS: "PATCH", then records [3-byte offset][2-byte size][data], then "EOF". Offsets must be below 16 MB.
// A record may not start at offset 0x454F46 (it would read as "EOF"), so such a record starts a byte earlier.
// Usage: ipsmake <original> <patched> <out.ips>
using System;
using System.Collections.Generic;
using System.IO;

class IpsMake
{
    const int MaxRecord = 0xFFFF, MergeGap = 6, EofMark = 0x454F46;

    static void Main(string[] a)
    {
        byte[] o = File.ReadAllBytes(a[0]), p = File.ReadAllBytes(a[1]);
        if (o.Length != p.Length) throw new Exception("the files differ in size");
        if (p.Length > 0x1000000) throw new Exception("IPS can't address files over 16 MB");

        // Changed runs, with small unchanged gaps merged in (a record costs 5 bytes of header).
        var runs = new List<int[]>();
        for (int i = 0; i < p.Length; i++)
        {
            if (o[i] == p[i]) continue;
            int s = i, e = i + 1;
            while (e < p.Length)
            {
                if (o[e] != p[e]) { e++; continue; }
                int g = e; while (g < p.Length && g - e < MergeGap && o[g] == p[g]) g++;
                if (g < p.Length && g - e < MergeGap && o[g] != p[g]) e = g; else break;
            }
            runs.Add(new[] { s, e });
            i = e - 1;
        }

        var ms = new MemoryStream();
        Action<byte[]> put = x => ms.Write(x, 0, x.Length);
        put(System.Text.Encoding.ASCII.GetBytes("PATCH"));
        int records = 0;
        foreach (var r in runs)
        {
            int s = r[0], e = r[1];
            if (s == EofMark) s--;
            for (int pos = s; pos < e; pos += MaxRecord)
            {
                int len = Math.Min(MaxRecord, e - pos);
                int start = pos == EofMark ? pos - 1 : pos;
                if (start != pos) len++;
                put(new[] { (byte)(start >> 16), (byte)(start >> 8), (byte)start, (byte)(len >> 8), (byte)len });
                ms.Write(p, start, len);
                records++;
            }
        }
        put(System.Text.Encoding.ASCII.GetBytes("EOF"));
        File.WriteAllBytes(a[2], ms.ToArray());

        // Check: apply to a copy of the original.
        byte[] ips = ms.ToArray(), t = (byte[])o.Clone();
        int k = 5;
        while (!(ips[k] == 'E' && ips[k + 1] == 'O' && ips[k + 2] == 'F' && k + 3 == ips.Length))
        {
            int off = ips[k] << 16 | ips[k + 1] << 8 | ips[k + 2], len = ips[k + 3] << 8 | ips[k + 4];
            Buffer.BlockCopy(ips, k + 5, t, off, len);
            k += 5 + len;
        }
        for (int i = 0; i < t.Length; i++) if (t[i] != p[i]) throw new Exception("check failed at 0x" + i.ToString("X"));
        Console.WriteLine("{0}: {1} records, {2} bytes; checked: applying it to the original gives the patched file exactly", a[2], records, ips.Length);
    }
}
