using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace MsiAfterburnerSync
{
    public class SyncAppContext : ApplicationContext
    {
        private readonly MainForm _mainForm;

        public SyncAppContext(bool startMinimized)
        {
            _mainForm = new MainForm();
            _mainForm.AppContext = this;

            if (!startMinimized)
            {
                _mainForm.RestoreWindow();
            }
        }
    }

    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int ASFW_ANY = -1;
        private const int SW_RESTORE = 9;

        [STAThread]
        private static void Main(string[] args)
        {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }

            bool startMinimized = HasArg(args, "--minimized") || HasArg(args, "--tray") || HasArg(args, "--silent");

            // If an instance is already running in background and user clicked to open, signal it immediately without prompt
            if (!startMinimized)
            {
                try
                {
                    using (EventWaitHandle showEv = EventWaitHandle.OpenExisting("Global\\MsiAfterburnerSync_ShowWindow"))
                    {
                        AllowSetForegroundWindow(ASFW_ANY);
                        showEv.Set();
                        IntPtr hWnd = FindWindow(null, "MSI Afterburner & Power Limit Sync");
                        if (hWnd != IntPtr.Zero)
                        {
                            ShowWindow(hWnd, SW_RESTORE);
                            SetForegroundWindow(hWnd);
                        }
                        return; // Successfully signaled background instance to show!
                    }
                }
                catch { }
            }

            if (!IsAdministrator())
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        Arguments = JoinArgs(args),
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                }
                catch { }
                return;
            }

            bool createdNew;
            Mutex singleInstanceMutex = null;
            try
            {
                System.Security.AccessControl.MutexSecurity mSec = new System.Security.AccessControl.MutexSecurity();
                mSec.AddAccessRule(new System.Security.AccessControl.MutexAccessRule(
                    new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null),
                    System.Security.AccessControl.MutexRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));
                singleInstanceMutex = new Mutex(true, "Global\\MsiAfterburnerSync_SingleInstance", out createdNew, mSec);
            }
            catch
            {
                createdNew = false;
            }

            if (HasArg(args, "--test-power-apply"))
            {
                int watts = 250;
                string pMsg;
                bool ok = PowerUnlockController.ApplyPowerLimit(watts, out pMsg);
                Console.WriteLine("PowerUnlock: " + (ok ? "OK" : "FAIL") + " -> " + pMsg);
                return;
            }

            if (!createdNew)
            {
                if (!startMinimized)
                {
                    try
                    {
                        using (EventWaitHandle showEv = EventWaitHandle.OpenExisting("Global\\MsiAfterburnerSync_ShowWindow"))
                        {
                            AllowSetForegroundWindow(ASFW_ANY);
                            showEv.Set();
                            IntPtr hWnd = FindWindow(null, "MSI Afterburner & Power Limit Sync");
                            if (hWnd != IntPtr.Zero)
                            {
                                ShowWindow(hWnd, SW_RESTORE);
                                SetForegroundWindow(hWnd);
                            }
                        }
                    }
                    catch { }
                }
                if (singleInstanceMutex != null) { try { singleInstanceMutex.Close(); } catch { } }
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SyncAppContext(startMinimized));
            }
            finally
            {
                if (singleInstanceMutex != null)
                {
                    try { singleInstanceMutex.ReleaseMutex(); } catch { }
                    try { singleInstanceMutex.Close(); } catch { }
                }
            }
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal p = new WindowsPrincipal(id);
            return p.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static bool HasArg(string[] args, string flag)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string JoinArgs(string[] args)
        {
            if (args == null || args.Length == 0) return "";
            string s = "";
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) s += " ";
                string a = args[i] ?? "";
                if (a.IndexOf(' ') >= 0 || a.IndexOf('"') >= 0)
                    s += "\"" + a.Replace("\"", "\\\"") + "\"";
                else s += a;
            }
            return s;
        }
    }
}
