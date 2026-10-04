using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

class Program {
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    static extern bool IsWindowEnabled(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    const int GWL_STYLE = -16;
    const int WS_DISABLED = 0x08000000;

    static void Main() {
        Process[] procs = Process.GetProcessesByName("mVolt+ (1)");
        if (procs.Length == 0) procs = Process.GetProcessesByName("mVolt+");
        if (procs.Length == 0) {
            Console.WriteLine("mVolt+ process not found.");
            return;
        }

        uint targetPid = (uint)procs[0].Id;
        Console.WriteLine("Found mVolt+ PID: " + targetPid);

        List<IntPtr> rootWindows = new List<IntPtr>();
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == targetPid) {
                rootWindows.Add(hWnd);
            }
            return true;
        }, IntPtr.Zero);

        Console.WriteLine("Root windows found: " + rootWindows.Count);

        int enabledCount = 0;
        int totalControls = 0;

        foreach (var root in rootWindows) {
            StringBuilder sbTitle = new StringBuilder(256);
            GetWindowText(root, sbTitle, 256);
            Console.WriteLine("Root Window: " + root + " Title: [" + sbTitle + "]");

            EnumChildWindows(root, (child, lParam) => {
                totalControls++;
                StringBuilder sbClass = new StringBuilder(256);
                GetClassName(child, sbClass, 256);
                StringBuilder sbText = new StringBuilder(256);
                GetWindowText(child, sbText, 256);

                bool enabled = IsWindowEnabled(child);
                int style = GetWindowLong(child, GWL_STYLE);

                Console.WriteLine("  Child: 0x{0:X} Class: [{1}] Text: [{2}] Enabled: {3} Style: 0x{4:X8}", 
                    child.ToInt64(), sbClass, sbText, enabled, style);

                if (!enabled || (style & WS_DISABLED) != 0) {
                    SetWindowLong(child, GWL_STYLE, style & ~WS_DISABLED);
                    EnableWindow(child, true);
                    enabledCount++;
                    Console.WriteLine("    -> UNLOCKED!");
                }
                return true;
            }, IntPtr.Zero);
        }

        Console.WriteLine("Done. Total controls: " + totalControls + ", newly enabled: " + enabledCount);
    }
}
