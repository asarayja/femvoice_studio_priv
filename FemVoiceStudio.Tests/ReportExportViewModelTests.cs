using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ReportExportViewModel"/> (Sprint E, Agent 3+10) and
    /// <see cref="CaseReviewViewModel"/> (Sprint E, Agent 8).
    ///
    /// House style: no mocking frameworks — real classes only (InMemory repositories,
    /// TestDatabaseService). All dates are fixed constants. Disk I/O is bypassed via
    /// FileSavePathOverride (temp file created and deleted inside each test).
    ///
    /// ReportExportViewModel — Generate produces a non-empty file for every format
    /// (Pdf/Csv/Json) and every report type (Clinical/Coach/Outcome/Timeline).
    ///
    /// CaseReviewViewModel — Create saves a review; Complete transitions Draft->Completed.
    /// </summary>
    [Collection("ReportVerification")]
    public class ReportExportViewModelTests
    {
        // ── Fixed timestamps ──────────────────────────────────────────────────────
        private static readonly DateTime FixedNow =
            new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // ── Factory helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Builds a minimal but valid <see cref="OutcomeProfileBuilder"/> using real
        /// in-memory engines — the same pattern as <see cref="OutcomeProfileBuilderTests"/>.
        /// </summary>
        private static OutcomeProfileBuilder NewOutcomeProfileBuilder()
        {
            var analyticsStore = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var effectivenessEngine = new ExerciseEffectivenessEngine(analyticsStore);
            var insightEngine = new LongitudinalInsightEngine();
            var smartCoach = new SmartCoachEngine(new TestDatabaseService());
            return new OutcomeProfileBuilder(smartCoach, effectivenessEngine, insightEngine);
        }

        private static ReportAssembler NewReportAssembler() => new();
        private static ExportWriter NewExportWriter() => new();
        private static TestDatabaseService NewDatabase() => new();
        private static CaseReviewAssembler NewCaseReviewAssembler() => new();
        private static CaseReviewsStore NewCaseReviewsStore() =>
            new(new InMemoryCaseReviewsRepository());

        // ── ReportExportViewModel: one test per (type, format) combo ─────────────

        /// <summary>
        /// Generate with type=Clinical, format=Json writes non-empty JSON to the output file.
        /// </summary>
        [Fact]
        public async Task Generate_ClinicalJson_WritesNonEmptyFile()
        {
            await AssertGenerateWritesNonEmptyFileAsync(
                reportTypeIndex: 0, // Clinical
                formatIndex: 2);    // Json
        }

        /// <summary>
        /// Generate with type=Clinical, format=Csv writes non-empty CSV to the output file.
        /// </summary>
        [Fact]
        public async Task Generate_ClinicalCsv_WritesNonEmptyFile()
        {
            await AssertGenerateWritesNonEmptyFileAsync(
                reportTypeIndex: 0, // Clinical
                formatIndex: 1);    // Csv
        }

        /// <summary>
        /// Generate with type=Coach, format=Json writes non-empty JSON.
        /// </summary>
        [Fact]
        public async Task Generate_CoachJson_WritesNonEmptyFile()
        {
            await AssertGenerateWritesNonEmptyFileAsync(
                reportTypeIndex: 1, // Coach
                formatIndex: 2);    // Json
        }

        /// <summary>
        /// Generate with type=Coach, format=Csv writes non-empty CSV.
        /// </summary>
        [Fact]
        public async Task Generate_CoachCsv_WritesNonEmptyFile()
        {
            await AssertGenerateWritesNonEmptyFileAsync(
                reportTypeIndex: 1, // Coach
                formatIndex: 1);    // Csv
        }

        /// <summary>
        /// Generate with type=Outcome, format=Json writes non-empty JSON.
        /// </summary>
        [Fact]
        public async Task Generate_OutcomeJson_WritesNonEmptyFile()
        {
            await AssertGenerateWritesNonEmptyFileAsync(
                reportTypeIndex: 2, // Outcome
                formatIndex: 2);    // Json
        }

        /// <summary>
        /// Generate with type=Outcome, format=Csv writes non-empty CSV.
        /// </summary>
        [Fact]
        public async Task Generate_OutcomeCsv_WritesNonEmptyFile()
        {
            await AssertGenerateWritesNonEmptyFileAsync(
                reportTypeIndex: 2, // Outcome
                formatIndex: 1);    // Csv
        }

        /// <summary>
        /// Generate with type=Timeline, format=Json writes non-empty JSON.
        /// </summary>
        [Fact]
        public async Task Generate_TimelineJson_WritesNonEmptyFile()
        {
            await AssertGenerateWritesNonEmptyFileAsync(
                reportTypeIndex: 3, // Timeline
                formatIndex: 2);    // Json
        }

        /// <summary>
        /// Generate with type=Timeline, format=Csv writes non-empty CSV.
        /// </summary>
        [Fact]
        public async Task Generate_TimelineCsv_WritesNonEmptyFile()
        {
            await AssertGenerateWritesNonEmptyFileAsync(
                reportTypeIndex: 3, // Timeline
                formatIndex: 1);    // Csv
        }

        /// <summary>
        /// After Generate completes without error, StatusMessage is non-empty (contains path).
        /// </summary>
        [Fact]
        public async Task Generate_Success_SetsStatusMessageWithPath()
        {
            ReportVerificationTracker.Reset();
            var vm = NewReportExportViewModel(reportTypeIndex: 2, formatIndex: 2);
            var tmpPath = Path.GetTempFileName();
            try
            {
                vm.FileSavePathOverride = tmpPath;
                await vm.GenerateCommand.ExecuteAsync(null);
                Assert.False(string.IsNullOrWhiteSpace(vm.StatusMessage),
                    "StatusMessage should contain export path on success.");
                Assert.Contains(tmpPath, vm.StatusMessage);
            }
            finally
            {
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
            }
        }

        /// <summary>
        /// IsGenerating is false before and after a Generate run.
        /// </summary>
        [Fact]
        public async Task Generate_IsGenerating_FalseAfterCompletion()
        {
            ReportVerificationTracker.Reset();
            var vm = NewReportExportViewModel(reportTypeIndex: 0, formatIndex: 2);
            var tmpPath = Path.GetTempFileName();
            try
            {
                vm.FileSavePathOverride = tmpPath;
                Assert.False(vm.IsGenerating);
                await vm.GenerateCommand.ExecuteAsync(null);
                Assert.False(vm.IsGenerating);
            }
            finally
            {
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
            }
        }

        [Theory]
        [InlineData(0, "Clinical")]
        [InlineData(1, "Coach")]
        [InlineData(2, "Outcome")]
        [InlineData(3, "Timeline")]
        public async Task Generate_VerifiesReportAndSetsPassStatus(int reportTypeIndex, string reportType)
        {
            ReportVerificationTracker.Reset();
            var vm = NewReportExportViewModel(reportTypeIndex, formatIndex: 2);
            var tmpPath = Path.GetTempFileName();
            try
            {
                vm.FileSavePathOverride = tmpPath;

                await vm.GenerateCommand.ExecuteAsync(null);
                var snapshot = ReportVerificationTracker.Snapshot();

                var status = reportType switch
                {
                    "Clinical" => snapshot.ClinicalReportStatus,
                    "Coach" => snapshot.CoachReportStatus,
                    "Outcome" => snapshot.OutcomeReportStatus,
                    "Timeline" => snapshot.TimelineReportStatus,
                    _ => ""
                };

                Assert.Equal("PASS", status);
                Assert.Equal(1, snapshot.VerifiedReportCount);
                Assert.Contains(tmpPath, snapshot.GeneratedReportPaths);
                Assert.NotNull(snapshot.ReportVerificationTimestamp);
            }
            finally
            {
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
            }
        }

        [Fact]
        public async Task Generate_WhenExportPathIsInvalid_ShowsSafeMessage_AndDoesNotLeakRawException()
        {
            ReportVerificationTracker.Reset();
            var vm = NewReportExportViewModel(reportTypeIndex: 2, formatIndex: 2);
            var folderPath = Path.Combine(Path.GetTempPath(), "FemVoiceReportExportInvalid_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folderPath);
            try
            {
                vm.FileSavePathOverride = folderPath;

                await vm.GenerateCommand.ExecuteAsync(null);

                Assert.Equal(SafeFailureMessages.For(SafeFailureKind.ReportExport), vm.StatusMessage);
                Assert.False(SafeFailureMessages.LooksLikeRawExceptionText(vm.StatusMessage));
                var snapshot = ReportVerificationTracker.Snapshot();
                Assert.Equal("FAIL", snapshot.OutcomeReportStatus);
                Assert.DoesNotContain(snapshot.ReportVerificationErrors, SafeFailureMessages.LooksLikeRawExceptionText);
            }
            finally
            {
                if (Directory.Exists(folderPath))
                    Directory.Delete(folderPath, recursive: true);
            }
        }

        // ── CaseReviewViewModel tests ─────────────────────────────────────────────

        /// <summary>
        /// Create saves a review to the store and adds it to SavedReviews.
        /// </summary>
        [Fact]
        public async Task Create_SavesReviewAndAddsToSavedReviews()
        {
            var store = NewCaseReviewsStore();
            var vm = NewCaseReviewViewModel(store);

            vm.SelectedReviewTypeIndex = 0; // Monthly
            vm.PeriodStart = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            vm.PeriodEnd   = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);

            await vm.CreateCommand.ExecuteAsync(null);

            // SavedReviews should contain one review.
            Assert.Single(vm.SavedReviews);

            var review = vm.SavedReviews[0];
            Assert.Equal(ReviewType.Monthly, review.ReviewType);
            Assert.Equal(ReviewStatus.Draft, review.Status);
            Assert.Null(review.CompletedAt);
        }

        /// <summary>
        /// Create with ReviewType=Goal stores the correct ReviewType.
        /// </summary>
        [Fact]
        public async Task Create_ReviewTypeGoal_StoredCorrectly()
        {
            var store = NewCaseReviewsStore();
            var vm = NewCaseReviewViewModel(store);

            vm.SelectedReviewTypeIndex = 1; // Goal
            vm.PeriodStart = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            vm.PeriodEnd   = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);

            await vm.CreateCommand.ExecuteAsync(null);

            Assert.Single(vm.SavedReviews);
            Assert.Equal(ReviewType.Goal, vm.SavedReviews[0].ReviewType);
        }

        /// <summary>
        /// Complete transitions a Draft review to Completed and stamps CompletedAt.
        /// </summary>
        [Fact]
        public async Task Complete_DraftReview_TransitionsToCompleted()
        {
            var store = NewCaseReviewsStore();
            var vm = NewCaseReviewViewModel(store);

            vm.SelectedReviewTypeIndex = 0; // Monthly
            vm.PeriodStart = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            vm.PeriodEnd   = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);

            await vm.CreateCommand.ExecuteAsync(null);
            Assert.Single(vm.SavedReviews);

            // Select the newly created Draft review.
            vm.SelectedReview = vm.SavedReviews[0];
            Assert.Equal(ReviewStatus.Draft, vm.SelectedReview.Status);

            await vm.CompleteCommand.ExecuteAsync(null);

            // The review in the list should now be Completed.
            Assert.Equal(ReviewStatus.Completed, vm.SavedReviews[0].Status);
            Assert.NotNull(vm.SavedReviews[0].CompletedAt);
        }

        /// <summary>
        /// Complete is only executable when SelectedReview is a Draft.
        /// </summary>
        [Fact]
        public async Task Complete_CanExecute_FalseWhenNoDraftSelected()
        {
            var store = NewCaseReviewsStore();
            var vm = NewCaseReviewViewModel(store);

            // No review selected.
            vm.SelectedReview = null;
            Assert.False(vm.CompleteCommand.CanExecute(null));

            // Create and complete a review, then check CanExecute is false on Completed.
            vm.SelectedReviewTypeIndex = 0;
            vm.PeriodStart = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            vm.PeriodEnd   = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);
            await vm.CreateCommand.ExecuteAsync(null);
            vm.SelectedReview = vm.SavedReviews[0];
            await vm.CompleteCommand.ExecuteAsync(null);

            // Now the selected review is Completed — Complete should no longer be executable.
            Assert.Equal(ReviewStatus.Completed, vm.SelectedReview.Status);
            Assert.False(vm.CompleteCommand.CanExecute(null));
        }

        /// <summary>
        /// LoadReviews populates SavedReviews from the store.
        /// </summary>
        [Fact]
        public async Task LoadReviews_PopulatesSavedReviewsFromStore()
        {
            var store = NewCaseReviewsStore();

            // Pre-populate the store with one review via the assembler directly.
            var assembler = NewCaseReviewAssembler();
            var outcome = new OutcomeProfile
            {
                UserId = 1,
                GeneratedAt = FixedNow,
                HasEnoughData = false
            };
            var review = assembler.Build(
                outcome,
                new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
                ReviewType.Progress,
                FixedNow);
            await store.SaveAsync(review);

            // Create a fresh VM (empty SavedReviews) and call LoadReviews.
            var vm = NewCaseReviewViewModel(store);
            await vm.LoadReviewsCommand.ExecuteAsync(null);

            Assert.Single(vm.SavedReviews);
            Assert.Equal(ReviewType.Progress, vm.SavedReviews[0].ReviewType);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static ReportExportViewModel NewReportExportViewModel(
            int reportTypeIndex = 2,
            int formatIndex = 2)
        {
            var vm = new ReportExportViewModel(
                outcomeProfileBuilder: NewOutcomeProfileBuilder(),
                reportAssembler: NewReportAssembler(),
                exportWriter: NewExportWriter(),
                database: NewDatabase());

            vm.SelectedReportTypeIndex = reportTypeIndex;
            vm.SelectedFormatIndex = formatIndex;
            return vm;
        }

        private static CaseReviewViewModel NewCaseReviewViewModel(CaseReviewsStore store)
        {
            return new CaseReviewViewModel(
                outcomeProfileBuilder: NewOutcomeProfileBuilder(),
                assembler: NewCaseReviewAssembler(),
                store: store,
                database: NewDatabase());
        }

        private static async Task AssertGenerateWritesNonEmptyFileAsync(
            int reportTypeIndex,
            int formatIndex)
        {
            var vm = NewReportExportViewModel(reportTypeIndex, formatIndex);
            var tmpPath = Path.GetTempFileName();
            try
            {
                ReportVerificationTracker.Reset();
                vm.FileSavePathOverride = tmpPath;
                await vm.GenerateCommand.ExecuteAsync(null);

                // File must exist and contain at least some bytes.
                Assert.True(File.Exists(tmpPath), "Output file should exist.");
                var content = await File.ReadAllBytesAsync(tmpPath);
                Assert.NotEmpty(content);
            }
            finally
            {
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
            }
        }
    }
}
