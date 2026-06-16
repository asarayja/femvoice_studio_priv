using System;
using System.IO;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Product-neutral diagnostics names with RC-0 compatibility aliases during closeout.
    /// </summary>
    public static class DiagnosticsNaming
    {
        public const string ValidationProfile = "RC0";

        // static readonly (not const) on purpose: this is a single closeout toggle for the
        // RC-0 backward-compatibility export. Keeping it non-const means the `if (!flag)`
        // guard clauses that consume it do not fold to a compile-time constant, so the
        // compiler no longer flags their bodies as unreachable (CS0162). Behaviour is
        // unchanged (still true); flip to false to drop the legacy aliases.
        public static readonly bool EnableRc0CompatibilityExport = true;

        public const string EvidenceJson = "EVIDENCE.json";
        public const string RuntimeLog = "RUNTIME_LOG.txt";
        public const string VerificationReport = "VERIFICATION_REPORT.md";
        public const string AudioPipelineDiagnosticReport = "AUDIO_PIPELINE_DIAGNOSTIC_REPORT.md";
        public const string SessionSummary = "SESSION_SUMMARY.md";
        public const string ErrorsOnly = "ERRORS_ONLY.txt";
        public const string ScreenshotChecklist = "SCREENSHOT_CHECKLIST.md";
        public const string StartupSentinel = "STARTUP_SENTINEL.txt";

        public const string Rc0EvidenceJson = "RC0_EVIDENCE.json";
        public const string Rc0RuntimeLog = "RC0_RUNTIME_LOG.txt";
        public const string Rc0VerificationReport = "RC0_VERIFICATION_REPORT.md";
        public const string Rc0AudioPipelineDiagnosticReport = "RC0_AUDIO_PIPELINE_DIAGNOSTIC_REPORT.md";
        public const string Rc0SessionSummary = "RC0_SESSION_SUMMARY.md";
        public const string Rc0ErrorsOnly = "RC0_ERRORS_ONLY.txt";
        public const string Rc0ScreenshotChecklist = "RC0_SCREENSHOT_CHECKLIST.md";
        public const string Rc0StartupSentinel = "RC0_STARTUP_SENTINEL.txt";

        public static string PrimaryRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FemVoiceStudio",
            "Diagnostics");

        public static string DocumentsMirrorRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FemVoiceStudio",
            "Diagnostics");

        public static string LegacyPrimaryRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FemVoiceStudio",
            "RC0_Evidence");

        public static string LegacyDocumentsMirrorRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FemVoiceStudio",
            "RC0_Evidence");

        public static string RuntimeDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FemVoiceStudio",
            "RuntimeDiagnostics");

        public static string MakeEvidenceFolderName(DateTime timestamp) =>
            $"EVIDENCE_{timestamp:yyyy-MM-dd_HHmmss}";

        public static string MakeLegacyEvidenceFolderName(DateTime timestamp) =>
            $"RC0_EVIDENCE_{timestamp:yyyy-MM-dd_HHmmss}";

        public static string MakeRuntimeLogName(DateTime timestamp) =>
            $"RUNTIME_{timestamp:yyyy-MM-dd_HHmmss}.txt";

        public static string MakeLegacyRuntimeLogName(DateTime timestamp) =>
            $"RC0_RUNTIME_{timestamp:yyyy-MM-dd_HHmmss}.txt";
    }
}
