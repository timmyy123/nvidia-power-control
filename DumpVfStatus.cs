using System;
using System.IO;
using System.Runtime.InteropServices;

class DumpVfStatus
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr QueryInterfaceDelegate(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvInitDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumGpuDelegate(IntPtr handles, ref uint count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GpuBufferDelegate(IntPtr gpu, IntPtr buffer);

    private const uint IdVfInfo = 0x507B4B59;
    private const uint IdVfStatus = 0x21537AD4;
    private const uint VfInfoVersion = 0x0001182C;
    private const int VfInfoSize = 6188;
    private const uint VfStatusVersion = 0x00011C28;
    private const int VfStatusSize = 7208;

    static void Main()
    {
        IntPtr shim = LoadLibrary(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvapi64.dll"));
        var QI = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(GetProcAddress(shim, "nvapi_QueryInterface"), typeof(QueryInterfaceDelegate));
        var init = (NvInitDelegate)Marshal.GetDelegateForFunctionPointer(QI(0x0150E828), typeof(NvInitDelegate));
        var enumGpu = (EnumGpuDelegate)Marshal.GetDelegateForFunctionPointer(QI(0xE5AC921F), typeof(EnumGpuDelegate));

        init();
        IntPtr handles = Marshal.AllocHGlobal(IntPtr.Size * 64);
        uint count = 0;
        enumGpu(handles, ref count);
        IntPtr gpu = Marshal.ReadIntPtr(handles, 0);

        var infoFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfInfo), typeof(GpuBufferDelegate));
        byte[] infoBuf = new byte[VfInfoSize];
        BitConverter.GetBytes(VfInfoVersion).CopyTo(infoBuf, 0);
        IntPtr mInfo = Marshal.AllocHGlobal(VfInfoSize);
        Marshal.Copy(infoBuf, 0, mInfo, VfInfoSize);
        infoFn(gpu, mInfo);
        Marshal.Copy(mInfo, infoBuf, 0, VfInfoSize);
        Marshal.FreeHGlobal(mInfo);

        byte[] vfMask = new byte[32];
        Array.Copy(infoBuf, 4, vfMask, 0, 32);

        var stFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfStatus), typeof(GpuBufferDelegate));
        byte[] stBuf = new byte[VfStatusSize];
        BitConverter.GetBytes(VfStatusVersion).CopyTo(stBuf, 0);
        Array.Copy(vfMask, 0, stBuf, 4, 32);

        IntPtr mSt = Marshal.AllocHGlobal(VfStatusSize);
        Marshal.Copy(stBuf, 0, mSt, VfStatusSize);
        stFn(gpu, mSt);
        Marshal.Copy(mSt, stBuf, 0, VfStatusSize);
        Marshal.FreeHGlobal(mSt);

        int hdr = 40;
        int stride = 28;
        Console.WriteLine("Index | Freq(kHz) | Volt(uV) / Fields...");
        for (int p = 0; p < 133; p++)
        {
            int off = hdr + p * stride;
            uint f0 = BitConverter.ToUInt32(stBuf, off);
            uint f1 = BitConverter.ToUInt32(stBuf, off + 4);
            uint f2 = BitConverter.ToUInt32(stBuf, off + 8);
            uint f3 = BitConverter.ToUInt32(stBuf, off + 12);
            uint f4 = BitConverter.ToUInt32(stBuf, off + 16);
            uint f5 = BitConverter.ToUInt32(stBuf, off + 20);
            uint f6 = BitConverter.ToUInt32(stBuf, off + 24);

            Console.WriteLine("Point {0:D3}: {1,10} {2,10} {3,10} {4,10} {5,10} {6,10} {7,10}",
                p, f0, f1, f2, f3, f4, f5, f6);
        }
    }
}
