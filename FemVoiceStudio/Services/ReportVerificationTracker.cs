using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Process-local diagnostics state for report generation attempts.
    /// This records evidence only; it does not mark reports as PASS unless export actually succeeded.
    /// </summary>
    public static class ReportVerificationTracker
    {
        private static readonly object Sync = new();
        private static string _clinicalStatus = "NOT_VERIFIED";
        private static string _coachStatus = "NOT_VERIFIED";
        private static string _outcomeStatus = "NOT_VERIFIED";
        private static string _timelineStatus = "NOT_VERIFIED";
        private static readonly List<string> Errors = new();

        public static void Reset()
        {
            lock (Sync)
            {
                _clinicalStatus = "NOT_VERIFIED";
                _coachStatus = "NOT_VERIFIED";
                _outcomeStatus = "NOT_VERIFIED";
                _timelineStatus = "NOT_VERIFIED";
                Errors.Clear();
            }
        }

        public static void MarkSucceeded(string reportType)
        {
            lock (Sync)
            {
                SetStatus(reportType, "PASS", null);
                WriteSnapshotNoThrow();
            }
        }

        public static void MarkFailed(string reportType, string error)
        {
            lock (Sync)
            {
                SetStatus(reportType, "FAIL", error);
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
                    Errors.ToArray());
            }
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
                Errors.Add($"{NormalizeReportType(reportType)}: {error}");
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
                    }
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
        string[] ReportVerificationErrors)
    {
        public bool ClinicalReportGenerated => string.Equals(ClinicalReportStatus, "PASS", StringComparison.OrdinalIgnoreCase);
        public bool CoachReportGenerated => string.Equals(CoachReportStatus, "PASS", StringComparison.OrdinalIgnoreCase);
        public bool OutcomeReportGenerated => string.Equals(OutcomeReportStatus, "PASS", StringComparison.OrdinalIgnoreCase);
        public bool TimelineReportGenerated => string.Equals(TimelineReportStatus, "PASS", StringComparison.OrdinalIgnoreCase);
    }
}
