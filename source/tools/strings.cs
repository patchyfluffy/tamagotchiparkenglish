// Lists the game's text lines in 0EXEC.BIN, decoded, with each line's slot size and the places in
// the program that point to it.
//
// Text encoding (found with relsearch):
//   0x20-0x5F  ASCII (font characters 0x00-0x3F); '@' (0x40) toggles katakana for the letters after it
//   0xA0-0xDF  kana in JIS X 0201 order (font 0x80-0xBF as hiragana, 0x40-0x7F as katakana after '@')
//   0x00       end of line; lines are padded with 0xFF to a multiple of 4 bytes
// Pointers: the program loads at 0x06010000, so a line at file offset X is referenced as 0x06010000+X.
//
// Usage: strings <0EXEC.BIN> <out.tsv> [minLetters] [loadAddressHex] [ptr]
//   e.g. for a game overlay:  strings MINI00.unp out.tsv 0 00280000 ptr
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class Strings
{
    static uint Base = 0x06010000;

    public static string Decode(byte[] b, int start, int end)
    {
        var sb = new StringBuilder(); bool kata = false;
        for (int p = start; p < end; p++)
        {
            int c = b[p];
            if (c == 0x40) { kata = !kata; continue; }
            if (c >= 0x20 && c <= 0x5F) sb.Append((char)c);
            else if (c >= 0xA1 && c <= 0xDF) sb.Append((kata ? RelSearchTables.Kata : RelSearchTables.Hira)[c - 0xA1]);
            else sb.Append("{" + c.ToString("X2") + "}");
        }
        // Join base letters with their voicing marks for readability (が instead of か゛).
        return sb.ToString().Replace("゛", "゙").Replace("゜", "゚").Normalize(NormalizationForm.FormC).Replace("゙", "゛").Replace("゚", "゜");
    }

    static bool IsText(int c) { return (c >= 0x20 && c <= 0x5F) || (c >= 0xA1 && c <= 0xDF); }

    static void Main(string[] a)
    {
        byte[] b = File.ReadAllBytes(a[0]);
        int minLetters = a.Length > 2 ? int.Parse(a[2]) : 2;
        // Optional: the file's load address (game overlays load at 0x00280000), and "ptr" to list only
        // lines the code points to (overlay text is chopped into tiny pieces, so letter counts don't help).
        if (a.Length > 3) Base = Convert.ToUInt32(a[3].Replace("0x", ""), 16);
        bool onlyPointed = a.Length > 4 && a[4] == "ptr";
        // Index every aligned 32-bit value that looks like a pointer into the program.
        var refs = new Dictionary<uint, List<int>>();
        for (int p = 0; p + 4 <= b.Length; p += 2)
        {
            uint v = (uint)(b[p] << 24 | b[p + 1] << 16 | b[p + 2] << 8 | b[p + 3]);
            if (v < Base || v >= Base + b.Length) continue;
            List<int> l; if (!refs.TryGetValue(v, out l)) refs[v] = l = new List<int>();
            l.Add(p);
        }
        var o = new StringBuilder();
        o.AppendLine("offset\taddress\tbytes\tslot\tpointers\ttext");
        int count = 0;
        for (int p = 0; p < b.Length; p++)
        {
            if (!IsText(b[p]) || (p > 0 && IsText(b[p - 1]))) continue;
            int e = p; int kana = 0;
            while (e < b.Length && IsText(b[e])) { if (b[e] >= 0xA1) kana++; e++; }
            if (e >= b.Length || b[e] != 0x00 || kana < minLetters) continue;
            // Slot: the line, its 00, and any 0xFF padding after it.
            int slotEnd = e + 1;
            while (slotEnd < b.Length && b[slotEnd] == 0xFF) slotEnd++;
            uint addr = Base + (uint)p;
            List<int> r; refs.TryGetValue(addr, out r);
            if (onlyPointed && r == null) continue;
            string ptrs = r == null ? "-" : string.Join(",", r.ConvertAll(x => "0x" + x.ToString("X5")));
            o.AppendLine(string.Format("0x{0:X5}\t0x{1:X8}\t{2}\t{3}\t{4}\t{5}", p, addr, e - p, slotEnd - p, ptrs, Decode(b, p, e)));
            count++;
        }
        File.WriteAllText(a[1], o.ToString(), new UTF8Encoding(true));
        Console.WriteLine("{0} text lines written to {1}", count, a[1]);
    }
}

static class RelSearchTables
{
    public const string Kata = "。「」、・ヲァィゥェォャュョッーアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン゛゜";
    public const string Hira = "。「」、・をぁぃぅぇぉゃゅょっーあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわん゛゜";
}
