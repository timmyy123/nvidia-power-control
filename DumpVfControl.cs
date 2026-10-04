using System;
using System.IO;
using System.Runtime.InteropServices;

class DumpVfControl
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
    private const uint IdVfControlGet = 0x23F1B133;
    private const uint VfInfoVersion = 0x0001182C;
    private const int VfInfoSize = 6188;
    private const uint VfControlVersion = 0x00012420;
    private const int VfControlSize = 9248;

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

        var ctrlFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfControlGet), typeof(GpuBufferDelegate));
        byte[] ctrlBuf = new byte[VfControlSize];
        BitConverter.GetBytes(VfControlVersion).CopyTo(ctrlBuf, 0);
        Array.Copy(vfMask, 0, ctrlBuf, 4, 32);

        IntPtr mCtrl = Marshal.AllocHGlobal(VfControlSize);
        Marshal.Copy(ctrlBuf, 0, mCtrl, VfControlSize);
        ctrlFn(gpu, mCtrl);
        Marshal.Copy(mCtrl, ctrlBuf, 0, VfControlSize);
        Marshal.FreeHGlobal(mCtrl);

        int hdr = 0x24; // 36 bytes header (4 version + 32 mask)
        int entrySize = 36;
        Console.WriteLine("Dumping first 10 and last 10 entries of VfControl (header={0}, entrySize={1}):", hdr, entrySize);
        for (int p = 0; p < 130; p++)
        {
            int off = hdr + p * entrySize;
            if (p < 5 || (p >= 60 && p <= 65) || p >= 120)
            {
                Console.Write("Point {0:D3} (off 0x{1:X4}): ", p, off);
                for (int k = 0; k < entrySize; k += 4)
                {
                    int val = BitConverter.ToInt32(ctrlBuf, off + k);
                    Console.Write("{0,8} ", val);
                }
                Console.WriteLine();
            }
        }
    }
}
