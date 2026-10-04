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

    private const uint ID_GET_CURRENT_VOLTAGE = 0x465F9BCF;

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

        var voltFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(ID_GET_CURRENT_VOLTAGE), typeof(GpuBufferDelegate));

        byte[] b = new byte[0x4C];
        BitConverter.GetBytes(0x0001004C).CopyTo(b, 0);
        IntPtr m = Marshal.AllocHGlobal(b.Length);
        Marshal.Copy(b, 0, m, b.Length);
        int rc = voltFn(gpu, m);
        Marshal.Copy(m, b, 0, b.Length);
        Marshal.FreeHGlobal(m);

        if (rc == 0)
        {
            uint uv = BitConverter.ToUInt32(b, 0x28);
            Console.WriteLine(string.Format("Live GPU Voltage: {0:F4} V ({1} mV / {2} uV)", uv / 1000000.0, uv / 1000, uv));
        }
        else
        {
            Console.WriteLine("Failed to read voltage rc = " + rc);
        }
    }
}
