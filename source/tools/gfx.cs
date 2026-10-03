// Renders raw bytes as images to eyeball graphics.
// Usage: gfx <in> <out.png> <fmt> <widthPx> [offsetHex] [lengthHex] [scale] [--pal <file> <offsetHex>]
//   fmt: 4t = 4bpp 8x8 tiles, 8t = 8bpp 8x8 tiles, 4l = 4bpp linear, 8l = 8bpp linear, 1l = 1bpp linear
//   --pal: color with a Saturn RGB555 palette (big-endian 0bbbbbgggggrrrrr) instead of grayscale
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

class Gfx
{
    public static Color[] LoadPalette(string file, int offset, int count)
    {
        byte[] p = File.ReadAllBytes(file);
        var c = new Color[count];
        for (int i = 0; i < count; i++)
        {
            int w = p[offset + i * 2] << 8 | p[offset + i * 2 + 1];
            int r = w & 31, g = (w >> 5) & 31, b = (w >> 10) & 31;
            c[i] = Color.FromArgb(r << 3 | r >> 2, g << 3 | g >> 2, b << 3 | b >> 2);
        }
        return c;
    }

    static void Main(string[] argv)
    {
        var a = new List<string>(argv);
        Color[] pal = null;
        int pi = a.IndexOf("--pal");
        if (pi >= 0)
        {
            pal = LoadPalette(a[pi + 1], Convert.ToInt32(a[pi + 2], 16), 256);
            a.RemoveRange(pi, 3);
        }
        byte[] all = File.ReadAllBytes(a[0]);
        string fmt = a[2];
        int width = int.Parse(a[3]);
        int off = a.Count > 4 ? Convert.ToInt32(a[4], 16) : 0;
        int len = a.Count > 5 ? Convert.ToInt32(a[5], 16) : all.Length - off;
        int scale = a.Count > 6 ? int.Parse(a[6]) : 1;
        len = Math.Min(len, all.Length - off);
        int bpp = fmt[0] == '4' ? 4 : fmt[0] == '8' ? 8 : 1;
        bool tiled = fmt[1] == 't';
        long pixels = (long)len * 8 / bpp;
        int height = (int)((pixels + width - 1) / width);
        if (tiled) height = (height + 7) / 8 * 8;
        int max = (1 << bpp) - 1;
        var bmp = new Bitmap(width * scale, height * scale, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.FromArgb(255, 0, 255));
        Func<long, int> pix = i =>
        {
            long bit = i * bpp;
            long bi = off + bit / 8;
            if (bi >= off + len) return -1;
            int b = all[bi];
            if (bpp == 8) return b;
            if (bpp == 4) return (bit % 8 == 0) ? b >> 4 : b & 0xF;
            return (b >> (7 - (int)(bit % 8))) & 1;
        };
        int tilesPerRow = width / 8;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                long i;
                if (tiled)
                {
                    int tile = (y / 8) * tilesPerRow + x / 8;
                    i = (long)tile * 64 + (y % 8) * 8 + (x % 8);
                }
                else i = (long)y * width + x;
                int v = pix(i);
                if (v < 0) continue;
                Color c;
                if (pal != null) c = pal[v];
                else { int gray = v * 255 / max; c = Color.FromArgb(gray, gray, gray); }
                for (int sy = 0; sy < scale; sy++)
                    for (int sx = 0; sx < scale; sx++)
                        bmp.SetPixel(x * scale + sx, y * scale + sy, c);
            }
        bmp.Save(a[1], ImageFormat.Png);
        Console.WriteLine("{0}: {1}x{2}", Path.GetFileName(a[1]), width * scale, height * scale);
    }
}
