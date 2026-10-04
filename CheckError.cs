using System;
using System.IO;
using System.Runtime.InteropServices;

class CheckError
{
    [DllImport("kernel32.dll")] static extern IntPtr LoadLibrary(string f);
    [DllImport("kernel32.dll")] static extern IntPtr GetProcAddress(IntPtr m, string n);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr QueryInterfaceDelegate(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GetErrorMsgDelegate(int status, IntPtr desc);

    static void Main()
    {
        IntPtr shim = LoadLibrary(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvapi64.dll"));
        var QI = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(GetProcAddress(shim, "nvapi_QueryInterface"), typeof(QueryInterfaceDelegate));
        var getMsg = (GetErrorMsgDelegate)Marshal.GetDelegateForFunctionPointer(QI(0x6C2D042A), typeof(GetErrorMsgDelegate));

        int[] codes = { 0, -1, -5, -6, -8, -9, -14, -137 };
        foreach (int c in codes)
        {
            IntPtr p = Marshal.AllocHGlobal(64);
            getMsg(c, p);
            string msg = Marshal.PtrToStringAnsi(p);
            Marshal.FreeHGlobal(p);
            Console.WriteLine(string.Format("Code {0} = {1}", c, msg));
        }
    }
}
