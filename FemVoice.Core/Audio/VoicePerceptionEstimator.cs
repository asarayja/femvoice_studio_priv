using System;

namespace FemVoiceStudio.Audio
{
    /// <summary>How a voice is likely PERCEIVED on a masculine→androgynous→feminine axis.</summary>
    public enum VoicePerceptionBand { Masculine, Androgynous, Feminine }

    /// <summary>The single most useful next step to move toward a more feminine-perceived voice (or to hold).</summary>
    public enum VoicePerceptionHint { None, RaisePitch, BrightenResonance, HoldSteady }

    /// <summary>
    /// A transparent, explainable estimate of how the voice currently READS (masculine / androgynous / feminine),
    /// combining the two strongest perceptual cues for voice gender: PITCH (fundamental frequency) and RESONANCE
    /// (brightness / forward placement). <see cref="Score"/> is a 0–100 "how feminine-perceived" value; the two
    /// component scores are exposed so the readout is a MIRROR the user can reason about, not a black box (the main
    /// complaint about competitor apps' opaque "gender" verdict). <see cref="Hint"/> names the single highest-leverage
    /// next step.
    /// </summary>
    public readonly record struct VoicePerception(
        VoicePerceptionBand Band,
        int Score,           // combined 0–100 (higher = reads more feminine)
        int PitchScore,      // 0–100 pitch contribution (transparency)
        int ResonanceScore,  // 0–100 resonance/brightness contribution (transparency)
        VoicePerceptionHint Hint);

    /// <summary>
    /// Turns the live pitch (Hz) and the calibrated brightness percent (from <see cref="VoiceBrightnessMeter"/>) into a
    /// perceived-gender reading. Pure and deterministic, so it is fully unit-testable.
    ///
    /// WHY THIS EXISTS: pitch and resonance shown as two separate meters do not answer the question a trainee actually
    /// asks — "does my voice read as feminine yet, and if not, what do I fix?". This estimator answers exactly that,
    /// honestly: it shows the combined reading AND both ingredients AND the single most useful next step.
    ///
    /// THE MODEL (documented, not hidden):
    ///   • Pitch → 0–100: fully masculine at/below <see cref="PitchFloorHz"/> (≈140 Hz), fully feminine at/above
    ///     <see cref="PitchCeilHz"/> (≈210 Hz), linear between (≈50% near 175 Hz — the commonly-cited androgynous
    ///     speaking range). Absolute and mic-independent (f0 is robust).
    ///   • Resonance → 0–100: the calibrated brightness percent is used directly. When the user has calibrated (mic
    ///     baseline), this is measured RELATIVE to their own relaxed voice, so it reads "brighter/more forward than my
    ///     habitual voice" — a reliable training signal but self-referential, which is why pitch (absolute) is weighted
    ///     a little higher.
    ///   • Combined = <see cref="PitchWeight"/>·pitch + <see cref="ResonanceWeight"/>·resonance (0.55 / 0.45).
    ///     Both cues matter — raising pitch without brightening resonance still tends to read masculine — but pitch is
    ///     the more portable measurement, hence the slight lean.
    ///   • Band: feminine ≥ <see cref="FeminineThreshold"/>, androgynous ≥ <see cref="AndrogynousThreshold"/>, else
    ///     masculine.
    ///   • Hint: if already feminine → hold; otherwise coach the WEAKER of the two ingredients (ties go to resonance,
    ///     the healthier and more sustainable lever than simply pushing pitch ever higher).
    /// These anchors are perceptual guides, NOT a clinical diagnosis, and the same voice can read differently to
    /// different listeners — the UI is expected to say so.
    /// </summary>
    public static class VoicePerceptionEstimator
    {
        // Pitch → 0–100 anchors (speaking fundamental frequency). Masculine-typical ≤140 Hz; feminine-typical ≥210 Hz.
        public const double PitchFloorHz = 140.0;
        public const double PitchCeilHz = 210.0;

        // Cue weighting. Pitch is absolute (mic-independent); calibrated resonance is self-referential → slight lean.
        public const double PitchWeight = 0.55;
        public const double ResonanceWeight = 0.45;

        // Band thresholds on the combined 0–100 score.
        public const int FeminineThreshold = 62;
        public const int AndrogynousThreshold = 38;

        /// <summary>
        /// Estimate the perceived-gender reading. <paramref name="pitchHz"/> is the current (stabilized) fundamental in
        /// Hz; <paramref name="brightnessPercent"/> is the 0–100 value from <see cref="VoiceBrightnessMeter"/>. Callers
        /// gate on voicing; a non-positive pitch is treated as the masculine floor for the pitch component.
        /// </summary>
        public static VoicePerception Estimate(double pitchHz, int brightnessPercent)
        {
            int pitchScore = PitchScoreOf(pitchHz);
            int resonanceScore = Math.Clamp(brightnessPercent, 0, 100);

            int combined = (int)Math.Round(Math.Clamp(
                PitchWeight * pitchScore + ResonanceWeight * resonanceScore, 0.0, 100.0));

            VoicePerceptionBand band =
                combined >= FeminineThreshold ? VoicePerceptionBand.Feminine :
                combined >= AndrogynousThreshold ? VoicePerceptionBand.Androgynous :
                VoicePerceptionBand.Masculine;

            VoicePerceptionHint hint =
                band == VoicePerceptionBand.Feminine ? VoicePerceptionHint.HoldSteady
                // Coach the weaker ingredient; a tie favours resonance (healthier/more sustainable than raising pitch).
                : pitchScore < resonanceScore ? VoicePerceptionHint.RaisePitch
                : VoicePerceptionHint.BrightenResonance;

            return new VoicePerception(band, combined, pitchScore, resonanceScore, hint);
        }

        /// <summary>Map a speaking pitch (Hz) to a 0–100 "how feminine on pitch alone" score (clamped, linear).</summary>
        public static int PitchScoreOf(double pitchHz)
        {
            if (pitchHz <= 0) return 0;
            double pct = (pitchHz - PitchFloorHz) / (PitchCeilHz - PitchFloorHz) * 100.0;
            return (int)Math.Round(Math.Clamp(pct, 0.0, 100.0));
        }
    }
}
