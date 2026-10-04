using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        byte[] bytes = File.ReadAllBytes(@"dist\state\rom-cache\auto-20260929-060251.rom");
        Console.WriteLine("--- Table around 0x0AB200 ---");
        int baseOff = 0x0AB200;
        for (int i = 0; i < 256; i++)
        {
            if (i % 16 == 0) Console.Write(string.Format("\n0x{0:X6} (Shadow 0x{1:X6}): ", baseOff + i, baseOff + i - 0x9400));
            Console.Write(bytes[baseOff + i].ToString("X2") + " ");
        }
        Console.WriteLine();
    }
}
