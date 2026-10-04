using System;
using System.IO;

class Program
{
    static void Main()
    {
        string romPath = @"C:\Users\timmy\Downloads\nvidia-power-control\NvpwrControl_Blackwell_Universal_v2_4_2_beta_adaptive_ui\NvpwrControl_Blackwell_Universal_v2_4_2_beta_adaptive_ui\dist\state\rom-cache\auto-20260929-060251.rom";
        byte[] rom = File.ReadAllBytes(romPath);

        // In legacy/modern PC VBIOS, 16-bit pointers in BIT table are offsets from 0x0000 in the ROM image or the PCIR image.
        // Let's dump 0x02EC and 0x040F, and also search for 0x040F or what is pointed to by token V.
        ushort pPtr = 0x02EC;
        ushort vPtr = 0x040F;

        DumpHex(rom, pPtr, 128, "Token P table at 0x02EC");
        DumpHex(rom, vPtr, 64, "Token V table at 0x040F");
    }

    static void DumpHex(byte[] bytes, int start, int count, string title)
    {
        Console.WriteLine("=== " + title + " ===");
        for (int i = 0; i < count; i += 16)
        {
            Console.Write(string.Format("{0:X4}: ", start + i));
            for (int j = 0; j < 16 && start + i + j < bytes.Length; j++)
                Console.Write(string.Format("{0:X2} ", bytes[start + i + j]));
            Console.WriteLine();
        }
    }
}
