using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// ViewModel for ExerciseSummaryView - viser oktoppsummering etter hver ovelse
    /// </summary>
    public partial class ExerciseSummaryViewModel : ObservableObject
    {
        private readonly DatabaseService? _databaseService;
        private readonly ExerciseDataService? _exerciseDataService;
        private readonly SmartCoachExerciseAdapter _adapter;
        private readonly ILocalizationService _localization;
        
        // Session data
        [ObservableProperty]
        private int _sessionId;
        
        [ObservableProperty]
        private int _exerciseId;
        
        [ObservableProperty]
        private string _exerciseName = "";
        
        // Score
        [ObservableProperty]
        private double _overallScore;
        
        [ObservableProperty]
        private string _scoreText = "0%";
        
        [ObservableProperty]
        private double? _improvement;
        
        [ObservableProperty]
        private string _improvementText = "";
        
        // Parameter breakdown
        [ObservableProperty]
        private double _resonancePercent;
        
        [ObservableProperty]
        private double _pitchPercent;
        
        [ObservableProperty]
        private double _stabilityPercent;
        
        [ObservableProperty]
        private double _intonationPercent;
        
        [ObservableProperty]
        private bool _showIntonation;
        
        // Strain
        [ObservableProperty]
        private string _strainLevel = "Low";
        
        [ObservableProperty]
        private string _strainDescription = "";
        
        // Coach recommendation
        [ObservableProperty]
        private string _coachRecommendation = "";
        
        // Events
        public event EventHandler? ContinueRequested;
        public event EventHandler? FinishRequested;
        
        public ExerciseSummaryViewModel() : this(null, null)
        {
        }
        
        public ExerciseSummaryViewModel(DatabaseService? databaseService, ExerciseDataService? exerciseDataService)
        {
            _databaseService = databaseService;
            _exerciseDataService = exerciseDataService;
            _adapter = new SmartCoachExerciseAdapter();
            _localization = LocalizationService.Instance;
        }
        
        /// <summary>
        /// Last inn session summary data
        /// </summary>
        public void LoadSummary(SessionEvaluationSummary summary, Exercise exercise)
        {
            try
            {
                SessionId = summary.SessionId;
                ExerciseId = summary.ExerciseId;
                ExerciseName = exercise?.Name ?? "";
                
                // Score
                OverallScore = summary.OverallScore;
                ScoreText = $"{summary.OverallScore:F0}%";
                
                // Parameters
                ResonancePercent = summary.ResonanceCorrectPercent;
                PitchPercent = summary.PitchCorrectPercent;
                StabilityPercent = summary.StabilityCorrectPercent;
                IntonationPercent = summary.IntonationCorrectPercent;
                ShowIntonation = summary.IntonationCorrectPercent > 0;
                
                // Strain
                StrainLevel = summary.StrainLevel;
                StrainDescription = summary.StrainLevel switch
                {
                    "High" => _localization.GetString("ExerciseSummary_StrainHigh"),
                    "Moderate" => _localization.GetString("ExerciseSummary_StrainModerate"),
                    _ => _localization.GetString("ExerciseSummary_StrainLow")
                };
                
                // Calculate improvement from previous session
                Improvement = CalculateImprovement(summary.ExerciseId, summary.OverallScore);
                if (Improvement.HasValue)
                {
                    var sign = Improvement.Value >= 0 ? "+" : "";
                    ImprovementText = $"{sign}{Improvement.Value:F0}%";
                }
                
                // Generate coach recommendation
                var userLevel = _adapter.CalculateUserLevel(summary.ExerciseId);
                CoachRecommendation = _adapter.GenerateSessionSummaryText(summary, userLevel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading summary: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Last inn fra ExerciseFeedbackEngine resultater
        /// </summary>
        public void LoadFromEngine(ExerciseFeedbackEngine engine, Exercise exercise)
        {
            var summary = engine.GetSessionSummary();
            LoadSummary(summary, exercise);
        }
        
        private double? CalculateImprovement(int exerciseId, double currentScore)
        {
            try
            {
                // This would query the database for previous session
                // For now, return null (no comparison)
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Lagre session til database
        /// </summary>
        public void SaveToDatabase()
        {
            try
            {
                if (_exerciseDataService != null && SessionId > 0)
                {
                    var duration = (int)(DateTime.Now - DateTime.Now.AddSeconds(-60)).TotalSeconds; // Approximate
                    _exerciseDataService.CompleteSession(SessionId, duration, OverallScore, "");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving to database: {ex.Message}");
            }
        }
        
        [RelayCommand]
        private void Continue()
        {
            ContinueRequested?.Invoke(this, EventArgs.Empty);
        }
        
        [RelayCommand]
        private void Finish()
        {
            SaveToDatabase();
            FinishRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
