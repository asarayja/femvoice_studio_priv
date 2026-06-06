using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ExerciseProfileFactory"/>.
    /// Verifies type-safe profile creation, key field values, Validate() compatibility,
    /// deterministic mapping, and out-of-range rejection.
    /// </summary>
    public class ExerciseProfileFactoryTests
    {
        private readonly IExerciseProfileFactory _sut = new ExerciseProfileFactory();

        // ── No null returned for valid values ────────────────────────────────────

        [Theory]
        [InlineData(ExerciseProfileType.ResonanceHumming)]
        [InlineData(ExerciseProfileType.ResonanceVowels)]
        [InlineData(ExerciseProfileType.CoordinatedGlideUp)]
        [InlineData(ExerciseProfileType.StabilityTraining)]
        public void CreateProfile_ValidType_ReturnsNonNull(ExerciseProfileType type)
        {
            var profile = _sut.CreateProfile(type);
            Assert.NotNull(profile);
        }

        // ── Validate() passes for all four profiles ───────────────────────────

        [Theory]
        [InlineData(ExerciseProfileType.ResonanceHumming)]
        [InlineData(ExerciseProfileType.ResonanceVowels)]
        [InlineData(ExerciseProfileType.CoordinatedGlideUp)]
        [InlineData(ExerciseProfileType.StabilityTraining)]
        public void CreateProfile_ValidType_PassesValidate(ExerciseProfileType type)
        {
            var profile = _sut.CreateProfile(type);
            var ex = Record.Exception(() => profile.Validate());
            Assert.Null(ex);
        }

        // ── Deterministic: same input always returns equivalent profile ────────

        [Theory]
        [InlineData(ExerciseProfileType.ResonanceHumming)]
        [InlineData(ExerciseProfileType.ResonanceVowels)]
        [InlineData(ExerciseProfileType.CoordinatedGlideUp)]
        [InlineData(ExerciseProfileType.StabilityTraining)]
        public void CreateProfile_CalledTwice_ReturnsSameValues(ExerciseProfileType type)
        {
            var a = _sut.CreateProfile(type);
            var b = _sut.CreateProfile(type);

            Assert.Equal(a.RequiredHoldSeconds, b.RequiredHoldSeconds);
            Assert.Equal(a.TargetResonanceMin,  b.TargetResonanceMin);
            Assert.Equal(a.TargetResonanceMax,  b.TargetResonanceMax);
            Assert.Equal(a.StabilityThreshold,  b.StabilityThreshold);
            Assert.Equal(a.UsesPitch,           b.UsesPitch);
            Assert.Equal(a.UsesResonance,       b.UsesResonance);
            Assert.Equal(a.UsesStability,       b.UsesStability);
        }

        // ── Unknown enum value throws ArgumentOutOfRangeException ─────────────

        [Fact]
        public void CreateProfile_UnknownEnumValue_ThrowsArgumentOutOfRange()
        {
            var unknown = (ExerciseProfileType)99;
            Assert.Throws<ArgumentOutOfRangeException>(() => _sut.CreateProfile(unknown));
        }

        // ── ResonanceHumming key fields ────────────────────────────────────────

        [Fact]
        public void CreateProfile_ResonanceHumming_HasExpectedFields()
        {
            var p = _sut.CreateProfile(ExerciseProfileType.ResonanceHumming);

            Assert.True(p.UsesResonance,   "UsesResonance must be true");
            Assert.True(p.UsesStability,   "UsesStability must be true");
            Assert.False(p.UsesPitch,      "UsesPitch must be false (safety-only)");
            Assert.False(p.UsesIntensity,  "UsesIntensity must be false");
            Assert.Equal(3.0, p.RequiredHoldSeconds);
            Assert.Equal(0.50, p.TargetResonanceMin);
            Assert.Equal(0.85, p.TargetResonanceMax);
            Assert.Equal(0.45, p.StabilityThreshold);
            Assert.Null(p.MinPitch);
            Assert.Null(p.MaxPitch);
        }

        // ── ResonanceVowels key fields ─────────────────────────────────────────

        [Fact]
        public void CreateProfile_ResonanceVowels_HasExpectedFields()
        {
            var p = _sut.CreateProfile(ExerciseProfileType.ResonanceVowels);

            Assert.True(p.UsesResonance);
            Assert.True(p.UsesStability);
            Assert.False(p.UsesPitch);
            Assert.Equal(4.0, p.RequiredHoldSeconds);
            Assert.Equal(0.58, p.TargetResonanceMin);
            Assert.Equal(0.92, p.TargetResonanceMax);
            Assert.Equal(0.55, p.StabilityThreshold);
            Assert.Null(p.MinPitch);
            Assert.Null(p.MaxPitch);
        }

        // ── CoordinatedGlideUp key fields ──────────────────────────────────────

        [Fact]
        public void CreateProfile_CoordinatedGlideUp_HasExpectedFields()
        {
            var p = _sut.CreateProfile(ExerciseProfileType.CoordinatedGlideUp);

            Assert.True(p.UsesPitch,      "UsesPitch must be true (primary metric)");
            Assert.True(p.UsesResonance,  "UsesResonance must be true (secondary)");
            Assert.True(p.UsesStability);
            Assert.Equal(0.0, p.RequiredHoldSeconds);   // continuous movement — no hold
            Assert.Equal(0.35, p.TargetResonanceMin);
            Assert.Equal(0.90, p.TargetResonanceMax);
            Assert.Equal(0.40, p.StabilityThreshold);
            Assert.Null(p.MinPitch);   // set by ComfortZoneController at runtime
            Assert.Null(p.MaxPitch);
        }

        // ── StabilityTraining key fields ───────────────────────────────────────

        [Fact]
        public void CreateProfile_StabilityTraining_HasExpectedFields()
        {
            var p = _sut.CreateProfile(ExerciseProfileType.StabilityTraining);

            Assert.True(p.UsesStability,  "UsesStability must be true (primary)");
            Assert.True(p.UsesResonance,  "UsesResonance must be true (secondary)");
            Assert.False(p.UsesPitch,     "UsesPitch must be false (safety-only)");
            Assert.Equal(6.0, p.RequiredHoldSeconds);   // longest hold
            Assert.Equal(0.45, p.TargetResonanceMin);
            Assert.Equal(0.88, p.TargetResonanceMax);
            Assert.Equal(0.70, p.StabilityThreshold);   // highest threshold
            Assert.Null(p.MinPitch);
            Assert.Null(p.MaxPitch);
        }

        // ── Clinical hold hierarchy: humming < vowels < stability; glide = 0 ──

        [Fact]
        public void HoldHierarchy_IsCorrect()
        {
            var humming   = _sut.CreateProfile(ExerciseProfileType.ResonanceHumming).RequiredHoldSeconds;
            var vowels    = _sut.CreateProfile(ExerciseProfileType.ResonanceVowels).RequiredHoldSeconds;
            var glide     = _sut.CreateProfile(ExerciseProfileType.CoordinatedGlideUp).RequiredHoldSeconds;
            var stability = _sut.CreateProfile(ExerciseProfileType.StabilityTraining).RequiredHoldSeconds;

            Assert.Equal(0.0, glide);
            Assert.True(humming < vowels,    $"humming ({humming}s) must be < vowels ({vowels}s)");
            Assert.True(vowels  < stability, $"vowels ({vowels}s) must be < stability ({stability}s)");
        }

        // ── StabilityTraining has highest stability threshold of all ──────────

        [Fact]
        public void StabilityTraining_HasHighestStabilityThreshold()
        {
            var humming   = _sut.CreateProfile(ExerciseProfileType.ResonanceHumming).StabilityThreshold;
            var vowels    = _sut.CreateProfile(ExerciseProfileType.ResonanceVowels).StabilityThreshold;
            var glide     = _sut.CreateProfile(ExerciseProfileType.CoordinatedGlideUp).StabilityThreshold;
            var stability = _sut.CreateProfile(ExerciseProfileType.StabilityTraining).StabilityThreshold;

            Assert.True(stability > humming,  $"stability ({stability}) must be > humming ({humming})");
            Assert.True(stability > vowels,   $"stability ({stability}) must be > vowels ({vowels})");
            Assert.True(stability > glide,    $"stability ({stability}) must be > glide ({glide})");
        }

        // ── ResonanceVowels stricter resonance target than ResonanceHumming ───

        [Fact]
        public void ResonanceVowels_HasStricterResonanceMin_ThanHumming()
        {
            var humming = _sut.CreateProfile(ExerciseProfileType.ResonanceHumming).TargetResonanceMin;
            var vowels  = _sut.CreateProfile(ExerciseProfileType.ResonanceVowels).TargetResonanceMin;

            Assert.True(vowels > humming,
                $"Vowels resonanceMin ({vowels}) must be > humming ({humming})");
        }

        // ── No hardcoded Hz values: pitch-primary profiles have null boundaries ─

        [Fact]
        public void CoordinatedGlideUp_PitchBoundaries_AreNullAtCreation()
        {
            var p = _sut.CreateProfile(ExerciseProfileType.CoordinatedGlideUp);
            Assert.Null(p.MinPitch);
            Assert.Null(p.MaxPitch);
        }

        // ── All thresholds within [0, 1] ──────────────────────────────────────

        [Theory]
        [InlineData(ExerciseProfileType.ResonanceHumming)]
        [InlineData(ExerciseProfileType.ResonanceVowels)]
        [InlineData(ExerciseProfileType.CoordinatedGlideUp)]
        [InlineData(ExerciseProfileType.StabilityTraining)]
        public void CreateProfile_AllNormalisedThresholds_AreInRange(ExerciseProfileType type)
        {
            var p = _sut.CreateProfile(type);
            Assert.InRange(p.TargetResonanceMin, 0.0, 1.0);
            Assert.InRange(p.TargetResonanceMax, 0.0, 1.0);
            Assert.InRange(p.StabilityThreshold, 0.0, 1.0);
            Assert.True(p.TargetResonanceMax >= p.TargetResonanceMin,
                "TargetResonanceMax must be >= TargetResonanceMin");
        }

        // ── RequiredHoldSeconds is non-negative for all profiles ──────────────

        [Theory]
        [InlineData(ExerciseProfileType.ResonanceHumming)]
        [InlineData(ExerciseProfileType.ResonanceVowels)]
        [InlineData(ExerciseProfileType.CoordinatedGlideUp)]
        [InlineData(ExerciseProfileType.StabilityTraining)]
        public void CreateProfile_RequiredHoldSeconds_IsNonNegative(ExerciseProfileType type)
        {
            var p = _sut.CreateProfile(type);
            Assert.True(p.RequiredHoldSeconds >= 0,
                $"RequiredHoldSeconds must be >= 0, was {p.RequiredHoldSeconds}");
        }
    }
}
