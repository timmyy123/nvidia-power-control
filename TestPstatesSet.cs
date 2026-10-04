using System;
using System.IO;
using System.Runtime.InteropServices;

class TestPstatesSet
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

        var getFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(0x6FF81213), typeof(GpuBufferDelegate));
        var setFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(0x0F4DAE6B), typeof(GpuBufferDelegate));

        byte[] getBuf = new byte[7416];
        BitConverter.GetBytes(0x00021CF8).CopyTo(getBuf, 0);
        IntPtr mGet = Marshal.AllocHGlobal(getBuf.Length);
        Marshal.Copy(getBuf, 0, mGet, getBuf.Length);
        int getRc = getFn(gpu, mGet);
        Marshal.Copy(mGet, getBuf, 0, getBuf.Length);
        Marshal.FreeHGlobal(mGet);

        // Hypothesis A: Header is 16 bytes: version (4), numPstates (4), numClocks (4), numBaseVoltages (4)
        // Shift pstates payload by 4 bytes (i.e. start at offset 16 instead of 20)
        {
            byte[] setBuf = new byte[7412];
            BitConverter.GetBytes(0x00021CF4).CopyTo(setBuf, 0);
            BitConverter.GetBytes(BitConverter.ToUInt32(getBuf, 8)).CopyTo(setBuf, 4);  // numPstates
            BitConverter.GetBytes(BitConverter.ToUInt32(getBuf, 12)).CopyTo(setBuf, 8); // numClocks
            BitConverter.GetBytes(BitConverter.ToUInt32(getBuf, 16)).CopyTo(setBuf, 12); // numBaseVoltages
            // Copy pstates array: in getBuf, pstates starts at 20. In setBuf, it starts at 16.
            Buffer.BlockCopy(getBuf, 20, setBuf, 16, 7412 - 16);

            IntPtr mSet = Marshal.AllocHGlobal(setBuf.Length);
            Marshal.Copy(setBuf, 0, mSet, setBuf.Length);
            int setRc = setFn(gpu, mSet);
            Marshal.FreeHGlobal(mSet);
            Console.WriteLine("Hypothesis A (Header 16 bytes, shift pstates to 16): rc=" + setRc);
        }

        // Hypothesis B: In SetPstates20, numPstates should only be 1 (only P0), or numClocks = 2
        {
            byte[] setBuf = new byte[7412];
            BitConverter.GetBytes(0x00021CF4).CopyTo(setBuf, 0);
            BitConverter.GetBytes(1u).CopyTo(setBuf, 4); // numPstates = 1
            BitConverter.GetBytes(2u).CopyTo(setBuf, 8); // numClocks = 2
            BitConverter.GetBytes(0u).CopyTo(setBuf, 12); // numBaseVoltages = 0
            // Copy P0
            Buffer.BlockCopy(getBuf, 20, setBuf, 16, 456);

            IntPtr mSet = Marshal.AllocHGlobal(setBuf.Length);
            Marshal.Copy(setBuf, 0, mSet, setBuf.Length);
            int setRc = setFn(gpu, mSet);
            Marshal.FreeHGlobal(mSet);
            Console.WriteLine("Hypothesis B (numPstates=1, numClocks=2, P0 at 16): rc=" + setRc);
        }

        // Hypothesis C: What if the 4-byte difference is at the END (e.g. ov struct or padding)?
        // And header is still 20 bytes? (e.g. version, flags, numPstates, numClocks, numBaseVoltages)
        // With numPstates = 1
        {
            byte[] setBuf = new byte[7412];
            BitConverter.GetBytes(0x00021CF4).CopyTo(setBuf, 0);
            BitConverter.GetBytes(0u).CopyTo(setBuf, 4); // reserved/flags
            BitConverter.GetBytes(1u).CopyTo(setBuf, 8); // numPstates = 1
            BitConverter.GetBytes(2u).CopyTo(setBuf, 12); // numClocks = 2
            BitConverter.GetBytes(0u).CopyTo(setBuf, 16); // numBaseVoltages = 0
            Buffer.BlockCopy(getBuf, 20, setBuf, 20, 456);

            IntPtr mSet = Marshal.AllocHGlobal(setBuf.Length);
            Marshal.Copy(setBuf, 0, mSet, setBuf.Length);
            int setRc = setFn(gpu, mSet);
            Marshal.FreeHGlobal(mSet);
            Console.WriteLine("Hypothesis C (Header 20 bytes, numPstates=1, P0 at 20): rc=" + setRc);
        }
    }
}
