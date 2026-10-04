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

        int currentDescriptor = rvaToOffset(importRva);
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
                if ((thunkValue & 0x8000000000000000UL) == 0) {
                    int funcRva = (int)(thunkValue & 0x7FFFFFFFU);
                    int funcOffset = rvaToOffset(funcRva) + 2;
                    StringBuilder fsb = new StringBuilder();
                    for (int i = funcOffset; data[i] != 0; i++) fsb.Append((char)data[i]);
                    string funcName = fsb.ToString();
                    if (iatThunkRva == 0x18DAD0 || iatThunkRva == 0x18DE10 || iatThunkRva == 0x18DDD0) {
                        Console.WriteLine("IAT RVA 0x{0:X} = {1}", iatThunkRva, funcName);
                    }
                }
                thunkOffset += 8;
                iatThunkRva += 8;
            }
            currentDescriptor += 20;
        }
    }
}
