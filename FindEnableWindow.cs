using System;
using System.IO;

class Program {
    static void Main() {
        byte[] data = File.ReadAllBytes(@"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe");
        int pe = BitConverter.ToInt32(data, 0x3C);
        int optHeaderOffset = pe + 24;
        long imageBase = BitConverter.ToInt64(data, optHeaderOffset + 24);
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

        // Find EnableWindow IAT RVA
        int currentDescriptor = rvaToOffset(importRva);
        int enableWindowIatRva = 0;

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
                    string name = "";
                    for (int i = funcOffset; data[i] != 0; i++) name += (char)data[i];
                    if (name == "EnableWindow") {
                        enableWindowIatRva = iatThunkRva;
                        Console.WriteLine("Found EnableWindow IAT RVA: 0x" + enableWindowIatRva.ToString("X"));
                        break;
                    }
                }
                thunkOffset += 8;
                iatThunkRva += 8;
            }
            if (enableWindowIatRva != 0) break;
            currentDescriptor += 20;
        }

        if (enableWindowIatRva == 0) {
            Console.WriteLine("EnableWindow not found");
            return;
        }

        // Find .text section
        int textVirtAddr = BitConverter.ToInt32(data, sectionHeaderOffset + 12);
        int textVirtSize = BitConverter.ToInt32(data, sectionHeaderOffset + 8);
        int textRawOffset = BitConverter.ToInt32(data, sectionHeaderOffset + 20);
        int textRawSize = BitConverter.ToInt32(data, sectionHeaderOffset + 16);

        Console.WriteLine(".text RVA: 0x" + textVirtAddr.ToString("X") + " size: 0x" + textRawSize.ToString("X"));

        // Scan .text for: FF 15 [rel32] (call qword ptr [rip + rel32]) or FF 25 [rel32] (jmp)
        int callCount = 0;
        for (int i = 0; i < textRawSize - 6; i++) {
            byte b0 = data[textRawOffset + i];
            byte b1 = data[textRawOffset + i + 1];
            if (b0 == 0xFF && (b1 == 0x15 || b1 == 0x25)) {
                int rel32 = BitConverter.ToInt32(data, textRawOffset + i + 2);
                int instrNextRva = textVirtAddr + i + 6;
                int targetRva = instrNextRva + rel32;
                if (targetRva == enableWindowIatRva) {
                    callCount++;
                    Console.WriteLine("Call #{0} to EnableWindow at RVA 0x{1:X} (File offset 0x{2:X})", 
                        callCount, textVirtAddr + i, textRawOffset + i);
                    
                    // Print previous 16 bytes
                    int start = Math.Max(0, textRawOffset + i - 16);
                    Console.Write("  Context before: ");
                    for (int j = start; j < textRawOffset + i; j++) {
                        Console.Write("{0:X2} ", data[j]);
                    }
                    Console.WriteLine();
                }
            }
        }
        Console.WriteLine("Total calls to EnableWindow: " + callCount);
    }
}
