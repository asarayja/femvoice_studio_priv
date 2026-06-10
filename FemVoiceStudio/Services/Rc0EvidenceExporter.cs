using System;
using System.IO;
using System.Text;
using System.Text.Json;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Developer-only RC-0 evidence export. It writes files for validation review and
    /// does not add a product report type or any UI surface.
    /// </summary>
    public static class Rc0EvidenceExporter
    {
        public sealed record SessionEvidence
        {
            public int SessionId { get; init; }
            public int ExerciseId { get; init; }
            public string ExerciseName { get; init; } = "";
            public string Language { get; init; } = "";
            public DateTime StartTime { get; init; }
            public DateTime EndTime { get; init; }
            public TimeSpan Duration { get; init; }
            public string CompletionStatus { get; init; } = "COMPLETED";
            public double Score { get; init; }
            public string ScoreSource { get; init; } = "";
            public long PitchDetectorCalledCount { get; init; }
            public long PitchSamplesCount { get; init; }
            public long PitchRejectedCount { get; init; }
            public long ResonanceSamplesCount { get; init; }
            // Resonance engine diagnostics
            public long ResonanceEngineCalledCount { get; init; }
            public long ResonanceAcceptedCount { get; init; }
            public long ResonanceRejectedCount { get; init; }
            public string[] ResonanceRejectedReasons { get; init; } = Array.Empty<string>();
            public long ResonanceFallbackCount { get; init; }
            public long ResonanceRealSampleCount { get; init; }
            public long GraphUpdateCount { get; init; }
            public int GuidanceItemCount { get; init; }
            public bool SmartCoachGenerated { get; init; }
            public bool VoiceHealthEvaluated { get; init; }
            public bool AnalyticsWritten { get; init; }
            public bool PersistenceSaved { get; init; }
            public bool PersistenceReadBack { get; init; }
            // Tri-state statuses for verification steps. Use "PASS", "FAIL", "NOT_VERIFIED" or "NOT_APPLICABLE".
            public string PersistenceReadBackStatus { get; init; } = "NOT_VERIFIED";
            public string ClinicalReportStatus { get; init; } = "NOT_VERIFIED";
            public string CoachReportStatus { get; init; } = "NOT_VERIFIED";
            public string OutcomeReportStatus { get; init; } = "NOT_VERIFIED";
            public string TimelineReportStatus { get; init; } = "NOT_VERIFIED";
            public string[] ReportVerificationErrors { get; init; } = Array.Empty<string>();
            public bool ClinicalReportGenerated { get; init; }
            public bool CoachReportGenerated { get; init; }
            public bool OutcomeReportGenerated { get; init; }
            public bool TimelineReportGenerated { get; init; }
            public string[] Errors { get; init; } = Array.Empty<string>();
            public string[] Warnings { get; init; } = Array.Empty<string>();

            // Faste forklaringer om hva evidensen dekker (counter-semantikk o.l.).
            // Skilles fra Warnings fordi Warnings nedgraderer Result til WARNING —
            // en alltid-tilstede note ville gjort PASS uoppnåelig.
            public string[] Notes { get; init; } = Array.Empty<string>();
        }

        // Primary evidence root lives in %LOCALAPPDATA%: it is never OneDrive-redirected
        // and not subject to Controlled Folder Access, unlike Documents.
        private static readonly string PrimaryEvidenceRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FemVoiceStudio",
            "RC0_Evidence");

        private static readonly string DocumentsEvidenceRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FemVoiceStudio",
            "RC0_Evidence");

        public static string EvidenceRoot => PrimaryEvidenceRoot;
        public static string DocumentsMirrorRoot => DocumentsEvidenceRoot;

        public static string Export(SessionEvidence evidence, AudioCaptureDiagnosticsSnapshot audio)
        {
            try
            {
                var folder = Path.Combine(PrimaryEvidenceRoot, $"RC0_EVIDENCE_{DateTime.Now:yyyy-MM-dd_HHmmss}");
                // To eksporter i samme sekund (øvelse + forsidemonitor) må ikke
                // overskrive hverandre.
                for (var suffix = 2; Directory.Exists(folder); suffix++)
                    folder = Path.Combine(PrimaryEvidenceRoot, $"RC0_EVIDENCE_{DateTime.Now:yyyy-MM-dd_HHmmss}_{suffix}");
                Directory.CreateDirectory(folder);

                var result = ResolveResult(evidence, audio);
                WriteSessionSummary(folder, evidence, audio, result);
                WriteRuntimeLog(folder);
                WriteVerificationReport(folder, evidence, audio, result);
                WriteDiagnosticReport(folder, evidence, audio, result);
                WriteJson(folder, evidence, audio, result);
                WriteErrorsOnly(folder, evidence, audio);
                WriteScreenshotChecklist(folder);

                Rc0RuntimeLog.Write("RC0EvidenceExport", $"ExportedFolder=\"{folder}\"; Result={result}");
                TryMirrorToDocuments(folder);
                return folder;
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("Rc0EvidenceExporter.Export", PrimaryEvidenceRoot, ex);
                Rc0RuntimeLog.Write("RC0EvidenceExport", $"Export FAILED; {ex.GetType().Name}: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Best-effort visibility copy into Documents\FemVoiceStudio\RC0_Evidence so the
        /// user finds the evidence without digging into %LOCALAPPDATA%.
        /// </summary>
        private static void TryMirrorToDocuments(string folder)
        {
            try
            {
                var destination = Path.Combine(DocumentsEvidenceRoot, Path.GetFileName(folder));
                Directory.CreateDirectory(destination);
                foreach (var file in Directory.GetFiles(folder))
                    File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("Rc0EvidenceExporter.MirrorToDocuments", DocumentsEvidenceRoot, ex);
            }
        }

        private static string ResolveResult(SessionEvidence evidence, AudioCaptureDiagnosticsSnapshot audio)
        {
            if (audio.DataAvailableCount <= 0 || evidence.PitchDetectorCalledCount <= 0)
                return "BLOCKED";

            // Bare harde feil gir FAIL. SILENCE_GATE/SIGNAL_LEVEL beskriver den SISTE
            // lydrammen ved stopp — en bruker som er stille idet hun klikker Stopp er
            // ikke en mislykket økt; det rapporteres som WARNING under.
            var hardFailure = audio.FailureClassification
                is AudioFailureClassification.CAPTURE_STOPS
                or AudioFailureClassification.DEVICE_SELECTION_ERROR
                or AudioFailureClassification.WINDOWS_OR_DRIVER_LEVEL_ISSUE;

            if (evidence.Errors.Length > 0 || hardFailure)
                return "FAIL";

            if (!evidence.PersistenceSaved || !evidence.AnalyticsWritten || evidence.PitchSamplesCount <= 0)
                return "FAIL";

            return evidence.Warnings.Length > 0 || audio.FailureClassification != AudioFailureClassification.UNKNOWN
                ? "WARNING"
                : "PASS";
        }

        private static void WriteSessionSummary(string folder, SessionEvidence evidence, AudioCaptureDiagnosticsSnapshot audio, string result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# RC0 Session Summary");
            sb.AppendLine();
            sb.AppendLine($"- App version / build: {typeof(Rc0EvidenceExporter).Assembly.GetName().Version}");
            sb.AppendLine($"- Test date/time: {DateTime.Now:O}");
            sb.AppendLine($"- Language: {evidence.Language}");
            sb.AppendLine($"- Exercise tested: {evidence.ExerciseName}");
            sb.AppendLine($"- Session ID: {evidence.SessionId}");
            sb.AppendLine($"- Duration: {evidence.Duration}");
            sb.AppendLine($"- Completion status: {evidence.CompletionStatus}");
            sb.AppendLine($"- Result: {result}");
            sb.AppendLine($"- Device: {audio.DeviceName}");
            sb.AppendLine($"- Failure classification: {audio.FailureClassification}");
            File.WriteAllText(Path.Combine(folder, "RC0_SESSION_SUMMARY.md"), sb.ToString());
        }

        private static void WriteRuntimeLog(string folder)
        {
            var destination = Path.Combine(folder, "RC0_RUNTIME_LOG.txt");
            try
            {
                if (Rc0RuntimeLog.TryCopyTo(destination))
                    return;

                File.WriteAllText(destination,
                    $"No RC0 runtime log existed at \"{Rc0RuntimeLog.CurrentLogPath}\" for this export.");
            }
            catch (Exception ex)
            {
                // A failed copy must not masquerade as "no log existed".
                Rc0WriteFailureSink.Report("Rc0EvidenceExporter.WriteRuntimeLog", destination, ex);
                try
                {
                    File.WriteAllText(destination,
                        $"Failed to copy runtime log from \"{Rc0RuntimeLog.CurrentLogPath}\": {ex.GetType().Name}: {ex.Message}");
                }
                catch { }
            }
        }

        private static void WriteVerificationReport(string folder, SessionEvidence evidence, AudioCaptureDiagnosticsSnapshot audio, string result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# RC0 Verification Report");
            sb.AppendLine();
            AppendCheck(sb, "Start Økt", evidence.SessionId > 0 && (audio.IsRecording || audio.DataAvailableCount > 0), "Session was created after audio start was attempted.");
            AppendCheck(sb, "Audio pipeline", audio.DataAvailableCount > 0, $"DataAvailableCount={audio.DataAvailableCount}");
            AppendCheck(sb, "Pitch data", evidence.PitchSamplesCount > 0, $"PitchSamplesCount={evidence.PitchSamplesCount}, Calls={evidence.PitchDetectorCalledCount}");
            // Prefer real-sample counter (engine-accepted). Fallback samples are not counted as real evidence.
            AppendCheck(sb, "Resonance data", evidence.ResonanceRealSampleCount > 0, $"ResonanceRealSampleCount={evidence.ResonanceRealSampleCount}, EngineCalls={evidence.ResonanceEngineCalledCount}, Accepted={evidence.ResonanceAcceptedCount}, Rejected={evidence.ResonanceRejectedCount}");
            AppendCheck(sb, "Guidance items", evidence.GuidanceItemCount > 0, $"GuidanceItemCount={evidence.GuidanceItemCount}");
            AppendCheck(sb, "SmartCoach loading", evidence.SmartCoachGenerated, "Session insight / coach output generated.");
            AppendCheck(sb, "SmartCoach DateTime parsing", evidence.SmartCoachGenerated, "No DateTime parsing error was reported in this run.");
            AppendCheck(sb, "Voice Health policies", evidence.VoiceHealthEvaluated, "Health score was evaluated during the exercise.");
            // Use tri-state statuses when available
            AppendCheck(sb, "Analytics write/read", evidence.AnalyticsWritten && string.Equals(evidence.PersistenceReadBackStatus, "PASS", StringComparison.OrdinalIgnoreCase), $"AnalyticsWritten={evidence.AnalyticsWritten}, ReadBackStatus={evidence.PersistenceReadBackStatus}");
            AppendCheck(sb, "Persistence save/load", evidence.PersistenceSaved && string.Equals(evidence.PersistenceReadBackStatus, "PASS", StringComparison.OrdinalIgnoreCase), $"Saved={evidence.PersistenceSaved}, ReadBackStatus={evidence.PersistenceReadBackStatus}");
            AppendCheck(sb, "Clinical Report", string.Equals(evidence.ClinicalReportStatus, "PASS", StringComparison.OrdinalIgnoreCase), $"ClinicalReportStatus={evidence.ClinicalReportStatus}");
            AppendCheck(sb, "Coach Report", string.Equals(evidence.CoachReportStatus, "PASS", StringComparison.OrdinalIgnoreCase), $"CoachReportStatus={evidence.CoachReportStatus}");
            AppendCheck(sb, "Outcome Report", string.Equals(evidence.OutcomeReportStatus, "PASS", StringComparison.OrdinalIgnoreCase), $"OutcomeReportStatus={evidence.OutcomeReportStatus}");
            AppendCheck(sb, "Timeline Report", string.Equals(evidence.TimelineReportStatus, "PASS", StringComparison.OrdinalIgnoreCase), $"TimelineReportStatus={evidence.TimelineReportStatus}");
            sb.AppendLine();
            sb.AppendLine($"## RC-0 Result: {result}");
            sb.AppendLine();
            sb.AppendLine($"- FailureClassification: {audio.FailureClassification}");
            sb.AppendLine($"- ScoreSource: {evidence.ScoreSource}");
            File.WriteAllText(Path.Combine(folder, "RC0_VERIFICATION_REPORT.md"), sb.ToString());
        }

        private static void WriteDiagnosticReport(string folder, SessionEvidence evidence, AudioCaptureDiagnosticsSnapshot audio, string result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# RC0 Audio Pipeline Diagnostic Report");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"Result: {result}");
            sb.AppendLine($"Root cause classification: {audio.FailureClassification}");
            sb.AppendLine();
            sb.AppendLine("## Device Comparison");
            sb.AppendLine($"- DeviceName: {audio.DeviceName}");
            sb.AppendLine($"- DeviceId: {audio.DeviceId}");
            sb.AppendLine($"- DefaultInputDeviceName: {audio.DefaultInputDeviceName}");
            sb.AppendLine($"- DefaultCommunicationsDeviceName: {audio.DefaultCommunicationsDeviceName}");
            sb.AppendLine($"- DeviceSelectedByFemVoice: {audio.DeviceSelectedByFemVoice}");
            sb.AppendLine($"- DeviceChangedDuringSession: {audio.DeviceChangedDuringSession}");
            sb.AppendLine();
            sb.AppendLine("## Front-page Pitch Graph Findings");
            sb.AppendLine("Front-page evidence is written to RC0_RUNTIME_LOG.txt under FrontPagePitchMonitor and PitchPipeline entries.");
            sb.AppendLine();
            sb.AppendLine("## Exercise Score Findings");
            sb.AppendLine($"- Score: {evidence.Score:F1}");
            sb.AppendLine($"- ScoreSource: {evidence.ScoreSource}");
            sb.AppendLine();
            sb.AppendLine("## Audio Capture Timeline");
            sb.AppendLine($"- AudioCaptureStarted: {audio.IsRecording || audio.DataAvailableCount > 0}");
            sb.AppendLine($"- DataAvailableCount: {audio.DataAvailableCount}");
            sb.AppendLine($"- BytesReceived: {audio.BytesReceived}");
            sb.AppendLine($"- SamplesReceived: {audio.SamplesReceived}");
            sb.AppendLine($"- CallbackIntervalMs: {audio.CallbackIntervalMs:F1}");
            sb.AppendLine($"- DroppedCallbackCount: {audio.DroppedCallbackCount}");
            sb.AppendLine($"- LastDataAvailableTime: {audio.LastDataAvailableTime:O}");
            sb.AppendLine($"- TimeSinceLastAudioFrame: {audio.TimeSinceLastAudioFrameSeconds:F2}s");
            sb.AppendLine();
            sb.AppendLine("## Input Level Timeline");
            sb.AppendLine($"- AverageRmsLevel: {audio.RmsLevel:F5}");
            sb.AppendLine($"- PeakLevel: {audio.PeakLevel:F5}");
            sb.AppendLine($"- InputLevelPercent: {audio.InputLevelPercent:F1}");
            sb.AppendLine($"- NoiseFloorEstimate: {audio.NoiseFloorEstimate:F5}");
            sb.AppendLine($"- SignalToNoiseEstimateDb: {audio.SignalToNoiseEstimateDb:F1}");
            sb.AppendLine($"- SustainedRmsLevel: {audio.RmsLevel:F5}");
            sb.AppendLine($"- LevelCollapsed: {audio.LevelCollapsed}");
            sb.AppendLine();
            sb.AppendLine("## Pitch Detection Timeline");
            sb.AppendLine($"- PitchDetectorCalledCount: {evidence.PitchDetectorCalledCount}");
            sb.AppendLine($"- PitchSamplesCount: {evidence.PitchSamplesCount}");
            sb.AppendLine($"- PitchRejectedCount: {evidence.PitchRejectedCount}");
            sb.AppendLine($"- PitchDetectionSuccessRate: {SuccessRate(evidence.PitchSamplesCount, evidence.PitchDetectorCalledCount):P1}");
            sb.AppendLine();
            sb.AppendLine("## Graph Update Timeline");
            sb.AppendLine($"- GraphUpdateCount: {evidence.GraphUpdateCount}");
            sb.AppendLine();
            sb.AppendLine("## Resonance Engine Diagnostics");
            sb.AppendLine($"- ResonanceEngineCalledCount: {evidence.ResonanceEngineCalledCount}");
            sb.AppendLine($"- ResonanceAcceptedCount: {evidence.ResonanceAcceptedCount}");
            sb.AppendLine($"- ResonanceRejectedCount: {evidence.ResonanceRejectedCount}");
            sb.AppendLine($"- ResonanceRejectedReasons: {string.Join("; ", evidence.ResonanceRejectedReasons ?? Array.Empty<string>())}");
            sb.AppendLine($"- ResonanceFallbackCount: {evidence.ResonanceFallbackCount}");
            sb.AppendLine($"- ResonanceRealSampleCount: {evidence.ResonanceRealSampleCount}");
            sb.AppendLine();
            sb.AppendLine("## Score Calculation Evidence");
            sb.AppendLine($"- ScoreSource: {evidence.ScoreSource}");
            sb.AppendLine($"- Score: {evidence.Score:F1}");
            sb.AppendLine();
            sb.AppendLine("## Recommended RC-0 Fix");
            sb.AppendLine("Use this evidence to classify whether the issue is capture, signal level, silence gate, pitch rejection, graph update, or score fallback before changing clinical scoring.");
            File.WriteAllText(Path.Combine(folder, "RC0_AUDIO_PIPELINE_DIAGNOSTIC_REPORT.md"), sb.ToString());
        }

        private static void WriteJson(string folder, SessionEvidence evidence, AudioCaptureDiagnosticsSnapshot audio, string result)
        {
            var payload = new
            {
                evidence.SessionId,
                evidence.ExerciseId,
                evidence.ExerciseName,
                StartTime = evidence.StartTime,
                EndTime = evidence.EndTime,
                Duration = evidence.Duration.ToString(),
                evidence.CompletionStatus,
                AudioStarted = audio.IsRecording || audio.DataAvailableCount > 0,
                RecordingActive = audio.IsRecording,
                DataAvailableCount = audio.DataAvailableCount,
                PitchSamplesCount = evidence.PitchSamplesCount,
                ResonanceSamplesCount = evidence.ResonanceSamplesCount,
                ResonanceEngineCalledCount = evidence.ResonanceEngineCalledCount,
                ResonanceAcceptedCount = evidence.ResonanceAcceptedCount,
                ResonanceRejectedCount = evidence.ResonanceRejectedCount,
                ResonanceRejectedReasons = evidence.ResonanceRejectedReasons,
                ResonanceFallbackCount = evidence.ResonanceFallbackCount,
                ResonanceRealSampleCount = evidence.ResonanceRealSampleCount,
                GuidanceItemCount = evidence.GuidanceItemCount,
                AnalyticsWritten = evidence.AnalyticsWritten,
                PersistenceSaved = evidence.PersistenceSaved,
                PersistenceReadBack = evidence.PersistenceReadBack,
                PersistenceReadBackStatus = evidence.PersistenceReadBackStatus,
                SmartCoachGenerated = evidence.SmartCoachGenerated,
                VoiceHealthEvaluated = evidence.VoiceHealthEvaluated,
                ReportsGenerated = new
                {
                    Clinical = evidence.ClinicalReportGenerated,
                    Coach = evidence.CoachReportGenerated,
                    Outcome = evidence.OutcomeReportGenerated,
                    Timeline = evidence.TimelineReportGenerated
                },
                ReportVerification = new
                {
                    PersistenceReadBackStatus = evidence.PersistenceReadBackStatus,
                    ClinicalReportStatus = evidence.ClinicalReportStatus,
                    CoachReportStatus = evidence.CoachReportStatus,
                    OutcomeReportStatus = evidence.OutcomeReportStatus,
                    TimelineReportStatus = evidence.TimelineReportStatus,
                    ReportVerificationErrors = evidence.ReportVerificationErrors
                },
                DeviceName = audio.DeviceName,
                DeviceId = audio.DeviceId,
                CaptureStatus = audio.FailureClassification == AudioFailureClassification.UNKNOWN ? "UNKNOWN_OR_OK" : audio.FailureClassification.ToString(),
                TimeSinceLastAudioFrame = audio.TimeSinceLastAudioFrameSeconds,
                AverageRmsLevel = audio.RmsLevel,
                SustainedRmsLevel = audio.RmsLevel,
                PitchDetectionSuccessRate = SuccessRate(evidence.PitchSamplesCount, evidence.PitchDetectorCalledCount),
                GraphUpdateCount = evidence.GraphUpdateCount,
                evidence.ScoreSource,
                FailureClassification = audio.FailureClassification.ToString(),
                evidence.Errors,
                evidence.Warnings,
                evidence.Notes,
                Result = result
            };

            File.WriteAllText(
                Path.Combine(folder, "RC0_EVIDENCE.json"),
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static void WriteErrorsOnly(string folder, SessionEvidence evidence, AudioCaptureDiagnosticsSnapshot audio)
        {
            var sb = new StringBuilder();
            foreach (var error in evidence.Errors)
                sb.AppendLine(error);

            if (audio.DataAvailableCount <= 0)
                sb.AppendLine("Missing audio data: DataAvailableCount=0");
            if (evidence.PitchDetectorCalledCount <= 0)
                sb.AppendLine("Missing pitch detection: PitchDetectorCalledCount=0");
            if (evidence.PersistenceSaved && !evidence.PersistenceReadBack)
                sb.AppendLine("Persistence readback was not verified.");
            if (Rc0WriteFailureSink.FirstWriteError is { } firstWriteError)
                sb.AppendLine($"RC0 write failure detected this process: {firstWriteError}");

            TryAppendRuntimeErrorLines(sb);
            File.WriteAllText(Path.Combine(folder, "RC0_ERRORS_ONLY.txt"), sb.Length == 0 ? "No errors captured." : sb.ToString());
        }

        private static void TryAppendRuntimeErrorLines(StringBuilder sb)
        {
            try
            {
                if (!File.Exists(Rc0RuntimeLog.CurrentLogPath))
                    return;

                foreach (var line in File.ReadLines(Rc0RuntimeLog.CurrentLogPath))
                {
                    if (line.Contains("FAILED", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("Exception", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("missing", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("null", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine(line);
                    }
                }
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("Rc0EvidenceExporter.TryAppendRuntimeErrorLines", Rc0RuntimeLog.CurrentLogPath, ex);
            }
        }

        private static void WriteScreenshotChecklist(string folder)
        {
            File.WriteAllText(Path.Combine(folder, "RC0_SCREENSHOT_CHECKLIST.md"),
                "# RC0 Screenshot Checklist\n\n" +
                "- [ ] Exercise list looks correct\n" +
                "- [ ] Exercise icons visible\n" +
                "- [ ] No mojibake\n" +
                "- [ ] Guidance text visible\n" +
                "- [ ] Timer works\n" +
                "- [ ] Pitch graph updates\n" +
                "- [ ] Resonance graph updates\n" +
                "- [ ] Reports open\n" +
                "- [ ] Reports contain session data\n");
        }

        private static void AppendCheck(StringBuilder sb, string name, bool passed, string detail)
            => sb.AppendLine($"- {(passed ? "PASS" : "FAIL")} {name}: {detail}");

        private static double SuccessRate(long success, long attempts)
            => attempts <= 0 ? 0 : (double)success / attempts;
    }
}
