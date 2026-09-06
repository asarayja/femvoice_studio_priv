using System;
using System.Linq;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    using SmartCoachBaseline = FemVoiceStudio.Data.SmartCoachBaseline;

    /// <summary>
    /// Regression guard: SmartCoach must never mint goals from an EMPTY baseline. A user opening the coach before any
    /// real sessions has an all-zero baseline; the engine then persisted permanent goals with targets derived from
    /// those zeros (pitch 0+20 = 20 Hz, resonance 0+15, intonation 0+20). Because goals are persisted and only
    /// regenerated when none are active, those absurd targets survived — and once real sessions existed, progress
    /// (current / target) clamped to a fabricated 100 % "goal reached" forever. That is invented data shown as real.
    /// </summary>
    public class SmartCoachZeroBaselineGoalTests
    {
        private static TestDatabaseService WithBaseline(double pitch, double resonance, double intonation, string confidence)
        {
            var db = new TestDatabaseService();
            db.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = pitch,
                BaselineResonanceScore = resonance,
                BaselineIntonation = intonation,
                ConfidenceLevel = confidence,
            });
            return db;
        }

        [Fact]
        public void ZeroBaseline_MintsNoGoals_AndPersistsNothing()
        {
            var db = WithBaseline(0, 0, 0, "low");
            var engine = new SmartCoachEngine(db, LocalizationService.Instance);

            var goals = engine.GenerateGoals(1);

            Assert.Empty(goals);
            Assert.Empty(db.GetSmartCoachGoals(1, true));
        }

        [Fact]
        public void ZeroBaseline_NeverPersistsTheAbsurdTargets()
        {
            var db = WithBaseline(0, 0, 0, "low");
            var engine = new SmartCoachEngine(db, LocalizationService.Instance);

            engine.GenerateGoals(1);
            var stored = db.GetSmartCoachGoals(1, true);

            // The exact poison values an all-zero baseline used to produce.
            Assert.DoesNotContain(stored, g => g.GoalType == "pitch" && Math.Abs(g.TargetValue - 20) < 0.001);
            Assert.DoesNotContain(stored, g => g.GoalType == "resonance" && Math.Abs(g.TargetValue - 15) < 0.001);
            Assert.DoesNotContain(stored, g => g.GoalType == "intonation" && Math.Abs(g.TargetValue - 20) < 0.001);
        }

        [Fact]
        public void PartialBaseline_OnlyMintsGoalsForDimensionsThatHaveRealData()
        {
            // Pitch measured, resonance/intonation not yet → only the pitch goal may be created.
            var db = WithBaseline(pitch: 160, resonance: 0, intonation: 0, confidence: "medium");
            var engine = new SmartCoachEngine(db, LocalizationService.Instance);

            var goals = engine.GenerateGoals(1);

            Assert.Contains(goals, g => g.GoalType == "pitch");
            Assert.DoesNotContain(goals, g => g.GoalType == "resonance");
            Assert.DoesNotContain(goals, g => g.GoalType == "intonation");
        }

        [Fact]
        public void RealBaseline_StillGeneratesRealGoals()
        {
            // The data-gate must not disable the feature.
            var db = WithBaseline(pitch: 160, resonance: 45, intonation: 35, confidence: "high");
            var engine = new SmartCoachEngine(db, LocalizationService.Instance);

            var goals = engine.GenerateGoals(1);

            Assert.NotEmpty(goals);
            Assert.All(goals, g => Assert.True(g.TargetValue > 0, $"{g.GoalType} target must be positive"));
            var pitch = goals.SingleOrDefault(g => g.GoalType == "pitch");
            Assert.NotNull(pitch);
            Assert.Equal(Math.Min(160.0 + 20.0, 220.0), pitch!.TargetValue);
        }
    }
}
