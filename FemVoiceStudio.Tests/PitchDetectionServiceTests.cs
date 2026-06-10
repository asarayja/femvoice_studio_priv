using System;
using Xunit;
using FemVoiceStudio.Audio;

namespace FemVoiceStudio.Tests
{
    public class PitchDetectionServiceTests
    {
        [Fact]
        public void AnalyzeIntonation_NullArrays_DoesNotThrow_ReturnsFailureReason()
        {
            var res = PitchDetectionService.AnalyzeIntonation(null, null);
            Assert.NotNull(res);
            Assert.Equal("INSUFFICIENT_PITCH_DATA", res.FailureReason);
        }

        [Fact]
        public void AnalyzeIntonation_EmptyArrays_ReturnsFailureReason()
        {
            var res = PitchDetectionService.AnalyzeIntonation(Array.Empty<double>(), Array.Empty<double>());
            Assert.NotNull(res);
            Assert.Equal("INSUFFICIENT_PITCH_DATA", res.FailureReason);
        }

        [Fact]
        public void AnalyzeIntonation_OnePitch_ReturnsInsufficient()
        {
            var res = PitchDetectionService.AnalyzeIntonation(new double[] { 220.0 }, new double[] { 0.01 });
            Assert.NotNull(res);
            Assert.Equal("INSUFFICIENT_PITCH_DATA", res.FailureReason);
        }

        [Fact]
        public void AnalyzeIntonation_MismatchedLengths_TruncatesAndReturnsReason()
        {
            var pitches = new double[] { 200, 210, 220, 230, 240, 250 };
            var intensities = new double[] { 0.01, 0.02 }; // shorter
            var res = PitchDetectionService.AnalyzeIntonation(pitches, intensities);
            Assert.NotNull(res);
            Assert.Equal("MISMATCHED_SIGNAL_ARRAY_LENGTHS", res.FailureReason);
        }

        [Fact]
        public void AnalyzeIntonation_SmallValidArray_ReturnsAnalysisOrNeutral()
        {
            var pitches = new double[] { 200, 205, 210, 215, 220, 225, 230, 235, 240, 245 };
            var intensities = new double[pitches.Length];
            var res = PitchDetectionService.AnalyzeIntonation(pitches, intensities);
            Assert.NotNull(res);
            // valid result but FailureReason may be null
        }
    }
}
