using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Detects plateau, breakthrough and regression patterns in a longitudinal series of
    /// <see cref="TrendWindow"/>s. Pure, stateless computation — takes an ordered list of
    /// windows (oldest first) and returns one optional detection per pattern.
    ///
    /// This is DESCRIPTIVE intelligence: results are observations, never safety gates.
    /// Health/Recovery/Safety gates are the only authorities that can restrict training;
    /// these results are surfaced as coaching insights only.
    ///
    /// Thresholds (all public so tests can reference them without magic literals):
    ///   <see cref="PlateauSlopeBand"/>  — |slope| &lt;= this ⇒ flat  (points/session)
    ///   <see cref="PlateauMinWindows"/> — consecutive flat windows required
    ///   <see cref="RegressionSlopeThreshold"/> — slope &lt;= this ⇒ regression
    ///   <see cref="BreakthroughMinDelta"/> — short−long slope difference required
    /// </summary>
    public sealed class VoicePatternDetector
    {
        // ─────────────────────────────────────────────────────────────────────────
        // Public thresholds (spec §A56)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Maximum absolute composite slope (points/session) that still counts as flat for
        /// plateau detection. |CompositeSlope| &lt;= this on ≥ <see cref="PlateauMinWindows"/>
        /// consecutive data-sufficient windows triggers a plateau.
        /// </summary>
        public const double PlateauSlopeBand = 0.2;

        /// <summary>
        /// Minimum number of consecutive data-sufficient windows with |slope| &lt;=
        /// <see cref="PlateauSlopeBand"/> required to declare a plateau.
        /// </summary>
        public const int PlateauMinWindows = 2;

        /// <summary>
        /// OLS slope threshold for regression detection (points/session). A per-dimension
        /// slope at or below this value triggers a regression flag. Mirrors the clinical
        /// ComputeComfortSlope threshold in RecoveryIntelligenceService.
        /// </summary>
        public const double RegressionSlopeThreshold = -1.5;

        /// <summary>
        /// Minimum short-minus-long composite slope difference (points/session) required to
        /// declare a breakthrough. Guards against noise-driven false positives.
        /// </summary>
        public const double BreakthroughMinDelta = 0.5;

        // ─────────────────────────────────────────────────────────────────────────
        // Core API
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Analyses the supplied windows (oldest first) and returns one optional detection
        /// per pattern type. Requires at least 2 data-sufficient windows for any detection.
        /// All three detectors run independently on the same input; returning a tuple keeps
        /// the API minimal and allocation-free.
        /// </summary>
        /// <param name="windows">
        /// Ordered list of trend windows, oldest first. May be empty or contain low-data
        /// windows (<see cref="TrendWindow.HasEnoughData"/> == false) — those are skipped
        /// for pattern detection but included for window-count bookkeeping.
        /// </param>
        public (PlateauState? Plateau, BreakthroughState? Breakthrough, RegressionState? Regression)
            Compute(IReadOnlyList<TrendWindow> windows)
        {
            if (windows is null || windows.Count < 2)
                return (null, null, null);

            var plateau      = DetectPlateau(windows);
            var breakthrough = DetectBreakthrough(windows);
            var regression   = DetectRegression(windows);

            return (plateau, breakthrough, regression);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Plateau
        // ─────────────────────────────────────────────────────────────────────────

        private static PlateauState? DetectPlateau(IReadOnlyList<TrendWindow> windows)
        {
            // Only consider windows with enough data; low-data ⇒ no plateau claim.
            var sufficient = windows.Where(w => w.HasEnoughData).ToList();
            if (sufficient.Count < PlateauMinWindows) return null;

            // Walk consecutive pairs looking for the longest flat run.
            // We check composite flatness; then pick the weakest (most-plateau) dimension.
            int bestRunLength = 0;
            int bestRunEnd    = -1;

            int currentRun  = 0;
            int currentStart = 0;

            for (var i = 0; i < sufficient.Count; i++)
            {
                var w = sufficient[i];
                if (Math.Abs(w.CompositeSlope) <= PlateauSlopeBand)
                {
                    if (currentRun == 0) currentStart = i;
                    currentRun++;
                    if (currentRun > bestRunLength)
                    {
                        bestRunLength = currentRun;
                        bestRunEnd    = i;
                    }
                }
                else
                {
                    currentRun = 0;
                }
            }

            if (bestRunLength < PlateauMinWindows) return null;

            // The run spans bestRunEnd-bestRunLength+1 .. bestRunEnd (inclusive).
            int runStart = bestRunEnd - bestRunLength + 1;
            var runWindows = sufficient.Skip(runStart).Take(bestRunLength).ToList();

            // Weakest dimension: lowest mean slope across the run (closest to zero or most
            // negative), as a proxy for "needs most attention".
            var dim = WeakestDimension(runWindows);

            var firstWindow = runWindows[0];
            var lastWindow  = runWindows[bestRunLength - 1];
            int durationDays = (int)Math.Round((lastWindow.To - firstWindow.From).TotalDays);
            double observedSlope = runWindows.Average(w => w.CompositeSlope);

            // SeverityScore: scales with duration and flatness (closer to 0 = worse plateau).
            // Cap at 100.
            double flatnessFactor  = 1.0 - Math.Min(Math.Abs(observedSlope) / PlateauSlopeBand, 1.0);
            double durationFactor  = Math.Min(durationDays / 90.0, 1.0);
            double severity        = Math.Clamp((flatnessFactor * 60.0) + (durationFactor * 40.0), 1.0, 100.0);

            return new PlateauState
            {
                ReasonCode          = $"PLATEAU_{dim}",
                Dimension           = dim,
                SeverityScore       = severity,
                WindowDays          = firstWindow.WindowDays,
                StartedAt           = firstWindow.From,
                PlateauDurationDays = durationDays,
                ObservedSlope       = observedSlope
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Breakthrough
        // ─────────────────────────────────────────────────────────────────────────

        private static BreakthroughState? DetectBreakthrough(IReadOnlyList<TrendWindow> windows)
        {
            // Need at least a shorter and a longer window, both data-sufficient.
            var sufficient = windows.Where(w => w.HasEnoughData).ToList();
            if (sufficient.Count < 2) return null;

            // Compare the most-recent window (shorter/later) with the one before it.
            // Breakthrough = recent slope is meaningfully more positive than the prior slope.
            var recent = sufficient[sufficient.Count - 1];
            var prior  = sufficient[sufficient.Count - 2];

            double delta = recent.CompositeSlope - prior.CompositeSlope;
            if (delta < BreakthroughMinDelta) return null;

            // Additional requirement: recent slope must actually be positive.
            if (recent.CompositeSlope <= 0.0) return null;

            // Find the dimension with the largest positive per-dimension inflection.
            var dim = BestInflectionDimension(recent, prior);

            double severity = Math.Clamp(delta / 2.0 * 50.0, 1.0, 100.0); // 2 pts/session delta ⇒ 50 severity

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
        // Regression
        // ─────────────────────────────────────────────────────────────────────────

        private static RegressionState? DetectRegression(IReadOnlyList<TrendWindow> windows)
        {
            // Use the most-recent data-sufficient window for regression.
            var sufficient = windows.Where(w => w.HasEnoughData).ToList();
            if (sufficient.Count == 0) return null;

            var latest = sufficient[sufficient.Count - 1];

            // Collect all dimensions whose slope is at or below the clinical threshold.
            var declining = latest.DimensionSlopes
                .Where(kv => kv.Value <= RegressionSlopeThreshold)
                .OrderBy(kv => kv.Value)    // most negative first
                .ToList();

            if (declining.Count == 0) return null;

            // Primary dimension: most severely declining.
            var primaryKv  = declining[0];
            var primaryDim = primaryKv.Key;
            double primarySlope = primaryKv.Value;

            // SeverityScore scales with how far below the threshold the slope is.
            double baseSeverity = Math.Clamp(
                Math.Abs(primarySlope - RegressionSlopeThreshold) / 3.0 * 70.0 + 20.0,
                20.0, 100.0);

            // CompoundSeverity: each additional declining dimension adds 15% compound factor.
            double compoundFactor = 1.0 + (declining.Count - 1) * 0.15;
            double compoundSeverity = Math.Clamp(baseSeverity * compoundFactor, baseSeverity, 100.0);

            return new RegressionState
            {
                ReasonCode       = $"REGRESSION_{primaryDim}",
                Dimension        = primaryDim,
                SeverityScore    = baseSeverity,
                WindowDays       = latest.WindowDays,
                DetectedAt       = latest.From,
                DeclineSlope     = primarySlope,
                CompoundSeverity = compoundSeverity
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the dimension whose mean slope across the run windows is closest to zero
        /// (most "stuck"). Ties broken by clinical hierarchy order (lower enum value wins).
        /// </summary>
        private static VoiceDimension WeakestDimension(IReadOnlyList<TrendWindow> runWindows)
        {
            // Aggregate mean slope per dimension across all run windows.
            var dimMeans = new Dictionary<VoiceDimension, (double sum, int count)>();

            foreach (var w in runWindows)
            {
                foreach (var kv in w.DimensionSlopes)
                {
                    if (!dimMeans.TryGetValue(kv.Key, out var acc))
                        acc = (0.0, 0);
                    dimMeans[kv.Key] = (acc.sum + kv.Value, acc.count + 1);
                }
            }

            if (dimMeans.Count == 0)
                return VoiceDimension.Resonance; // fallback: primary training dimension

            // Weakest = smallest mean slope (could be slightly negative or near-zero).
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
        /// Returns the dimension with the largest positive slope delta (recent minus prior).
        /// Ties broken by clinical hierarchy order.
        /// </summary>
        private static VoiceDimension BestInflectionDimension(TrendWindow recent, TrendWindow prior)
        {
            VoiceDimension best = VoiceDimension.Resonance;
            double bestDelta = double.MinValue;

            foreach (var kv in recent.DimensionSlopes)
            {
                prior.DimensionSlopes.TryGetValue(kv.Key, out var priorSlope);
                double delta = kv.Value - priorSlope;
                if (delta > bestDelta || (delta == bestDelta && (int)kv.Key < (int)best))
                {
                    bestDelta = delta;
                    best = kv.Key;
                }
            }

            return best;
        }

        // Ordinary-least-squares slope of y over x = 0,1,2,…  Returns 0 when undetermined.
        // Verbatim copy of LearningPathProfileBuilder.LinearSlope (per reuse spec).
        private static double LinearSlope(IReadOnlyList<double> y)
        {
            var n = y.Count;
            if (n < 2) return 0.0;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var i = 0; i < n; i++)
            {
                sumX  += i;
                sumY  += y[i];
                sumXY += i * y[i];
                sumX2 += (double)i * i;
            }

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 0.0001) return 0.0;
            return (n * sumXY - sumX * sumY) / denominator;
        }
    }
}
