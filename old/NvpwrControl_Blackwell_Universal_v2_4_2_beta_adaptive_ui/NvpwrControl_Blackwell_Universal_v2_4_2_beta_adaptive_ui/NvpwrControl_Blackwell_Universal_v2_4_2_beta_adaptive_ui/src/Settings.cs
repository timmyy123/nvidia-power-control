using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace NvpwrControlBlackwell
{
    internal sealed class AppSettings
    {
        public string Language = "en";
        public string Theme = "dark";
        public string Accent = "purple";
        public string ButtonAccent = "purple";
        public string SliderAccent = "purple";
        public int MaxSelection = 0;
        public int CurrentSelection = 0;
        public bool TelemetryEnabled = true;
        public int TelemetryIntervalMs = 1500;
        public string LastPage = "power";
        public int WindowWidth = 1320;
        public int WindowHeight = 900;
        public bool WindowMaximized = false;
        public int UiScalePercent = 100;
    }

    internal static class SettingsStore
    {
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NvpwrControlBlackwell");
        private static readonly string FilePath = Path.Combine(Dir, "settings.ini");

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                Dictionary<string, string> kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int p = line.IndexOf('=');
                    if (p <= 0) continue;
                    kv[line.Substring(0, p).Trim()] = line.Substring(p + 1).Trim();
                }

                string v; int n; bool b;
                if (kv.TryGetValue("language", out v) && (v == "ru" || v == "en")) s.Language = v;
                if (kv.TryGetValue("theme", out v) && (v == "dark" || v == "midnight" || v == "light")) s.Theme = v;
                if (kv.TryGetValue("accent", out v)) s.Accent = v;
                if (kv.TryGetValue("buttonAccent", out v)) s.ButtonAccent = v; else s.ButtonAccent = s.Accent;
                if (kv.TryGetValue("sliderAccent", out v)) s.SliderAccent = v; else s.SliderAccent = s.Accent;
                if (kv.TryGetValue("max", out v) && int.TryParse(v, out n)) s.MaxSelection = n;
                if (kv.TryGetValue("current", out v) && int.TryParse(v, out n)) s.CurrentSelection = n;
                if (kv.TryGetValue("telemetry", out v) && bool.TryParse(v, out b)) s.TelemetryEnabled = b;
                if (kv.TryGetValue("telemetryMs", out v) && int.TryParse(v, out n)) s.TelemetryIntervalMs = Math.Max(500, Math.Min(10000, n));
                if (kv.TryGetValue("page", out v)) s.LastPage = v;
                if (kv.TryGetValue("width", out v) && int.TryParse(v, out n)) s.WindowWidth = Math.Max(1040, n);
                if (kv.TryGetValue("height", out v) && int.TryParse(v, out n)) s.WindowHeight = Math.Max(700, n);
                if (kv.TryGetValue("maximized", out v) && bool.TryParse(v, out b)) s.WindowMaximized = b;
                if (kv.TryGetValue("uiScale", out v) && int.TryParse(v, out n))
                    s.UiScalePercent = (n == 80 || n == 90 || n == 100 || n == 110 || n == 125 || n == 140 || n == 150) ? n : 100;
            }
            catch { }
            return s;
        }

        public static void Save(AppSettings s)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FilePath, new string[]
                {
                    "language=" + s.Language,
                    "theme=" + s.Theme,
                    "accent=" + s.Accent,
                    "buttonAccent=" + s.ButtonAccent,
                    "sliderAccent=" + s.SliderAccent,
                    "max=" + s.MaxSelection.ToString(CultureInfo.InvariantCulture),
                    "current=" + s.CurrentSelection.ToString(CultureInfo.InvariantCulture),
                    "telemetry=" + s.TelemetryEnabled.ToString(),
                    "telemetryMs=" + s.TelemetryIntervalMs.ToString(CultureInfo.InvariantCulture),
                    "page=" + s.LastPage,
                    "width=" + s.WindowWidth.ToString(CultureInfo.InvariantCulture),
                    "height=" + s.WindowHeight.ToString(CultureInfo.InvariantCulture),
                    "maximized=" + s.WindowMaximized.ToString(),
                    "uiScale=" + s.UiScalePercent.ToString(CultureInfo.InvariantCulture)
                });
            }
            catch { }
        }
    }
}
