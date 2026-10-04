using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        byte[] bytes = File.ReadAllBytes(@"dist\state\rom-cache\auto-20260929-060251.rom");
        int e01 = 0x0A06B6;
        Console.WriteLine("Entry 01 bytes:");
        for (int i = 0; i < 91; i++)
        {
            Console.Write(bytes[e01 + i].ToString("X2") + " ");
            if ((i + 1) % 16 == 0) Console.WriteLine();
        }
        Console.WriteLine("\n");

        int e02 = 0x0A0711;
        Console.WriteLine("Entry 02 bytes:");
        for (int i = 0; i < 91; i++)
        {
            Console.Write(bytes[e02 + i].ToString("X2") + " ");
            if ((i + 1) % 16 == 0) Console.WriteLine();
        }
        Console.WriteLine();
    }
}
