// Draws a run of equal-sized 4bpp sprites (stored one after another, rows left to right) as a
// labeled grid, to look at fonts and small text pieces.
// Usage: glyphs <file> <offsetHex> <count> <cellW> <cellH> <perRow> <out.png> [scale]
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

class Glyphs
{
    static void Main(string[] a)
    {
        byte[] b = File.ReadAllBytes(a[0]);
        int off = Convert.ToInt32(a[1], 16), count = int.Parse(a[2]), cw = int.Parse(a[3]), ch = int.Parse(a[4]), per = int.Parse(a[5]);
        int scale = a.Length > 7 ? int.Parse(a[7]) : 2;
        int bytesPer = cw * ch / 2, rows = (count + per - 1) / per, label = 40, gap = 2;
        using (var bmp = new Bitmap(label + per * (cw * scale + gap), rows * (ch * scale + gap)))
        using (var g = Graphics.FromImage(bmp))
        using (var font = new Font("Consolas", 8))
        {
            g.Clear(Color.FromArgb(40, 40, 60));
            for (int r = 0; r < rows; r++)
                g.DrawString((r * per).ToString("X3"), font, Brushes.Yellow, 2, r * (ch * scale + gap) + 2);
            for (int i = 0; i < count; i++)
            {
                int gx = label + (i % per) * (cw * scale + gap), gy = (i / per) * (ch * scale + gap);
                int start = off + i * bytesPer;
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        int p = start + (y * cw + x) / 2;
                        if (p >= b.Length) continue;
                        int v = (x & 1) == 0 ? b[p] >> 4 : b[p] & 15;
                        var c = v == 0 ? Color.Black : Color.FromArgb(255, 255 - v * 6, 255 - v * 12);
                        for (int sy = 0; sy < scale; sy++) for (int sx = 0; sx < scale; sx++) bmp.SetPixel(gx + x * scale + sx, gy + y * scale + sy, c);
                    }
            }
            bmp.Save(a[6], ImageFormat.Png);
        }
        Console.WriteLine("wrote " + a[6]);
    }
}
