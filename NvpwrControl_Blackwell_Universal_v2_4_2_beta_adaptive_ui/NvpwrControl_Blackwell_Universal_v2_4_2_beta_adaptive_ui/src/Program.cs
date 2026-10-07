using System;
using System.Diagnostics;
using System.IO;
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
            bool isSilent = TryGetSilentTarget(args, out silentTarget);
            bool applyNow = HasArg(args, "--now") || HasArg(args, "--apply-now");

            if (isSilent)
            {
                bool createdSilentMutex;
                Mutex silentMutex = null;
                try
                {
                    silentMutex = new Mutex(true, "Local\\NvpwrControl_SilentWatcher_Mutex", out createdSilentMutex);
                }
                catch
                {
                    createdSilentMutex = false;
                }

                if (!createdSilentMutex)
                {
                    AppLog.Write("Another silent watcher instance is already running. Exiting.");
                    if (silentMutex != null) { try { silentMutex.Close(); } catch { } }
                    return;
                }

                try
                {
                    RunSilentWatcher(silentTarget, applyNow);
                }
                finally
                {
                    if (silentMutex != null)
                    {
                        try { silentMutex.ReleaseMutex(); } catch { }
                        try { silentMutex.Close(); } catch { }
                    }
                }
                return;
            }

            // GUI Mode
            bool createdGuiMutex;
            Mutex guiMutex = null;
            try
            {
                guiMutex = new Mutex(true, "Local\\NvpwrControl_Gui_Mutex", out createdGuiMutex);
            }
            catch
            {
                createdGuiMutex = false;
            }

            if (!createdGuiMutex)
            {
                AppLog.Write("Another GUI instance is already running.");
                if (guiMutex != null) { try { guiMutex.Close(); } catch { } }
                try
                {
                    using (EventWaitHandle showEv = EventWaitHandle.OpenExisting("Local\\NvpwrControl_ShowWindow_Event"))
                    {
                        showEv.Set();
                    }
                }
                catch { }
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(startMinimized: false));
            }
            finally
            {
                if (guiMutex != null)
                {
                    try { guiMutex.ReleaseMutex(); } catch { }
                    try { guiMutex.Close(); } catch { }
                }
            }
        }

        private static bool HasArg(string[] args, string flag)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != null && args[i].Equals(flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
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

        private static void RunSilentApply(int target, bool skipDelay)
        {
            try
            {
                if (!skipDelay)
                {
                    Thread.Sleep(15000);
                }

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

                AppSettings s = SettingsStore.Load();
                if (s.CoreOffsetEnabled || s.MemoryOffsetEnabled)
                {
                    TuneRequest tr = new TuneRequest();
                    tr.SetCore = s.CoreOffsetEnabled;
                    tr.CoreMHz = s.CoreOffsetMHz;
                    tr.SetMemory = s.MemoryOffsetEnabled;
                    tr.MemoryMHz = s.MemoryOffsetMHz;
                    TunerState post;
                    OperationResult oc = NvApiTuner.Apply(tr, c.Profile != null && c.Profile.AllowMsvdd, out post);
                    AppLog.Write("Autostart OC result (Core " + (s.CoreOffsetEnabled ? (s.CoreOffsetMHz >= 0 ? "+" : "") + s.CoreOffsetMHz.ToString() + " MHz" : "stock")
                        + ", Mem " + (s.MemoryOffsetEnabled ? (s.MemoryOffsetMHz >= 0 ? "+" : "") + s.MemoryOffsetMHz.ToString() + " MHz" : "stock") + "): " + (oc.Success ? "OK" : ("FAIL: " + oc.Message)));
                }

                ReapplyAfterburnerIfRunning();
            }
            catch (Exception ex)
            {
                AppLog.Write("Autostart exception: " + ex.ToString());
            }
        }

        private static void RunSilentWatcher(int target, bool skipDelay)
        {
            RunSilentApply(target, skipDelay);

            AppSettings s = SettingsStore.Load();
            if (!s.MsiAutoApply || !MsiScenarioWatcher.IsMsiCenterInstalled())
            {
                AppLog.Write("Silent apply finished. MsiAutoApply is disabled or MSI Center is not installed. Headless process exiting.");
                return;
            }

            AppLog.Write("Entering headless background watcher (0% CPU, 0 UI, 0 tray) for MSI Center scenario changes.");

            using (ManualResetEvent shutdownEvent = new ManualResetEvent(false))
            {
                Microsoft.Win32.SessionEndingEventHandler onSessionEnding = delegate(object sender, Microsoft.Win32.SessionEndingEventArgs e)
                {
                    AppLog.Write("Windows SessionEnding event received. Exiting background watcher immediately (0s delay).");
                    shutdownEvent.Set();
                };

                try
                {
                    Microsoft.Win32.SystemEvents.SessionEnding += onSessionEnding;
                }
                catch { }

                using (MsiScenarioWatcher watcher = new MsiScenarioWatcher(delegate
                {
                    try
                    {
                        AppSettings cur = SettingsStore.Load();
                        int reapplyW = cur.CurrentSelection > 0 ? cur.CurrentSelection : target;
                        AppLog.Write("MSI Center scenario change detected! Waiting 3.5s to let EC and hardware settle...");
                        Thread.Sleep(3500);

                        PowerBackend pb = new PowerBackend();
                        CompatibilityState comp = pb.CheckCompatibility();
                        if (comp != null && comp.CurrentWritesReady)
                        {
                            OperationResult r = pb.SetCurrent(reapplyW);
                            AppLog.Write("MSI scenario re-apply power (" + reapplyW + "W) result: " + (r.Success ? "OK" : ("FAIL: " + r.Message)));
                        }

                        if (cur.CoreOffsetEnabled || cur.MemoryOffsetEnabled)
                        {
                            TuneRequest tr = new TuneRequest();
                            tr.SetCore = cur.CoreOffsetEnabled;
                            tr.CoreMHz = cur.CoreOffsetMHz;
                            tr.SetMemory = cur.MemoryOffsetEnabled;
                            tr.MemoryMHz = cur.MemoryOffsetMHz;
                            TunerState post;
                            OperationResult oc = NvApiTuner.Apply(tr, comp != null && comp.Profile != null && comp.Profile.AllowMsvdd, out post);
                            AppLog.Write("MSI scenario re-apply OC result: " + (oc.Success ? "OK" : ("FAIL: " + oc.Message)));
                        }

                        ReapplyAfterburnerIfRunning();
                    }
                    catch (Exception ex)
                    {
                        AppLog.Write("Silent watcher callback exception: " + ex.Message);
                    }
                }))
                {
                    watcher.TimeoutSec = 3;
                    watcher.Start();
                    shutdownEvent.WaitOne();
                    watcher.Stop();
                }

                try
                {
                    Microsoft.Win32.SystemEvents.SessionEnding -= onSessionEnding;
                }
                catch { }
            }

            AppLog.Write("Headless background watcher exited cleanly.");
        }

        private static void ReapplyAfterburnerIfRunning()
        {
            try
            {
                Process[] procs = Process.GetProcessesByName("MSIAfterburner");
                if (procs != null && procs.Length > 0)
                {
                    string abPath = null;
                    try { abPath = procs[0].MainModule.FileName; } catch { }
                    if (string.IsNullOrEmpty(abPath) || !File.Exists(abPath))
                    {
                        string p86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MSI Afterburner", "MSIAfterburner.exe");
                        if (File.Exists(p86)) abPath = p86;
                    }
                    if (!string.IsNullOrEmpty(abPath) && File.Exists(abPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = abPath,
                            Arguments = "-profile1 -s",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        AppLog.Write("Dispatched -profile1 -s to MSI Afterburner.");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("ReapplyAfterburner exception: " + ex.Message);
            }
        }
    }
}
