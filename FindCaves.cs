using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int count = 0;
        int caveStart = -1;
        for (int i = 0x1000; i < 0x150000; i++) {
            if (d[i] == 0xCC) {
                if (caveStart == -1) caveStart = i;
            } else {
                if (caveStart != -1) {
                    int len = i - caveStart;
                    if (len >= 32) {
                        Console.WriteLine("Code cave at File offset 0x{0:X6}, length: {1} bytes", caveStart, len);
                        count++;
                        if (count > 10) break;
                    }
                    caveStart = -1;
                }
            }
        }
    }
}
