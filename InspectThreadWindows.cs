using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

class Program {
    delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumThreadWindows(int dwThreadId, EnumThreadWndProc lpfn, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    static void Main() {
        Process p = Process.GetProcessById(28076);
        foreach (ProcessThread t in p.Threads) {
            Console.WriteLine("Checking thread: " + t.Id);
            EnumThreadWindows(t.Id, (hWnd, lParam) => {
                StringBuilder sbClass = new StringBuilder(256);
                GetClassName(hWnd, sbClass, 256);
                StringBuilder sbText = new StringBuilder(256);
                GetWindowText(hWnd, sbText, 256);
                Console.WriteLine("  Window: HWND=0x" + hWnd.ToInt64().ToString("X") + " Class=[" + sbClass + "] Title=[" + sbText + "]");
                return true;
            }, IntPtr.Zero);
        }
    }
}
