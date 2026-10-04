using System;
using System.IO;

class Program {
    static void Main() {
        string target = @"c:\Users\timmy\Downloads\nvidia-power-control\mVolt+ (1).exe";
        byte[] d = File.ReadAllBytes(target);

        // 1. IsDomainVoltageSupported: B0 01 C3 90
        d[0x8D670] = 0xB0;
        d[0x8D671] = 0x01;
        d[0x8D672] = 0xC3;
        d[0x8D673] = 0x90;

        // 2. IsAltOpAvailable: B8 01 01 00 00 C3
        d[0x8D9F0] = 0xB8;
        d[0x8D9F1] = 0x01;
        d[0x8D9F2] = 0x01;
        d[0x8D9F3] = 0x00;
        d[0x8D9F4] = 0x00;
        d[0x8D9F5] = 0xC3;

        // 3. IsOvervoltageAvailable: B0 01 C3 90
        d[0x8DA70] = 0xB0;
        d[0x8DA71] = 0x01;
        d[0x8DA72] = 0xC3;
        d[0x8DA73] = 0x90;

        // 4. NOP jl at 0x8F261
        d[0x8F261] = 0x90;
        d[0x8F262] = 0x90;

        // 5. NOP jae at 0x8F2AE
        d[0x8F2AE] = 0x90;
        d[0x8F2AF] = 0x90;

        // 6. mov bl, 1 at 0x8F2B4
        d[0x8F2B4] = 0xB3;
        d[0x8F2B5] = 0x01;

        // 7. NOP jae at 0x8F404
        d[0x8F404] = 0x90;
        d[0x8F405] = 0x90;

        // 8. mov dl, 1; nop at 0x8F429
        d[0x8F429] = 0xB2;
        d[0x8F42A] = 0x01;
        d[0x8F42B] = 0x90;

        // 9. mov dl, 1; nop at 0x8F463
        d[0x8F463] = 0xB2;
        d[0x8F464] = 0x01;
        d[0x8F465] = 0x90;

        File.WriteAllBytes(target, d);
        Console.WriteLine("SUCCESS: Patched mVolt+ (1).exe with full voltage UI unlock!");
    }
}
