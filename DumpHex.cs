using System;
using System.IO;

class Program {
    static void Main() {
        byte[] data = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int start = 0x8F100;
        int end = 0x8F550;

        Console.WriteLine("Dumping 0x" + start.ToString("X") + " to 0x" + end.ToString("X") + ":");
        for (int i = start; i < end; i += 16) {
            Console.Write("{0:X6}: ", i);
            for (int j = 0; j < 16 && i + j < end; j++) {
                Console.Write("{0:X2} ", data[i + j]);
            }
            Console.WriteLine();
        }
    }
}
