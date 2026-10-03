// "BZ.w" codec used by Tamagotchi Park (Saturn), reimplemented from the routine at 0x06011468 in 0EXEC.BIN.
//
// Block layout: "BZ.w" (4 bytes), u16 unknown (0x01FF seen), u16 mode (always 1), then 16-bit big-endian tokens:
//   1LLooooo oooooooo        copy L+2 words from (out - o words); may be followed by
//   011nnnnn nnnnnnnn          ...continuation tokens that copy n more words from the same running source
//   01xnnnnn nnnnnnnn vvvv   repeat word v, n times (bit 13 is ignored)
//   001nnnnn nnnnnnnn ...    copy the next n words literally
//   000Fxxxx xxxxxxxx        end; if F (0x1000) is set, the output length is odd: drop its last byte
//
// Usage:
//   bz unpackall <srcDir> <outDir>         scan every file for BZ.w blocks and unpack them
//   bz pack <in> <out> [srcWithHeader]     pack a file; copies header bytes 4-5 from srcWithHeader if given
//   bz roundtrip <srcDir>                  repack every block on the disc and check it unpacks identically
using System;
using System.Collections.Generic;
using System.IO;

class Bz
{
    static int W(byte[] b, int i) { return b[i] << 8 | b[i + 1]; }

    // Returns the unpacked bytes; 'consumed' receives the packed size including the 8-byte header.
    public static byte[] Unpack(byte[] src, int start, out int consumed)
    {
        if (W(src, start) != 0x425A || W(src, start + 2) != 0x2E77) throw new Exception("not a BZ.w block");
        if (W(src, start + 6) != 1) throw new Exception("unsupported mode " + W(src, start + 6));
        var o = new List<byte>();
        int p = start + 8;
        while (true)
        {
            int t = W(src, p); p += 2;
            int n = t & 0x1FFF;
            if ((t & 0x8000) != 0)
            {
                int len = ((t >> 13) & 3) + 2;
                int from = o.Count - n * 2;
                for (int k = 0; k < len * 2; k++) o.Add(o[from++]);
                while ((W(src, p) & 0xE000) == 0x6000)
                {
                    int more = W(src, p) & 0x1FFF; p += 2;
                    for (int k = 0; k < more * 2; k++) o.Add(o[from++]);
                }
            }
            else if ((t & 0x4000) != 0)
            {
                byte hi = src[p], lo = src[p + 1]; p += 2;
                for (int k = 0; k < n; k++) { o.Add(hi); o.Add(lo); }
            }
            else if ((t & 0x2000) != 0)
            {
                for (int k = 0; k < n * 2; k++) o.Add(src[p++]);
            }
            else
            {
                if ((t & 0x1000) != 0) o.RemoveAt(o.Count - 1);
                break;
            }
        }
        consumed = p - start;
        return o.ToArray();
    }

    const int MaxN = 0x1FFF;      // largest count/offset a token can hold
    const int MaxChain = 512;     // match candidates checked per position
    const int MaxMatch = 4096;    // longest match measured (longer ones just become two tokens)

    // Packs with an optimal parse: a dynamic program picks the cheapest mix of literal runs,
    // repeats and back-references, measured in output words.
    public static byte[] Pack(byte[] data, int unknownHeaderWord)
    {
        bool odd = (data.Length & 1) != 0;
        int n = (data.Length + 1) / 2;
        var w = new int[n];
        for (int i = 0; i < n; i++) w[i] = data[2 * i] << 8 | (2 * i + 1 < data.Length ? data[2 * i + 1] : 0);

        var run = new int[n + 1];
        for (int i = n - 1; i >= 0; i--) run[i] = (i + 1 < n && w[i] == w[i + 1]) ? run[i + 1] + 1 : 1;

        // Longest back-reference at each position, via hash chains on word pairs.
        var lzLen = new int[n];
        var lzOff = new int[n];
        var head = new Dictionary<long, int>();
        var prev = new int[n];
        for (int i = 0; i < n; i++)
        {
            prev[i] = -1;
            if (i + 1 >= n) continue;
            long key = (long)w[i] << 16 | (uint)w[i + 1];
            int j;
            if (head.TryGetValue(key, out j)) prev[i] = j;
            head[key] = i;
            int best = 0, bestOff = 0, chain = 0;
            for (int c = prev[i]; c >= 0 && i - c <= MaxN && chain < MaxChain; c = prev[c], chain++)
            {
                int k = 0, lim = Math.Min(n - i, MaxMatch);
                while (k < lim && w[i + k] == w[c + k]) k++;
                if (k > best) { best = k; bestOff = i - c; if (k >= lim) break; }
            }
            if (best >= 2) { lzLen[i] = best; lzOff[i] = best >= 2 ? bestOff : 0; }
        }

        // costA[i]: cheapest way to encode w[i..] starting at a token boundary.
        // costB[i]: same, but we're inside a literal run whose header is already paid for.
        const int LIT = 0, CONT = 0, END = 1;
        var costA = new long[n + 1];
        var costB = new long[n + 1];
        var choiceA = new int[n + 1];   // LIT, >0 back-reference length, <0 repeat length
        var choiceB = new int[n + 1];   // CONT or END
        for (int i = n - 1; i >= 0; i--)
        {
            long best = 2 + costB[i + 1];
            int choice = LIT;
            int r = Math.Min(run[i], MaxN);
            if (2 + costA[i + r] < best) { best = 2 + costA[i + r]; choice = -r; }
            int L = lzLen[i];
            if (L >= 2)
            {
                for (int l = 2; l <= Math.Min(5, L); l++) if (1 + costA[i + l] < best) { best = 1 + costA[i + l]; choice = l; }
                for (int l = 6; l <= Math.Min(32, L); l++) if (2 + costA[i + l] < best) { best = 2 + costA[i + l]; choice = l; }
                if (L > 32)
                {
                    long c = 1 + (L - 5 + MaxN - 1) / MaxN + costA[i + L];
                    if (c < best) { best = c; choice = L; }
                }
            }
            costA[i] = best; choiceA[i] = choice;
            if (1 + costB[i + 1] <= costA[i]) { costB[i] = 1 + costB[i + 1]; choiceB[i] = CONT; }
            else { costB[i] = costA[i]; choiceB[i] = END; }
        }

        var o = new List<int>();
        int p = 0;
        while (p < n)
        {
            int ch = choiceA[p];
            if (ch == LIT)
            {
                var lit = new List<int> { w[p++] };
                while (p < n && choiceB[p] == CONT) lit.Add(w[p++]);
                for (int s = 0; s < lit.Count; s += MaxN)
                {
                    int cnt = Math.Min(MaxN, lit.Count - s);
                    o.Add(0x2000 | cnt);
                    for (int k = 0; k < cnt; k++) o.Add(lit[s + k]);
                }
            }
            else if (ch < 0)
            {
                o.Add(0x4000 | -ch); o.Add(w[p]); p += -ch;
            }
            else
            {
                int first = Math.Min(ch, 5);
                o.Add(0x8000 | (first - 2) << 13 | lzOff[p]);
                for (int rest = ch - first; rest > 0; rest -= MaxN) o.Add(0x6000 | Math.Min(rest, MaxN));
                p += ch;
            }
        }
        o.Add(odd ? 0x1000 : 0x0000);

        var outB = new byte[8 + o.Count * 2];
        outB[0] = 0x42; outB[1] = 0x5A; outB[2] = 0x2E; outB[3] = 0x77;
        outB[4] = (byte)(unknownHeaderWord >> 8); outB[5] = (byte)unknownHeaderWord;
        outB[6] = 0; outB[7] = 1;
        for (int k = 0; k < o.Count; k++) { outB[8 + 2 * k] = (byte)(o[k] >> 8); outB[9 + 2 * k] = (byte)o[k]; }
        return outB;
    }

    static IEnumerable<Tuple<string, byte[], int>> Blocks(string srcDir)
    {
        foreach (var f in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(f) == "0EXEC.BIN") continue; // contains the magic as a constant, not a block
            byte[] b = File.ReadAllBytes(f);
            for (int i = 0; i + 8 <= b.Length; i += 2)
                if (b[i] == 0x42 && b[i + 1] == 0x5A && b[i + 2] == 0x2E && b[i + 3] == 0x77)
                    yield return Tuple.Create(f, b, i);
        }
    }

    static void Main(string[] args)
    {
        if (args[0] == "unpackall")
        {
            string outDir = args[2];
            Directory.CreateDirectory(outDir);
            foreach (var blk in Blocks(args[1]))
            {
                string name = Path.GetFileName(blk.Item1);
                try
                {
                    int used;
                    byte[] u = Unpack(blk.Item2, blk.Item3, out used);
                    File.WriteAllBytes(Path.Combine(outDir, string.Format("{0}@{1:X6}.unp", name, blk.Item3)), u);
                    Console.WriteLine("{0,-14} @0x{1:X6}  packed {2,8}  unpacked {3,8}  ends 0x{4:X6}", name, blk.Item3, used, u.Length, blk.Item3 + used);
                }
                catch (Exception e)
                {
                    Console.WriteLine("{0,-14} @0x{1:X6}  FAILED: {2}", name, blk.Item3, e.Message);
                }
            }
        }
        else if (args[0] == "pack")
        {
            byte[] data = File.ReadAllBytes(args[1]);
            int hdr = 0x01FF;
            if (args.Length > 3) { byte[] src = File.ReadAllBytes(args[3]); hdr = W(src, 4); }
            byte[] packed = Pack(data, hdr);
            int used;
            byte[] check = Unpack(packed, 0, out used);
            if (used != packed.Length || check.Length != data.Length) throw new Exception("pack check failed (size)");
            for (int k = 0; k < data.Length; k++) if (check[k] != data[k]) throw new Exception("pack check failed at byte " + k);
            File.WriteAllBytes(args[2], packed);
            Console.WriteLine("packed {0} -> {1} bytes (verified)", data.Length, packed.Length);
        }
        else if (args[0] == "roundtrip")
        {
            int ok = 0, bad = 0; long origTotal = 0, newTotal = 0;
            foreach (var blk in Blocks(args[1]))
            {
                int used, used2;
                byte[] u = Unpack(blk.Item2, blk.Item3, out used);
                byte[] packed = Pack(u, W(blk.Item2, blk.Item3 + 4));
                byte[] u2 = Unpack(packed, 0, out used2);
                bool same = u2.Length == u.Length && used2 == packed.Length;
                for (int k = 0; same && k < u.Length; k++) same = u[k] == u2[k];
                if (same) ok++; else bad++;
                origTotal += used; newTotal += packed.Length;
                Console.WriteLine("{0,-14} @0x{1:X6}  game {2,7}  ours {3,7}  {4}", Path.GetFileName(blk.Item1), blk.Item3, used, packed.Length, same ? "OK" : "MISMATCH");
            }
            Console.WriteLine("{0} OK, {1} mismatched. Total packed: game {2}, ours {3}", ok, bad, origTotal, newTotal);
        }
    }
}
