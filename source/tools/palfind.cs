// Looks for 256-color palettes whose brightness rises steadily with the color index.
// The options screen's pixel values behave as if the palette is sorted dark-to-light, so such
// a run is a strong palette candidate. Checks both Saturn color formats:
//   RGB555 (2 bytes per color, CRAM mode 0/1) and RGB888 (4 bytes per color, CRAM mode 2).
// Usage: palfind <dir> [minScore]
using System;
using System.IO;

class PalFind
{
    static double Lum555(int w) { int r = w & 31, g = (w >> 5) & 31, b = (w >> 10) & 31; return 0.3 * r + 0.59 * g + 0.11 * b; }
    static double Lum888(uint v) { uint r = v & 0xFF, g = (v >> 8) & 0xFF, b = (v >> 16) & 0xFF; return (0.3 * r + 0.59 * g + 0.11 * b) / 8.0; }

    static void Main(string[] a)
    {
        int minScore = a.Length > 1 ? int.Parse(a[1]) : 200;
        foreach (var f in Directory.GetFiles(a[0], "*", SearchOption.AllDirectories))
        {
            byte[] b = File.ReadAllBytes(f);
            Scan(f, b, 2, minScore);
            Scan(f, b, 4, minScore);
        }
    }

    static void Scan(string f, byte[] b, int size, int minScore)
    {
        int n = b.Length / size;
        if (n < 256) return;
        var lum = new double[n];
        for (int i = 0; i < n; i++)
        {
            int o = i * size;
            if (size == 2) lum[i] = Lum555(b[o] << 8 | b[o + 1]);
            else lum[i] = Lum888((uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]) & 0xFFFFFF);
        }
        // A pair "rises" if brightness doesn't drop and the color isn't identical (rules out runs of one value).
        int best = -1, bestAt = 0;
        for (int start = 0; start + 256 <= n; start++)
        {
            int score = 0;
            for (int i = start + 1; i < start + 256; i++) if (lum[i] >= lum[i - 1] - 0.5 && lum[i] != lum[i - 1]) score++;
            if (score > best) { best = score; bestAt = start; }
            if (score >= minScore)
            {
                Console.WriteLine("{0,-40} off 0x{1:X6}  {2}-byte  score {3}/255  first lum {4:F1} last lum {5:F1}",
                    Path.GetFileName(f), start * size, size, score, lum[start], lum[start + 255]);
                start += 255;
            }
        }
    }
}
