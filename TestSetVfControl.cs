using System;
using System.IO;
using System.Runtime.InteropServices;

class TestSetVfControl
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
    private const uint IdVfControlSet = 0x0733E009;

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

        // 1. Get Info
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

        // 2. Get Control
        var getFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfControlGet), typeof(GpuBufferDelegate));
        byte[] ctrlBuf = new byte[VfControlSize];
        BitConverter.GetBytes(VfControlVersion).CopyTo(ctrlBuf, 0);
        Array.Copy(vfMask, 0, ctrlBuf, 4, 32);

        IntPtr mCtrl = Marshal.AllocHGlobal(VfControlSize);
        Marshal.Copy(ctrlBuf, 0, mCtrl, VfControlSize);
        int getRc = getFn(gpu, mCtrl);
        Marshal.Copy(mCtrl, ctrlBuf, 0, VfControlSize);
        Marshal.FreeHGlobal(mCtrl);

        Console.WriteLine("GetControl rc: " + getRc);
        if (getRc != 0) return;

        // 3. Test Set Control (writing back identical buffer)
        var setFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfControlSet), typeof(GpuBufferDelegate));
        IntPtr mSet = Marshal.AllocHGlobal(VfControlSize);
        Marshal.Copy(ctrlBuf, 0, mSet, VfControlSize);
        int setRc = setFn(gpu, mSet);
        Marshal.FreeHGlobal(mSet);

        Console.WriteLine("SetControl (identical buffer) rc: " + setRc);
    }
}
