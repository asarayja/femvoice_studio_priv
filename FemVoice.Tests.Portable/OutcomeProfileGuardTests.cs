using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// SPEC AGENT 13 — Validation, SCENARIO «Outcome Tracking» (Sprint E, W0-A4).
    ///
    /// Proves that an <see cref="OutcomeProfile"/> assembled by the PURE
    /// <see cref="OutcomeProfileBuilder.Build"/> over a HAND-MADE, in-memory history tracks
    /// development correctly across all four layers, AND that the resulting profile is
    /// strictly DESCRIPTIVE — it carries no blocking authority of any kind.
    ///
    /// These tests are deliberately COMPLEMENTARY to <see cref="OutcomeProfileBuilderTests"/>:
    /// where that suite isolates each layer field-by-field, this one drives ONE realistic
    /// multi-window history end-to-end and then asserts the cross-cutting clinical
    /// invariants the spec calls out:
    ///   • GoalProgress.PercentComplete / DeltaToGoal are arithmetically correct;
    ///   • RecoveryProgress mirrors the forecast (status/severity/debt/ACWR);
    ///   • LongTermDevelopment carries Weekly + Monthly trend windows and the detected
    ///     Plateau/Breakthrough pattern states;
    ///   • HasEnoughData is false on a deliberately thin grounding;
    ///   • the OutcomeProfile is purely descriptive (no gate/blocking surface).
    ///
    /// House style: NO mocking frameworks — real engine instances over in-memory fakes; all
    /// dates are fixed constants (no DateTime.Now non-determinism).
    /// </summary>
    public class OutcomeProfileGuardTests
    {
        private static readonly DateTime At = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // The builder needs an ExerciseEffectivenessEngine (RankMostEffective/FlagConcerns are
        // pure over the passed profiles) — a real engine over a throwaway in-memory analytics
        // store never touches a DB. SmartCoachEngine is only used by the store path, never by
        // Build, so a minimal instance suffices.
        private static OutcomeProfileBuilder NewBuilder()
        {
            var analytics = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var effEngine = new ExerciseEffectivenessEngine(analytics);
            var insightEngine = new LongitudinalInsightEngine();
            var smartCoach = new SmartCoachEngine(new TestDatabaseService());
            return new OutcomeProfileBuilder(smartCoach, effEngine, insightEngine);
        }

        private static TrendWindow Window(int days, int sessions, double composite, double slope) => new()
        {
            WindowDays = days,
            From = At.AddDays(-days),
            To = At,
            CompositeMean = composite,
            CompositeSlope = slope,
            SessionCount = sessions,
            HasEnoughData = sessions >= 3
        };

        // ── 1. SCENARIO «Outcome Tracking» — full hand-built history, all four layers ──
        // One realistic snapshot: two explicit goals (one mid-progress, one achieved), a
        // Strained recovery forecast, four evaluated exercises, and a multi-window
        // development profile carrying a plateau + a breakthrough. Every derived field is
        // hand-computed.
        [Fact]
        public void Build_FullHandBuiltHistory_TracksDevelopmentAcrossAllLayers()
        {
            var builder = NewBuilder();

            // GOALS — the user's PrimaryFocus is resonance; one goal at 60/80, one achieved.
            var goals = new List<SmartCoachGoal>
            {
                new() { Id = 1, GoalType = "resonance", TargetValue = 80, CurrentValue = 60 },
                new() { Id = 2, GoalType = "pitch",     TargetValue = 50, CurrentValue = 55 },
            };
            var goalProfile = new VoiceGoalProfile { PrimaryFocus = "resonance" };

            // RECOVERY — a Strained, debt-bearing forecast (Recommend severity).
            var forecast = new RecoveryForecast
            {
                Current = new RecoveryResult
                {
                    Score = 48.0, Status = RecoveryStatus.Strained, Explanation = "moderate load"
                },
                OvertrainingPredicted = false,
                RecoveryDebt = 55.0,
                AcuteChronicWorkloadRatio = 1.4,
                Recommendation = "Consider a lighter session.",
                Severity = RecoverySeverity.Recommend
            };

            // EXERCISE EFFECTIVENESS — two evidenced (one strong), one under-evidenced, one taxing.
            var effectiveness = new List<ExerciseEffectivenessProfile>
            {
                new() { ExerciseId = 1, SessionCount = 7, HasEnoughData = true,
                        CompositeEffectiveness = 50, UserSuccessRate = 55, RecoveryCost = 25 },
                new() { ExerciseId = 2, SessionCount = 7, HasEnoughData = true,
                        CompositeEffectiveness = 82, UserSuccessRate = 75, RecoveryCost = 12 },
                new() { ExerciseId = 3, SessionCount = 1, HasEnoughData = false,
                        CompositeEffectiveness = 95, UserSuccessRate = 95, RecoveryCost = 5 },
                new() { ExerciseId = 4, SessionCount = 9, HasEnoughData = true,
                        CompositeEffectiveness = 28, UserSuccessRate = 18, RecoveryCost = 80 },
            };

            // LONG-TERM DEVELOPMENT — weekly + monthly windows, with a plateau and a breakthrough.
            var weekly = new[] { Window(7, sessions: 5, composite: 64, slope: 2.5) };
            var monthly = new[]
            {
                Window(90, sessions: 14, composite: 60, slope: 0.2),
                Window(180, sessions: 22, composite: 57, slope: 0.1),
            };
            var plateau = new PlateauState
            {
                ReasonCode = "PLATEAU_Resonance", Dimension = VoiceDimension.Resonance,
                SeverityScore = 38, WindowDays = 90, PlateauDurationDays = 21, ObservedSlope = 0.05
            };
            var breakthrough = new BreakthroughState
            {
                ReasonCode = "BREAKTHROUGH_Resonance", Dimension = VoiceDimension.Resonance,
                SeverityScore = 55, WindowDays = 7, MagnitudeDelta = 2.3
            };
            var dev = new VoiceDevelopmentProfile
            {
                UserId = 7, GeneratedAt = At,
                WeeklyTrend = weekly, MonthlyTrend = monthly,
                CompositeVoiceScore = 63.0, Plateau = plateau, Breakthrough = breakthrough,
                HasEnoughData = true
            };
            var insights = new[]
            {
                new LongitudinalInsight
                {
                    ReasonCode = "IMPROVEMENT", Dimension = VoiceDimension.Resonance, Confidence = 72
                }
            };

            var profile = builder.Build(
                userId: 7, generatedAt: At,
                goals: goals, goalProfile: goalProfile,
                recoveryForecast: forecast,
                effectivenessProfiles: effectiveness,
                developmentProfile: dev,
                insights: insights);

            // ── GOAL PROGRESS — delta/percent/achieved hand-computed ──────────────────
            Assert.Equal(7, profile.UserId);
            Assert.Equal(At, profile.GeneratedAt);
            Assert.Equal(2, profile.GoalProgress.Goals.Count);

            var resonanceGoal = profile.GoalProgress.Goals[0];
            // PrimaryFocus is taken from the user's goal-profile focus, not the goal token.
            Assert.Equal(VoiceDimension.Resonance, resonanceGoal.PrimaryFocus);
            Assert.Equal(80.0, resonanceGoal.TargetValue, 9);
            Assert.Equal(60.0, resonanceGoal.CurrentValue, 9);
            Assert.Equal(20.0, resonanceGoal.DeltaToGoal, 9);          // 80 − 60
            Assert.Equal(75.0, resonanceGoal.PercentComplete, 9);     // 60 / 80 × 100
            Assert.False(resonanceGoal.IsAchieved);

            var pitchGoal = profile.GoalProgress.Goals[1];
            Assert.Equal(-5.0, pitchGoal.DeltaToGoal, 9);             // 50 − 55 (over target)
            Assert.Equal(100.0, pitchGoal.PercentComplete, 9);       // clamped at 100
            Assert.True(pitchGoal.IsAchieved);

            // ── RECOVERY PROGRESS — mirrors the forecast verbatim ─────────────────────
            var rp = profile.RecoveryProgress;
            Assert.Equal(48.0, rp.CurrentScore0to100, 9);
            Assert.Equal(RecoveryStatus.Strained.ToString(), rp.Status);
            Assert.Equal(RecoverySeverity.Recommend.ToString(), rp.Severity);
            Assert.Equal(55.0, rp.RecoveryDebt, 9);
            Assert.Equal(1.4, rp.AcuteChronicWorkloadRatio, 9);
            Assert.False(rp.OvertrainingPredicted);
            Assert.Equal("Consider a lighter session.", rp.RecommendationText);

            // ── EXERCISE EFFECTIVENESS — ranked drops under-evidenced; taxing flagged ──
            var ranked = profile.ExerciseEffectiveness.Ranked;
            Assert.DoesNotContain(ranked, p => p.ExerciseId == 3);   // dropped (HasEnoughData=false)
            Assert.Equal(2, ranked[0].ExerciseId);                   // highest composite first
            Assert.Contains(profile.ExerciseEffectiveness.Concerns, c => c.ExerciseId == 4);

            // ── LONG-TERM DEVELOPMENT — Weekly/Monthly trend + Plateau/Breakthrough ───
            var lt = profile.LongTermDevelopment;
            Assert.Single(lt.WeeklyTrend);
            Assert.Equal(2, lt.MonthlyTrend.Count);
            Assert.Equal(7, lt.WeeklyTrend[0].WindowDays);
            Assert.Equal(new[] { 90, 180 }, lt.MonthlyTrend.Select(w => w.WindowDays).ToArray());
            Assert.Equal(63.0, lt.CompositeVoiceScore, 9);

            Assert.NotNull(lt.Plateau);
            Assert.Equal("PLATEAU_Resonance", lt.Plateau!.ReasonCode);
            Assert.NotNull(lt.Breakthrough);
            Assert.Equal("BREAKTHROUGH_Resonance", lt.Breakthrough!.ReasonCode);
            // The detector populated both states; the snapshot carries them unchanged.
            Assert.Equal(VoiceDimension.Resonance, lt.Plateau.Dimension);
            Assert.Equal(VoiceDimension.Resonance, lt.Breakthrough.Dimension);

            Assert.Single(lt.Insights);
            Assert.Equal("IMPROVEMENT", lt.Insights[0].ReasonCode);

            // A real composite > 0 with goals + recovery + evidence ⇒ trustworthy.
            Assert.True(profile.HasEnoughData);
        }

        // ── 2. Thin grounding ⇒ HasEnoughData == false (insufficient evidence) ────────
        // A history with NO goal target, NO recovery forecast, NO evidenced exercise, and a
        // development profile whose composite is the pre-migration sentinel 0 must report
        // HasEnoughData=false — "insufficient evidence", never "no progress".
        [Fact]
        public void Build_ThinHistory_ReportsInsufficientEvidence()
        {
            var builder = NewBuilder();

            // A goal with target 0 carries no meaningful progress fraction ⇒ not evidence.
            var goals = new List<SmartCoachGoal>
            {
                new() { Id = 1, GoalType = "resonance", TargetValue = 0, CurrentValue = 5 },
            };
            // Under-evidenced exercise ⇒ RankMostEffective drops it ⇒ no effectiveness evidence.
            var effectiveness = new List<ExerciseEffectivenessProfile>
            {
                new() { ExerciseId = 1, SessionCount = 1, HasEnoughData = false,
                        CompositeEffectiveness = 90, UserSuccessRate = 90, RecoveryCost = 5 },
            };
            // Development claims HasEnoughData but composite is the pre-migration sentinel 0.
            var dev = new VoiceDevelopmentProfile
            {
                UserId = 1, GeneratedAt = At, CompositeVoiceScore = 0.0, HasEnoughData = true
            };

            var profile = builder.Build(
                userId: 1, generatedAt: At,
                goals: goals, goalProfile: null,
                recoveryForecast: null,
                effectivenessProfiles: effectiveness,
                developmentProfile: dev,
                insights: null);

            Assert.False(profile.HasEnoughData);
            // Yet the snapshot is still well-formed and descriptive — the goal is surfaced,
            // it simply does not count as trustworthy evidence.
            Assert.Single(profile.GoalProgress.Goals);
            Assert.Equal(0.0, profile.GoalProgress.Goals[0].PercentComplete, 9);
            Assert.Empty(profile.ExerciseEffectiveness.Ranked);
        }

        // ── 3. RecoveryProgress mirrors the forecast EXACTLY across severities ────────
        // Proves the recovery block is a faithful copy of the forecast (no derivation, no
        // softening): every severity round-trips and the predictive flags are passed through.
        [Theory]
        [InlineData(82.0, RecoveryStatus.WellRecovered, RecoverySeverity.None, false, 0.0)]
        [InlineData(30.0, RecoveryStatus.Overtrained, RecoverySeverity.Urgent, true, 90.0)]
        public void Build_RecoveryProgress_MirrorsForecast(
            double score, RecoveryStatus status, RecoverySeverity severity,
            bool overtraining, double debt)
        {
            var builder = NewBuilder();
            var forecast = new RecoveryForecast
            {
                Current = new RecoveryResult { Score = score, Status = status, Explanation = "x" },
                OvertrainingPredicted = overtraining,
                RecoveryDebt = debt,
                AcuteChronicWorkloadRatio = 1.25,
                Recommendation = "advice",
                Severity = severity
            };

            var profile = builder.Build(
                userId: 1, generatedAt: At, goals: null, goalProfile: null,
                recoveryForecast: forecast, effectivenessProfiles: null,
                developmentProfile: null, insights: null);

            var rp = profile.RecoveryProgress;
            Assert.Equal(score, rp.CurrentScore0to100, 9);
            Assert.Equal(status.ToString(), rp.Status);
            Assert.Equal(severity.ToString(), rp.Severity);
            Assert.Equal(overtraining, rp.OvertrainingPredicted);
            Assert.Equal(debt, rp.RecoveryDebt, 9);
            Assert.Equal(1.25, rp.AcuteChronicWorkloadRatio, 9);
            // Recovery sits ABOVE Comfort/Voice-Development in the hierarchy but the outcome
            // snapshot only REPORTS it — it never turns the forecast into a block (see test 4).
        }

        // ── 4. OutcomeProfile is purely DESCRIPTIVE — it gates nothing ────────────────
        // The record exposes only read-only data: there is no method/property that blocks,
        // freezes, locks, vetoes, or otherwise authorises/denies training. We prove this
        // structurally (reflection over the public surface) so a future field that smuggles
        // in a gate breaks this test.
        [Fact]
        public void OutcomeProfile_IsDescriptiveOnly_NoGatingSurface()
        {
            var type = typeof(OutcomeProfile);

            // No public method may be a blocking/authorising verb. Record-generated members
            // (Equals/GetHashCode/ToString/Deconstruct/<Clone>/get_/set_/op_) are exempt.
            var gatingVerbs = new[]
            {
                "block", "freeze", "lock", "gate", "veto", "deny", "allow", "permit",
                "approve", "reject", "abort", "halt", "stop", "enforce", "authorize",
                "authorise", "shouldstop", "mustrest", "canprogress", "isblocked"
            };

            var offendingMethods = type.GetMethods()
                .Where(m => m.DeclaringType == type)
                .Select(m => m.Name)
                .Where(n => !n.StartsWith("get_", StringComparison.Ordinal)
                         && !n.StartsWith("set_", StringComparison.Ordinal)
                         && !n.StartsWith("op_", StringComparison.Ordinal)
                         && n != "Equals" && n != "GetHashCode" && n != "ToString"
                         && n != "Deconstruct" && n != "<Clone>$" && n != "PrintMembers")
                .Where(n => gatingVerbs.Any(v => n.Contains(v, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            Assert.True(offendingMethods.Count == 0,
                "OutcomeProfile must be descriptive only — found gating-shaped member(s): "
                + string.Join(", ", offendingMethods));

            // Likewise no property name implies a gate/authority decision.
            var offendingProps = type.GetProperties()
                .Select(p => p.Name)
                .Where(n => gatingVerbs.Any(v => n.Contains(v, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            Assert.True(offendingProps.Count == 0,
                "OutcomeProfile must expose no gating-shaped property: "
                + string.Join(", ", offendingProps));

            // HasEnoughData is an EVIDENCE flag, not a gate: an "insufficient" profile is still
            // a fully usable, renderable snapshot (every sub-block defaults to its empty state).
            var empty = new OutcomeProfile();
            Assert.False(empty.HasEnoughData);
            Assert.Empty(empty.GoalProgress.Goals);
            Assert.Empty(empty.ExerciseEffectiveness.Ranked);
            Assert.Empty(empty.LongTermDevelopment.WeeklyTrend);
            Assert.Empty(empty.LongTermDevelopment.MonthlyTrend);
            Assert.Empty(empty.LongTermDevelopment.Insights);
            Assert.Null(empty.LongTermDevelopment.Plateau);
            Assert.Null(empty.LongTermDevelopment.Breakthrough);
            Assert.Null(empty.LongTermDevelopment.Regression);
        }
    }
}
