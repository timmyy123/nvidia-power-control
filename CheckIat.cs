using System;
using System.IO;

class Program {
    static void Main() {
        byte[] data = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int pe = BitConverter.ToInt32(data, 0x3C);
        int optHeaderOffset = pe + 24;
        int numSections = BitConverter.ToInt16(data, pe + 6);
        int sectionHeaderOffset = optHeaderOffset + 240;

        Func<int, int> rvaToOffset = (rva) => {
            for (int i = 0; i < numSections; i++) {
                int secOffset = sectionHeaderOffset + i * 40;
                int virtSize = BitConverter.ToInt32(data, secOffset + 8);
                int virtAddr = BitConverter.ToInt32(data, secOffset + 12);
                int rawSize = BitConverter.ToInt32(data, secOffset + 16);
                int rawAddr = BitConverter.ToInt32(data, secOffset + 20);

                if (rva >= virtAddr && rva < virtAddr + Math.Max(virtSize, rawSize)) {
                    return rawAddr + (rva - virtAddr);
                }
            }
            return -1;
        };

        // Check IAT calls
        int[] addrs = { 0x8F11C, 0x8F14E, 0x8F198, 0x8F1F2, 0x8F237, 0x8F127, 0x8F159, 0x8F1A3, 0x8F1FD, 0x8F242 };
        foreach (var addr in addrs) {
            int rel = BitConverter.ToInt32(data, addr + 2);
            int targetRva = (addr + 0x1000) + 6 + rel; // raw offset is 0x1000 less than RVA
            Console.WriteLine("Call at 0x{0:X}: target IAT RVA 0x{1:X}", addr, targetRva);
        }
    }
}
