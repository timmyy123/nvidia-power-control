using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        byte[] bytes = File.ReadAllBytes(@"dist\state\rom-cache\auto-20260929-060251.rom");
        int off = 0x0A0627 + 52 + (20 * 91);
        Console.WriteLine(string.Format("Offset 0x{0:X6}:", off));
        for (int i = 0; i < 64; i++)
        {
            Console.Write(bytes[off + i].ToString("X2") + " ");
            if ((i + 1) % 16 == 0) Console.WriteLine();
        }
    }
}
