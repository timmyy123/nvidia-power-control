using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int pe = BitConverter.ToInt32(d, 0x3C);
        int optHeaderOffset = pe + 24;
        int numSections = BitConverter.ToInt16(d, pe + 6);
        int sectionHeaderOffset = optHeaderOffset + 240;

        Func<int, int> offsetToRva = (offset) => {
            for (int i = 0; i < numSections; i++) {
                int secOffset = sectionHeaderOffset + i * 40;
                int virtSize = BitConverter.ToInt32(d, secOffset + 8);
                int virtAddr = BitConverter.ToInt32(d, secOffset + 12);
                int rawSize = BitConverter.ToInt32(d, secOffset + 16);
                int rawAddr = BitConverter.ToInt32(d, secOffset + 20);

                if (offset >= rawAddr && offset < rawAddr + rawSize) {
                    return virtAddr + (offset - rawAddr);
                }
            }
            return -1;
        };

        int strRva = offsetToRva(0x1A16E8);
        Console.WriteLine("String RVA: 0x" + strRva.ToString("X"));

        for (int i = 0; i < 0x150000; i++) {
            if (d[i] == 0x48 && d[i+1] == 0x8D) { // lea r64, [rip+rel32]
                int rel = BitConverter.ToInt32(d, i + 3);
                int rvaHere = offsetToRva(i);
                if (rvaHere + 7 + rel == strRva) {
                    Console.WriteLine("Found LEA to string at File offset 0x{0:X6}", i);
                }
            }
        }
    }
}
