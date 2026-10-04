using System;
using System.IO;
using System.Runtime.InteropServices;

class Program
{
    [DllImport("kernel32.dll")] static extern IntPtr LoadLibrary(string f);
    [DllImport("kernel32.dll")] static extern IntPtr GetProcAddress(IntPtr m, string n);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr QueryInterfaceDelegate(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int NvInitDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int EnumGpuDelegate(IntPtr handles, ref uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GpuBufferDelegate(IntPtr gpu, IntPtr buffer);

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

        var infoFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(0x507B4B59), typeof(GpuBufferDelegate));
        byte[] infoBuf = new byte[6188];
        BitConverter.GetBytes(0x0001182C).CopyTo(infoBuf, 0);
        IntPtr mInfo = Marshal.AllocHGlobal(6188);
        Marshal.Copy(infoBuf, 0, mInfo, 6188);
        infoFn(gpu, mInfo);
        Marshal.Copy(mInfo, infoBuf, 0, 6188);

        byte[] vfMask = new byte[32];
        Array.Copy(infoBuf, 4, vfMask, 0, 32);

        var stFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(0x21537AD4), typeof(GpuBufferDelegate));
        byte[] stBuf = new byte[7208];
        BitConverter.GetBytes(0x00011C28).CopyTo(stBuf, 0);
        Array.Copy(vfMask, 0, stBuf, 4, 32);
        IntPtr mSt = Marshal.AllocHGlobal(7208);
        Marshal.Copy(stBuf, 0, mSt, 7208);
        stFn(gpu, mSt);
        Marshal.Copy(mSt, stBuf, 0, 7208);

        Console.WriteLine("Status Header (40 bytes):");
        for (int i = 0; i < 40; i += 4)
            Console.WriteLine(string.Format("  +{0:D2}: 0x{1:X8} ({2})", i, BitConverter.ToUInt32(stBuf, i), BitConverter.ToInt32(stBuf, i)));

        Console.WriteLine("\nDumping Status Entries 75 to 85 (28 bytes each):");
        for (int p = 75; p <= 85; p++)
        {
            int off = 40 + p * 28;
            Console.WriteLine(string.Format("--- Point {0:D3} (offset 0x{1:X4}) ---", p, off));
            for (int j = 0; j < 28; j += 4)
            {
                uint uval = BitConverter.ToUInt32(stBuf, off + j);
                int ival = BitConverter.ToInt32(stBuf, off + j);
                Console.WriteLine(string.Format("  +{0:D2}: 0x{1:X8} ({2})", j, uval, ival));
            }
        }
    }
}
