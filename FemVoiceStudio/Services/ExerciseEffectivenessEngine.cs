using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// EXERCISE EFFECTIVENESS INTELLIGENCE (Sprint C.2, Agent EFF — Agents 3+4+5+6).
    ///
    /// Builds a per-exercise <see cref="ExerciseEffectivenessProfile"/> from persisted
    /// analytics history, ranks the catalog by effectiveness, and flags taxing or
    /// comfort-eroding exercises. Follows the <see cref="MasteryEvaluator"/> shape: it
    /// takes a <see cref="SessionAnalyticsStore"/> in the constructor for the store-reading
    /// overloads, and exposes PURE Compute* overloads that take already-fetched series so
    /// every numeric behaviour is unit-testable without a database (no mocking).
    ///
    /// ── HONEST PROVENANCE (read this before trusting a "Gain") ───────────────────────
    /// There is NO real before/after measurement in the data. Each *Gain is the
    /// ordinary-least-squares SLOPE of a per-session metric over the exercise's own
    /// chronological trend (metric-points per session step). A positive slope means the
    /// metric rose while the user practised the exercise — an explainable proxy, NOT a
    /// causal claim. With fewer than <see cref="MinSessionsForTrust"/> sessions,
    /// <see cref="ExerciseEffectivenessProfile.HasEnoughData"/> is false so the result is
    /// read as "insufficient evidence", never "ineffective".
    ///
    /// ── THRESHOLDS / FORMULAS (deterministic, documented) ────────────────────────────
    /// • ResonanceGain   = OLS slope of (ResonanceQualityIndex × 100) over the trend.
    /// • ConsistencyGain = OLS slope of (StabilityConsistency × 100) over the trend.
    /// • ComfortGain     = OLS slope of ComfortScore100 over the sessions that ran this
    ///                     exercise, joined per SessionId from the VI trend (one session =
    ///                     one exercise). No joinable comfort point ⇒ 0 + HasComfortData=false.
    /// • RecoveryCost (0–100) = clamp( FatigueWeight·avgFatiguePerSession
    ///                                 + SafetyWeight·avgSafetyPerSession
    ///                                 + StrainPauseWeight·avgStrainPausePerSession , 0, 100),
    ///   where the averages are per session of THIS exercise and the strain/pause density
    ///   counts StrainPeriod + PauseRecommended health events whose SessionId belongs to the
    ///   exercise. Higher ⇒ more taxing.
    /// • UserSuccessRate (0–100) = share of sessions clearing the success gate:
    ///   HoldCompletionRate ≥ <see cref="SuccessHoldThreshold"/> AND
    ///   ResonanceQualityIndex ≥ <see cref="SuccessResonanceThreshold"/>.
    /// • CompositeEffectiveness (0–100, RANKING ONLY) = clamp(
    ///       NeutralMidpoint
    ///     + GainSpan·(ResonanceGain + ComfortGain + ConsistencyGain)
    ///     − CostSpan·(RecoveryCost − NeutralCost)/100 , 0, 100).
    ///   A profile without enough data sits exactly at NeutralMidpoint so it never out- or
    ///   under-ranks an evidenced exercise.
    ///
    /// ── SAFETY FLAGGING (Agent 6) — DATA, NOT A GATE ─────────────────────────────────
    /// <see cref="FlagConcerns"/> marks an exercise when RecoveryCost ≥
    /// <see cref="HighRecoveryCostThreshold"/>, average fatigue ≥
    /// <see cref="HighFatigueThreshold"/>, or ComfortGain ≤ −<see cref="ComfortDeclineThreshold"/>.
    /// A flag means "de-prioritise in recommendations"; it never blocks training — the
    /// ProgressionSafetyGate and health-gates remain the sole authorities for that.
    /// </summary>
    public sealed class ExerciseEffectivenessEngine
    {
        // ── The REAL catalog: ExerciseId 1–15. 16–50 are phantom (no rows) and are
        //    deliberately excluded from EvaluateAllAsync / ranking. ────────────────────
        public const int CatalogFirstExerciseId = 1;
        public const int CatalogLastExerciseId = 15;

        /// <summary>Default analytics look-back window for the store-reading overloads.</summary>
        public const int DefaultLookbackDays = 90;

        /// <summary>Minimum sessions before the slopes are trusted (else HasEnoughData=false).</summary>
        public const int MinSessionsForTrust = 4;

        // ── Recovery-cost weighting (per-session averages → 0–100). ──────────────────
        public const double FatigueWeight = 18.0;
        public const double SafetyWeight = 35.0;
        public const double StrainPauseWeight = 14.0;

        // ── Success gate (mirrors the MasteryEvaluator gate shape). ──────────────────
        public const double SuccessHoldThreshold = 0.70;
        public const double SuccessResonanceThreshold = 0.50;

        // ── Composite (ranking-only) anchoring. ──────────────────────────────────────
        public const double NeutralMidpoint = 50.0;
        public const double GainSpan = 6.0;     // points of composite per point/session of gain
        public const double CostSpan = 30.0;    // composite penalty span over the cost range
        public const double NeutralCost = 15.0; // a "normal" recovery cost that is not penalised

        // ── Safety-flag thresholds (Agent 6). ────────────────────────────────────────
        public const double HighRecoveryCostThreshold = 60.0;
        public const double HighFatigueThreshold = 2.0;       // avg fatigue indicators per session
        public const double ComfortDeclineThreshold = 1.5;    // comfort slope ≤ −this is a decline

        private readonly SessionAnalyticsStore _analyticsStore;

        public ExerciseEffectivenessEngine(SessionAnalyticsStore analyticsStore)
            => _analyticsStore = analyticsStore ?? throw new ArgumentNullException(nameof(analyticsStore));

        // ─────────────────────────────────────────────────────────────────────────
        // STORE-READING overloads (Agent 3 + 4). Thin: read history, then Compute.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates a single exercise from persisted history as of <paramref name="now"/>.
        /// Reads the exercise trend (resonance/stability/fatigue/safety per session), the
        /// Voice Intelligence trend (for comfort, joined per SessionId), and the health
        /// events (for strain/pause density), then delegates to the pure
        /// <see cref="Compute(int, IReadOnlyList{ExercisePerformanceSummary}, IReadOnlyList{VoiceIntelligenceTrendPoint}, IReadOnlyList{HealthAnalyticsEvent})"/>.
        /// </summary>
        public async Task<ExerciseEffectivenessProfile> EvaluateAsync(
            int exerciseId,
            DateTime now,
            int userId = 1,
            int lookbackDays = DefaultLookbackDays,
            CancellationToken cancellationToken = default)
        {
            var from = now.AddDays(-Math.Max(1, lookbackDays));
            var to = now.AddTicks(1);

            var trend = await _analyticsStore
                .GetExerciseTrendAsync(exerciseId, from, to, userId, cancellationToken)
                .ConfigureAwait(false);
            var voiceTrend = await _analyticsStore
                .GetVoiceIntelligenceTrendAsync(from, to, userId, cancellationToken)
                .ConfigureAwait(false);
            var healthEvents = await _analyticsStore
                .GetHealthEventsAsync(from, to, userId, cancellationToken)
                .ConfigureAwait(false);

            return Compute(exerciseId, trend, voiceTrend, healthEvents);
        }

        /// <summary>
        /// Evaluates every REAL catalog exercise (1–15 — NOT the phantom 16–50). Reads the
        /// window once and partitions per exercise, so the database is touched a constant
        /// number of times regardless of catalog size.
        /// </summary>
        public async Task<IReadOnlyList<ExerciseEffectivenessProfile>> EvaluateAllAsync(
            DateTime now,
            int userId = 1,
            int lookbackDays = DefaultLookbackDays,
            CancellationToken cancellationToken = default)
        {
            var from = now.AddDays(-Math.Max(1, lookbackDays));
            var to = now.AddTicks(1);

            var allSummaries = await _analyticsStore
                .GetExerciseSummariesAsync(from, to, userId, cancellationToken)
                .ConfigureAwait(false);
            var voiceTrend = await _analyticsStore
                .GetVoiceIntelligenceTrendAsync(from, to, userId, cancellationToken)
                .ConfigureAwait(false);
            var healthEvents = await _analyticsStore
                .GetHealthEventsAsync(from, to, userId, cancellationToken)
                .ConfigureAwait(false);

            var byExercise = allSummaries
                .GroupBy(s => s.ExerciseId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<ExercisePerformanceSummary>)g
                    .OrderBy(s => s.StartedAt)
                    .ToList());

            var profiles = new List<ExerciseEffectivenessProfile>(
                CatalogLastExerciseId - CatalogFirstExerciseId + 1);
            for (var id = CatalogFirstExerciseId; id <= CatalogLastExerciseId; id++)
            {
                var trend = byExercise.TryGetValue(id, out var rows)
                    ? rows
                    : Array.Empty<ExercisePerformanceSummary>();
                profiles.Add(Compute(id, trend, voiceTrend, healthEvents));
            }

            return profiles;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PURE compute (no IO) — every numeric behaviour is tested against this.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the profile from already-fetched series. <paramref name="trend"/> is the
        /// chronological per-session history of THIS exercise; <paramref name="voiceTrend"/>
        /// is the full Voice Intelligence trend (comfort is joined per SessionId);
        /// <paramref name="healthEvents"/> supplies strain/pause density. Pure and total:
        /// any input (including empty) yields a well-formed, clamped profile.
        /// </summary>
        public ExerciseEffectivenessProfile Compute(
            int exerciseId,
            IReadOnlyList<ExercisePerformanceSummary> trend,
            IReadOnlyList<VoiceIntelligenceTrendPoint>? voiceTrend = null,
            IReadOnlyList<HealthAnalyticsEvent>? healthEvents = null)
        {
            var ordered = (trend ?? Array.Empty<ExercisePerformanceSummary>())
                .Where(s => s.ExerciseId == exerciseId)
                .OrderBy(s => s.StartedAt)
                .ToList();
            var voice = voiceTrend ?? Array.Empty<VoiceIntelligenceTrendPoint>();
            var events = healthEvents ?? Array.Empty<HealthAnalyticsEvent>();

            var sessionCount = ordered.Count;
            var hasEnough = sessionCount >= MinSessionsForTrust;

            if (sessionCount == 0)
            {
                return new ExerciseEffectivenessProfile
                {
                    ExerciseId = exerciseId,
                    HasEnoughData = false,
                    HasComfortData = false,
                    CompositeEffectiveness = NeutralMidpoint,
                    Explanation = string.Create(CultureInfo.InvariantCulture,
                        $"Exercise {exerciseId}: no recorded sessions yet — no effectiveness evidence.")
                };
            }

            // ── Gains: OLS slope of each per-session metric (×100 for 0–1 metrics). ──
            var resonanceSeries = ordered.Select(s => s.ResonanceQualityIndex * 100.0).ToList();
            var consistencySeries = ordered.Select(s => s.StabilityConsistency * 100.0).ToList();
            var resonanceGain = LinearSlope(resonanceSeries);
            var consistencyGain = LinearSlope(consistencySeries);

            // ── Comfort: join VI ComfortScore100 per SessionId (one session = one exercise). ──
            var sessionIds = ordered.Select(s => s.SessionId).ToHashSet();
            var comfortSeries = voice
                .Where(p => sessionIds.Contains(p.SessionId))
                .OrderBy(p => p.StartedAt)
                .Select(p => p.ComfortScore100)
                .ToList();
            var hasComfortData = comfortSeries.Count > 0;
            var comfortGain = LinearSlope(comfortSeries);

            // ── Recovery cost: per-session fatigue + safety + strain/pause density. ──
            var avgFatigue = ordered.Average(s => Math.Max(0, s.FatigueIndicators));
            var avgSafety = ordered.Average(s => Math.Max(0, s.SafetyEventsCount));
            var strainPauseCount = events.Count(e =>
                sessionIds.Contains(e.SessionId)
                && (e.EventType == HealthAnalyticsEventType.StrainPeriod
                    || e.EventType == HealthAnalyticsEventType.PauseRecommended));
            var avgStrainPause = strainPauseCount / (double)sessionCount;
            var recoveryCost = Math.Clamp(
                FatigueWeight * avgFatigue
                + SafetyWeight * avgSafety
                + StrainPauseWeight * avgStrainPause,
                0.0, 100.0);

            // ── Success rate: share of sessions clearing the success gate. ──
            var successes = ordered.Count(s =>
                s.HoldCompletionRate >= SuccessHoldThreshold
                && s.ResonanceQualityIndex >= SuccessResonanceThreshold);
            var successRate = successes / (double)sessionCount * 100.0;

            // ── Composite (ranking only). No-data profiles never reach here. ──
            var composite = hasEnough
                ? Math.Clamp(
                    NeutralMidpoint
                    + GainSpan * (resonanceGain + comfortGain + consistencyGain)
                    - CostSpan * (recoveryCost - NeutralCost) / 100.0,
                    0.0, 100.0)
                : NeutralMidpoint;

            var explanation = BuildExplanation(
                exerciseId, sessionCount, hasEnough, resonanceGain, comfortGain,
                hasComfortData, consistencyGain, recoveryCost, successRate);

            return new ExerciseEffectivenessProfile
            {
                ExerciseId = exerciseId,
                ResonanceGain = Round2(resonanceGain),
                ComfortGain = Round2(comfortGain),
                HasComfortData = hasComfortData,
                ConsistencyGain = Round2(consistencyGain),
                RecoveryCost = Round2(recoveryCost),
                UserSuccessRate = Round2(successRate),
                SessionCount = sessionCount,
                HasEnoughData = hasEnough,
                CompositeEffectiveness = Round2(composite),
                Explanation = explanation
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // RANKING (Agent 5). All operate on a set of already-computed profiles.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Most effective first (highest <see cref="ExerciseEffectivenessProfile.CompositeEffectiveness"/>).
        /// Profiles without enough data are dropped by default so the ranking never elevates
        /// an under-evidenced exercise; pass <paramref name="includeLowData"/> to keep them.
        /// Ties break by higher success rate, then lower recovery cost, then ExerciseId.
        /// </summary>
        public IReadOnlyList<ExerciseEffectivenessProfile> RankMostEffective(
            IEnumerable<ExerciseEffectivenessProfile> profiles, bool includeLowData = false)
            => Filter(profiles, includeLowData)
                .OrderByDescending(p => p.CompositeEffectiveness)
                .ThenByDescending(p => p.UserSuccessRate)
                .ThenBy(p => p.RecoveryCost)
                .ThenBy(p => p.ExerciseId)
                .ToList();

        /// <summary>Least effective first (lowest composite). Same filtering/tie rules,
        /// reversed primary ordering.</summary>
        public IReadOnlyList<ExerciseEffectivenessProfile> RankLeastEffective(
            IEnumerable<ExerciseEffectivenessProfile> profiles, bool includeLowData = false)
            => Filter(profiles, includeLowData)
                .OrderBy(p => p.CompositeEffectiveness)
                .ThenBy(p => p.UserSuccessRate)
                .ThenByDescending(p => p.RecoveryCost)
                .ThenBy(p => p.ExerciseId)
                .ToList();

        /// <summary>Highest recovery cost (most taxing) first.</summary>
        public IReadOnlyList<ExerciseEffectivenessProfile> RankByRecoveryCost(
            IEnumerable<ExerciseEffectivenessProfile> profiles, bool includeLowData = false)
            => Filter(profiles, includeLowData)
                .OrderByDescending(p => p.RecoveryCost)
                .ThenBy(p => p.ExerciseId)
                .ToList();

        /// <summary>Highest resonance gain first.</summary>
        public IReadOnlyList<ExerciseEffectivenessProfile> RankByResonanceGain(
            IEnumerable<ExerciseEffectivenessProfile> profiles, bool includeLowData = false)
            => Filter(profiles, includeLowData)
                .OrderByDescending(p => p.ResonanceGain)
                .ThenBy(p => p.ExerciseId)
                .ToList();

        // ─────────────────────────────────────────────────────────────────────────
        // SAFETY FLAGGING (Agent 6) — DATA insight, never a gate.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Flags exercises whose recent history is taxing or comfort-eroding, so a
        /// recommender can DE-PRIORITISE them. A flag is NOT a safety block — the
        /// ProgressionSafetyGate / health-gates remain the only authorities that stop
        /// training. Only data-bearing profiles are considered (no-data exercises are never
        /// flagged, to avoid false alarms). One exercise may raise multiple flags.
        /// </summary>
        public IReadOnlyList<ExerciseEffectivenessFlag> FlagConcerns(
            IEnumerable<ExerciseEffectivenessProfile> profiles)
        {
            var flags = new List<ExerciseEffectivenessFlag>();
            foreach (var p in profiles ?? Array.Empty<ExerciseEffectivenessProfile>())
            {
                if (p is null || p.SessionCount == 0) continue;

                if (p.RecoveryCost >= HighRecoveryCostThreshold)
                {
                    flags.Add(new ExerciseEffectivenessFlag
                    {
                        ExerciseId = p.ExerciseId,
                        ReasonCode = "HIGH_RECOVERY_COST",
                        Magnitude = p.RecoveryCost,
                        Explanation = string.Create(CultureInfo.InvariantCulture,
                            $"Exercise {p.ExerciseId} is taxing recently (recovery cost {p.RecoveryCost:0}/100) — consider de-prioritising it in favour of lighter work.")
                    });
                }

                // Average fatigue per session, recovered from the cost weighting so the
                // flag is independent of the safety/strain contributions.
                var avgFatigue = EstimateAverageFatigue(p);
                if (avgFatigue >= HighFatigueThreshold)
                {
                    flags.Add(new ExerciseEffectivenessFlag
                    {
                        ExerciseId = p.ExerciseId,
                        ReasonCode = "HIGH_FATIGUE",
                        Magnitude = avgFatigue,
                        Explanation = string.Create(CultureInfo.InvariantCulture,
                            $"Exercise {p.ExerciseId} shows repeated fatigue signals (~{avgFatigue:0.0} per session) — a lighter alternative may be kinder.")
                    });
                }

                if (p.HasComfortData && p.ComfortGain <= -ComfortDeclineThreshold)
                {
                    flags.Add(new ExerciseEffectivenessFlag
                    {
                        ExerciseId = p.ExerciseId,
                        ReasonCode = "COMFORT_DECLINE",
                        Magnitude = p.ComfortGain,
                        Explanation = string.Create(CultureInfo.InvariantCulture,
                            $"Comfort has been easing down during exercise {p.ExerciseId} (slope {p.ComfortGain:0.0}/session) — worth de-prioritising while it recovers.")
                    });
                }
            }

            return flags
                .OrderBy(f => f.ExerciseId)
                .ThenBy(f => f.ReasonCode, StringComparer.Ordinal)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers.
        // ─────────────────────────────────────────────────────────────────────────

        private static IEnumerable<ExerciseEffectivenessProfile> Filter(
            IEnumerable<ExerciseEffectivenessProfile> profiles, bool includeLowData)
        {
            var source = profiles ?? Array.Empty<ExerciseEffectivenessProfile>();
            return includeLowData ? source : source.Where(p => p is not null && p.HasEnoughData);
        }

        /// <summary>
        /// Recovers the average fatigue-per-session that fed <see cref="ExerciseEffectivenessProfile.RecoveryCost"/>.
        /// RecoveryCost can saturate at 100, so this is a floor estimate; it is only used
        /// to raise the HIGH_FATIGUE flag, never to re-derive a stored metric.
        /// </summary>
        private static double EstimateAverageFatigue(ExerciseEffectivenessProfile p)
            => FatigueWeight <= 0 ? 0.0 : p.RecoveryCost / FatigueWeight;

        // Ordinary-least-squares slope of y over x = 0,1,2,… Returns 0 when undetermined
        // (mirrors LearningPathProfileBuilder.LinearSlope / RecoveryIntelligenceService).
        private static double LinearSlope(IReadOnlyList<double> y)
        {
            var clean = (y ?? Array.Empty<double>())
                .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
                .ToList();
            var n = clean.Count;
            if (n < 2) return 0.0;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var i = 0; i < n; i++)
            {
                sumX += i;
                sumY += clean[i];
                sumXY += i * clean[i];
                sumX2 += (double)i * i;
            }

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 0.0001) return 0.0;
            return (n * sumXY - sumX * sumY) / denominator;
        }

        private static string BuildExplanation(
            int exerciseId, int sessionCount, bool hasEnough,
            double resonanceGain, double comfortGain, bool hasComfortData,
            double consistencyGain, double recoveryCost, double successRate)
        {
            var dataNote = hasEnough
                ? string.Create(CultureInfo.InvariantCulture, $"{sessionCount} sessions")
                : string.Create(CultureInfo.InvariantCulture,
                    $"only {sessionCount} sessions (below {MinSessionsForTrust} — treat as insufficient evidence, not as ineffective)");

            var comfortNote = hasComfortData
                ? string.Create(CultureInfo.InvariantCulture, $"comfort {comfortGain:+0.0;-0.0;0.0}/session")
                : "comfort data unavailable (no comfort score joined per session)";

            var body = string.Create(CultureInfo.InvariantCulture,
                $"Exercise {exerciseId} over {dataNote}: resonance {resonanceGain:+0.0;-0.0;0.0}/session, consistency {consistencyGain:+0.0;-0.0;0.0}/session, {comfortNote}; recovery cost {recoveryCost:0}/100, success {successRate:0}%.");
            return body + " Gains are OLS trend slopes (a proxy, not a clinical before/after).";
        }

        private static double Round2(double v)
            => double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : Math.Round(v, 2, MidpointRounding.AwayFromZero);
    }
}
