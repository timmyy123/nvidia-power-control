using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        byte[] bytes = File.ReadAllBytes(@"dist\state\rom-cache\auto-20260929-060251.rom");
        int baseOff = 0x0A0627;
        int hdrSize = bytes[baseOff + 1];
        int entSize = bytes[baseOff + 2];
        int entCnt = bytes[baseOff + 3];

        Console.WriteLine(string.Format("Power/Perf Table: Hdr={0}, EntSize={1}, EntCnt={2}", hdrSize, entSize, entCnt));

        int curr = baseOff + hdrSize;
        for (int e = 0; e < entCnt; e++)
        {
            Console.WriteLine(string.Format("\n--- Entry {0:D2} (ROM 0x{1:X6}, Shadow 0x{2:X6}) ---", e, curr, curr - 0x9400));
            // Scan for milliwatts (power) and microvolts (voltage) inside this entry
            for (int i = 0; i <= entSize - 4; i++)
            {
                uint val = BitConverter.ToUInt32(bytes, curr + i);
                if (val >= 50000 && val <= 300000 && val % 1000 == 0)
                {
                    Console.WriteLine(string.Format("  +0x{0:X2} (ROM 0x{1:X6}, Shadow 0x{2:X6}) : Power {3} W ({4} mW)", i, curr + i, curr + i - 0x9400, val / 1000, val));
                }
                if (val >= 800000 && val <= 1150000 && val % 1000 == 0)
                {
                    Console.WriteLine(string.Format("  +0x{0:X2} (ROM 0x{1:X6}, Shadow 0x{2:X6}) : Volt {3} uV ({4:F3} V)", i, curr + i, curr + i - 0x9400, val, val / 1000000.0));
                }
            }
            curr += entSize;
        }
    }
}
