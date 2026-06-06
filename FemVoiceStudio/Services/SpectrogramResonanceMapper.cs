using System;
using FemVoiceStudio.Audio;

namespace FemVoiceStudio.Services
{
    public enum SpectrogramResonanceTone
    {
        NoSignal,
        Back,
        Balanced,
        Forward,
        Pressed
    }

    public sealed record SpectrogramFormantMarker(string Name, double FrequencyHz, double Y);

    public sealed record SpectrogramResonanceVisualState
    {
        public SpectrogramResonanceTone Tone { get; init; }
        public double ResonanceScore { get; init; }
        public double Brightness { get; init; }
        public double Confidence { get; init; }
        public double ForwardZoneTopY { get; init; }
        public double ForwardZoneBottomY { get; init; }
        public SpectrogramFormantMarker[] Formants { get; init; } = Array.Empty<SpectrogramFormantMarker>();
    }

    public sealed class SpectrogramResonanceMapper
    {
        private const double ForwardZoneMinHz = 1800;
        private const double ForwardZoneMaxHz = 3200;
        private const double PressedCentroidHz = 3400;
        private double? _smoothedBrightness;

        public SpectrogramResonanceVisualState Map(
            FormantSnapshot snapshot,
            double resonanceScore,
            double minFrequencyHz,
            double maxFrequencyHz,
            double height)
        {
            var safeMin = Math.Max(1, minFrequencyHz);
            var safeMax = Math.Max(safeMin + 1, maxFrequencyHz);
            var safeHeight = Math.Max(1, height);
            var score = Math.Clamp(resonanceScore, 0, 1);
            var brightness = Smooth(NormalizeBrightness(snapshot.SpectralCentroid));

            var tone = Classify(snapshot, score, brightness);
            var formants = snapshot.IsValid
                ? new[]
                {
                    Marker("F1", snapshot.F1, safeMin, safeMax, safeHeight),
                    Marker("F2", snapshot.F2, safeMin, safeMax, safeHeight),
                    Marker("F3", snapshot.F3, safeMin, safeMax, safeHeight)
                }
                : Array.Empty<SpectrogramFormantMarker>();

            return new SpectrogramResonanceVisualState
            {
                Tone = tone,
                ResonanceScore = score,
                Brightness = brightness,
                Confidence = Math.Clamp(snapshot.Confidence, 0, 1),
                ForwardZoneTopY = FrequencyToY(ForwardZoneMaxHz, safeMin, safeMax, safeHeight),
                ForwardZoneBottomY = FrequencyToY(ForwardZoneMinHz, safeMin, safeMax, safeHeight),
                Formants = formants
            };
        }

        public void Reset()
        {
            _smoothedBrightness = null;
        }

        public static double FrequencyToY(double frequencyHz, double minFrequencyHz, double maxFrequencyHz, double height)
        {
            var clamped = Math.Clamp(frequencyHz, minFrequencyHz, maxFrequencyHz);
            return (maxFrequencyHz - clamped) * height / (maxFrequencyHz - minFrequencyHz);
        }

        private static SpectrogramFormantMarker Marker(
            string name,
            double frequencyHz,
            double minFrequencyHz,
            double maxFrequencyHz,
            double height) =>
            new(name, frequencyHz, FrequencyToY(frequencyHz, minFrequencyHz, maxFrequencyHz, height));

        private static double NormalizeBrightness(double spectralCentroid)
        {
            if (spectralCentroid <= 0)
                return 0;

            return Math.Clamp((spectralCentroid - 1200) / 2200, 0, 1);
        }

        private double Smooth(double brightness)
        {
            _smoothedBrightness = _smoothedBrightness.HasValue
                ? _smoothedBrightness.Value * 0.70 + brightness * 0.30
                : brightness;

            return _smoothedBrightness.Value;
        }

        private static SpectrogramResonanceTone Classify(
            FormantSnapshot snapshot,
            double resonanceScore,
            double brightness)
        {
            if (!snapshot.IsValid)
                return SpectrogramResonanceTone.NoSignal;

            if (snapshot.SpectralCentroid >= PressedCentroidHz && brightness > 0.82)
                return SpectrogramResonanceTone.Pressed;

            if (snapshot.F2 >= ForwardZoneMinHz && snapshot.F2 <= ForwardZoneMaxHz && resonanceScore >= 0.55)
                return SpectrogramResonanceTone.Forward;

            if (snapshot.F2 < ForwardZoneMinHz || resonanceScore < 0.35)
                return SpectrogramResonanceTone.Back;

            return SpectrogramResonanceTone.Balanced;
        }
    }
}
