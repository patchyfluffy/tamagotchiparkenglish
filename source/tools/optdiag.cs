// Measures the options-screen background: where the checkerboard edges are and where the
// radial glow is centered. Only looks at pixels outside the two character areas.
// Usage: optdiag <image.unp> <paletteFile> <paletteOffsetHex>
using System;
using System.IO;
using System.Linq;

class OptDiag
{
    static bool InBoxes(int x, int y) { return (x >= 30 && x <= 126 && y >= 55 && y <= 176) || (x >= 180 && x <= 310 && y >= 58 && y <= 170); }

    static void Main(string[] a)
    {
        byte[] img = File.ReadAllBytes(a[0]);
        byte[] pf = File.ReadAllBytes(a[1]);
        int po = Convert.ToInt32(a[2], 16);
        var lumOf = new double[256];
        for (int i = 0; i < 256; i++)
        {
            int w = pf[po + 2 * i] << 8 | pf[po + 2 * i + 1];
            lumOf[i] = 0.299 * (w & 31) * 255 / 31 + 0.587 * ((w >> 5) & 31) * 255 / 31 + 0.114 * ((w >> 10) & 31) * 255 / 31;
        }
        Func<int, int, double> L = (x, y) => lumOf[img[y * 320 + x]];

        Console.WriteLine("Row edge strength (mean |lum(y) - lum(y-1)|), strongest first:");
        var rows = Enumerable.Range(1, 223).Select(y =>
        {
            double s = 0; int n = 0;
            for (int x = 0; x < 320; x++) if (!InBoxes(x, y) && !InBoxes(x, y - 1)) { s += Math.Abs(L(x, y) - L(x, y - 1)); n++; }
            return Tuple.Create(y, n > 0 ? s / n : 0);
        }).OrderByDescending(t => t.Item2).Take(8);
        foreach (var t in rows) Console.WriteLine("  y={0,3}  {1:F1}", t.Item1, t.Item2);

        Console.WriteLine("Column edge strength, strongest first:");
        var cols = Enumerable.Range(1, 319).Select(x =>
        {
            double s = 0; int n = 0;
            for (int y = 0; y < 224; y++) if (!InBoxes(x, y) && !InBoxes(x - 1, y)) { s += Math.Abs(L(x, y) - L(x - 1, y)); n++; }
            return Tuple.Create(x, n > 0 ? s / n : 0);
        }).OrderByDescending(t => t.Item2).Take(8);
        foreach (var t in cols) Console.WriteLine("  x={0,3}  {1:F1}", t.Item1, t.Item2);
    }
}
