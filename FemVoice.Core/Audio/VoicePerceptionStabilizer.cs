using System;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Makes the perceived-voice mirror READABLE. <see cref="VoicePerceptionEstimator"/> is pure and evaluates a single
    /// audio frame, so its raw output changes tens of times per second: a voice sitting near a band threshold made the
    /// headline label flip "Maskulin ↔ Androgyn" continuously, and the coaching tip flipped with it. That is unusable
    /// while training and reads as an unreliable instrument.
    ///
    /// This holds the small amount of state needed to fix that, kept out of the estimator so that stays pure:
    ///   • an exponential moving average over the combined score AND both ingredient scores, and
    ///   • HYSTERESIS on the band — a band must be entered at its threshold but is only left once the smoothed score
    ///     falls <see cref="BandHysteresis"/> points BELOW it, so a value hovering on a threshold cannot oscillate.
    /// The hint is then derived from the smoothed components and the stabilized band, so it cannot flip on its own.
    ///
    /// Deterministic and frame-rate agnostic (no clocks), so it is fully unit-testable.
    /// </summary>
    public sealed class VoicePerceptionStabilizer
    {
        /// <summary>EMA weight for each new frame. At the ~10–40 frames/s the capture pipeline produces this is roughly
        /// a 0.2–0.5 s time constant: fast enough to feel live, slow enough to stop per-frame jitter.</summary>
        public const double SmoothingAlpha = 0.12;

        /// <summary>Points the smoothed score must fall BELOW a band's entry threshold before that band is left.</summary>
        public const int BandHysteresis = 5;

        private double? _score;
        private double? _pitch;
        private double? _resonance;
        private VoicePerceptionBand? _band;

        /// <summary>Forget all history (call when a session starts/stops so a new session does not inherit the old voice).</summary>
        public void Reset()
        {
            _score = _pitch = _resonance = null;
            _band = null;
        }

        /// <summary>
        /// Feed one raw per-frame estimate and get the stabilized reading to display. The first call after
        /// <see cref="Reset"/> seeds the averages with the raw values, so a reading appears immediately instead of
        /// easing up from zero.
        /// </summary>
        public VoicePerception Update(VoicePerception raw)
        {
            _score = Ema(_score, raw.Score);
            _pitch = Ema(_pitch, raw.PitchScore);
            _resonance = Ema(_resonance, raw.ResonanceScore);

            int score = Round(_score!.Value);
            int pitch = Round(_pitch!.Value);
            int resonance = Round(_resonance!.Value);

            VoicePerceptionBand band = StabilizeBand(score);
            _band = band;

            VoicePerceptionHint hint =
                band == VoicePerceptionBand.Feminine ? VoicePerceptionHint.HoldSteady
                // Same rule as the estimator: coach the weaker ingredient; a tie favours resonance.
                : pitch < resonance ? VoicePerceptionHint.RaisePitch
                : VoicePerceptionHint.BrightenResonance;

            return new VoicePerception(band, score, pitch, resonance, hint);
        }

        private static double Ema(double? previous, int sample)
            => previous is null ? sample : previous.Value + SmoothingAlpha * (sample - previous.Value);

        private static int Round(double v) => (int)Math.Round(Math.Clamp(v, 0.0, 100.0));

        /// <summary>Band for the smoothed score, requiring a clear move before leaving the current band.</summary>
        private VoicePerceptionBand StabilizeBand(int score)
        {
            int feminineEnter = VoicePerceptionEstimator.FeminineThreshold;
            int feminineLeave = feminineEnter - BandHysteresis;
            int androgynousEnter = VoicePerceptionEstimator.AndrogynousThreshold;
            int androgynousLeave = androgynousEnter - BandHysteresis;

            if (_band is null)
                return score >= feminineEnter ? VoicePerceptionBand.Feminine
                     : score >= androgynousEnter ? VoicePerceptionBand.Androgynous
                     : VoicePerceptionBand.Masculine;

            switch (_band.Value)
            {
                case VoicePerceptionBand.Feminine:
                    if (score >= feminineLeave) return VoicePerceptionBand.Feminine;
                    return score >= androgynousLeave ? VoicePerceptionBand.Androgynous : VoicePerceptionBand.Masculine;

                case VoicePerceptionBand.Androgynous:
                    if (score >= feminineEnter) return VoicePerceptionBand.Feminine;
                    return score < androgynousLeave ? VoicePerceptionBand.Masculine : VoicePerceptionBand.Androgynous;

                default:   // Masculine
                    if (score >= feminineEnter) return VoicePerceptionBand.Feminine;
                    return score >= androgynousEnter ? VoicePerceptionBand.Androgynous : VoicePerceptionBand.Masculine;
            }
        }
    }
}
