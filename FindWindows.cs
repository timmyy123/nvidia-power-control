using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

class Program {
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    static void Main() {
        Process[] procs = Process.GetProcessesByName("mVolt+ (1)");
        if (procs.Length == 0) procs = Process.GetProcessesByName("mVolt+");
        Console.WriteLine("Procs found: " + procs.Length);
        if (procs.Length > 0) {
            uint pid = (uint)procs[0].Id;
            Console.WriteLine("PID: " + pid);
            foreach (ProcessThread thread in procs[0].Threads) {
                // check threads
            }
        }

        int count = 0;
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (procs.Length > 0 && pid == (uint)procs[0].Id) {
                StringBuilder sbClass = new StringBuilder(256);
                GetClassName(hWnd, sbClass, 256);
                StringBuilder sbText = new StringBuilder(256);
                GetWindowText(hWnd, sbText, 256);
                Console.WriteLine("Found window for mVolt: HWND=0x" + hWnd.ToInt64().ToString("X") + " Class=[" + sbClass + "] Title=[" + sbText + "]");
                count++;
            }
            return true;
        }, IntPtr.Zero);
        Console.WriteLine("Total windows found for mVolt PID: " + count);
    }
}
