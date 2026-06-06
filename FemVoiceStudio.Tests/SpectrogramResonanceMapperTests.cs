using FemVoiceStudio.Audio;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class SpectrogramResonanceMapperTests
    {
        [Fact]
        public void Map_PlacesHigherFormantsNearTopOfSpectrogram()
        {
            var mapper = new SpectrogramResonanceMapper();

            var state = mapper.Map(Snapshot(f1: 350, f2: 2200, f3: 3000), 0.75, 80, 4000, 400);

            Assert.Equal(3, state.Formants.Length);
            Assert.True(state.Formants[2].Y < state.Formants[1].Y);
            Assert.True(state.Formants[1].Y < state.Formants[0].Y);
        }

        [Fact]
        public void Map_ClassifiesForwardResonanceWhenF2IsInForwardZone()
        {
            var mapper = new SpectrogramResonanceMapper();

            var state = mapper.Map(Snapshot(f1: 330, f2: 2300, f3: 3000), 0.72, 80, 4000, 400);

            Assert.Equal(SpectrogramResonanceTone.Forward, state.Tone);
            Assert.True(state.ForwardZoneTopY < state.ForwardZoneBottomY);
        }

        [Fact]
        public void Map_ClassifiesBackResonanceWhenF2IsLow()
        {
            var mapper = new SpectrogramResonanceMapper();

            var state = mapper.Map(Snapshot(f1: 420, f2: 1450, f3: 2500), 0.42, 80, 4000, 400);

            Assert.Equal(SpectrogramResonanceTone.Back, state.Tone);
        }

        [Fact]
        public void Map_ClassifiesPressedWhenCentroidIsTooBright()
        {
            var mapper = new SpectrogramResonanceMapper();

            var state = mapper.Map(Snapshot(f1: 320, f2: 2500, f3: 3300, centroid: 3800), 0.80, 80, 4000, 400);

            Assert.Equal(SpectrogramResonanceTone.Pressed, state.Tone);
        }

        [Fact]
        public void Map_SmoothsBrightnessAcrossFrames()
        {
            var mapper = new SpectrogramResonanceMapper();

            var first = mapper.Map(Snapshot(centroid: 1400), 0.50, 80, 4000, 400);
            var second = mapper.Map(Snapshot(centroid: 3400), 0.50, 80, 4000, 400);

            Assert.True(second.Brightness > first.Brightness);
            Assert.True(second.Brightness < 1.0);
        }

        private static FormantSnapshot Snapshot(
            double f1 = 350,
            double f2 = 2200,
            double f3 = 3000,
            double centroid = 2500) =>
            new()
            {
                F1 = f1,
                F2 = f2,
                F3 = f3,
                SpectralCentroid = centroid,
                Stability = 0.8,
                RmsValue = 0.05,
                Confidence = 0.8
            };
    }
}
