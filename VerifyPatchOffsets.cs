using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        Console.WriteLine("08F260: {0:X2} {1:X2} {2:X2} {3:X2}", d[0x8F260], d[0x8F261], d[0x8F262], d[0x8F263]);
        Console.WriteLine("08F2AC: {0:X2} {1:X2} {2:X2} {3:X2}", d[0x8F2AC], d[0x8F2AD], d[0x8F2AE], d[0x8F2AF]);
        Console.WriteLine("08F400: {0:X2} {1:X2} {2:X2} {3:X2} {4:X2} {5:X2}", d[0x8F400], d[0x8F401], d[0x8F402], d[0x8F403], d[0x8F404], d[0x8F405]);
    }
}
