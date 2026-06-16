using FemVoiceStudio.Services;
using FemVoiceStudio.Models;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace FemVoiceStudio.Tests
{
    [Collection("ReportVerification")]
    public sealed class ReportVerificationTrackerTests
    {
        [Fact]
        public void ExerciseOnlyEvidence_WhenReportsAreNotAttempted_RemainsNotGenerated()
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
        public void DedicatedReportGeneration_AllFourPdfReports_VerifiesAsPass()
        {
            ReportVerificationTracker.Reset();
            var outputDirectory = Path.Combine(
                Path.GetTempPath(),
                "FemVoiceReportVerification_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(outputDirectory);
                var assembler = new ReportAssembler();
                var writer = new ExportWriter();
                var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
                var start = now.AddDays(-30);
                var outcome = MakeOutcomeProfile(now);

                var reports = new (string Type, object Report)[]
                {
                    ("Clinical", assembler.BuildClinicalReport(
                        outcome,
                        Array.Empty<ClinicalNote>(),
                        Array.Empty<AuditEvent>(),
                        start,
                        now,
                        now)),
                    ("Coach", assembler.BuildCoachReport(outcome, start, now, now)),
                    ("Outcome", assembler.BuildOutcomeReport(outcome, start, now, now)),
                    ("Timeline", assembler.BuildTimelineReport(outcome, start, now, now))
                };

                foreach (var (type, report) in reports)
                {
                    var path = Path.Combine(outputDirectory, $"{type}.pdf");
                    using (var stream = File.Create(path))
                        writer.Write(report, ExportFormat.Pdf, stream);

                    ReportVerificationTracker.VerifyAndMarkSucceeded(type, report, ExportFormat.Pdf, path);
                }

                var snapshot = ReportVerificationTracker.Snapshot();

                Assert.Equal("PASS", snapshot.ClinicalReportStatus);
                Assert.Equal("PASS", snapshot.CoachReportStatus);
                Assert.Equal("PASS", snapshot.OutcomeReportStatus);
                Assert.Equal("PASS", snapshot.TimelineReportStatus);
                Assert.Equal(4, snapshot.VerifiedReportCount);
                Assert.Empty(snapshot.ReportVerificationErrors);
                Assert.NotNull(snapshot.ReportVerificationTimestamp);
                Assert.All(snapshot.GeneratedReportPaths, path =>
                {
                    Assert.True(File.Exists(path));
                    Assert.True(new FileInfo(path).Length > 0);
                    using var stream = File.OpenRead(path);
                    Span<byte> header = stackalloc byte[4];
                    Assert.Equal(4, stream.Read(header));
                    Assert.Equal((byte)'%', header[0]);
                    Assert.Equal((byte)'P', header[1]);
                    Assert.Equal((byte)'D', header[2]);
                    Assert.Equal((byte)'F', header[3]);
                });
            }
            finally
            {
                if (Directory.Exists(outputDirectory))
                    Directory.Delete(outputDirectory, recursive: true);
            }
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

        private static OutcomeProfile MakeOutcomeProfile(DateTime now) => new()
        {
            UserId = 1,
            GeneratedAt = now,
            HasEnoughData = true,
            GoalProgress = new GoalProgress
            {
                Goals = new[]
                {
                    new GoalProgressEntry
                    {
                        GoalType = "resonance",
                        PrimaryFocus = VoiceDimension.Resonance,
                        TargetValue = 80,
                        CurrentValue = 72,
                        PercentComplete = 90,
                        IsAchieved = false
                    }
                }
            },
            RecoveryProgress = new RecoveryProgress
            {
                CurrentScore0to100 = 82,
                Status = "WellRecovered",
                RecommendationText = "Maintain a comfortable practice rhythm."
            },
            ExerciseEffectiveness = new ExerciseEffectivenessSummary
            {
                Ranked = new List<ExerciseEffectivenessProfile>
                {
                    new()
                    {
                        ExerciseId = 1,
                        HasEnoughData = true,
                        CompositeEffectiveness = 74,
                        ResonanceGain = 1.5,
                        ComfortGain = 1.1,
                        RecoveryCost = 12,
                        UserSuccessRate = 80,
                        SessionCount = 4
                    }
                }
            },
            LongTermDevelopment = new LongTermDevelopment
            {
                CompositeVoiceScore = 76,
                WeeklyTrend = new[]
                {
                    new TrendWindow
                    {
                        WindowDays = 7,
                        From = now.AddDays(-7),
                        To = now,
                        CompositeSlope = 0.8,
                        CompositeMean = 76,
                        SessionCount = 4,
                        HasEnoughData = true
                    },
                    new TrendWindow
                    {
                        WindowDays = 30,
                        From = now.AddDays(-30),
                        To = now,
                        CompositeSlope = 0.5,
                        CompositeMean = 73,
                        SessionCount = 8,
                        HasEnoughData = true
                    }
                },
                MonthlyTrend = new[]
                {
                    new TrendWindow
                    {
                        WindowDays = 90,
                        From = now.AddDays(-90),
                        To = now,
                        CompositeSlope = 0.3,
                        CompositeMean = 70,
                        SessionCount = 16,
                        HasEnoughData = true
                    },
                    new TrendWindow
                    {
                        WindowDays = 180,
                        From = now.AddDays(-180),
                        To = now,
                        CompositeSlope = 0.2,
                        CompositeMean = 68,
                        SessionCount = 20,
                        HasEnoughData = true
                    }
                }
            }
        };
    }
}
