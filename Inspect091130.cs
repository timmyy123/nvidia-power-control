using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int start = 0x091130;
        int end = 0x091180;
        Console.WriteLine("Instructions 0x091130 to 0x091180:");
        for (int i = start; i < end; i += 16) {
            Console.Write("{0:X6}: ", i);
            for (int j = 0; j < 16 && i + j < end; j++) {
                Console.Write("{0:X2} ", d[i + j]);
            }
            Console.WriteLine();
        }
    }
}
