using FemVoiceStudio.Services;
using FemVoiceStudio.Models;
using System;
using System.IO;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public sealed class ReportVerificationTrackerTests
    {
        [Fact]
        public void NotAttemptedReportsRemainNotVerified()
        {
            ReportVerificationTracker.Reset();

            var snapshot = ReportVerificationTracker.Snapshot();

            Assert.Equal("NOT_GENERATED", snapshot.ClinicalReportStatus);
            Assert.Equal("NOT_GENERATED", snapshot.CoachReportStatus);
            Assert.Equal("NOT_GENERATED", snapshot.OutcomeReportStatus);
            Assert.Equal("NOT_GENERATED", snapshot.TimelineReportStatus);
            Assert.Equal(0, snapshot.VerifiedReportCount);
            Assert.Null(snapshot.ReportVerificationTimestamp);
        }

        [Fact]
        public void SuccessfulReportGenerationSetsStatusToPass()
        {
            ReportVerificationTracker.Reset();

            ReportVerificationTracker.MarkSucceeded("Clinical");
            var snapshot = ReportVerificationTracker.Snapshot();

            Assert.Equal("PASS", snapshot.ClinicalReportStatus);
            Assert.True(snapshot.ClinicalReportGenerated);
            Assert.NotNull(snapshot.ReportVerificationTimestamp);
        }

        [Fact]
        public void VerifiedReportGenerationSetsStatusToPassWithMetadata()
        {
            ReportVerificationTracker.Reset();
            var path = Path.Combine(Path.GetTempPath(), $"FemVoice_Report_{Guid.NewGuid():N}.json");
            try
            {
                var report = MakeOutcomeReport();
                using (var stream = File.Create(path))
                    new ExportWriter().Write(report, ExportFormat.Json, stream);

                ReportVerificationTracker.VerifyAndMarkSucceeded("Outcome", report, ExportFormat.Json, path);
                var snapshot = ReportVerificationTracker.Snapshot();

                Assert.Equal("PASS", snapshot.OutcomeReportStatus);
                Assert.True(snapshot.OutcomeReportGenerated);
                Assert.True(snapshot.VerifiedReportCount >= 1);
                Assert.Contains(path, snapshot.GeneratedReportPaths);
                Assert.NotNull(snapshot.ReportVerificationTimestamp);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void VerifiedReportGenerationFailsWhenFileIsMissing()
        {
            ReportVerificationTracker.Reset();
            var path = Path.Combine(Path.GetTempPath(), $"FemVoice_Missing_{Guid.NewGuid():N}.json");

            ReportVerificationTracker.VerifyAndMarkSucceeded("Outcome", MakeOutcomeReport(), ExportFormat.Json, path);
            var snapshot = ReportVerificationTracker.Snapshot();

            Assert.Equal("FAIL", snapshot.OutcomeReportStatus);
            Assert.Contains(snapshot.ReportVerificationErrors, e => e.Contains("not written", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void FailedReportGenerationSetsFailAndCapturesError()
        {
            ReportVerificationTracker.Reset();

            ReportVerificationTracker.MarkFailed("Coach", "PDF writer failed");
            var snapshot = ReportVerificationTracker.Snapshot();

            Assert.Equal("FAIL", snapshot.CoachReportStatus);
            Assert.Contains(snapshot.ReportVerificationErrors, e => e.Contains("PDF writer failed"));
        }

        private static OutcomeReport MakeOutcomeReport()
        {
            var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            return new OutcomeReport
            {
                Title = "Outcome Summary",
                Period = new ReportPeriod
                {
                    PeriodStart = now.AddDays(-30),
                    PeriodEnd = now,
                    GeneratedAt = now
                },
                Outcome = new OutcomeProfile
                {
                    UserId = 1,
                    GeneratedAt = now,
                    HasEnoughData = true
                },
                HasEnoughData = true
            };
        }
    }
}
