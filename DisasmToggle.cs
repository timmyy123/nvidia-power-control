using System;
using System.IO;

class Program {
    static void Main() {
        byte[] data = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int offset = 0x8F0F0;
        int end = 0x8F250;

        Console.WriteLine("Instructions 0x8F0F0 to 0x8F250:");
        for (int i = offset; i < end; ) {
            Console.Write("{0:X6}: ", i);
            byte b = data[i];
            if (b == 0xFF && data[i+1] == 0x15) {
                Console.WriteLine("call [iat 0x{0:X8}]", BitConverter.ToInt32(data, i + 2));
                i += 6;
            } else if (b == 0xEB) {
                sbyte rel = (sbyte)data[i+1];
                Console.WriteLine("jmp 0x{0:X6}", i + 2 + rel);
                i += 2;
            } else if (b == 0x74) {
                sbyte rel = (sbyte)data[i+1];
                Console.WriteLine("jz 0x{0:X6}", i + 2 + rel);
                i += 2;
            } else if (b == 0x75) {
                sbyte rel = (sbyte)data[i+1];
                Console.WriteLine("jnz 0x{0:X6}", i + 2 + rel);
                i += 2;
            } else {
                Console.WriteLine("{0:X2} {1:X2}", data[i], data[i+1]);
                i++;
            }
        }
    }
}
