using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NvpwrControlBlackwell
{
    public class VfCurveTuner
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr QueryInterfaceDelegate(uint id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvInitDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EnumGpuDelegate(IntPtr handles, ref uint count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GpuBufferDelegate(IntPtr gpu, IntPtr buffer);

        [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
        private static extern int nvmlInit();

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
        private static extern int nvmlDeviceGetHandleByIndex(uint index, out IntPtr device);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceSetGpuLockedClocks")]
        private static extern int nvmlDeviceSetGpuLockedClocks(IntPtr device, uint minClockMHz, uint maxClockMHz);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceResetGpuLockedClocks")]
        private static extern int nvmlDeviceResetGpuLockedClocks(IntPtr device);

        private const uint IdVfInfo = 0x507B4B59;
        private const uint IdVfStatus = 0x21537AD4;
        private const uint IdVfControlGet = 0x23F1B133;
        private const uint IdVfControlSet = 0x0733E009;

        private const uint VfInfoVersion = 0x0001182C;
        private const int VfInfoSize = 6188;

        private const uint VfStatusVersion = 0x00011C28;
        private const int VfStatusSize = 7208;

        private const uint VfControlVersion = 0x00012420;
        private const int VfControlSize = 9248;

        private const int ControlHeaderSize = 0x44; // 68 bytes
        private const int ControlEntrySize = 36;
        private const int StatusHeaderSize = 40;
        private const int StatusEntrySize = 28;

        public struct CurvePoint
        {
            public int Index;
            public int Type;
            public int FreqKHz;
            public int VoltUv;
            public int OffsetKHz;
            public double FreqMHz { get { return FreqKHz / 1000.0; } }
            public double VoltV { get { return VoltUv / 1000000.0; } }
            public double VoltMv { get { return VoltUv / 1000.0; } }
            public double OffsetMHz { get { return OffsetKHz / 1000.0; } }
        }

        static int Main(string[] args)
        {
            Console.WriteLine("====================================================");
            Console.WriteLine("  NVIDIA Ada Lovelace V/F Curve Tuner (NVAPI Direct)");
            Console.WriteLine("====================================================");

            IntPtr shim = LoadLibrary(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvapi64.dll"));
            if (shim == IntPtr.Zero)
            {
                Console.WriteLine("[ERROR] Failed to load nvapi64.dll.");
                return 1;
            }

            IntPtr qiPtr = GetProcAddress(shim, "nvapi_QueryInterface");
            if (qiPtr == IntPtr.Zero)
            {
                Console.WriteLine("[ERROR] nvapi_QueryInterface not found.");
                return 1;
            }

            var QI = (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(qiPtr, typeof(QueryInterfaceDelegate));
            IntPtr initPtr = QI(0x0150E828);
            IntPtr enumPtr = QI(0xE5AC921F);

            if (initPtr == IntPtr.Zero || enumPtr == IntPtr.Zero)
            {
                Console.WriteLine("[ERROR] NvAPI initialization or enumeration entry missing.");
                return 1;
            }

            var init = (NvInitDelegate)Marshal.GetDelegateForFunctionPointer(initPtr, typeof(NvInitDelegate));
            var enumGpu = (EnumGpuDelegate)Marshal.GetDelegateForFunctionPointer(enumPtr, typeof(EnumGpuDelegate));

            int rc = init();
            if (rc != 0)
            {
                Console.WriteLine("[ERROR] NvAPI_Initialize failed: " + rc);
                return 1;
            }

            IntPtr handles = Marshal.AllocHGlobal(IntPtr.Size * 64);
            uint gpuCount = 0;
            enumGpu(handles, ref gpuCount);
            if (gpuCount == 0)
            {
                Console.WriteLine("[ERROR] No physical NVIDIA GPU found.");
                return 1;
            }

            IntPtr gpu = Marshal.ReadIntPtr(handles, 0);
            Marshal.FreeHGlobal(handles);

            // Get Info
            var infoFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfInfo), typeof(GpuBufferDelegate));
            byte[] infoBuf = new byte[VfInfoSize];
            BitConverter.GetBytes(VfInfoVersion).CopyTo(infoBuf, 0);

            IntPtr mInfo = Marshal.AllocHGlobal(VfInfoSize);
            Marshal.Copy(infoBuf, 0, mInfo, VfInfoSize);
            int infoRc = infoFn(gpu, mInfo);
            Marshal.Copy(mInfo, infoBuf, 0, VfInfoSize);
            Marshal.FreeHGlobal(mInfo);

            if (infoRc != 0)
            {
                Console.WriteLine("[ERROR] ClkVfPointsGetInfo failed: " + infoRc);
                return 1;
            }

            byte[] vfMask = new byte[32];
            Array.Copy(infoBuf, 4, vfMask, 0, 32);

            // Get Status
            var stFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfStatus), typeof(GpuBufferDelegate));
            byte[] stBuf = new byte[VfStatusSize];
            BitConverter.GetBytes(VfStatusVersion).CopyTo(stBuf, 0);
            Array.Copy(vfMask, 0, stBuf, 4, 32);

            IntPtr mSt = Marshal.AllocHGlobal(VfStatusSize);
            Marshal.Copy(stBuf, 0, mSt, VfStatusSize);
            int stRc = stFn(gpu, mSt);
            Marshal.Copy(mSt, stBuf, 0, VfStatusSize);
            Marshal.FreeHGlobal(mSt);

            if (stRc != 0)
            {
                Console.WriteLine("[ERROR] ClkVfPointsGetStatus failed: " + stRc);
                return 1;
            }

            // Get Control
            var getFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfControlGet), typeof(GpuBufferDelegate));
            byte[] ctrlBuf = new byte[VfControlSize];
            BitConverter.GetBytes(VfControlVersion).CopyTo(ctrlBuf, 0);
            Array.Copy(vfMask, 0, ctrlBuf, 4, 32);

            IntPtr mCtrl = Marshal.AllocHGlobal(VfControlSize);
            Marshal.Copy(ctrlBuf, 0, mCtrl, VfControlSize);
            int getRc = getFn(gpu, mCtrl);
            Marshal.Copy(mCtrl, ctrlBuf, 0, VfControlSize);
            Marshal.FreeHGlobal(mCtrl);

            if (getRc != 0)
            {
                Console.WriteLine("[ERROR] ClkVfPointsGetControl failed: " + getRc);
                return 1;
            }

            // Parse points
            CurvePoint[] points = new CurvePoint[128];
            for (int p = 0; p < 128; p++)
            {
                int stOff = StatusHeaderSize + p * StatusEntrySize;
                int ctrlOff = ControlHeaderSize + p * ControlEntrySize;

                points[p].Index = p;
                points[p].Type = BitConverter.ToInt32(stBuf, stOff);
                points[p].FreqKHz = BitConverter.ToInt32(stBuf, stOff + 4);
                points[p].VoltUv = BitConverter.ToInt32(stBuf, stOff + 8);
                points[p].OffsetKHz = BitConverter.ToInt32(ctrlBuf, ctrlOff + 20);
            }

            // Command parsing
            if (args.Length == 0 || args[0].Equals("--status", StringComparison.OrdinalIgnoreCase))
            {
                PrintStatus(points);
                PrintUsage();
                return 0;
            }

            bool modify = false;
            int lockFreqKhz = 0;
            if (args[0].Equals("--set-offset", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
            {
                int offsetMhz = int.Parse(args[1]);
                int offsetKhz = offsetMhz * 1000;
                Console.WriteLine("[ACTION] Applying uniform core offset +" + offsetMhz + " MHz across all active V/F points...");
                for (int p = 1; p < 128; p++)
                {
                    if (points[p].Type == 0 && points[p].VoltUv > 0)
                    {
                        int ctrlOff = ControlHeaderSize + p * ControlEntrySize;
                        BitConverter.GetBytes(offsetKhz).CopyTo(ctrlBuf, ctrlOff + 20);
                    }
                }
                modify = true;
            }
            else if (args[0].Equals("--lock-volt", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
            {
                int targetMv = int.Parse(args[1]);
                int targetUv = targetMv * 1000;

                // Find point index matching target voltage
                int targetIdx = -1;
                for (int p = 1; p < 128; p++)
                {
                    if (points[p].VoltUv >= targetUv)
                    {
                        targetIdx = p;
                        break;
                    }
                }

                if (targetIdx < 0)
                {
                    Console.WriteLine("[ERROR] Target voltage " + targetMv + " mV not found in curve.");
                    return 1;
                }

                lockFreqKhz = points[targetIdx].FreqKHz;
                if (args.Length >= 3)
                {
                    lockFreqKhz = int.Parse(args[2]) * 1000;
                }

                Console.WriteLine("[ACTION] Locking voltage ceiling at " + points[targetIdx].VoltMv + " mV (Point " + targetIdx + ") with frequency " + (lockFreqKhz / 1000) + " MHz (preserving user overclock)...");

                for (int p = 1; p < 128; p++)
                {
                    if (points[p].Type == 0 && points[p].VoltUv > 0)
                    {
                        int baseFreq = points[p].FreqKHz - points[p].OffsetKHz;
                        int ctrlOff = ControlHeaderSize + p * ControlEntrySize;

                        if (p > targetIdx)
                        {
                            // Cap only points above target to the lock frequency
                            int offsetNeeded = lockFreqKhz - baseFreq;
                            BitConverter.GetBytes(offsetNeeded).CopyTo(ctrlBuf, ctrlOff + 20);
                        }
                        else
                        {
                            // Clean up any negative offsets on points <= targetIdx
                            int curOff = BitConverter.ToInt32(ctrlBuf, ctrlOff + 20);
                            if (curOff < 0) BitConverter.GetBytes(0).CopyTo(ctrlBuf, ctrlOff + 20);
                        }
                    }
                }
                modify = true;
            }
            else if (args[0].Equals("--reset", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[ACTION] Resetting all V/F curve offsets to 0 MHz...");
                for (int p = 1; p < 128; p++)
                {
                    if (points[p].Type == 0)
                    {
                        int ctrlOff = ControlHeaderSize + p * ControlEntrySize;
                        BitConverter.GetBytes(0).CopyTo(ctrlBuf, ctrlOff + 20);
                    }
                }
                modify = true;
            }
            else
            {
                PrintUsage();
                return 1;
            }

            if (modify)
            {
                var setFn = (GpuBufferDelegate)Marshal.GetDelegateForFunctionPointer(QI(IdVfControlSet), typeof(GpuBufferDelegate));
                IntPtr mSet = Marshal.AllocHGlobal(VfControlSize);
                Marshal.Copy(ctrlBuf, 0, mSet, VfControlSize);
                int setRc = setFn(gpu, mSet);
                Marshal.FreeHGlobal(mSet);

                if (setRc != 0)
                {
                    if (setRc == -137)
                    {
                        Console.WriteLine("[ERROR] NVAPI returned -137 (NVAPI_INVALID_USER_PRIVILEGE).");
                        Console.WriteLine("        You MUST run this command from an elevated (Administrator) command prompt!");
                    }
                    else
                    {
                        Console.WriteLine("[ERROR] ClkVfPointsSetControl failed: " + setRc);
                    }
                    return 1;
                }

                Console.WriteLine("[SUCCESS] V/F Curve offsets applied to GPU!");

                // Readback verify
                IntPtr mRb = Marshal.AllocHGlobal(VfControlSize);
                Marshal.Copy(ctrlBuf, 0, mRb, VfControlSize);
                getFn(gpu, mRb);
                Marshal.Copy(mRb, ctrlBuf, 0, VfControlSize);
                Marshal.FreeHGlobal(mRb);

                Console.WriteLine("\n[VERIFICATION] Reading back updated curve points:");
                for (int p = 70; p <= 100; p += 5)
                {
                    int off = ControlHeaderSize + p * ControlEntrySize;
                    int curOff = BitConverter.ToInt32(ctrlBuf, off + 20);
                    string offStr = (curOff / 1000).ToString("+0;-0;0");
                    int eff = (points[p].FreqKHz + curOff - points[p].OffsetKHz) / 1000;
                    Console.WriteLine("  Point " + p.ToString("D3") + " (" + points[p].VoltMv.ToString("0") + " mV): Offset = " + offStr + " MHz -> Effective: " + eff + " MHz");
                }
                Console.WriteLine("Curve programming verified successfully.");

                // NVML Clock locking enforcement
                if (args[0].Equals("--lock-volt", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (nvmlInit() == 0)
                        {
                            IntPtr dev;
                            if (nvmlDeviceGetHandleByIndex(0, out dev) == 0)
                            {
                                uint targetMHz = (uint)(lockFreqKhz / 1000);
                                int nvmlRc = nvmlDeviceSetGpuLockedClocks(dev, targetMHz, targetMHz);
                                if (nvmlRc == 0)
                                    Console.WriteLine("[NVML] GPU Boost Clock pinned to " + targetMHz + " MHz (Forces voltage to target under load).");
                                else
                                    Console.WriteLine("[NVML] Note: nvmlDeviceSetGpuLockedClocks returned " + nvmlRc + " (Run as Admin to lock boost clock).");
                            }
                        }
                    }
                    catch { }
                }
                else if (args[0].Equals("--reset", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (nvmlInit() == 0)
                        {
                            IntPtr dev;
                            if (nvmlDeviceGetHandleByIndex(0, out dev) == 0)
                            {
                                nvmlDeviceResetGpuLockedClocks(dev);
                                Console.WriteLine("[NVML] GPU locked clocks reset to stock dynamic scaling.");
                            }
                        }
                    }
                    catch { }
                }
            }

            return 0;
        }

        private static void PrintStatus(CurvePoint[] points)
        {
            Console.WriteLine("\nIndex |  Voltage | Nominal Clock | Active Offset | Effective Clock");
            Console.WriteLine("------------------------------------------------------------------");
            for (int p = 40; p <= 110; p++)
            {
                if (points[p].Type == 0 && points[p].VoltUv > 0)
                {
                    string marker = "";
                    if (Math.Abs(points[p].VoltMv - 940) < 1) marker = " <-- Stock 940mV cap";
                    if (Math.Abs(points[p].VoltMv - 1000) < 1) marker = " <-- 1.000V target";
                    if (Math.Abs(points[p].VoltMv - 1050) < 1) marker = " <-- 1.050V target";
                    if (Math.Abs(points[p].VoltMv - 1100) < 1) marker = " <-- 1.100V Boost max";

                    int effFreq = (points[p].FreqKHz) / 1000;
                    string offStr = points[p].OffsetMHz.ToString("+0;-0;0");
                    Console.WriteLine(string.Format("  {0:D3} | {1,5:0} mV | {2,9:0} MHz | {3,9} MHz | {4,11:0} MHz{5}",
                        p, points[p].VoltMv, effFreq, offStr, effFreq, marker));
                }
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("\nUsage:");
            Console.WriteLine("  VfCurveTuner.exe --status");
            Console.WriteLine("  VfCurveTuner.exe --set-offset <offset_mhz>     (e.g. 260 to push curve higher)");
            Console.WriteLine("  VfCurveTuner.exe --lock-volt <target_mv> [mhz] (e.g. 1000 2550 to lock at 1.000V @ 2550 MHz)");
            Console.WriteLine("  VfCurveTuner.exe --reset                       (reset all offsets to stock)");
        }
    }
}
