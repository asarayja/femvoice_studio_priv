using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="ReportAssembler"/> (W0-A3A10).
    ///
    /// Approach: hand-craft a minimal but representative <see cref="OutcomeProfile"/> with
    /// supporting collections, then verify each Build* method returns a populated DTO with
    /// the correct period, title, and key metrics — without touching any I/O.
    /// </summary>
    public class ReportAssemblerTests
    {
        // ── Shared fixtures ───────────────────────────────────────────────────────

        private static readonly DateTime T0 = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime T1 = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);
        private static readonly DateTime Now = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

        private static OutcomeProfile MakeOutcomeProfile() =>
            new OutcomeProfile
            {
                UserId = 42,
                GeneratedAt = Now,
                HasEnoughData = true,
                GoalProgress = new GoalProgress
                {
                    Goals = new List<GoalProgressEntry>
                    {
                        new GoalProgressEntry
                        {
                            GoalType = "pitch",
                            PrimaryFocus = VoiceDimension.Pitch,
                            TargetValue = 220.0,
                            CurrentValue = 195.0,
                            DeltaToGoal = 25.0,
                            PercentComplete = 75.0,
                            IsAchieved = false
                        },
                        new GoalProgressEntry
                        {
                            GoalType = "resonance",
                            PrimaryFocus = VoiceDimension.Resonance,
                            TargetValue = 80.0,
                            CurrentValue = 82.0,
                            DeltaToGoal = -2.0,
                            PercentComplete = 100.0,
                            IsAchieved = true
                        }
                    }
                },
                RecoveryProgress = new RecoveryProgress
                {
                    CurrentScore0to100 = 78.5,
                    Status = "WellRecovered",
                    OvertrainingPredicted = false,
                    RecoveryDebt = 12.3,
                    AcuteChronicWorkloadRatio = 1.1,
                    Severity = "None",
                    RecommendationText = "Recovery looks good — maintain current training load."
                },
                ExerciseEffectiveness = new ExerciseEffectivenessSummary
                {
                    Ranked = new List<ExerciseEffectivenessProfile>
                    {
                        new ExerciseEffectivenessProfile
                        {
                            ExerciseId = 3,
                            ResonanceGain = 2.4,
                            ComfortGain = 1.1,
                            ConsistencyGain = 0.9,
                            RecoveryCost = 20.0,
                            UserSuccessRate = 88.0,
                            SessionCount = 12,
                            HasEnoughData = true,
                            CompositeEffectiveness = 72.0,
                            Explanation = "Resonance improving steadily over 12 sessions."
                        }
                    },
                    Concerns = new List<ExerciseEffectivenessFlag>
                    {
                        new ExerciseEffectivenessFlag
                        {
                            ExerciseId = 7,
                            ReasonCode = "HIGH_RECOVERY_COST",
                            Explanation = "Exercise 7 has high recovery cost — consider de-prioritising.",
                            Magnitude = 72.5
                        }
                    }
                },
                LongTermDevelopment = new LongTermDevelopment
                {
                    CompositeVoiceScore = 68.4,
                    WeeklyTrend = new List<TrendWindow>
                    {
                        new TrendWindow
                        {
                            WindowDays = 7,
                            From = T0,
                            To = T0.AddDays(7),
                            CompositeSlope = 1.2,
                            CompositeMean = 65.0,
                            CompositeMin = 60.0,
                            CompositeMax = 71.0,
                            SessionCount = 5,
                            Confidence = 70.0,
                            HasEnoughData = true,
                            DimensionSlopes = new Dictionary<VoiceDimension, double>
                            {
                                { VoiceDimension.Resonance, 1.5 },
                                { VoiceDimension.Pitch, 0.9 }
                            }
                        }
                    },
                    MonthlyTrend = new List<TrendWindow>
                    {
                        new TrendWindow
                        {
                            WindowDays = 30,
                            From = T0,
                            To = T1,
                            CompositeSlope = 0.7,
                            CompositeMean = 64.2,
                            CompositeMin = 55.0,
                            CompositeMax = 72.0,
                            SessionCount = 18,
                            Confidence = 82.0,
                            HasEnoughData = true,
                            DimensionSlopes = new Dictionary<VoiceDimension, double>
                            {
                                { VoiceDimension.Resonance, 0.8 },
                                { VoiceDimension.Pitch, 0.6 }
                            }
                        }
                    },
                    Breakthrough = new BreakthroughState
                    {
                        ReasonCode = "BREAKTHROUGH_Resonance",
                        Dimension = VoiceDimension.Resonance,
                        SeverityScore = 60.0,
                        WindowDays = 7,
                        DetectedAt = T0.AddDays(21),
                        MagnitudeDelta = 2.5
                    },
                    Plateau = null,
                    Regression = null,
                    Insights = new List<LongitudinalInsight>
                    {
                        new LongitudinalInsight
                        {
                            ReasonCode = "IMPROVEMENT",
                            Dimension = VoiceDimension.Resonance,
                            Confidence = 80.0,
                            What = "Resonance improving steadily",
                            Why = "Consistent practice and good recovery.",
                            Evidence = new[] { "window=7d", "slope=+1.5", "sessions=5" }
                        }
                    }
                }
            };

        private static IReadOnlyList<ClinicalNote> MakeNotes() =>
            new List<ClinicalNote>
            {
                new ClinicalNote
                {
                    NoteId = Guid.NewGuid(),
                    UserId = 42,
                    NoteType = ClinicalNoteType.Coach,
                    AuthorRole = "Coach",
                    CreatedAt = T0.AddDays(5),
                    BodyText = "Good progress on resonance exercises.",
                    LinkedEntityType = "Session"
                },
                new ClinicalNote
                {
                    NoteId = Guid.NewGuid(),
                    UserId = 42,
                    NoteType = ClinicalNoteType.Clinical,
                    AuthorRole = "Clinician",
                    CreatedAt = T0.AddDays(15),
                    BodyText = "Voice health appears stable.",
                    LinkedEntityType = null
                },
                // This note is outside the period and must be filtered out
                new ClinicalNote
                {
                    NoteId = Guid.NewGuid(),
                    UserId = 42,
                    NoteType = ClinicalNoteType.Review,
                    AuthorRole = "System",
                    CreatedAt = T1.AddDays(5), // June 5 — outside [T0, T1]
                    BodyText = "Monthly review note outside period."
                }
            };

        private static IReadOnlyList<AuditEvent> MakeAuditEvents() =>
            new List<AuditEvent>
            {
                new AuditEvent
                {
                    AuditId = Guid.NewGuid(),
                    UserId = 42,
                    OccurredAt = T0.AddDays(10),
                    EntityType = AuditEntityType.GoalChange,
                    EntityId = "goal-pitch",
                    ActorRole = "Coach",
                    ReasonCode = "GOAL_UPDATED"
                },
                // Outside period — must be filtered out
                new AuditEvent
                {
                    AuditId = Guid.NewGuid(),
                    UserId = 42,
                    OccurredAt = T1.AddDays(3),
                    EntityType = AuditEntityType.RecoveryEvent,
                    EntityId = "rec-1",
                    ActorRole = "System",
                    ReasonCode = "ACWR_ALERT"
                }
            };

        private readonly ReportAssembler _assembler = new();

        // ── BuildClinicalReport ───────────────────────────────────────────────────

        [Fact]
        public void BuildClinicalReport_ReturnsPopulatedReport_WithCorrectPeriodAndTitle()
        {
            var outcome = MakeOutcomeProfile();
            var notes = MakeNotes();
            var audit = MakeAuditEvents();

            var report = _assembler.BuildClinicalReport(outcome, notes, audit, T0, T1, Now);

            Assert.NotNull(report);
            Assert.False(string.IsNullOrWhiteSpace(report.Title));
            Assert.Contains("May 2026", report.Title);
            Assert.Equal(T0, report.Period.PeriodStart);
            Assert.Equal(T1, report.Period.PeriodEnd);
            Assert.Equal(Now, report.Period.GeneratedAt);
        }

        [Fact]
        public void BuildClinicalReport_FiltersNotes_ToWithinPeriod()
        {
            var report = _assembler.BuildClinicalReport(
                MakeOutcomeProfile(), MakeNotes(), MakeAuditEvents(), T0, T1, Now);

            // Only 2 notes fall within [T0, T1]; the third is in June
            Assert.Equal(2, report.Notes.Count);
            Assert.All(report.Notes, n =>
            {
                Assert.True(n.CreatedAt >= T0);
                Assert.True(n.CreatedAt <= T1);
            });
        }

        [Fact]
        public void BuildClinicalReport_FiltersAuditEvents_ToWithinPeriod()
        {
            var report = _assembler.BuildClinicalReport(
                MakeOutcomeProfile(), MakeNotes(), MakeAuditEvents(), T0, T1, Now);

            // Only 1 audit event is within [T0, T1]
            Assert.Single(report.AuditEvents);
            Assert.Equal("GOAL_UPDATED", report.AuditEvents[0].ReasonCode);
        }

        [Fact]
        public void BuildClinicalReport_NotesAreOrderedChronologically()
        {
            var report = _assembler.BuildClinicalReport(
                MakeOutcomeProfile(), MakeNotes(), MakeAuditEvents(), T0, T1, Now);

            for (var i = 1; i < report.Notes.Count; i++)
                Assert.True(report.Notes[i].CreatedAt >= report.Notes[i - 1].CreatedAt);
        }

        [Fact]
        public void BuildClinicalReport_CarriesCorrectOutcomeMetrics()
        {
            var outcome = MakeOutcomeProfile();
            var report = _assembler.BuildClinicalReport(outcome, MakeNotes(), MakeAuditEvents(), T0, T1, Now);

            Assert.Equal(42, report.Outcome.UserId);
            Assert.True(report.Outcome.HasEnoughData);
            Assert.Equal(78.5, report.Outcome.RecoveryProgress.CurrentScore0to100);
        }

        // ── BuildCoachReport ──────────────────────────────────────────────────────

        [Fact]
        public void BuildCoachReport_ReturnsPopulatedReport_WithPeriodAndTitle()
        {
            var report = _assembler.BuildCoachReport(MakeOutcomeProfile(), T0, T1, Now);

            Assert.NotNull(report);
            Assert.False(string.IsNullOrWhiteSpace(report.Title));
            Assert.Equal(T0, report.Period.PeriodStart);
            Assert.Equal(T1, report.Period.PeriodEnd);
        }

        [Fact]
        public void BuildCoachReport_SurfacesBreakthroughFromLongTermDevelopment()
        {
            var report = _assembler.BuildCoachReport(MakeOutcomeProfile(), T0, T1, Now);

            Assert.NotNull(report.Breakthrough);
            Assert.Equal("BREAKTHROUGH_Resonance", report.Breakthrough!.ReasonCode);
        }

        [Fact]
        public void BuildCoachReport_HasNoPlateau_WhenNoneInOutcome()
        {
            var report = _assembler.BuildCoachReport(MakeOutcomeProfile(), T0, T1, Now);
            Assert.Null(report.Plateau);
        }

        [Fact]
        public void BuildCoachReport_FocusAreasIsNonEmpty()
        {
            var report = _assembler.BuildCoachReport(MakeOutcomeProfile(), T0, T1, Now);

            Assert.NotEmpty(report.FocusAreas);
        }

        [Fact]
        public void BuildCoachReport_FocusAreasContainsBreakthroughAndGoalEntries()
        {
            var report = _assembler.BuildCoachReport(MakeOutcomeProfile(), T0, T1, Now);

            Assert.Contains(report.FocusAreas, fa => fa.Contains("breakthrough", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(report.FocusAreas, fa => fa.Contains("Pitch", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void BuildCoachReport_RecommendationsContainsRecoveryText()
        {
            var report = _assembler.BuildCoachReport(MakeOutcomeProfile(), T0, T1, Now);

            Assert.NotEmpty(report.Recommendations);
            Assert.Contains(report.Recommendations, r =>
                r.Contains("Recovery", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("training load", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void BuildCoachReport_InsightsSurfacedFromLongTermDevelopment()
        {
            var report = _assembler.BuildCoachReport(MakeOutcomeProfile(), T0, T1, Now);

            Assert.NotEmpty(report.Insights);
            Assert.Equal("IMPROVEMENT", report.Insights[0].ReasonCode);
        }

        // ── BuildOutcomeReport ────────────────────────────────────────────────────

        [Fact]
        public void BuildOutcomeReport_ReturnsPopulatedReport_WithPeriodAndTitle()
        {
            var report = _assembler.BuildOutcomeReport(MakeOutcomeProfile(), T0, T1, Now);

            Assert.NotNull(report);
            Assert.False(string.IsNullOrWhiteSpace(report.Title));
            Assert.Equal(T0, report.Period.PeriodStart);
            Assert.Equal(T1, report.Period.PeriodEnd);
        }

        [Fact]
        public void BuildOutcomeReport_CompositeScoreMatchesOutcomeProfile()
        {
            var outcome = MakeOutcomeProfile();
            var report = _assembler.BuildOutcomeReport(outcome, T0, T1, Now);

            Assert.Equal(outcome.LongTermDevelopment.CompositeVoiceScore, report.CompositeVoiceScore);
        }

        [Fact]
        public void BuildOutcomeReport_RecoveryStatusAndScore_MatchOutcomeProfile()
        {
            var outcome = MakeOutcomeProfile();
            var report = _assembler.BuildOutcomeReport(outcome, T0, T1, Now);

            Assert.Equal("WellRecovered", report.RecoveryStatus);
            Assert.Equal(78.5, report.RecoveryScore);
        }

        [Fact]
        public void BuildOutcomeReport_GoalProgressContainsBothGoals()
        {
            var report = _assembler.BuildOutcomeReport(MakeOutcomeProfile(), T0, T1, Now);

            Assert.Equal(2, report.GoalProgress.Count);
            Assert.Contains(report.GoalProgress, g => g.GoalType == "pitch" && !g.IsAchieved);
            Assert.Contains(report.GoalProgress, g => g.GoalType == "resonance" && g.IsAchieved);
        }

        [Fact]
        public void BuildOutcomeReport_TopExercisesContainsRankedExercise()
        {
            var report = _assembler.BuildOutcomeReport(MakeOutcomeProfile(), T0, T1, Now);

            Assert.Single(report.TopExercises);
            Assert.Equal(3, report.TopExercises[0].ExerciseId);
        }

        [Fact]
        public void BuildOutcomeReport_HasEnoughData_ReflectsOutcomeProfile()
        {
            var report = _assembler.BuildOutcomeReport(MakeOutcomeProfile(), T0, T1, Now);
            Assert.True(report.HasEnoughData);
        }

        // ── BuildTimelineReport ───────────────────────────────────────────────────

        [Fact]
        public void BuildTimelineReport_ReturnsPopulatedReport_WithPeriodAndTitle()
        {
            var report = _assembler.BuildTimelineReport(MakeOutcomeProfile(), T0, T1, Now);

            Assert.NotNull(report);
            Assert.False(string.IsNullOrWhiteSpace(report.Title));
            Assert.Contains("Timeline", report.Title);
            Assert.Equal(T0, report.Period.PeriodStart);
            Assert.Equal(T1, report.Period.PeriodEnd);
        }

        [Fact]
        public void BuildTimelineReport_TimelineEntries_CoversBothWeeklyAndMonthlyWindows()
        {
            var report = _assembler.BuildTimelineReport(MakeOutcomeProfile(), T0, T1, Now);

            // Fixture has 1 weekly (7d) + 1 monthly (30d) → 2 entries
            Assert.Equal(2, report.TimelineEntries.Count);
        }

        [Fact]
        public void BuildTimelineReport_TimelineEntries_AreOrderedChronologically()
        {
            var report = _assembler.BuildTimelineReport(MakeOutcomeProfile(), T0, T1, Now);

            for (var i = 1; i < report.TimelineEntries.Count; i++)
                Assert.True(report.TimelineEntries[i].Window.From >= report.TimelineEntries[i - 1].Window.From);
        }

        [Fact]
        public void BuildTimelineReport_DirectionIsImproving_ForPositiveSlope()
        {
            var report = _assembler.BuildTimelineReport(MakeOutcomeProfile(), T0, T1, Now);

            // Both windows in fixture have CompositeSlope > 0.5 → Improving
            Assert.All(report.TimelineEntries, e => Assert.Equal("Improving", e.Direction));
        }

        [Fact]
        public void BuildTimelineReport_DirectionIsInsufficient_ForWindowWithoutEnoughData()
        {
            var outcome = MakeOutcomeProfile() with
            {
                LongTermDevelopment = new LongTermDevelopment
                {
                    WeeklyTrend = new List<TrendWindow>
                    {
                        new TrendWindow
                        {
                            WindowDays = 7,
                            From = T0,
                            To = T0.AddDays(7),
                            CompositeSlope = 0.0,
                            CompositeMean = 0.0,
                            SessionCount = 1, // below min-3
                            HasEnoughData = false,
                            DimensionSlopes = new Dictionary<VoiceDimension, double>()
                        }
                    }
                }
            };

            var report = _assembler.BuildTimelineReport(outcome, T0, T1, Now);

            Assert.Single(report.TimelineEntries);
            Assert.Equal("Insufficient data", report.TimelineEntries[0].Direction);
        }

        [Fact]
        public void BuildTimelineReport_DirectionIsStable_ForNearZeroSlope()
        {
            var outcome = MakeOutcomeProfile() with
            {
                LongTermDevelopment = new LongTermDevelopment
                {
                    WeeklyTrend = new List<TrendWindow>
                    {
                        new TrendWindow
                        {
                            WindowDays = 7,
                            From = T0,
                            To = T0.AddDays(7),
                            CompositeSlope = 0.2, // ≤ 0.5 → Stable
                            CompositeMean = 65.0,
                            SessionCount = 4,
                            HasEnoughData = true,
                            DimensionSlopes = new Dictionary<VoiceDimension, double>()
                        }
                    }
                }
            };

            var report = _assembler.BuildTimelineReport(outcome, T0, T1, Now);

            Assert.Single(report.TimelineEntries);
            Assert.Equal("Stable", report.TimelineEntries[0].Direction);
        }

        // ── Null guard tests ──────────────────────────────────────────────────────

        [Fact]
        public void BuildClinicalReport_NullOutcome_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _assembler.BuildClinicalReport(null!, Array.Empty<ClinicalNote>(),
                    Array.Empty<AuditEvent>(), T0, T1, Now));
        }

        [Fact]
        public void BuildCoachReport_NullOutcome_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _assembler.BuildCoachReport(null!, T0, T1, Now));
        }

        [Fact]
        public void BuildOutcomeReport_NullOutcome_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _assembler.BuildOutcomeReport(null!, T0, T1, Now));
        }

        [Fact]
        public void BuildTimelineReport_NullOutcome_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _assembler.BuildTimelineReport(null!, T0, T1, Now));
        }
    }
}
