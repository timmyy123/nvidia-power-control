using System;
using System.IO;
using System.Runtime.InteropServices;

class Program
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

    static void Main()
    {
        IntPtr shim = LoadLibrary(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvapi64.dll"));
        var qi = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(GetProcAddress(shim, "nvapi_QueryInterface"), typeof(QueryInterfaceDelegate));
        var init = (NvInitDelegate)Marshal.GetDelegateForFunctionPointer(qi(0x0150E828), typeof(NvInitDelegate));
        init();

        var enumGpu = (EnumGpuDelegate)Marshal.GetDelegateForFunctionPointer(qi(0xE5AC9226), typeof(EnumGpuDelegate));
        IntPtr[] gpus = new IntPtr[64];
        uint count = 0;
        IntPtr mGpus = Marshal.AllocHGlobal(64 * IntPtr.Size);
        enumGpu(mGpus, ref count);
        IntPtr gpu = Marshal.ReadIntPtr(mGpus);
        Marshal.FreeHGlobal(mGpus);

        // ClkVfPointsGetInfo (0x507B4B59)
        var infoFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(qi(0x507B4B59), typeof(GpuBufferDelegate));
        byte[] infoBuf = new byte[6188];
        BitConverter.GetBytes(0x0001182C).CopyTo(infoBuf, 0);
        IntPtr mInfo = Marshal.AllocHGlobal(6188);
        Marshal.Copy(infoBuf, 0, mInfo, 6188);
        infoFn(gpu, mInfo);
        Marshal.Copy(mInfo, infoBuf, 0, 6188);
        Marshal.FreeHGlobal(mInfo);

        byte[] vfMask = new byte[32];
        Array.Copy(infoBuf, 4, vfMask, 0, 32);

        // ClkVfPointsGetStatus (0x21537AD4)
        var stFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(qi(0x21537AD4), typeof(GpuBufferDelegate));
        byte[] stBuf = new byte[7208];
        BitConverter.GetBytes(0x00011C28).CopyTo(stBuf, 0);
        Array.Copy(vfMask, 0, stBuf, 4, 32);
        IntPtr mSt = Marshal.AllocHGlobal(7208);
        Marshal.Copy(stBuf, 0, mSt, 7208);
        stFn(gpu, mSt);
        Marshal.Copy(mSt, stBuf, 0, 7208);
        Marshal.FreeHGlobal(mSt);

        // ClkVfPointsGetControl (0x23F1B133)
        var getFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(qi(0x23F1B133), typeof(GpuBufferDelegate));
        byte[] ctrlBuf = new byte[9248];
        BitConverter.GetBytes(0x00012420).CopyTo(ctrlBuf, 0);
        Array.Copy(vfMask, 0, ctrlBuf, 4, 32);
        IntPtr mCtrl = Marshal.AllocHGlobal(9248);
        Marshal.Copy(ctrlBuf, 0, mCtrl, 9248);
        getFn(gpu, mCtrl);
        Marshal.Copy(mCtrl, ctrlBuf, 0, 9248);
        Marshal.FreeHGlobal(mCtrl);

        Console.WriteLine("Point | Volt (mV) | Type | StFreq (MHz) | CtrlOff (MHz) | BaseFreq (MHz)");
        for (int p = 70; p <= 105; p++)
        {
            int stOff = 40 + p * 28;
            int ctrlOff = 68 + p * 36;
            int type = BitConverter.ToInt32(stBuf, stOff);
            int freq = BitConverter.ToInt32(stBuf, stOff + 4) / 1000;
            int volt = BitConverter.ToInt32(stBuf, stOff + 8) / 1000;
            int off = BitConverter.ToInt32(ctrlBuf, ctrlOff + 20) / 1000;
            int baseFreq = freq - off;
            Console.WriteLine(string.Format(" {0:D3}  |  {1,4} mV  |  {2}   |  {3,5} MHz   |   {4,5} MHz  |  {5,5} MHz",
                p, volt, type, freq, off, baseFreq));
        }
    }
}
