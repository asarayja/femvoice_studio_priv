using System;
using System.Collections.Generic;
using System.IO;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Last-resort sink for RC-0 diagnostics write failures. When any RC-0 log, evidence,
    /// or settings write fails, one line is appended to RC0_LOGGING_FAILURE.txt in the first
    /// writable location of: Documents\FemVoiceStudio\logs, %LOCALAPPDATA%\FemVoiceStudio\logs,
    /// AppContext.BaseDirectory\logs. Without this sink a blocked write (OneDrive sync lock,
    /// Controlled Folder Access, permissions) is indistinguishable from "feature off".
    /// Never throws.
    /// </summary>
    public static class Rc0WriteFailureSink
    {
        private static readonly object Sync = new();
        private static string? _writableRoot;

        /// <summary>
        /// First failure reported this process, surfaced in RC0_ERRORS_ONLY.txt by the exporter.
        /// </summary>
        public static string? FirstWriteError { get; private set; }

        public static void Report(string component, string? targetPath, Exception exception)
        {
            try
            {
                var line = $"{DateTime.Now:O} [{component}] target=\"{targetPath}\" {exception.GetType().Name}: {exception.Message}";
                lock (Sync)
                {
                    FirstWriteError ??= line;
                    foreach (var root in CandidateRoots())
                    {
                        try
                        {
                            Directory.CreateDirectory(root);
                            File.AppendAllText(Path.Combine(root, "RC0_LOGGING_FAILURE.txt"), line + Environment.NewLine);
                            _writableRoot = root;
                            return;
                        }
                        catch
                        {
                            // Try the next fallback root; the sink itself must never throw.
                        }
                    }
                }
            }
            catch
            {
                // Diagnostics must never break the app.
            }
        }

        private static IEnumerable<string> CandidateRoots()
        {
            if (_writableRoot is not null)
                yield return _writableRoot;

            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FemVoiceStudio", "logs");
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FemVoiceStudio", "logs");
            yield return Path.Combine(AppContext.BaseDirectory, "logs");
        }
    }
}
