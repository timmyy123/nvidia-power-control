using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int[] spots = { 0x907F5, 0x90C1B, 0x91135, 0x9138D };
        foreach (int spot in spots) {
            Console.WriteLine("--- Context around 0x" + spot.ToString("X") + " ---");
            int start = spot - 32;
            int end = spot + 48;
            for (int i = start; i < end; i += 16) {
                Console.Write("{0:X6}: ", i);
                for (int j = 0; j < 16 && i + j < end; j++) {
                    Console.Write("{0:X2} ", d[i + j]);
                }
                Console.WriteLine();
            }
        }
    }
}
