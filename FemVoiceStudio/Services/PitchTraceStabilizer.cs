using System;

namespace FemVoiceStudio.Services
{
    public sealed class PitchTraceStabilizer
    {
        private const double MinimumPitch = 60;
        private const double MaximumDisplayPitch = 340;
        private const double MaximumInstantJumpHz = 90;
        private const double FastJumpWindowSeconds = 0.25;
        private const double HarmonicMatchTolerance = 0.35;

        private double _lastAcceptedPitch;
        private DateTime _lastAcceptedAt;

        public void Reset()
        {
            _lastAcceptedPitch = 0;
            _lastAcceptedAt = default;
        }

        public double Filter(double rawPitch, DateTime timestamp)
        {
            if (rawPitch < MinimumPitch || double.IsNaN(rawPitch) || double.IsInfinity(rawPitch))
                return 0;

            var corrected = CorrectLikelyHarmonic(rawPitch);
            if (corrected <= 0)
                return 0;

            if (_lastAcceptedPitch > 0)
            {
                var elapsedSeconds = Math.Max(0, (timestamp - _lastAcceptedAt).TotalSeconds);
                var fastJump = elapsedSeconds <= FastJumpWindowSeconds &&
                    Math.Abs(corrected - _lastAcceptedPitch) > MaximumInstantJumpHz;

                if (fastJump)
                    return _lastAcceptedPitch;
            }

            _lastAcceptedPitch = corrected;
            _lastAcceptedAt = timestamp;
            return corrected;
        }

        private double CorrectLikelyHarmonic(double rawPitch)
        {
            if (_lastAcceptedPitch <= 0)
                return rawPitch <= MaximumDisplayPitch ? rawPitch : 0;

            var bestPitch = rawPitch;
            var bestDistance = Math.Abs(rawPitch - _lastAcceptedPitch);

            foreach (var divisor in new[] { 2.0, 3.0, 4.0 })
            {
                var candidate = rawPitch / divisor;
                if (candidate < MinimumPitch || candidate > MaximumDisplayPitch)
                    continue;

                var relativeDistance = Math.Abs(candidate - _lastAcceptedPitch) / _lastAcceptedPitch;
                if (relativeDistance <= HarmonicMatchTolerance && relativeDistance < bestDistance / _lastAcceptedPitch)
                {
                    bestPitch = candidate;
                    bestDistance = Math.Abs(candidate - _lastAcceptedPitch);
                }
            }

            return bestPitch <= MaximumDisplayPitch ? bestPitch : 0;
        }
    }
}
