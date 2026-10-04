using System;
using System.IO;
using System.Runtime.InteropServices;

class TestVfStatus
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
        // Test various struct sizes and versions for VfStatus
        // Common versions: v1 = 0x1xxxx, v2 = 0x2xxxx
        // Stride could be 24, 28, 32, 36, 40, 44, 48
        int[] strides = { 20, 24, 28, 32, 36, 40, 44, 48, 52, 56, 60, 64 };
        int[] hdrs = { 36, 40, 44 };
        for (uint ver = 1; ver <= 3; ver++)
        {
            foreach (int h in hdrs)
            {
                foreach (int s in strides)
                {
                    int totalSize = h + 256 * s;
                    byte[] stBuf = new byte[totalSize];
                    uint v = (ver << 16) | (uint)totalSize;
                    BitConverter.GetBytes(v).CopyTo(stBuf, 0);
                    Array.Copy(vfMask, 0, stBuf, 4, 32);

                    IntPtr m = Marshal.AllocHGlobal(totalSize);
                    Marshal.Copy(stBuf, 0, m, totalSize);
                    int rc = stFn(gpu, m);
                    Marshal.Copy(m, stBuf, 0, totalSize);
                    Marshal.FreeHGlobal(m);

                    if (rc == 0)
                    {
                        Console.WriteLine("FOUND WORKING STATUS STRUCT! ver={0}, hdr={1}, stride={2}, totalSize={3}, v=0x{4:X8}",
                            ver, h, s, totalSize, v);
                        return;
                    }
                }
            }
        }
        Console.WriteLine("No status struct found in standard search range.");
    }
}
