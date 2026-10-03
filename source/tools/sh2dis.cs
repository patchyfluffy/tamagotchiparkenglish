// Minimal SH-2 disassembler. Usage: sh2dis <file> <loadAddrHex> [startHex endHex]
using System;
using System.IO;
using System.Text;

class Sh2Dis
{
    static byte[] mem;
    static uint baseAddr;

    static uint Rd32(uint a) { long o = a - baseAddr; if (o < 0 || o + 4 > mem.Length) return 0xDEADBEEF; return (uint)(mem[o] << 24 | mem[o + 1] << 16 | mem[o + 2] << 8 | mem[o + 3]); }
    static ushort Rd16(uint a) { long o = a - baseAddr; if (o < 0 || o + 2 > mem.Length) return 0xDEAD; return (ushort)(mem[o] << 8 | mem[o + 1]); }
    static string H(uint v) { return "0x" + v.ToString("X"); }
    static string R(int n) { return "r" + n; }

    public static string Dis(uint pc, ushort op)
    {
        int n = (op >> 8) & 0xF, m = (op >> 4) & 0xF, d4 = op & 0xF, i8 = op & 0xFF;
        sbyte s8 = (sbyte)i8;
        switch (op >> 12)
        {
            case 0x0:
                switch (op & 0xF)
                {
                    case 0x2: if (m == 0) return "stc sr," + R(n); if (m == 1) return "stc gbr," + R(n); if (m == 2) return "stc vbr," + R(n); break;
                    case 0x3: if (m == 0) return "bsrf " + R(n); if (m == 2) return "braf " + R(n); break;
                    case 0x4: return "mov.b " + R(m) + ",@(r0," + R(n) + ")";
                    case 0x5: return "mov.w " + R(m) + ",@(r0," + R(n) + ")";
                    case 0x6: return "mov.l " + R(m) + ",@(r0," + R(n) + ")";
                    case 0x7: return "mul.l " + R(m) + "," + R(n);
                    case 0x8: if (op == 0x0008) return "clrt"; if (op == 0x0018) return "sett"; if (op == 0x0028) return "clrmac"; break;
                    case 0x9: if (op == 0x0009) return "nop"; if (op == 0x0019) return "div0u"; if (m == 2) return "movt " + R(n); break;
                    case 0xA: if (m == 0) return "sts mach," + R(n); if (m == 1) return "sts macl," + R(n); if (m == 2) return "sts pr," + R(n); break;
                    case 0xB: if (op == 0x000B) return "rts"; if (op == 0x001B) return "sleep"; if (op == 0x002B) return "rte"; break;
                    case 0xC: return "mov.b @(r0," + R(m) + ")," + R(n);
                    case 0xD: return "mov.w @(r0," + R(m) + ")," + R(n);
                    case 0xE: return "mov.l @(r0," + R(m) + ")," + R(n);
                    case 0xF: return "mac.l @" + R(m) + "+,@" + R(n) + "+";
                }
                break;
            case 0x1: return "mov.l " + R(m) + ",@(" + (d4 * 4) + "," + R(n) + ")";
            case 0x2:
                {
                    string[] t = { "mov.b {0},@{1}", "mov.w {0},@{1}", "mov.l {0},@{1}", null, "mov.b {0},@-{1}", "mov.w {0},@-{1}", "mov.l {0},@-{1}", "div0s {0},{1}", "tst {0},{1}", "and {0},{1}", "xor {0},{1}", "or {0},{1}", "cmp/str {0},{1}", "xtrct {0},{1}", "mulu.w {0},{1}", "muls.w {0},{1}" };
                    if (t[d4] != null) return string.Format(t[d4], R(m), R(n));
                    break;
                }
            case 0x3:
                {
                    string[] t = { "cmp/eq", null, "cmp/hs", "cmp/ge", "div1", "dmulu.l", "cmp/hi", "cmp/gt", "sub", null, "subc", "subv", "add", "dmuls.l", "addc", "addv" };
                    if (t[d4] != null) return t[d4] + " " + R(m) + "," + R(n);
                    break;
                }
            case 0x4:
                {
                    int lo = op & 0xFF;
                    if ((lo & 0xF) == 0xF) return "mac.w @" + R(m) + "+,@" + R(n) + "+";
                    switch (lo)
                    {
                        case 0x00: return "shll " + R(n);
                        case 0x10: return "dt " + R(n);
                        case 0x20: return "shal " + R(n);
                        case 0x01: return "shlr " + R(n);
                        case 0x21: return "shar " + R(n);
                        case 0x11: return "cmp/pz " + R(n);
                        case 0x15: return "cmp/pl " + R(n);
                        case 0x04: return "rotl " + R(n);
                        case 0x24: return "rotcl " + R(n);
                        case 0x05: return "rotr " + R(n);
                        case 0x25: return "rotcr " + R(n);
                        case 0x08: return "shll2 " + R(n);
                        case 0x18: return "shll8 " + R(n);
                        case 0x28: return "shll16 " + R(n);
                        case 0x09: return "shlr2 " + R(n);
                        case 0x19: return "shlr8 " + R(n);
                        case 0x29: return "shlr16 " + R(n);
                        case 0x0B: return "jsr @" + R(n);
                        case 0x1B: return "tas.b @" + R(n);
                        case 0x2B: return "jmp @" + R(n);
                        case 0x02: return "sts.l mach,@-" + R(n);
                        case 0x12: return "sts.l macl,@-" + R(n);
                        case 0x22: return "sts.l pr,@-" + R(n);
                        case 0x03: return "stc.l sr,@-" + R(n);
                        case 0x13: return "stc.l gbr,@-" + R(n);
                        case 0x23: return "stc.l vbr,@-" + R(n);
                        case 0x06: return "lds.l @" + R(n) + "+,mach";
                        case 0x16: return "lds.l @" + R(n) + "+,macl";
                        case 0x26: return "lds.l @" + R(n) + "+,pr";
                        case 0x07: return "ldc.l @" + R(n) + "+,sr";
                        case 0x17: return "ldc.l @" + R(n) + "+,gbr";
                        case 0x27: return "ldc.l @" + R(n) + "+,vbr";
                        case 0x0A: return "lds " + R(n) + ",mach";
                        case 0x1A: return "lds " + R(n) + ",macl";
                        case 0x2A: return "lds " + R(n) + ",pr";
                        case 0x0E: return "ldc " + R(n) + ",sr";
                        case 0x1E: return "ldc " + R(n) + ",gbr";
                        case 0x2E: return "ldc " + R(n) + ",vbr";
                    }
                    break;
                }
            case 0x5: return "mov.l @(" + (d4 * 4) + "," + R(m) + ")," + R(n);
            case 0x6:
                {
                    string[] t = { "mov.b @{0},{1}", "mov.w @{0},{1}", "mov.l @{0},{1}", "mov {0},{1}", "mov.b @{0}+,{1}", "mov.w @{0}+,{1}", "mov.l @{0}+,{1}", "not {0},{1}", "swap.b {0},{1}", "swap.w {0},{1}", "negc {0},{1}", "neg {0},{1}", "extu.b {0},{1}", "extu.w {0},{1}", "exts.b {0},{1}", "exts.w {0},{1}" };
                    return string.Format(t[d4], R(m), R(n));
                }
            case 0x7: return "add #" + s8 + "," + R(n);
            case 0x8:
                switch (n)
                {
                    case 0x0: return "mov.b r0,@(" + d4 + "," + R(m) + ")";
                    case 0x1: return "mov.w r0,@(" + (d4 * 2) + "," + R(m) + ")";
                    case 0x4: return "mov.b @(" + d4 + "," + R(m) + "),r0";
                    case 0x5: return "mov.w @(" + (d4 * 2) + "," + R(m) + "),r0";
                    case 0x8: return "cmp/eq #" + s8 + ",r0";
                    case 0x9: return "bt " + H((uint)(pc + 4 + s8 * 2));
                    case 0xB: return "bf " + H((uint)(pc + 4 + s8 * 2));
                    case 0xD: return "bt/s " + H((uint)(pc + 4 + s8 * 2));
                    case 0xF: return "bf/s " + H((uint)(pc + 4 + s8 * 2));
                }
                break;
            case 0x9:
                {
                    uint a = (uint)(pc + 4 + i8 * 2);
                    return "mov.w @(" + H(a) + ")," + R(n) + "    ; =" + H(Rd16(a)) + " (" + (short)Rd16(a) + ")";
                }
            case 0xA:
            case 0xB:
                {
                    int d12 = op & 0xFFF; if ((d12 & 0x800) != 0) d12 -= 0x1000;
                    return ((op >> 12) == 0xA ? "bra " : "bsr ") + H((uint)(pc + 4 + d12 * 2));
                }
            case 0xC:
                switch (n)
                {
                    case 0x0: return "mov.b r0,@(" + i8 + ",gbr)";
                    case 0x1: return "mov.w r0,@(" + (i8 * 2) + ",gbr)";
                    case 0x2: return "mov.l r0,@(" + (i8 * 4) + ",gbr)";
                    case 0x3: return "trapa #" + i8;
                    case 0x4: return "mov.b @(" + i8 + ",gbr),r0";
                    case 0x5: return "mov.w @(" + (i8 * 2) + ",gbr),r0";
                    case 0x6: return "mov.l @(" + (i8 * 4) + ",gbr),r0";
                    case 0x7: return "mova @(" + H((uint)((pc & ~3u) + 4 + i8 * 4)) + "),r0";
                    case 0x8: return "tst #" + H((uint)i8) + ",r0";
                    case 0x9: return "and #" + H((uint)i8) + ",r0";
                    case 0xA: return "xor #" + H((uint)i8) + ",r0";
                    case 0xB: return "or #" + H((uint)i8) + ",r0";
                    case 0xC: return "tst.b #" + H((uint)i8) + ",@(r0,gbr)";
                    case 0xD: return "and.b #" + H((uint)i8) + ",@(r0,gbr)";
                    case 0xE: return "xor.b #" + H((uint)i8) + ",@(r0,gbr)";
                    case 0xF: return "or.b #" + H((uint)i8) + ",@(r0,gbr)";
                }
                break;
            case 0xD:
                {
                    uint a = (uint)((pc & ~3u) + 4 + i8 * 4);
                    return "mov.l @(" + H(a) + ")," + R(n) + "    ; =" + H(Rd32(a));
                }
            case 0xE: return "mov #" + s8 + "," + R(n);
        }
        return ".word " + H(op);
    }

    static void Main(string[] args)
    {
        mem = File.ReadAllBytes(args[0]);
        baseAddr = Convert.ToUInt32(args[1], 16);
        uint start = args.Length > 2 ? Convert.ToUInt32(args[2], 16) : baseAddr;
        uint end = args.Length > 3 ? Convert.ToUInt32(args[3], 16) : baseAddr + (uint)mem.Length;
        var sb = new StringBuilder();
        for (uint pc = start & ~1u; pc + 2 <= end; pc += 2)
        {
            ushort op = Rd16(pc);
            sb.Append(pc.ToString("X8")).Append(":  ").Append(op.ToString("X4")).Append("  ").Append(Dis(pc, op)).Append('\n');
        }
        Console.Out.Write(sb.ToString());
    }
}
