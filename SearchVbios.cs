using System;
using System.IO;

class Program
{
    static void Main()
    {
        string romPath = @"C:\Users\timmy\Downloads\nvidia-power-control\NvpwrControl_Blackwell_Universal_v2_4_2_beta_adaptive_ui\NvpwrControl_Blackwell_Universal_v2_4_2_beta_adaptive_ui\dist\state\rom-cache\auto-20260929-060251.rom";
        byte[] rom = File.ReadAllBytes(romPath);

        DumpHex(rom, 0x0AB660, 96, "VBIOS Table Header around 0x0AB670");
    }

    static void DumpHex(byte[] bytes, int start, int count, string title)
    {
        Console.WriteLine("=== " + title + " ===");
        for (int i = 0; i < count; i += 16)
        {
            Console.Write(string.Format("{0:X6}: ", start + i));
            for (int j = 0; j < 16 && start + i + j < bytes.Length; j++)
                Console.Write(string.Format("{0:X2} ", bytes[start + i + j]));
            Console.WriteLine();
        }
    }
}
