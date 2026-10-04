using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int start = 0x08ECD0;
        int end = 0x08ED40;
        Console.WriteLine("Instructions 0x08ECD0 to 0x08ED40:");
        for (int i = start; i < end; ) {
            Console.Write("{0:X6}: ", i);
            byte b = d[i];
            if (b == 0xFF && d[i+1] == 0x15) {
                Console.WriteLine("call [iat 0x{0:X8}]", BitConverter.ToInt32(d, i + 2));
                i += 6;
            } else if (b == 0xE8) {
                int rel = BitConverter.ToInt32(d, i + 1);
                Console.WriteLine("call 0x{0:X6}", i + 5 + rel);
                i += 5;
            } else {
                Console.WriteLine("{0:X2} {1:X2}", d[i], d[i+1]);
                i++;
            }
        }
    }
}
