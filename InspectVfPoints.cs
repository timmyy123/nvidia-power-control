using System;
using System.IO;
using System.Runtime.InteropServices;

class InspectVfPoints
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

    static void Main()
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

        byte[] infoBuf = new byte[6188];
        BitConverter.GetBytes(0x0001182C).CopyTo(infoBuf, 0);
        IntPtr mInfo = Marshal.AllocHGlobal(6188);
        Marshal.Copy(infoBuf, 0, mInfo, 6188);
        infoFn(gpu, mInfo);
        Marshal.Copy(mInfo, infoBuf, 0, 6188);
        Marshal.FreeHGlobal(mInfo);
        byte[] vfMask = new byte[32];
        Array.Copy(infoBuf, 4, vfMask, 0, 32);

        byte[] stBuf = new byte[7208];
        BitConverter.GetBytes(0x00011C28).CopyTo(stBuf, 0);
        Array.Copy(vfMask, 0, stBuf, 4, 32);
        IntPtr mSt = Marshal.AllocHGlobal(7208);
        Marshal.Copy(stBuf, 0, mSt, 7208);
        stFn(gpu, mSt);
        Marshal.Copy(mSt, stBuf, 0, 7208);
        Marshal.FreeHGlobal(mSt);

        byte[] ctrlBuf = new byte[9248];
        BitConverter.GetBytes(0x00012420).CopyTo(ctrlBuf, 0);
        Array.Copy(vfMask, 0, ctrlBuf, 4, 32);
        IntPtr mCtrl = Marshal.AllocHGlobal(9248);
        Marshal.Copy(ctrlBuf, 0, mCtrl, 9248);
        getFn(gpu, mCtrl);
        Marshal.Copy(mCtrl, ctrlBuf, 0, 9248);
        Marshal.FreeHGlobal(mCtrl);

        Console.WriteLine("Index | Type | Freq(kHz) | Volt(uV) | CtrlOffset(kHz) | RawHex");
        for (int p = 0; p < 128; p++)
        {
            int stOff = 40 + p * 28;
            int ctrlOff = 68 + p * 36;
            if (stOff + 28 > stBuf.Length || ctrlOff + 36 > ctrlBuf.Length) break;
            int type = BitConverter.ToInt32(stBuf, stOff);
            int freq = BitConverter.ToInt32(stBuf, stOff + 4);
            int volt = BitConverter.ToInt32(stBuf, stOff + 8);
            int off = BitConverter.ToInt32(ctrlBuf, ctrlOff + 20);
            if (type != 0 || freq != 0 || volt != 0)
            {
                Console.WriteLine(string.Format("{0,5} | {1,4} | {2,9} | {3,8} | {4,15} | type={1}",
                    p, type, freq, volt, off));
            }
        }
    }
}
