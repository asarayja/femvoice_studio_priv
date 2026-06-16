using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Detects plateau, breakthrough and regression patterns across the canonical set of
    /// HORIZON windows produced by <see cref="TrendEngineService.Compute"/>. Pure, stateless,
    /// order-independent computation: returns one optional detection per pattern.
    ///
    /// HORIZON SEMANTICS (critical):
    /// <see cref="TrendEngineService.Compute"/> emits CUMULATIVE, NESTED windows
    /// [now−7,now) / [now−30,now) / [now−90,now) / [now−180,now). These are NOT a disjoint
    /// "oldest-first" chronological series — every recent session is counted in ALL windows
    /// that contain it. The shorter the window, the finer/fresher its resolution; the longer
    /// the window, the more aggregated and the further back its slope reaches.
    ///
    /// Therefore this detector NEVER relies on list position. It re-sorts internally by
    /// <see cref="TrendWindow.WindowDays"/> ascending and reasons by HORIZON:
    ///   recent  = smallest WindowDays  (freshest resolution, e.g. 7d)
    ///   longest = largest  WindowDays  (most aggregated / longest reach, e.g. 180d)
    /// Passing the same windows in any order yields identical results.
    ///
    /// This is DESCRIPTIVE intelligence: results are observations, never safety gates.
    /// Health/Recovery/Safety gates are the only authorities that can restrict training;
    /// these results are surfaced as coaching insights only.
    ///
    /// Thresholds (all public so tests can reference them without magic literals):
    ///   <see cref="PlateauSlopeBand"/>            — |slope| &lt;= this ⇒ flat  (points/session)
    ///   <see cref="RegressionDeclineThreshold"/>  — per-dim slope &lt;= this ⇒ regression
    ///   <see cref="BreakthroughDeltaThreshold"/>  — (recent − longest) slope acceleration required
    /// </summary>
    public sealed class VoicePatternDetector
    {
        // ─────────────────────────────────────────────────────────────────────────
        // Public thresholds
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Maximum absolute composite slope (points/session) that still counts as flat for
        /// plateau detection. A persistent plateau requires BOTH the recent (shortest) and
        /// the longest horizon to have |CompositeSlope| &lt;= this.
        /// </summary>
        public const double PlateauSlopeBand = 0.2;

        /// <summary>
        /// OLS slope threshold for regression detection (points/session). A per-dimension
        /// slope in the RECENT (shortest) horizon at or below this value triggers a
        /// regression flag. Mirrors the clinical ComputeComfortSlope threshold in
        /// RecoveryIntelligenceService.
        /// </summary>
        public const double RegressionDeclineThreshold = -1.5;

        /// <summary>
        /// Minimum acceleration — (recent horizon composite slope) minus (longest horizon
        /// composite slope), in points/session — required to declare a breakthrough.
        ///
        /// Default 3.0 points/session. Rationale: the composite score is on a 0–100 scale and
        /// the slope unit is points per SESSION step. A genuine, coaching-worthy inflection
        /// means the freshest horizon is climbing several points per session FASTER than the
        /// long-run average. 3.0 is well above the ±1.0 band the confidence model treats as
        /// "normal" trend (see TrendEngineService.BuildConfidence, which clamps slope to
        /// [−1,1]); it filters out ordinary week-to-week noise while still firing on a real
        /// acceleration that is visible only in the short window. Lower values (≈0.5) re-admit
        /// the noise-driven false positives this redesign exists to remove.
        /// </summary>
        public const double BreakthroughDeltaThreshold = 3.0;

        // ─────────────────────────────────────────────────────────────────────────
        // Core API
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Analyses the supplied horizon windows and returns one optional detection per
        /// pattern type. The input may be in any order — windows are re-sorted internally by
        /// <see cref="TrendWindow.WindowDays"/>, so detection depends only on horizon span,
        /// never on list position.
        ///
        /// Degradation:
        ///   • 0 data-sufficient windows ⇒ all three null.
        ///   • 1 data-sufficient window  ⇒ breakthrough null AND plateau null (both need a
        ///     short+long contrast); regression may still fire on the single window.
        ///   • ≥2 data-sufficient windows ⇒ all three detectors may fire.
        /// Never throws on empty / NaN / Inf input.
        /// </summary>
        /// <param name="windows">
        /// The canonical cumulative-nested horizon windows from
        /// <see cref="TrendEngineService.Compute"/> (any order). Low-data windows
        /// (<see cref="TrendWindow.HasEnoughData"/> == false) are ignored.
        /// </param>
        public (PlateauState? Plateau, BreakthroughState? Breakthrough, RegressionState? Regression)
            Compute(IReadOnlyList<TrendWindow> windows)
        {
            if (windows is null || windows.Count == 0)
                return (null, null, null);

            // Order-independence: re-sort by horizon span ascending so that
            // First() = freshest resolution (shortest), Last() = longest reach.
            // Distinct horizons only — duplicate WindowDays must not masquerade as a contrast.
            var sufficient = windows
                .Where(w => w is not null && w.HasEnoughData)
                .GroupBy(w => w.WindowDays)
                .Select(g => g.First())
                .OrderBy(w => w.WindowDays)
                .ToList();

            if (sufficient.Count == 0)
                return (null, null, null);

            var recent  = sufficient[0];                       // smallest WindowDays = freshest
            var longest = sufficient[sufficient.Count - 1];    // largest  WindowDays = longest reach

            var hasTwoHorizons = sufficient.Count >= 2;

            var plateau      = hasTwoHorizons ? DetectPlateau(recent, longest) : null;
            var breakthrough = hasTwoHorizons ? DetectBreakthrough(recent, longest) : null;
            var regression   = DetectRegression(recent);

            return (plateau, breakthrough, regression);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Breakthrough — recent horizon accelerating clearly past the long-run average.
        // ─────────────────────────────────────────────────────────────────────────

        private static BreakthroughState? DetectBreakthrough(TrendWindow recent, TrendWindow longest)
        {
            double recentSlope  = Sanitize(recent.CompositeSlope);
            double longestSlope  = Sanitize(longest.CompositeSlope);

            // Recent horizon must actually be improving.
            if (recentSlope <= 0.0) return null;

            double delta = recentSlope - longestSlope;
            if (!IsFinite(delta) || delta < BreakthroughDeltaThreshold) return null;

            // Dimension with the largest positive per-dim acceleration (recent − longest).
            var dim = BestAccelerationDimension(recent, longest);

            // Severity scales with how far past the threshold the acceleration runs.
            // delta == threshold ⇒ ~50; 2× threshold ⇒ ~100.
            double severity = Math.Clamp(delta / BreakthroughDeltaThreshold * 50.0, 1.0, 100.0);

            return new BreakthroughState
            {
                ReasonCode     = $"BREAKTHROUGH_{dim}",
                Dimension      = dim,
                SeverityScore  = severity,
                WindowDays     = recent.WindowDays,
                DetectedAt     = recent.From,
                MagnitudeDelta = delta
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Regression — freshest horizon shows a clearly declining dimension.
        // ─────────────────────────────────────────────────────────────────────────

        private static RegressionState? DetectRegression(TrendWindow recent)
        {
            if (recent.DimensionSlopes is null || recent.DimensionSlopes.Count == 0)
                return null;

            // Dimensions whose RECENT-horizon slope is at or below the clinical threshold.
            var declining = recent.DimensionSlopes
                .Where(kv => IsFinite(kv.Value) && kv.Value <= RegressionDeclineThreshold)
                .OrderBy(kv => kv.Value)                        // most negative first
                .ThenBy(kv => (int)kv.Key)                      // stable tie-break: hierarchy order
                .ToList();

            if (declining.Count == 0) return null;

            var primaryDim   = declining[0].Key;
            double primarySlope = declining[0].Value;

            // Base severity grows with how far below the threshold the worst slope sits.
            double baseSeverity = Math.Clamp(
                Math.Abs(primarySlope - RegressionDeclineThreshold) / 3.0 * 70.0 + 20.0,
                20.0, 100.0);

            // CompoundSeverity rises when several dimensions fall together (+15% each extra).
            double compoundFactor   = 1.0 + (declining.Count - 1) * 0.15;
            double compoundSeverity = Math.Clamp(baseSeverity * compoundFactor, baseSeverity, 100.0);

            return new RegressionState
            {
                ReasonCode       = $"REGRESSION_{primaryDim}",
                Dimension        = primaryDim,
                SeverityScore    = baseSeverity,
                WindowDays       = recent.WindowDays,
                DetectedAt       = recent.From,
                DeclineSlope     = primarySlope,
                CompoundSeverity = compoundSeverity
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Plateau — flatness that persists across BOTH the recent and the longest horizon.
        // ─────────────────────────────────────────────────────────────────────────

        private static PlateauState? DetectPlateau(TrendWindow recent, TrendWindow longest)
        {
            double recentSlope  = Sanitize(recent.CompositeSlope);
            double longestSlope = Sanitize(longest.CompositeSlope);

            // Persistent plateau ⇒ BOTH horizons flat. If the long reach is flat but the
            // freshest horizon is moving, that is (potential) breakthrough/regression, not a
            // plateau; if the freshest is flat but the long reach is steep, the flatness is
            // not yet persistent.
            if (Math.Abs(recentSlope) > PlateauSlopeBand) return null;
            if (Math.Abs(longestSlope) > PlateauSlopeBand) return null;

            // Most relevant dimension: flattest/weakest mean slope across the two horizons.
            var dim = WeakestDimension(recent, longest);

            // Flatness extends as far back as the longest horizon reaches — this is the
            // duration, taken DIRECTLY from the longest window span. NEVER a cross-window
            // To−From subtraction over overlapping windows.
            int durationDays = longest.WindowDays;

            // Observed slope = the freshest reading of the (flat) trend.
            double observedSlope = recentSlope;

            // Severity scales with how flat it is and how far back the flatness reaches.
            double flatnessFactor = 1.0 - Math.Min(Math.Abs(observedSlope) / PlateauSlopeBand, 1.0);
            double durationFactor = Math.Min(durationDays / 180.0, 1.0);
            double severity = Math.Clamp((flatnessFactor * 60.0) + (durationFactor * 40.0), 1.0, 100.0);

            return new PlateauState
            {
                ReasonCode          = $"PLATEAU_{dim}",
                Dimension           = dim,
                SeverityScore       = severity,
                WindowDays          = recent.WindowDays,
                StartedAt           = longest.From,
                PlateauDurationDays = durationDays,
                ObservedSlope       = observedSlope
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Weakest dimension across the two horizons: lowest mean slope (most "stuck" or
        /// declining). Ties broken by clinical hierarchy order (lower enum value wins).
        /// </summary>
        private static VoiceDimension WeakestDimension(TrendWindow recent, TrendWindow longest)
        {
            var dimMeans = new Dictionary<VoiceDimension, (double sum, int count)>();

            foreach (var w in new[] { recent, longest })
            {
                if (w.DimensionSlopes is null) continue;
                foreach (var kv in w.DimensionSlopes)
                {
                    if (!IsFinite(kv.Value)) continue;
                    if (!dimMeans.TryGetValue(kv.Key, out var acc)) acc = (0.0, 0);
                    dimMeans[kv.Key] = (acc.sum + kv.Value, acc.count + 1);
                }
            }

            if (dimMeans.Count == 0)
                return VoiceDimension.Resonance; // fallback: primary training dimension

            VoiceDimension best = dimMeans.Keys.First();
            double bestMean = double.MaxValue;

            foreach (var kv in dimMeans)
            {
                var mean = kv.Value.sum / kv.Value.count;
                if (mean < bestMean || (mean == bestMean && (int)kv.Key < (int)best))
                {
                    bestMean = mean;
                    best = kv.Key;
                }
            }

            return best;
        }

        /// <summary>
        /// Dimension with the largest positive per-dim acceleration (recent slope minus
        /// longest slope). Ties broken by clinical hierarchy order (lower enum value wins).
        /// </summary>
        private static VoiceDimension BestAccelerationDimension(TrendWindow recent, TrendWindow longest)
        {
            VoiceDimension best = VoiceDimension.Resonance;
            double bestDelta = double.MinValue;
            bool found = false;

            if (recent.DimensionSlopes is not null)
            {
                foreach (var kv in recent.DimensionSlopes)
                {
                    if (!IsFinite(kv.Value)) continue;
                    double longestSlope = 0.0;
                    if (longest.DimensionSlopes is not null)
                        longest.DimensionSlopes.TryGetValue(kv.Key, out longestSlope);
                    if (!IsFinite(longestSlope)) longestSlope = 0.0;

                    double delta = kv.Value - longestSlope;
                    if (!found || delta > bestDelta || (delta == bestDelta && (int)kv.Key < (int)best))
                    {
                        bestDelta = delta;
                        best = kv.Key;
                        found = true;
                    }
                }
            }

            return best;
        }

        // ─── numeric guards ───────────────────────────────────────────────────────

        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        private static double Sanitize(double v) => IsFinite(v) ? v : 0.0;
    }
}
