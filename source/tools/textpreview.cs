// Previews text with the game's own font at its on-screen positions (320x224), to check that
// English fits before building. Letters are drawn 13 pixels apart like the game does, with the
// bold font underneath for the outline.
//
// Usage: textpreview <USER.BIN> <out.png> <item> [item...]
//   item = "L|x|y|text"   text starting at x (left edge), y (top), outlined style (options menu)
//          "C|x|y|text"   text centered on x,y (with a guide box around it), outlined style
//          "l|x|y|text"   / "c|x|y|text": same, but dark bold letters only (the boot warning's style)
//          "B|x|y|w|h"    a box outline (e.g. the game's fixed message box)
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

class TextPreview
{
    const int Advance = 13, CW = 16, CH = 12, Scale = 3;
    static byte[] font;

    static void Glyph(Bitmap bmp, int fontOff, int idx, int x0, int y0, Color col)
    {
        for (int y = 0; y < CH; y++)
            for (int x = 0; x < CW; x++)
            {
                int b = font[fontOff + idx * 96 + (y * CW + x) / 2];
                int v = (x & 1) == 0 ? b >> 4 : b & 15;
                int px = x0 + x, py = y0 + y;
                if (v == 0 || px < 0 || py < 0 || px >= 320 || py >= 224) continue;
                for (int sy = 0; sy < Scale; sy++) for (int sx = 0; sx < Scale; sx++) bmp.SetPixel(px * Scale + sx, py * Scale + sy, col);
            }
    }

    static void Text(Bitmap bmp, string s, int x, int y, bool darkOnly)
    {
        for (int pass = 0; pass < (darkOnly ? 1 : 2); pass++)
            for (int i = 0; i < s.Length; i++)
            {
                int c = s[i];
                if (c < 0x20 || c > 0x5F) throw new Exception("not in font: " + s[i]);
                // The bold copy (0x7000) is drawn first in dark, then the normal font in white on top.
                Glyph(bmp, pass == 0 ? 0x7000 : 0, c - 0x20, x + i * Advance, y, pass == 0 ? Color.FromArgb(40, 30, 20) : Color.White);
            }
    }

    static void Rect(Graphics g, int x, int y, int w, int h, Color c)
    {
        using (var pen = new Pen(c, 2)) g.DrawRectangle(pen, x * Scale, y * Scale, w * Scale, h * Scale);
    }

    static void Main(string[] a)
    {
        font = File.ReadAllBytes(a[0]);
        using (var bmp = new Bitmap(320 * Scale, 224 * Scale))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(120, 150, 200));
            using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255))) g.DrawRectangle(pen, 0, 0, 320 * Scale - 1, 224 * Scale - 1);
            for (int i = 2; i < a.Length; i++)
            {
                var f = a[i].Split(new[] { '|' }, 4);
                int x = int.Parse(f[1]), y = int.Parse(f[2]);
                bool dark = f[0] == "l" || f[0] == "c";
                if (f[0] == "B")
                {
                    var wh = f[3].Split('|');
                    using (var fill = new SolidBrush(Color.FromArgb(240, 236, 170))) g.FillRectangle(fill, x * Scale, y * Scale, int.Parse(wh[0]) * Scale, int.Parse(wh[1]) * Scale);
                    Rect(g, x, y, int.Parse(wh[0]), int.Parse(wh[1]), Color.FromArgb(120, 90, 40));
                }
                else if (f[0] == "L" || f[0] == "l") Text(bmp, f[3], x, y, dark);
                else
                {
                    int w = f[3].Length * Advance;
                    if (dark) using (var fill = new SolidBrush(Color.FromArgb(240, 236, 170))) g.FillRectangle(fill, (x - w / 2 - 4) * Scale, (y - CH / 2 - 4) * Scale, (w + 8) * Scale, (CH + 8) * Scale);
                    Rect(g, x - w / 2 - 4, y - CH / 2 - 4, w + 8, CH + 8, Color.FromArgb(120, 90, 40));
                    Text(bmp, f[3], x - w / 2, y - CH / 2, dark);
                }
            }
            bmp.Save(a[1], ImageFormat.Png);
        }
        Console.WriteLine("wrote " + a[1]);
    }
}
