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

    private const uint ID_PSTATES20_GET = 0x6FF81213;
    private const uint PstatesVersion = 0x00021CF8;
    private const int PstatesSize = 7416;

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

        var pstatesFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(ID_PSTATES20_GET), typeof(GpuBufferDelegate));

        byte[] b = new byte[PstatesSize];
        BitConverter.GetBytes(PstatesVersion).CopyTo(b, 0);

        IntPtr m = Marshal.AllocHGlobal(b.Length);
        Marshal.Copy(b, 0, m, b.Length);
        int rc = pstatesFn(gpu, m);
        Marshal.Copy(m, b, 0, b.Length);
        Marshal.FreeHGlobal(m);

        Console.WriteLine("Pstates20_Get rc = " + rc);
        if (rc == 0)
        {
            uint ver = BitConverter.ToUInt32(b, 0);
            uint flags = BitConverter.ToUInt32(b, 4);
            uint numPstates = BitConverter.ToUInt32(b, 8);
            uint numClocks = BitConverter.ToUInt32(b, 12);
            uint numBaseVoltages = BitConverter.ToUInt32(b, 16);
            Console.WriteLine(string.Format("Ver=0x{0:X8}, numPstates={1}, numClocks={2}, numBaseVoltages={3}", ver, numPstates, numClocks, numBaseVoltages));

            int p0 = -1;
            for (uint p = 0; p < numPstates; p++)
            {
                int po = 20 + (int)p * 456;
                uint pstateId = BitConverter.ToUInt32(b, po);
                Console.WriteLine(string.Format("PState {0}: ID = {1}", p, pstateId));
                if (pstateId == 0) p0 = po;
            }

            if (p0 >= 0)
            {
                Console.WriteLine("\n--- P0 Clocks ---");
                for (uint c = 0; c < numClocks; c++)
                {
                    int co = p0 + 8 + (int)c * 44;
                    uint domain = BitConverter.ToUInt32(b, co);
                    uint type = BitConverter.ToUInt32(b, co + 4);
                    int cur = BitConverter.ToInt32(b, co + 12);
                    int min = BitConverter.ToInt32(b, co + 16);
                    int max = BitConverter.ToInt32(b, co + 20);
                    Console.WriteLine(string.Format("  Domain {0}: type={1}, cur={2} kHz, min={3} kHz, max={4} kHz", domain, type, cur, min, max));
                }

                Console.WriteLine("\n--- P0 Base Voltages ---");
                for (uint v = 0; v < numBaseVoltages; v++)
                {
                    int vo = p0 + 8 + (int)numClocks * 44 + (int)v * 24;
                    uint domain = BitConverter.ToUInt32(b, vo);
                    uint type = BitConverter.ToUInt32(b, vo + 4);
                    int cur = BitConverter.ToInt32(b, vo + 12);
                    int min = BitConverter.ToInt32(b, vo + 16);
                    int max = BitConverter.ToInt32(b, vo + 20);
                    Console.WriteLine(string.Format("  Volt Domain {0}: type={1}, cur={2} uV ({3} mV), min={4} uV, max={5} uV", domain, type, cur, cur/1000, min, max));
                }
            }
        }
    }
}
