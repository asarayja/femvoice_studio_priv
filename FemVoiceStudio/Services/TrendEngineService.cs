using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Sprint C.3/C.4 Bølge 1 — Agent A4 (Trend Engine).
    ///
    /// Partitions a chronological <see cref="VoiceIntelligenceTrendPoint"/> series into
    /// closed-open [now−N days, now) windows (7 / 30 / 90 / 180 days) and computes
    /// per-dimension OLS slopes, composite statistics, and a confidence score for each.
    ///
    /// CLINICAL FRAMING: all output is DESCRIPTIVE / EXPLANATORY intelligence. It must
    /// never override the Safety &gt; Health &gt; Recovery priority hierarchy. Consumers must
    /// treat <see cref="TrendWindow.HasEnoughData"/> == false as "insufficient evidence".
    ///
    /// OLS FORMULA: verbatim copy of LearningPathProfileBuilder.LinearSlope (x = session
    /// index 0, 1, 2, …; returns 0 when undetermined). One private static copy per motor,
    /// exactly as ExerciseEffectivenessEngine does.
    ///
    /// CONFIDENCE FORMULA: matches LearningPathProfileBuilder.BuildConfidence —
    ///   raw = EmptyHistoryConfidence × BaseRetention + volume + trend + stability
    ///   where volume    = min(count, 12) / 12 × 25
    ///         trend     = clamp(compositeSlope, −1, +1) mapped 0..1 × 25   (≥3 pts only)
    ///         stability = (1 − clamp(stdDev / 25, 0, 1)) × 32.5
    ///   clamped to [0, 100].
    /// </summary>
    public sealed class TrendEngineService
    {
        // ── Window configuration ─────────────────────────────────────────────────────

        private static readonly int[] WindowDaysSet = { 7, 30, 90, 180 };

        // ── Confidence constants (verbatim from LearningPathProfileBuilder) ──────────

        private const double EmptyHistoryConfidence = 35.0;
        private const double BaseRetention          = 0.5;
        private const double VolumeWeight           = 25.0;
        private const double TrendWeight            = 25.0;
        private const double StabilityWeight        = 32.5;
        private const int    VolumeCap              = 12;
        private const int    MinPointsForTrend      = 3;
        private const double VolatilityNormaliser   = 25.0;

        // ── VoiceDimension → TrendPoint accessor map ─────────────────────────────────

        private static readonly IReadOnlyDictionary<VoiceDimension, Func<VoiceIntelligenceTrendPoint, double>>
            DimensionAccessors = new Dictionary<VoiceDimension, Func<VoiceIntelligenceTrendPoint, double>>
            {
                [VoiceDimension.Recovery]    = p => p.RecoveryScore100,
                [VoiceDimension.Comfort]     = p => p.ComfortScore100,
                [VoiceDimension.Resonance]   = p => p.ResonanceScore100,
                [VoiceDimension.Consistency] = p => p.ConsistencyScore100,
                [VoiceDimension.Intonation]  = p => p.IntonationScore100,
                [VoiceDimension.VocalWeight] = p => p.VocalWeightScore100,
                [VoiceDimension.Pitch]       = p => p.PitchScore100,
            };

        // ── Dependencies ─────────────────────────────────────────────────────────────

        private readonly SessionAnalyticsStore _store;

        /// <summary>
        /// Creates a new <see cref="TrendEngineService"/>.
        /// </summary>
        /// <param name="store">The analytics store used by <see cref="AnalyzeAsync"/>.</param>
        public TrendEngineService(SessionAnalyticsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        // ── Public API ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Partitions <paramref name="trend"/> into the four canonical windows
        /// (7 / 30 / 90 / 180 days relative to <paramref name="now"/>) and returns one
        /// <see cref="TrendWindow"/> per window span, always in ascending order of
        /// <see cref="TrendWindow.WindowDays"/>. The list always has exactly four entries.
        /// </summary>
        /// <param name="trend">
        /// Chronologically ordered Voice Intelligence points. The caller is responsible
        /// for ordering; points outside all windows are silently ignored.
        /// </param>
        /// <param name="now">
        /// Exclusive upper bound for all windows (i.e. the exclusive <c>To</c> of the
        /// 180-day window). Callers must pass this explicitly — no DateTime.Now dependency
        /// inside the engine — so tests remain fully deterministic.
        /// </param>
        public IReadOnlyList<TrendWindow> Compute(
            IReadOnlyList<VoiceIntelligenceTrendPoint> trend,
            DateTime now)
        {
            if (trend is null) throw new ArgumentNullException(nameof(trend));

            var result = new List<TrendWindow>(WindowDaysSet.Length);
            foreach (var days in WindowDaysSet)
            {
                var from = now.AddDays(-days);
                var points = trend
                    .Where(p => p.StartedAt >= from && p.StartedAt < now)
                    .ToList();
                result.Add(BuildWindow(days, from, now, points));
            }
            return result;
        }

        /// <summary>
        /// Reads the Voice Intelligence trend for <paramref name="userId"/> from
        /// <c>[now − 180 days, now)</c> via <see cref="SessionAnalyticsStore.GetVoiceIntelligenceTrendAsync"/>,
        /// then calls <see cref="Compute"/> and returns the four windows.
        /// </summary>
        public async Task<IReadOnlyList<TrendWindow>> AnalyzeAsync(
            DateTime now,
            int userId,
            CancellationToken ct = default)
        {
            var from = now.AddDays(-180);
            var trend = await _store.GetVoiceIntelligenceTrendAsync(from, now, userId, ct);
            return Compute(trend, now);
        }

        /// <summary>
        /// Fills a <see cref="VoiceDevelopmentProfile"/> from a pre-computed window list.
        /// WeeklyTrend  = 7-day and 30-day windows.
        /// MonthlyTrend = 90-day and 180-day windows.
        /// CompositeVoiceScore = the most-recent session's composite score (last point in
        /// the 7-day window if available, otherwise last point in 30/90/180-day window,
        /// otherwise 0).
        /// Plateau, Breakthrough, Regression are set to null here — they are populated
        /// by the A56 integration layer.
        /// HasEnoughData = true when at least one window has SessionCount ≥ 3.
        /// </summary>
        /// <param name="windows">
        /// The four windows returned by <see cref="Compute"/> or <see cref="AnalyzeAsync"/>.
        /// </param>
        /// <param name="userId">The user this profile belongs to.</param>
        /// <param name="generatedAt">The profile generation timestamp.</param>
        /// <param name="allPoints">
        /// The full point list from the 180-day fetch, used to pick the latest composite
        /// score. Pass an empty list or null when unavailable; the score falls back to 0.
        /// </param>
        public VoiceDevelopmentProfile BuildProfile(
            IReadOnlyList<TrendWindow> windows,
            int userId,
            DateTime generatedAt,
            IReadOnlyList<VoiceIntelligenceTrendPoint>? allPoints = null)
        {
            if (windows is null) throw new ArgumentNullException(nameof(windows));

            var weekly  = windows.Where(w => w.WindowDays <= 30).OrderBy(w => w.WindowDays).ToList();
            var monthly = windows.Where(w => w.WindowDays >  30).OrderBy(w => w.WindowDays).ToList();

            var hasEnoughData = windows.Any(w => w.HasEnoughData);

            double compositeScore = 0.0;
            if (allPoints != null && allPoints.Count > 0)
            {
                compositeScore = allPoints[allPoints.Count - 1].CompositeVoiceScore;
            }
            else if (windows.Count > 0)
            {
                // Fallback: use the CompositeMean of the smallest window that has data.
                var smallestWithData = windows
                    .OrderBy(w => w.WindowDays)
                    .FirstOrDefault(w => w.SessionCount > 0);
                if (smallestWithData != null)
                    compositeScore = smallestWithData.CompositeMean;
            }

            return new VoiceDevelopmentProfile
            {
                UserId           = userId,
                GeneratedAt      = generatedAt,
                WeeklyTrend      = weekly,
                MonthlyTrend     = monthly,
                Plateau          = null,
                Breakthrough     = null,
                Regression       = null,
                CompositeVoiceScore = compositeScore,
                HasEnoughData    = hasEnoughData,
            };
        }

        // ── Window builder ────────────────────────────────────────────────────────────

        private static TrendWindow BuildWindow(
            int windowDays,
            DateTime from,
            DateTime to,
            IReadOnlyList<VoiceIntelligenceTrendPoint> points)
        {
            if (points.Count == 0)
            {
                return new TrendWindow
                {
                    WindowDays      = windowDays,
                    From            = from,
                    To              = to,
                    DimensionSlopes = new Dictionary<VoiceDimension, double>(),
                    CompositeSlope  = 0.0,
                    CompositeMean   = 0.0,
                    CompositeMin    = 0.0,
                    CompositeMax    = 0.0,
                    SessionCount    = 0,
                    Confidence      = EmptyHistoryConfidence,
                    HasEnoughData   = false,
                };
            }

            // Per-dimension OLS slopes.
            var dimSlopes = new Dictionary<VoiceDimension, double>();
            foreach (var (dim, accessor) in DimensionAccessors)
            {
                var series = points.Select(accessor).ToList();
                dimSlopes[dim] = LinearSlope(series);
            }

            // Composite statistics.
            var composites = points.Select(p => p.CompositeVoiceScore).ToList();
            var compositeSlope = LinearSlope(composites);
            var compositeMean  = composites.Average();
            var compositeMin   = composites.Min();
            var compositeMax   = composites.Max();

            var confidence = BuildConfidence(points.Count, composites, compositeSlope);

            return new TrendWindow
            {
                WindowDays      = windowDays,
                From            = from,
                To              = to,
                DimensionSlopes = dimSlopes,
                CompositeSlope  = compositeSlope,
                CompositeMean   = compositeMean,
                CompositeMin    = compositeMin,
                CompositeMax    = compositeMax,
                SessionCount    = points.Count,
                Confidence      = confidence,
                HasEnoughData   = points.Count >= MinPointsForTrend,
            };
        }

        // ── Confidence (verbatim formula from LearningPathProfileBuilder.BuildConfidence) ──

        private static double BuildConfidence(
            int count,
            IReadOnlyList<double> composites,
            double compositeSlope)
        {
            if (count == 0)
                return EmptyHistoryConfidence;

            // Volume evidence (saturates at VolumeCap).
            var volume = Math.Min(count, VolumeCap) / (double)VolumeCap * VolumeWeight;

            // Trend evidence from the composite slope (only meaningful with ≥ MinPointsForTrend).
            double trendComponent;
            if (count >= MinPointsForTrend)
            {
                var normalised  = Math.Clamp(compositeSlope, -1.0, 1.0);
                var trend01     = (normalised + 1.0) / 2.0;
                trendComponent  = trend01 * TrendWeight;
            }
            else
            {
                trendComponent = 0.5 * TrendWeight;   // too short to judge ⇒ neutral half-weight
            }

            // Stability evidence: low composite volatility ⇒ steadier picture.
            var volatility        = StandardDeviation(composites);
            var normVolatility    = Math.Clamp(volatility / VolatilityNormaliser, 0.0, 1.0);
            var stabilityComponent = (1.0 - normVolatility) * StabilityWeight;

            var raw = EmptyHistoryConfidence * BaseRetention + volume + trendComponent + stabilityComponent;
            return Math.Clamp(raw, 0.0, 100.0);
        }

        // ── OLS slope — verbatim copy of LearningPathProfileBuilder.LinearSlope ───────
        // (same as ExerciseEffectivenessEngine.LinearSlope — one private copy per motor)

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

        // ── Standard deviation helper ────────────────────────────────────────────────

        private static double StandardDeviation(IReadOnlyList<double> values)
        {
            if (values.Count < 2) return 0.0;
            var mean  = values.Average();
            var sumSq = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSq / values.Count);
        }
    }
}
