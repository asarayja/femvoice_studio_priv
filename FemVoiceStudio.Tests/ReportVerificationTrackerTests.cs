using FemVoiceStudio.Services;
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

            Assert.Equal("NOT_VERIFIED", snapshot.ClinicalReportStatus);
            Assert.Equal("NOT_VERIFIED", snapshot.CoachReportStatus);
            Assert.Equal("NOT_VERIFIED", snapshot.OutcomeReportStatus);
            Assert.Equal("NOT_VERIFIED", snapshot.TimelineReportStatus);
        }

        [Fact]
        public void SuccessfulReportGenerationSetsStatusToPass()
        {
            ReportVerificationTracker.Reset();

            ReportVerificationTracker.MarkSucceeded("Clinical");
            ReportVerificationTracker.MarkSucceeded("Coach");
            ReportVerificationTracker.MarkSucceeded("Outcome");
            ReportVerificationTracker.MarkSucceeded("Timeline");
            var snapshot = ReportVerificationTracker.Snapshot();

            Assert.Equal("PASS", snapshot.ClinicalReportStatus);
            Assert.Equal("PASS", snapshot.CoachReportStatus);
            Assert.Equal("PASS", snapshot.OutcomeReportStatus);
            Assert.Equal("PASS", snapshot.TimelineReportStatus);
            Assert.True(snapshot.ClinicalReportGenerated);
            Assert.True(snapshot.CoachReportGenerated);
            Assert.True(snapshot.OutcomeReportGenerated);
            Assert.True(snapshot.TimelineReportGenerated);
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
    }
}
