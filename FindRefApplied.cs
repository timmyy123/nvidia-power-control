using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_Unlocked.exe");
        int targetRva = 0x1A4130;

        for (int i = 0x400; i < 0x18B800 - 7; i++) {
            if (d[i] == 0x48 && d[i+1] == 0x8D) {
                int rel = BitConverter.ToInt32(d, i + 3);
                int instrNextRva = (i + 0xC00) + 7;
                if (instrNextRva + rel == targetRva) {
                    Console.WriteLine("Ref to string at File offset 0x{0:X6}", i);
                }
            }
        }
    }
}
