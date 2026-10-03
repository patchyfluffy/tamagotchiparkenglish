// Finds text in an unknown encoding by its pattern of repeated letters.
// For a known phrase like "このままゲ〜ムを…", any run of bytes (or 16-bit words) where the same
// letters line up with the same codes, and different letters with different codes, is a match,
// whatever the actual codes are. Each match also reveals part of the game's letter-code table.
// Phrases are tried as written and with voiced kana split into base + ゛ (some games store them that way).
//
// Usage: patsearch <phrasesFile (UTF-8, one per line)> <dir> [dir...]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class PatSearch
{
    static void Main(string[] a)
    {
        var phrases = File.ReadAllLines(a[0], Encoding.UTF8).Where(l => l.Trim().Length > 0).ToList();
        var files = new List<string>();
        for (int i = 1; i < a.Length; i++) files.AddRange(Directory.GetFiles(a[i], "*", SearchOption.AllDirectories));
        var outLines = new List<string>();
        foreach (string phrase in phrases)
        {
            var variants = new List<string> { phrase };
            string split = phrase.Normalize(NormalizationForm.FormD).Replace('゙', '゛').Replace('゚', '゜');
            if (split != phrase) variants.Add(split);
            foreach (string v in variants)
                foreach (int unit in new[] { 1, 2 })
                    foreach (string f in files)
                    {
                        byte[] b = File.ReadAllBytes(f);
                        int n = v.Length;
                        for (int s = 0; s + n * unit <= b.Length; s++)
                        {
                            var map = new Dictionary<char, int>();
                            var back = new Dictionary<int, char>();
                            bool ok = true;
                            for (int i = 0; i < n && ok; i++)
                            {
                                int code = unit == 1 ? b[s + i] : b[s + 2 * i] << 8 | b[s + 2 * i + 1];
                                int have; char who;
                                if (map.TryGetValue(v[i], out have)) ok = have == code;
                                else if (back.TryGetValue(code, out who)) ok = false;
                                else { map[v[i]] = code; back[code] = v[i]; }
                            }
                            if (!ok) continue;
                            // Reject runs where the codes are spread wildly (random data); real text codes cluster.
                            string table = string.Join(" ", v.Distinct().Select(c => c + "=" + map[c].ToString(unit == 1 ? "X2" : "X4")));
                            outLines.Add(string.Format("{0} @0x{1:X6} ({2}-byte codes, {3}): {4}", Path.GetFileName(f), s, unit,
                                v == phrase ? "as written" : "voiced split", table));
                        }
                    }
        }
        File.WriteAllLines(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(a[0])), "patsearch-results.txt"), outLines, Encoding.UTF8);
        Console.WriteLine("{0} matches (written to patsearch-results.txt)", outLines.Count);
    }
}
