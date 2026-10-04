using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : @"dist\state\rom-cache\auto-20260929-060251.rom";
        byte[] bytes = File.ReadAllBytes(path);

        Console.WriteLine("--- Checking 0x0A0000 to 0x0AC000 ---");
        for (int i = 0x0A0000; i <= 0x0AC000; i++)
        {
            // 32-bit check
            uint u32 = BitConverter.ToUInt32(bytes, i);
            if (u32 >= 800000 && u32 <= 1150000)
            {
                Console.WriteLine(string.Format("U32  Offset=0x{0:X6} Shadow=0x{1:X6} : {2} uV ({3:F4} V)", i, i - 0x9400, u32, u32 / 1000000.0));
            }

            // 16-bit millivolt check
            ushort u16 = BitConverter.ToUInt16(bytes, i);
            if (u16 >= 900 && u16 <= 1050)
            {
                // check surrounding bytes to avoid random matching
                // Usually table entries are aligned or clustered
                // Let's print
                // Console.WriteLine(string.Format("U16  Offset=0x{0:X6} Shadow=0x{1:X6} : {2} mV", i, i - 0x9400, u16));
            }
        }

        Console.WriteLine("\n--- Checking Voltage Table at 0x0AB67D ---");
        int vtOffset = 0x0AB67D;
        for (int i = 0; i < 64; i++)
        {
            Console.Write(bytes[vtOffset + i].ToString("X2") + " ");
            if ((i + 1) % 16 == 0) Console.WriteLine();
        }

        Console.WriteLine("\n--- Checking 0x0A0FAC ---");
        int ptOffset = 0x0A0FAC - 16;
        for (int i = 0; i < 48; i++)
        {
            Console.Write(bytes[ptOffset + i].ToString("X2") + " ");
            if ((i + 1) % 16 == 0) Console.WriteLine();
        }
    }
}
