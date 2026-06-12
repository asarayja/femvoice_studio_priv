using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Process-local diagnostics state for report generation attempts.
    /// This records evidence only; it does not mark reports as PASS unless export actually succeeded.
    /// </summary>
    public static class ReportVerificationTracker
    {
        private static readonly object Sync = new();
        private static string _clinicalStatus = "NOT_GENERATED";
        private static string _coachStatus = "NOT_GENERATED";
        private static string _outcomeStatus = "NOT_GENERATED";
        private static string _timelineStatus = "NOT_GENERATED";
        private static readonly List<string> Errors = new();
        private static readonly Dictionary<string, string> GeneratedPaths = new(StringComparer.OrdinalIgnoreCase);
        private static DateTime? _verificationTimestamp;

        public static void Reset()
        {
            lock (Sync)
            {
                _clinicalStatus = "NOT_GENERATED";
                _coachStatus = "NOT_GENERATED";
                _outcomeStatus = "NOT_GENERATED";
                _timelineStatus = "NOT_GENERATED";
                Errors.Clear();
                GeneratedPaths.Clear();
                _verificationTimestamp = null;
            }
        }

        public static void MarkSucceeded(string reportType)
        {
            lock (Sync)
            {
                SetStatus(reportType, "PASS", null);
                _verificationTimestamp = DateTime.UtcNow;
                WriteSnapshotNoThrow();
            }
        }

        public static void VerifyAndMarkSucceeded(string reportType, object report, ExportFormat format, string path)
        {
            lock (Sync)
            {
                var normalizedType = NormalizeReportType(reportType);
                var failure = VerifyGeneratedReport(normalizedType, report, format, path);
                if (failure is null)
                {
                    SetStatus(normalizedType, "PASS", null);
                    GeneratedPaths[normalizedType] = path;
                }
                else
                {
                    SetStatus(normalizedType, "FAIL", failure);
                }

                _verificationTimestamp = DateTime.UtcNow;
                WriteSnapshotNoThrow();
            }
        }

        public static void MarkFailed(string reportType, string error)
        {
            lock (Sync)
            {
                SetStatus(reportType, "FAIL", ToSafeError(error));
                _verificationTimestamp = DateTime.UtcNow;
                WriteSnapshotNoThrow();
            }
        }

        public static ReportVerificationSnapshot Snapshot()
        {
            lock (Sync)
            {
                return new ReportVerificationSnapshot(
                    _clinicalStatus,
                    _coachStatus,
                    _outcomeStatus,
                    _timelineStatus,
                    Errors.ToArray(),
                    GeneratedPaths.Values.ToArray(),
                    GeneratedPaths.Count,
                    _verificationTimestamp);
            }
        }

        private static string? VerifyGeneratedReport(string reportType, object report, ExportFormat format, string path)
        {
            if (report is null)
                return "Report object was empty.";
            if (string.IsNullOrWhiteSpace(path))
                return "Report output path was empty.";
            if (!File.Exists(path))
                return "Report file was not written.";

            var length = new FileInfo(path).Length;
            if (length <= 0)
                return "Report file was empty.";

            if (!ReportMatchesType(reportType, report))
                return "Report object type did not match the requested report type.";

            if (!ReportHasRelevantData(report))
                return "Report did not contain relevant report data.";

            return format switch
            {
                ExportFormat.Pdf => HasPdfMagic(path) ? null : "PDF output did not contain a valid PDF header.",
                ExportFormat.Json => HasReadableJson(path) ? null : "JSON output could not be read.",
                ExportFormat.Csv => HasReadableCsv(path) ? null : "CSV output could not be read.",
                _ => "Unknown export format."
            };
        }

        private static bool ReportMatchesType(string reportType, object report) => reportType switch
        {
            "Clinical" => report is ClinicalReport,
            "Coach" => report is CoachReport,
            "Outcome" => report is OutcomeReport,
            "Timeline" => report is TimelineReport,
            _ => false
        };

        private static bool ReportHasRelevantData(object report) => report switch
        {
            ClinicalReport r => !string.IsNullOrWhiteSpace(r.Title) && r.Outcome is not null && r.Period.GeneratedAt != default,
            CoachReport r => !string.IsNullOrWhiteSpace(r.Title) && r.Outcome is not null && r.Period.GeneratedAt != default,
            OutcomeReport r => !string.IsNullOrWhiteSpace(r.Title) && r.Outcome is not null && r.Period.GeneratedAt != default,
            TimelineReport r => !string.IsNullOrWhiteSpace(r.Title) && r.Outcome is not null && r.Period.GeneratedAt != default,
            _ => false
        };

        private static bool HasPdfMagic(string path)
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[4];
            return stream.Read(header) == 4
                && header[0] == '%'
                && header[1] == 'P'
                && header[2] == 'D'
                && header[3] == 'F';
        }

        private static bool HasReadableJson(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var _ = JsonDocument.Parse(stream);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasReadableCsv(string path)
        {
            var text = File.ReadAllText(path);
            return !string.IsNullOrWhiteSpace(text)
                && text.Contains(',', StringComparison.Ordinal)
                && text.Contains(Environment.NewLine, StringComparison.Ordinal);
        }

        private static void SetStatus(string reportType, string status, string? error)
        {
            switch (NormalizeReportType(reportType))
            {
                case "Clinical":
                    _clinicalStatus = status;
                    break;
                case "Coach":
                    _coachStatus = status;
                    break;
                case "Outcome":
                    _outcomeStatus = status;
                    break;
                case "Timeline":
                    _timelineStatus = status;
                    break;
                default:
                    error = string.IsNullOrWhiteSpace(error)
                        ? $"Unknown report type: {reportType}"
                        : $"Unknown report type: {reportType}; {error}";
                    break;
            }

            if (!string.IsNullOrWhiteSpace(error))
                Errors.Add($"{NormalizeReportType(reportType)}: {ToSafeError(error)}");
        }

        private static string NormalizeReportType(string reportType)
        {
            if (string.Equals(reportType, "Clinical", StringComparison.OrdinalIgnoreCase))
                return "Clinical";
            if (string.Equals(reportType, "Coach", StringComparison.OrdinalIgnoreCase)
                || string.Equals(reportType, "Coaching", StringComparison.OrdinalIgnoreCase))
                return "Coach";
            if (string.Equals(reportType, "Outcome", StringComparison.OrdinalIgnoreCase))
                return "Outcome";
            if (string.Equals(reportType, "Timeline", StringComparison.OrdinalIgnoreCase))
                return "Timeline";

            return reportType ?? string.Empty;
        }

        private static string ToSafeError(string? error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return "Report verification failed.";

            var safe = error.Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase);
            foreach (var invalid in Path.GetInvalidPathChars())
                safe = safe.Replace(invalid.ToString(), string.Empty);

            return safe.Length > 220 ? safe[..220] : safe;
        }

        private static void WriteSnapshotNoThrow()
        {
            try
            {
                var snapshot = Snapshot();
                Directory.CreateDirectory(DiagnosticsNaming.PrimaryRoot);
                var payload = new
                {
                    ValidationProfile = DiagnosticsNaming.ValidationProfile,
                    ReportVerification = snapshot,
                    ReportsGenerated = new
                    {
                        Clinical = snapshot.ClinicalReportGenerated,
                        Coach = snapshot.CoachReportGenerated,
                        Outcome = snapshot.OutcomeReportGenerated,
                        Timeline = snapshot.TimelineReportGenerated
                    },
                    snapshot.GeneratedReportPaths,
                    snapshot.VerifiedReportCount,
                    snapshot.ReportVerificationTimestamp
                };
                File.WriteAllText(
                    Path.Combine(DiagnosticsNaming.PrimaryRoot, "REPORT_VERIFICATION_STATUS.json"),
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("ReportVerificationTracker.WriteSnapshot", DiagnosticsNaming.PrimaryRoot, ex);
            }
        }
    }

    public sealed record ReportVerificationSnapshot(
        string ClinicalReportStatus,
        string CoachReportStatus,
        string OutcomeReportStatus,
        string TimelineReportStatus,
        string[] ReportVerificationErrors,
        string[] GeneratedReportPaths,
        int VerifiedReportCount,
        DateTime? ReportVerificationTimestamp)
    {
        public bool ClinicalReportGenerated => string.Equals(ClinicalReportStatus, "PASS", StringComparison.OrdinalIgnoreCase);
        public bool CoachReportGenerated => string.Equals(CoachReportStatus, "PASS", StringComparison.OrdinalIgnoreCase);
        public bool OutcomeReportGenerated => string.Equals(OutcomeReportStatus, "PASS", StringComparison.OrdinalIgnoreCase);
        public bool TimelineReportGenerated => string.Equals(TimelineReportStatus, "PASS", StringComparison.OrdinalIgnoreCase);
    }
}
