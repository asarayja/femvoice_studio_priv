using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoice.Tests.Portable
{
    /// <summary>
    /// The mirror must be readable while training: a voice sitting near a band threshold must not make the headline
    /// label (and its coaching tip) flip tens of times per second. These tests drive the stabilizer with the exact
    /// pathological input — a score oscillating across a threshold — and assert the label settles.
    /// </summary>
    public class VoicePerceptionStabilizerTests
    {
        private static VoicePerception Raw(int score, int pitch = 50, int resonance = 50)
            => new(VoicePerceptionBand.Androgynous, score, pitch, resonance, VoicePerceptionHint.BrightenResonance);

        [Fact]
        public void FirstReading_AppearsImmediately_NotEasedFromZero()
        {
            var s = new VoicePerceptionStabilizer();
            var first = s.Update(Raw(80, pitch: 90, resonance: 70));
            Assert.Equal(80, first.Score);
            Assert.Equal(90, first.PitchScore);
            Assert.Equal(70, first.ResonanceScore);
            Assert.Equal(VoicePerceptionBand.Feminine, first.Band);
        }

        [Fact]
        public void SteadyInput_ConvergesToThatValue()
        {
            var s = new VoicePerceptionStabilizer();
            s.Update(Raw(20));
            VoicePerception last = default;
            for (int i = 0; i < 200; i++) last = s.Update(Raw(70));
            Assert.InRange(last.Score, 69, 70);
        }

        [Fact]
        public void ScoreOscillatingAcrossAThreshold_DoesNotFlipTheLabelRepeatedly()
        {
            // The real failure: raw frames alternating either side of the Feminine threshold (62).
            var s = new VoicePerceptionStabilizer();
            var band = s.Update(Raw(58)).Band;
            int changes = 0;
            for (int i = 0; i < 400; i++)
            {
                var next = s.Update(Raw(i % 2 == 0 ? 58 : 66)).Band;
                if (next != band) { changes++; band = next; }
            }
            Assert.True(changes <= 1, $"Band flipped {changes} times on a voice hovering at the threshold.");
        }

        [Fact]
        public void Hysteresis_KeepsTheBandUntilTheScoreClearlyDrops()
        {
            var s = new VoicePerceptionStabilizer();
            for (int i = 0; i < 100; i++) s.Update(Raw(80));       // settle firmly in Feminine
            Assert.Equal(VoicePerceptionBand.Feminine, s.Update(Raw(80)).Band);

            // A dip that stays within the hysteresis margin must NOT leave Feminine.
            VoicePerception r = default;
            for (int i = 0; i < 100; i++) r = s.Update(Raw(59));   // 59 >= 62-5 → still Feminine
            Assert.Equal(VoicePerceptionBand.Feminine, r.Band);

            // A clear drop does leave it.
            for (int i = 0; i < 100; i++) r = s.Update(Raw(45));
            Assert.NotEqual(VoicePerceptionBand.Feminine, r.Band);
        }

        [Fact]
        public void Hint_FollowsTheStabilizedBand_AndCoachesTheWeakerIngredient()
        {
            var s = new VoicePerceptionStabilizer();
            VoicePerception r = default;
            for (int i = 0; i < 100; i++) r = s.Update(Raw(85, pitch: 95, resonance: 75));
            Assert.Equal(VoicePerceptionBand.Feminine, r.Band);
            Assert.Equal(VoicePerceptionHint.HoldSteady, r.Hint);

            s.Reset();
            for (int i = 0; i < 100; i++) r = s.Update(Raw(45, pitch: 20, resonance: 80));
            Assert.NotEqual(VoicePerceptionBand.Feminine, r.Band);
            Assert.Equal(VoicePerceptionHint.RaisePitch, r.Hint);   // pitch is the weaker ingredient
        }

        [Fact]
        public void RealSignal_RawEstimateFlipsEveryFrame_StabilizedDoesNot()
        {
            // The actual defect, driven through the real estimator: a steady 200 Hz voice whose brightness wobbles a
            // few points either side of the Feminine threshold. Raw, the headline label flips on essentially every
            // audio frame; stabilized it must settle. This is the two-way proof that the stabilizer is doing the work.
            const double pitchHz = 200;
            int[] brightness = { 28, 38 };   // combined score straddles the 62 threshold (≈60 / ≈64)

            var rawBands = new System.Collections.Generic.List<VoicePerceptionBand>();
            for (int i = 0; i < 200; i++)
                rawBands.Add(VoicePerceptionEstimator.Estimate(pitchHz, brightness[i % 2]).Band);
            int rawChanges = 0;
            for (int i = 1; i < rawBands.Count; i++) if (rawBands[i] != rawBands[i - 1]) rawChanges++;
            Assert.True(rawChanges > 100, $"Expected the raw per-frame band to flip constantly; it changed {rawChanges} times.");

            var s = new VoicePerceptionStabilizer();
            var band = s.Update(VoicePerceptionEstimator.Estimate(pitchHz, brightness[0])).Band;
            int stableChanges = 0;
            for (int i = 1; i < 200; i++)
            {
                var next = s.Update(VoicePerceptionEstimator.Estimate(pitchHz, brightness[i % 2])).Band;
                if (next != band) { stableChanges++; band = next; }
            }
            Assert.True(stableChanges <= 1, $"Stabilized band still flipped {stableChanges} times.");
        }

        [Fact]
        public void Reset_ForgetsThePreviousVoice()
        {
            var s = new VoicePerceptionStabilizer();
            for (int i = 0; i < 100; i++) s.Update(Raw(90));
            s.Reset();
            var first = s.Update(Raw(10, pitch: 5, resonance: 15));
            Assert.Equal(10, first.Score);                          // seeded again, not eased down from 90
            Assert.Equal(VoicePerceptionBand.Masculine, first.Band);
        }
    }
}
