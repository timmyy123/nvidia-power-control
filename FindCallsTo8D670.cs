using System;
using System.IO;

class Program {
    static void Main() {
        byte[] data = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int targetOffset = 0x8D670;
        int callCount = 0;

        for (int i = 0; i < data.Length - 5; i++) {
            if (data[i] == 0xE8) {
                int rel = BitConverter.ToInt32(data, i + 1);
                int dest = i + 5 + rel;
                if (dest == targetOffset) {
                    callCount++;
                    Console.WriteLine("Call #{0} to 0x8D670 at File offset 0x{1:X}", callCount, i);
                }
            }
        }
        Console.WriteLine("Total calls to 0x8D670: " + callCount);
    }
}
