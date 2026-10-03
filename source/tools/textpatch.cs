// Replaces text in a game file (0EXEC.BIN or an unpacked game overlay) using a translation sheet,
// moving lines and updating pointers to them where needed, and rewriting "layout boxes".
//
// Sheet (UTF-8, tab-separated, '#' lines ignored):
//   offset (hex) <TAB> Japanese <TAB> English [<TAB> note]
//     - The Japanese column must match what's on the disc (a safety check against typos/wrong offsets).
//     - English is written in capitals (the font has no lowercase). '@' can't be used (it switches to katakana).
//     - Note "room=N": the line is in a fixed-size table; it stays put and may use N bytes.
//     - An empty English column leaves the line untranslated; write {empty} to replace it with nothing.
//     - Note "keepold": move the English elsewhere and leave the old slot's bytes untouched (for text whose
//       old space now holds something else, e.g. new code from code-patches.txt).
//   box <TAB> tableOffset (hex) [<TAB> left]
//   |line of English, with {c} color tags      (one '|' line per screen line; "|" alone = blank line)
//   endbox
//     - A layout box is a list of 12-byte entries [style][flags][x][y][text pointer] ending in style FFFF,
//       drawn by 0x0601C7D6, which sizes the box to the text and centers it on the screen.
//     - {c} sets the color for the following text: 0 default (black), 2 black, 3 red, 4 blue, 5 green.
//       {c,h,mask} also flashes color h while input bits 'mask' (hex) are pressed (used by control hints);
//       {c,h,mask,4} does the same using the second input source (style bit 0x0400 instead of 0x0200).
//     - {_N} writes N spaces (use it for spaces at the end of a line, which editors tend to strip).
//     - Lines are centered within the box (or left-aligned with "left"). The box's old Japanese pieces
//       are freed and the English pieces stored in their place; the box must not need more entries
//       than it had.
//
// How space is found: each translated line frees its slot (the text, its 00, and any 0xFF padding),
// as does each old box piece. Neighboring slots join into one region, and the English is packed into
// those regions. A line can only move if something points to it directly (an aligned 32-bit address);
// a line without such a pointer must fit in its own slot.
//
// Usage: textpatch <in> <sheet.tsv> <out> [--base hexLoadAddress] [otherFilesToCheckForPointers...]
//   (--base defaults to 0x06010000, the main program; game overlays load at 0x00280000)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class TextPatch
{
    static uint Base = 0x06010000;
    const int Advance = 13, LineHeight = 13;

    class Line { public int Off, SlotEnd; public string Ja, En; public byte[] Bytes; public List<int> Ptrs; public int NewOff = -1; public bool Pinned, KeepOld; }
    class Seg { public int Style, Flags; public string Text; public byte[] Bytes; public int NewOff = -1; }
    class Box { public int Table, Records; public bool Left; public List<List<Seg>> Lines = new List<List<Seg>>(); public List<uint> Pieces = new List<uint>(); }

    static int W16(byte[] b, int o) { return b[o] << 8 | b[o + 1]; }
    static uint W32(byte[] b, int o) { return (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]); }
    static void P16(byte[] b, int o, int v) { b[o] = (byte)(v >> 8); b[o + 1] = (byte)v; }
    static void P32(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    static byte[] Encode(string en)
    {
        var o = new List<byte>();
        foreach (char c0 in en)
        {
            char c = char.ToUpperInvariant(c0);
            if (c < 0x20 || c > 0x5F || c == '@') throw new Exception("can't write '" + c0 + "' with the game's font (in \"" + en + "\")");
            o.Add((byte)c);
        }
        o.Add(0);
        return o.ToArray();
    }

    static string Norm(string s) { return s.Normalize(NormalizationForm.FormC); }

    // Splits "{4}POINT {0}THE SAME WAY" into colored pieces.
    static List<Seg> ParseBoxLine(string s)
    {
        var segs = new List<Seg>();
        int style = 0, flags = 0;
        var cur = new StringBuilder();
        for (int i = 0; i <= s.Length; i++)
        {
            // {_N}: N spaces in the current piece (trailing spaces in a sheet are easily lost by editors).
            if (i < s.Length && s[i] == '{' && i + 1 < s.Length && s[i + 1] == '_')
            {
                int k = s.IndexOf('}', i);
                if (k < 0) throw new Exception("missing } in box line: " + s);
                cur.Append(' ', int.Parse(s.Substring(i + 2, k - i - 2)));
                i = k;
                continue;
            }
            if (i == s.Length || s[i] == '{')
            {
                if (cur.Length > 0) { segs.Add(new Seg { Style = style, Flags = flags, Text = cur.ToString(), Bytes = Encode(cur.ToString()) }); cur.Clear(); }
                if (i == s.Length) break;
                int j = s.IndexOf('}', i);
                if (j < 0) throw new Exception("missing } in box line: " + s);
                var parts = s.Substring(i + 1, j - i - 1).Split(',');
                int color = int.Parse(parts[0]);
                style = color > 0 ? 0x0800 | (color & 7) : 0;
                flags = 0;
                // {c,h,mask[,4]}: flash color h while 'mask' inputs are held; ",4" uses the second input source (style bit 0x0400).
                if (parts.Length >= 3) { style |= (parts.Length >= 4 && parts[3] == "4" ? 0x0400 : 0x0200) | (int.Parse(parts[1]) & 7) << 3; flags = Convert.ToInt32(parts[2], 16); }
                i = j;
            }
            else cur.Append(s[i]);
        }
        return segs;
    }

    static int SlotEnd(byte[] b, int off)
    {
        int e = off;
        while (e < b.Length && b[e] != 0) e++;
        e++;
        while (e < b.Length && b[e] == 0xFF) e++;
        return e;
    }

    static void Main(string[] argv)
    {
        var a = new List<string>(argv);
        int bi = a.IndexOf("--base");
        if (bi >= 0) { Base = Convert.ToUInt32(a[bi + 1].Replace("0x", ""), 16); a.RemoveRange(bi, 2); }
        byte[] b = File.ReadAllBytes(a[0]);
        var refs = new Dictionary<uint, List<int>>();
        for (int p = 0; p + 4 <= b.Length; p += 4)
        {
            uint v = W32(b, p);
            List<int> l; if (!refs.TryGetValue(v, out l)) refs[v] = l = new List<int>();
            l.Add(p);
        }

        var lines = new List<Line>();
        var boxes = new List<Box>();
        Box box = null;
        foreach (string raw0 in File.ReadAllLines(a[1], Encoding.UTF8))
        {
            string raw = raw0.TrimEnd('\r');
            if (box != null)
            {
                if (raw.Trim() == "endbox") { box = null; continue; }
                if (raw.StartsWith("|")) { box.Lines.Add(ParseBoxLine(raw.Substring(1))); continue; }
                if (raw.Trim().Length == 0 || raw.TrimStart().StartsWith("#")) continue;
                throw new Exception("inside a box, expected '|' lines or 'endbox', got: " + raw);
            }
            if (raw.Trim().Length == 0 || raw.TrimStart().StartsWith("#")) continue;
            var f = raw.Split('\t');
            if (f[0].Trim() == "box")
            {
                box = new Box { Table = Convert.ToInt32(f[1].Trim().Replace("0x", ""), 16), Left = f.Length > 2 && f[2].Trim() == "left" };
                boxes.Add(box);
                continue;
            }
            if (f.Length < 3 || f[2].Length == 0) continue;
            // Spaces at the start of a line matter (some labels are indented), so only line endings are stripped.
            // An empty English column means "not translated yet"; "{empty}" means "replace with nothing".
            var ln = new Line { Off = Convert.ToInt32(f[0].Trim().Replace("0x", ""), 16), Ja = f[1], En = f[2] == "{empty}" ? "" : f[2] };
            int e = ln.Off;
            while (e < b.Length && b[e] != 0) e++;
            string onDisc = Strings.Decode(b, ln.Off, e);
            if (Norm(onDisc) != Norm(ln.Ja)) throw new Exception(string.Format("0x{0:X5}: sheet says \"{1}\" but the file has \"{2}\"", ln.Off, ln.Ja, onDisc));
            ln.SlotEnd = SlotEnd(b, ln.Off);
            // Note "keepold": the old slot holds something else now (e.g. new code), so the English must move
            // elsewhere and the old bytes are left exactly as they are.
            ln.KeepOld = System.Text.RegularExpressions.Regex.IsMatch(f.Length > 3 ? f[3] : "", @"^\s*keepold\b");
            var room = System.Text.RegularExpressions.Regex.Match(f.Length > 3 ? f[3] : "", @"^\s*room=(\d+)");
            if (room.Success)
            {
                ln.Pinned = true;
                ln.SlotEnd = ln.Off + int.Parse(room.Groups[1].Value);
                for (int p = e + 1; p < ln.SlotEnd; p++)
                    if (b[p] != 0) throw new Exception(string.Format("0x{0:X5}: room={1} would overwrite non-zero data at 0x{2:X5}", ln.Off, ln.SlotEnd - ln.Off, p));
            }
            ln.Bytes = Encode(ln.En);
            List<int> r; ln.Ptrs = refs.TryGetValue(Base + (uint)ln.Off, out r) ? r : new List<int>();
            // A pointer stored at a 2-byte (not 4-byte) boundary wouldn't be updated; flag it.
            for (int p = 2; p + 4 <= b.Length; p += 4)
                if (W32(b, p) == Base + (uint)ln.Off)
                    Console.WriteLine("WARNING: 0x{0:X5} \"{1}\" may also be referenced from 0x{2:X5} (not updated)", ln.Off, ln.Ja, p);
            lines.Add(ln);
        }
        if (box != null) throw new Exception("box at 0x" + box.Table.ToString("X") + " has no endbox");
        lines = lines.OrderBy(l => l.Off).ToList();

        // Boxes: read the existing entries, and free each old piece whose only pointers are box entries.
        var boxPtrFields = new HashSet<int>();
        foreach (var bx in boxes)
        {
            int n = 0;
            while (W16(b, bx.Table + 12 * n) != 0xFFFF) { if (++n > 64) throw new Exception("box at 0x" + bx.Table.ToString("X") + ": no end marker within 64 entries; wrong offset?"); }
            bx.Records = n;
            for (int k = 0; k < n; k++) { boxPtrFields.Add(bx.Table + 12 * k + 8); bx.Pieces.Add(W32(b, bx.Table + 12 * k + 8)); }
            int needed = bx.Lines.Sum(l => l.Count);
            if (needed > n) throw new Exception(string.Format("box at 0x{0:X}: the English needs {1} pieces but the box only has {2} entries; use fewer color changes", bx.Table, needed, n));
        }
        var freedPieces = new List<int[]>();
        foreach (uint addr in boxes.SelectMany(bx => bx.Pieces).Distinct())
        {
            int off = (int)(addr - Base);
            if (off < 0 || off >= b.Length) continue;
            List<int> r; refs.TryGetValue(addr, out r);
            if (r == null || r.Any(p => !boxPtrFields.Contains(p))) { Console.WriteLine("note: box piece 0x{0:X5} is also used elsewhere; left in place", off); continue; }
            if (lines.Any(l => l.Off == off)) continue;
            freedPieces.Add(new[] { off, SlotEnd(b, off) });
        }

        // Free regions: runs of touching slots (translated lines + freed box pieces).
        var slots = lines.Where(l => !l.Pinned && !l.KeepOld).Select(l => new[] { l.Off, l.SlotEnd }).Concat(freedPieces).OrderBy(s => s[0]).ToList();
        var regions = new List<int[]>();
        foreach (var s in slots)
        {
            if (regions.Count > 0 && regions[regions.Count - 1][1] >= s[0]) regions[regions.Count - 1][1] = Math.Max(regions[regions.Count - 1][1], s[1]);
            else regions.Add(new[] { s[0], s[1] });
        }
        var used = new List<int[]>();
        Func<int, int> place = len =>
        {
            foreach (var rg in regions)
            {
                int pos = rg[0];
                while (pos + len <= rg[1])
                {
                    var hit = used.FirstOrDefault(u => pos < u[1] && pos + len > u[0]);
                    if (hit == null) { used.Add(new[] { pos, pos + len }); return pos; }
                    pos = hit[1];
                }
            }
            return -1;
        };
        // Lines without pointers (and table entries) stay put; everything else is packed into the gaps.
        foreach (var l in lines.Where(l => l.Ptrs.Count == 0 || l.Pinned))
        {
            if (l.KeepOld) throw new Exception(string.Format("0x{0:X5}: keepold needs a line that can move (one with a pointer)", l.Off));
            if (l.Bytes.Length > l.SlotEnd - l.Off) throw new Exception(string.Format("0x{0:X5} \"{1}\": {2}, so it can't move, and \"{3}\" needs {4} bytes but there are {5}",
                l.Off, l.Ja, l.Pinned ? "it's in a fixed-size table" : "no pointer", l.En, l.Bytes.Length, l.SlotEnd - l.Off));
            l.NewOff = l.Off; used.Add(new[] { l.Off, l.Off + l.Bytes.Length });
        }
        foreach (var l in lines.Where(l => l.Ptrs.Count > 0 && !l.Pinned))
        {
            l.NewOff = place(l.Bytes.Length);
            if (l.NewOff < 0) throw new Exception(string.Format("out of space for 0x{0:X5} \"{1}\" -> \"{2}\" ({3} bytes). Shorten some lines.", l.Off, l.Ja, l.En, l.Bytes.Length));
        }
        foreach (var bx in boxes)
            foreach (var sg in bx.Lines.SelectMany(x => x))
            {
                sg.NewOff = place(sg.Bytes.Length);
                if (sg.NewOff < 0) throw new Exception(string.Format("out of space for box 0x{0:X} text \"{1}\". Shorten some lines.", bx.Table, sg.Text));
            }

        // Write: clear the freed regions to 0xFF padding, then place the English and fix pointers.
        foreach (var rg in regions) for (int p = rg[0]; p < rg[1]; p++) b[p] = 0xFF;
        foreach (var l in lines.Where(l => l.Pinned)) for (int p = l.Off; p < l.SlotEnd; p++) b[p] = 0x00;
        foreach (var l in lines)
        {
            Buffer.BlockCopy(l.Bytes, 0, b, l.NewOff, l.Bytes.Length);
            if (l.NewOff != l.Off) foreach (int p in l.Ptrs) P32(b, p, Base + (uint)l.NewOff);
            Console.WriteLine("0x{0:X5} {1,-12} -> 0x{2:X5} {3,-12} ({4} bytes, {5})", l.Off, l.Ja, l.NewOff, l.En, l.Bytes.Length - 1,
                l.Pinned ? "fixed table slot, kept in place" : l.Ptrs.Count == 0 ? "no pointer, kept in place" : "pointers at " + string.Join(",", l.Ptrs.Select(p => "0x" + p.ToString("X5"))));
        }
        foreach (var bx in boxes)
        {
            int maxLen = bx.Lines.Max(l => l.Sum(s => s.Text.Length));
            int k = 0;
            for (int li = 0; li < bx.Lines.Count; li++)
            {
                var segs = bx.Lines[li];
                int len = segs.Sum(s => s.Text.Length);
                int x = bx.Left ? 0 : (maxLen - len) * Advance / 2;
                for (int si = 0; si < segs.Count; si++, k++)
                {
                    var sg = segs[si];
                    int o = bx.Table + 12 * k;
                    Buffer.BlockCopy(sg.Bytes, 0, b, sg.NewOff, sg.Bytes.Length);
                    P16(b, o, si == 0 ? sg.Style : 0xC000 | sg.Style);
                    P16(b, o + 2, sg.Flags);
                    P16(b, o + 4, si == 0 ? x : 0);
                    P16(b, o + 6, si == 0 ? li * LineHeight : 0);
                    P32(b, o + 8, Base + (uint)sg.NewOff);
                }
            }
            int t = bx.Table + 12 * k;
            P16(b, t, 0xFFFF); for (int z = 2; z < 12; z++) b[t + z] = 0;
            Console.WriteLine("box 0x{0:X5}: {1} lines, {2} of {3} entries used: {4}", bx.Table, bx.Lines.Count, k, bx.Records,
                string.Join(" / ", bx.Lines.Select(l => string.Concat(l.Select(s => s.Text)))));
        }
        int free = regions.Sum(r => r[1] - r[0]) - lines.Where(l => !l.Pinned).Sum(l => l.Bytes.Length) - boxes.SelectMany(x => x.Lines).SelectMany(x => x).Sum(s => s.Bytes.Length);
        Console.WriteLine("{0} lines and {1} boxes in {2} regions; {3} bytes still free in those regions", lines.Count, boxes.Count, regions.Count, free);

        // Other files (e.g. code overlays) that point at a line we moved would now show the wrong text.
        for (int i = 3; i < a.Count; i++)
        {
            byte[] o = File.ReadAllBytes(a[i]);
            foreach (var l in lines.Where(l => l.NewOff != l.Off))
                for (int p = 0; p + 4 <= o.Length; p += 2)
                    if (W32(o, p) == Base + (uint)l.Off)
                        Console.WriteLine("WARNING: {0} @0x{1:X} also points at \"{2}\"", Path.GetFileName(a[i]), p, l.Ja);
        }
        File.WriteAllBytes(a[2], b);
    }
}
