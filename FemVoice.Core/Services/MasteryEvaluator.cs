using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>Result of a mastery evaluation. ReasonCode is for logging/tests — never shown raw in UI.</summary>
    public sealed record MasteryEvaluation
    {
        public MasteryLevel Level { get; init; } = MasteryLevel.Beginner;
        public string ReasonCode { get; init; } = string.Empty;
    }

    /// <summary>
    /// Computes per-exercise <see cref="MasteryLevel"/> from persisted session analytics.
    ///
    /// Clinical gates (all hard requirements — score alone can never unlock a level):
    ///   Beginner    &lt; 3 completed sessions.
    ///   Developing  default, and forced ceiling when a safety lock occurred in the
    ///               last 7 days (demotion-on-risk) or when analytics history is too
    ///               thin to verify consistency (conservative default).
    ///   Stable      ≥ 8 sessions, recent resonance/stability averages meet the
    ///               profile thresholds, no safety locks in the last 7 days.
    ///   Mastered    ≥ 20 sessions, Stable requirements, no safety locks in the last
    ///               14 days, at most 2 of the last 10 sessions with repeated comfort
    ///               breaches, and fatigue trend not rising.
    ///
    /// Pitch never enters the evaluation — comfort-zone events are the only
    /// pitch-adjacent signal, and they only ever gate downward.
    /// </summary>
    public sealed class MasteryEvaluator
    {
        private const int MinSessionsForStable = 8;
        private const int MinSessionsForMastered = 20;
        private const int RecentWindowSessions = 10;
        private const int MinAnalyticsSessionsRequired = 5;
        private const int MaxBreachSessionsForMastered = 2;

        // 90 dager: antallsgatene krever klinisk VERIFISERTE økter (analytics-rader),
        // ikke legacy-oppmøte — vinduet må derfor romme 20 reelle økter i normal
        // treningstakt. Konsistens måles fortsatt på de siste 10 øktene, og
        // safety-vinduene er uavhengig avgrenset til 7/14 dager.
        private const int LookbackDays = 90;

        private readonly SessionAnalyticsStore _analyticsStore;

        public MasteryEvaluator(SessionAnalyticsStore analyticsStore)
            => _analyticsStore = analyticsStore ?? throw new ArgumentNullException(nameof(analyticsStore));

        public async Task<MasteryEvaluation> EvaluateAsync(
            int exerciseId,
            int totalSessions,
            ExerciseTargetProfile profile,
            DateTime now,
            int userId = 1,
            CancellationToken cancellationToken = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            if (totalSessions < 3)
                return new MasteryEvaluation { Level = MasteryLevel.Beginner, ReasonCode = "SESSION_COUNT" };

            var from = now.AddDays(-LookbackDays);
            var trend = await _analyticsStore.GetExerciseTrendAsync(
                exerciseId, from, now.AddTicks(1), userId, cancellationToken).ConfigureAwait(false);
            var recent = trend.TakeLast(RecentWindowSessions).ToList();

            // ── Demotion-gate: fersk safety-lock overstyrer alt annet ────────────
            var locks7d = trend
                .Where(s => s.StartedAt >= now.AddDays(-7))
                .Sum(s => s.SafetyEventsCount);
            if (locks7d > 0)
                return new MasteryEvaluation { Level = MasteryLevel.Developing, ReasonCode = "GATE_SAFETY_RECENT_LOCK" };

            // ── Konservativ default: uten verifiserbar historikk, aldri over Developing ──
            if (recent.Count < MinAnalyticsSessionsRequired)
                return new MasteryEvaluation { Level = MasteryLevel.Developing, ReasonCode = "INSUFFICIENT_HISTORY" };

            var avgResonance = recent.Average(s => s.ResonanceQualityIndex);
            var avgStability = recent.Average(s => s.StabilityConsistency);
            var resonanceOk  = !profile.UsesResonance || avgResonance >= profile.TargetResonanceMin;
            var stabilityOk  = !profile.UsesStability || avgStability >= profile.StabilityThreshold;

            // Antallsgaten krever BÅDE totalSessions (legacy-teller, beholdes av
            // score-migreringen) OG like mange klinisk verifiserte analytics-rader.
            // Uten sistnevnte kunne en returnerende bruker med 20+ gamle tidsbaserte
            // økter nå Mastered med bare 5 ferske økter (review-funn).
            if (totalSessions >= MinSessionsForMastered
                && trend.Count >= MinSessionsForMastered
                && resonanceOk && stabilityOk)
            {
                var locks14d = trend
                    .Where(s => s.StartedAt >= now.AddDays(-14))
                    .Sum(s => s.SafetyEventsCount);

                var breachSessions = await CountBreachSessionsAsync(
                    recent.Select(s => s.SessionId).ToHashSet(), from, now, userId, cancellationToken)
                    .ConfigureAwait(false);

                var fatigueRising = IsFatigueRising(recent);

                if (locks14d == 0 && breachSessions <= MaxBreachSessionsForMastered && !fatigueRising)
                    return new MasteryEvaluation { Level = MasteryLevel.Mastered, ReasonCode = "ALL_GATES_PASSED" };

                var reason = locks14d > 0 ? "GATE_SAFETY_14D"
                           : fatigueRising ? "GATE_FATIGUE_RISING"
                           : "GATE_COMFORT_BREACHES";
                return new MasteryEvaluation { Level = MasteryLevel.Stable, ReasonCode = reason };
            }

            if (totalSessions >= MinSessionsForStable
                && trend.Count >= MinSessionsForStable
                && resonanceOk && stabilityOk)
                return new MasteryEvaluation { Level = MasteryLevel.Stable, ReasonCode = "CONSISTENT" };

            return new MasteryEvaluation { Level = MasteryLevel.Developing, ReasonCode = "BUILDING" };
        }

        /// <summary>Counts distinct recent sessions journaled with repeated comfort breaches.</summary>
        private async Task<int> CountBreachSessionsAsync(
            System.Collections.Generic.HashSet<int> recentSessionIds,
            DateTime from,
            DateTime to,
            int userId,
            CancellationToken cancellationToken)
        {
            var events = await _analyticsStore.GetHealthEventsAsync(
                from, to.AddTicks(1), userId, cancellationToken).ConfigureAwait(false);
            return events
                .Where(e => e.EventType == HealthAnalyticsEventType.ComfortZoneBreach
                            && recentSessionIds.Contains(e.SessionId))
                .Select(e => e.SessionId)
                .Distinct()
                .Count();
        }

        /// <summary>
        /// Fatigue trend over the recent window: rising when the second half carries
        /// more fatigue indicators than the first AND the absolute level is meaningful.
        /// </summary>
        private static bool IsFatigueRising(
            System.Collections.Generic.IReadOnlyList<ExercisePerformanceSummary> recent)
        {
            if (recent.Count < 4) return false;
            var firstHalf  = recent.Take(recent.Count / 2).Sum(s => s.FatigueIndicators);
            var secondHalf = recent.Skip(recent.Count / 2).Sum(s => s.FatigueIndicators);
            return secondHalf > firstHalf && secondHalf >= 3;
        }
    }
}
