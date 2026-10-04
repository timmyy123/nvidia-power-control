using System;
using System.IO;
using System.Text;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_Unlocked.exe");
        string s = Encoding.Unicode.GetString(d);
        int idx = s.IndexOf("Setting applied and verified");
        if (idx >= 0) {
            Console.WriteLine("Found Unicode at byte 0x{0:X6}", idx * 2);
        }
    }
}
