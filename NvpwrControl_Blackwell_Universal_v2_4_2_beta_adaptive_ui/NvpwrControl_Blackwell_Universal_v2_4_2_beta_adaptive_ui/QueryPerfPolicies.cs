using System;
using System.Runtime.InteropServices;

class Program
{
    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr NvAPI_QueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_InitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_EnumPhysicalGPUsDelegate([Out] IntPtr[] handles, out int count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_GPU_GetFullNameDelegate(IntPtr gpu, byte[] name);

    static T GetDelegate<T>(uint id) where T : class
    {
        try
        {
            IntPtr ptr = NvAPI_QueryInterface(id);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T;
        }
        catch { return null; }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int BufferDelegate(IntPtr gpu, byte[] buf);

    static void Main(string[] args)
    {
        var init = GetDelegate<NvAPI_InitializeDelegate>(0x0150E828);
        if (init == null) { Console.WriteLine("NvAPI Init delegate missing"); return; }
        int rc = init();
        if (rc != 0) { Console.WriteLine("NvAPI Init failed rc=" + rc); return; }

        var enumGpus = GetDelegate<NvAPI_EnumPhysicalGPUsDelegate>(0xE5AC9226);
        IntPtr[] gpus = new IntPtr[64];
        int count = 0;
        enumGpus(gpus, out count);
        Console.WriteLine("GPU Count: " + count);
        if (count == 0) return;

        IntPtr gpu = gpus[0];
        var getName = GetDelegate<NvAPI_GPU_GetFullNameDelegate>(0xCEEE8E9F);
        byte[] nameBuf = new byte[64];
        if (getName != null && getName(gpu, nameBuf) == 0)
        {
            Console.WriteLine("GPU: " + System.Text.Encoding.ASCII.GetString(nameBuf).TrimEnd('\0'));
        }

        // Perf Policies Info: 0x409D9841
        var getPoliciesInfo = GetDelegate<BufferDelegate>(0x409D9841);
        if (getPoliciesInfo != null)
        {
            byte[] info = new byte[16];
            BitConverter.GetBytes(0x10000 | 16).CopyTo(info, 0); // version 1
            rc = getPoliciesInfo(gpu, info);
            Console.WriteLine("GetPerfPoliciesInfo rc=" + rc + " mask=0x" + BitConverter.ToUInt32(info, 4).ToString("X8"));
        }
        else Console.WriteLine("GetPerfPoliciesInfo not found");

        // Perf Policies Status: 0x3D35218E
        var getPoliciesStatus = GetDelegate<BufferDelegate>(0x3D35218E);
        if (getPoliciesStatus != null)
        {
            byte[] status = new byte[16];
            BitConverter.GetBytes(0x10000 | 16).CopyTo(status, 0);
            rc = getPoliciesStatus(gpu, status);
            Console.WriteLine("GetPerfPoliciesStatus rc=" + rc + " raw=0x" + BitConverter.ToUInt32(status, 4).ToString("X8"));
        }
        else Console.WriteLine("GetPerfPoliciesStatus not found");
    }
}
