using System;
using System.IO;
using System.Text;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        string s = Encoding.Unicode.GetString(d);
        int idx = s.IndexOf("Unavailable on this GPU");
        if (idx >= 0) {
            int byteOffset = idx * 2;
            Console.WriteLine("Found 'Unavailable on this GPU' at byte offset 0x{0:X6}", byteOffset);
        }
    }
}
