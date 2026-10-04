using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_Unlocked.exe");
        for (int i = 0x05AC70; i < 0x05ACBF; i++) {
            Console.Write("{0:X2} ", d[i]);
            if ((i + 1) % 16 == 0) Console.WriteLine();
        }
        Console.WriteLine();
    }
}
