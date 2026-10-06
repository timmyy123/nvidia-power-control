using System;
using System.IO;

namespace NvpwrControlBlackwell
{
    internal static class AppLog
    {
        private static readonly object Gate = new object();
        public static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "NvpwrControlBlackwell");
        public static readonly string LogPath = Path.Combine(DirectoryPath, "nvpwr-control.log");

        public static void Write(string text)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(DirectoryPath);
                    File.AppendAllText(LogPath, DateTime.Now.ToString("o") + "  " + text + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
