using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FemVoiceStudio.Services
{
    /// <summary>Decision returned by <see cref="ProgressionSafetyGate"/>.</summary>
    public sealed record ProgressionGateResult
    {
        public bool IsBlocked { get; init; }
        public string ReasonCode { get; init; } = "CLEAR";
        public int RecommendedRestDays { get; init; } = 2;

        public static readonly ProgressionGateResult Clear = new();
    }

    /// <summary>
    /// Stateless clinical gate evaluated before any difficulty promotion.
    /// Reads the persisted health-event history (SessionAnalyticsStore) so the
    /// decision survives app restarts — unlike the in-memory safety lock in
    /// ProgressionService, which this gate engages externally when blocked.
    ///
    /// Blocking rules (any one suffices, regardless of how high the score is):
    ///   • REPEATED_SAFETY_LOCKS    — ≥ 2 SafetyFreeze events in the last 7 days.
    ///   • STRAIN_TREND_RISING      — more StrainPeriod events in the last 7 days
    ///                                than in the 7 days before, and ≥ 2 recent.
    ///   • FATIGUE_TREND_RISING     — exercise-summary fatigue indicators rising
    ///                                week-over-week with ≥ 3 in the recent week.
    ///   • REPEATED_COMFORT_BREACHES — ≥ 3 sessions journaled with repeated
    ///                                comfort-zone breaches in the last 7 days.
    /// </summary>
    public sealed class ProgressionSafetyGate
    {
        private const int SafetyLockBlockThreshold = 2;
        private const int StrainRecentMinimum = 2;
        private const int FatigueRecentMinimum = 3;
        private const int BreachSessionBlockThreshold = 3;

        private readonly SessionAnalyticsStore _analyticsStore;

        public ProgressionSafetyGate(SessionAnalyticsStore analyticsStore)
            => _analyticsStore = analyticsStore ?? throw new ArgumentNullException(nameof(analyticsStore));

        public async Task<ProgressionGateResult> EvaluateAsync(
            DateTime now,
            int userId = 1,
            CancellationToken cancellationToken = default)
        {
            var events = await _analyticsStore.GetHealthEventsAsync(
                now.AddDays(-14), now.AddTicks(1), userId, cancellationToken).ConfigureAwait(false);

            var recentWeekStart = now.AddDays(-7);

            var recentLocks = events.Count(e =>
                e.EventType == HealthAnalyticsEventType.SafetyFreeze && e.OccurredAt >= recentWeekStart);
            if (recentLocks >= SafetyLockBlockThreshold)
                return new ProgressionGateResult { IsBlocked = true, ReasonCode = "REPEATED_SAFETY_LOCKS" };

            var recentStrain = events.Count(e =>
                e.EventType == HealthAnalyticsEventType.StrainPeriod && e.OccurredAt >= recentWeekStart);
            var priorStrain = events.Count(e =>
                e.EventType == HealthAnalyticsEventType.StrainPeriod && e.OccurredAt < recentWeekStart);
            if (recentStrain >= StrainRecentMinimum && recentStrain > priorStrain)
                return new ProgressionGateResult { IsBlocked = true, ReasonCode = "STRAIN_TREND_RISING" };

            var recentBreachSessions = events
                .Where(e => e.EventType == HealthAnalyticsEventType.ComfortZoneBreach
                            && e.OccurredAt >= recentWeekStart)
                .Select(e => e.SessionId)
                .Distinct()
                .Count();
            if (recentBreachSessions >= BreachSessionBlockThreshold)
                return new ProgressionGateResult { IsBlocked = true, ReasonCode = "REPEATED_COMFORT_BREACHES" };

            var summaries = await _analyticsStore.GetExerciseSummariesAsync(
                now.AddDays(-14), now.AddTicks(1), userId, cancellationToken).ConfigureAwait(false);
            var recentFatigue = summaries
                .Where(s => s.StartedAt >= recentWeekStart)
                .Sum(s => s.FatigueIndicators);
            var priorFatigue = summaries
                .Where(s => s.StartedAt < recentWeekStart)
                .Sum(s => s.FatigueIndicators);
            if (recentFatigue >= FatigueRecentMinimum && recentFatigue > priorFatigue)
                return new ProgressionGateResult { IsBlocked = true, ReasonCode = "FATIGUE_TREND_RISING" };

            return ProgressionGateResult.Clear;
        }
    }
}
