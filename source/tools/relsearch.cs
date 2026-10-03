// "Relative search": finds text whose letter codes follow the font's letter order, at any offset.
//
// The font in USER.BIN (16x12, 192 characters) is laid out as:
//   0x00-0x3F  ASCII 0x20-0x5F (space, punctuation, digits, A-Z)
//   0x40-0x7F  katakana in JIS X 0201 order (。「」、・ヲァィ…ワン゛゜)
//   0x80-0xBF  hiragana in the same order
// Voiced kana (が) are a base letter plus a separate ゛. For each phrase we look for bytes (or
// 16-bit words) where code - fontIndex is the same for every letter, trying ゛ after and before
// the base letter. Each hit is printed with the surrounding text decoded using that offset.
//
// Usage: relsearch <phrasesFile (UTF-8)> <dir> [dir...]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class RelSearch
{
    public const string Kata = "。「」、・ヲァィゥェォャュョッーアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン゛゜";
    public const string Hira = "。「」、・をぁぃぅぇぉゃゅょっーあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわん゛゜";

    // Font indexes for a phrase; null if it uses a letter the font doesn't have.
    public static List<int> ToFont(string phrase, bool markFirst)
    {
        var o = new List<int>();
        foreach (char c0 in phrase.Normalize(NormalizationForm.FormD))
        {
            char c = c0;
            if (c == '゙' || c == '゚')
            {
                // Same half (hiragana/katakana) as the letter it marks (NFD puts the base letter first).
                bool hira = o[o.Count - 1] >= 0x80;
                int mark = (c == '゙' ? 0x3E : 0x3F) + (hira ? 0x80 : 0x40);
                if (markFirst) { o.Insert(o.Count - 1, mark); } else o.Add(mark);
                continue;
            }
            int i;
            if (c >= 0x20 && c <= 0x5F) o.Add(c - 0x20);
            else if ((i = Kata.IndexOf(c)) >= 0) o.Add(0x41 + i);
            else if ((i = Hira.IndexOf(c)) >= 0) o.Add(0x81 + i);
            else return null;
        }
        return o;
    }

    public static string FromFont(int g)
    {
        if (g >= 0x00 && g <= 0x3F) return ((char)(g + 0x20)).ToString();
        if (g >= 0x41 && g <= 0x7F) return Kata[g - 0x41].ToString();
        if (g >= 0x81 && g <= 0xBF) return Hira[g - 0x81].ToString();
        return "{" + g.ToString("X2") + "}";
    }

    static void Main(string[] a)
    {
        var phrases = File.ReadAllLines(a[0], Encoding.UTF8).Where(l => l.Trim().Length > 0).ToList();
        var files = new List<string>();
        for (int i = 1; i < a.Length; i++) files.AddRange(Directory.GetFiles(a[i], "*", SearchOption.AllDirectories));
        var cache = files.ToDictionary(f => f, File.ReadAllBytes);
        var sb = new StringBuilder();
        int hits = 0;
        foreach (string phrase in phrases)
            foreach (bool markFirst in new[] { false, true })
            {
                var g = ToFont(phrase, markFirst);
                if (g == null) { sb.AppendLine("(skipped, letter not in font) " + phrase); break; }
                if (markFirst && !phrase.Normalize(NormalizationForm.FormD).Any(c => c == '゙' || c == '゚')) continue;
                foreach (int unit in new[] { 1, 2 })
                {
                    int mod = unit == 1 ? 0x100 : 0x10000;
                    foreach (var kv in cache)
                    {
                        byte[] b = kv.Value; int n = g.Count;
                        Func<int, int> V = p => unit == 1 ? b[p] : b[p] << 8 | b[p + 1];
                        for (int s = 0; s + n * unit <= b.Length; s++)
                        {
                            int k = ((V(s) - g[0]) % mod + mod) % mod, i = 1;
                            while (i < n && ((V(s + i * unit) - g[i]) % mod + mod) % mod == k) i++;
                            if (i < n) continue;
                            hits++;
                            // Decode around the hit with the same offset.
                            var ctx = new StringBuilder();
                            for (int p = Math.Max(0, s - 24 * unit); p + unit <= Math.Min(b.Length, s + (n + 60) * unit); p += unit)
                            {
                                if (p == s) ctx.Append("【");
                                int code = V(p), gl = ((code - k) % mod + mod) % mod;
                                ctx.Append(gl <= 0xBF ? FromFont(gl) : "{" + code.ToString(unit == 1 ? "X2" : "X4") + "}");
                                if (p == s + (n - 1) * unit) ctx.Append("】");
                            }
                            sb.AppendLine(string.Format("{0} @0x{1:X6}  {2}-byte codes, code = font index + 0x{3:X}, ゛ {4}:", Path.GetFileName(kv.Key), s, unit, k, markFirst ? "before" : "after"));
                            sb.AppendLine("   " + ctx);
                        }
                    }
                }
            }
        string outFile = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(a[0])), "relsearch-results.txt");
        File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
        Console.WriteLine("{0} hits, written to {1}", hits, outFile);
    }
}
