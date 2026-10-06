using System;
using System.Diagnostics;
using System.Security.Principal;

namespace MsiAfterburnerSync
{
    public static class TaskSchedulerHelper
    {
        private const string TaskName = "MsiAfterburnerSync";

        public static bool IsTaskInstalled()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Query /TN \"" + TaskName + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(3000);
                    return p.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool InstallTask(string exePath, out string error)
        {
            error = "";
            try
            {
                string userName = WindowsIdentity.GetCurrent().Name;
                string script =
                    "$a = New-ScheduledTaskAction -Execute '" + PsQuote(exePath) + "' -Argument '--minimized';" +
                    "$t = New-ScheduledTaskTrigger -AtLogOn;" +
                    "$p = New-ScheduledTaskPrincipal -UserId '" + PsQuote(userName) + "' -LogonType Interactive -RunLevel Highest;" +
                    "$s = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0;" +
                    "Register-ScheduledTask -TaskName '" + PsQuote(TaskName) + "' -Action $a -Trigger $t -Principal $p -Settings $s -Force | Out-Null;";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(8000);
                    if (p.ExitCode != 0)
                    {
                        error = p.StandardError.ReadToEnd();
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool RemoveTask(out string error)
        {
            error = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Delete /TN \"" + TaskName + "\" /F",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(5000);
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool DeleteNvpwrControlTask(out string error)
        {
            error = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Delete /TN \"NvpwrControlBlackwell\" /F",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(5000);
                    if (p.ExitCode != 0)
                    {
                        error = p.StandardError.ReadToEnd();
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string PsQuote(string s)
        {
            return (s ?? "").Replace("'", "''");
        }
    }
}
