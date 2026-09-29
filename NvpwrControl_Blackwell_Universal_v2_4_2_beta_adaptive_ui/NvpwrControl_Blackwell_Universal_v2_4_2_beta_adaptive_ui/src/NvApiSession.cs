using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace NvpwrControlBlackwell
{
    internal sealed class NvApiSession : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr QueryInterfaceDelegate(uint id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int NvInitDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int EnumGpuDelegate(IntPtr handles, ref uint count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int GpuBufferDelegate(IntPtr gpu, IntPtr data);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private IntPtr _shim;
        private NvInitDelegate _unload;

        public QueryInterfaceDelegate QI;
        public IntPtr Gpu;
        public string ImplPath = "";
        public string ImplHash = "";
        public IntPtr ImplBase;

        public bool Open(out string error)
        {
            error = "";
            _shim = LoadLibrary(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvapi64.dll"));
            if (_shim == IntPtr.Zero)
            {
                error = "nvapi64.dll could not be loaded.";
                return false;
            }

            IntPtr qiPtr = GetProcAddress(_shim, "nvapi_QueryInterface");
            if (qiPtr == IntPtr.Zero)
            {
                error = "nvapi_QueryInterface is missing.";
                return false;
            }

            QI = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(qiPtr, typeof(QueryInterfaceDelegate));
            IntPtr initPtr = QI(0x0150E828);
            IntPtr enumPtr = QI(0xE5AC921F);
            IntPtr unloadPtr = QI(0xD22BDD7E);
            if (initPtr == IntPtr.Zero || enumPtr == IntPtr.Zero)
            {
                error = "NvAPI init/enumeration interfaces are missing.";
                return false;
            }

            NvInitDelegate init = (NvInitDelegate)Marshal.GetDelegateForFunctionPointer(initPtr, typeof(NvInitDelegate));
            EnumGpuDelegate enumGpu = (EnumGpuDelegate)Marshal.GetDelegateForFunctionPointer(enumPtr, typeof(EnumGpuDelegate));
            if (unloadPtr != IntPtr.Zero)
                _unload = (NvInitDelegate)Marshal.GetDelegateForFunctionPointer(unloadPtr, typeof(NvInitDelegate));

            int rc = init();
            if (rc != 0)
            {
                error = "NvAPI_Initialize failed rc=" + rc.ToString();
                return false;
            }

            IntPtr handles = Marshal.AllocHGlobal(IntPtr.Size * 64);
            try
            {
                Marshal.Copy(new byte[IntPtr.Size * 64], 0, handles, IntPtr.Size * 64);
                uint count = 0;
                rc = enumGpu(handles, ref count);
                if (rc != 0 || count != 1)
                {
                    error = "NvAPI_EnumPhysicalGPUs rc=" + rc.ToString() + ", count=" + count.ToString();
                    return false;
                }
                Gpu = Marshal.ReadIntPtr(handles, 0);
            }
            finally
            {
                Marshal.FreeHGlobal(handles);
            }

            foreach (ProcessModule m in Process.GetCurrentProcess().Modules)
            {
                if (m.ModuleName.Equals("nvapi64_impl.dll", StringComparison.OrdinalIgnoreCase))
                {
                    ImplPath = m.FileName;
                    ImplBase = m.BaseAddress;
                    ImplHash = SystemProbe.Sha256File(m.FileName);
                    break;
                }
            }

            if (ImplBase == IntPtr.Zero)
            {
                error = "nvapi64_impl.dll was not found in the current process.";
                return false;
            }

            return true;
        }

        public GpuBufferDelegate GetGpuBuffer(uint id)
        {
            if (QI == null) return null;
            IntPtr p = QI(id);
            if (p == IntPtr.Zero) return null;
            return (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(p, typeof(GpuBufferDelegate));
        }

        public void Dispose()
        {
            try { if (_unload != null) _unload(); } catch { }
            try { if (_shim != IntPtr.Zero) FreeLibrary(_shim); } catch { }
            _shim = IntPtr.Zero;
            QI = null;
            _unload = null;
        }
    }
}
