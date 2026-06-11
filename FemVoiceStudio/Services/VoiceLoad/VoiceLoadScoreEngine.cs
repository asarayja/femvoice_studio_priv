using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models.VoiceLoad;

namespace FemVoiceStudio.Services.VoiceLoad
{
    /// <summary>
    /// Sprint F (Agent 2) — pure, explainable VoiceLoadScore calculation.
    ///
    /// Maps accumulated, already-derived session aggregates to a 0–100 cumulative-load
    /// score plus the contributing drivers. NO signal processing, NO clock, NO mutation:
    /// it is a deterministic function of its input. Smoothing/flicker-prevention is the
    /// monitor's responsibility (it EMA-smooths the raw score before banding).
    ///
    /// Design rules (from the brief): score is capped at 100; each contribution is capped
    /// so no single non-severe metric can dominate; strain/fatigue (health-derived) carry
    /// the largest weights because they ARE the severe signals; the band only jumps
    /// straight to PauseRecommended when a safety/health rule has already triggered.
    /// </summary>
    public static class VoiceLoadScoreEngine
    {
        /// <summary>Accumulated, already-derived session aggregates (the monitor maintains these).</summary>
        public readonly record struct Aggregates(
            double ActiveVoicedSeconds,
            double TimeSinceLastPauseSeconds,
            double StabilityEma,          // 0–1 (higher = more stable)
            double DropoutRate,           // 0–1
            double RmsVolatility,         // 0–1, or negative if RMS never measured
            double ResonanceInstability,  // 0–1, or negative if resonance not used
            double StrainEma,             // 0–1
            double FatigueEma,            // 0–1
            double OutOfComfortRate,      // 0–1
            bool HealthPauseRecommended,
            bool IsSafetyLocked,
            int HealthStateRank);         // 0 Normal .. 3 Lock

        public readonly record struct ScoreResult(double RawScore, IReadOnlyList<string> Drivers);

        // Per-driver caps. Strain & fatigue (severe, health-derived) are weighted highest.
        private const double DurationCap = 22;
        private const double SincePauseCap = 18;
        private const double InstabilityCap = 18;
        private const double DropoutCap = 10;
        private const double RmsCap = 8;
        private const double ResonanceCap = 8;
        private const double StrainCap = 24;
        private const double FatigueCap = 24;
        private const double ComfortCap = 8;

        // A driver is "primary" once its contribution clears this many points.
        private const double DriverThreshold = 5;

        /// <summary>Computes the raw (un-smoothed) 0–100 score and the list of primary driver codes.</summary>
        public static ScoreResult ComputeRawScore(in Aggregates a)
        {
            var contributions = new List<(string Driver, double Points)>
            {
                ("DURATION",              DurationCap   * Ramp(a.ActiveVoicedSeconds, 900)),        // ~15 min voiced
                ("TIME_SINCE_PAUSE",      SincePauseCap * Ramp(a.TimeSinceLastPauseSeconds, 480)),  // ~8 min
                ("PITCH_INSTABILITY",     InstabilityCap * Deficit(a.StabilityEma, 0.70)),
                ("PITCH_DROPOUTS",        DropoutCap    * Ramp(a.DropoutRate, 0.5)),
                ("RMS_VOLATILITY",        a.RmsVolatility < 0 ? 0 : RmsCap * Ramp(a.RmsVolatility, 0.30)),
                ("RESONANCE_INSTABILITY", a.ResonanceInstability < 0 ? 0 : ResonanceCap * Clamp01(a.ResonanceInstability)),
                ("STRAIN",                StrainCap     * Clamp01(a.StrainEma)),
                ("FATIGUE",               FatigueCap    * Clamp01(a.FatigueEma)),
                ("COMFORT_DECLINE",       ComfortCap    * Clamp01(a.OutOfComfortRate)),
            };

            var raw = Math.Min(100.0, contributions.Sum(c => c.Points));

            var drivers = contributions
                .Where(c => c.Points >= DriverThreshold)
                .OrderByDescending(c => c.Points)
                .Select(c => c.Driver)
                .ToList();

            if (a.IsSafetyLocked) drivers.Insert(0, "SAFETY_LOCK");
            else if (a.HealthPauseRecommended || a.HealthStateRank >= 2) drivers.Insert(0, "HEALTH_PAUSE");

            return new ScoreResult(raw, drivers);
        }

        /// <summary>
        /// Resolves the band from the SMOOTHED score, with the brief's rule: a straight jump
        /// to PauseRecommended is only allowed when a safety/health rule has already triggered.
        /// </summary>
        public static VoiceLoadBand ResolveBand(double smoothedScore, bool isSafetyLocked, bool healthPauseRecommended, int healthStateRank, bool dataSufficient)
        {
            if (!dataSufficient) return VoiceLoadBand.InsufficientData;

            if (isSafetyLocked || healthPauseRecommended || healthStateRank >= 2)
                return VoiceLoadBand.PauseRecommended;

            return smoothedScore switch
            {
                <= 30 => VoiceLoadBand.Low,
                <= 60 => VoiceLoadBand.Moderate,
                <= 80 => VoiceLoadBand.High,
                _ => VoiceLoadBand.PauseRecommended
            };
        }

        private static double Ramp(double value, double full)
            => full <= 0 ? 0 : Clamp01(value / full);

        // Fraction by which `value` falls below `floor` (0 when at/above floor).
        private static double Deficit(double value, double floor)
            => floor <= 0 ? 0 : Clamp01(Math.Max(0, floor - value) / floor);

        private static double Clamp01(double v)
            => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1);
    }
}
