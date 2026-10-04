using System;
using System.IO;

class Program
{
    static void Main()
    {
        string path = @"C:\Windows\System32\DriverStore\FileRepository\nvmii.inf_amd64_185e17ffef00b7bf\nvlddmkm.sys";
        byte[] bytes = File.ReadAllBytes(path);

        DumpHex(bytes, 0x00333150, 160, "RVA around 0x333150");
    }

    static void DumpHex(byte[] bytes, int start, int count, string title)
    {
        Console.WriteLine("=== " + title + " ===");
        for (int i = 0; i < count; i += 16)
        {
            Console.Write(string.Format("{0:X8}: ", start + i));
            for (int j = 0; j < 16; j++)
            {
                if (i + j < count)
                    Console.Write(string.Format("{0:X2} ", bytes[start + i + j]));
                else
                    Console.Write("   ");
            }
            Console.WriteLine();
        }
        Console.WriteLine();
    }
}
