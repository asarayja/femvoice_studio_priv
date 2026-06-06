using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.Services.Progression;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// ViewModel for the Progression Dashboard
    /// Handles level display, score visualization, and progress tracking
    /// </summary>
    public partial class ProgressionDashboardViewModel : ObservableObject
    {
        private readonly DatabaseService _database;
        private readonly FemVoiceScore _scoreCalculator;
        private readonly LevelClassificationSystem _levelSystem;
        private readonly LocalizationService? _localizationService;

        [ObservableProperty]
        private TrainingLevel _currentLevel = TrainingLevel.Beginner;

        [ObservableProperty]
        private string _levelEmoji = "🟢";

        [ObservableProperty]
        private double _upgradeProgress = 0;

        [ObservableProperty]
        private double _currentScore = 0;

        [ObservableProperty]
        private double _resonanceScore = 0;

        [ObservableProperty]
        private double _pitchScore = 0;

        [ObservableProperty]
        private double _intonationScore = 0;

        [ObservableProperty]
        private double _voiceHealthScore = 0;

        [ObservableProperty]
        private string _focusArea = "";

        [ObservableProperty]
        private string _mostImprovedParameter = "";

        [ObservableProperty]
        private int _sessionsThisWeek = 0;

        [ObservableProperty]
        private int _minutesThisWeek = 0;

        [ObservableProperty]
        private double _weeklyAverageScore = 0;

        [ObservableProperty]
        private bool _showNoDataMessage = true;

        [ObservableProperty]
        private ObservableCollection<ScoreHistoryItem> _scoreHistory = new();

        // Localized strings
        [ObservableProperty]
        private string _scoreLabel = LocalizationService.Instance["Dashboard_Score"];

        [ObservableProperty]
        private string _parametersLabel = LocalizationService.Instance["Dashboard_Parameters"];

        [ObservableProperty]
        private string _resonanceLabel = LocalizationService.Instance["Dashboard_Resonance"];

        [ObservableProperty]
        private string _pitchLabel = LocalizationService.Instance["Dashboard_Pitch"];

        [ObservableProperty]
        private string _intonationLabel = LocalizationService.Instance["Dashboard_Intonation"];

        [ObservableProperty]
        private string _voiceHealthLabel = LocalizationService.Instance["Dashboard_VoiceHealth"];

        [ObservableProperty]
        private string _todaysFocusLabel = LocalizationService.Instance["Dashboard_TodaysFocus"];

        [ObservableProperty]
        private string _startExerciseLabel = LocalizationService.Instance["Dashboard_StartExercise"];

        [ObservableProperty]
        private string _quickestImprovementLabel = LocalizationService.Instance["Dashboard_QuickestImprovement"];

        [ObservableProperty]
        private string _improvedByText = LocalizationService.Instance["Dashboard_ImprovedBy"];

        [ObservableProperty]
        private string _pointsThisWeekText = LocalizationService.Instance["Dashboard_PointsThisWeek"];

        [ObservableProperty]
        private string _scoreHistoryLabel = LocalizationService.Instance["Dashboard_ScoreHistory"];

        [ObservableProperty]
        private string _notEnoughDataText = LocalizationService.Instance["Dashboard_NotEnoughData"];

        [ObservableProperty]
        private string _sessionsLabel = LocalizationService.Instance["Dashboard_Sessions"];

        [ObservableProperty]
        private string _minutesLabel = LocalizationService.Instance["Dashboard_Minutes"];

        [ObservableProperty]
        private string _averageLabel = LocalizationService.Instance["Dashboard_Average"];
        
        // ============================
        // Complexity Properties
        // ============================
        
        [ObservableProperty]
        private SpeechComplexityLevel _currentComplexityLevel;
        
        [ObservableProperty]
        private string _currentComplexityLevelDisplay = "";
        
        [ObservableProperty]
        private int _sessionsAtCurrentComplexity;
        
        [ObservableProperty]
        private bool _isComplexityReadyForNext;
        
        [ObservableProperty]
        private string _complexityProgressText = "";
        
        [ObservableProperty]
        private ObservableCollection<ComplexityLevelStep> _complexityProgressionSteps = new();
        
        public ProgressionDashboardViewModel()
        {
            _database = new DatabaseService();
            _scoreCalculator = new FemVoiceScore();
            _levelSystem = new LevelClassificationSystem();
        }

        public ProgressionDashboardViewModel(DatabaseService database)
        {
            _database = database;
            _scoreCalculator = new FemVoiceScore();
            _levelSystem = new LevelClassificationSystem();
        }

        public ProgressionDashboardViewModel(LocalizationService localizationService)
        {
            _database = new DatabaseService();
            _scoreCalculator = new FemVoiceScore();
            _levelSystem = new LevelClassificationSystem();
            _localizationService = localizationService;
            LoadTranslations();
        }

        public ProgressionDashboardViewModel(DatabaseService database, LocalizationService localizationService)
        {
            _database = database;
            _scoreCalculator = new FemVoiceScore();
            _levelSystem = new LevelClassificationSystem();
            _localizationService = localizationService;
            LoadTranslations();
        }

        private void LoadTranslations()
        {
            if (_localizationService == null) return;

            ScoreLabel = _localizationService.GetString("Dashboard_Score");
            ParametersLabel = _localizationService.GetString("Dashboard_Parameters");
            ResonanceLabel = _localizationService.GetString("Dashboard_Resonance");
            PitchLabel = _localizationService.GetString("Dashboard_Pitch");
            IntonationLabel = _localizationService.GetString("Dashboard_Intonation");
            VoiceHealthLabel = _localizationService.GetString("Dashboard_VoiceHealth");
            TodaysFocusLabel = _localizationService.GetString("Dashboard_TodaysFocus");
            StartExerciseLabel = _localizationService.GetString("Dashboard_StartExercise");
            QuickestImprovementLabel = _localizationService.GetString("Dashboard_QuickestImprovement");
            ImprovedByText = _localizationService.GetString("Dashboard_ImprovedBy");
            PointsThisWeekText = _localizationService.GetString("Dashboard_PointsThisWeek");
            ScoreHistoryLabel = _localizationService.GetString("Dashboard_ScoreHistory");
            NotEnoughDataText = _localizationService.GetString("Dashboard_NotEnoughData");
            SessionsLabel = _localizationService.GetString("Dashboard_Sessions");
            MinutesLabel = _localizationService.GetString("Dashboard_Minutes");
            AverageLabel = _localizationService.GetString("Dashboard_Average");
        }

        /// <summary>
        /// Load dashboard data
        /// </summary>
        public void LoadData()
        {
            // Implementation would load from database
            LevelEmoji = LevelClassificationSystem.GetLevelEmoji(CurrentLevel);
            
            // Load complexity data
            LoadComplexityData();
        }
        
        /// <summary>
        /// Loads complexity progression data.
        /// </summary>
        private void LoadComplexityData()
        {
            try
            {
                var complexityEngine = new ComplexityEngine(_database);
                var evaluation = complexityEngine.EvaluateCurrentLevel(1);
                
                CurrentComplexityLevel = evaluation.CurrentLevel;
                CurrentComplexityLevelDisplay = ComplexityLevelStep.GetDisplayName(evaluation.CurrentLevel);
                SessionsAtCurrentComplexity = evaluation.SessionsAtCurrentLevel;
                IsComplexityReadyForNext = evaluation.IsReadyForNext;
                
                // Progress text
                var progress = Math.Min(100, (evaluation.SessionsAtCurrentLevel * 20));
                ComplexityProgressText = LocalizationService.Instance.GetFormattedString(
                    "Progression_ComplexityProgressFormat",
                    evaluation.SessionsAtCurrentLevel,
                    progress);
                
                // Progression steps
                ComplexityProgressionSteps.Clear();
                foreach (var step in complexityEngine.GetProgressionSteps(1))
                {
                    ComplexityProgressionSteps.Add(step);
                }
            }
            catch
            {
                // Fallback values
                CurrentComplexityLevel = SpeechComplexityLevel.IsolatedSounds;
                CurrentComplexityLevelDisplay = LocalizationService.Instance["Complexity_IsolatedSounds"];
                SessionsAtCurrentComplexity = 0;
                IsComplexityReadyForNext = false;
                ComplexityProgressText = LocalizationService.Instance.GetFormattedString("Progression_ComplexityProgressFormat", 0, 0);
            }
        }
    }

    /// <summary>
    /// Score history item for visualization
    /// </summary>
    public class ScoreHistoryItem
    {
        public DateTime Date { get; set; }
        public double Score { get; set; }
        public string DayLabel { get; set; } = "";
    }
}
