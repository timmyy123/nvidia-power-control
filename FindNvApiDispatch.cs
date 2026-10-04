using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_Unlocked.exe");
        int start = 0x0C8000;
        int end = 0x0D5000;
        for (int i = start; i < end - 4; i++) {
            uint val = BitConverter.ToUInt32(d, i);
            // check for common NvAPI IDs
            if (val == 0x6FF81213 || val == 0x0F4DAE6B || val == 0x507B4B59 || val == 0x23F1B133 ||
                val == 0x82C7E552 || val == 0x43D9B26A || val == 0x68789E2A || (val & 0xFF000000) == 0x6F000000) {
                Console.WriteLine("Found NvAPI ID 0x{0:X8} at 0x{1:X6}", val, i);
            }
        }
    }
}
