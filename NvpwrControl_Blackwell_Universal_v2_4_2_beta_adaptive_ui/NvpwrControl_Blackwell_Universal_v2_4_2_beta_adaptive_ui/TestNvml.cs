using System;
using System.Runtime.InteropServices;

class Program
{
    [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
    public static extern int nvmlInit();

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    public static extern int nvmlDeviceGetHandleByIndex(uint index, out IntPtr device);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetName")]
    public static extern int nvmlDeviceGetName(IntPtr device, byte[] name, uint length);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetClockInfo")]
    public static extern int nvmlDeviceGetClockInfo(IntPtr device, int clockType, out uint clockMHz);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceSetGpuLockedClocks")]
    public static extern int nvmlDeviceSetGpuLockedClocks(IntPtr device, uint minClockMHz, uint maxClockMHz);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceResetGpuLockedClocks")]
    public static extern int nvmlDeviceResetGpuLockedClocks(IntPtr device);

    static void Main(string[] args)
    {
        int rc = nvmlInit();
        Console.WriteLine("nvmlInit rc = " + rc);
        if (rc != 0) return;

        IntPtr dev;
        rc = nvmlDeviceGetHandleByIndex(0, out dev);
        Console.WriteLine("nvmlDeviceGetHandleByIndex rc = " + rc);
        if (rc != 0) return;

        byte[] nameBuf = new byte[64];
        nvmlDeviceGetName(dev, nameBuf, 64);
        Console.WriteLine("Device: " + System.Text.Encoding.ASCII.GetString(nameBuf).TrimEnd('\0'));

        uint smClock = 0;
        nvmlDeviceGetClockInfo(dev, 1, out smClock); // 1 = SM clock
        Console.WriteLine("Current SM Clock: " + smClock + " MHz");

        if (args.Length > 0 && args[0] == "--lock" && args.Length >= 2)
        {
            uint target = uint.Parse(args[1]);
            Console.WriteLine("Attempting nvmlDeviceSetGpuLockedClocks(" + target + ", " + target + ")...");
            rc = nvmlDeviceSetGpuLockedClocks(dev, target, target);
            Console.WriteLine("nvmlDeviceSetGpuLockedClocks rc = " + rc + (rc == 0 ? " (SUCCESS)" : (rc == 2 ? " (NO_PERMISSION)" : "")));
        }
        else if (args.Length > 0 && args[0] == "--reset")
        {
            rc = nvmlDeviceResetGpuLockedClocks(dev);
            Console.WriteLine("nvmlDeviceResetGpuLockedClocks rc = " + rc);
        }
    }
}
