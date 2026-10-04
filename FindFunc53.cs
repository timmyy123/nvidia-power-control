using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        for (int i = 0x53664; i >= 0x52000; i--) {
            if (d[i] == 0xCC && d[i+1] != 0xCC) {
                Console.WriteLine("Function start after CC at 0x{0:X6}", i + 1);
                break;
            }
        }
    }
}
