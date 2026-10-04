using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        byte[] bytes = File.ReadAllBytes(@"dist\state\rom-cache\auto-20260929-060251.rom");
        int baseOff = 0x0AB67D;
        byte ver = bytes[baseOff];
        byte hdrSize = bytes[baseOff + 1];
        byte entrySize = bytes[baseOff + 2];
        byte entryCount = bytes[baseOff + 3];

        Console.WriteLine(string.Format("Voltage Table: Ver=0x{0:X2}, HeaderSize={1}, EntrySize={2}, EntryCount={3}", ver, hdrSize, entrySize, entryCount));

        // Dump header
        Console.WriteLine("Header bytes:");
        for (int i = 0; i < hdrSize; i++)
        {
            Console.Write(bytes[baseOff + i].ToString("X2") + " ");
            if ((i + 1) % 16 == 0) Console.WriteLine();
        }
        Console.WriteLine();

        // Dump entries
        int curr = baseOff + hdrSize;
        for (int e = 0; e < entryCount; e++)
        {
            Console.Write(string.Format("Entry {0:D2} (0x{1:X6}, Shadow 0x{2:X6}): ", e, curr, curr - 0x9400));
            for (int i = 0; i < entrySize; i++)
            {
                Console.Write(bytes[curr + i].ToString("X2") + " ");
            }
            Console.WriteLine();
            curr += entrySize;
        }
    }
}
