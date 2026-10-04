using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_Unlocked.exe");
        int start = 0x05AC60;
        int end = 0x05AD00;
        Console.WriteLine("Instructions 0x05AC60 to 0x05AD00:");
        for (int i = start; i < end; i += 16) {
            Console.Write("{0:X6}: ", i);
            for (int j = 0; j < 16 && i + j < end; j++) {
                Console.Write("{0:X2} ", d[i + j]);
            }
            Console.WriteLine();
        }
    }
}
