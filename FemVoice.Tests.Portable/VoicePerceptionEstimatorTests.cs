using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoice.Tests.Portable
{
    /// <summary>
    /// The perceived-voice mirror must be HONEST and ACTIONABLE: the two ingredients feed the combined reading
    /// monotonically, the bands land where the documented anchors say, and the hint always names the weaker lever
    /// (or "hold" once feminine). These invariants must hold regardless of any later tuning of the anchor constants.
    /// </summary>
    public class VoicePerceptionEstimatorTests
    {
        [Fact]
        public void PitchScore_IsClampedAndMonotonic()
        {
            Assert.Equal(0, VoicePerceptionEstimator.PitchScoreOf(90));    // well below the masculine floor
            Assert.Equal(0, VoicePerceptionEstimator.PitchScoreOf(VoicePerceptionEstimator.PitchFloorHz));
            Assert.Equal(100, VoicePerceptionEstimator.PitchScoreOf(VoicePerceptionEstimator.PitchCeilHz));
            Assert.Equal(100, VoicePerceptionEstimator.PitchScoreOf(300));  // well above the feminine ceil
            Assert.Equal(0, VoicePerceptionEstimator.PitchScoreOf(0));      // no voice → floor

            int prev = -1;
            for (double f = 100; f <= 260; f += 5)
            {
                int s = VoicePerceptionEstimator.PitchScoreOf(f);
                Assert.True(s >= prev, $"pitch score dropped at {f} Hz");
                prev = s;
            }
        }

        [Fact]
        public void LowPitchDarkResonance_ReadsMasculine()
        {
            var p = VoicePerceptionEstimator.Estimate(pitchHz: 120, brightnessPercent: 15);
            Assert.Equal(VoicePerceptionBand.Masculine, p.Band);
            Assert.True(p.Score < VoicePerceptionEstimator.AndrogynousThreshold);
        }

        [Fact]
        public void HighPitchBrightResonance_ReadsFeminine_AndSaysHold()
        {
            var p = VoicePerceptionEstimator.Estimate(pitchHz: 215, brightnessPercent: 85);
            Assert.Equal(VoicePerceptionBand.Feminine, p.Band);
            Assert.True(p.Score >= VoicePerceptionEstimator.FeminineThreshold);
            Assert.Equal(VoicePerceptionHint.HoldSteady, p.Hint);
        }

        [Fact]
        public void MidRange_ReadsAndrogynous()
        {
            var p = VoicePerceptionEstimator.Estimate(pitchHz: 175, brightnessPercent: 50);
            Assert.Equal(VoicePerceptionBand.Androgynous, p.Band);
        }

        [Fact]
        public void CombinedScore_RisesWithEitherCue()
        {
            int baseScore = VoicePerceptionEstimator.Estimate(160, 40).Score;
            Assert.True(VoicePerceptionEstimator.Estimate(190, 40).Score > baseScore, "raising pitch must raise the reading");
            Assert.True(VoicePerceptionEstimator.Estimate(160, 75).Score > baseScore, "brightening resonance must raise the reading");
        }

        [Fact]
        public void Hint_CoachesTheWeakerIngredient()
        {
            // Good pitch, dark resonance → the fix is brightness.
            var lowResonance = VoicePerceptionEstimator.Estimate(pitchHz: 200, brightnessPercent: 10);
            Assert.Equal(VoicePerceptionHint.BrightenResonance, lowResonance.Hint);

            // Bright resonance, low pitch → the fix is pitch.
            var lowPitch = VoicePerceptionEstimator.Estimate(pitchHz: 130, brightnessPercent: 80);
            Assert.Equal(VoicePerceptionHint.RaisePitch, lowPitch.Hint);
        }

        [Fact]
        public void ComponentScores_AreExposedForTransparency()
        {
            var p = VoicePerceptionEstimator.Estimate(pitchHz: 210, brightnessPercent: 30);
            Assert.Equal(100, p.PitchScore);       // 210 Hz == the feminine ceil
            Assert.Equal(30, p.ResonanceScore);    // brightness passed straight through
            Assert.InRange(p.Score, 0, 100);
        }
    }
}
