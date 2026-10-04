using System;
using System.IO;

class Program
{
    static void Main()
    {
        string path = @"c:\Users\timmy\Downloads\nvidia-power-control\Scar16_4090_stock.rom";
        byte[] b = File.ReadAllBytes(path);

        int maxOff = 0x09F4CD;
        Console.WriteLine("Inspecting backwards from 0x" + maxOff.ToString("X") + "...");

        for (int p = maxOff - 1; p >= maxOff - 500; p--)
        {
            if (b[p] == 0x30 || b[p] == 0x40)
            {
                int hdr = b[p + 1];
                int esize = b[p + 2];
                int count = b[p + 3];
                if (count >= 1 && count <= 30 && esize >= 50 && esize <= 120 && hdr >= 20 && hdr <= 80)
                {
                    Console.WriteLine(string.Format("Found table at 0x{0:X6}: Ver=0x{1:X2} Hdr={2} EntSize={3} Count={4}", p, b[p], hdr, esize, count));
                    // Check if maxOff lands inside one of the entries
                    int rel = maxOff - (p + hdr);
                    if (rel >= 0 && rel % esize < esize)
                    {
                        int entryIndex = rel / esize;
                        int fieldOffset = rel % esize;
                        Console.WriteLine(string.Format("  -> Max lands in Entry {0} at field offset +0x{1:X2} (total offset 0x{2:X6})", entryIndex, fieldOffset, maxOff));
                    }
                }
            }
        }
    }
}
