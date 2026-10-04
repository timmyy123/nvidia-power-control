using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string path = @"C:\Windows\System32\DriverStore\FileRepository\nvmii.inf_amd64_185e17ffef00b7bf\nvlddmkm.sys";
        byte[] bytes = File.ReadAllBytes(path);

        // Find references to 0x00D062D0 in .text
        // In x64 PE, lea rdx, [rip + disp32]
        long strRva = 0x00D062D0; // Note this is raw offset. Let's check sections.
        Console.WriteLine("Searching for references...");
    }
}
