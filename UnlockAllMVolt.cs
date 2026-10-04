using System;
using System.IO;

class Program {
    static void Main() {
        string source = @"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe.original";
        string targetUnlocked1 = @"c:\Users\timmy\Downloads\nvidia-power-control\mVolt_Unlocked.exe";
        string targetUnlocked2 = @"C:\Users\timmy\Downloads\mVolt_Unlocked.exe";
        string targetOrig = @"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe";
        string targetOrig2 = @"C:\Users\timmy\Downloads\mVolt+ (1).exe";

        byte[] d = File.ReadAllBytes(source);

        // 1. Write the EnableWindow hook stub at 0x18BBE0 (RVA 0x18C7E0)
        // 0x18BBE0: mov edx, 1 (BA 01 00 00 00)
        // 0x18BBE5: jmp qword ptr [rip + 0x1225] (FF 25 25 12 00 00) -> jumps to 0x18DA10 (EnableWindow IAT)
        d[0x18BBE0] = 0xBA;
        d[0x18BBE1] = 0x01;
        d[0x18BBE2] = 0x00;
        d[0x18BBE3] = 0x00;
        d[0x18BBE4] = 0x00;

        d[0x18BBE5] = 0xFF;
        d[0x18BBE6] = 0x25;
        d[0x18BBE7] = 0x25;
        d[0x18BBE8] = 0x12;
        d[0x18BBE9] = 0x00;
        d[0x18BBEA] = 0x00;

        int stubRva = 0x18C7E0;
        int enableWindowIatRva = 0x18DA10;
        int redirectedCalls = 0;

        // Redirect all FF 15 [EnableWindow] calls to our stub using E8 [rel32] 90
        for (int i = 0x400; i < 0x18B800 - 6; i++) {
            if (d[i] == 0xFF && d[i+1] == 0x15) {
                int rel = BitConverter.ToInt32(d, i + 2);
                int instrNextRva = (i + 0xC00) + 6;
                if (instrNextRva + rel == enableWindowIatRva) {
                    // Replace with E8 [rel32 to stub] 90
                    int callRel = stubRva - (instrNextRva - 1); // 5 bytes for E8 rel32
                    byte[] relBytes = BitConverter.GetBytes(callRel);
                    d[i] = 0xE8;
                    d[i+1] = relBytes[0];
                    d[i+2] = relBytes[1];
                    d[i+3] = relBytes[2];
                    d[i+4] = relBytes[3];
                    d[i+5] = 0x90; // NOP
                    redirectedCalls++;
                }
            }
        }
        Console.WriteLine("Redirected " + redirectedCalls + " calls of EnableWindow to force bEnable=TRUE stub.");

        // 2. Initialize active voltage slots to 0 instead of -1
        // 0x03079A: 48 C7 83 98 31 00 00 [FF FF FF FF] -> [00 00 00 00]
        if (d[0x03079A] == 0xFF && d[0x03079B] == 0xFF) {
            d[0x03079A] = 0x00;
            d[0x03079B] = 0x00;
            d[0x03079C] = 0x00;
            d[0x03079D] = 0x00;
            Console.WriteLine("Patched 0x03079A slot init to 0.");
        }
        // 0x0307A5: C7 83 A0 31 00 00 [FF FF FF FF] -> [00 00 00 00]
        if (d[0x0307A5] == 0xFF && d[0x0307A6] == 0xFF) {
            d[0x0307A5] = 0x00;
            d[0x0307A6] = 0x00;
            d[0x0307A7] = 0x00;
            d[0x0307A8] = 0x00;
            Console.WriteLine("Patched 0x0307A5 slot init to 0.");
        }

        // 3. NOP negative slot checks in handlers
        // Toggle Handler at 0x090827: 0F 8C DA 02 00 00 -> 6 NOPs
        for (int i = 0; i < 6; i++) d[0x090827 + i] = 0x90;

        // Edit Box Handler at 0x090C4E: 0F 8C C5 03 00 00 -> 6 NOPs
        for (int i = 0; i < 6; i++) d[0x090C4E + i] = 0x90;

        // Slider Drag Handler at 0x091168: 0F 88 9B 01 00 00 -> 6 NOPs
        for (int i = 0; i < 6; i++) d[0x091168 + i] = 0x90;
        // Slider Drag Handler at 0x091176: 0F 88 8D 01 00 00 -> 6 NOPs
        for (int i = 0; i < 6; i++) d[0x091176 + i] = 0x90;

        // Apply Handler at 0x0913BE: 0F 8C D7 01 00 00 -> 6 NOPs
        for (int i = 0; i < 6; i++) d[0x0913BE + i] = 0x90;
        // Apply Handler at 0x0913CB: 0F 8C CA 01 00 00 -> 6 NOPs
        for (int i = 0; i < 6; i++) d[0x0913CB + i] = 0x90;

        // 4. Force capability functions to return TRUE
        // 0x08D670 (IsDomainVoltageSupported): B0 01 C3 90 (mov al, 1; ret; nop)
        d[0x08D670] = 0xB0; d[0x08D671] = 0x01; d[0x08D672] = 0xC3; d[0x08D673] = 0x90;

        // 0x08D9F0 (IsAltOpAvailable): B8 01 01 00 00 C3 (mov eax, 0x101; ret)
        d[0x08D9F0] = 0xB8; d[0x08D9F1] = 0x01; d[0x08D9F2] = 0x01; d[0x08D9F3] = 0x00; d[0x08D9F4] = 0x00; d[0x08D9F5] = 0xC3;

        // 0x08DA70 (IsOvervoltageAvailable): B0 01 C3 90 (mov al, 1; ret; nop)
        d[0x08DA70] = 0xB0; d[0x08DA71] = 0x01; d[0x08DA72] = 0xC3; d[0x08DA73] = 0x90;

        // 5. Force bl=1 in 0x08F2B4
        d[0x08F261] = 0x90; d[0x08F262] = 0x90;
        d[0x08F2AE] = 0x90; d[0x08F2AF] = 0x90;
        d[0x08F2B4] = 0xB3; d[0x08F2B5] = 0x01; // mov bl, 1

        File.WriteAllBytes(targetUnlocked1, d);
        File.WriteAllBytes(targetUnlocked2, d);
        Console.WriteLine("SUCCESS: Written " + targetUnlocked1);
        Console.WriteLine("SUCCESS: Written " + targetUnlocked2);

        try {
            File.WriteAllBytes(targetOrig, d);
            Console.WriteLine("SUCCESS: Overwritten " + targetOrig);
        } catch { Console.WriteLine("targetOrig is locked (mVolt is currently running)."); }

        try {
            File.WriteAllBytes(targetOrig2, d);
            Console.WriteLine("SUCCESS: Overwritten " + targetOrig2);
        } catch { Console.WriteLine("targetOrig2 is locked (mVolt is currently running)."); }
    }
}
