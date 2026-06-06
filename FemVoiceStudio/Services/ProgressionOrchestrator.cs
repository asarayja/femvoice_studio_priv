using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    public enum ProgressionOrchestratorDecisionKind
    {
        Maintain,
        DifficultyAdjustmentSuggested,
        ExerciseProfileUpdated,
        ProgressionPaused,
        RegressionTriggered,
        PlateauDetected
    }

    public enum ProgressionAdjustmentDimension
    {
        None,
        Resonance,
        Stability,
        HoldLength,
        PitchComfort,
        Recovery,
        ExerciseVariation
    }

    public sealed record ProgressionOrchestratorOptions
    {
        public int MinimumSessionsForDecision { get; init; } = 3;
        public int PlateauSessionThreshold { get; init; } = 6;
        public int LookbackDays { get; init; } = 28;
        public double ImprovementThreshold { get; init; } = 0.05;
        public double RegressionThreshold { get; init; } = 0.08;
        public double PlateauTolerance { get; init; } = 0.02;
        public double MinimumStableResonance { get; init; } = 0.65;
        public double MinimumStableStability { get; init; } = 0.60;
        public double MinimumHoldCompletion { get; init; } = 0.70;
        public int MaxSafetyEventsBeforeRegression { get; init; } = 2;
        public int MaxFatigueIndicatorsBeforePause { get; init; } = 2;
        public int MinimumSessionsForVariation { get; init; } = 8;
        public double VariationDominanceRatio { get; init; } = 0.75;
    }

    public sealed record ProgressionOrchestratorContext
    {
        public int UserId { get; init; } = 1;
        public int ExerciseId { get; init; }
        public ExerciseTargetProfile CurrentProfile { get; init; } = ExerciseTargetProfile.CreateResonanceHumming();
        public DateTime EvaluationTime { get; init; } = DateTime.Now;
        public SubjectiveReport? SubjectiveReport { get; init; }
    }

    public sealed record ProgressionOrchestratorDecision
    {
        public ProgressionOrchestratorDecisionKind Kind { get; init; }
        public ProgressionAdjustmentDimension Dimension { get; init; }
        public string ReasonCode { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public double Confidence { get; init; }
        public ExerciseTargetProfile? SuggestedProfile { get; init; }
        public int SessionsAnalyzed { get; init; }
        public double RecentCompositeScore { get; init; }
        public double BaselineCompositeScore { get; init; }

        public static ProgressionOrchestratorDecision Maintain(string reasonCode, string reason, int sessionsAnalyzed = 0)
            => new()
            {
                Kind = ProgressionOrchestratorDecisionKind.Maintain,
                Dimension = ProgressionAdjustmentDimension.None,
                ReasonCode = reasonCode,
                Reason = reason,
                SessionsAnalyzed = sessionsAnalyzed
            };
    }

    public sealed class ProgressionOrchestrator
    {
        private readonly SessionAnalyticsStore _analyticsStore;
        private readonly ProgressionOrchestratorOptions _options;

        public ProgressionOrchestrator(
            SessionAnalyticsStore analyticsStore,
            ProgressionOrchestratorOptions? options = null)
        {
            _analyticsStore = analyticsStore ?? throw new ArgumentNullException(nameof(analyticsStore));
            _options = options ?? new ProgressionOrchestratorOptions();
        }

        public event EventHandler<ProgressionOrchestratorDecision>? DifficultyAdjustmentSuggested;
        public event EventHandler<ProgressionOrchestratorDecision>? ExerciseProfileUpdated;
        public event EventHandler<ProgressionOrchestratorDecision>? ProgressionPaused;
        public event EventHandler<ProgressionOrchestratorDecision>? RegressionTriggered;
        public event EventHandler<ProgressionOrchestratorDecision>? PlateauDetected;

        public async Task<ProgressionOrchestratorDecision> EvaluateAsync(
            ProgressionOrchestratorContext context,
            CancellationToken cancellationToken = default)
        {
            if (context.ExerciseId <= 0)
            {
                throw new ArgumentException("ExerciseId must be positive.", nameof(context));
            }

            var subjectiveDecision = EvaluateSubjectiveReport(context);
            if (subjectiveDecision != null)
            {
                return subjectiveDecision;
            }

            var to = context.EvaluationTime;
            var from = to.AddDays(-_options.LookbackDays);
            var exerciseTrend = await _analyticsStore.GetExerciseTrendAsync(
                context.ExerciseId,
                from,
                to.AddTicks(1),
                context.UserId,
                cancellationToken);

            var sessions = exerciseTrend.OrderBy(s => s.StartedAt).ToList();
            if (sessions.Count < _options.MinimumSessionsForDecision)
            {
                return ProgressionOrchestratorDecision.Maintain(
                    "INSUFFICIENT_HISTORY",
                    "Not enough historical sessions to adjust progression.",
                    sessions.Count);
            }

            var dailySummaries = await LoadDailySummariesAsync(from, to, context.UserId, cancellationToken);
            var allExerciseSummaries = await _analyticsStore.GetExerciseSummariesAsync(
                from,
                to.AddTicks(1),
                context.UserId,
                cancellationToken);
            var recent = sessions.TakeLast(_options.MinimumSessionsForDecision).ToList();
            var baseline = sessions.Take(Math.Max(1, sessions.Count - recent.Count)).ToList();

            var recentComposite = CompositeScore(recent);
            var baselineComposite = baseline.Count == 0 ? recentComposite : CompositeScore(baseline);
            var recentSafetyEvents = dailySummaries.Sum(d => d.SafetyEventsCount);
            var recentFatigue = dailySummaries.Sum(d => d.FatigueIndicatorCount);

            if (recentSafetyEvents >= _options.MaxSafetyEventsBeforeRegression)
            {
                return Publish(RegressionTriggered, new ProgressionOrchestratorDecision
                {
                    Kind = ProgressionOrchestratorDecisionKind.RegressionTriggered,
                    Dimension = ProgressionAdjustmentDimension.Recovery,
                    ReasonCode = "SAFETY_EVENTS",
                    Reason = "Multiple safety events were found in recent history.",
                    Confidence = Confidence(sessions.Count),
                    SessionsAnalyzed = sessions.Count,
                    RecentCompositeScore = recentComposite,
                    BaselineCompositeScore = baselineComposite,
                    SuggestedProfile = ScaleForRecovery(context.CurrentProfile)
                });
            }

            if (recentFatigue >= _options.MaxFatigueIndicatorsBeforePause)
            {
                return Publish(ProgressionPaused, new ProgressionOrchestratorDecision
                {
                    Kind = ProgressionOrchestratorDecisionKind.ProgressionPaused,
                    Dimension = ProgressionAdjustmentDimension.Recovery,
                    ReasonCode = "FATIGUE_RISING",
                    Reason = "Fatigue indicators are too frequent for safe progression.",
                    Confidence = Confidence(sessions.Count),
                    SessionsAnalyzed = sessions.Count,
                    RecentCompositeScore = recentComposite,
                    BaselineCompositeScore = baselineComposite,
                    SuggestedProfile = context.CurrentProfile
                });
            }

            if (baseline.Count >= _options.MinimumSessionsForDecision &&
                baselineComposite - recentComposite >= _options.RegressionThreshold)
            {
                return Publish(RegressionTriggered, new ProgressionOrchestratorDecision
                {
                    Kind = ProgressionOrchestratorDecisionKind.RegressionTriggered,
                    Dimension = ProgressionAdjustmentDimension.Recovery,
                    ReasonCode = "PERFORMANCE_REGRESSION",
                    Reason = "Recent performance dropped compared with baseline.",
                    Confidence = Confidence(sessions.Count),
                    SessionsAnalyzed = sessions.Count,
                    RecentCompositeScore = recentComposite,
                    BaselineCompositeScore = baselineComposite,
                    SuggestedProfile = ScaleForRecovery(context.CurrentProfile)
                });
            }

            if (ShouldSuggestVariation(context.ExerciseId, allExerciseSummaries))
            {
                return Publish(DifficultyAdjustmentSuggested, new ProgressionOrchestratorDecision
                {
                    Kind = ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated,
                    Dimension = ProgressionAdjustmentDimension.ExerciseVariation,
                    ReasonCode = "EXERCISE_VARIATION_RECOMMENDED",
                    Reason = "Recent history is dominated by one exercise; vary the stimulus before increasing load.",
                    Confidence = Confidence(sessions.Count),
                    SessionsAnalyzed = sessions.Count,
                    RecentCompositeScore = recentComposite,
                    BaselineCompositeScore = baselineComposite,
                    SuggestedProfile = SelectVariationProfile(context.CurrentProfile)
                });
            }

            if (IsPlateau(sessions))
            {
                return Publish(PlateauDetected, new ProgressionOrchestratorDecision
                {
                    Kind = ProgressionOrchestratorDecisionKind.PlateauDetected,
                    Dimension = ProgressionAdjustmentDimension.Resonance,
                    ReasonCode = "PLATEAU_DETECTED",
                    Reason = "Performance is stable but no longer improving.",
                    Confidence = Confidence(sessions.Count),
                    SessionsAnalyzed = sessions.Count,
                    RecentCompositeScore = recentComposite,
                    BaselineCompositeScore = baselineComposite,
                    SuggestedProfile = ScaleResonance(context.CurrentProfile)
                });
            }

            if (!HasConsistentImprovement(recent, baselineComposite))
            {
                return ProgressionOrchestratorDecision.Maintain(
                    "NO_CONSISTENT_IMPROVEMENT",
                    "Progression requires consistent improvement across multiple sessions.",
                    sessions.Count) with
                    {
                        Confidence = Confidence(sessions.Count),
                        RecentCompositeScore = recentComposite,
                        BaselineCompositeScore = baselineComposite
                    };
            }

            var decision = BuildProgressionDecision(context.CurrentProfile, recent, sessions.Count, recentComposite, baselineComposite);
            return Publish(DifficultyAdjustmentSuggested, decision);
        }

        public Task<ProgressionOrchestratorDecision> OnSessionCompletedAsync(
            ProgressionOrchestratorContext context,
            CancellationToken cancellationToken = default)
            => EvaluateAsync(context, cancellationToken);

        public Task<ProgressionOrchestratorDecision> OnExercisePerformanceSummaryAsync(
            ProgressionOrchestratorContext context,
            CancellationToken cancellationToken = default)
            => EvaluateAsync(context, cancellationToken);

        public Task<ProgressionOrchestratorDecision> OnHealthTrendUpdatedAsync(
            ProgressionOrchestratorContext context,
            CancellationToken cancellationToken = default)
            => EvaluateAsync(context, cancellationToken);

        public Task<ProgressionOrchestratorDecision> OnSafetyFreezeOccurredAsync(
            ProgressionOrchestratorContext context,
            CancellationToken cancellationToken = default)
            => EvaluateAsync(context, cancellationToken);

        public Task<ProgressionOrchestratorDecision> OnPauseRecommendedAsync(
            ProgressionOrchestratorContext context,
            CancellationToken cancellationToken = default)
            => EvaluateAsync(context, cancellationToken);

        public Task<ProgressionOrchestratorDecision> OnHydrationSuggestedAsync(
            ProgressionOrchestratorContext context,
            CancellationToken cancellationToken = default)
            => EvaluateAsync(context, cancellationToken);

        public Task<ProgressionOrchestratorDecision> OnSubjectiveReportSubmittedAsync(
            ProgressionOrchestratorContext context,
            CancellationToken cancellationToken = default)
            => EvaluateAsync(context, cancellationToken);

        private ProgressionOrchestratorDecision? EvaluateSubjectiveReport(ProgressionOrchestratorContext context)
        {
            var report = context.SubjectiveReport;
            if (report == null)
                return null;

            if (report.IndicatesHealthConcern)
            {
                return Publish(ProgressionPaused, new ProgressionOrchestratorDecision
                {
                    Kind = ProgressionOrchestratorDecisionKind.ProgressionPaused,
                    Dimension = ProgressionAdjustmentDimension.Recovery,
                    ReasonCode = "SUBJECTIVE_HEALTH_CONCERN",
                    Reason = "Subjective report indicates fatigue, strain or low comfort.",
                    Confidence = 1.0,
                    SuggestedProfile = context.CurrentProfile
                });
            }

            if (!report.WantsToContinue)
            {
                return Publish(ProgressionPaused, new ProgressionOrchestratorDecision
                {
                    Kind = ProgressionOrchestratorDecisionKind.ProgressionPaused,
                    Dimension = ProgressionAdjustmentDimension.Recovery,
                    ReasonCode = "MOTIVATION_DROPPING",
                    Reason = "Subjective report indicates motivation or readiness is dropping.",
                    Confidence = 0.85,
                    SuggestedProfile = context.CurrentProfile
                });
            }

            return null;
        }

        private async Task<IReadOnlyList<DailyAnalyticsSummary>> LoadDailySummariesAsync(
            DateTime from,
            DateTime to,
            int userId,
            CancellationToken cancellationToken)
        {
            var days = new List<DailyAnalyticsSummary>();
            for (var day = DateOnly.FromDateTime(from.Date); day <= DateOnly.FromDateTime(to.Date); day = day.AddDays(1))
            {
                days.Add(await _analyticsStore.GetDailySummaryAsync(day, userId, cancellationToken));
            }

            return days;
        }

        private ProgressionOrchestratorDecision BuildProgressionDecision(
            ExerciseTargetProfile profile,
            IReadOnlyList<ExercisePerformanceSummary> recent,
            int sessionsAnalyzed,
            double recentComposite,
            double baselineComposite)
        {
            var avgResonance = recent.Average(s => s.ResonanceQualityIndex);
            var avgStability = recent.Average(s => s.StabilityConsistency);
            var avgHold = recent.Average(s => s.HoldCompletionRate);

            // Resonans-først gjelder ALLE profiler: er resonansen ennå ikke stabil,
            // konsolideres den før noen annen dimensjon skaleres. (Tidligere sto det
            // «|| profile.UsesResonance» her — da tok enhver resonansprofil alltid
            // denne grenen, og stabilitets-/hold-gatene under ble aldri nådd.)
            if (avgResonance < _options.MinimumStableResonance)
            {
                return ProfileUpdate(
                    ProgressionAdjustmentDimension.Resonance,
                    "RESONANCE_FIRST",
                    "Resonance is prioritized before pitch progression.",
                    ScaleResonance(profile),
                    sessionsAnalyzed,
                    recentComposite,
                    baselineComposite);
            }

            if (avgStability < _options.MinimumStableStability)
            {
                return ProfileUpdate(
                    ProgressionAdjustmentDimension.Stability,
                    "STABILITY_GATE",
                    "Stability must consolidate before progression continues.",
                    ScaleStability(profile),
                    sessionsAnalyzed,
                    recentComposite,
                    baselineComposite);
            }

            if (avgHold < _options.MinimumHoldCompletion && profile.RequiredHoldSeconds > 0)
            {
                return ProfileUpdate(
                    ProgressionAdjustmentDimension.HoldLength,
                    "HOLD_CONSOLIDATION",
                    "Hold quality needs consolidation before pitch changes.",
                    ScaleHoldLength(profile),
                    sessionsAnalyzed,
                    recentComposite,
                    baselineComposite);
            }

            // Alle gater bestått → øk vanskelighet langs profilens primærdimensjon.
            // Pitch alltid sist; resonansprofiler progredierer på resonans.
            if (profile.UsesPitch)
            {
                return ProfileUpdate(
                    ProgressionAdjustmentDimension.PitchComfort,
                    "PITCH_AFTER_RESONANCE_STABILITY",
                    "Difficulty can increase after stable resonance and stability history.",
                    ScalePitchComfort(profile),
                    sessionsAnalyzed,
                    recentComposite,
                    baselineComposite);
            }

            if (profile.UsesResonance)
            {
                return ProfileUpdate(
                    ProgressionAdjustmentDimension.Resonance,
                    "RESONANCE_PROGRESSION",
                    "Resonance targets can advance after a stable, safe history.",
                    ScaleResonance(profile),
                    sessionsAnalyzed,
                    recentComposite,
                    baselineComposite);
            }

            return ProfileUpdate(
                ProgressionAdjustmentDimension.Stability,
                "STABILITY_ENDURANCE",
                "Difficulty can increase after stable resonance and stability history.",
                ScaleStability(profile),
                sessionsAnalyzed,
                recentComposite,
                baselineComposite);
        }

        private ProgressionOrchestratorDecision ProfileUpdate(
            ProgressionAdjustmentDimension dimension,
            string reasonCode,
            string reason,
            ExerciseTargetProfile suggestedProfile,
            int sessionsAnalyzed,
            double recentComposite,
            double baselineComposite)
            => new()
            {
                Kind = ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated,
                Dimension = dimension,
                ReasonCode = reasonCode,
                Reason = reason,
                Confidence = Confidence(sessionsAnalyzed),
                SuggestedProfile = suggestedProfile,
                SessionsAnalyzed = sessionsAnalyzed,
                RecentCompositeScore = recentComposite,
                BaselineCompositeScore = baselineComposite
            };

        private bool HasConsistentImprovement(IReadOnlyList<ExercisePerformanceSummary> recent, double baselineComposite)
        {
            if (recent.Count < _options.MinimumSessionsForDecision)
            {
                return false;
            }

            return recent.All(s => CompositeScore(s) >= baselineComposite + _options.ImprovementThreshold);
        }

        private bool IsPlateau(IReadOnlyList<ExercisePerformanceSummary> sessions)
        {
            if (sessions.Count < _options.PlateauSessionThreshold)
            {
                return false;
            }

            var plateauWindow = sessions.TakeLast(_options.PlateauSessionThreshold).ToList();
            var firstHalf = plateauWindow.Take(_options.PlateauSessionThreshold / 2).ToList();
            var secondHalf = plateauWindow.Skip(_options.PlateauSessionThreshold / 2).ToList();
            var firstScore = CompositeScore(firstHalf);
            var secondScore = CompositeScore(secondHalf);

            return Math.Abs(secondScore - firstScore) <= _options.PlateauTolerance
                && secondHalf.Average(s => s.SafetyEventsCount) == 0
                && secondHalf.Average(s => s.FatigueIndicators) < 1;
        }

        private bool ShouldSuggestVariation(
            int exerciseId,
            IReadOnlyList<ExercisePerformanceSummary> allExerciseSummaries)
        {
            if (allExerciseSummaries.Count < _options.MinimumSessionsForVariation)
            {
                return false;
            }

            var recentWindow = allExerciseSummaries
                .TakeLast(_options.MinimumSessionsForVariation)
                .ToList();
            var currentExerciseCount = recentWindow.Count(s => s.ExerciseId == exerciseId);
            var dominanceRatio = currentExerciseCount / (double)recentWindow.Count;
            var uniqueExercises = recentWindow.Select(s => s.ExerciseId).Distinct().Count();
            var healthClear = recentWindow.Sum(s => s.SafetyEventsCount + s.FatigueIndicators) == 0;
            var technicallyReady = CompositeScore(recentWindow.Where(s => s.ExerciseId == exerciseId))
                >= _options.MinimumStableResonance;

            return uniqueExercises <= 2
                && dominanceRatio >= _options.VariationDominanceRatio
                && healthClear
                && technicallyReady;
        }

        private double Confidence(int sessionCount)
            => Math.Clamp(sessionCount / 10.0, 0.1, 1.0);

        private static double CompositeScore(IEnumerable<ExercisePerformanceSummary> summaries)
        {
            var list = summaries.ToList();
            return list.Count == 0 ? 0 : list.Average(CompositeScore);
        }

        private static double CompositeScore(ExercisePerformanceSummary summary)
        {
            return Math.Clamp(
                summary.ResonanceQualityIndex * 0.45
                + summary.StabilityConsistency * 0.35
                + summary.HoldCompletionRate * 0.20,
                0,
                1);
        }

        private ProgressionOrchestratorDecision Publish(
            EventHandler<ProgressionOrchestratorDecision>? eventHandler,
            ProgressionOrchestratorDecision decision)
        {
            eventHandler?.Invoke(this, decision);

            if (decision.Kind == ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated)
            {
                ExerciseProfileUpdated?.Invoke(this, decision);
            }

            return decision;
        }

        private static ExerciseTargetProfile ScaleResonance(ExerciseTargetProfile profile)
        {
            var min = Math.Clamp(profile.TargetResonanceMin + 0.02, 0, 0.90);
            var max = Math.Clamp(Math.Max(profile.TargetResonanceMax, min + 0.10), min, 1.0);
            return CloneProfile(profile, targetResonanceMin: min, targetResonanceMax: max);
        }

        private static ExerciseTargetProfile ScaleStability(ExerciseTargetProfile profile)
        {
            return CloneProfile(profile, stabilityThreshold: Math.Clamp(profile.StabilityThreshold + 0.02, 0, 0.90));
        }

        private static ExerciseTargetProfile ScaleHoldLength(ExerciseTargetProfile profile)
        {
            return CloneProfile(profile, requiredHoldSeconds: Math.Clamp(profile.RequiredHoldSeconds + 0.5, 0, 10));
        }

        private static ExerciseTargetProfile ScalePitchComfort(ExerciseTargetProfile profile)
        {
            if (!profile.MinPitch.HasValue || !profile.MaxPitch.HasValue)
            {
                return profile;
            }

            return CloneProfile(
                profile,
                minPitch: profile.MinPitch.Value + 1,
                maxPitch: profile.MaxPitch.Value + 1);
        }

        private static ExerciseTargetProfile ScaleForRecovery(ExerciseTargetProfile profile)
        {
            return CloneProfile(
                profile,
                targetResonanceMin: Math.Clamp(profile.TargetResonanceMin - 0.04, 0, 1),
                stabilityThreshold: Math.Clamp(profile.StabilityThreshold - 0.04, 0, 1),
                requiredHoldSeconds: Math.Max(0, profile.RequiredHoldSeconds - 1));
        }

        private static ExerciseTargetProfile SelectVariationProfile(ExerciseTargetProfile profile)
        {
            if (profile.UsesIntensity)
            {
                return ExerciseTargetProfile.ResonanceExercise(
                    targetResonanceMin: Math.Clamp(profile.TargetResonanceMin, 0.45, 0.70),
                    targetResonanceMax: Math.Clamp(profile.TargetResonanceMax, 0.80, 1.00),
                    stabilityThreshold: Math.Clamp(profile.StabilityThreshold, 0.45, 0.75),
                    requiredHoldSeconds: Math.Clamp(profile.RequiredHoldSeconds, 2, 6));
            }

            if (profile.UsesPitch)
            {
                return ExerciseTargetProfile.IntonationExercise(
                    minPitch: profile.MinPitch,
                    maxPitch: profile.MaxPitch,
                    stabilityThreshold: Math.Clamp(profile.StabilityThreshold, 0.35, 0.75));
            }

            return ExerciseTargetProfile.StrawPhonation(
                targetResonanceMin: Math.Clamp(profile.TargetResonanceMin - 0.10, 0.20, 0.80),
                targetResonanceMax: Math.Clamp(profile.TargetResonanceMax, 0.80, 1.00),
                stabilityThreshold: Math.Clamp(profile.StabilityThreshold + 0.03, 0.45, 0.80),
                requiredHoldSeconds: Math.Clamp(profile.RequiredHoldSeconds + 1, 3, 8));
        }

        private static ExerciseTargetProfile CloneProfile(
            ExerciseTargetProfile profile,
            double? targetResonanceMin = null,
            double? targetResonanceMax = null,
            double? stabilityThreshold = null,
            double? requiredHoldSeconds = null,
            double? minPitch = null,
            double? maxPitch = null)
        {
            var clone = new ExerciseTargetProfile
            {
                UsesResonance = profile.UsesResonance,
                UsesPitch = profile.UsesPitch,
                UsesStability = profile.UsesStability,
                UsesIntensity = profile.UsesIntensity,
                ClinicalPurposeKey = profile.ClinicalPurposeKey,
                PhysicalFocusKey = profile.PhysicalFocusKey,
                CommonMistakesKey = profile.CommonMistakesKey,
                SafetyInfoKey = profile.SafetyInfoKey,
                FeedbackModeKey = profile.FeedbackModeKey,
                ThresholdStrategyKey = profile.ThresholdStrategyKey,
                IndicatorPackageSummaryKey = profile.IndicatorPackageSummaryKey,
                MinPitch = minPitch ?? profile.MinPitch,
                MaxPitch = maxPitch ?? profile.MaxPitch,
                TargetResonanceMin = targetResonanceMin ?? profile.TargetResonanceMin,
                TargetResonanceMax = targetResonanceMax ?? profile.TargetResonanceMax,
                RequiredHoldSeconds = requiredHoldSeconds ?? profile.RequiredHoldSeconds,
                StabilityThreshold = stabilityThreshold ?? profile.StabilityThreshold
            };

            clone.Validate();
            return clone;
        }
    }
}
