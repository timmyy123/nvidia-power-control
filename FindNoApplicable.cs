using System;
using System.IO;
using System.Text;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_Unlocked.exe");
        string s = Encoding.Unicode.GetString(d);
        int idx = s.IndexOf("No applicable settings are available for this control");
        if (idx >= 0) {
            Console.WriteLine("Found Unicode at byte 0x{0:X6}", idx * 2);
        } else {
            string sAscii = Encoding.ASCII.GetString(d);
            int idxA = sAscii.IndexOf("No applicable settings are available for this control");
            if (idxA >= 0) {
                Console.WriteLine("Found ASCII at byte 0x{0:X6}", idxA);
            } else {
                Console.WriteLine("String not found directly.");
            }
        }
    }
}
