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

        Console.WriteLine("Info Header:");
        for (int i = 0; i < 44; i += 4)
            Console.WriteLine(string.Format("  +{0:D2}: 0x{1:X8} ({2})", i, BitConverter.ToUInt32(infoBuf, i), BitConverter.ToInt32(infoBuf, i)));

        Console.WriteLine("\nInfo entries 75 to 85:");
        // Size = 6188. If header is 44 bytes, 6188 - 44 = 6144. 6144 / 256 = 24 bytes per entry!
        int entrySize = (6188 - 44) / 256;
        Console.WriteLine("Entry size: " + entrySize);
        for (int p = 75; p <= 85; p++)
        {
            int off = 44 + p * entrySize;
            Console.WriteLine(string.Format("--- Point {0:D3} (offset 0x{1:X4}) ---", p, off));
            for (int j = 0; j < entrySize; j += 4)
            {
                Console.WriteLine(string.Format("  +{0:D2}: 0x{1:X8} ({2})", j, BitConverter.ToUInt32(infoBuf, off + j), BitConverter.ToInt32(infoBuf, off + j)));
            }
        }
    }
}
