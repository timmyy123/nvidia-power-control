using System;
using System.Runtime.InteropServices;

class Program {
    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr NvApi_QueryInterface(uint id);

    delegate int NvAPI_InitializeDelegate();
    delegate int NvAPI_EnumPhysicalGPUsDelegate([Out] IntPtr[] handles, out int count);
    delegate int NvAPI_GPU_GetPstates20Delegate(IntPtr gpu, [In, Out] byte[] pstates);

    static void Main() {
        IntPtr initPtr = NvApi_QueryInterface(0x0150E828);
        if (initPtr == IntPtr.Zero) { Console.WriteLine("NvAPI init ptr missing"); return; }
        var init = (NvAPI_InitializeDelegate)Marshal.GetDelegateForFunctionPointer(initPtr, typeof(NvAPI_InitializeDelegate));
        int rc = init();
        Console.WriteLine("NvAPI_Initialize rc=" + rc);

        IntPtr enumPtr = NvApi_QueryInterface(0xE5AC921F);
        var enumGpus = (NvAPI_EnumPhysicalGPUsDelegate)Marshal.GetDelegateForFunctionPointer(enumPtr, typeof(NvAPI_EnumPhysicalGPUsDelegate));
        IntPtr[] gpus = new IntPtr[64];
        int count;
        enumGpus(gpus, out count);
        Console.WriteLine("GPU count=" + count);
        if (count == 0) return;

        IntPtr getPstatesPtr = NvApi_QueryInterface(0x6FF81213);
        if (getPstatesPtr == IntPtr.Zero) { Console.WriteLine("GetPstates20 ptr missing"); return; }
        var getPstates = (NvAPI_GPU_GetPstates20Delegate)Marshal.GetDelegateForFunctionPointer(getPstatesPtr, typeof(NvAPI_GPU_GetPstates20Delegate));

        byte[] buf = new byte[7416];
        BitConverter.GetBytes(0x00021CF8).CopyTo(buf, 0);
        rc = getPstates(gpus[0], buf);
        Console.WriteLine("GetPstates20 rc=" + rc);
        if (rc != 0) return;

        uint np = BitConverter.ToUInt32(buf, 8);
        uint nc = BitConverter.ToUInt32(buf, 12);
        uint nv = BitConverter.ToUInt32(buf, 16);
        Console.WriteLine("Pstates: numPstates=" + np + " numClocks=" + nc + " numVoltages=" + nv);

        int p0 = 20; // Header = 20
        Console.WriteLine("P0 State ID: " + BitConverter.ToUInt32(buf, p0));

        // VoltagesOff = 8 + 8 * 44 = 360
        // VoltageSize = 24
        for (int v = 0; v < nv; v++) {
            int vo = p0 + 360 + v * 24;
            uint domain = BitConverter.ToUInt32(buf, vo);
            uint type = BitConverter.ToUInt32(buf, vo + 4);
            int cur = BitConverter.ToInt32(buf, vo + 12);
            int mn = BitConverter.ToInt32(buf, vo + 16);
            int mx = BitConverter.ToInt32(buf, vo + 20);
            Console.WriteLine("Voltage entry {0}: domain={1} type={2} cur={3}uV min={4}uV max={5}uV", 
                v, domain, type, cur, mn, mx);
        }
    }
}
