using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace MsiAfterburnerSync
{
    public static class AfterburnerController
    {
        public static string FindAfterburnerPath()
        {
            // 1. Registry 32-bit MSI path
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\MSI\Afterburner"))
                {
                    if (k != null)
                    {
                        object p = k.GetValue("InstallPath");
                        if (p != null && File.Exists(p.ToString())) return p.ToString();
                    }
                }
            }
            catch { }

            // 2. Registry 64-bit MSI path
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\MSI\Afterburner"))
                {
                    if (k != null)
                    {
                        object p = k.GetValue("InstallPath");
                        if (p != null && File.Exists(p.ToString())) return p.ToString();
                    }
                }
            }
            catch { }

            // 3. Standard Program Files paths
            string standard32 = @"C:\Program Files (x86)\MSI Afterburner\MSIAfterburner.exe";
            if (File.Exists(standard32)) return standard32;

            string standard64 = @"C:\Program Files\MSI Afterburner\MSIAfterburner.exe";
            if (File.Exists(standard64)) return standard64;

            return "";
        }

        public static string GetProfileDetails(string exePath, int slot)
        {
            try
            {
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return "Afterburner executable not found";
                string dir = Path.GetDirectoryName(exePath);
                string profilesDir = Path.Combine(dir, "Profiles");
                if (!Directory.Exists(profilesDir)) return "Profile " + slot;

                string[] cfgs = Directory.GetFiles(profilesDir, "VEN_*.cfg");
                if (cfgs.Length == 0) return "Profile " + slot;

                string targetSection = "[Profile" + slot + "]";
                foreach (string cfg in cfgs)
                {
                    string[] lines = File.ReadAllLines(cfg);
                    bool inSection = false;
                    string core = "";
                    string mem = "";
                    foreach (string raw in lines)
                    {
                        string line = raw.Trim();
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            inSection = string.Equals(line, targetSection, StringComparison.OrdinalIgnoreCase);
                            continue;
                        }
                        if (inSection)
                        {
                            if (line.StartsWith("CoreClkBoost=", StringComparison.OrdinalIgnoreCase))
                            {
                                int val;
                                if (int.TryParse(line.Substring(13), out val))
                                {
                                    core = (val / 1000) >= 0 ? ("+" + (val / 1000) + " MHz Core") : ((val / 1000) + " MHz Core");
                                }
                            }
                            else if (line.StartsWith("MemClkBoost=", StringComparison.OrdinalIgnoreCase))
                            {
                                int val;
                                if (int.TryParse(line.Substring(12), out val))
                                {
                                    mem = (val / 1000) >= 0 ? ("+" + (val / 1000) + " MHz Mem") : ((val / 1000) + " MHz Mem");
                                }
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(core) || !string.IsNullOrEmpty(mem))
                    {
                        string summary = core;
                        if (!string.IsNullOrEmpty(mem))
                        {
                            if (!string.IsNullOrEmpty(summary)) summary += ", ";
                            summary += mem;
                        }
                        return summary;
                    }
                }
            }
            catch { }
            return "Profile " + slot;
        }

        public static bool GetProfileOffsets(string exePath, int slot, out int coreMhz, out int memMhz)
        {
            coreMhz = 0;
            memMhz = 0;
            try
            {
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return false;
                string dir = Path.GetDirectoryName(exePath);
                string profilesDir = Path.Combine(dir, "Profiles");
                if (!Directory.Exists(profilesDir)) return false;

                string[] cfgs = Directory.GetFiles(profilesDir, "VEN_*.cfg");
                if (cfgs.Length == 0) return false;

                string targetSection = "[Profile" + slot + "]";
                foreach (string cfg in cfgs)
                {
                    string[] lines = File.ReadAllLines(cfg);
                    bool inSection = false;
                    foreach (string raw in lines)
                    {
                        string line = raw.Trim();
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            inSection = string.Equals(line, targetSection, StringComparison.OrdinalIgnoreCase);
                            continue;
                        }
                        if (inSection)
                        {
                            if (line.StartsWith("CoreClkBoost=", StringComparison.OrdinalIgnoreCase))
                            {
                                int val;
                                if (int.TryParse(line.Substring(13), out val)) coreMhz = val / 1000;
                            }
                            else if (line.StartsWith("MemClkBoost=", StringComparison.OrdinalIgnoreCase))
                            {
                                int val;
                                if (int.TryParse(line.Substring(12), out val)) memMhz = val / 1000;
                            }
                        }
                    }
                    if (coreMhz != 0 || memMhz != 0) return true;
                }
            }
            catch { }
            return false;
        }

        public static bool ApplyProfile(string exePath, int slot, out string message)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                message = "MSI Afterburner executable not found at specified path.";
                return false;
            }

            try
            {
                // Dispatch profile command to MSI Afterburner in silent tray mode (-s).
                // If Afterburner is already running, this tells the active instance to switch profiles via IPC without terminating or restarting it.

                // Launch MSIAfterburner with target profile in silent tray mode (-s)
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-profile" + slot + " -s",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        p.WaitForExit(3000);
                    }
                }

                message = "Profile " + slot + " applied to MSI Afterburner (-profile" + slot + " -s).";
                return true;
            }
            catch (Exception ex)
            {
                message = "Failed to apply profile: " + ex.Message;
                return false;
            }
        }
    }
}
