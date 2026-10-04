using System;
using System.IO;
using System.Runtime.InteropServices;

class DumpVfInfo
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
    private const uint VfInfoVersion = 0x0001182C;
    private const int VfInfoSize = 6188;

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
        byte[] b = new byte[VfInfoSize];
        BitConverter.GetBytes(VfInfoVersion).CopyTo(b, 0);

        IntPtr m = Marshal.AllocHGlobal(VfInfoSize);
        Marshal.Copy(b, 0, m, VfInfoSize);
        infoFn(gpu, m);
        Marshal.Copy(m, b, 0, VfInfoSize);
        Marshal.FreeHGlobal(m);

        // Header: 0x00..0x03 ver, 0x04..0x23 mask (32 bytes = 256 bits).
        // Let's see what is at 0x24..
        Console.WriteLine("Header bytes from 0x20 to 0x60:");
        for (int i = 0x20; i < 0x60; i += 16)
            Console.WriteLine("{0:X4}: {1}", i, BitConverter.ToString(b, i, 16));

        // 6188 total size - 36 header = 6152 bytes.
        // 6152 / 256 = 24.03125? Or 6188 - 0x24 = 6152?
        // Let's check entry stride:
        // Could each entry be 24 bytes? (24 * 256 = 6144, 6144 + 44 = 6188!)
        // 6188 - 6144 = 44 bytes header! (0x2C header!)
        Console.WriteLine("Assuming 44 (0x2C) byte header, entry size 24 bytes:");
        int hdr = 0x2C;
        int entrySize = 24;
        for (int p = 0; p < 133; p++)
        {
            int off = hdr + p * entrySize;
            uint d0 = BitConverter.ToUInt32(b, off);
            uint d1 = BitConverter.ToUInt32(b, off + 4);
            uint d2 = BitConverter.ToUInt32(b, off + 8);
            uint d3 = BitConverter.ToUInt32(b, off + 12);
            uint d4 = BitConverter.ToUInt32(b, off + 16);
            uint d5 = BitConverter.ToUInt32(b, off + 20);
            if (p < 15 || p > 120 || p == 50)
            {
                Console.WriteLine("Point {0:D3} (off 0x{1:X4}): {2:X8} {3:X8} {4:X8} {5:X8} {6:X8} {7:X8}",
                    p, off, d0, d1, d2, d3, d4, d5);
            }
        }
    }
}
