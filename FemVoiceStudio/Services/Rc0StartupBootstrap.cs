using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// RC-0 startup bootstrap (acceptance Test A): when Debug.EnableRc0Diagnostics is true
    /// in Documents\FemVoiceStudio\settings.json, the evidence files below are created at
    /// app launch — before any session, device selection, or click — so a later crash or
    /// silent failure still leaves a baseline. The resolved-paths log lines are written
    /// unconditionally so the runtime log always shows where everything actually lives
    /// (exposing OneDrive Known Folder Move redirection of Documents). Never throws.
    /// </summary>
    public static class Rc0StartupBootstrap
    {
        public static void Run()
        {
            try
            {
                Rc0RuntimeLog.Write("AppStartup",
                    $"FemVoiceStudio {typeof(Rc0StartupBootstrap).Assembly.GetName().Version} starting; " +
                    $"OS={Environment.OSVersion}; ProcessPath=\"{Environment.ProcessPath}\"");
                Rc0RuntimeLog.Write("Paths",
                    $"Settings=\"{ThemeManager.SettingsPath}\"; " +
                    $"RuntimeLog=\"{Rc0RuntimeLog.CurrentLogPath}\"; " +
                    $"EvidenceRoot=\"{Rc0EvidenceExporter.EvidenceRoot}\"; " +
                    $"Documents=\"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\"; " +
                    $"LocalAppData=\"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\"");

                var debug = DebugSettingsService.Instance;
                Rc0RuntimeLog.Write("Settings",
                    $"EnableRc0Diagnostics={debug.EnableRc0Diagnostics}; " +
                    $"EnablePitchDebug={debug.EnablePitchDebug}; " +
                    $"EnableAnalyzerDebug={debug.EnableAnalyzerDebug}");

                if (!debug.EnableRc0Diagnostics)
                    return;

                var root = Rc0EvidenceExporter.EvidenceRoot;
                Directory.CreateDirectory(root);

                // Hver fil i egen try: én blokkert skriving (antivirus/lås) skal ikke
                // hindre resten av baseline-filene fra å bli skrevet.
                Try(() => WriteStartupSentinel(root, debug), "StartupSentinel");
                Try(() => WriteInitialRuntimeLogCopy(root), "InitialRuntimeLogCopy");
                Try(() => WriteInitialErrorsOnly(root), "InitialErrorsOnly");
                Try(() => WriteInitialEvidenceJson(root, debug), "InitialEvidenceJson");
                Try(() => WriteInitialDiagnosticReport(root), "InitialDiagnosticReport");
                TryCopySentinelToDocuments(root);
                TryCopyStartupBaselineToLegacy(root);

                Rc0RuntimeLog.Write("AppStartup", $"Diagnostics startup bootstrap completed; EvidenceRoot=\"{root}\"");
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("Rc0StartupBootstrap", Rc0EvidenceExporter.EvidenceRoot, ex);
            }
        }

        private static void Try(Action write, string step)
        {
            try
            {
                write();
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report($"Rc0StartupBootstrap.{step}", Rc0EvidenceExporter.EvidenceRoot, ex);
            }
        }

        private static void WriteStartupSentinel(string root, DebugSettingsService debug)
        {
            var sb = new StringBuilder();
            sb.AppendLine("DIAGNOSTICS STARTUP SENTINEL");
            sb.AppendLine($"ValidationProfile={DiagnosticsNaming.ValidationProfile}");
            sb.AppendLine($"Timestamp={DateTime.Now:O}");
            sb.AppendLine($"SettingsPath={ThemeManager.SettingsPath}");
            sb.AppendLine($"EnableRc0Diagnostics={debug.EnableRc0Diagnostics}");
            sb.AppendLine($"EnablePitchDebug={debug.EnablePitchDebug}");
            sb.AppendLine($"EnableAnalyzerDebug={debug.EnableAnalyzerDebug}");
            sb.AppendLine($"RuntimeLogPath={Rc0RuntimeLog.CurrentLogPath}");
            sb.AppendLine($"EvidenceRoot={root}");
            sb.AppendLine($"DocumentsFolder={Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}");
            sb.AppendLine($"LocalAppDataFolder={Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}");
            File.WriteAllText(Path.Combine(root, DiagnosticsNaming.StartupSentinel), sb.ToString(), Encoding.UTF8);
            WriteRc0Alias(root, DiagnosticsNaming.StartupSentinel, DiagnosticsNaming.Rc0StartupSentinel);
        }

        private static void WriteInitialRuntimeLogCopy(string root)
        {
            var destination = Path.Combine(root, DiagnosticsNaming.RuntimeLog);
            if (!Rc0RuntimeLog.TryCopyTo(destination))
                File.WriteAllText(destination,
                    $"Startup snapshot. The live runtime log for this process is \"{Rc0RuntimeLog.CurrentLogPath}\".",
                    Encoding.UTF8);
            WriteRc0Alias(root, DiagnosticsNaming.RuntimeLog, DiagnosticsNaming.Rc0RuntimeLog);
        }

        private static void WriteInitialErrorsOnly(string root)
        {
            var content = Rc0WriteFailureSink.FirstWriteError is { } firstWriteError
                ? $"Diagnostics write failure detected during startup: {firstWriteError}{Environment.NewLine}"
                : "No errors captured at startup.";
            File.WriteAllText(Path.Combine(root, DiagnosticsNaming.ErrorsOnly), content, Encoding.UTF8);
            WriteRc0Alias(root, DiagnosticsNaming.ErrorsOnly, DiagnosticsNaming.Rc0ErrorsOnly);
        }

        private static void WriteInitialEvidenceJson(string root, DebugSettingsService debug)
        {
            var payload = new
            {
                ValidationProfile = DiagnosticsNaming.ValidationProfile,
                StartupCompleted = true,
                Timestamp = DateTime.Now,
                SettingsPath = ThemeManager.SettingsPath,
                RuntimeLogPath = Rc0RuntimeLog.CurrentLogPath,
                EvidenceRoot = root,
                EnableRc0Diagnostics = debug.EnableRc0Diagnostics,
                EnablePitchDebug = debug.EnablePitchDebug,
                EnableAnalyzerDebug = debug.EnableAnalyzerDebug,
                CaptureStatus = "STARTUP_ONLY",
                Note = "Written by Rc0StartupBootstrap at app launch, before any session. " +
                       "Per-session evidence folders (EVIDENCE_<timestamp>) are created next to this file when a session ends."
            };
            File.WriteAllText(
                Path.Combine(root, DiagnosticsNaming.EvidenceJson),
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);
            WriteRc0Alias(root, DiagnosticsNaming.EvidenceJson, DiagnosticsNaming.Rc0EvidenceJson);
        }

        private static void WriteInitialDiagnosticReport(string root)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Audio Pipeline Diagnostic Report (startup baseline)");
            sb.AppendLine();
            sb.AppendLine($"Generated at: {DateTime.Now:O}");
            sb.AppendLine();
            sb.AppendLine("No session has run yet in this process. This file proves diagnostics");
            sb.AppendLine("bootstrapped at startup; a per-session report is written into each");
            sb.AppendLine("EVIDENCE_<timestamp> folder when a session ends.");
            sb.AppendLine();
            sb.AppendLine($"- Runtime log: {Rc0RuntimeLog.CurrentLogPath}");
            sb.AppendLine($"- Evidence root: {root}");
            File.WriteAllText(Path.Combine(root, DiagnosticsNaming.AudioPipelineDiagnosticReport), sb.ToString(), Encoding.UTF8);
            WriteRc0Alias(root, DiagnosticsNaming.AudioPipelineDiagnosticReport, DiagnosticsNaming.Rc0AudioPipelineDiagnosticReport);
        }

        private static void TryCopySentinelToDocuments(string root)
        {
            try
            {
                var destination = Rc0EvidenceExporter.DocumentsMirrorRoot;
                Directory.CreateDirectory(destination);
                File.Copy(
                    Path.Combine(root, DiagnosticsNaming.StartupSentinel),
                    Path.Combine(destination, DiagnosticsNaming.StartupSentinel),
                    overwrite: true);
                if (DiagnosticsNaming.EnableRc0CompatibilityExport)
                {
                    File.Copy(
                        Path.Combine(root, DiagnosticsNaming.Rc0StartupSentinel),
                        Path.Combine(destination, DiagnosticsNaming.Rc0StartupSentinel),
                        overwrite: true);
                }
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("Rc0StartupBootstrap.CopySentinelToDocuments", Rc0EvidenceExporter.DocumentsMirrorRoot, ex);
            }
        }

        private static void TryCopyStartupBaselineToLegacy(string root)
        {
            if (!DiagnosticsNaming.EnableRc0CompatibilityExport)
                return;

            try
            {
                Directory.CreateDirectory(DiagnosticsNaming.LegacyPrimaryRoot);
                foreach (var file in Directory.GetFiles(root, "RC0_*"))
                {
                    File.Copy(
                        file,
                        Path.Combine(DiagnosticsNaming.LegacyPrimaryRoot, Path.GetFileName(file)),
                        overwrite: true);
                }
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("Rc0StartupBootstrap.CopyStartupBaselineToLegacy", DiagnosticsNaming.LegacyPrimaryRoot, ex);
            }
        }

        private static void WriteRc0Alias(string root, string neutralName, string rc0Name)
        {
            if (!DiagnosticsNaming.EnableRc0CompatibilityExport)
                return;

            File.Copy(
                Path.Combine(root, neutralName),
                Path.Combine(root, rc0Name),
                overwrite: true);
        }
    }
}
