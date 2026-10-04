using System;
using System.Runtime.InteropServices;

class Program {
    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr NvApi_QueryInterface(uint id);

    delegate int InitDelegate();
    delegate int EnumDelegate([Out] IntPtr[] handles, out int count);
    delegate int ActionGpu(IntPtr gpu);
    delegate int ActionGpuBuf(IntPtr gpu, [In, Out] byte[] buf);

    static void Main() {
        var init = (InitDelegate)Marshal.GetDelegateForFunctionPointer(NvApi_QueryInterface(0x0150E828), typeof(InitDelegate));
        init();

        IntPtr enumPtr = NvApi_QueryInterface(0xE5AC921F);
        var enumGpus = (EnumDelegate)Marshal.GetDelegateForFunctionPointer(enumPtr, typeof(EnumDelegate));
        IntPtr[] gpus = new IntPtr[64];
        int count;
        enumGpus(gpus, out count);
        IntPtr gpu = gpus[0];

        uint[] apis = {
            0x82C7E552, // GetVoltageDomainsStatus
            0x18996DA1, // SetVoltageDomainsStatus
            0x465F9BCF, // GetVoltageStep
            0x507B4B59, // GetVfCurveInfo
            0x23F1B133, // GetVfCurveData
            0x43D9B26A, // AdcStatus
            0x68789E2A, // AdcInfo
            0x76EE1822, // SetVfCurveData
            0xEE1342F9  // ClientPowerTopology
        };

        string[] names = {
            "GetVoltageDomainsStatus",
            "SetVoltageDomainsStatus",
            "GetVoltageStep",
            "GetVfCurveInfo",
            "GetVfCurveData",
            "AdcStatus",
            "AdcInfo",
            "SetVfCurveData",
            "ClientPowerTopology"
        };

        for (int i = 0; i < apis.Length; i++) {
            IntPtr ptr = NvApi_QueryInterface(apis[i]);
            Console.WriteLine("{0,-25} (0x{1:X8}): {2}", names[i], apis[i], ptr != IntPtr.Zero ? "EXISTS" : "MISSING");
            if (ptr != IntPtr.Zero) {
                // try calling with 4KB buffer with version header
                byte[] b = new byte[4096];
                BitConverter.GetBytes(0x00010000 | 4096).CopyTo(b, 0);
                try {
                    var fn = (ActionGpuBuf)Marshal.GetDelegateForFunctionPointer(ptr, typeof(ActionGpuBuf));
                    int rc = fn(gpu, b);
                    Console.WriteLine("    -> Call rc=" + rc);
                } catch (Exception ex) {
                    Console.WriteLine("    -> Call ex: " + ex.Message);
                }
            }
        }
    }
}
