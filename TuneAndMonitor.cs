using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

class Program
{
    [DllImport("kernel32.dll")] static extern IntPtr LoadLibrary(string f);
    [DllImport("kernel32.dll")] static extern IntPtr GetProcAddress(IntPtr m, string n);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr QueryInterfaceDelegate(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int NvInitDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int EnumGpuDelegate(IntPtr handles, ref uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GpuBufferDelegate(IntPtr gpu, IntPtr buffer);

    private const uint IdVfInfo = 0x507B4B59;
    private const uint IdVfStatus = 0x21537AD4;
    private const uint IdVfControlGet = 0x23F1B133;
    private const uint IdVfControlSet = 0x0733E009;
    private const uint IdCurrentVoltage = 0x465F9BCF;

    static void Main(string[] args)
    {
        IntPtr shim = LoadLibrary(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvapi64.dll"));
        var QI = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(GetProcAddress(shim, "nvapi_QueryInterface"), typeof(QueryInterfaceDelegate));
        var init = (NvInitDelegate)Marshal.GetDelegateForFunctionPointer(QI(0x0150E828), typeof(NvInitDelegate));
        var enumGpu = (EnumGpuDelegate)Marshal.GetDelegateForFunctionPointer(QI(0xE5AC921F), typeof(EnumGpuDelegate));

        init();
        IntPtr handles = Marshal.AllocHGlobal(512);
        uint count = 0;
        enumGpu(handles, ref count);
        IntPtr gpu = Marshal.ReadIntPtr(handles, 0);

        var infoFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfInfo), typeof(GpuBufferDelegate));
        var stFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfStatus), typeof(GpuBufferDelegate));
        var getFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfControlGet), typeof(GpuBufferDelegate));
        var setFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfControlSet), typeof(GpuBufferDelegate));
        var voltFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdCurrentVoltage), typeof(GpuBufferDelegate));

        // 1. Info
        byte[] infoBuf = new byte[6188];
        BitConverter.GetBytes(0x0001182C).CopyTo(infoBuf, 0);
        IntPtr mInfo = Marshal.AllocHGlobal(6188);
        Marshal.Copy(infoBuf, 0, mInfo, 6188);
        infoFn(gpu, mInfo);
        Marshal.Copy(mInfo, infoBuf, 0, 6188);
        Marshal.FreeHGlobal(mInfo);
        byte[] vfMask = new byte[32];
        Array.Copy(infoBuf, 4, vfMask, 0, 32);

        // 2. Status
        byte[] stBuf = new byte[7208];
        BitConverter.GetBytes(0x00011C28).CopyTo(stBuf, 0);
        Array.Copy(vfMask, 0, stBuf, 4, 32);
        IntPtr mSt = Marshal.AllocHGlobal(7208);
        Marshal.Copy(stBuf, 0, mSt, 7208);
        stFn(gpu, mSt);
        Marshal.Copy(mSt, stBuf, 0, 7208);
        Marshal.FreeHGlobal(mSt);

        // 3. Control Get
        byte[] ctrlBuf = new byte[9248];
        BitConverter.GetBytes(0x00012420).CopyTo(ctrlBuf, 0);
        Array.Copy(vfMask, 0, ctrlBuf, 4, 32);
        IntPtr mCtrl = Marshal.AllocHGlobal(9248);
        Marshal.Copy(ctrlBuf, 0, mCtrl, 9248);
        getFn(gpu, mCtrl);
        Marshal.Copy(mCtrl, ctrlBuf, 0, 9248);
        Marshal.FreeHGlobal(mCtrl);

        if (args.Length > 0 && args[0] == "--reset")
        {
            Console.WriteLine("Resetting all offsets to 0...");
            for (int p = 1; p < 128; p++)
            {
                int ctrlOff = 68 + p * 36;
                BitConverter.GetBytes(0).CopyTo(ctrlBuf, ctrlOff + 20);
            }
            IntPtr mSet = Marshal.AllocHGlobal(9248);
            Marshal.Copy(ctrlBuf, 0, mSet, 9248);
            int setRc = setFn(gpu, mSet);
            Marshal.FreeHGlobal(mSet);
            Console.WriteLine("Reset rc = " + setRc);
            return;
        }

        // Shape curve to force voltage step up:
        // Set points up to 079 (940 mV) to -200 MHz
        // Set points 080 to 097 (945 mV to 1050 mV) to 0 MHz (Point 097 is 2610 MHz)
        // Set points 098 to 110 capped at 2610 MHz
        Console.WriteLine("Applying stepped curve: -200 MHz on <= 940 mV, 0 MHz on 945-1050 mV, cap above 1050 mV...");
        int lockFreqKhz = 2610000;
        for (int p = 1; p < 128; p++)
        {
            int stOff = 40 + p * 28;
            int type = BitConverter.ToInt32(stBuf, stOff);
            int freq = BitConverter.ToInt32(stBuf, stOff + 4);
            int volt = BitConverter.ToInt32(stBuf, stOff + 8);

            if (type == 0 && volt > 0)
            {
                int ctrlOff = 68 + p * 36;
                if (volt <= 940000)
                {
                    // Negative offset on lower curve
                    BitConverter.GetBytes(-200000).CopyTo(ctrlBuf, ctrlOff + 20);
                }
                else if (volt <= 1050000)
                {
                    // 0 offset on target region
                    BitConverter.GetBytes(0).CopyTo(ctrlBuf, ctrlOff + 20);
                }
                else
                {
                    // Flatten above 1050 mV
                    int offsetNeeded = lockFreqKhz - freq;
                    BitConverter.GetBytes(offsetNeeded).CopyTo(ctrlBuf, ctrlOff + 20);
                }
            }
        }

        IntPtr mSetApply = Marshal.AllocHGlobal(9248);
        Marshal.Copy(ctrlBuf, 0, mSetApply, 9248);
        int applyRc = setFn(gpu, mSetApply);
        Marshal.FreeHGlobal(mSetApply);
        Console.WriteLine("Apply rc = " + applyRc);

        if (applyRc == 0)
        {
            Console.WriteLine("Monitoring voltage and core clock for 5 seconds (5 samples)...");
            byte[] voltBuf = new byte[0x4C];
            BitConverter.GetBytes(0x0001004C).CopyTo(voltBuf, 0);

            for (int s = 0; s < 5; s++)
            {
                IntPtr mV = Marshal.AllocHGlobal(voltBuf.Length);
                Marshal.Copy(voltBuf, 0, mV, voltBuf.Length);
                voltFn(gpu, mV);
                Marshal.Copy(mV, voltBuf, 0, voltBuf.Length);
                Marshal.FreeHGlobal(mV);

                uint uv = BitConverter.ToUInt32(voltBuf, 0x28);
                Console.WriteLine(string.Format("Sample {0}: Voltage = {1:F4} V ({2} mV)", s + 1, uv / 1000000.0, uv / 1000));
                Thread.Sleep(1000);
            }
        }
    }
}
