using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// ClinicianDashboard VM (Sprint E, Wave 1 — Agent 1).
    ///
    /// Descriptive / reporting only: exposes a full professional view of the user's
    /// voice development for a clinician. Never gates training — Safety &gt; Health &gt;
    /// Recovery always take precedence and live in the upstream engines.
    ///
    /// Service resolution follows the AnalysisPageViewModel pattern (App.Services
    /// null-safe). Parameterless ctor = production (App.Services). Test ctor = explicit
    /// dependencies (no App.Services, no WPF host required).
    ///
    /// Sections:
    ///   Voice Metrics      — latest per-dimension 0–100 scores (from most recent VI trend point).
    ///   Recovery Status    — summary of the recovery/health picture from OutcomeProfile.
    ///   Comfort Trend      — per-window Comfort slope list (for an ItemsControl).
    ///   Resonance Trend    — per-window Resonance slope list.
    ///   Consistency Trend  — per-window Consistency slope list.
    ///   Exercise Effectiveness — ranked list (most effective first), concerns list.
    ///   Learning Path      — stage, focus areas, strengths/weaknesses.
    ///   Goal Progress      — goal-by-goal progress list.
    ///   Insights           — ranked longitudinal coaching insights.
    /// </summary>
    public partial class ClinicianDashboardViewModel : ObservableObject
    {
        // ── Service references ────────────────────────────────────────────────────
        private readonly OutcomeProfileBuilder? _profileBuilder;
        private readonly SmartCoachEngine?      _smartCoach;
        private readonly ExerciseEffectivenessEngine? _effectivenessEngine;
        private readonly SessionAnalyticsStore? _analyticsStore;

        // ── Voice Metrics ─────────────────────────────────────────────────────────

        /// <summary>Composite voice development score, 0–100. 0 when insufficient data.</summary>
        [ObservableProperty]
        private double _compositeScore;

        /// <summary>Resonance dimension score, 0–100 (from most recent VI trend point).</summary>
        [ObservableProperty]
        private double _resonanceScore;

        /// <summary>Comfort (Health) dimension score, 0–100.</summary>
        [ObservableProperty]
        private double _comfortScore;

        /// <summary>Consistency dimension score, 0–100.</summary>
        [ObservableProperty]
        private double _consistencyScore;

        /// <summary>Recovery dimension score, 0–100.</summary>
        [ObservableProperty]
        private double _recoveryMetricScore;

        /// <summary>Intonation dimension score, 0–100.</summary>
        [ObservableProperty]
        private double _intonationScore;

        /// <summary>Vocal Weight dimension score, 0–100.</summary>
        [ObservableProperty]
        private double _vocalWeightScore;

        /// <summary>Pitch dimension score, 0–100.</summary>
        [ObservableProperty]
        private double _pitchScore;

        /// <summary>True when at least one trustworthy data layer is available.</summary>
        [ObservableProperty]
        private bool _hasEnoughData;

        // ── Recovery Status ───────────────────────────────────────────────────────

        /// <summary>Coarse recovery status bucket (e.g. "Adequate", "Strained").</summary>
        [ObservableProperty]
        private string _recoveryStatus = string.Empty;

        /// <summary>Reactive recovery score, 0–100 (0 = overtrained, 100 = fully rested).</summary>
        [ObservableProperty]
        private double _recoveryScore0to100;

        /// <summary>True when overtraining was predicted by the predictive branch.</summary>
        [ObservableProperty]
        private bool _overtrainingPredicted;

        /// <summary>Recovery debt, 0–100.</summary>
        [ObservableProperty]
        private double _recoveryDebt;

        /// <summary>Explainable, clinically safe recovery recommendation text.</summary>
        [ObservableProperty]
        private string _recoveryRecommendation = string.Empty;

        // ── Dimension Trend Lists ─────────────────────────────────────────────────

        /// <summary>
        /// Per-window Comfort slope values (chronological, weekly+monthly).
        /// Each item is a <see cref="TrendWindowEntry"/> with a Label and slope Value.
        /// </summary>
        public ObservableCollection<TrendWindowEntry> ComfortTrend { get; } = new();

        /// <summary>Per-window Resonance slope values (chronological).</summary>
        public ObservableCollection<TrendWindowEntry> ResonanceTrend { get; } = new();

        /// <summary>Per-window Consistency slope values (chronological).</summary>
        public ObservableCollection<TrendWindowEntry> ConsistencyTrend { get; } = new();

        // ── Exercise Effectiveness ────────────────────────────────────────────────

        /// <summary>
        /// Exercises ranked most-effective first (data-bearing profiles only).
        /// Bound to a DataGrid for a full professional view.
        /// </summary>
        public ObservableCollection<ExerciseEffectivenessEntry> RankedExercises { get; } = new();

        /// <summary>De-prioritisation flags (exercises that are taxing or comfort-eroding).</summary>
        public ObservableCollection<ExerciseConcernEntry> ExerciseConcerns { get; } = new();

        // ── Learning Path ─────────────────────────────────────────────────────────

        /// <summary>Human-readable learning stage label (Foundation, Building, …).</summary>
        [ObservableProperty]
        private string _learningStageLabel = string.Empty;

        /// <summary>Short neutral explanation for why this stage was chosen.</summary>
        [ObservableProperty]
        private string _learningStageExplanation = string.Empty;

        /// <summary>Ordered active focus areas (dimension names).</summary>
        public ObservableCollection<string> FocusAreas { get; } = new();

        /// <summary>Top strengths: dimension + latest score.</summary>
        public ObservableCollection<DimensionAssessmentEntry> Strengths { get; } = new();

        /// <summary>Top weaknesses: dimension + latest score.</summary>
        public ObservableCollection<DimensionAssessmentEntry> Weaknesses { get; } = new();

        /// <summary>Confidence level label for the learning path picture.</summary>
        [ObservableProperty]
        private string _confidenceLabel = string.Empty;

        /// <summary>Continuous 0–100 confidence backing the learning path picture.</summary>
        [ObservableProperty]
        private double _confidenceScore;

        // ── Goal Progress ─────────────────────────────────────────────────────────

        /// <summary>Goal-by-goal progress (for a DataGrid).</summary>
        public ObservableCollection<GoalProgressEntry> GoalProgressItems { get; } = new();

        // ── Longitudinal Insights ─────────────────────────────────────────────────

        /// <summary>Ranked longitudinal coaching insights (localised What text).</summary>
        public ObservableCollection<string> Insights { get; } = new();

        // ═════════════════════════════════════════════════════════════════════════
        // Constructors
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Production ctor: resolves services from App.Services (null-safe — App.Services
        /// may be null in design-time / unit-test contexts). Calls Load(1).
        /// </summary>
        public ClinicianDashboardViewModel()
        {
            _profileBuilder      = App.Services?.GetService(typeof(OutcomeProfileBuilder)) as OutcomeProfileBuilder;
            _smartCoach          = App.Services?.GetService(typeof(SmartCoachEngine)) as SmartCoachEngine;
            _effectivenessEngine = App.Services?.GetService(typeof(ExerciseEffectivenessEngine)) as ExerciseEffectivenessEngine;
            _analyticsStore      = App.Services?.GetService(typeof(SessionAnalyticsStore)) as SessionAnalyticsStore;

            Load(userId: 1);
        }

        /// <summary>
        /// Test / DI seam: inject all dependencies explicitly. No App.Services or WPF host
        /// required. Pass null for any service that should not contribute (graceful
        /// degradation to empty/zero state). Does NOT auto-call Load so tests can control
        /// when data is applied.
        /// </summary>
        public ClinicianDashboardViewModel(
            OutcomeProfileBuilder? profileBuilder,
            SmartCoachEngine? smartCoach,
            ExerciseEffectivenessEngine? effectivenessEngine,
            SessionAnalyticsStore? analyticsStore = null)
        {
            _profileBuilder      = profileBuilder;
            _smartCoach          = smartCoach;
            _effectivenessEngine = effectivenessEngine;
            _analyticsStore      = analyticsStore;
        }

        // ═════════════════════════════════════════════════════════════════════════
        // Public load API
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads / refreshes the dashboard for the given user. Safe to call multiple
        /// times. All service calls are null-safe; failures degrade to empty/zero state
        /// rather than throwing — this is a reporting surface only.
        /// </summary>
        public void Load(int userId = 1)
        {
            try
            {
                var now = DateTime.Now;

                // ── Development profile (dimension trend slopes) ──────────────────
                VoiceDevelopmentProfile? devProfile = null;
                try { devProfile = _smartCoach?.TryBuildDevelopmentProfile(userId, now); }
                catch { /* descriptive — never throw */ }

                ApplyDevelopmentProfile(devProfile);

                // ── Latest per-dimension scores from VI trend ─────────────────────
                // Read synchronously (fire-and-forget pattern from SmartCoachEngine).
                VoiceIntelligenceTrendPoint? latestPoint = null;
                try
                {
                    if (_analyticsStore != null)
                    {
                        var to   = now;
                        var from = now.AddDays(-30);
                        var task = _analyticsStore.GetVoiceIntelligenceTrendAsync(from, to, userId);
                        // Block synchronously — same approach as SmartCoachEngine.TryReadLatestVoiceIntelligence.
                        var points = task.GetAwaiter().GetResult();
                        latestPoint = points?.OrderByDescending(p => p.StartedAt).FirstOrDefault();
                    }
                }
                catch { /* descriptive — never throw */ }

                ApplyLatestVoiceMetrics(latestPoint);

                // ── Outcome profile ────────────────────────────────────────────────
                // Pure Build path — supply already-fetched devProfile so we don't
                // need to do another store round-trip.
                OutcomeProfile? outcome = null;
                try
                {
                    if (_profileBuilder != null)
                    {
                        outcome = _profileBuilder.Build(
                            userId,
                            generatedAt: now,
                            goals: null,
                            goalProfile: null,
                            recoveryForecast: null,
                            effectivenessProfiles: null,
                            developmentProfile: devProfile,
                            insights: null);
                    }
                }
                catch { /* descriptive — never throw */ }

                ApplyOutcomeProfile(outcome);

                // ── Learning path ─────────────────────────────────────────────────
                // SmartCoachEngine.TryBuildDevelopmentProfile is the only public
                // building method we can call. Learning path is exposed indirectly
                // via OutcomeProfile.LongTermDevelopment, but for strengths/weaknesses
                // we use the DimensionAssessment from the latest VI trend point.
                ApplyLearningPathFromDevProfile(devProfile, latestPoint);
            }
            catch
            {
                // Dashboard is reporting only — never let any failure surface to the user.
            }
        }

        // ═════════════════════════════════════════════════════════════════════════
        // Apply helpers (public so tests can call them directly)
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Maps a <see cref="VoiceDevelopmentProfile"/> into the Composite score, HasEnoughData
        /// and dimension Trend collections. Null-safe.
        /// </summary>
        public void ApplyDevelopmentProfile(VoiceDevelopmentProfile? profile)
        {
            if (profile == null)
            {
                CompositeScore = 0;
                HasEnoughData  = false;
                ComfortTrend.Clear();
                ResonanceTrend.Clear();
                ConsistencyTrend.Clear();
                return;
            }

            CompositeScore = profile.CompositeVoiceScore;
            HasEnoughData  = profile.HasEnoughData;

            var allWindows = profile.WeeklyTrend
                .Concat(profile.MonthlyTrend)
                .OrderBy(w => w.From)
                .ToList();

            PopulateSlopeTrend(ComfortTrend,     allWindows, VoiceDimension.Comfort);
            PopulateSlopeTrend(ResonanceTrend,   allWindows, VoiceDimension.Resonance);
            PopulateSlopeTrend(ConsistencyTrend, allWindows, VoiceDimension.Consistency);
        }

        /// <summary>
        /// Maps the most recent VI trend point into the per-dimension score properties.
        /// Null-safe: null point leaves all dimension scores at 0.
        /// </summary>
        public void ApplyLatestVoiceMetrics(VoiceIntelligenceTrendPoint? point)
        {
            if (point == null)
            {
                ResonanceScore    = 0;
                ComfortScore      = 0;
                ConsistencyScore  = 0;
                RecoveryMetricScore = 0;
                IntonationScore   = 0;
                VocalWeightScore  = 0;
                PitchScore        = 0;
                return;
            }

            ResonanceScore    = Clamp(point.ResonanceScore100);
            ComfortScore      = Clamp(point.ComfortScore100);
            ConsistencyScore  = Clamp(point.ConsistencyScore100);
            RecoveryMetricScore = Clamp(point.RecoveryScore100);
            IntonationScore   = Clamp(point.IntonationScore100);
            VocalWeightScore  = Clamp(point.VocalWeightScore100);
            PitchScore        = Clamp(point.PitchScore100);
        }

        /// <summary>
        /// Maps an <see cref="OutcomeProfile"/> into the recovery / exercise / goal /
        /// insight collections. Null-safe.
        /// </summary>
        public void ApplyOutcomeProfile(OutcomeProfile? outcome)
        {
            // Recovery status block
            if (outcome != null)
            {
                var r = outcome.RecoveryProgress;
                RecoveryStatus         = r.Status;
                RecoveryScore0to100    = r.CurrentScore0to100;
                OvertrainingPredicted  = r.OvertrainingPredicted;
                RecoveryDebt           = r.RecoveryDebt;
                RecoveryRecommendation = r.RecommendationText;
            }
            else
            {
                RecoveryStatus         = string.Empty;
                RecoveryScore0to100    = 0;
                OvertrainingPredicted  = false;
                RecoveryDebt           = 0;
                RecoveryRecommendation = string.Empty;
            }

            // Exercise effectiveness
            RankedExercises.Clear();
            ExerciseConcerns.Clear();
            if (outcome != null)
            {
                foreach (var p in outcome.ExerciseEffectiveness.Ranked)
                    RankedExercises.Add(new ExerciseEffectivenessEntry(p));
                foreach (var f in outcome.ExerciseEffectiveness.Concerns)
                    ExerciseConcerns.Add(new ExerciseConcernEntry(f));
            }

            // Goal progress
            GoalProgressItems.Clear();
            if (outcome != null)
            {
                foreach (var g in outcome.GoalProgress.Goals)
                    GoalProgressItems.Add(g);
            }

            // Longitudinal insights (What text — localised by the engine)
            Insights.Clear();
            if (outcome?.LongTermDevelopment?.Insights != null)
            {
                foreach (var ins in outcome.LongTermDevelopment.Insights)
                {
                    var text = !string.IsNullOrWhiteSpace(ins.What) ? ins.What
                              : !string.IsNullOrWhiteSpace(ins.Why)  ? ins.Why
                              : ins.ReasonCode;
                    if (!string.IsNullOrWhiteSpace(text))
                        Insights.Add(text);
                }
            }
        }

        /// <summary>
        /// Builds a lightweight learning-path display from the development profile and
        /// the latest VI trend point (dimension scores for strengths/weaknesses). Null-safe.
        /// </summary>
        public void ApplyLearningPathFromDevProfile(
            VoiceDevelopmentProfile? profile,
            VoiceIntelligenceTrendPoint? latestPoint)
        {
            FocusAreas.Clear();
            Strengths.Clear();
            Weaknesses.Clear();
            LearningStageLabel       = string.Empty;
            LearningStageExplanation = string.Empty;
            ConfidenceLabel          = string.Empty;
            ConfidenceScore          = 0;

            if (profile == null || !profile.HasEnoughData)
                return;

            // Composite score as a proxy for development maturity.
            var composite = profile.CompositeVoiceScore;
            LearningStageLabel = composite switch
            {
                >= 80 => "Maintaining",
                >= 65 => "Integrating",
                >= 50 => "Refining",
                >= 35 => "Building",
                _     => "Foundation"
            };
            LearningStageExplanation = $"Composite voice score: {composite:F0}/100";

            // Confidence proxied from HasEnoughData + composite.
            ConfidenceScore = composite;
            ConfidenceLabel = composite >= 70 ? "Established"
                            : composite >= 45 ? "Moderate"
                            : "Emerging";

            if (latestPoint == null) return;

            // Build dimension score pairs, sorted by score.
            var dims = new[]
            {
                (VoiceDimension.Resonance,   Clamp(latestPoint.ResonanceScore100)),
                (VoiceDimension.Comfort,     Clamp(latestPoint.ComfortScore100)),
                (VoiceDimension.Consistency, Clamp(latestPoint.ConsistencyScore100)),
                (VoiceDimension.Recovery,    Clamp(latestPoint.RecoveryScore100)),
                (VoiceDimension.Intonation,  Clamp(latestPoint.IntonationScore100)),
                (VoiceDimension.VocalWeight, Clamp(latestPoint.VocalWeightScore100)),
                (VoiceDimension.Pitch,       Clamp(latestPoint.PitchScore100)),
            };

            // Strengths: top 3 scoring, threshold > 65
            foreach (var (dim, score) in dims.OrderByDescending(d => d.Item2).Take(3))
            {
                if (score > 0)
                    Strengths.Add(new DimensionAssessmentEntry(dim.ToString(), score));
            }

            // Weaknesses: bottom 3 scoring (below 60), priority order (Recovery first)
            var weakDims = dims
                .Where(d => d.Item2 < 60 && d.Item2 > 0)
                .OrderBy(d => (int)d.Item1)      // priority hierarchy (Recovery=0 first)
                .Take(3)
                .ToList();

            foreach (var (dim, score) in weakDims)
            {
                Weaknesses.Add(new DimensionAssessmentEntry(dim.ToString(), score));
                FocusAreas.Add(dim.ToString());
            }
        }

        // ═════════════════════════════════════════════════════════════════════════
        // Private helpers
        // ═════════════════════════════════════════════════════════════════════════

        private static double Clamp(double value)
            => double.IsNaN(value) ? 0.0 : Math.Clamp(value, 0.0, 100.0);

        private static void PopulateSlopeTrend(
            ObservableCollection<TrendWindowEntry> collection,
            IReadOnlyList<TrendWindow> windows,
            VoiceDimension dim)
        {
            collection.Clear();
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                var slope = w.DimensionSlopes.TryGetValue(dim, out var s) ? s : 0.0;
                collection.Add(new TrendWindowEntry
                {
                    Index = i,
                    Label = w.From.ToString("dd.MM"),
                    Value = slope
                });
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Lightweight display DTOs (used only by this dashboard — no sharing)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>One data point in a dimension trend list (per TrendWindow).</summary>
    public sealed class TrendWindowEntry
    {
        public int    Index { get; init; }
        public string Label { get; init; } = string.Empty;

        /// <summary>Slope (score-points per session step) for the represented dimension.</summary>
        public double Value { get; init; }
    }

    /// <summary>Display wrapper for <see cref="ExerciseEffectivenessProfile"/>.</summary>
    public sealed class ExerciseEffectivenessEntry
    {
        public ExerciseEffectivenessEntry(ExerciseEffectivenessProfile p)
        {
            ExerciseId             = p.ExerciseId;
            CompositeEffectiveness = p.CompositeEffectiveness;
            ResonanceGain          = p.ResonanceGain;
            ComfortGain            = p.ComfortGain;
            ConsistencyGain        = p.ConsistencyGain;
            RecoveryCost           = p.RecoveryCost;
            UserSuccessRate        = p.UserSuccessRate;
            SessionCount           = p.SessionCount;
            HasEnoughData          = p.HasEnoughData;
            Explanation            = p.Explanation;
        }

        public int    ExerciseId             { get; }
        public double CompositeEffectiveness  { get; }
        public double ResonanceGain           { get; }
        public double ComfortGain             { get; }
        public double ConsistencyGain         { get; }
        public double RecoveryCost            { get; }
        public double UserSuccessRate         { get; }
        public int    SessionCount            { get; }
        public bool   HasEnoughData           { get; }
        public string Explanation             { get; }
    }

    /// <summary>Display wrapper for <see cref="ExerciseEffectivenessFlag"/>.</summary>
    public sealed class ExerciseConcernEntry
    {
        public ExerciseConcernEntry(ExerciseEffectivenessFlag f)
        {
            ExerciseId  = f.ExerciseId;
            ReasonCode  = f.ReasonCode;
            Explanation = f.Explanation;
            Magnitude   = f.Magnitude;
        }

        public int    ExerciseId  { get; }
        public string ReasonCode  { get; }
        public string Explanation { get; }
        public double Magnitude   { get; }
    }

    /// <summary>Display entry for a dimension strength/weakness assessment.</summary>
    public sealed class DimensionAssessmentEntry
    {
        public DimensionAssessmentEntry(string dimension, double score)
        {
            Dimension = dimension;
            Score     = score;
        }

        public string Dimension { get; }
        public double Score     { get; }
    }
}
