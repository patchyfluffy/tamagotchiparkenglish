// Makes a BPS patch from an original file and a patched file, then checks it by applying it.
// BPS ("BPS1"): sizes, then actions (SourceRead = copy the original's bytes at the same position,
// TargetRead = new bytes follow), then CRC32s of the original, the result and the patch itself, so
// patching tools can refuse the wrong original file.
// Usage: bpsmake <original> <patched> <out.bps>
using System;
using System.IO;

class BpsMake
{
    static uint[] table = MakeTable();
    static uint[] MakeTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; t[i] = c; }
        return t;
    }
    static uint Crc(byte[] d, int len) { uint c = 0xFFFFFFFF; for (int i = 0; i < len; i++) c = table[(c ^ d[i]) & 0xFF] ^ (c >> 8); return c ^ 0xFFFFFFFF; }

    static void Num(Stream s, ulong v)
    {
        while (true) { ulong x = v & 0x7F; v >>= 7; if (v == 0) { s.WriteByte((byte)(0x80 | x)); break; } s.WriteByte((byte)x); v--; }
    }
    static ulong ReadNum(byte[] d, ref int p)
    {
        ulong v = 0, shift = 1;
        while (true) { byte x = d[p++]; v += (ulong)(x & 0x7F) * shift; if ((x & 0x80) != 0) break; shift <<= 7; v += shift; }
        return v;
    }
    static void U32(Stream s, uint v) { for (int i = 0; i < 4; i++) s.WriteByte((byte)(v >> (8 * i))); }

    static void Main(string[] a)
    {
        byte[] o = File.ReadAllBytes(a[0]), t = File.ReadAllBytes(a[1]);
        var ms = new MemoryStream();
        ms.Write(new byte[] { (byte)'B', (byte)'P', (byte)'S', (byte)'1' }, 0, 4);
        Num(ms, (ulong)o.Length); Num(ms, (ulong)t.Length); Num(ms, 0);
        int pos = 0;
        while (pos < t.Length)
        {
            int start = pos;
            bool same = pos < o.Length && o[pos] == t[pos];
            if (same)
            {
                while (pos < t.Length && pos < o.Length && o[pos] == t[pos]) pos++;
                Num(ms, ((ulong)(pos - start - 1) << 2) | 0);          // SourceRead
            }
            else
            {
                // Run of new bytes; short unchanged stretches (under 8 bytes) are cheaper to include.
                while (pos < t.Length)
                {
                    if (pos < o.Length && o[pos] == t[pos])
                    {
                        int q = pos; while (q < t.Length && q < o.Length && o[q] == t[q] && q - pos < 8) q++;
                        if (q - pos >= 8 || q >= t.Length) break;
                        pos = q;
                    }
                    else pos++;
                }
                Num(ms, ((ulong)(pos - start - 1) << 2) | 1);          // TargetRead
                ms.Write(t, start, pos - start);
            }
        }
        U32(ms, Crc(o, o.Length)); U32(ms, Crc(t, t.Length));
        byte[] body = ms.ToArray(); U32(ms, Crc(body, body.Length));
        byte[] bps = ms.ToArray();
        File.WriteAllBytes(a[2], bps);

        // Check: apply it.
        int p = 4; ulong ss = ReadNum(bps, ref p), ts = ReadNum(bps, ref p), md = ReadNum(bps, ref p); p += (int)md;
        var r = new byte[ts]; int op = 0;
        while (p < bps.Length - 12)
        {
            ulong d = ReadNum(bps, ref p); int len = (int)(d >> 2) + 1, cmd = (int)(d & 3);
            if (cmd == 0) { Buffer.BlockCopy(o, op, r, op, len); op += len; }
            else if (cmd == 1) { Buffer.BlockCopy(bps, p, r, op, len); p += len; op += len; }
            else throw new Exception("unexpected action");
        }
        if ((ulong)op != ts || (ulong)o.Length != ss) throw new Exception("size check failed");
        for (int i = 0; i < r.Length; i++) if (r[i] != t[i]) throw new Exception("check failed at 0x" + i.ToString("X"));
        Console.WriteLine("{0}: {1} bytes; source CRC32 {2:X8}, result CRC32 {3:X8}; checked: applying it gives the patched file exactly",
            a[2], bps.Length, Crc(o, o.Length), Crc(t, t.Length));
    }
}
