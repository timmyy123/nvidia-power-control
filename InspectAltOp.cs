using System;
using System.IO;

class Program {
    static void Main() {
        byte[] data = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int start = 0x8D9F0;
        int end = 0x8DAB0;

        Console.WriteLine("Instructions 0x8D9F0 to 0x8DAB0:");
        for (int i = start; i < end; i += 16) {
            Console.Write("{0:X6}: ", i);
            for (int j = 0; j < 16 && i + j < end; j++) {
                Console.Write("{0:X2} ", data[i + j]);
            }
            Console.WriteLine();
        }
    }
}
