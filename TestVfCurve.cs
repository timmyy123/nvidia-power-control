using System;
using System.IO;
using System.Runtime.InteropServices;

class TestVfCurve
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
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
    private const uint IdVfStatus = 0x21537AD4;

    private const uint VfInfoVersion = 0x0001182C;
    private const int VfInfoSize = 6188;
    private const uint VfControlVersion = 0x00012420;
    private const int VfControlSize = 9248;

    static void Main()
    {
        Console.WriteLine("=== Testing NVAPI VF Curve on RTX 4090 Mobile ===");
        IntPtr shim = LoadLibrary(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvapi64.dll"));
        if (shim == IntPtr.Zero) { Console.WriteLine("nvapi64.dll failed to load"); return; }
        IntPtr qiPtr = GetProcAddress(shim, "nvapi_QueryInterface");
        if (qiPtr == IntPtr.Zero) { Console.WriteLine("nvapi_QueryInterface not found"); return; }

        var QI = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(qiPtr, typeof(QueryInterfaceDelegate));
        var init = (NvInitDelegate)Marshal.GetDelegateForFunctionPointer(QI(0x0150E828), typeof(NvInitDelegate));
        var enumGpu = (EnumGpuDelegate)Marshal.GetDelegateForFunctionPointer(QI(0xE5AC921F), typeof(EnumGpuDelegate));

        int rc = init();
        if (rc != 0) { Console.WriteLine("init failed: " + rc); return; }

        IntPtr handles = Marshal.AllocHGlobal(IntPtr.Size * 64);
        uint count = 0;
        enumGpu(handles, ref count);
        Console.WriteLine("GPU count: " + count);
        if (count == 0) return;
        IntPtr gpu = Marshal.ReadIntPtr(handles, 0);

        // 1. ClkVfPointsGetInfo
        IntPtr infoPtr = QI(IdVfInfo);
        Console.WriteLine("IdVfInfo: " + (infoPtr != IntPtr.Zero ? "found" : "null"));
        byte[] infoBuf = new byte[VfInfoSize];
        BitConverter.GetBytes(VfInfoVersion).CopyTo(infoBuf, 0);
        byte[] vfMask = new byte[32];

        if (infoPtr != IntPtr.Zero)
        {
            var infoFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(infoPtr, typeof(GpuBufferDelegate));
            IntPtr m = Marshal.AllocHGlobal(VfInfoSize);
            Marshal.Copy(infoBuf, 0, m, VfInfoSize);
            int infoRc = infoFn(gpu, m);
            Marshal.Copy(m, infoBuf, 0, VfInfoSize);
            Marshal.FreeHGlobal(m);

            Console.WriteLine("ClkVfPointsGetInfo rc: " + infoRc);
            if (infoRc == 0)
            {
                Console.WriteLine("ClkVfPointsGetInfo version: 0x{0:X8}", BitConverter.ToUInt32(infoBuf, 0));
                Array.Copy(infoBuf, 4, vfMask, 0, 32);
                Console.WriteLine("Active mask: " + BitConverter.ToString(vfMask));
            }
        }

        // 2. ClkVfPointsGetControl
        IntPtr ctrlPtr = QI(IdVfControlGet);
        Console.WriteLine("IdVfControlGet: " + (ctrlPtr != IntPtr.Zero ? "found" : "null"));
        if (ctrlPtr != IntPtr.Zero)
        {
            byte[] ctrlBuf = new byte[VfControlSize];
            BitConverter.GetBytes(VfControlVersion).CopyTo(ctrlBuf, 0);
            Array.Copy(vfMask, 0, ctrlBuf, 4, 32);

            var ctrlFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(ctrlPtr, typeof(GpuBufferDelegate));
            IntPtr m = Marshal.AllocHGlobal(VfControlSize);
            Marshal.Copy(ctrlBuf, 0, m, VfControlSize);
            int ctrlRc = ctrlFn(gpu, m);
            Marshal.Copy(m, ctrlBuf, 0, VfControlSize);
            Marshal.FreeHGlobal(m);

            Console.WriteLine("ClkVfPointsGetControl rc: " + ctrlRc);
            if (ctrlRc == 0)
            {
                Console.WriteLine("ClkVfPointsGetControl version: 0x{0:X8}", BitConverter.ToUInt32(ctrlBuf, 0));
                for (int i = 0; i < 96; i += 16)
                    Console.WriteLine("ctrl[{0:X2}]: {1}", i, BitConverter.ToString(ctrlBuf, i, 16));
            }
        }

        // 3. SetControl pointer
        IntPtr setCtrlPtr = QI(IdVfControlSet);
        Console.WriteLine("IdVfControlSet: " + (setCtrlPtr != IntPtr.Zero ? "found" : "null"));

        // 4. ClkVfStatus pointer
        IntPtr statusPtr = QI(IdVfStatus);
        Console.WriteLine("IdVfStatus: " + (statusPtr != IntPtr.Zero ? "found" : "null"));
    }
}
