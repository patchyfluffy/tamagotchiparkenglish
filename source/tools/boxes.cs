// Lists the layout boxes in a game file: runs of 12-byte entries [style][flags][x][y][text pointer]
// ending with style FFFF (drawn by 0x0601C7D6). Prints each entry's style, flags, position and text,
// and where the code refers to the box.
// Usage: boxes <file> <out.txt> [loadAddressHex]   (game overlays load at 0x00280000)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class BoxList
{
    static byte[] b;
    static uint Base = 0x06010000;
    static int W16(int o) { return b[o] << 8 | b[o + 1]; }
    static uint W32(int o) { return (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]); }
    static bool IsText(int c) { return (c >= 0x20 && c <= 0x5F) || (c >= 0xA1 && c <= 0xDF); }

    // Text at a file offset, if it looks like a line (text bytes then 00); null otherwise.
    static string TextAt(uint addr)
    {
        long o = (long)addr - Base;
        if (o < 0 || o >= b.Length) return null;
        int e = (int)o;
        while (e < b.Length && IsText(b[e])) e++;
        if (e >= b.Length || b[e] != 0 || e == o) return null;
        return Strings.Decode(b, (int)o, e);
    }

    static bool IsEntry(int o) { return o + 12 <= b.Length && W16(o) != 0xFFFF && TextAt(W32(o + 8)) != null; }

    static void Main(string[] a)
    {
        b = File.ReadAllBytes(a[0]);
        if (a.Length > 2) Base = Convert.ToUInt32(a[2].Replace("0x", ""), 16);
        var refs = new Dictionary<uint, List<int>>();
        for (int p = 0; p + 4 <= b.Length; p += 2)
        {
            uint v = W32(p);
            List<int> l; if (!refs.TryGetValue(v, out l)) refs[v] = l = new List<int>();
            l.Add(p);
        }
        var o = new StringBuilder();
        int count = 0;
        for (int p = 0; p + 12 <= b.Length; p += 4)
        {
            if (!IsEntry(p) || (p >= 12 && IsEntry(p - 12))) continue;
            int n = 0;
            while (IsEntry(p + 12 * n)) n++;
            if (p + 12 * n + 2 > b.Length || W16(p + 12 * n) != 0xFFFF) continue;
            List<int> r; refs.TryGetValue(Base + (uint)p, out r);
            o.AppendLine(string.Format("box\t0x{0:X5}\t{1} entries\tused by code at: {2}", p, n,
                r == null ? "(nothing points here directly)" : string.Join(",", r.Select(x => "0x" + x.ToString("X5")))));
            for (int k = 0; k < n; k++)
            {
                int e = p + 12 * k;
                int st = W16(e);
                string kind = (st & 0x8000) != 0 ? "  +" : "new";
                o.AppendLine(string.Format("  {0} style {1:X4} flags {2:X4} x {3,4} y {4,4}  {5}", kind, st, W16(e + 2), (short)W16(e + 4), (short)W16(e + 6), TextAt(W32(e + 8))));
            }
            count++;
        }
        File.WriteAllText(a[1], o.ToString(), new UTF8Encoding(true));
        Console.WriteLine("{0} boxes written to {1}", count, a[1]);
    }
}
