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
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GpuOutDelegate(IntPtr gpu, ref uint val);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GpuInDelegate(IntPtr gpu, uint val);

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

        uint[] queryIds = new uint[] {
            0x06656349, // GetVoltageDomainsStatus
            0x189C10E8, // GetVoltageStep
            0x212954F2, // GetVoltageTable
            0x465F9BCF, // GetCurrentVoltage
            0x64593C41, // ClientPowerTopologyGetStatus
            0x4C5F690B, // GetCoreVoltageBoostPercent
            0xC154E255, // SetCoreVoltageBoostPercent
            0xC700E085, // ClientVoltageGetStatus
            0x9E7019BE, // ClientVoltageSetControl
            0x51E28189, // GetOverclockingInfo
            0x507B4B59, // ClkVfPointsGetInfo
            0x21537AD4, // ClkVfPointsGetStatus
            0x23F1B133, // ClkVfPointsGetControl
            0x0733E009  // ClkVfPointsSetControl
        };

        string[] names = new string[] {
            "GetVoltageDomainsStatus (0x06656349)",
            "GetVoltageStep (0x189C10E8)",
            "GetVoltageTable (0x212954F2)",
            "GetCurrentVoltage (0x465F9BCF)",
            "ClientPowerTopologyGetStatus (0x64593C41)",
            "GetCoreVoltageBoostPercent (0x4C5F690B)",
            "SetCoreVoltageBoostPercent (0xC154E255)",
            "ClientVoltageGetStatus (0xC700E085)",
            "ClientVoltageSetControl (0x9E7019BE)",
            "GetOverclockingInfo (0x51E28189)",
            "ClkVfPointsGetInfo (0x507B4B59)",
            "ClkVfPointsGetStatus (0x21537AD4)",
            "ClkVfPointsGetControl (0x23F1B133)",
            "ClkVfPointsSetControl (0x0733E009)"
        };

        for (int i = 0; i < queryIds.Length; i++)
        {
            IntPtr p = QI(queryIds[i]);
            Console.WriteLine(string.Format("{0,-45} : {1}", names[i], p != IntPtr.Zero ? "0x" + p.ToString("X") : "NULL"));
        }

        // Test GetCoreVoltageBoostPercent
        IntPtr boostGet = QI(0x4C5F690B);
        if (boostGet != IntPtr.Zero)
        {
            try
            {
                var bg = (GpuOutDelegate)Marshal.GetDelegateForFunctionPointer(boostGet, typeof(GpuOutDelegate));
                uint val = 0;
                int rc = bg(gpu, ref val);
                Console.WriteLine("GetCoreVoltageBoostPercent rc = " + rc + ", val = " + val);
            }
            catch (Exception ex) { Console.WriteLine("GetCoreVoltageBoostPercent ex: " + ex.Message); }
        }

        // Test ClientVoltageGetStatus
        IntPtr cvGet = QI(0xC700E085);
        if (cvGet != IntPtr.Zero)
        {
            try
            {
                var cvFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(cvGet, typeof(GpuBufferDelegate));
                byte[] b = new byte[0x2000];
                BitConverter.GetBytes(0x00010000 | (uint)b.Length).CopyTo(b, 0); // version test
                IntPtr m = Marshal.AllocHGlobal(b.Length);
                Marshal.Copy(b, 0, m, b.Length);
                int rc = cvFn(gpu, m);
                Marshal.Copy(m, b, 0, b.Length);
                Marshal.FreeHGlobal(m);
                Console.WriteLine("ClientVoltageGetStatus rc = " + rc);
                if (rc == 0)
                {
                    Console.WriteLine("ClientVoltageGetStatus dump:");
                    for (int j = 0; j < 64; j += 16)
                    {
                        Console.Write(string.Format("  {0:X4}: ", j));
                        for (int k = 0; k < 16; k++) Console.Write(string.Format("{0:X2} ", b[j + k]));
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("ClientVoltageGetStatus ex: " + ex.Message); }
        }
    }
}
