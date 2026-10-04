using System;
using System.IO;

class Program {
    static void Main() {
        byte[] data = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int offset = 0x8F250;
        int end = 0x8F4A0;

        Console.WriteLine("Instructions around 0x8F250:");
        for (int i = offset; i < end; ) {
            // print bytes
            Console.Write("{0:X6}: ", i);
            int len = 0;
            // simple heuristic print
            byte b = data[i];
            if (b == 0xFF && data[i+1] == 0x15) {
                Console.WriteLine("call EnableWindow (rel: {0:X8})", BitConverter.ToInt32(data, i + 2));
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
            } else if (b == 0x7C) {
                sbyte rel = (sbyte)data[i+1];
                Console.WriteLine("jl 0x{0:X6}", i + 2 + rel);
                i += 2;
            } else if (b == 0x73) {
                sbyte rel = (sbyte)data[i+1];
                Console.WriteLine("jae 0x{0:X6}", i + 2 + rel);
                i += 2;
            } else if (b == 0x33 && data[i+1] == 0xD2) {
                Console.WriteLine("xor edx, edx (edx=0)");
                i += 2;
            } else if (b == 0xBA && data[i+1] == 0x01 && data[i+2] == 0x00 && data[i+3] == 0x00 && data[i+4] == 0x00) {
                Console.WriteLine("mov edx, 1 (edx=1)");
                i += 5;
            } else {
                Console.WriteLine("{0:X2} {1:X2}", data[i], data[i+1]);
                i++;
            }
        }
    }
}
