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

        var getFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(0x23F1B133), typeof(GpuBufferDelegate));
        byte[] ctrlBuf = new byte[9248];
        BitConverter.GetBytes(0x00012420).CopyTo(ctrlBuf, 0);
        Array.Copy(vfMask, 0, ctrlBuf, 4, 32);
        IntPtr mCtrl = Marshal.AllocHGlobal(9248);
        Marshal.Copy(ctrlBuf, 0, mCtrl, 9248);
        getFn(gpu, mCtrl);
        Marshal.Copy(mCtrl, ctrlBuf, 0, 9248);

        Console.WriteLine("Listing non-zero control entries:");
        for (int p = 0; p < 255; p++)
        {
            int off = 68 + p * 36;
            if (off + 36 > ctrlBuf.Length) break;
            bool nonZero = false;
            for (int j = 0; j < 36; j++)
            {
                if (ctrlBuf[off + j] != 0) { nonZero = true; break; }
            }
            if (nonZero)
            {
                int offKhz = BitConverter.ToInt32(ctrlBuf, off + 20);
                Console.WriteLine(string.Format("Entry {0:D3}: offset = {1} kHz ({2} MHz)", p, offKhz, offKhz / 1000));
            }
        }
    }
}
