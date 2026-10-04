using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int off = 0x18C610;
        ulong val = BitConverter.ToUInt64(d, off);
        Console.WriteLine("At 0x{0:X6}: 0x{1:X16}", off, val);
    }
}
