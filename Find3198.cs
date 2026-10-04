using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        for (int i = 0; i < d.Length - 4; i++) {
            if (d[i] == 0x98 && d[i+1] == 0x31 && d[i+2] == 0x00 && d[i+3] == 0x00) {
                Console.WriteLine("Ref to 3198h at 0x{0:X6}: {1:X2} {2:X2} {3:X2} {4:X2}", 
                    i, d[i-3], d[i-2], d[i-1], d[i]);
            }
            if (d[i] == 0x9C && d[i+1] == 0x31 && d[i+2] == 0x00 && d[i+3] == 0x00) {
                Console.WriteLine("Ref to 319Ch at 0x{0:X6}: {1:X2} {2:X2} {3:X2} {4:X2}", 
                    i, d[i-3], d[i-2], d[i-1], d[i]);
            }
        }
    }
}
