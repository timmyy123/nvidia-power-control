using System;
using System.Collections.Generic;
using System.IO;

namespace MsiAfterburnerSync
{
    public sealed class SyncSettings
    {
        public string AfterburnerPath = "";
        public int ProfileSlot = 1; // 1 to 5
        public int DelaySec = 1;     // 1 to 10
        public bool AutoStart = true;
        public bool CloseToTray = true;
        public bool NotifyOnApply = true;
        public bool ApplyPowerUnlock = true;
        public int PowerTargetWatts = 250;
        public string NvpwrControlPath = @"C:\Users\timmy\Downloads\nvidia-power-control\NvpwrControl_Blackwell_Universal_v2_4_2_beta_adaptive_ui\NvpwrControl_Blackwell_Universal_v2_4_2_beta_adaptive_ui\dist\NvpwrControl.exe";
        public bool ApplyOnStartup = true;
    }

    public static class SettingsStore
    {
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MsiAfterburnerSync");
        private static readonly string FilePath = Path.Combine(Dir, "settings.ini");

        public static SyncSettings Load()
        {
            SyncSettings s = new SyncSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed.StartsWith(";")) continue;
                    int eq = trimmed.IndexOf('=');
                    if (eq > 0)
                    {
                        dict[trimmed.Substring(0, eq).Trim()] = trimmed.Substring(eq + 1).Trim();
                    }
                }

                string v; int n; bool b;
                if (dict.TryGetValue("afterburnerPath", out v) && !string.IsNullOrEmpty(v)) s.AfterburnerPath = v;
                if (dict.TryGetValue("profileSlot", out v) && int.TryParse(v, out n)) s.ProfileSlot = Math.Max(1, Math.Min(5, n));
                if (dict.TryGetValue("delaySec", out v) && int.TryParse(v, out n)) s.DelaySec = Math.Max(1, Math.Min(10, n));
                if (dict.TryGetValue("autoStart", out v) && bool.TryParse(v, out b)) s.AutoStart = b;
                if (dict.TryGetValue("closeToTray", out v) && bool.TryParse(v, out b)) s.CloseToTray = b;
                if (dict.TryGetValue("notifyOnApply", out v) && bool.TryParse(v, out b)) s.NotifyOnApply = b;
                if (dict.TryGetValue("applyPowerUnlock", out v) && bool.TryParse(v, out b)) s.ApplyPowerUnlock = b;
                if (dict.TryGetValue("powerTargetWatts", out v) && int.TryParse(v, out n)) s.PowerTargetWatts = Math.Max(50, Math.Min(600, n));
                if (dict.TryGetValue("nvpwrControlPath", out v) && !string.IsNullOrEmpty(v)) s.NvpwrControlPath = v;
                if (dict.TryGetValue("applyOnStartup", out v) && bool.TryParse(v, out b)) s.ApplyOnStartup = b;
            }
            catch { }
            return s;
        }

        public static void Save(SyncSettings s)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FilePath, new string[]
                {
                    "afterburnerPath=" + (s.AfterburnerPath ?? ""),
                    "profileSlot=" + s.ProfileSlot,
                    "delaySec=" + s.DelaySec,
                    "autoStart=" + s.AutoStart,
                    "closeToTray=" + s.CloseToTray,
                    "notifyOnApply=" + s.NotifyOnApply,
                    "applyPowerUnlock=" + s.ApplyPowerUnlock,
                    "powerTargetWatts=" + s.PowerTargetWatts,
                    "nvpwrControlPath=" + (s.NvpwrControlPath ?? ""),
                    "applyOnStartup=" + s.ApplyOnStartup
                });
            }
            catch { }
        }
    }
}
