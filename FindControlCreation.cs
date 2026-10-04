using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int[] targets = { 0x30E8, 0x30F0, 0x3118, 0x3120, 0x3128, 0x3110, 0x3138, 0x3130 };
        foreach (var t in targets) {
            byte b0 = (byte)(t & 0xFF);
            byte b1 = (byte)((t >> 8) & 0xFF);
            for (int i = 0; i < d.Length - 4; i++) {
                if (d[i] == b0 && d[i+1] == b1 && d[i+2] == 0x00 && d[i+3] == 0x00) {
                    Console.WriteLine("Ref to 0x{0:X4} at 0x{1:X6}: {2:X2} {3:X2} {4:X2} {5:X2}", 
                        t, i, d[i-3], d[i-2], d[i-1], d[i]);
                }
            }
        }
    }
}
