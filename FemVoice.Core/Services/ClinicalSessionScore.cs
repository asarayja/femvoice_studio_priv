using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Clinically weighted session score (0–100) replacing the previous time-based
    /// score in ExerciseWindow. Design rules (see analysis doc):
    ///
    ///   • Pitch contributes ONLY through comfort-zone compliance (share of time
    ///     spent inside the zone) — never through pitch height. Higher pitch can
    ///     therefore never raise the score on its own.
    ///   • Time is a floor requirement (hard cap when under half the target
    ///     duration), not a point source.
    ///   • Safety gates are hard caps, not subtractions: a session with a safety
    ///     lock can never look "good" regardless of the other metrics.
    ///   • Weights mirror the profile flags so exercises are scored only on the
    ///     dimensions they actually train (same pattern as CalculateLiveCompositeScore).
    /// </summary>
    public static class ClinicalSessionScore
    {
        public static double Calculate(
            ExerciseSessionOutcome outcome,
            ExerciseTargetProfile profile,
            int elapsedSeconds,
            int targetSeconds)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            // No evaluated voice data → no score. A silent session earns nothing.
            if (outcome.EvaluatedTicks == 0)
                return 0;

            double weightedTotal = 0;
            double weight = 0;

            if (profile.UsesResonance)
            {
                weightedTotal += Math.Clamp(outcome.AverageResonance, 0, 1) * 0.45;
                weight += 0.45;
            }

            if (profile.UsesStability)
            {
                weightedTotal += Math.Clamp(outcome.AverageStability, 0, 1) * 0.25;
                weight += 0.25;
            }

            if (profile.UsesPitch)
            {
                // Comfort compliance — being IN the zone, never how high in the zone.
                weightedTotal += Math.Clamp(outcome.ComfortCompliance, 0, 1) * 0.20;
                weight += 0.20;
            }

            if (profile.RequiredHoldSeconds > 0)
            {
                weightedTotal += Math.Clamp(outcome.HoldCompletion, 0, 1) * 0.10;
                weight += 0.10;
            }

            var score = weight > 0 ? (weightedTotal / weight) * 100 : 0;

            // ── Harde kliniske gater (caps) ──────────────────────────────────────
            if (outcome.SafetyLockEpisodes > 0)
                score = Math.Min(score, 40);

            if (profile.UsesPitch && outcome.ComfortCompliance < 0.5)
                score = Math.Min(score, 55);

            if (targetSeconds > 0 && elapsedSeconds < targetSeconds / 2)
                score = Math.Min(score, 30);

            return Math.Clamp(score, 0, 100);
        }
    }
}
