using System;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class PitchTraceStabilizerTests
    {
        [Fact]
        public void Filter_CorrectsFourthHarmonicSpikeForLowVoice()
        {
            var stabilizer = new PitchTraceStabilizer();
            var now = DateTime.UtcNow;

            var lowPitch = stabilizer.Filter(100, now);
            var spike = stabilizer.Filter(500, now.AddMilliseconds(80));

            Assert.Equal(100, lowPitch);
            Assert.InRange(spike, 120, 130);
        }

        [Fact]
        public void Filter_CorrectsSecondHarmonicSpike()
        {
            var stabilizer = new PitchTraceStabilizer();
            var now = DateTime.UtcNow;

            stabilizer.Filter(145, now);
            var filtered = stabilizer.Filter(315, now.AddMilliseconds(60));

            Assert.Equal(157.5, filtered);
        }

        [Fact]
        public void Filter_AllowsGradualPitchMovement()
        {
            var stabilizer = new PitchTraceStabilizer();
            var now = DateTime.UtcNow;

            stabilizer.Filter(150, now);
            var moved = stabilizer.Filter(175, now.AddMilliseconds(300));

            Assert.Equal(175, moved);
        }

        [Fact]
        public void Filter_RejectsExtremePitchWithoutTrack()
        {
            var stabilizer = new PitchTraceStabilizer();

            var filtered = stabilizer.Filter(500, DateTime.UtcNow);

            Assert.Equal(0, filtered);
        }
    }
}
