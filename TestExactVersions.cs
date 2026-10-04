using System;
using System.IO;
using System.Runtime.InteropServices;

class TestExactVersions
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

        var setFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(0x0F4DAE6B), typeof(GpuBufferDelegate));

        uint[] vers = {
            0x00011C94, // (1 << 16) | 7316
            0x00021CF8, // (2 << 16) | 7416
            0x00021CF4, // (2 << 16) | 7412
            0x00011CF8, // (1 << 16) | 7416
            0x00011CF4, // (1 << 16) | 7412
            0x00031CF8,
            0x00031CF4
        };

        byte[] buf = new byte[8192];
        IntPtr mSet = Marshal.AllocHGlobal(buf.Length);

        foreach (uint v in vers)
        {
            Array.Clear(buf, 0, buf.Length);
            BitConverter.GetBytes(v).CopyTo(buf, 0);
            Marshal.Copy(buf, 0, mSet, (int)(v & 0xFFFF));
            int rc = setFn(gpu, mSet);
            Console.WriteLine(string.Format("Version 0x{0:X8} (sz={1}): rc={2}", v, (v & 0xFFFF), rc));
        }
        Marshal.FreeHGlobal(mSet);
    }
}
