using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class PitchTargetZonePolicyTests
    {
        [Fact]
        public void ForDifficulty_AdvancedCapsAtComfortableMaximum()
        {
            var zone = PitchTargetZonePolicy.ForDifficulty(DifficultyLevel.Avansert);

            Assert.Equal(175, zone.Min);
            Assert.Equal(240, zone.Max);
        }

        [Fact]
        public void ForDifficulty_TargetZonesDoNotGetWiderAtAdvancedLevel()
        {
            var beginner = PitchTargetZonePolicy.ForDifficulty(DifficultyLevel.Nybegynner);
            var intermediate = PitchTargetZonePolicy.ForDifficulty(DifficultyLevel.Middels);
            var advanced = PitchTargetZonePolicy.ForDifficulty(DifficultyLevel.Avansert);

            Assert.True(intermediate.Max - intermediate.Min <= beginner.Max - beginner.Min);
            Assert.True(advanced.Max - advanced.Min <= intermediate.Max - intermediate.Min + 5);
        }

        [Fact]
        public void ClampForDifficulty_ExerciseCannotPushTargetAbove240Hz()
        {
            var zone = PitchTargetZonePolicy.ClampForDifficulty(
                DifficultyLevel.Avansert,
                requestedMin: 170,
                requestedMax: 310);

            Assert.True(zone.Max <= 240);
            Assert.True(zone.Max - zone.Min <= 70);
        }
    }
}
