using System;
using System.IO;

class Program {
    static void Main() {
        byte[] data = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        // E8 B5 E3 FF FF at 0x8F2B6:
        // target = 0x8F2B6 + 5 + (-0x1C4B) = 0x8F2BB - 0x1C4B = 0x8D670
        int start = 0x8D670;
        int end = 0x8D700;

        Console.WriteLine("Instructions at 0x8D670:");
        for (int i = start; i < end; i += 16) {
            Console.Write("{0:X6}: ", i);
            for (int j = 0; j < 16 && i + j < end; j++) {
                Console.Write("{0:X2} ", data[i + j]);
            }
            Console.WriteLine();
        }
    }
}
