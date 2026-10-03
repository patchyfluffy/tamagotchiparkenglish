// Applies small code/data changes to a file, refusing any change whose "before" bytes don't match
// (a guard against typos and wrong offsets).
//
// Patch file (UTF-8): lines "offset  before-hex  after-hex  [comment]", '#' starts a comment line.
//   offset is a file offset in hex; before/after are hex bytes (spaces inside not allowed; use one token).
//   before may be "*" to skip the check (only for areas we deliberately take over, e.g. debug text).
// Usage: codepatch <in> <patches.txt> <out>
using System;
using System.IO;
using System.Linq;
using System.Text;

class CodePatch
{
    static byte[] Hex(string s)
    {
        if (s.Length % 2 != 0) throw new Exception("odd-length hex: " + s);
        return Enumerable.Range(0, s.Length / 2).Select(i => Convert.ToByte(s.Substring(i * 2, 2), 16)).ToArray();
    }

    static void Main(string[] a)
    {
        byte[] b = File.ReadAllBytes(a[0]);
        int n = 0;
        foreach (string raw in File.ReadAllLines(a[1], Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var f = line.Split(new[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
            int off = Convert.ToInt32(f[0].Replace("0x", ""), 16);
            byte[] after = Hex(f[2]);
            if (f[1] != "*")
            {
                byte[] before = Hex(f[1]);
                if (before.Length != after.Length) throw new Exception(string.Format("0x{0:X}: before and after differ in length", off));
                for (int i = 0; i < before.Length; i++)
                    if (b[off + i] != before[i])
                        throw new Exception(string.Format("0x{0:X}: expected {1} but found {2} -- wrong offset, or the file already changed",
                            off, f[1], BitConverter.ToString(b, off, before.Length).Replace("-", "")));
            }
            Buffer.BlockCopy(after, 0, b, off, after.Length);
            Console.WriteLine("0x{0:X5}: {1} bytes  {2}", off, after.Length, f.Length > 3 ? f[3] : "");
            n++;
        }
        File.WriteAllBytes(a[2], b);
        Console.WriteLine("{0} changes applied", n);
    }
}
