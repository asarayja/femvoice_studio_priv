using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class PitchChartAxisRangeCalculatorTests
    {
        [Fact]
        public void Calculate_WithPitchBelowTarget_KeepsLivePitchVisible()
        {
            var range = PitchChartAxisRangeCalculator.Calculate(
                new[] { 95.0, 105.0 },
                targetMin: 165,
                targetMax: 255);

            Assert.True(range.Minimum <= 95);
            Assert.True(range.Maximum >= 255);
        }

        [Fact]
        public void Calculate_WithPitchAboveTarget_KeepsLivePitchVisible()
        {
            var range = PitchChartAxisRangeCalculator.Calculate(
                new[] { 280.0, 305.0 },
                targetMin: 165,
                targetMax: 255);

            Assert.True(range.Minimum <= 165);
            Assert.True(range.Maximum >= 305);
        }

        [Fact]
        public void Calculate_WithoutPitchData_UsesTargetRange()
        {
            var range = PitchChartAxisRangeCalculator.Calculate(
                System.Array.Empty<double>(),
                targetMin: 165,
                targetMax: 255);

            Assert.True(range.Minimum < 165);
            Assert.True(range.Maximum > 255);
        }
    }
}
