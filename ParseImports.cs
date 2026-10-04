using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

class Program {
    static void Main() {
        byte[] data = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int pe = BitConverter.ToInt32(data, 0x3C);
        int numSections = BitConverter.ToInt16(data, pe + 6);
        int optHeaderOffset = pe + 24;
        long imageBase = BitConverter.ToInt64(data, optHeaderOffset + 24);
        int importRva = BitConverter.ToInt32(data, optHeaderOffset + 120);
        int sectionHeaderOffset = optHeaderOffset + 240;

        Console.WriteLine("ImageBase: 0x" + imageBase.ToString("X"));
        Console.WriteLine("Import RVA: 0x" + importRva.ToString("X"));

        // Helper to convert RVA to FileOffset
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

        // Parse Imports
        int importOffset = rvaToOffset(importRva);
        Console.WriteLine("Import Offset: 0x" + importOffset.ToString("X"));

        int currentDescriptor = importOffset;
        while (true) {
            int originalFirstThunk = BitConverter.ToInt32(data, currentDescriptor);
            int nameRva = BitConverter.ToInt32(data, currentDescriptor + 12);
            int firstThunk = BitConverter.ToInt32(data, currentDescriptor + 16);

            if (nameRva == 0) break;

            int nameOffset = rvaToOffset(nameRva);
            StringBuilder sb = new StringBuilder();
            for (int i = nameOffset; data[i] != 0; i++) sb.Append((char)data[i]);
            string dllName = sb.ToString();

            Console.WriteLine("DLL: " + dllName);

            // Read thunks
            int thunkOffset = rvaToOffset(originalFirstThunk != 0 ? originalFirstThunk : firstThunk);
            while (true) {
                ulong thunkValue = BitConverter.ToUInt64(data, thunkOffset);
                if (thunkValue == 0) break;
                if ((thunkValue & 0x8000000000000000UL) == 0) {
                    int funcRva = (int)(thunkValue & 0x7FFFFFFFU);
                    int funcOffset = rvaToOffset(funcRva) + 2;
                    StringBuilder fsb = new StringBuilder();
                    for (int i = funcOffset; data[i] != 0; i++) fsb.Append((char)data[i]);
                    string funcName = fsb.ToString();
                    if (funcName.Contains("Window") || funcName.Contains("Enable") || funcName.Contains("Track") || funcName.Contains("Edit")) {
                        Console.WriteLine("  Func: " + funcName);
                    }
                }
                thunkOffset += 8;
            }

            currentDescriptor += 20;
        }
    }
}
