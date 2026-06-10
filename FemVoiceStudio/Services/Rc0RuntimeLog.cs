using System;
using System.IO;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Developer-only RC-0 runtime log. Not surfaced in the UI and not part of product reports.
    /// </summary>
    public static class Rc0RuntimeLog
    {
        private static readonly object Sync = new();
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FemVoiceStudio",
            "RC0_Runtime");

        private static readonly string LogPath = Path.Combine(
            DirectoryPath,
            $"RC0_RUNTIME_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");

        public static string CurrentLogPath => LogPath;

        public static void Write(string area, string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(DirectoryPath);
                    File.AppendAllText(
                        LogPath,
                        $"{DateTime.Now:O} [{area}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Diagnostics must never break capture or exercise execution.
            }
        }
    }
}
