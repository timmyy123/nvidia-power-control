using System;
using System.IO;
using System.Text;

class Program {
    static void Main() {
        byte[] data = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int pe = BitConverter.ToInt32(data, 0x3C);
        int optHeaderOffset = pe + 24;
        int importRva = BitConverter.ToInt32(data, optHeaderOffset + 120);
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

        int[] offsets = { 0x8F11C, 0x8F14E, 0x8F198, 0x8F1F2, 0x8F237, 0x8F127, 0x8F159, 0x8F1A3, 0x8F1FD, 0x8F242 };

        foreach (var off in offsets) {
            int rel = BitConverter.ToInt32(data, off + 2);
            int targetRva = (off + 0xC00) + 6 + rel;

            // Search which IAT this is
            int currentDescriptor = rvaToOffset(importRva);
            bool found = false;
            while (true) {
                int originalFirstThunk = BitConverter.ToInt32(data, currentDescriptor);
                int nameRva = BitConverter.ToInt32(data, currentDescriptor + 12);
                int firstThunk = BitConverter.ToInt32(data, currentDescriptor + 16);
                if (nameRva == 0) break;

                int thunkOffset = rvaToOffset(originalFirstThunk != 0 ? originalFirstThunk : firstThunk);
                int iatThunkRva = firstThunk;

                while (true) {
                    ulong thunkValue = BitConverter.ToUInt64(data, thunkOffset);
                    if (thunkValue == 0) break;
                    if (iatThunkRva == targetRva) {
                        int funcRva = (int)(thunkValue & 0x7FFFFFFFU);
                        int funcOffset = rvaToOffset(funcRva) + 2;
                        StringBuilder fsb = new StringBuilder();
                        for (int i = funcOffset; data[i] != 0; i++) fsb.Append((char)data[i]);
                        Console.WriteLine("Offset 0x{0:X}: call [{1}]", off, fsb.ToString());
                        found = true;
                        break;
                    }
                    thunkOffset += 8;
                    iatThunkRva += 8;
                }
                if (found) break;
                currentDescriptor += 20;
            }
        }
    }
}
