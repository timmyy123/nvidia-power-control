using System;
using System.IO;
using System.Runtime.InteropServices;

class InspectVfControlHeader
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

        Console.WriteLine("Header 0x00 to 0x44:");
        for (int i = 0; i < 0x44; i += 16)
        {
            int len = Math.Min(16, 0x44 - i);
            Console.WriteLine("{0:X2}: {1}", i, BitConverter.ToString(ctrlBuf, i, len));
        }

        Console.WriteLine("\nFirst 5 points at offset 0x44, stride 36:");
        for (int p = 0; p < 5; p++)
        {
            int off = 0x44 + p * 36;
            Console.WriteLine("Point {0} (0x{1:X4}): {2}", p, off, BitConverter.ToString(ctrlBuf, off, 36));
            int offsetKhz = BitConverter.ToInt32(ctrlBuf, off + 20);
            Console.WriteLine("   -> freq_offset_kHz at +20: {0}", offsetKhz);
        }

        Console.WriteLine("\nPoint 79 (940 mV):");
        int off79 = 0x44 + 79 * 36;
        Console.WriteLine("Point 79 (0x{0:X4}): {1}", off79, BitConverter.ToString(ctrlBuf, off79, 36));
        Console.WriteLine("   -> freq_offset_kHz at +20: {0}", BitConverter.ToInt32(ctrlBuf, off79 + 20));
    }
}
