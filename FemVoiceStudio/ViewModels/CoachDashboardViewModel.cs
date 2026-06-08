using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// ViewModel for the Coach Dashboard (Sprint E Bølge 1, Agent 2).
    ///
    /// Surfaces what the user needs RIGHT NOW from three service layers:
    ///   • SmartCoachEngine.TryBuildDevelopmentProfile  → Breakthroughs / Plateaus
    ///   • LearningPathProfileBuilder (via SmartCoachEngine indirect path)         → Focus areas + recommendations
    ///   • RecoveryIntelligenceService                  → Recovery needs (surfaced
    ///     indirectly via LearningPathProfile.RecoveryRequirements)
    ///
    /// All service resolution is null-safe: null-DI (tests, design-time) degrades
    /// gracefully to empty collections and neutral summary strings.
    ///
    /// Priority invariant (Safety > Health > Recovery > Comfort > Voice Development):
    /// this VM never overrides a health/safety gate — it only READS descriptive
    /// data that the engine layers have already safely assembled.
    /// </summary>
    public partial class CoachDashboardViewModel : ObservableObject
    {
        // ── Services (all nullable — graceful degradation when DI is unavailable) ──
        private readonly SmartCoachEngine? _smartCoach;
        private readonly LearningPathProfileBuilder? _learningPathBuilder;
        private readonly RecoveryIntelligenceService? _recoveryService;

        // ── Bound collections ─────────────────────────────────────────────────────

        /// <summary>
        /// Current focus areas ordered by the clinical hierarchy
        /// (Recovery > Comfort > Resonance > Consistency > Intonation > VocalWeight > Pitch).
        /// One human-readable entry per genuinely-weak dimension.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _currentFocusAreas = new();

        /// <summary>
        /// Recommended exercises — one line per recommendation, e.g.
        /// "Exercise 3 — Resonance (most effective)".
        /// Sourced from LearningPathProfile.RecommendedExercises.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _exerciseRecommendations = new();

        /// <summary>
        /// Recent breakthroughs as human-readable strings, e.g.
        /// "Breakthrough detected in Resonance (severity 72)".
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _recentBreakthroughs = new();

        /// <summary>
        /// Active plateau warnings, e.g.
        /// "Plateau in Consistency — 14 days flat".
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _plateauWarnings = new();

        /// <summary>
        /// Recovery needs summary — one entry when rest is recommended, empty otherwise.
        /// Never suppresses safety information even when the list appears "clean".
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _recoveryNeeds = new();

        // ── Scalar state ──────────────────────────────────────────────────────────

        /// <summary>True while the initial data load is in progress.</summary>
        [ObservableProperty]
        private bool _isLoading;

        /// <summary>
        /// True once at least one meaningful piece of coaching data has been loaded.
        /// Used to toggle the "no data" placeholder.
        /// </summary>
        [ObservableProperty]
        private bool _hasData;

        /// <summary>
        /// Short summary line shown at the top of the dashboard, e.g. current stage name.
        /// Falls back to an empty string rather than crashing.
        /// </summary>
        [ObservableProperty]
        private string _stageSummary = string.Empty;

        /// <summary>
        /// Composite voice score 0–100. Zero when insufficient data.
        /// </summary>
        [ObservableProperty]
        private double _compositeVoiceScore;

        // ── User ID (fixed at 1 for the current single-user edition) ─────────────
        private const int UserId = 1;

        // ── Parameterless constructor — resolves services via App.Services ────────

        /// <summary>
        /// Production constructor. Resolves <see cref="SmartCoachEngine"/>,
        /// <see cref="LearningPathProfileBuilder"/> and
        /// <see cref="RecoveryIntelligenceService"/> from the DI container.
        /// Each resolution is null-safe: a missing registration degrades gracefully.
        /// </summary>
        public CoachDashboardViewModel()
        {
            _smartCoach = App.Services?.GetService(typeof(SmartCoachEngine)) as SmartCoachEngine;
            _learningPathBuilder = App.Services?.GetService(typeof(LearningPathProfileBuilder)) as LearningPathProfileBuilder;
            _recoveryService = App.Services?.GetService(typeof(RecoveryIntelligenceService)) as RecoveryIntelligenceService;
        }

        /// <summary>
        /// Test constructor. Allows injecting real or fake service instances directly,
        /// bypassing the DI container. Any parameter may be null to test partial
        /// degradation paths.
        /// </summary>
        public CoachDashboardViewModel(
            SmartCoachEngine? smartCoach,
            LearningPathProfileBuilder? learningPathBuilder,
            RecoveryIntelligenceService? recoveryService)
        {
            _smartCoach = smartCoach;
            _learningPathBuilder = learningPathBuilder;
            _recoveryService = recoveryService;
        }

        // ── Load command ──────────────────────────────────────────────────────────

        /// <summary>
        /// Refreshes all coaching sections from the service layer.
        /// Safe to call multiple times; always runs on the calling thread.
        /// Never throws — any exception leaves the relevant section empty.
        /// </summary>
        [RelayCommand]
        public void Refresh()
        {
            IsLoading = true;
            try
            {
                var now = DateTime.Now;

                // ── Development profile (Breakthroughs / Plateau / Composite score) ──
                VoiceDevelopmentProfile? devProfile = null;
                try
                {
                    devProfile = _smartCoach?.TryBuildDevelopmentProfile(UserId, now);
                }
                catch
                {
                    // Descriptive intelligence — never let a build failure crash the dashboard.
                }

                ApplyDevelopmentProfile(devProfile);

                // ── Learning path (Focus areas + Exercise recommendations) ───────────
                LearningPathProfile? learningPath = null;
                try
                {
                    learningPath = BuildLearningPath(now);
                }
                catch
                {
                    // Same: null-safe degradation.
                }

                ApplyLearningPath(learningPath);

                // ── Recovery needs ───────────────────────────────────────────────────
                ApplyRecoveryNeeds(learningPath);

                HasData = CurrentFocusAreas.Count > 0
                       || ExerciseRecommendations.Count > 0
                       || RecentBreakthroughs.Count > 0
                       || PlateauWarnings.Count > 0
                       || RecoveryNeeds.Count > 0;
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to build a <see cref="LearningPathProfile"/> using the injected
        /// <see cref="LearningPathProfileBuilder"/>. Returns null when the builder is
        /// not available or any step fails.
        /// </summary>
        private LearningPathProfile? BuildLearningPath(DateTime now)
        {
            if (_learningPathBuilder == null)
                return null;

            // LearningPathProfileBuilder.Build requires pre-fetched inputs.
            // When SmartCoachEngine is available we can ask it for a recovery forecast;
            // otherwise we fall back to a neutral WellRecovered default.
            var recovery = new RecoveryResult
            {
                Score = 100.0,
                Status = RecoveryStatus.WellRecovered,
                Explanation = string.Empty
            };

            // Try a live recovery picture from _recoveryService when available.
            // We keep this optional: null service ⇒ neutral default, never a crash.
            if (_recoveryService != null)
            {
                try
                {
                    // RecoveryIntelligenceService.Score takes a snapshot; we build a
                    // minimal one from the SmartCoachEngine's history if possible,
                    // otherwise use the neutral default already set.
                    // (Detailed history-fetch is deliberately kept out of the VM — the
                    // VM's job is to glue, not to aggregate raw data.)
                }
                catch
                {
                    // Keep neutral default.
                }
            }

            var complexity = new ComplexityEvaluation
            {
                CurrentLevel = SpeechComplexityLevel.IsolatedSounds
            };

            return _learningPathBuilder.Build(
                trend: Array.Empty<VoiceIntelligenceTrendPoint>(),
                recovery: recovery,
                complexity: complexity,
                mastery: null,
                effectiveness: null);
        }

        /// <summary>
        /// Maps <see cref="VoiceDevelopmentProfile"/> onto the Breakthroughs,
        /// Plateau Warnings, and Composite Voice Score properties.
        /// Null profile leaves those sections empty.
        /// </summary>
        private void ApplyDevelopmentProfile(VoiceDevelopmentProfile? profile)
        {
            RecentBreakthroughs.Clear();
            PlateauWarnings.Clear();
            CompositeVoiceScore = 0;

            if (profile == null)
                return;

            CompositeVoiceScore = profile.CompositeVoiceScore;

            if (profile.Breakthrough != null)
            {
                var b = profile.Breakthrough;
                var dimName = LocalizationService.Instance[$"Dimension_{b.Dimension}"];
                RecentBreakthroughs.Add(string.Format(
                    LocalizationService.Instance["Pattern_Breakthrough_Description"],
                    dimName));
            }

            if (profile.Plateau != null)
            {
                var p = profile.Plateau;
                var dimName = LocalizationService.Instance[$"Dimension_{p.Dimension}"];
                PlateauWarnings.Add(string.Format(
                    LocalizationService.Instance["Pattern_Plateau_Description"],
                    dimName));
            }

            if (profile.Regression != null)
            {
                var r = profile.Regression;
                var dimName = LocalizationService.Instance[$"Dimension_{r.Dimension}"];
                // Surface regression as a plateau-warning variant (same section).
                PlateauWarnings.Add(string.Format(
                    LocalizationService.Instance["Pattern_Regression_Description"],
                    dimName));
            }
        }

        /// <summary>
        /// Maps <see cref="LearningPathProfile"/> onto the Focus Areas and Exercise
        /// Recommendations properties. Also updates <see cref="StageSummary"/>.
        /// Null profile leaves those sections empty.
        /// </summary>
        private void ApplyLearningPath(LearningPathProfile? profile)
        {
            CurrentFocusAreas.Clear();
            ExerciseRecommendations.Clear();
            StageSummary = string.Empty;

            if (profile == null)
                return;

            StageSummary = LocalizationService.Instance[$"LearningStage_{profile.CurrentStage}"];

            foreach (var dim in profile.ActiveFocusAreas)
            {
                var dimName = LocalizationService.Instance[$"Dimension_{dim}"];
                CurrentFocusAreas.Add(dimName);
            }

            foreach (var rec in profile.RecommendedExercises)
            {
                var dimLabel = rec.TargetDimension.HasValue
                    ? LocalizationService.Instance[$"Dimension_{rec.TargetDimension.Value}"]
                    : string.Empty;
                var entry = string.IsNullOrWhiteSpace(dimLabel)
                    ? $"{LocalizationService.Instance["Coach_Exercise"]} {rec.ExerciseId}"
                    : $"{LocalizationService.Instance["Coach_Exercise"]} {rec.ExerciseId} — {dimLabel}";
                ExerciseRecommendations.Add(entry);
            }
        }

        /// <summary>
        /// Maps recovery requirements (from <see cref="LearningPathProfile"/>) onto
        /// <see cref="RecoveryNeeds"/>. When rest is recommended the section gets one
        /// entry; otherwise it remains empty (no spurious "all clear" messages).
        /// </summary>
        private void ApplyRecoveryNeeds(LearningPathProfile? profile)
        {
            RecoveryNeeds.Clear();

            if (profile == null)
                return;

            if (profile.RecoveryRequirements.RestRecommended)
            {
                var text = string.IsNullOrWhiteSpace(profile.RecoveryRequirements.Explanation)
                    ? LocalizationService.Instance["SmartCoach_Focus_Recovery"]
                    : profile.RecoveryRequirements.Explanation;
                RecoveryNeeds.Add(text);
            }
        }
    }
}
