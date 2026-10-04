using System;
using System.IO;

class Program {
    static void Main() {
        string src = @"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_Unlocked.exe";
        string out1 = @"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_DirectApply.exe";
        string out2 = @"C:\Users\timmy\Downloads\mVolt_DirectApply.exe";
        string outUnlocked1 = @"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_Unlocked.exe";
        string outUnlocked2 = @"C:\Users\timmy\Downloads\mVolt_Unlocked.exe";

        byte[] d = File.ReadAllBytes(src);

        // 1. NOP out all 4 error jumps:
        // 0x05AC7C: 74 42 -> 90 90
        d[0x05AC7C] = 0x90;
        d[0x05AC7D] = 0x90;

        // 0x05AC85: 75 39 -> 90 90
        d[0x05AC85] = 0x90;
        d[0x05AC86] = 0x90;

        // 0x05AC8E: 75 30 -> 90 90
        d[0x05AC8E] = 0x90;
        d[0x05AC8F] = 0x90;

        // 0x05AC97: 74 27 -> 90 90
        d[0x05AC97] = 0x90;
        d[0x05AC98] = 0x90;

        // 2. Unconditional jump to apply dispatch:
        // 0x05ACA0: 75 4A -> EB 4A
        d[0x05ACA0] = 0xEB;
        d[0x05ACA1] = 0x4A;

        File.WriteAllBytes(out1, d);
        File.WriteAllBytes(out2, d);
        Console.WriteLine("SUCCESS: Written " + out1);
        Console.WriteLine("SUCCESS: Written " + out2);

        try {
            File.WriteAllBytes(outUnlocked1, d);
            Console.WriteLine("SUCCESS: Overwritten " + outUnlocked1);
        } catch { Console.WriteLine("outUnlocked1 is locked by running process."); }

        try {
            File.WriteAllBytes(outUnlocked2, d);
            Console.WriteLine("SUCCESS: Overwritten " + outUnlocked2);
        } catch { Console.WriteLine("outUnlocked2 is locked by running process."); }
    }
}
