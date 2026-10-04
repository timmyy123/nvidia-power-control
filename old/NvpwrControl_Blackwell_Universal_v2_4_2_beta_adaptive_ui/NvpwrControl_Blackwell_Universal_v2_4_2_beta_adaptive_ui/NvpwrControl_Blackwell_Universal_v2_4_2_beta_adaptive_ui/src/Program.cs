using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NvpwrControlBlackwell
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [STAThread]
        private static void Main(string[] args)
        {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }

            if (!IsAdministrator())
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = Application.ExecutablePath;
                    psi.Arguments = JoinArgs(args);
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    Process.Start(psi);
                }
                catch { }
                return;
            }

            AppLog.Write("Start " + PowerBackend.Version + " args=" + JoinArgs(args));

            int silentTarget;
            if (TryGetSilentTarget(args, out silentTarget))
            {
                RunSilentApply(silentTarget);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal p = new WindowsPrincipal(id);
            return p.IsInRole(WindowsBuiltInRole.Administrator);
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

        private static bool TryGetSilentTarget(string[] args, out int target)
        {
            target = 0;
            if (args == null) return false;
            bool silent = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--silent", StringComparison.OrdinalIgnoreCase)) silent = true;
                if (args[i].Equals("--apply-current", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    Int32.TryParse(args[i + 1], out target);
            }
            return silent && target > 0;
        }

        private static void RunSilentApply(int target)
        {
            try
            {
                Thread.Sleep(15000);
                PowerBackend p = new PowerBackend();
                CompatibilityState c = p.CheckCompatibility();
                if (c.Profile == null || !c.CurrentWritesReady)
                {
                    AppLog.Write("Autostart blocked: " + c.Reason);
                    return;
                }
                if (!c.Profile.IsInRange(target))
                {
                    AppLog.Write("Autostart blocked: target outside profile range " + target.ToString());
                    return;
                }
                PowerState ps = p.GetPowerState();
                if (!ps.MaxW.HasValue || ps.MaxW.Value + 0.01 < target)
                {
                    AppLog.Write("Autostart blocked: MAX=" + (ps.MaxW.HasValue ? ps.MaxW.Value.ToString("0.00") : "N/A") + " target=" + target.ToString());
                    return;
                }
                OperationResult r = p.SetCurrent(target);
                AppLog.Write("Autostart target=" + target.ToString() + " success=" + r.Success.ToString() + " msg=" + r.Message);
            }
            catch (Exception ex)
            {
                AppLog.Write("Autostart exception: " + ex.ToString());
            }
        }
    }
}
