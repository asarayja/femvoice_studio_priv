using System;
using System.Globalization;
using System.Text;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Coarse-grained recovery status derived from the recovery score (0–100).
    /// Higher score ⇒ better recovered. Ordered worst → best in numeric value so the
    /// enum can be compared, but the canonical mapping is the threshold table in
    /// <see cref="RecoveryScorer.ClassifyStatus"/>.
    /// </summary>
    public enum RecoveryStatus
    {
        /// <summary>Score &lt; 25 — sustained overload; rest is clinically indicated.</summary>
        Overtrained = 0,

        /// <summary>25 ≤ Score &lt; 50 — measurable strain; caution / lighter load.</summary>
        Strained = 1,

        /// <summary>50 ≤ Score &lt; 75 — workable but not fully rested.</summary>
        Adequate = 2,

        /// <summary>Score ≥ 75 — well rested, low recent load.</summary>
        WellRecovered = 3
    }

    /// <summary>
    /// Pure (side-effect-free) input snapshot for <see cref="RecoveryScorer"/>.
    /// Every field is directly derivable from <c>SessionAnalyticsStore</c> history
    /// (sessions + <c>HealthAnalyticsEvent</c>s) — this scorer never reads a DB itself,
    /// it only reasons over the numbers a caller has already aggregated.
    ///
    /// All counts are "recent" = a rolling ~7-day window unless the field name says
    /// otherwise. An all-zero/default instance represents a brand-new user with no
    /// history, which the scorer treats as well rested (neutral-high default).
    /// </summary>
    public readonly record struct RecoveryScoreInput
    {
        /// <summary>Sum of fatigue indicators over the recent (~7-day) window.</summary>
        public int RecentFatigueIndicators { get; init; }

        /// <summary>Sum of fatigue indicators over the PRIOR week (the 7 days before
        /// the recent window). Used only to detect a rising trend.</summary>
        public int PriorFatigueIndicators { get; init; }

        /// <summary>Recent strain periods (HealthAnalyticsEventType.StrainPeriod).</summary>
        public int RecentStrainEpisodes { get; init; }

        /// <summary>Recent safety freezes / locks (HealthAnalyticsEventType.SafetyFreeze).
        /// The single strongest negative signal — a hard clinical brake fired.</summary>
        public int RecentSafetyLocks { get; init; }

        /// <summary>Recent pause recommendations (HealthAnalyticsEventType.PauseRecommended).</summary>
        public int RecentPauseRecommendations { get; init; }

        /// <summary>Number of training sessions in the last 7 days (training density).</summary>
        public int SessionsLast7Days { get; init; }

        /// <summary>Wall-clock hours since the most recent session ended. Large values
        /// mean the voice has had time to rest; very small values combined with a high
        /// session count indicate overtraining.</summary>
        public double HoursSinceLastSession { get; init; }

        /// <summary>Recent hydration suggestions (HealthAnalyticsEventType.HydrationSuggested).
        /// A soft signal — dryness nudges, not strain.</summary>
        public int HydrationSuggestionsRecent { get; init; }
    }

    /// <summary>
    /// Result of a recovery evaluation: an explainable, traceable 0–100 score.
    /// </summary>
    public readonly record struct RecoveryResult
    {
        /// <summary>Recovery score, 0 (overtrained) … 100 (fully rested).</summary>
        public double Score { get; init; }

        /// <summary>Coarse status bucket derived from <see cref="Score"/>.</summary>
        public RecoveryStatus Status { get; init; }

        /// <summary>Short human-readable explanation of the dominant drivers — the
        /// "explainable" requirement of the dimension contract.</summary>
        public string Explanation { get; init; }
    }

    /// <summary>
    /// Computes a Recovery (restitution) score, 0–100, sitting high in the Health
    /// hierarchy (Health &gt; Comfort &gt; Resonance &gt; …). Recovery answers a single
    /// question: <em>how rested / unloaded is the voice right now?</em> High when the
    /// user is well recovered (little recent fatigue/strain, sane training density,
    /// good hydration); low under overload.
    ///
    /// ── DESIGN / WEIGHTS (all documented; deterministic, no IO) ──────────────────
    /// Start from a fully-rested 100 and subtract weighted penalties, then apply
    /// trend and overtraining adjustments, then clamp to [0,100].
    ///
    /// Per-unit penalties (each count is clamped at a cap so one runaway signal can't
    /// make the arithmetic meaningless, but safety locks are intentionally able to
    /// dominate the whole score):
    ///   • Safety locks          −18.0 each, capped contribution 90  (DOMINANT brake)
    ///   • Strain episodes        −9.0 each, capped contribution 45
    ///   • Pause recommendations  −6.0 each, capped contribution 30
    ///   • Fatigue indicators     −3.0 each, capped contribution 36
    ///   • Hydration suggestions  −1.5 each, capped contribution 12  (soft signal)
    ///
    /// Fatigue TREND: if RecentFatigueIndicators &gt; PriorFatigueIndicators the body
    /// is trending worse, so subtract an extra −2.5 per unit of (recent − prior),
    /// capped at −20. A flat or improving trend adds nothing (and never rewards).
    ///
    /// OVERTRAINING: high density with too little rest. When SessionsLast7Days &gt; 5
    /// AND HoursSinceLastSession &lt; 12, subtract a penalty that grows with the
    /// session surplus over 5 and shrinks toward 0 as rest approaches 12h:
    ///   overtrainPenalty = (sessions − 5) × 4.0 × restShortfallFactor,  cap −30
    ///   where restShortfallFactor = clamp((12 − hours) / 12, 0, 1).
    ///
    /// REST REWARD (restitution): rest buys back a bounded amount of any penalty.
    /// For every full 12h since the last session, add back +4 (e.g. 24h ⇒ +8),
    /// capped at +12, but the reward can never push above 100 (final clamp) and
    /// never resurrects a safety-locked session above its own cap-driven floor —
    /// it is applied before the clamp, so a deeply negative raw score stays low.
    ///
    /// Empty/zero input (new user) ⇒ no penalties, no trend, no overtraining, and
    /// HoursSinceLastSession defaults to 0 ⇒ no rest reward either ⇒ score stays at
    /// the neutral-high default of 100 (WellRecovered). A user with no history has
    /// nothing to recover from.
    /// </summary>
    public sealed class RecoveryScorer
    {
        // ── Per-unit penalty weights ───────────────────────────────────────────
        private const double SafetyLockPenalty = 18.0;
        private const double StrainPenalty = 9.0;
        private const double PausePenalty = 6.0;
        private const double FatiguePenalty = 3.0;
        private const double HydrationPenalty = 1.5;

        // ── Per-signal contribution caps ───────────────────────────────────────
        private const double SafetyLockCap = 90.0;
        private const double StrainCap = 45.0;
        private const double PauseCap = 30.0;
        private const double FatigueCap = 36.0;
        private const double HydrationCap = 12.0;

        // ── Fatigue trend ──────────────────────────────────────────────────────
        private const double TrendPenaltyPerUnit = 2.5;
        private const double TrendCap = 20.0;

        // ── Overtraining ───────────────────────────────────────────────────────
        private const int OvertrainSessionThreshold = 5;
        private const double OvertrainRestThresholdHours = 12.0;
        private const double OvertrainPenaltyPerExtraSession = 4.0;
        private const double OvertrainCap = 30.0;

        // ── Rest reward ────────────────────────────────────────────────────────
        private const double RestRewardPerBlock = 4.0;
        private const double RestRewardBlockHours = 12.0;
        private const double RestRewardCap = 12.0;

        // ── Status thresholds (inclusive lower bounds) ─────────────────────────
        private const double WellRecoveredThreshold = 75.0;
        private const double AdequateThreshold = 50.0;
        private const double StrainedThreshold = 25.0;

        /// <summary>
        /// Computes the recovery score for the supplied snapshot. Pure and total:
        /// any input (including all-zero, negative, NaN, ±∞) yields a clamped 0–100
        /// result. Negative counts and non-finite hours are sanitised to 0.
        /// </summary>
        public RecoveryResult Score(RecoveryScoreInput input)
        {
            // Sanitise — counts are non-negative; hours must be finite & non-negative.
            var safetyLocks = NonNeg(input.RecentSafetyLocks);
            var strain = NonNeg(input.RecentStrainEpisodes);
            var pauses = NonNeg(input.RecentPauseRecommendations);
            var fatigue = NonNeg(input.RecentFatigueIndicators);
            var priorFatigue = NonNeg(input.PriorFatigueIndicators);
            var hydration = NonNeg(input.HydrationSuggestionsRecent);
            var sessions = NonNeg(input.SessionsLast7Days);
            var hours = SanitiseHours(input.HoursSinceLastSession);

            // ── Weighted, capped penalties ─────────────────────────────────────
            var safetyPenalty = Math.Min(safetyLocks * SafetyLockPenalty, SafetyLockCap);
            var strainPenalty = Math.Min(strain * StrainPenalty, StrainCap);
            var pausePenalty = Math.Min(pauses * PausePenalty, PauseCap);
            var fatiguePenalty = Math.Min(fatigue * FatiguePenalty, FatigueCap);
            var hydrationPenalty = Math.Min(hydration * HydrationPenalty, HydrationCap);

            // ── Fatigue trend (only when worsening) ────────────────────────────
            var trendDelta = Math.Max(0, fatigue - priorFatigue);
            var trendPenalty = Math.Min(trendDelta * TrendPenaltyPerUnit, TrendCap);

            // ── Overtraining: density without rest ─────────────────────────────
            var overtrainPenalty = 0.0;
            var isOvertraining =
                sessions > OvertrainSessionThreshold && hours < OvertrainRestThresholdHours;
            if (isOvertraining)
            {
                var restShortfall =
                    Math.Clamp((OvertrainRestThresholdHours - hours) / OvertrainRestThresholdHours, 0.0, 1.0);
                overtrainPenalty = Math.Min(
                    (sessions - OvertrainSessionThreshold) * OvertrainPenaltyPerExtraSession * restShortfall,
                    OvertrainCap);
            }

            // ── Rest reward (restitution) ──────────────────────────────────────
            var restReward = Math.Min(
                Math.Floor(hours / RestRewardBlockHours) * RestRewardPerBlock,
                RestRewardCap);

            var raw =
                100.0
                - safetyPenalty
                - strainPenalty
                - pausePenalty
                - fatiguePenalty
                - hydrationPenalty
                - trendPenalty
                - overtrainPenalty
                + restReward;

            var score = Math.Clamp(raw, 0.0, 100.0);
            var status = ClassifyStatus(score);
            var explanation = BuildExplanation(
                score, status,
                safetyLocks, strain, pauses, fatigue, hydration,
                trendDelta, isOvertraining, restReward);

            return new RecoveryResult
            {
                Score = score,
                Status = status,
                Explanation = explanation
            };
        }

        /// <summary>
        /// Maps a 0–100 score to its coarse status bucket. Boundaries are inclusive
        /// lower bounds: 75 ⇒ WellRecovered, 50 ⇒ Adequate, 25 ⇒ Strained, &lt;25 ⇒
        /// Overtrained.
        /// </summary>
        public static RecoveryStatus ClassifyStatus(double score)
        {
            if (score >= WellRecoveredThreshold) return RecoveryStatus.WellRecovered;
            if (score >= AdequateThreshold) return RecoveryStatus.Adequate;
            if (score >= StrainedThreshold) return RecoveryStatus.Strained;
            return RecoveryStatus.Overtrained;
        }

        private static string BuildExplanation(
            double score,
            RecoveryStatus status,
            int safetyLocks,
            int strain,
            int pauses,
            int fatigue,
            int hydration,
            int trendDelta,
            bool isOvertraining,
            double restReward)
        {
            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture, $"Recovery {score:0} ({StatusLabel(status)}). ");

            var noLoad =
                safetyLocks == 0 && strain == 0 && pauses == 0 &&
                fatigue == 0 && hydration == 0 && trendDelta == 0 && !isOvertraining;

            if (noLoad)
            {
                sb.Append("No recent fatigue, strain or safety events — voice is well rested.");
                return sb.ToString();
            }

            var drivers = new System.Collections.Generic.List<string>();
            if (safetyLocks > 0)
                drivers.Add($"{safetyLocks} safety lock{Plural(safetyLocks)} (dominant)");
            if (strain > 0)
                drivers.Add($"{strain} strain episode{Plural(strain)}");
            if (pauses > 0)
                drivers.Add($"{pauses} pause recommendation{Plural(pauses)}");
            if (fatigue > 0)
                drivers.Add($"{fatigue} fatigue indicator{Plural(fatigue)}");
            if (trendDelta > 0)
                drivers.Add($"rising fatigue trend (+{trendDelta})");
            if (isOvertraining)
                drivers.Add("high training density with little rest");
            if (hydration > 0)
                drivers.Add($"{hydration} hydration nudge{Plural(hydration)}");

            sb.Append("Lowered by: ");
            sb.Append(string.Join(", ", drivers));
            sb.Append('.');

            if (restReward > 0)
                sb.Append(CultureInfo.InvariantCulture, $" Rest since last session recovered +{restReward:0}.");

            return sb.ToString();
        }

        private static string StatusLabel(RecoveryStatus status) => status switch
        {
            RecoveryStatus.WellRecovered => "well recovered",
            RecoveryStatus.Adequate => "adequate",
            RecoveryStatus.Strained => "strained",
            RecoveryStatus.Overtrained => "overtrained",
            _ => "unknown"
        };

        private static string Plural(int count) => count == 1 ? string.Empty : "s";

        private static int NonNeg(int value) => value < 0 ? 0 : value;

        private static double SanitiseHours(double hours)
        {
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0)
                return 0.0;
            return hours;
        }
    }
}
