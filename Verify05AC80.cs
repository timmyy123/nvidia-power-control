using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_Unlocked.exe");
        Console.WriteLine("0x05AC83: {0:X2} {1:X2} (expected 75 39)", d[0x05AC83], d[0x05AC84]);
        Console.WriteLine("0x05AC8C: {0:X2} {1:X2} (expected 75 30)", d[0x05AC8C], d[0x05AC8D]);
        Console.WriteLine("0x05AC95: {0:X2} {1:X2} (expected 74 27)", d[0x05AC95], d[0x05AC96]);
        Console.WriteLine("0x05AC9E: {0:X2} {1:X2} (expected 75 4A)", d[0x05AC9E], d[0x05AC9F]);
    }
}
