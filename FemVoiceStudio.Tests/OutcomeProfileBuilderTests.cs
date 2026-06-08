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
    /// W0-A4 (Outcome Tracking) — verifies the PURE <see cref="OutcomeProfileBuilder.Build"/>
    /// over hand-built inputs. No mocks, no IO: real engine instances (constructed over a
    /// throwaway store) supply the ranking/flagging behaviour, every other input is a
    /// hand-built record. All dates are fixed constants.
    /// </summary>
    public class OutcomeProfileBuilderTests
    {
        private static readonly DateTime At = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // The builder needs an ExerciseEffectivenessEngine for ranking/flagging — those
        // methods are pure over the passed profiles (no store read), so a real engine over a
        // throwaway in-memory analytics store is sufficient and never touches a DB.
        private static OutcomeProfileBuilder NewBuilder()
        {
            var analyticsStore = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var effEngine = new ExerciseEffectivenessEngine(analyticsStore);
            var insightEngine = new LongitudinalInsightEngine();
            // SmartCoachEngine is only used by AssembleFromStoreAsync, never by Build — a
            // minimal instance is fine here.
            var smartCoach = SmartCoachEngineFactory();
            return new OutcomeProfileBuilder(smartCoach, effEngine, insightEngine);
        }

        // ── 1. Goal progress: target/current → delta, percent, achieved ───────────
        [Fact]
        public void Build_GoalProgress_ComputesDeltaPercentAndAchieved()
        {
            var builder = NewBuilder();
            var goals = new List<SmartCoachGoal>
            {
                // 75 of 100 ⇒ delta 25, 75 %, not achieved.
                new() { Id = 1, GoalType = "resonance", TargetValue = 100, CurrentValue = 75 },
                // 50 of 50 ⇒ delta 0, 100 %, achieved.
                new() { Id = 2, GoalType = "pitch",     TargetValue = 50,  CurrentValue = 50 },
            };
            var goalProfile = new VoiceGoalProfile { PrimaryFocus = "resonance" };

            var profile = builder.Build(
                userId: 7, generatedAt: At,
                goals: goals, goalProfile: goalProfile,
                recoveryForecast: null,
                effectivenessProfiles: null,
                developmentProfile: null,
                insights: null);

            Assert.Equal(2, profile.GoalProgress.Goals.Count);

            var g1 = profile.GoalProgress.Goals[0];
            Assert.Equal("resonance", g1.GoalType);
            // PrimaryFocus comes from the goal profile focus ("resonance").
            Assert.Equal(VoiceDimension.Resonance, g1.PrimaryFocus);
            Assert.Equal(25.0, g1.DeltaToGoal, 6);
            Assert.Equal(75.0, g1.PercentComplete, 6);
            Assert.False(g1.IsAchieved);

            var g2 = profile.GoalProgress.Goals[1];
            Assert.Equal(0.0, g2.DeltaToGoal, 6);
            Assert.Equal(100.0, g2.PercentComplete, 6);
            Assert.True(g2.IsAchieved);
        }

        // ── 2. Goal progress: zero/negative target ⇒ 0 % (no divide-by-zero) ──────
        [Fact]
        public void Build_GoalProgress_ZeroTarget_IsZeroPercentAndNotAchieved()
        {
            var builder = NewBuilder();
            var goals = new List<SmartCoachGoal>
            {
                new() { Id = 1, GoalType = "comfort", TargetValue = 0, CurrentValue = 10 },
            };

            var profile = builder.Build(
                userId: 1, generatedAt: At, goals: goals, goalProfile: null,
                recoveryForecast: null, effectivenessProfiles: null,
                developmentProfile: null, insights: null);

            var g = Assert.Single(profile.GoalProgress.Goals);
            Assert.Equal(0.0, g.PercentComplete, 6);
            Assert.False(g.IsAchieved);
            // No goal profile ⇒ PrimaryFocus is mapped from the goal type ("comfort").
            Assert.Equal(VoiceDimension.Comfort, g.PrimaryFocus);
        }

        // ── 3. Goal progress: current over target clamps to 100 % and is achieved ─
        [Fact]
        public void Build_GoalProgress_CurrentOverTarget_ClampsTo100AndAchieved()
        {
            var builder = NewBuilder();
            var goals = new List<SmartCoachGoal>
            {
                new() { Id = 1, GoalType = "intonation", TargetValue = 40, CurrentValue = 60 },
            };

            var profile = builder.Build(
                userId: 1, generatedAt: At, goals: goals, goalProfile: null,
                recoveryForecast: null, effectivenessProfiles: null,
                developmentProfile: null, insights: null);

            var g = Assert.Single(profile.GoalProgress.Goals);
            Assert.Equal(-20.0, g.DeltaToGoal, 6); // target − current
            Assert.Equal(100.0, g.PercentComplete, 6);
            Assert.True(g.IsAchieved);
        }

        // ── 4. Recovery progress copies forecast fields verbatim ──────────────────
        [Fact]
        public void Build_RecoveryProgress_CopiesForecastFields()
        {
            var builder = NewBuilder();
            var forecast = new RecoveryForecast
            {
                Current = new RecoveryResult
                {
                    Score = 42.0, Status = RecoveryStatus.Strained, Explanation = "x"
                },
                OvertrainingPredicted = true,
                RecoveryDebt = 70.0,
                AcuteChronicWorkloadRatio = 1.6,
                Recommendation = "Rest soon.",
                Severity = RecoverySeverity.Urgent
            };

            var profile = builder.Build(
                userId: 1, generatedAt: At, goals: null, goalProfile: null,
                recoveryForecast: forecast, effectivenessProfiles: null,
                developmentProfile: null, insights: null);

            var r = profile.RecoveryProgress;
            Assert.Equal(42.0, r.CurrentScore0to100, 6);
            Assert.Equal("Strained", r.Status);
            Assert.True(r.OvertrainingPredicted);
            Assert.Equal(70.0, r.RecoveryDebt, 6);
            Assert.Equal(1.6, r.AcuteChronicWorkloadRatio, 6);
            Assert.Equal("Urgent", r.Severity);
            Assert.Equal("Rest soon.", r.RecommendationText);
        }

        // ── 5. Exercise effectiveness: ranked drops low-data, orders by composite ──
        [Fact]
        public void Build_ExerciseEffectiveness_RanksAndFlags()
        {
            var builder = NewBuilder();
            var profiles = new List<ExerciseEffectivenessProfile>
            {
                // Evidenced, lower composite.
                new() { ExerciseId = 1, SessionCount = 6, HasEnoughData = true,
                        CompositeEffectiveness = 55, UserSuccessRate = 60, RecoveryCost = 20 },
                // Evidenced, higher composite — should rank first.
                new() { ExerciseId = 2, SessionCount = 6, HasEnoughData = true,
                        CompositeEffectiveness = 80, UserSuccessRate = 70, RecoveryCost = 10 },
                // Under-evidenced — dropped by RankMostEffective default.
                new() { ExerciseId = 3, SessionCount = 1, HasEnoughData = false,
                        CompositeEffectiveness = 90, UserSuccessRate = 90, RecoveryCost = 5 },
                // Data-bearing but taxing — should raise a concern flag.
                new() { ExerciseId = 4, SessionCount = 8, HasEnoughData = true,
                        CompositeEffectiveness = 30, UserSuccessRate = 20, RecoveryCost = 75 },
            };

            var profile = builder.Build(
                userId: 1, generatedAt: At, goals: null, goalProfile: null,
                recoveryForecast: null, effectivenessProfiles: profiles,
                developmentProfile: null, insights: null);

            var ranked = profile.ExerciseEffectiveness.Ranked;
            // The under-evidenced exercise 3 is dropped; 2 (highest composite) ranks first.
            Assert.DoesNotContain(ranked, p => p.ExerciseId == 3);
            Assert.Equal(2, ranked[0].ExerciseId);

            // Exercise 4 is taxing ⇒ at least one concern names it.
            Assert.Contains(profile.ExerciseEffectiveness.Concerns, c => c.ExerciseId == 4);
        }

        // ── 6. Long-term development copies windows/patterns/insights ─────────────
        [Fact]
        public void Build_LongTermDevelopment_CopiesProfileAndInsights()
        {
            var builder = NewBuilder();
            var weekly = new[] { Window(7, sessions: 5, composite: 62) };
            var monthly = new[] { Window(90, sessions: 12, composite: 60) };
            var plateau = new PlateauState { ReasonCode = "PLATEAU_Resonance", Dimension = VoiceDimension.Resonance, SeverityScore = 40 };
            var dev = new VoiceDevelopmentProfile
            {
                UserId = 1, GeneratedAt = At,
                WeeklyTrend = weekly, MonthlyTrend = monthly,
                CompositeVoiceScore = 61.0, Plateau = plateau, HasEnoughData = true
            };
            var insights = new[]
            {
                new LongitudinalInsight { ReasonCode = "IMPROVEMENT", Dimension = VoiceDimension.Resonance, Confidence = 70 }
            };

            var profile = builder.Build(
                userId: 1, generatedAt: At, goals: null, goalProfile: null,
                recoveryForecast: null, effectivenessProfiles: null,
                developmentProfile: dev, insights: insights);

            var lt = profile.LongTermDevelopment;
            Assert.Single(lt.WeeklyTrend);
            Assert.Single(lt.MonthlyTrend);
            Assert.Equal(61.0, lt.CompositeVoiceScore, 6);
            Assert.NotNull(lt.Plateau);
            Assert.Equal("PLATEAU_Resonance", lt.Plateau!.ReasonCode);
            Assert.Single(lt.Insights);
            Assert.Equal("IMPROVEMENT", lt.Insights[0].ReasonCode);
        }

        // ── 7. HasEnoughData: a development row with composite==0 is NOT evidence ──
        [Fact]
        public void Build_HasEnoughData_PreMigrationZeroComposite_IsNotEvidence()
        {
            var builder = NewBuilder();
            // Development says HasEnoughData but composite is the pre-migration sentinel 0.
            var dev = new VoiceDevelopmentProfile
            {
                UserId = 1, GeneratedAt = At, CompositeVoiceScore = 0.0, HasEnoughData = true
            };

            var profile = builder.Build(
                userId: 1, generatedAt: At, goals: null, goalProfile: null,
                recoveryForecast: null, effectivenessProfiles: null,
                developmentProfile: dev, insights: null);

            // No goals, no recovery, no exercises, development composite==0 ⇒ no evidence.
            Assert.False(profile.HasEnoughData);
        }

        // ── 8. HasEnoughData: a real development composite > 0 counts as evidence ──
        [Fact]
        public void Build_HasEnoughData_RealComposite_IsEvidence()
        {
            var builder = NewBuilder();
            var dev = new VoiceDevelopmentProfile
            {
                UserId = 1, GeneratedAt = At, CompositeVoiceScore = 58.0, HasEnoughData = true
            };

            var profile = builder.Build(
                userId: 1, generatedAt: At, goals: null, goalProfile: null,
                recoveryForecast: null, effectivenessProfiles: null,
                developmentProfile: dev, insights: null);

            Assert.True(profile.HasEnoughData);
        }

        // ── 9. HasEnoughData: a forecast alone counts as recovery evidence ────────
        [Fact]
        public void Build_HasEnoughData_RecoveryForecast_IsEvidence()
        {
            var builder = NewBuilder();
            var forecast = new RecoveryForecast
            {
                Current = new RecoveryResult { Score = 80, Status = RecoveryStatus.WellRecovered, Explanation = "ok" },
                Recommendation = "Train as planned.",
                Severity = RecoverySeverity.None
            };

            var profile = builder.Build(
                userId: 1, generatedAt: At, goals: null, goalProfile: null,
                recoveryForecast: forecast, effectivenessProfiles: null,
                developmentProfile: null, insights: null);

            Assert.True(profile.HasEnoughData);
        }

        // ── 10. Empty everything ⇒ well-formed profile, HasEnoughData false ───────
        [Fact]
        public void Build_AllEmpty_ProducesSafeFalseProfile()
        {
            var builder = NewBuilder();

            var profile = builder.Build(
                userId: 9, generatedAt: At, goals: null, goalProfile: null,
                recoveryForecast: null, effectivenessProfiles: null,
                developmentProfile: null, insights: null);

            Assert.Equal(9, profile.UserId);
            Assert.Equal(At, profile.GeneratedAt);
            Assert.Empty(profile.GoalProgress.Goals);
            Assert.Empty(profile.ExerciseEffectiveness.Ranked);
            Assert.Empty(profile.ExerciseEffectiveness.Concerns);
            Assert.Empty(profile.LongTermDevelopment.WeeklyTrend);
            Assert.Empty(profile.LongTermDevelopment.Insights);
            Assert.False(profile.HasEnoughData);
        }

        // ── 11. Goal with a target > 0 alone counts as goal evidence ──────────────
        [Fact]
        public void Build_HasEnoughData_GoalWithTarget_IsEvidence()
        {
            var builder = NewBuilder();
            var goals = new List<SmartCoachGoal>
            {
                new() { Id = 1, GoalType = "resonance", TargetValue = 100, CurrentValue = 0 },
            };

            var profile = builder.Build(
                userId: 1, generatedAt: At, goals: goals, goalProfile: null,
                recoveryForecast: null, effectivenessProfiles: null,
                developmentProfile: null, insights: null);

            Assert.True(profile.HasEnoughData);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static TrendWindow Window(int days, int sessions, double composite) => new()
        {
            WindowDays = days,
            From = At.AddDays(-days),
            To = At,
            CompositeMean = composite,
            SessionCount = sessions,
            HasEnoughData = sessions >= 3
        };

        // SmartCoachEngine has many ctor dependencies; only AssembleFromStoreAsync ever uses
        // it and these unit tests exercise Build only. A factory keeps the construction in
        // one place so a ctor change is a single edit.
        private static SmartCoachEngine SmartCoachEngineFactory()
            => new SmartCoachEngine(new TestDatabaseService());
    }
}
