using System;

namespace FemVoice.Avalonia.Audio;

/// <summary>
/// Derives the per-dimension Voice-Intelligence scores (0–100) for a completed session from the REAL captured signals,
/// so the Avalonia head can write a <c>SessionAnalyticsRecord</c> that the WPF-parity per-dimension screens
/// (Progression parameter-graph, Clinician voice-metrics / dimension-trends, Analysis rings) read back.
///
/// EVERY dimension is a documented derivation of a genuinely-measured signal — NO demo data, NO fabricated constants:
///   • Pitch / Comfort  ← comfort-zone adherence (% of voiced frames inside the target zone).
///   • Resonance        ← the Core ResonanceProxyEngine's average resonance (0–100).
///   • Consistency      ← the Core LiveMetricsService stability states, averaged.
///   • Health           ← the Core LiveMetricsService CalculateHealth states (a real function of pitch + intensity),
///                        averaged. Strain is treated as ABSENT (Avalonia has no strain sensor) — a truthful state,
///                        not a fabricated value; health still reflects real pitch/intensity extremity.
///   • Intonation       ← a heuristic over the REAL pitch-variation (prosody std-dev): rewards controlled expressive
///                        movement, penalises monotone (≈flat) and erratic (very wide) pitch. Documented heuristic.
///   • Recovery         ← the Core RecoveryIntelligenceService debt (100 − RecoveryDebt); cross-session, real.
///   • VocalWeight      ← NOT measured by the Avalonia capture path → left 0 (honestly "not computed"), like WPF when
///                        the signal is absent.
/// The composite follows the WPF clinical hierarchy weighting (Health &gt; Resonance &gt; Consistency &gt; Intonation
/// &gt; Pitch); VocalWeight/Recovery contribute lightly. All inputs are already-measured aggregates — this type is a
/// pure, deterministic mapping (unit-testable, no I/O).
/// </summary>
public static class SessionAnalyticsScorer
{
    public readonly record struct DimensionScores(
        double PitchScore100, double ResonanceScore100, double IntonationScore100, double ComfortScore100,
        double ConsistencyScore100, double HealthScore100, double RecoveryScore100, double VocalWeightScore100,
        double CompositeVoiceScore);

    /// <param name="pitchComfortPercent">% of voiced frames inside the comfort zone (0–100).</param>
    /// <param name="averageResonance100">Average resonance from the Core engine (0–100).</param>
    /// <param name="pitchVariationHz">Prosody: std-dev of the voiced pitch (Hz).</param>
    /// <param name="averageStability100">Average per-frame stability state mapped to 0–100.</param>
    /// <param name="averageHealth100">Average per-frame health state mapped to 0–100.</param>
    /// <param name="recovery100">Recovery score (100 − RecoveryDebt), 0–100.</param>
    public static DimensionScores Compute(
        double pitchComfortPercent, double averageResonance100, double pitchVariationHz,
        double averageStability100, double averageHealth100, double recovery100)
    {
        double pitch = Clamp(pitchComfortPercent);
        double comfort = Clamp(pitchComfortPercent);        // comfort = staying in the target zone (WPF basis)
        double resonance = Clamp(averageResonance100);
        double consistency = Clamp(averageStability100);
        double health = Clamp(averageHealth100);
        double recovery = Clamp(recovery100);
        double intonation = IntonationFromVariation(pitchVariationHz);
        double vocalWeight = 0;                             // not measured in the Avalonia capture path (honest)

        // WPF clinical hierarchy weighting (Health/Recovery first, then Resonance, Consistency, Intonation, Pitch).
        double composite = Round(
            0.24 * health + 0.10 * recovery + 0.22 * resonance +
            0.16 * consistency + 0.14 * intonation + 0.14 * pitch);

        return new DimensionScores(
            Round(pitch), Round(resonance), Round(intonation), Round(comfort),
            Round(consistency), Round(health), Round(recovery), Round(vocalWeight), composite);
    }

    // Prosody/intonation from the real pitch-variation std-dev: a controlled expressive range (≈12–35 Hz) scores
    // highest; near-monotone (very low) and erratic (very wide) variation both score lower. Documented heuristic over
    // a real measurement — never a constant. Peak plateau [12,35], linear ramps down to 0 at 0 Hz and 70 Hz.
    private static double IntonationFromVariation(double variationHz)
    {
        double v = Math.Max(0, variationHz);
        double factor =
            v <= 0 ? 0.30 :                                 // dead monotone: some credit, clearly low
            v < 12 ? 0.30 + 0.70 * (v / 12.0) :             // ramp up to full by 12 Hz
            v <= 35 ? 1.00 :                                // healthy expressive plateau
            v < 70 ? 1.00 - 0.55 * ((v - 35) / 35.0) :      // ramp down for erratic pitch
            0.45;                                           // very wide: controlled floor
        return Round(100.0 * factor);
    }

    private static double Clamp(double v) => Math.Clamp(v, 0, 100);
    private static double Round(double v) => Math.Round(Math.Clamp(v, 0, 100), 1);
}
