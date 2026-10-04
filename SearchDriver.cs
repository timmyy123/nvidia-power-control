using System;
using System.IO;

class Program
{
    static void Main()
    {
        string path = @"C:\Windows\System32\DriverStore\FileRepository\nvmii.inf_amd64_185e17ffef00b7bf\nvlddmkm.sys";
        byte[] bytes = File.ReadAllBytes(path);

        byte[] pat = BitConverter.GetBytes(940000); // 0x000E57E0: E0 57 0E 00
        Console.WriteLine("Searching nvlddmkm.sys for 940000 uV (E0 57 0E 00):");
        int count = 0;
        for (int i = 0; i <= bytes.Length - pat.Length; i++)
        {
            if (bytes[i] == pat[0] && bytes[i+1] == pat[1] && bytes[i+2] == pat[2] && bytes[i+3] == pat[3])
            {
                Console.WriteLine(string.Format("  Found at 0x{0:X8}", i));
                count++;
                if (count >= 15) { Console.WriteLine("  ... more"); break; }
            }
        }
        Console.WriteLine("Total found: " + count);
    }
}
