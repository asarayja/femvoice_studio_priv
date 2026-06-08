using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// How strongly the recovery layer wants to be heard, ordered low → high so the
    /// enum can be compared numerically. This is a <em>recommendation</em> severity,
    /// never a gate: even <see cref="Urgent"/> only asks for rest / a lighter session —
    /// it can add an earlier, softer recovery nudge but can NEVER weaken an existing
    /// <c>ProgressionSafetyGate</c> (Safety) decision (clinical invariant).
    /// </summary>
    public enum RecoverySeverity
    {
        /// <summary>No predictive signal — train as planned.</summary>
        None = 0,

        /// <summary>Early drift worth watching; no action required yet.</summary>
        Watch = 1,

        /// <summary>A lighter session or a rest day is advisable soon.</summary>
        Recommend = 2,

        /// <summary>Strong predictive overload — rest / very light work is advised now,
        /// ideally BEFORE a strain episode or safety lock occurs.</summary>
        Urgent = 3
    }

    /// <summary>
    /// Pre-aggregated history a <see cref="RecoveryIntelligenceService"/> reasons over.
    /// Every field is directly derivable from <see cref="SessionAnalyticsStore"/> reads
    /// (sessions, exercise summaries, health events, voice-intelligence trend). The
    /// service can build this for you from a store, or you can hand it a ready snapshot
    /// (the no-IO path used by tests).
    ///
    /// "Acute" = the recent ~7-day window; "chronic" = the trailing ~28-day window used
    /// only to normalise acute load into an Acute:Chronic Workload Ratio.
    /// </summary>
    public readonly record struct RecoveryHistorySnapshot
    {
        /// <summary>Training sessions in the last 7 days (acute density).</summary>
        public int SessionsLast7Days { get; init; }

        /// <summary>Training sessions in the last 28 days (chronic volume).</summary>
        public int SessionsLast28Days { get; init; }

        /// <summary>Wall-clock hours since the most recent session started. Large values
        /// mean the voice has rested. <see cref="double.PositiveInfinity"/>/NaN are treated
        /// as "no recent session" (fully rested) by the scorer.</summary>
        public double HoursSinceLastSession { get; init; }

        /// <summary>Sum of fatigue indicators over the last 7 days.</summary>
        public int RecentFatigueIndicators { get; init; }

        /// <summary>Sum of fatigue indicators over the PRIOR week (days 7–14 ago), for the
        /// rising-trend branch of <see cref="RecoveryScorer"/>.</summary>
        public int PriorFatigueIndicators { get; init; }

        /// <summary>Strain episodes (StrainPeriod) in the last 7 days.</summary>
        public int RecentStrainEpisodes { get; init; }

        /// <summary>Safety freezes / locks (SafetyFreeze) in the last 7 days.</summary>
        public int RecentSafetyLocks { get; init; }

        /// <summary>Pause recommendations (PauseRecommended) in the last 7 days.</summary>
        public int RecentPauseRecommendations { get; init; }

        /// <summary>Hydration suggestions (HydrationSuggested) in the last 7 days.</summary>
        public int RecentHydrationSuggestions { get; init; }

        /// <summary>Acute training load = Σ(intensity × duration-minutes) over the last
        /// 7 days. Intensity is a 0–1 proxy (composite voice score / 100, neutral 0.5
        /// when unscored). Used as the numerator of the ACWR.</summary>
        public double AcuteTrainingLoad { get; init; }

        /// <summary>Chronic training load = Σ(intensity × duration-minutes) over the last
        /// 28 days. Divided by 4 to get a weekly average for the ACWR denominator.</summary>
        public double ChronicTrainingLoad { get; init; }

        /// <summary>ComfortScore100 values of recent completed sessions, CHRONOLOGICAL.
        /// The slope across these is the early "comfort declining before a breach" signal.</summary>
        public IReadOnlyList<double> RecentComfortScores { get; init; }
    }

    /// <summary>
    /// A predictive recovery forecast. Wraps the existing reactive
    /// <see cref="RecoveryResult"/> (now fed a FULL input so its overtraining/trend
    /// branches actually fire) with four forward-looking flags, an explainable
    /// recommendation, and a single <see cref="Severity"/>.
    ///
    /// CLINICAL INVARIANT: this is the Health/Recovery layer. It may ADVISE rest or a
    /// lighter session earlier and more softly than a hard gate would, but it never
    /// represents or weakens a Safety decision. A consumer must treat a forecast as
    /// additive to — never a replacement for — <c>ProgressionSafetyGate</c>.
    /// </summary>
    public readonly record struct RecoveryForecast
    {
        /// <summary>The reactive recovery score, computed by <see cref="RecoveryScorer"/>
        /// from a FULL <see cref="RecoveryScoreInput"/> (density, rest, fatigue trend).</summary>
        public required RecoveryResult Current { get; init; }

        /// <summary>True when high training density meets too little rest — the scorer's
        /// own overtraining branch fired (sessions &gt; 5 AND hours &lt; 12).</summary>
        public bool OvertrainingPredicted { get; init; }

        /// <summary>True when accumulated unpaid rest (recovery debt) is high.</summary>
        public bool RecoveryDebtHigh { get; init; }

        /// <summary>True when the Acute:Chronic Workload Ratio exceeds the overload
        /// threshold (&gt; 1.3) — load is ramping faster than the body has adapted to.</summary>
        public bool TrainingOverload { get; init; }

        /// <summary>True when the recent ComfortScore100 slope is clearly negative — comfort
        /// is eroding BEFORE any strain/safety event has been logged.</summary>
        public bool ComfortDeclining { get; init; }

        /// <summary>Acute:Chronic Workload Ratio actually computed (0 when no chronic base).</summary>
        public double AcuteChronicWorkloadRatio { get; init; }

        /// <summary>Recovery debt, 0–100 (0 = none, higher = more unpaid rest).</summary>
        public double RecoveryDebt { get; init; }

        /// <summary>Linear slope of recent ComfortScore100 (negative = declining).</summary>
        public double ComfortDeclineSlope { get; init; }

        /// <summary>Explainable, clinically safe recommendation. Calm and non-pressuring;
        /// advises rest / a lighter session, never strain.</summary>
        public required string Recommendation { get; init; }

        /// <summary>How strongly recovery wants to be heard (None…Urgent). Advisory only.</summary>
        public RecoverySeverity Severity { get; init; }
    }

    /// <summary>
    /// PREDICTIVE recovery intelligence. Today's recovery signal is purely reactive: the
    /// live recorder builds a <see cref="RecoveryScoreInput"/> with SessionsLast7Days,
    /// HoursSinceLastSession and PriorFatigueIndicators left at 0, so the scorer's
    /// overtraining and rising-trend branches never fire. This service closes that gap.
    ///
    /// It (1) builds a FULL input from persisted history so those existing-but-dormant
    /// branches actually activate, (2) computes four explainable predictive aggregates —
    /// training load, Acute:Chronic Workload Ratio, recovery debt and comfort-decline
    /// slope — and (3) emits a <see cref="RecoveryForecast"/> that can flag overload
    /// BEFORE a strain episode or safety lock occurs.
    ///
    /// ── THRESHOLDS / FORMULAS (all documented, deterministic, no hidden IO) ─────────
    /// • Window: ACUTE = last 7 days, CHRONIC = last 28 days.
    /// • TrainingLoad (per session) = Intensity × DurationMinutes, where Intensity =
    ///   clamp(CompositeVoiceScore/100, 0, 1), defaulting to 0.5 when unscored. Acute =
    ///   Σ over 7d, Chronic = Σ over 28d.
    /// • ACWR = AcuteLoad / (ChronicLoad / 4)  [chronic/4 = the chronic *weekly average*].
    ///   ACWR &gt; 1.3 ⇒ TrainingOverload (sports-medicine "danger zone" convention).
    ///   No chronic base (new user) ⇒ ACWR 0 ⇒ never an overload flag.
    /// • RecoveryDebt: each acute session accrues 1 debt unit; rest pays it down at
    ///   1 unit per <see cref="RestPaydownHours"/> (24h) since the last session. Scaled to
    ///   0–100 against <see cref="DebtUnitsForFullScale"/> sessions. ≥ 60 ⇒ RecoveryDebtHigh.
    /// • ComfortDeclineSlope = least-squares slope of the recent ComfortScore100 series
    ///   (need ≥ <see cref="MinComfortPointsForSlope"/> points). ≤ −<see cref="ComfortDeclineSlopeThreshold"/>
    ///   (−1.5 points/session) ⇒ ComfortDeclining.
    /// • Severity (max of the contributing signals):
    ///     Urgent    — scorer already Overtrained/Strained, OR (ACWR &gt; 1.5),
    ///                 OR (overtraining branch fired AND comfort declining).
    ///     Recommend — TrainingOverload OR OvertrainingPredicted OR RecoveryDebtHigh
    ///                 OR ComfortDeclining.
    ///     Watch     — mild drift: ACWR in (1.15, 1.3], or a gently negative comfort slope.
    ///     None      — otherwise.
    ///
    /// The service is pure given a <see cref="RecoveryHistorySnapshot"/>; the store-reading
    /// overload only assembles that snapshot, so all numeric behaviour is unit-testable
    /// without a database.
    /// </summary>
    public sealed class RecoveryIntelligenceService
    {
        // ── Windows ──────────────────────────────────────────────────────────────
        public const int AcuteWindowDays = 7;
        public const int ChronicWindowDays = 28;

        // ── ACWR ─────────────────────────────────────────────────────────────────
        /// <summary>Acute:Chronic ratio above which load is "ramping too fast".</summary>
        public const double AcwrOverloadThreshold = 1.3;

        /// <summary>Upper ACWR band that escalates the forecast to Urgent.</summary>
        public const double AcwrUrgentThreshold = 1.5;

        /// <summary>Lower ACWR band that only warrants a Watch (mild ramp).</summary>
        public const double AcwrWatchThreshold = 1.15;

        /// <summary>Chronic load is averaged over this many weeks (28d / 4 = weekly mean).</summary>
        private const double ChronicWeeks = 4.0;

        // ── Training-load intensity proxy ────────────────────────────────────────
        /// <summary>Neutral intensity used when a session has no composite voice score.</summary>
        public const double NeutralIntensity = 0.5;

        // ── Recovery debt ────────────────────────────────────────────────────────
        /// <summary>Hours of rest that pay down one accrued debt unit.</summary>
        public const double RestPaydownHours = 24.0;

        /// <summary>Number of net debt units that maps to a full 100 on the debt scale.</summary>
        public const double DebtUnitsForFullScale = 7.0;

        /// <summary>Debt (0–100) at/above which <see cref="RecoveryForecast.RecoveryDebtHigh"/>.</summary>
        public const double RecoveryDebtHighThreshold = 60.0;

        // ── Comfort decline ──────────────────────────────────────────────────────
        /// <summary>Minimum comfort data points before a slope is trustworthy.</summary>
        public const int MinComfortPointsForSlope = 3;

        /// <summary>Slope (points/session) at/below −this is a clear decline.</summary>
        public const double ComfortDeclineSlopeThreshold = 1.5;

        /// <summary>A gentler negative slope at/below −this only warrants a Watch.</summary>
        public const double ComfortWatchSlopeThreshold = 0.5;

        private readonly RecoveryScorer _scorer;

        public RecoveryIntelligenceService(RecoveryScorer? scorer = null)
        {
            _scorer = scorer ?? new RecoveryScorer();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PURE path — forecast directly from a pre-aggregated snapshot. No IO. This is
        // the core all numeric behaviour is tested against.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Produces a <see cref="RecoveryForecast"/> from a pre-aggregated snapshot.
        /// Pure and total: any input (including all-zero / non-finite) yields a sane,
        /// clamped result. This is the predictive core.
        /// </summary>
        public RecoveryForecast Forecast(RecoveryHistorySnapshot snapshot)
        {
            // (1) FULL reactive input — this is what makes the dormant scorer branches
            //     fire (live input leaves SessionsLast7Days / Hours / PriorFatigue at 0).
            var input = BuildScoreInput(snapshot);
            var current = _scorer.Score(input);

            // (2) Predictive aggregates.
            var acwr = ComputeAcwr(snapshot.AcuteTrainingLoad, snapshot.ChronicTrainingLoad);
            var debt = ComputeRecoveryDebt(snapshot.SessionsLast7Days, snapshot.HoursSinceLastSession);
            var comfortSlope = ComputeComfortSlope(snapshot.RecentComfortScores);

            // (3) Flags. Reuse the scorer's OWN overtraining condition for the
            //     OvertrainingPredicted flag so the contract stays a single source of truth.
            var sessions = Math.Max(0, snapshot.SessionsLast7Days);
            var hours = SanitiseHours(snapshot.HoursSinceLastSession);
            var overtrainingPredicted =
                sessions > RecoveryScorerOvertrainSessions && hours < RecoveryScorerOvertrainHours;

            var trainingOverload = acwr > AcwrOverloadThreshold;
            var recoveryDebtHigh = debt >= RecoveryDebtHighThreshold;
            var comfortDeclining = comfortSlope <= -ComfortDeclineSlopeThreshold;

            var severity = ClassifySeverity(
                current.Status, acwr, overtrainingPredicted, trainingOverload,
                recoveryDebtHigh, comfortDeclining, comfortSlope);

            var recommendation = BuildRecommendation(
                severity, current, overtrainingPredicted, trainingOverload,
                recoveryDebtHigh, comfortDeclining, acwr, debt, comfortSlope);

            return new RecoveryForecast
            {
                Current = current,
                OvertrainingPredicted = overtrainingPredicted,
                RecoveryDebtHigh = recoveryDebtHigh,
                TrainingOverload = trainingOverload,
                ComfortDeclining = comfortDeclining,
                AcuteChronicWorkloadRatio = acwr,
                RecoveryDebt = debt,
                ComfortDeclineSlope = comfortSlope,
                Recommendation = recommendation,
                Severity = severity
            };
        }

        /// <summary>
        /// Translates a snapshot into the FULL <see cref="RecoveryScoreInput"/>. Exposed
        /// so callers/tests can assert that the previously-empty density/rest/trend fields
        /// are now populated (the whole point: dormant scorer branches now fire).
        /// </summary>
        public static RecoveryScoreInput BuildScoreInput(RecoveryHistorySnapshot snapshot)
        {
            return new RecoveryScoreInput
            {
                SessionsLast7Days = Math.Max(0, snapshot.SessionsLast7Days),
                HoursSinceLastSession = SanitiseHours(snapshot.HoursSinceLastSession),
                RecentFatigueIndicators = Math.Max(0, snapshot.RecentFatigueIndicators),
                PriorFatigueIndicators = Math.Max(0, snapshot.PriorFatigueIndicators),
                RecentStrainEpisodes = Math.Max(0, snapshot.RecentStrainEpisodes),
                RecentSafetyLocks = Math.Max(0, snapshot.RecentSafetyLocks),
                RecentPauseRecommendations = Math.Max(0, snapshot.RecentPauseRecommendations),
                HydrationSuggestionsRecent = Math.Max(0, snapshot.RecentHydrationSuggestions)
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // STORE path — assemble the snapshot from persisted history, then forecast.
        // Thin: it only does reads + the same aggregation the snapshot documents.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a <see cref="RecoveryHistorySnapshot"/> from a <see cref="SessionAnalyticsStore"/>
        /// as of <paramref name="now"/>, then forecasts. The acute window is the 7 days
        /// before <paramref name="now"/>; chronic is the 28 days before it.
        /// </summary>
        public async Task<RecoveryForecast> ForecastFromHistoryAsync(
            SessionAnalyticsStore store,
            DateTime now,
            int userId = 1,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(store);

            var snapshot = await BuildSnapshotAsync(store, now, userId, cancellationToken)
                .ConfigureAwait(false);
            return Forecast(snapshot);
        }

        /// <summary>
        /// Assembles the snapshot from store reads. Acute = [now−7d, now); the week before
        /// that (= [now−14d, now−7d)) supplies PriorFatigueIndicators; chronic = [now−28d, now).
        /// Counts strain/safety/pause/hydration from health events; training load from each
        /// session's intensity × duration.
        /// </summary>
        public async Task<RecoveryHistorySnapshot> BuildSnapshotAsync(
            SessionAnalyticsStore store,
            DateTime now,
            int userId = 1,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(store);

            var acuteFrom = now.AddDays(-AcuteWindowDays);
            var priorFrom = now.AddDays(-2 * AcuteWindowDays);
            var chronicFrom = now.AddDays(-ChronicWindowDays);

            // One chronic read covers every window we need (acute ⊂ prior ⊂ chronic).
            var chronicSessions = await store
                .GetVoiceIntelligenceTrendAsync(chronicFrom, now, userId, cancellationToken)
                .ConfigureAwait(false);
            var acuteEvents = await store
                .GetHealthEventsAsync(acuteFrom, now, userId, cancellationToken)
                .ConfigureAwait(false);

            var acuteSessions = chronicSessions
                .Where(s => s.StartedAt >= acuteFrom && s.StartedAt < now)
                .OrderBy(s => s.StartedAt)
                .ToList();
            var priorSessions = chronicSessions
                .Where(s => s.StartedAt >= priorFrom && s.StartedAt < acuteFrom)
                .ToList();

            var acuteFromStore = await store
                .GetExerciseSummariesAsync(acuteFrom, now, userId, cancellationToken)
                .ConfigureAwait(false);
            var priorFromStore = await store
                .GetExerciseSummariesAsync(priorFrom, acuteFrom, userId, cancellationToken)
                .ConfigureAwait(false);

            var lastSession = chronicSessions
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefault();
            var hoursSinceLast = lastSession is null
                ? double.PositiveInfinity
                : Math.Max(0.0, (now - lastSession.StartedAt).TotalHours);

            return new RecoveryHistorySnapshot
            {
                SessionsLast7Days = acuteSessions.Count,
                SessionsLast28Days = chronicSessions.Count,
                HoursSinceLastSession = hoursSinceLast,
                RecentFatigueIndicators = acuteFromStore.Sum(e => Math.Max(0, e.FatigueIndicators)),
                PriorFatigueIndicators = priorFromStore.Sum(e => Math.Max(0, e.FatigueIndicators)),
                RecentStrainEpisodes = acuteEvents.Count(e => e.EventType == HealthAnalyticsEventType.StrainPeriod),
                RecentSafetyLocks = acuteEvents.Count(e => e.EventType == HealthAnalyticsEventType.SafetyFreeze),
                RecentPauseRecommendations = acuteEvents.Count(e => e.EventType == HealthAnalyticsEventType.PauseRecommended),
                RecentHydrationSuggestions = acuteEvents.Count(e => e.EventType == HealthAnalyticsEventType.HydrationSuggested),
                AcuteTrainingLoad = acuteSessions.Sum(TrainingLoadOf),
                ChronicTrainingLoad = chronicSessions.Sum(TrainingLoadOf),
                RecentComfortScores = acuteSessions
                    .Select(s => s.ComfortScore100)
                    .ToList()
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Aggregate helpers (pure, documented).
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Per-session training load = Intensity × DurationMinutes. Intensity is the
        /// composite voice score on 0–1 (neutral 0.5 when the session is unscored, so a
        /// session still contributes its duration). A session with no usable duration
        /// (missing/!valid EndedAt) contributes 0.
        /// </summary>
        public static double TrainingLoadOf(VoiceIntelligenceTrendPoint session)
        {
            if (session is null) return 0.0;

            var durationMinutes = session.EndedAt.HasValue && session.EndedAt.Value > session.StartedAt
                ? (session.EndedAt.Value - session.StartedAt).TotalMinutes
                : 0.0;
            if (durationMinutes <= 0.0) return 0.0;

            var composite = session.CompositeVoiceScore;
            var intensity = composite > 0.0 && !double.IsNaN(composite) && !double.IsInfinity(composite)
                ? Math.Clamp(composite / 100.0, 0.0, 1.0)
                : NeutralIntensity;

            return intensity * durationMinutes;
        }

        /// <summary>
        /// Acute:Chronic Workload Ratio = acute / (chronic / 4). Returns 0 when there is
        /// no genuine chronic BASELINE — i.e. all the chronic load sits inside the acute
        /// week (older portion = chronic − acute ≤ 0). A ratio is only meaningful once
        /// there is prior history to ramp away from; otherwise a brand-new user's very
        /// first week would spuriously read as "overload" (acute == chronic ⇒ ratio 4).
        /// </summary>
        public static double ComputeAcwr(double acuteLoad, double chronicLoad)
        {
            var acute = SanitiseLoad(acuteLoad);
            var chronic = SanitiseLoad(chronicLoad);

            // No prior baseline beyond the acute week ⇒ ACWR undefined ⇒ never overload.
            var olderBaseline = chronic - acute;
            if (olderBaseline <= 0.0) return 0.0;

            var chronicWeekly = chronic / ChronicWeeks;
            if (chronicWeekly <= 0.0) return 0.0;
            return acute / chronicWeekly;
        }

        /// <summary>
        /// Recovery debt, 0–100. Each acute session accrues one debt unit; rest pays it
        /// down at one unit per <see cref="RestPaydownHours"/> since the last session. Net
        /// units are clamped ≥ 0 and scaled against <see cref="DebtUnitsForFullScale"/>.
        /// </summary>
        public static double ComputeRecoveryDebt(int sessionsLast7Days, double hoursSinceLastSession)
        {
            var accrued = Math.Max(0, sessionsLast7Days);
            var hours = SanitiseHours(hoursSinceLastSession);
            var paidDown = hours / RestPaydownHours;
            var netUnits = Math.Max(0.0, accrued - paidDown);
            var scaled = netUnits / DebtUnitsForFullScale * 100.0;
            return Math.Clamp(scaled, 0.0, 100.0);
        }

        /// <summary>
        /// Least-squares slope of the comfort series (points per session). Negative ⇒
        /// declining. Returns 0 with fewer than <see cref="MinComfortPointsForSlope"/>
        /// usable points (no trustworthy trend). Non-finite values are dropped.
        /// </summary>
        public static double ComputeComfortSlope(IReadOnlyList<double>? comfortScores)
        {
            if (comfortScores is null) return 0.0;

            var ys = comfortScores
                .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
                .ToList();
            if (ys.Count < MinComfortPointsForSlope) return 0.0;

            // Simple linear regression y = mx + b, x = 0..n-1 (mirrors FemVoiceScoreEngine).
            var n = ys.Count;
            var sumX = 0.0;
            var sumY = 0.0;
            var sumXY = 0.0;
            var sumX2 = 0.0;
            for (var i = 0; i < n; i++)
            {
                sumX += i;
                sumY += ys[i];
                sumXY += i * ys[i];
                sumX2 += (double)i * i;
            }

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 0.0001) return 0.0;

            return (n * sumXY - sumX * sumY) / denominator;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Severity + recommendation.
        // ─────────────────────────────────────────────────────────────────────────

        private static RecoverySeverity ClassifySeverity(
            RecoveryStatus reactiveStatus,
            double acwr,
            bool overtrainingPredicted,
            bool trainingOverload,
            bool recoveryDebtHigh,
            bool comfortDeclining,
            double comfortSlope)
        {
            // Urgent: the reactive scorer is already in the bottom two buckets (real load
            // already present), OR the load ramp is severe, OR density-without-rest is
            // co-occurring with eroding comfort (the classic pre-injury pattern).
            var reactiveSevere =
                reactiveStatus == RecoveryStatus.Overtrained ||
                reactiveStatus == RecoveryStatus.Strained;
            if (reactiveSevere ||
                acwr > AcwrUrgentThreshold ||
                (overtrainingPredicted && comfortDeclining))
            {
                return RecoverySeverity.Urgent;
            }

            // Recommend: any single strong predictive flag.
            if (trainingOverload || overtrainingPredicted || recoveryDebtHigh || comfortDeclining)
            {
                return RecoverySeverity.Recommend;
            }

            // Watch: mild ramp or a gently negative comfort drift not yet at the threshold.
            if ((acwr > AcwrWatchThreshold && acwr <= AcwrOverloadThreshold) ||
                comfortSlope <= -ComfortWatchSlopeThreshold)
            {
                return RecoverySeverity.Watch;
            }

            return RecoverySeverity.None;
        }

        /// <summary>
        /// Builds a calm, non-pressuring, explainable recommendation. The copy never tells
        /// the user to push/strain; it offers rest or a lighter session and always names
        /// the driving signals so the advice is traceable.
        /// </summary>
        private static string BuildRecommendation(
            RecoverySeverity severity,
            RecoveryResult current,
            bool overtrainingPredicted,
            bool trainingOverload,
            bool recoveryDebtHigh,
            bool comfortDeclining,
            double acwr,
            double debt,
            double comfortSlope)
        {
            var drivers = new List<string>();
            if (overtrainingPredicted)
                drivers.Add("high training density with little rest");
            if (trainingOverload)
                drivers.Add(string.Create(CultureInfo.InvariantCulture,
                    $"recent load is climbing faster than usual (ratio {acwr:0.00})"));
            if (recoveryDebtHigh)
                drivers.Add(string.Create(CultureInfo.InvariantCulture,
                    $"rest is running behind recent activity (debt {debt:0})"));
            if (comfortDeclining)
                drivers.Add(string.Create(CultureInfo.InvariantCulture,
                    $"comfort has been easing down lately (slope {comfortSlope:0.0}/session)"));

            var sb = new StringBuilder();
            switch (severity)
            {
                case RecoverySeverity.Urgent:
                    sb.Append("A rest day or a very light, easy session looks best right now");
                    break;
                case RecoverySeverity.Recommend:
                    sb.Append("A lighter session or a rest day soon would help your voice stay comfortable");
                    break;
                case RecoverySeverity.Watch:
                    sb.Append("Things look fine — just keep an eye on how your voice feels");
                    break;
                default:
                    sb.Append("Your voice looks well recovered — train as you planned");
                    break;
            }

            if (drivers.Count > 0)
            {
                sb.Append(" — noticing ");
                sb.Append(string.Join(", ", drivers));
            }

            sb.Append('.');

            // Always restate the clinical boundary so any consumer is reminded this is a
            // gentle recovery suggestion, never a safety decision.
            if (severity >= RecoverySeverity.Recommend)
            {
                sb.Append(" This is a gentle suggestion to rest earlier, not a limit on your training.");
            }

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Sanitisation + mirrored scorer constants (kept in lock-step with RecoveryScorer
        // so OvertrainingPredicted uses the SAME condition as the scorer's own branch).
        // ─────────────────────────────────────────────────────────────────────────

        private const int RecoveryScorerOvertrainSessions = 5;
        private const double RecoveryScorerOvertrainHours = 12.0;

        private static double SanitiseHours(double hours)
        {
            if (double.IsNaN(hours) || hours < 0) return 0.0;
            // +∞ ("no recent session") is mapped to a very large FINITE value here so it
            // represents "effectively unlimited rest" ⇒ capped rest reward, zero recovery
            // debt, never overtraining. NB: this DIFFERS from RecoveryScorer.SanitiseHours,
            // which maps +∞ → 0; both are clinically equivalent (rest reward saturates / debt
            // floors at 0 either way), and the ∞ path is effectively unreachable in the live
            // completion flow (the current session's start row is already journaled).
            if (double.IsInfinity(hours)) return double.MaxValue / 4.0;
            return hours;
        }

        private static double SanitiseLoad(double load)
        {
            if (double.IsNaN(load) || double.IsInfinity(load) || load < 0) return 0.0;
            return load;
        }
    }
}
