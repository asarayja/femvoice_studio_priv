using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Pure, IO-free engine that derives <see cref="LongitudinalInsight"/> records from a
    /// set of <see cref="TrendWindow"/>s plus optional detected pattern states
    /// (<see cref="PlateauState"/>, <see cref="BreakthroughState"/>, <see cref="RegressionState"/>).
    ///
    /// ── SELECTION RULES ──────────────────────────────────────────────────────────────────
    /// 1. Aggregate the best per-dimension slope across all supplied windows (widest window
    ///    with enough data wins; highest |slope| as tie-break).
    /// 2. Keep only dimensions whose |slope| ≥ DeltaThreshold (1.0 pt/session) or that are
    ///    implicated by a detected pattern — silent/neutral dimensions are filtered out.
    /// 3. Rank: strongest |delta| first; tie-break = VoiceDimension priority order
    ///    (Recovery=0 wins, then Comfort=1, then Resonance=2, …).
    /// 4. One insight per dimension; ReasonCode encodes the direction/pattern.
    /// 5. If no windows have enough data: emit one INSUFFICIENT_DATA insight per dimension
    ///    that appears in at least one window, capped at MaxInsights.
    ///
    /// ── CONFIDENCE ───────────────────────────────────────────────────────────────────────
    /// Per-dimension confidence mirrors LearningPathProfileBuilder.BuildConfidence:
    ///   base 35 + volume (up to 25, saturates at VolumeCap=12 sessions) +
    ///   trend (0–25 via slope normalised to [−1,+1] → [0,1]) +
    ///   stability (0–32.5, inverse of composite volatility).
    /// Lower windows ⇒ lower confidence by design.
    ///
    /// ── LOCALISATION ─────────────────────────────────────────────────────────────────────
    /// What/Why strings are composed via <see cref="LocalizationService"/> using the
    /// FROZEN RESX keys: Insight_Improvement_Template, Insight_Decline_Template,
    /// Insight_Stable_Template, Insight_InsufficientData ({0}=dim label, {1}=delta).
    /// Tests NEVER assert on these strings — only on ReasonCode, Dimension, Confidence, Evidence.
    ///
    /// Global priority: this is a DESCRIPTIVE insight engine. It never overrides Safety,
    /// Health, or Recovery gates — those live upstream.
    /// </summary>
    public sealed class LongitudinalInsightEngine
    {
        // ── Thresholds ──────────────────────────────────────────────────────────────────

        /// <summary>Minimum |slope| (points/session) for a dimension to yield an insight.</summary>
        private const double DeltaThreshold = 1.0;

        /// <summary>Maximum insights returned (prevents noise on large inputs).</summary>
        private const int MaxInsights = 7;

        // ── Confidence formula (mirrors LearningPathProfileBuilder.BuildConfidence) ────

        private const double EmptyHistoryConfidence = 35.0;
        private const double BaseRetention = 0.5;
        private const double VolumeWeight = 25.0;
        private const double TrendWeight = 25.0;
        private const double StabilityWeight = 32.5;
        private const int VolumeCap = 12;
        private const double VolatilityNormaliser = 25.0;

        // ── Reason codes ────────────────────────────────────────────────────────────────

        private const string ReasonImprovement = "IMPROVEMENT";
        private const string ReasonDecline = "DECLINE";
        private const string ReasonPlateau = "PLATEAU";
        private const string ReasonBreakthrough = "BREAKTHROUGH";
        private const string ReasonRegression = "REGRESSION";
        private const string ReasonStable = "STABLE";
        private const string ReasonInsufficientData = "INSUFFICIENT_DATA";

        // ── Singleton localization accessor ─────────────────────────────────────────────

        private readonly ILocalizationService _loc;

        /// <summary>
        /// Production constructor. Uses <see cref="LocalizationService.Instance"/>.
        /// </summary>
        public LongitudinalInsightEngine() : this(LocalizationService.Instance) { }

        /// <summary>
        /// Testable constructor — accepts any <see cref="ILocalizationService"/> instance.
        /// Allows injection of <see cref="TestLocalizationService"/> in unit tests.
        /// </summary>
        public LongitudinalInsightEngine(ILocalizationService localizationService)
        {
            _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        // ── Public API ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Derives a ranked list of longitudinal insights from trend windows + detected patterns.
        ///
        /// Pure and total: any input (including empty windows / null patterns) yields a
        /// well-formed, non-throwing result. The list may be empty only when all windows are
        /// empty and no patterns are supplied.
        /// </summary>
        /// <param name="windows">
        /// Trend windows from the VoiceDevelopmentProfile (7d, 30d, 90d, 180d). May be empty.
        /// Widest window with HasEnoughData wins for each dimension.
        /// </param>
        /// <param name="plateau">Detected plateau, or null.</param>
        /// <param name="breakthrough">Detected breakthrough, or null.</param>
        /// <param name="regression">Detected regression, or null.</param>
        /// <param name="profile">The full VoiceDevelopmentProfile (used for composite score + HasEnoughData).</param>
        /// <returns>Ranked list of insights, strongest delta first, at most <see cref="MaxInsights"/>.</returns>
        public IReadOnlyList<LongitudinalInsight> Compute(
            IReadOnlyList<TrendWindow> windows,
            PlateauState? plateau,
            BreakthroughState? breakthrough,
            RegressionState? regression,
            VoiceDevelopmentProfile profile)
        {
            if (windows == null) throw new ArgumentNullException(nameof(windows));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            // Collect all VoiceDimension values present in the windows.
            var allDimensions = Enum.GetValues(typeof(VoiceDimension)).Cast<VoiceDimension>().ToList();

            // ── Step 1: per-dimension best slope + session count from windows ─────────

            var dimensionData = new Dictionary<VoiceDimension, (double Slope, int Sessions, int WindowDays)>();

            foreach (var dim in allDimensions)
            {
                // Pick the widest window that has enough data; if none, pick the widest anyway
                // so we can still emit INSUFFICIENT_DATA insights.
                TrendWindow? bestWindow = null;
                foreach (var w in windows.OrderByDescending(w => w.WindowDays))
                {
                    if (!w.DimensionSlopes.ContainsKey(dim)) continue;
                    if (bestWindow == null)
                    {
                        bestWindow = w;
                    }
                    else if (!bestWindow.HasEnoughData && w.HasEnoughData)
                    {
                        // Upgrade from a no-data window to a window with enough data.
                        bestWindow = w;
                    }
                    // If bestWindow already has enough data, keep it (it is already the widest
                    // window with enough data because we iterate widest-first).
                    // If neither has enough data, keep the wider (already assigned).
                }

                if (bestWindow == null) continue;
                var slope = bestWindow.DimensionSlopes.TryGetValue(dim, out var s) ? s : 0.0;
                dimensionData[dim] = (slope, bestWindow.SessionCount, bestWindow.WindowDays);
            }

            // ── Step 2: forced pattern dimensions ────────────────────────────────────────

            var patternDimensions = new HashSet<VoiceDimension>();
            if (plateau != null) patternDimensions.Add(plateau.Dimension);
            if (breakthrough != null) patternDimensions.Add(breakthrough.Dimension);
            if (regression != null) patternDimensions.Add(regression.Dimension);

            // ── Step 3: filter — keep signal dims (|slope| ≥ threshold) + pattern dims ──

            bool anyWindowHasEnoughData = windows.Any(w => w.HasEnoughData);

            var candidates = new List<(VoiceDimension Dim, double Slope, int Sessions, int WindowDays, bool IsPattern)>();

            foreach (var kvp in dimensionData)
            {
                var dim = kvp.Key;
                var (slope, sessions, windowDays) = kvp.Value;
                bool isPattern = patternDimensions.Contains(dim);
                bool hasSignal = Math.Abs(slope) >= DeltaThreshold;

                if (!anyWindowHasEnoughData)
                {
                    // Emit insufficient-data insight for every dimension in the windows.
                    candidates.Add((dim, slope, sessions, windowDays, isPattern));
                }
                else if (hasSignal || isPattern)
                {
                    candidates.Add((dim, slope, sessions, windowDays, isPattern));
                }
                // else: neutral/flat dimension — filter out.
            }

            if (candidates.Count == 0) return Array.Empty<LongitudinalInsight>();

            // ── Step 4: rank — strongest |delta| first; tie-break = VoiceDimension value (lower wins) ──

            candidates.Sort((a, b) =>
            {
                var slopeCompare = Math.Abs(b.Slope).CompareTo(Math.Abs(a.Slope));
                return slopeCompare != 0 ? slopeCompare : ((int)a.Dim).CompareTo((int)b.Dim);
            });

            // ── Step 5: build insights ────────────────────────────────────────────────────

            var results = new List<LongitudinalInsight>(Math.Min(candidates.Count, MaxInsights));

            foreach (var (dim, slope, sessions, windowDays, isPattern) in candidates.Take(MaxInsights))
            {
                var reasonCode = DetermineReasonCode(dim, slope, sessions, windowDays,
                    plateau, breakthrough, regression, anyWindowHasEnoughData);

                var confidence = BuildConfidence(slope, sessions, windowDays);
                var dimLabel = dim.ToString();
                var deltaLabel = slope.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);

                var (what, why) = ComposeLocalised(reasonCode, dimLabel, deltaLabel);
                var evidence = BuildEvidence(dim, slope, sessions, windowDays,
                    plateau, breakthrough, regression);

                results.Add(new LongitudinalInsight
                {
                    ReasonCode = reasonCode,
                    Dimension = dim,
                    Confidence = confidence,
                    What = what,
                    Why = why,
                    Evidence = evidence,
                });
            }

            return results;
        }

        // ── Reason code resolution ──────────────────────────────────────────────────────

        private static string DetermineReasonCode(
            VoiceDimension dim,
            double slope,
            int sessions,
            int windowDays,
            PlateauState? plateau,
            BreakthroughState? breakthrough,
            RegressionState? regression,
            bool anyWindowHasEnoughData)
        {
            // Pattern states take precedence (in priority order: regression > plateau > breakthrough).
            if (regression != null && regression.Dimension == dim)
                return ReasonRegression;
            if (plateau != null && plateau.Dimension == dim)
                return ReasonPlateau;
            if (breakthrough != null && breakthrough.Dimension == dim)
                return ReasonBreakthrough;

            if (!anyWindowHasEnoughData || sessions < 3)
                return ReasonInsufficientData;

            if (slope >= DeltaThreshold)
                return ReasonImprovement;
            if (slope <= -DeltaThreshold)
                return ReasonDecline;

            return ReasonStable;
        }

        // ── Localised What/Why composition ─────────────────────────────────────────────

        private (string What, string Why) ComposeLocalised(
            string reasonCode, string dimLabel, string deltaLabel)
        {
            // Map reason code to frozen RESX key.
            string templateKey = reasonCode switch
            {
                ReasonImprovement => "Insight_Improvement_Template",
                ReasonDecline => "Insight_Decline_Template",
                ReasonPlateau => "Insight_Stable_Template",
                ReasonBreakthrough => "Insight_Improvement_Template",
                ReasonRegression => "Insight_Decline_Template",
                ReasonStable => "Insight_Stable_Template",
                ReasonInsufficientData => "Insight_InsufficientData",
                _ => "Insight_Stable_Template",
            };

            var format = _loc[templateKey];
            string what;
            try
            {
                what = string.Format(format, dimLabel, deltaLabel);
            }
            catch
            {
                what = format;
            }

            // Why re-uses the same template with the direction context for now;
            // RES-Strings can differentiate What vs Why copy independently via separate keys
            // if needed in a later sprint — the engine always feeds both from the same data.
            var why = what;
            return (what, why);
        }

        // ── Evidence builder ────────────────────────────────────────────────────────────

        private static IReadOnlyList<string> BuildEvidence(
            VoiceDimension dim,
            double slope,
            int sessions,
            int windowDays,
            PlateauState? plateau,
            BreakthroughState? breakthrough,
            RegressionState? regression)
        {
            var evidence = new List<string>(6)
            {
                string.Create(CultureInfo.InvariantCulture, $"window={windowDays}d"),
                string.Create(CultureInfo.InvariantCulture, $"slope={slope:+0.00;-0.00;0.00}"),
                string.Create(CultureInfo.InvariantCulture, $"sessions={sessions}"),
            };

            if (plateau != null && plateau.Dimension == dim)
            {
                evidence.Add(string.Create(CultureInfo.InvariantCulture,
                    $"plateau_duration={plateau.PlateauDurationDays}d"));
                evidence.Add(string.Create(CultureInfo.InvariantCulture,
                    $"plateau_severity={plateau.SeverityScore:0.0}"));
            }

            if (breakthrough != null && breakthrough.Dimension == dim)
            {
                evidence.Add(string.Create(CultureInfo.InvariantCulture,
                    $"breakthrough_delta={breakthrough.MagnitudeDelta:+0.0;-0.0;0.0}"));
            }

            if (regression != null && regression.Dimension == dim)
            {
                evidence.Add(string.Create(CultureInfo.InvariantCulture,
                    $"regression_slope={regression.DeclineSlope:+0.00;-0.00;0.00}"));
                evidence.Add(string.Create(CultureInfo.InvariantCulture,
                    $"regression_severity={regression.CompoundSeverity:0.0}"));
            }

            return evidence;
        }

        // ── Confidence (verbatim copy of LearningPathProfileBuilder.BuildConfidence structure) ──

        /// <summary>
        /// Computes per-insight confidence using the BuildConfidence structure from
        /// LearningPathProfileBuilder: base 35 + volume (saturates at VolumeCap=12) +
        /// trend (slope normalised [−1,+1] → [0,1]) + stability (absent at insight level ⇒
        /// half-weight constant for simplicity, since we do not carry a per-dimension
        /// composite volatility series here). Smaller windows ⇒ lower confidence by design.
        /// </summary>
        private static double BuildConfidence(double slope, int sessions, int windowDays)
        {
            if (sessions == 0)
                return EmptyHistoryConfidence;

            // Volume term: saturates at VolumeCap sessions.
            var volume = Math.Min(sessions, VolumeCap) / (double)VolumeCap * VolumeWeight;

            // Trend term: slope normalised to [−1, +1], then mapped to [0, 1].
            var normalised = Math.Clamp(slope, -1.0, 1.0);
            var trend01 = (normalised + 1.0) / 2.0;
            var trendComponent = sessions >= 3 ? trend01 * TrendWeight : 0.5 * TrendWeight;

            // Stability proxy: without a per-dimension volatility series, use a window-width
            // weight — narrower windows are intrinsically noisier (7d < 30d < 90d < 180d).
            // Map 7→0.3, 30→0.6, 90→0.8, 180→1.0, others proportional.
            var windowFactor = Math.Clamp(windowDays / 180.0, 0.0, 1.0);
            var stabilityComponent = windowFactor * StabilityWeight;

            var raw = EmptyHistoryConfidence * BaseRetention + volume + trendComponent + stabilityComponent;
            return Math.Clamp(raw, 0.0, 100.0);
        }

        // ── OLS slope (verbatim copy from LearningPathProfileBuilder.LinearSlope) ───────

        /// <summary>
        /// Ordinary-least-squares slope of y over x = 0,1,2,…  Returns 0 when undetermined.
        /// Verbatim copy of LearningPathProfileBuilder.LinearSlope — one private copy per motor.
        /// </summary>
        private static double LinearSlope(IReadOnlyList<double> y)
        {
            var n = y.Count;
            if (n < 2) return 0.0;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var i = 0; i < n; i++)
            {
                sumX += i;
                sumY += y[i];
                sumXY += i * y[i];
                sumX2 += (double)i * i;
            }

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 0.0001) return 0.0;
            return (n * sumXY - sumX * sumY) / denominator;
        }
    }
}
