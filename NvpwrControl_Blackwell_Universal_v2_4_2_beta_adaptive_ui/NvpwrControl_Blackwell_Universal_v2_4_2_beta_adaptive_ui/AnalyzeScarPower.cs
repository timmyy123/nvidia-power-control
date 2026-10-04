using System;
using System.IO;

class Program
{
    static void Main()
    {
        string path = @"c:\Users\timmy\Downloads\nvidia-power-control\Scar16_4090_stock.rom";
        byte[] b = File.ReadAllBytes(path);
        Console.WriteLine("Scar ROM size: " + b.Length);

        // Search for 175000 mW (175 W)
        uint target = 175000;
        byte[] targetBytes = BitConverter.GetBytes(target);

        Console.WriteLine("\n--- Searching for 175000 (0x" + target.ToString("X") + ") ---");
        for (int i = 0; i <= b.Length - 4; i++)
        {
            if (b[i] == targetBytes[0] && b[i+1] == targetBytes[1] && b[i+2] == targetBytes[2] && b[i+3] == targetBytes[3])
            {
                Console.WriteLine(string.Format("Offset 0x{0:X6} (Shadow 0x{1:X6})", i, i - 0x9400));
                // Print surrounding bytes
                int start = Math.Max(0, i - 16);
                int len = Math.Min(b.Length - start, 48);
                for (int j = 0; j < len; j++)
                {
                    Console.Write(b[start + j].ToString("X2") + " ");
                }
                Console.WriteLine();
            }
        }

        // Search for PCIR blocks
        Console.WriteLine("\n--- PCIR blocks ---");
        for (int i = 0; i <= b.Length - 0x20; i++)
        {
            if (b[i] == 'P' && b[i+1] == 'C' && b[i+2] == 'I' && b[i+3] == 'R')
            {
                ushort vendor = BitConverter.ToUInt16(b, i + 4);
                ushort device = BitConverter.ToUInt16(b, i + 6);
                byte codeType = b[i + 0x14];
                Console.WriteLine(string.Format("PCIR at 0x{0:X6}: Vendor=0x{1:X4} Device=0x{2:X4} CodeType=0x{3:X2}", i, vendor, device, codeType));
            }
        }
    }
}
