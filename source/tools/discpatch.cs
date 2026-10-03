// Patches files inside a raw MODE1/2352 disc image (the "Track 1" .bin) and recomputes each changed
// sector's error detection/correction codes (EDC/ECC), the same way a real CD mastering tool would.
//
// Usage:
//   discpatch selftest <track1.bin>
//       recompute EDC/ECC for every sector and confirm they match what's on the disc
//   discpatch patch <in.bin> <out.bin> <isoPath> <fileOffsetHex> <dataFile> [padToHex]
//       write dataFile into the file at isoPath (e.g. /DATA/USER.BIN) starting at fileOffset,
//       then fill with zeros up to padTo (if given). Never changes a file's size or position.
//   discpatch replace <in.bin> <out.bin> <isoPath> <dataFile>
//       replace a whole file (e.g. a repacked game overlay). Smaller data is zero-padded to the old size;
//       bigger data must fit in the file's existing sectors, and its directory size is updated.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class DiscPatch
{
    const int Raw = 2352, User = 2048, UserOff = 16;
    static byte[] eccF = new byte[256], eccB = new byte[256];
    static uint[] edcLut = new uint[256];

    static DiscPatch()
    {
        for (uint i = 0; i < 256; i++)
        {
            uint j = (i << 1) ^ ((i & 0x80) != 0 ? 0x11Du : 0);
            eccF[i] = (byte)j;
            eccB[i ^ j] = (byte)i;
            uint edc = i;
            for (int k = 0; k < 8; k++) edc = (edc >> 1) ^ ((edc & 1) != 0 ? 0xD8018001u : 0);
            edcLut[i] = edc;
        }
    }

    static uint Edc(byte[] b, int off, int len)
    {
        uint edc = 0;
        for (int i = 0; i < len; i++) edc = (edc >> 8) ^ edcLut[(edc ^ b[off + i]) & 0xFF];
        return edc;
    }

    static void EccBlock(byte[] s, int src, int majorCount, int minorCount, int majorMult, int minorInc, int dest)
    {
        int size = majorCount * minorCount;
        for (int major = 0; major < majorCount; major++)
        {
            int index = (major >> 1) * majorMult + (major & 1);
            byte a = 0, b = 0;
            for (int minor = 0; minor < minorCount; minor++)
            {
                byte t = s[src + index];
                index += minorInc;
                if (index >= size) index -= size;
                a ^= t; b ^= t; a = eccF[a];
            }
            a = eccB[eccF[a] ^ b];
            s[dest + major] = a;
            s[dest + major + majorCount] = (byte)(a ^ b);
        }
    }

    // Rewrites the EDC and both ECC parity blocks of the Mode 1 sector starting at 'o'.
    static void FixSector(byte[] d, int o)
    {
        uint edc = Edc(d, o, 0x810);
        d[o + 0x810] = (byte)edc; d[o + 0x811] = (byte)(edc >> 8); d[o + 0x812] = (byte)(edc >> 16); d[o + 0x813] = (byte)(edc >> 24);
        for (int i = 0x814; i < 0x81C; i++) d[o + i] = 0;
        EccBlock(d, o + 0xC, 86, 24, 2, 86, o + 0x81C);
        EccBlock(d, o + 0xC, 52, 43, 86, 88, o + 0x8C8);
    }

    static byte[] ReadUser(byte[] d, int lba, int len)
    {
        var o = new byte[len];
        for (int i = 0; i < len; i++) o[i] = d[(lba + i / User) * Raw + UserOff + i % User];
        return o;
    }

    // Finds a file by path in the ISO9660 filesystem; returns its first sector and size.
    static void Find(byte[] d, string path, out int lba, out int size)
    {
        int recLba, recOff;
        Find(d, path, out lba, out size, out recLba, out recOff);
    }

    // Same, plus where its directory record is (sector, and byte offset within that sector's data).
    static void Find(byte[] d, string path, out int lba, out int size, out int recLba, out int recOff)
    {
        byte[] pvd = ReadUser(d, 16, User);
        lba = BitConverter.ToInt32(pvd, 156 + 2); size = BitConverter.ToInt32(pvd, 156 + 10);
        recLba = -1; recOff = -1;
        foreach (string part in path.Trim('/').Split('/'))
        {
            byte[] dir = ReadUser(d, lba, size);
            int dirLba = lba;
            bool found = false;
            for (int i = 0; i < dir.Length && !found;)
            {
                int len = dir[i];
                if (len == 0) { i = (i / User + 1) * User; continue; }
                int nl = dir[i + 32];
                string name = Encoding.ASCII.GetString(dir, i + 33, nl);
                int semi = name.IndexOf(';'); if (semi >= 0) name = name.Substring(0, semi);
                if (string.Equals(name, part, StringComparison.OrdinalIgnoreCase))
                {
                    lba = BitConverter.ToInt32(dir, i + 2); size = BitConverter.ToInt32(dir, i + 10); found = true;
                    recLba = dirLba + i / User; recOff = i % User;
                }
                i += len;
            }
            if (!found) throw new Exception("not found on disc: " + path);
        }
    }

    static void Main(string[] a)
    {
        if (a[0] == "selftest")
        {
            byte[] d = File.ReadAllBytes(a[1]);
            int sectors = d.Length / Raw, bad = 0;
            var copy = new byte[Raw];
            for (int s = 0; s < sectors; s++)
            {
                Buffer.BlockCopy(d, s * Raw, copy, 0, Raw);
                if (copy[15] != 1) continue; // only Mode 1 sectors carry EDC/ECC
                FixSector(copy, 0);
                for (int i = 0; i < Raw; i++) if (copy[i] != d[s * Raw + i]) { bad++; break; }
            }
            Console.WriteLine("Checked {0} sectors: {1} mismatches", sectors, bad);
            if (bad > 0) Environment.Exit(1);
        }
        else if (a[0] == "replace")
        {
            // Replace a whole file's contents. If the new data is no bigger, it's zero-padded to the old size
            // (the size on disc stays the same). If it's bigger, it must still fit in the sectors the file
            // already occupies, and the size in its directory record is updated.
            byte[] d = File.ReadAllBytes(a[1]);
            string isoPath = a[3];
            byte[] data = File.ReadAllBytes(a[4]);
            int lba, size, recLba, recOff;
            Find(d, isoPath, out lba, out size, out recLba, out recOff);
            int sectors = (size + User - 1) / User;
            if (data.Length > sectors * User) throw new Exception(string.Format("{0}: new data is {1} bytes, but the file only has {2} sectors ({3} bytes) on disc", isoPath, data.Length, sectors, sectors * User));
            int newSize = Math.Max(size, data.Length);
            var touched = new SortedSet<int>();
            for (int i = 0; i < sectors * User; i++)
            {
                int s = lba + i / User;
                d[s * Raw + UserOff + i % User] = i < data.Length ? data[i] : (byte)0;
                touched.Add(s);
            }
            if (newSize != size)
            {
                int o = recLba * Raw + UserOff + recOff;
                byte[] le = BitConverter.GetBytes(newSize);
                for (int k = 0; k < 4; k++) { d[o + 10 + k] = le[k]; d[o + 14 + k] = le[3 - k]; }
                touched.Add(recLba);
            }
            foreach (int s in touched) FixSector(d, s * Raw);
            File.WriteAllBytes(a[2], d);
            Console.WriteLine("{0}: wrote {1} bytes (was {2}){3}; {4} sectors updated", isoPath, data.Length, size,
                newSize != size ? ", directory size updated to " + newSize : ", size on disc unchanged", touched.Count);
        }
        else if (a[0] == "patch")
        {
            byte[] d = File.ReadAllBytes(a[1]);
            string isoPath = a[3];
            int fileOff = Convert.ToInt32(a[4], 16);
            byte[] data = File.ReadAllBytes(a[5]);
            int padTo = a.Length > 6 ? Convert.ToInt32(a[6], 16) : fileOff + data.Length;
            int lba, size;
            Find(d, isoPath, out lba, out size);
            if (fileOff + data.Length > padTo) throw new Exception(string.Format("data is {0} bytes too big for its slot", fileOff + data.Length - padTo));
            if (padTo > size) throw new Exception("patch runs past the end of " + isoPath);
            var touched = new SortedSet<int>();
            for (int i = fileOff; i < padTo; i++)
            {
                int s = lba + i / User;
                d[s * Raw + UserOff + i % User] = i - fileOff < data.Length ? data[i - fileOff] : (byte)0;
                touched.Add(s);
            }
            foreach (int s in touched) FixSector(d, s * Raw);
            File.WriteAllBytes(a[2], d);
            Console.WriteLine("{0}: wrote {1} bytes at 0x{2:X} (+{3} zero bytes up to 0x{4:X}); {5} sectors updated (LBA {6}-{7})",
                isoPath, data.Length, fileOff, padTo - fileOff - data.Length, padTo, touched.Count, touched.Min, touched.Max);
        }
    }
}
