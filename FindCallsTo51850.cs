using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int target = 0x051850;
        int count = 0;
        for (int i = 0; i < d.Length - 5; i++) {
            if (d[i] == 0xE8) {
                int rel = BitConverter.ToInt32(d, i + 1);
                if (i + 5 + rel == target) {
                    count++;
                    Console.WriteLine("Call to 0x{0:X6} at 0x{1:X6}", target, i);
                }
            }
        }
        Console.WriteLine("Total calls: " + count);
    }
}
