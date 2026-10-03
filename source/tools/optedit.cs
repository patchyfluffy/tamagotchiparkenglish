// Repaints the tiled background word on the options screen (USER.BIN block @0x14000).
//
// The screen is a 4x3 checkerboard with a radial glow; the word "おぷしょん" is stamped in a
// semi-transparent orange tint in three rows, partly hidden behind two characters. This tool:
//   1. finds the rows holding the word,
//   2. predicts the background color anywhere from (square color, distance to the glow center),
//   3. learns the word's tint (text = k*background + m, per color channel and square color),
//   4. marks character pixels as off-limits: any color that isn't background mixed with the tint,
//   5. erases the old word by filling it in from the surrounding background (per checker cell),
//   6. draws the new word with the same tint where each old word was, and
//   7. maps the colors back onto the screen's own palette with a 2x2 ordered dither.
// Previews only show the word rows, with character pixels blanked out.
//
// Usage: optedit <in.unp> <paletteFile> <paletteOffsetHex> <out.unp> <previewDir> <text> [fontName]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

class OptEdit
{
    const int W = 320, H = 224;
    // Checker edges measured with optdiag: columns at 80/160/240, rows at 76/156.
    static int Cell(int x, int y) { return (y < 76 ? 0 : y < 156 ? 1 : 2) * 4 + x / 80; }
    static int Parity(int x, int y) { int c = Cell(x, y); return (c / 4 + c % 4) & 1; }
    // Generous rectangles around the two characters and their speech bubbles.
    static bool InBoxes(int x, int y) { return (x >= 12 && x <= 126 && y >= 52 && y <= 176) || (x >= 180 && x <= 310 && y >= 56 && y <= 170); }

    static double[,] pal = new double[256, 3];
    static double Lum(double r, double g, double b) { return 0.299 * r + 0.587 * g + 0.114 * b; }
    static double LumI(int i) { return Lum(pal[i, 0], pal[i, 1], pal[i, 2]); }

    // Distance from color c to the line segment b..t, and how far along it the closest point is (0 = b, 1 = t).
    static double SegDist(double[] c, double[] b, double[] t, out double along)
    {
        double num = 0, den = 0;
        for (int ch = 0; ch < 3; ch++) { double d = t[ch] - b[ch]; num += (c[ch] - b[ch]) * d; den += d * d; }
        along = den > 0 ? Math.Max(0, Math.Min(1, num / den)) : 0;
        double s = 0;
        for (int ch = 0; ch < 3; ch++) { double e = c[ch] - (b[ch] + along * (t[ch] - b[ch])); s += e * e; }
        return Math.Sqrt(s);
    }

    static void Main(string[] a)
    {
        byte[] img = File.ReadAllBytes(a[0]);
        byte[] pf = File.ReadAllBytes(a[1]);
        int po = Convert.ToInt32(a[2], 16);
        string outPath = a[3], previewDir = a[4], text = a[5];
        string fontName = a.Length > 6 ? a[6] : "Arial Rounded MT Bold";
        for (int i = 0; i < 256; i++)
        {
            int w = pf[po + 2 * i] << 8 | pf[po + 2 * i + 1];
            pal[i, 0] = (w & 31) * 255.0 / 31; pal[i, 1] = ((w >> 5) & 31) * 255.0 / 31; pal[i, 2] = ((w >> 10) & 31) * 255.0 / 31;
        }
        Func<int, int, int> I = (x, y) => img[y * W + x];
        Func<int, int, double> L = (x, y) => LumI(img[y * W + x]);
        Func<int, double[]> Col = i => new[] { pal[i, 0], pal[i, 1], pal[i, 2] };

        // --- Radial glow center: the point that makes brightness most constant along circles.
        double bestVar = double.MaxValue; int cx = 160, cy = 112;
        for (int tx = 150; tx <= 170; tx++)
            for (int ty = 100; ty <= 126; ty++)
            {
                var s = new double[2, 260]; var s2 = new double[2, 260]; var n = new int[2, 260];
                for (int y = 0; y < H; y += 2)
                    for (int x = 0; x < W; x += 2)
                    {
                        if (InBoxes(x, y)) continue;
                        int r = (int)Math.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty)), p = Parity(x, y);
                        double l = L(x, y); s[p, r] += l; s2[p, r] += l * l; n[p, r]++;
                    }
                double v = 0;
                for (int p = 0; p < 2; p++) for (int r = 0; r < 260; r++) if (n[p, r] > 1) v += s2[p, r] - s[p, r] * s[p, r] / n[p, r];
                if (v < bestVar) { bestVar = v; cx = tx; cy = ty; }
            }
        Console.WriteLine("Glow center: ({0},{1})", cx, cy);
        Func<int, int, int> Rad = (x, y) => (int)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        // --- 1. Which rows hold the word: pixels brighter than a grayscale "opening" (same cell).
        // Only reliable on the brown squares (on green the word differs mostly in hue), which is
        // enough to find the rows.
        const int Rw = 4; // 9x9 window
        var ero = new double[W * H]; var open = new double[W * H];
        for (int pass = 0; pass < 2; pass++)
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int c = Cell(x, y);
                    double v = pass == 0 ? double.MaxValue : double.MinValue;
                    for (int dy = -Rw; dy <= Rw; dy++)
                        for (int dx = -Rw; dx <= Rw; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= W || ny < 0 || ny >= H || Cell(nx, ny) != c) continue;
                            if (pass == 0) v = Math.Min(v, L(nx, ny)); else v = Math.Max(v, ero[ny * W + nx]);
                        }
                    if (pass == 0) ero[y * W + x] = v; else open[y * W + x] = v;
                }
        var rowCount = new int[H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (!InBoxes(x, y) && L(x, y) - open[y * W + x] > 12) rowCount[y]++;
        var bands = new List<int[]>();
        for (int y = 0; y < H; y++)
        {
            if (rowCount[y] < 3) continue;
            if (bands.Count > 0 && y - bands[bands.Count - 1][1] <= 3) bands[bands.Count - 1][1] = y;
            else bands.Add(new[] { y, y });
        }
        bands = bands.Where(b => b[1] - b[0] >= 8).ToList();
        Console.WriteLine("Word rows: " + string.Join(", ", bands.Select(b => b[0] + "-" + b[1])));
        Func<int, bool> nearWordRow = y => bands.Any(b => y >= b[0] - 3 && y <= b[1] + 3);

        // --- 2. Background prediction: median color of each (square color, distance) ring,
        // from rows with no words, outside the character areas.
        var ring = new List<double>[2, 260, 3];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (nearWordRow(y) || InBoxes(x, y)) continue;
                int p = Parity(x, y), r = Rad(x, y);
                for (int ch = 0; ch < 3; ch++) { if (ring[p, r, ch] == null) ring[p, r, ch] = new List<double>(); ring[p, r, ch].Add(pal[I(x, y), ch]); }
            }
        var model = new double[2, 260][];
        for (int p = 0; p < 2; p++)
            for (int r = 0; r < 260; r++)
            {
                // Use the nearest ring with enough samples.
                int rr = -1;
                for (int d = 0; d < 260 && rr < 0; d++)
                {
                    if (r - d >= 0 && ring[p, r - d, 0] != null && ring[p, r - d, 0].Count >= 5) rr = r - d;
                    else if (r + d < 260 && ring[p, r + d, 0] != null && ring[p, r + d, 0].Count >= 5) rr = r + d;
                }
                model[p, r] = new double[3];
                for (int ch = 0; ch < 3; ch++) { var l = ring[p, rr, ch]; l.Sort(); model[p, r][ch] = l[l.Count / 2]; }
            }
        Func<int, int, double[]> Bg = (x, y) => model[Parity(x, y), Rad(x, y)];
        Func<int, int, double> dev = (x, y) =>
        {
            double[] b = Bg(x, y); int i = I(x, y); double s = 0;
            for (int ch = 0; ch < 3; ch++) { double d = pal[i, ch] - b[ch]; s += d * d; }
            return Math.Sqrt(s);
        };
        var noise = new List<double>();
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (!nearWordRow(y) && !InBoxes(x, y)) noise.Add(dev(x, y));
        noise.Sort();
        Console.WriteLine("Background prediction error percentiles: " + string.Join("  ", new[] { 50, 75, 90, 95, 97, 98, 99 }.Select(q => q + "%=" + noise[noise.Count * q / 100].ToString("F1"))));
        // The tail is stray non-background pixels (bubble squiggles outside the boxes), so the
        // cutoff comes from the bulk of the distribution, not its extreme.
        double textThreshold = Math.Max(14, noise[noise.Count * 95 / 100] * 1.5);
        Console.WriteLine("Word threshold: {0:F1}", textThreshold);

        // --- 3. Tint model, per square color, from the strongest old word pixels outside the character areas.
        var kk = new double[2, 3]; var mm = new double[2, 3];
        for (int p = 0; p < 2; p++)
        {
            var cand = new List<int>();
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (nearWordRow(y) && !InBoxes(x, y) && Parity(x, y) == p && dev(x, y) > textThreshold) cand.Add(y * W + x);
            var devs = cand.Select(k => dev(k % W, k / W)).OrderBy(v => v).ToList();
            double strongCut = devs[(int)(devs.Count * 0.6)];
            var strong = cand.Where(k => dev(k % W, k / W) >= strongCut).ToList();
            for (int ch = 0; ch < 3; ch++)
            {
                double sx = 0, sy = 0, sxx = 0, sxy = 0; int n = strong.Count;
                foreach (int k in strong) { double xv = Bg(k % W, k / W)[ch], yv = pal[img[k], ch]; sx += xv; sy += yv; sxx += xv * xv; sxy += xv * yv; }
                kk[p, ch] = (n * sxy - sx * sy) / (n * sxx - sx * sx); mm[p, ch] = (sy - kk[p, ch] * sx) / n;
            }
            Console.WriteLine("Tint ({0} squares): R = {1:F2}*bg + {2:F0}, G = {3:F2}*bg + {4:F0}, B = {5:F2}*bg + {6:F0}  (from {7} pixels)",
                p == 0 ? "green" : "brown", kk[p, 0], mm[p, 0], kk[p, 1], mm[p, 1], kk[p, 2], mm[p, 2], strong.Count);
        }
        Func<int, double[], double[]> Tint = (p, b) =>
        {
            var t = new double[3];
            for (int ch = 0; ch < 3; ch++) t[ch] = Math.Max(0, Math.Min(255, kk[p, ch] * b[ch] + mm[p, ch]));
            return t;
        };

        // --- 4. Character pixels (inside the character areas only). A pixel is background or word
        // if its color is some mix of the predicted background and the word tint; anything else is
        // a character. Colors the background never uses, and anything darker than the background
        // ever gets at that distance (outlines), always count as character.
        var usedOutside = new bool[256];
        var minL = new double[2, 260];
        for (int p = 0; p < 2; p++) for (int r = 0; r < 260; r++) minL[p, r] = double.MaxValue;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (InBoxes(x, y)) continue;
                usedOutside[I(x, y)] = true;
                int p = Parity(x, y), r = Rad(x, y);
                minL[p, r] = Math.Min(minL[p, r], L(x, y));
            }
        // How far real background/word pixels stray from the mix line, measured outside the character areas.
        var segErr = new List<double>();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (!nearWordRow(y) || InBoxes(x, y)) continue;
                double al; int p = Parity(x, y);
                segErr.Add(SegDist(Col(I(x, y)), Bg(x, y), Tint(p, Bg(x, y)), out al));
            }
        segErr.Sort();
        double segTol = Math.Max(16, segErr[segErr.Count * 99 / 100] * 1.25);
        Console.WriteLine("Mix-line error outside the characters: 95% {0:F1}, 99% {1:F1} -> tolerance {2:F1}", segErr[segErr.Count * 95 / 100], segErr[segErr.Count * 99 / 100], segTol);

        // The word is one stamp repeated every 80 pixels across. The 8 copies in the top and bottom
        // rows are fully visible, so their median tint amount at each spot is the stamp's exact
        // shape. That tells us what color each middle-row pixel would have if no character covered it.
        int originX;
        {
            var starts = new List<int>();
            for (int c = 1; c < 4; c++)
            {
                int mx = int.MaxValue;
                foreach (var b in bands)
                    for (int y = b[0]; y <= b[1]; y++)
                        for (int x = c * 80; x < c * 80 + 80; x++)
                            if (!InBoxes(x, y) && dev(x, y) > textThreshold) mx = Math.Min(mx, x);
                if (mx != int.MaxValue) starts.Add(mx - c * 80);
            }
            starts.Sort(); originX = starts[starts.Count / 2];
        }
        const int Pad = 3;
        int stampH = bands.Max(b => b[1] - b[0] + 1) + 2 * Pad;
        Func<int, int, double> AlongAt = (x, y) =>
        {
            double al; int p = Parity(x, y);
            SegDist(Col(I(x, y)), Bg(x, y), Tint(p, Bg(x, y)), out al);
            return al;
        };
        var clean = bands.Where(b => Enumerable.Range(0, W).All(x => !InBoxes(x, b[0] - Pad) && !InBoxes(x, b[1] + Pad))).ToList();
        var stamp = new double[80, stampH];
        for (int v = 0; v < stampH; v++)
            for (int u = 0; u < 80; u++)
            {
                var vals = new List<double>();
                foreach (var b in clean)
                    for (int c = 0; c < 4; c++)
                    {
                        int x = originX + c * 80 + u, y = b[0] - Pad + v;
                        if (x >= 0 && x < W && y >= 0 && y < H && Cell(x, y) == Cell(originX + c * 80, b[0])) vals.Add(AlongAt(x, y));
                    }
                vals.Sort();
                stamp[u, v] = vals.Count > 0 ? vals[vals.Count / 2] : 0;
            }
        Func<int, int, double[]> Expected = (x, y) =>
        {
            foreach (var b in bands)
            {
                int v = y - (b[0] - Pad), u = x - originX - (x - originX) / 80 * 80;
                if (v < 0 || v >= stampH || x < originX) continue;
                double s = stamp[u, v]; double[] bg = Bg(x, y), t = Tint(Parity(x, y), bg);
                return new[] { bg[0] + s * (t[0] - bg[0]), bg[1] + s * (t[1] - bg[1]), bg[2] + s * (t[2] - bg[2]) };
            }
            return null;
        };
        Func<int, int, double> ExpDist = (x, y) =>
        {
            double[] e = Expected(x, y); if (e == null) return double.MaxValue;
            double s = 0; for (int ch = 0; ch < 3; ch++) { double d = pal[I(x, y), ch] - e[ch]; s += d * d; }
            return Math.Sqrt(s);
        };
        var expErr = new List<double>();
        foreach (var b in clean)
            for (int y = b[0] - Pad; y <= b[1] + Pad; y++)
                for (int x = originX; x < W; x++) expErr.Add(ExpDist(x, y));
        expErr.Sort();
        double expTol = Math.Max(16, expErr[expErr.Count * 99 / 100] * 1.25);
        Console.WriteLine("Stamp: starts at x={0} (+80 per copy), learned from {1} rows. Visible-copy error: 95% {2:F1}, 99% {3:F1} -> tolerance {4:F1}",
            originX, clean.Count, expErr[expErr.Count * 95 / 100], expErr[expErr.Count * 99 / 100], expTol);

        var isChar = new bool[W * H];       // never modified
        var definite = new bool[W * H];     // character for sure (used for the final check)
        var along = new double[W * H];      // how much word tint a non-character pixel shows (0..1)
        int explained = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (!InBoxes(x, y)) continue;
                int k = y * W + x, p = Parity(x, y), r = Rad(x, y);
                // Exactly the color the stamp predicts here: background or old word, whatever its palette index.
                if (ExpDist(x, y) <= expTol) { along[k] = AlongAt(x, y); explained++; continue; }
                double lo = double.MaxValue;
                for (int d = -3; d <= 3; d++) { int rr = r + d; if (rr >= 0 && rr < 260) lo = Math.Min(lo, minL[p, rr]); }
                if (!usedOutside[img[k]] || L(x, y) < lo - 10) { isChar[k] = definite[k] = true; continue; }
                double al;
                if (SegDist(Col(img[k]), Bg(x, y), Tint(p, Bg(x, y)), out al) > segTol) isChar[k] = true;
                along[k] = al;
            }
        Console.WriteLine("Pixels in the character areas matching the stamp's prediction: {0}", explained);
        // Of those, the ones using palette colors never seen outside the character areas are the
        // risky ones: they'd be character colors if the match were a coincidence. List them.
        var risky = new Dictionary<int, int>();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (InBoxes(x, y) && !isChar[y * W + x] && !usedOutside[I(x, y)])
                { int i = I(x, y); int cnt; risky.TryGetValue(i, out cnt); risky[i] = cnt + 1; }
        Console.WriteLine("  ...using colors otherwise only seen in the character areas: " +
            string.Join("  ", risky.OrderByDescending(kv => kv.Value).Select(kv => string.Format("#{0}({1:F0},{2:F0},{3:F0})x{4}", kv.Key, pal[kv.Key, 0], pal[kv.Key, 1], pal[kv.Key, 2], kv.Value))));
        // Protect the characters' soft edges: grow into neighbors that look like plain background
        // (not into pixels showing word tint, which are leftovers to erase).
        var grown = (bool[])isChar.Clone();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (isChar[y * W + x])
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < W && ny >= 0 && ny < H && InBoxes(nx, ny) && along[ny * W + nx] < 0.25) grown[ny * W + nx] = true;
                        }
        isChar = grown;
        Console.WriteLine("Character pixels (protected): {0}, of which {1} by palette/darkness alone", isChar.Count(c => c), definite.Count(c => c));

        // --- 5. Old word pixels: in a word row, not a character, and off the predicted background.
        var isOld = new bool[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (nearWordRow(y) && !isChar[y * W + x]) isOld[y * W + x] = dev(x, y) > textThreshold;
        var oldGrown = (bool[])isOld.Clone();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (isOld[y * W + x])
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < W && ny >= 0 && ny < H && !isChar[ny * W + nx] && Cell(nx, ny) == Cell(x, y)) oldGrown[ny * W + nx] = true;
                        }
        Console.WriteLine("Old word pixels: {0} (with soft edges: {1})", isOld.Count(b => b), oldGrown.Count(b => b));

        // Per column: x extent of the old word across all rows, using only pixels outside the character areas.
        var colX = new int[4, 2];
        for (int c = 0; c < 4; c++) { colX[c, 0] = int.MaxValue; colX[c, 1] = int.MinValue; }
        foreach (var b in bands)
            for (int y = b[0]; y <= b[1]; y++)
                for (int x = 0; x < W; x++)
                    if (isOld[y * W + x] && !InBoxes(x, y)) { int c = x / 80; colX[c, 0] = Math.Min(colX[c, 0], x); colX[c, 1] = Math.Max(colX[c, 1], x); }
        for (int c = 0; c < 4; c++) Console.WriteLine("Column {0}: x {1}-{2}", c, colX[c, 0], colX[c, 1]);

        // --- 6. Background under the old word: repeated neighbor averaging (same cell, skipping character pixels).
        var B = new double[W * H, 3];
        for (int k = 0; k < W * H; k++) for (int ch = 0; ch < 3; ch++) B[k, ch] = pal[img[k], ch];
        var unknown = new List<int>();
        for (int k = 0; k < W * H; k++) if (oldGrown[k]) unknown.Add(k);
        // Start each unknown pixel from the predicted background, then relax toward its known neighbors.
        foreach (int k in unknown) { double[] b = Bg(k % W, k / W); for (int ch = 0; ch < 3; ch++) B[k, ch] = b[ch]; }
        for (int it = 0; it < 1500; it++)
            foreach (int k in unknown)
            {
                int x = k % W, y = k / W, c = Cell(x, y); double[] s = new double[3]; int n = 0;
                int[] nxs = { x - 1, x + 1, x, x }, nys = { y, y, y - 1, y + 1 };
                for (int j = 0; j < 4; j++)
                {
                    int nx = nxs[j], ny = nys[j];
                    if (nx < 0 || nx >= W || ny < 0 || ny >= H || Cell(nx, ny) != c || isChar[ny * W + nx]) continue;
                    for (int ch = 0; ch < 3; ch++) s[ch] += B[ny * W + nx, ch]; n++;
                }
                if (n > 0) for (int ch = 0; ch < 3; ch++) B[k, ch] = s[ch] / n;
            }

        // --- 7. New word coverage (0..1 per pixel), rendered 4x supersampled.
        var alpha = new double[W * H];
        const int SS = 4;
        using (var fam = new FontFamily(fontName))
        using (var path = new GraphicsPath())
        {
            path.AddString(text, fam, (int)FontStyle.Regular, 100f, new PointF(0, 0), StringFormat.GenericTypographic);
            RectangleF tb = path.GetBounds();
            using (var bmp = new Bitmap(W * SS, H * SS, PixelFormat.Format24bppRgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (var b in bands)
                    for (int c = 0; c < 4; c++)
                    {
                        double boxW = colX[c, 1] - colX[c, 0] + 1, boxH = b[1] - b[0] + 1;
                        double targetH = boxH * 0.78, targetW = Math.Min(boxW + 2, 76);
                        double sy = targetH / tb.Height, sx = Math.Min(sy, targetW / tb.Width);
                        if (sx < sy * 0.82) sy = sx / 0.82;   // squeeze at most ~18% narrower than normal
                        double centerX = (colX[c, 0] + colX[c, 1] + 1) / 2.0, centerY = (b[0] + b[1] + 1) / 2.0;
                        using (var m = new Matrix())
                        using (var pth = (GraphicsPath)path.Clone())
                        {
                            m.Translate((float)(centerX * SS), (float)(centerY * SS));
                            m.Scale((float)(sx * SS), (float)(sy * SS));
                            m.Translate(-(tb.X + tb.Width / 2), -(tb.Y + tb.Height / 2));
                            pth.Transform(m);
                            g.FillPath(Brushes.White, pth);
                        }
                        if (c == 0 && b == bands[0]) Console.WriteLine("New word size: {0:F0}x{1:F0}px", tb.Width * sx, tb.Height * sy);
                    }
                var data = bmp.LockBits(new Rectangle(0, 0, W * SS, H * SS), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                var px = new byte[data.Stride * H * SS];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);
                bmp.UnlockBits(data);
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int s = 0;
                        for (int sy2 = 0; sy2 < SS; sy2++) for (int sx2 = 0; sx2 < SS; sx2++) s += px[(y * SS + sy2) * data.Stride + (x * SS + sx2) * 3];
                        alpha[y * W + x] = s / (255.0 * SS * SS);
                    }
            }
        }

        // --- 8. Composite and map back onto the palette.
        var subset = new List<int>[2] { new List<int>(), new List<int>() };
        var seen = new bool[2, 256];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (!InBoxes(x, y)) { int p = Parity(x, y), i = I(x, y); if (!seen[p, i]) { seen[p, i] = true; subset[p].Add(i); } }
        double[] bayer = { 0.125, 0.625, 0.875, 0.375 };
        byte[] outImg = (byte[])img.Clone();
        int changed = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int k = y * W + x;
                if (isChar[k]) continue;
                double al = alpha[k];
                if (!oldGrown[k] && al <= 0) continue;
                int p = Parity(x, y);
                var bg = new[] { B[k, 0], B[k, 1], B[k, 2] };
                var tint = Tint(p, bg);
                var col = new double[3];
                for (int ch = 0; ch < 3; ch++) col[ch] = bg[ch] + al * (tint[ch] - bg[ch]);
                int q = Quantize(col, subset[p], bayer[(y & 1) * 2 + (x & 1)]);
                if (q != outImg[k]) { outImg[k] = (byte)q; changed++; }
            }
        for (int k = 0; k < W * H; k++) if ((isChar[k] || definite[k]) && outImg[k] != img[k]) throw new Exception("a character pixel changed");
        File.WriteAllBytes(outPath, outImg);
        Console.WriteLine("Changed {0} pixels; no character pixel touched. Wrote {1}", changed, outPath);

        // --- Previews: the word rows before and after, character pixels blanked to flat gray.
        Directory.CreateDirectory(previewDir);
        SavePreview(img, isChar, bands, Path.Combine(previewDir, "options-before.png"));
        SavePreview(outImg, isChar, bands, Path.Combine(previewDir, "options-after.png"));
        var maskView = (byte[])img.Clone();
        int brightest = Enumerable.Range(0, 256).OrderByDescending(LumI).First();
        for (int k = 0; k < W * H; k++) if (oldGrown[k]) maskView[k] = (byte)brightest;
        SavePreview(maskView, isChar, bands, Path.Combine(previewDir, "options-erase-mask.png"));
    }

    static int Quantize(double[] c, List<int> set, double threshold)
    {
        int c1 = Nearest(c, set, -1);
        var refl = new double[3];
        for (int ch = 0; ch < 3; ch++) refl[ch] = 2 * c[ch] - pal[c1, ch];
        int c2 = Nearest(refl, set, c1);
        if (c2 < 0) return c1;
        double num = 0, den = 0;
        for (int ch = 0; ch < 3; ch++) { double d = pal[c2, ch] - pal[c1, ch]; num += (c[ch] - pal[c1, ch]) * d; den += d * d; }
        double f = den > 0 ? Math.Max(0, Math.Min(1, num / den)) : 0;
        return f > threshold ? c2 : c1;
    }

    static int Nearest(double[] c, List<int> set, int exclude)
    {
        int best = -1; double bd = double.MaxValue;
        foreach (int i in set)
        {
            if (i == exclude) continue;
            double dr = c[0] - pal[i, 0], dg = c[1] - pal[i, 1], db = c[2] - pal[i, 2];
            double d = 0.3 * dr * dr + 0.59 * dg * dg + 0.11 * db * db;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    static void SavePreview(byte[] im, bool[] isChar, List<int[]> bands, string path)
    {
        const int S = 3, Pad = 6;
        int h = bands.Sum(b => b[1] - b[0] + 1 + 2 * Pad) + (bands.Count - 1) * 4;
        using (var bmp = new Bitmap(W * S, h * S))
        {
            int oy = 0;
            foreach (var b in bands)
            {
                for (int y = b[0] - Pad; y <= b[1] + Pad; y++, oy++)
                    for (int x = 0; x < W; x++)
                    {
                        Color c;
                        if (y < 0 || y >= H) c = Color.Black;
                        else if (isChar[y * W + x]) c = Color.FromArgb(128, 128, 128);
                        else { int i = im[y * W + x]; c = Color.FromArgb((int)pal[i, 0], (int)pal[i, 1], (int)pal[i, 2]); }
                        for (int sy = 0; sy < S; sy++) for (int sx = 0; sx < S; sx++) bmp.SetPixel(x * S + sx, oy * S + sy, c);
                    }
                for (int g = 0; g < 4 && oy < h; g++, oy++)
                    for (int x = 0; x < W * S; x++) for (int sy = 0; sy < S; sy++) bmp.SetPixel(x, oy * S + sy, Color.White);
            }
            bmp.Save(path, ImageFormat.Png);
        }
    }
}
