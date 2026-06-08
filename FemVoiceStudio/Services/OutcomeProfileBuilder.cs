using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// OUTCOME TRACKING assembler (Sprint E, W0-A4). Joins four already-existing
    /// intelligence layers into one <see cref="OutcomeProfile"/>:
    /// <list type="number">
    ///   <item><description>GOALS — legacy <see cref="SmartCoachGoal"/> rows
    ///   (TargetValue/CurrentValue) joined to the user's <see cref="VoiceGoalProfile"/>
    ///   PrimaryFocus via a string→<see cref="VoiceDimension"/> mapping.</description></item>
    ///   <item><description>RECOVERY — <see cref="RecoveryIntelligenceService"/>'s
    ///   predictive <see cref="RecoveryForecast"/>.</description></item>
    ///   <item><description>EXERCISE EFFECTIVENESS —
    ///   <see cref="ExerciseEffectivenessEngine"/> (EvaluateAll + RankMostEffective +
    ///   FlagConcerns).</description></item>
    ///   <item><description>LONG-TERM DEVELOPMENT —
    ///   <see cref="SmartCoachEngine.TryBuildDevelopmentProfile"/> (which DOES populate
    ///   Plateau/Breakthrough/Regression via the injected VoicePatternDetector) plus the
    ///   <see cref="LongitudinalInsightEngine"/>.</description></item>
    /// </list>
    ///
    /// The numeric core is the PURE <see cref="Build"/> method over already-fetched inputs;
    /// <see cref="AssembleFromStoreAsync"/> is a thin wrapper that gathers those inputs from
    /// the live engines/services and then calls <see cref="Build"/>.
    ///
    /// CLINICAL FRAMING: this builder is DESCRIPTIVE / REPORTING intelligence ONLY. It never
    /// overrides the Safety &gt; Health &gt; Recovery hierarchy.
    /// </summary>
    public sealed class OutcomeProfileBuilder
    {
        private readonly SmartCoachEngine _smartCoachEngine;
        private readonly ExerciseEffectivenessEngine _effectivenessEngine;
        private readonly LongitudinalInsightEngine _insightEngine;

        /// <summary>
        /// Constructs the store-reading assembler. The engines are the SAME live instances
        /// used elsewhere; this builder only READS through them, never mutates state.
        /// </summary>
        public OutcomeProfileBuilder(
            SmartCoachEngine smartCoachEngine,
            ExerciseEffectivenessEngine effectivenessEngine,
            LongitudinalInsightEngine insightEngine)
        {
            _smartCoachEngine = smartCoachEngine ?? throw new ArgumentNullException(nameof(smartCoachEngine));
            _effectivenessEngine = effectivenessEngine ?? throw new ArgumentNullException(nameof(effectivenessEngine));
            _insightEngine = insightEngine ?? throw new ArgumentNullException(nameof(insightEngine));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PURE core — assemble from already-fetched inputs. No IO. All numeric
        // behaviour is unit-testable against this method.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Assembles an <see cref="OutcomeProfile"/> from already-fetched inputs. Pure and
        /// total: any input (including empty / null sub-pieces) yields a well-formed,
        /// clamped profile.
        ///
        /// Rows whose development composite is exactly 0 (pre-migration sentinel) are NOT
        /// treated as evidence: <see cref="OutcomeProfile.HasEnoughData"/> only reflects the
        /// development layer when its composite is &gt; 0 AND it has enough data.
        /// </summary>
        /// <param name="userId">The user this snapshot is for.</param>
        /// <param name="generatedAt">Snapshot timestamp (UTC).</param>
        /// <param name="goals">Legacy goal rows (active goals). May be empty/null.</param>
        /// <param name="goalProfile">The user's voice goal profile, for PrimaryFocus. May be null.</param>
        /// <param name="recoveryForecast">The predictive recovery forecast, or null if unavailable.</param>
        /// <param name="effectivenessProfiles">All evaluated exercise effectiveness profiles. May be empty/null.</param>
        /// <param name="developmentProfile">The longitudinal development profile, or null if unavailable.</param>
        /// <param name="insights">The longitudinal insights, or null/empty.</param>
        public OutcomeProfile Build(
            int userId,
            DateTime generatedAt,
            IReadOnlyList<SmartCoachGoal>? goals,
            VoiceGoalProfile? goalProfile,
            RecoveryForecast? recoveryForecast,
            IReadOnlyList<ExerciseEffectivenessProfile>? effectivenessProfiles,
            VoiceDevelopmentProfile? developmentProfile,
            IReadOnlyList<LongitudinalInsight>? insights)
        {
            var goalProgress = BuildGoalProgress(goals, goalProfile);
            var recoveryProgress = BuildRecoveryProgress(recoveryForecast);
            var effectiveness = BuildEffectiveness(effectivenessProfiles);
            var longTerm = BuildLongTermDevelopment(developmentProfile, insights);

            // HasEnoughData is an OR across layers — any one trustworthy signal qualifies.
            // The development layer only counts when its composite is > 0 (pre-migration
            // rows carry composite == 0 and must NOT be read as evidence) AND it has enough
            // data.
            var goalsHaveEvidence = goalProgress.Goals.Any(g => g.TargetValue > 0.0);
            var recoveryHasEvidence = recoveryForecast.HasValue;
            var effectivenessHasEvidence = effectiveness.Ranked.Count > 0;
            var developmentHasEvidence =
                developmentProfile is not null &&
                developmentProfile.HasEnoughData &&
                developmentProfile.CompositeVoiceScore > 0.0;

            var hasEnoughData =
                goalsHaveEvidence ||
                recoveryHasEvidence ||
                effectivenessHasEvidence ||
                developmentHasEvidence;

            return new OutcomeProfile
            {
                UserId = userId,
                GeneratedAt = generatedAt,
                GoalProgress = goalProgress,
                RecoveryProgress = recoveryProgress,
                ExerciseEffectiveness = effectiveness,
                LongTermDevelopment = longTerm,
                HasEnoughData = hasEnoughData
            };
        }

        // ── GOALS ─────────────────────────────────────────────────────────────────

        private static GoalProgress BuildGoalProgress(
            IReadOnlyList<SmartCoachGoal>? goals,
            VoiceGoalProfile? goalProfile)
        {
            if (goals is null || goals.Count == 0)
                return new GoalProgress();

            // The goal profile carries ONE PrimaryFocus for the user; goals not matching a
            // recognised focus fall back to a mapping of their own GoalType token.
            var profileFocus = goalProfile is null
                ? (VoiceDimension?)null
                : MapDimension(goalProfile.PrimaryFocus);

            var entries = new List<GoalProgressEntry>(goals.Count);
            foreach (var goal in goals)
            {
                if (goal is null) continue;

                var target = goal.TargetValue;
                var current = goal.CurrentValue;
                var delta = target - current;

                // PercentComplete: current / target on a 0–100 scale, clamped. A 0/negative
                // target carries no meaningful progress fraction ⇒ 0 (avoids divide-by-zero).
                double percent;
                if (target <= 0.0)
                    percent = 0.0;
                else
                    percent = Math.Clamp(current / target * 100.0, 0.0, 100.0);

                var achieved = target > 0.0 && current >= target;
                if (achieved) percent = 100.0;

                // PrimaryFocus: prefer the user's explicit goal-profile focus; otherwise
                // map the goal's own type token.
                var focus = profileFocus ?? MapDimension(goal.GoalType);

                entries.Add(new GoalProgressEntry
                {
                    GoalType = goal.GoalType ?? string.Empty,
                    PrimaryFocus = focus,
                    TargetValue = target,
                    CurrentValue = current,
                    DeltaToGoal = delta,
                    PercentComplete = percent,
                    IsAchieved = achieved
                });
            }

            return new GoalProgress { Goals = entries };
        }

        /// <summary>
        /// Maps a string focus / goal-type token to a <see cref="VoiceDimension"/>. Mirrors
        /// the SmartCoach FocusAreaToDimension convention (case-insensitive); unknown tokens
        /// fall back to Resonance, the primary feminisation dimension.
        /// </summary>
        private static VoiceDimension MapDimension(string? token)
        {
            return (token ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "recovery"    => VoiceDimension.Recovery,
                "comfort"     => VoiceDimension.Comfort,
                "resonance"   => VoiceDimension.Resonance,
                "consistency" => VoiceDimension.Consistency,
                "intonation"  => VoiceDimension.Intonation,
                "vocalweight" => VoiceDimension.VocalWeight,
                "pitch"       => VoiceDimension.Pitch,
                "balanced"    => VoiceDimension.Resonance,
                "breathing"   => VoiceDimension.Comfort,
                _             => VoiceDimension.Resonance,
            };
        }

        // ── RECOVERY ────────────────────────────────────────────────────────────────

        private static RecoveryProgress BuildRecoveryProgress(RecoveryForecast? forecast)
        {
            if (!forecast.HasValue)
                return new RecoveryProgress();

            var f = forecast.Value;
            return new RecoveryProgress
            {
                CurrentScore0to100 = f.Current.Score,
                Status = f.Current.Status.ToString(),
                OvertrainingPredicted = f.OvertrainingPredicted,
                RecoveryDebt = f.RecoveryDebt,
                AcuteChronicWorkloadRatio = f.AcuteChronicWorkloadRatio,
                Severity = f.Severity.ToString(),
                RecommendationText = f.Recommendation ?? string.Empty
            };
        }

        // ── EXERCISE EFFECTIVENESS ──────────────────────────────────────────────────

        private ExerciseEffectivenessSummary BuildEffectiveness(
            IReadOnlyList<ExerciseEffectivenessProfile>? profiles)
        {
            if (profiles is null || profiles.Count == 0)
                return new ExerciseEffectivenessSummary();

            // RankMostEffective drops under-evidenced profiles by default; FlagConcerns
            // only considers data-bearing profiles. Both are the engine's own authorities.
            var ranked = _effectivenessEngine.RankMostEffective(profiles);
            var concerns = _effectivenessEngine.FlagConcerns(profiles);

            return new ExerciseEffectivenessSummary
            {
                Ranked = ranked,
                Concerns = concerns
            };
        }

        // ── LONG-TERM DEVELOPMENT ───────────────────────────────────────────────────

        private static LongTermDevelopment BuildLongTermDevelopment(
            VoiceDevelopmentProfile? profile,
            IReadOnlyList<LongitudinalInsight>? insights)
        {
            if (profile is null)
            {
                return new LongTermDevelopment
                {
                    Insights = insights ?? Array.Empty<LongitudinalInsight>()
                };
            }

            return new LongTermDevelopment
            {
                WeeklyTrend = profile.WeeklyTrend,
                MonthlyTrend = profile.MonthlyTrend,
                CompositeVoiceScore = profile.CompositeVoiceScore,
                Plateau = profile.Plateau,
                Breakthrough = profile.Breakthrough,
                Regression = profile.Regression,
                Insights = insights ?? Array.Empty<LongitudinalInsight>()
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // STORE path — gather inputs from the live engines/services, then Build().
        // Thin: it only does reads + the same join the pure Build documents.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gathers every input from the live engines/services and assembles an
        /// <see cref="OutcomeProfile"/>. Each source is best-effort: a null/failed source
        /// degrades that block to its empty state rather than failing the whole profile.
        /// </summary>
        /// <param name="database">Legacy database, for the active goal rows.</param>
        /// <param name="goalProfileProvider">Provider for the user's voice goal profile (PrimaryFocus).</param>
        /// <param name="recoveryService">Predictive recovery intelligence.</param>
        /// <param name="analyticsStore">Session analytics store (recovery snapshot source).</param>
        /// <param name="now">Snapshot timestamp.</param>
        /// <param name="userId">The user this snapshot is for.</param>
        /// <param name="cancellationToken">Cancellation.</param>
        public async Task<OutcomeProfile> AssembleFromStoreAsync(
            IDatabaseService database,
            IVoiceGoalProfileProvider? goalProfileProvider,
            RecoveryIntelligenceService recoveryService,
            SessionAnalyticsStore analyticsStore,
            DateTime now,
            int userId = 1,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(recoveryService);
            ArgumentNullException.ThrowIfNull(analyticsStore);

            // 1) GOALS — legacy active goals + the user's goal profile (PrimaryFocus).
            IReadOnlyList<SmartCoachGoal> goals;
            try { goals = database.GetSmartCoachGoals(userId, activeOnly: true); }
            catch { goals = Array.Empty<SmartCoachGoal>(); }

            VoiceGoalProfile? goalProfile = null;
            if (goalProfileProvider is not null)
            {
                try { goalProfile = goalProfileProvider.GetProfile(userId); }
                catch { goalProfile = null; }
            }

            // 2) RECOVERY — predictive forecast from persisted history.
            RecoveryForecast? recoveryForecast = null;
            try
            {
                recoveryForecast = await recoveryService
                    .ForecastFromHistoryAsync(analyticsStore, now, userId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch { recoveryForecast = null; }

            // 3) EXERCISE EFFECTIVENESS — evaluate every real catalog exercise.
            IReadOnlyList<ExerciseEffectivenessProfile>? effectiveness = null;
            try
            {
                effectiveness = await _effectivenessEngine
                    .EvaluateAllAsync(now, userId, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch { effectiveness = null; }

            // 4) LONG-TERM DEVELOPMENT — use TryBuildDevelopmentProfile (it populates
            //    Plateau/Breakthrough/Regression via the injected VoicePatternDetector).
            VoiceDevelopmentProfile? development = null;
            IReadOnlyList<LongitudinalInsight>? insights = null;
            try
            {
                development = _smartCoachEngine.TryBuildDevelopmentProfile(userId, now);
                if (development is not null)
                {
                    var allWindows = development.WeeklyTrend.Concat(development.MonthlyTrend).ToList();
                    insights = _insightEngine.Compute(
                        allWindows,
                        development.Plateau,
                        development.Breakthrough,
                        development.Regression,
                        development);
                }
            }
            catch
            {
                development = null;
                insights = null;
            }

            return Build(
                userId,
                now,
                goals,
                goalProfile,
                recoveryForecast,
                effectiveness,
                development,
                insights);
        }
    }
}
