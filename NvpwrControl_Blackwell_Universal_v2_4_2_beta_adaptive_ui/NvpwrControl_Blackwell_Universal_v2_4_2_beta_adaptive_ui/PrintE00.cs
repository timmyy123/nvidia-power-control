using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        byte[] bytes = File.ReadAllBytes(@"dist\state\rom-cache\auto-20260929-060251.rom");
        int e00 = 0x0A065B;
        Console.WriteLine("Entry 00 bytes:");
        for (int i = 0; i < 91; i++)
        {
            Console.Write(bytes[e00 + i].ToString("X2") + " ");
            if ((i + 1) % 16 == 0) Console.WriteLine();
        }
        Console.WriteLine("\n");
    }
}
