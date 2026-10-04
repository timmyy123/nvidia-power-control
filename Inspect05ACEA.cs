using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_Unlocked.exe");
        int start = 0x05ACEA;
        int end = 0x05AD70;
        Console.WriteLine("Instructions 0x05ACEA to 0x05AD70:");
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
            } else if (b == 0xEB) {
                sbyte rel = (sbyte)d[i+1];
                Console.WriteLine("jmp 0x{0:X6}", i + 2 + rel);
                i += 2;
            } else if (b == 0x74) {
                sbyte rel = (sbyte)d[i+1];
                Console.WriteLine("jz 0x{0:X6}", i + 2 + rel);
                i += 2;
            } else if (b == 0x75) {
                sbyte rel = (sbyte)d[i+1];
                Console.WriteLine("jnz 0x{0:X6}", i + 2 + rel);
                i += 2;
            } else {
                Console.WriteLine("{0:X2} {1:X2}", d[i], d[i+1]);
                i++;
            }
        }
    }
}
