// Redraws characters in the game's font (USER.BIN: normal font at 0x0000, bold copy at 0x7000;
// 16x12 pixels, 4 bits per pixel, ink = 0xF) from a text file of '#'/'.' pictures.
// File format: "glyph <character> <normal|bold>" followed by exactly 12 rows of 16 '#'/'.' characters.
// Outside a glyph block, lines starting with '#' are comments.
// Usage: fontpatch <USER.BIN in> <font-fixes.txt> <USER.BIN out>
using System;
using System.IO;
using System.Linq;
using System.Text;

class FontPatch
{
    const int CW = 16, CH = 12, Bytes = CW * CH / 2;

    static void Main(string[] a)
    {
        byte[] b = File.ReadAllBytes(a[0]);
        var lines = File.ReadAllLines(a[1], Encoding.UTF8).Select(l => l.Trim()).ToList();
        int count = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            string l = lines[i];
            if (l.Length == 0 || (l.StartsWith("#") && !l.StartsWith("glyph"))) continue;
            if (!l.StartsWith("glyph ")) throw new Exception("line " + (i + 1) + ": expected 'glyph <char> <normal|bold>', got: " + l);
            var f = l.Split(' ');
            char c = f[1][0];
            if (f[1].Length != 1 || c < 0x20 || c > 0x5F || c == '@') throw new Exception("can only redraw ASCII characters 0x20-0x5F: " + l);
            int baseOff = f[2] == "normal" ? 0 : f[2] == "bold" ? 0x7000 : -1;
            if (baseOff < 0) throw new Exception("font must be 'normal' or 'bold': " + l);
            int off = baseOff + (c - 0x20) * Bytes;
            // The 12 picture rows follow immediately (they may start with '#', so they're never comments).
            for (int y = 0; y < CH; y++)
            {
                string row = i + 1 + y < lines.Count ? lines[i + 1 + y] : "";
                if (row.Length != CW || row.Any(ch => ch != '#' && ch != '.')) throw new Exception(string.Format("glyph {0} {1}, row {2}: need 16 '#'/'.' characters, got \"{3}\"", c, f[2], y, row));
                for (int x = 0; x < CW; x += 2)
                    b[off + (y * CW + x) / 2] = (byte)((row[x] == '#' ? 0xF0 : 0) | (row[x + 1] == '#' ? 0x0F : 0));
            }
            i += CH;
            count++;
        }
        File.WriteAllBytes(a[2], b);
        Console.WriteLine("Redrew {0} characters", count);
    }
}
