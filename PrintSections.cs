using System;
using System.IO;

class Program {
    static void Main() {
        byte[] d = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int pe = BitConverter.ToInt32(d, 0x3C);
        int opt = pe + 24;
        int numSections = BitConverter.ToInt16(d, pe + 6);
        int secHeader = opt + 240;

        for (int i = 0; i < numSections; i++) {
            int off = secHeader + i * 40;
            string name = "";
            for (int j = 0; j < 8; j++) if (d[off + j] != 0) name += (char)d[off + j];
            int virtSize = BitConverter.ToInt32(d, off + 8);
            int virtAddr = BitConverter.ToInt32(d, off + 12);
            int rawSize = BitConverter.ToInt32(d, off + 16);
            int rawAddr = BitConverter.ToInt32(d, off + 20);
            Console.WriteLine("Section {0,-8} VirtAddr=0x{1:X8} VirtSize=0x{2:X8} RawAddr=0x{3:X8} RawSize=0x{4:X8}",
                name, virtAddr, virtSize, rawAddr, rawSize);
        }
    }
}
