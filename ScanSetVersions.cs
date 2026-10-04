using System;
using System.IO;
using System.Runtime.InteropServices;

class ScanSetVersions
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

        // Scan versions: (v << 16) | size
        // sizes from 100 to 16000, v from 1 to 4
        byte[] buf = new byte[32768];
        IntPtr mSet = Marshal.AllocHGlobal(buf.Length);

        for (uint v = 1; v <= 4; v++)
        {
            for (int sz = 16; sz <= 16384; sz += 4)
            {
                uint ver = (v << 16) | (uint)sz;
                BitConverter.GetBytes(ver).CopyTo(buf, 0);
                Marshal.Copy(buf, 0, mSet, sz);
                int rc = setFn(gpu, mSet);
                if (rc != -137) // Not INCOMPATIBLE_STRUCT_VERSION
                {
                    Console.WriteLine(string.Format("Version 0x{0:X8} (v={1}, sz={2}) returned rc={3}", ver, v, sz, rc));
                }
            }
        }
        Marshal.FreeHGlobal(mSet);
    }
}
