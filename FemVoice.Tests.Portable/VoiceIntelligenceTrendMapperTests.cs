using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Sprint D-A — COMFORT-01 (medium safety): the user-facing VoiceHealthScore proxy
    /// is the WORST-OF (min) of the Health pair (Comfort, Recovery), NOT their mean.
    ///
    /// Clinical rationale (global priority Safety &gt; Health &gt; Recovery &gt; Comfort):
    /// a rising Comfort must never be able to lift the headline health number while
    /// Recovery is falling. With a mean, +Comfort could cancel −Recovery and mask a
    /// deteriorating recovery state; with min, the headline tracks the worse of the two
    /// and can only move with the limiting dimension. This is a measurement-only number
    /// (it gates nothing) — the change only ever tightens what the user is shown.
    /// </summary>
    public class VoiceIntelligenceTrendMapperTests
    {
        private static VoiceIntelligenceTrendPoint Point(
            DateTime startedAt,
            double comfort,
            double recovery)
            => new VoiceIntelligenceTrendPoint
            {
                SessionId = 1,
                UserId = 1,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(10),
                ResonanceScore100 = 60,
                ComfortScore100 = comfort,
                ConsistencyScore100 = 55,
                IntonationScore100 = 50,
                VocalWeightScore100 = 45,
                RecoveryScore100 = recovery,
                PitchScore100 = 40,
                CompositeVoiceScore = 58
            };

        [Fact]
        public void VoiceHealthScore_IsMinOfComfortAndRecovery_NotMean()
        {
            // Comfort 72, Recovery 81 -> min = 72 (the old mean would have been 76.5).
            var snapshot = VoiceIntelligenceTrendMapper.ToSnapshot(
                Point(new DateTime(2026, 6, 1, 9, 0, 0), comfort: 72, recovery: 81));

            Assert.Equal(72, snapshot.VoiceHealthScore);
        }

        [Fact]
        public void VoiceHealthScore_TracksTheWorseDimension_WhenRecoveryIsLow()
        {
            // Recovery is the limiting (worse) dimension -> headline must equal Recovery,
            // never be pulled up by the much-higher Comfort.
            var snapshot = VoiceIntelligenceTrendMapper.ToSnapshot(
                Point(new DateTime(2026, 6, 1, 9, 0, 0), comfort: 95, recovery: 30));

            Assert.Equal(30, snapshot.VoiceHealthScore);
        }

        /// <summary>
        /// COMFORT-01 core invariant: when Recovery FALLS and Comfort RISES between two
        /// sessions, the user-facing VoiceHealthScore must NOT rise. Under the old mean
        /// it could (rising comfort masking falling recovery); under min it cannot,
        /// because the headline is bounded above by the falling Recovery.
        /// </summary>
        [Fact]
        public void VoiceHealthScore_DoesNotRise_WhenRecoveryFallsAndComfortRises()
        {
            var t1 = new DateTime(2026, 6, 1, 9, 0, 0);
            var t2 = new DateTime(2026, 6, 2, 9, 0, 0);

            // Session 1: comfort 50, recovery 70 -> min = 50.
            var earlier = VoiceIntelligenceTrendMapper.ToSnapshot(
                Point(t1, comfort: 50, recovery: 70));

            // Session 2: comfort rises to 80, recovery falls to 45 -> min = 45.
            // (Old mean would have RISEN: 60 -> 62.5, masking the recovery drop.)
            var later = VoiceIntelligenceTrendMapper.ToSnapshot(
                Point(t2, comfort: 80, recovery: 45));

            Assert.True(
                later.VoiceHealthScore <= earlier.VoiceHealthScore,
                $"VoiceHealthScore rose ({earlier.VoiceHealthScore} -> {later.VoiceHealthScore}) " +
                "despite falling recovery; rising comfort must never mask falling recovery.");

            // And concretely it must equal the (lower) limiting recovery, not the mean.
            Assert.Equal(45, later.VoiceHealthScore);
        }

        [Fact]
        public void VoiceHealthScore_StaysWithin0To100_AfterClamping()
        {
            // Out-of-range inputs are clamped before min is taken.
            var snapshot = VoiceIntelligenceTrendMapper.ToSnapshot(
                Point(new DateTime(2026, 6, 1, 9, 0, 0), comfort: 250, recovery: -40));

            // recovery clamps to 0, comfort clamps to 100 -> min = 0.
            Assert.Equal(0, snapshot.VoiceHealthScore);
            Assert.InRange(snapshot.VoiceHealthScore, 0.0, 100.0);
        }
    }
}
