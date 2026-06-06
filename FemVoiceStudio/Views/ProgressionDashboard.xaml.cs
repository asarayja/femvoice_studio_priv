using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Interaction logic for ProgressionDashboard.xaml
    /// Main progression visualization with level indicator, parameter graphs, and score history
    /// </summary>
    public partial class ProgressionDashboard : UserControl
    {
        public ProgressionDashboard()
        {
            InitializeComponent();
            
            // Set DataContext to the ViewModel
            var viewModel = new ProgressionDashboardViewModel(this);
            DataContext = viewModel;
        }
    }
    
    /// <summary>
    /// ViewModel for ProgressionDashboard
    /// Displays level, parameters, score history, and focus areas
    /// </summary>
    public partial class ProgressionDashboardViewModel : ObservableObject
    {
        private readonly DatabaseService? _database;
        private readonly SmartCoachEngine? _engine;
        private readonly LevelClassificationSystem? _levelSystem;
        private readonly DirectionAnalyzer? _directionAnalyzer;
        private readonly FemVoiceScore? _scoreCalculator;
        private readonly ProgressionDashboard? _dashboard;
        private readonly ILocalizationService _localization = LocalizationService.Instance;
        
        // Level indicator properties
        [ObservableProperty]
        private TrainingLevel _currentLevel = TrainingLevel.Beginner;
        
        [ObservableProperty]
        private string _levelName = LevelClassificationSystem.GetLevelName(TrainingLevel.Beginner);
        
        [ObservableProperty]
        private string _levelEmoji = "🟢";
        
        [ObservableProperty]
        private string _levelDescription = LevelClassificationSystem.GetLevelFocus(TrainingLevel.Beginner);
        
        [ObservableProperty]
        private double _progressToNextLevel;
        
        [ObservableProperty]
        private string _progressText = "";
        
        [ObservableProperty]
        private double _progressBarWidth;
        
        // Score properties
        [ObservableProperty]
        private double _femVoiceScore;
        
        [ObservableProperty]
        private string _scoreColorHex = "#4CAF50";
        
        [ObservableProperty]
        private Brush _scoreColor = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        
        // Parameter scores
        [ObservableProperty]
        private double _resonanceScore;
        
        [ObservableProperty]
        private double _pitchScore;
        
        [ObservableProperty]
        private double _intonationScore;
        
        [ObservableProperty]
        private double _voiceHealthScore;
        
        // Direction arrows
        [ObservableProperty]
        private string _resonanceDirection = "➡";
        
        [ObservableProperty]
        private string _pitchDirection = "➡";
        
        [ObservableProperty]
        private string _intonationDirection = "➡";
        
        [ObservableProperty]
        private string _voiceHealthDirection = "➡";
        
        // Bar widths for parameter progress
        [ObservableProperty]
        private double _resonanceBarWidth;
        
        [ObservableProperty]
        private double _pitchBarWidth;
        
        [ObservableProperty]
        private double _intonationBarWidth;
        
        [ObservableProperty]
        private double _voiceHealthBarWidth;
        
        // Focus area
        [ObservableProperty]
        private string _focusAreaTitle = "";
        
        [ObservableProperty]
        private string _focusAreaDescription = "";
        
        [ObservableProperty]
        private string _focusAreaIcon = "🎯";
        
        // Improvement highlight
        [ObservableProperty]
        private bool _showImprovementHighlight;
        
        [ObservableProperty]
        private string _mostImprovedParameter = "";
        
        [ObservableProperty]
        private double _improvementAmount;
        
        // Score history
        public ObservableCollection<ScoreHistoryItem> ScoreHistory { get; } = new();
        
        [ObservableProperty]
        private bool _showNoDataMessage = true;
        
        // Weekly summary
        [ObservableProperty]
        private int _sessionsThisWeek;
        
        [ObservableProperty]
        private int _minutesThisWeek;
        
        [ObservableProperty]
        private double _weeklyAverageScore;
        
        public ProgressionDashboardViewModel()
        {
            try
            {
                _database = new DatabaseService();
                _engine = new SmartCoachEngine(_database as IDatabaseService);
                _levelSystem = new LevelClassificationSystem();
                _directionAnalyzer = new DirectionAnalyzer();
                _scoreCalculator = new FemVoiceScore();
                
                LoadData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProgressionDashboardViewModel Error: {ex.Message}");
            }
        }
        
        public ProgressionDashboardViewModel(ProgressionDashboard dashboard)
        {
            _dashboard = dashboard;
            
            try
            {
                _database = new DatabaseService();
                _engine = new SmartCoachEngine(_database as IDatabaseService);
                _levelSystem = new LevelClassificationSystem();
                _directionAnalyzer = new DirectionAnalyzer();
                _scoreCalculator = new FemVoiceScore();
                
                LoadData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProgressionDashboardViewModel Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load all data for the dashboard
        /// </summary>
        public void LoadData()
        {
            try
            {
                LoadLevelInfo();
                LoadScoreData();
                LoadDirectionData();
                LoadFocusArea();
                LoadScoreHistory();
                LoadWeeklySummary();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadData Error: {ex.Message}");
            }
        }
        
        private void LoadLevelInfo()
        {
            if (_database == null || _levelSystem == null) return;
            
            try
            {
                var userSettings = _database.GetUserSettings();
                CurrentLevel = (TrainingLevel)userSettings.CurrentDifficulty;
            }
            catch
            {
                CurrentLevel = TrainingLevel.Beginner;
            }
            
            LevelName = LevelClassificationSystem.GetLevelName(CurrentLevel);
            LevelEmoji = LevelClassificationSystem.GetLevelEmoji(CurrentLevel);
            LevelDescription = LevelClassificationSystem.GetLevelFocus(CurrentLevel);
            
            // Calculate progress to next level based on recent sessions
            try
            {
                var recentScores = _database.GetRecentFemVoiceScores(10);
                if (recentScores.Count >= 3)
                {
                    var avgScore = recentScores.Average(s => s.OverallScore);
                    FemVoiceScore = avgScore;
                    
                    // Progress is percentage of sessions above threshold
                    var aboveThreshold = recentScores.Count(s => s.OverallScore >= 70);
                    ProgressToNextLevel = (double)aboveThreshold / recentScores.Count * 100;
                    ProgressText = _localization.GetFormattedString("ProgressionDashboard_ProgressOverThreshold", aboveThreshold, recentScores.Count);
                    
                    // Calculate bar width (max 200px)
                    ProgressBarWidth = Math.Min(200, ProgressToNextLevel * 2);
                }
                else
                {
                    FemVoiceScore = 50;
                    ProgressToNextLevel = 0;
                    ProgressText = _localization.GetString("ProgressionDashboard_TrainingMore");
                    ProgressBarWidth = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadLevelInfo Error: {ex.Message}");
                FemVoiceScore = 50;
                ProgressToNextLevel = 0;
                ProgressText = _localization.GetString("ProgressionDashboard_LoadFailed");
                ProgressBarWidth = 0;
            }
            
            UpdateScoreColor();
        }
        
        private void UpdateScoreColor()
        {
            var color = FemVoiceScore switch
            {
                >= 80 => Color.FromRgb(76, 175, 80),   // Green
                >= 60 => Color.FromRgb(255, 193, 7),   // Yellow/Amber
                >= 40 => Color.FromRgb(255, 152, 0),   // Orange
                _ => Color.FromRgb(244, 67, 54)        // Red
            };
            
            ScoreColor = new SolidColorBrush(color);
            ScoreColorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        
        private void LoadScoreData()
        {
            if (_database == null) return;
            
            var recentScores = _database.GetRecentFemVoiceScores(10);
            
            if (recentScores.Count > 0)
            {
                ResonanceScore = Math.Round(recentScores.Average(s => s.ResonanceScore));
                PitchScore = Math.Round(recentScores.Average(s => s.PitchScore));
                IntonationScore = Math.Round(recentScores.Average(s => s.IntonationScore));
                VoiceHealthScore = Math.Round(recentScores.Average(s => s.VoiceHealthScore));
            }
            else
            {
                ResonanceScore = 50;
                PitchScore = 50;
                IntonationScore = 50;
                VoiceHealthScore = 100;
            }
            
            // Calculate bar widths (assuming max width of 200px)
            ResonanceBarWidth = Math.Min(200, ResonanceScore * 2);
            PitchBarWidth = Math.Min(200, PitchScore * 2);
            IntonationBarWidth = Math.Min(200, IntonationScore * 2);
            VoiceHealthBarWidth = Math.Min(200, VoiceHealthScore * 2);
        }
        
        private void LoadDirectionData()
        {
            if (_directionAnalyzer == null || _database == null) return;
            
            var recentScores = _database.GetRecentFemVoiceScores(1);
            
            if (recentScores.Count > 0)
            {
                var latest = recentScores.First();
                
                var analysis = _directionAnalyzer.Analyze(
                    latest.PitchScore * 2 + 100,  // Approximate pitch from score
                    20,                             // Pitch variation
                    500,                            // F1
                    latest.ResonanceScore * 15 + 1000, // F2 approximation
                    latest.IntonationScore * 2,    // Intonation
                    100 - latest.VoiceHealthScore, // Strain
                    (FemVoiceStudio.Models.DifficultyLevel)CurrentLevel
                );
                
                ResonanceDirection = GetDirectionArrow(analysis.Resonance.Direction);
                PitchDirection = GetDirectionArrow(analysis.Pitch.Direction);
                IntonationDirection = GetDirectionArrow(analysis.Intonation.Direction);
                VoiceHealthDirection = GetDirectionArrow(analysis.VoiceHealth.Direction);
            }
            else
            {
                ResonanceDirection = "➡";
                PitchDirection = "➡";
                IntonationDirection = "➡";
                VoiceHealthDirection = "➡";
            }
        }
        
        private string GetDirectionArrow(Direction direction)
        {
            return direction switch
            {
                Direction.Increase => "⬆",
                Direction.Decrease => "⬇",
                Direction.Stabilize => "➡",
                Direction.Maintain => "✅",
                _ => "➡"
            };
        }
        
        private void LoadFocusArea()
        {
            if (_engine == null) return;
            
            var recommendation = _engine.GenerateDailyRecommendation(1);
            
            FocusAreaTitle = recommendation.FocusArea switch
            {
                "resonance" => _localization.GetString("Focus_Resonance"),
                "pitch" => _localization.GetString("Focus_Pitch"),
                "intonation" => _localization.GetString("Focus_Intonation"),
                "breathing" => _localization.GetString("Focus_Breathing"),
                "recovery" => _localization.GetString("Focus_Recovery"),
                "balanced" => _localization.GetString("Focus_BalancedTraining"),
                _ => _localization.GetString("Focus_GeneralTraining")
            };
            
            FocusAreaDescription = recommendation.RecommendationText;
            
            FocusAreaIcon = recommendation.FocusArea switch
            {
                "resonance" => "🔊",
                "pitch" => "🎵",
                "intonation" => "📈",
                "breathing" => "🌬️",
                "recovery" => "🛡️",
                _ => "🎯"
            };
            
            // Check for improvement highlight
            if (recommendation.HealthWarning)
            {
                ShowImprovementHighlight = false;
            }
            else
            {
                ShowImprovementHighlight = true;
                MostImprovedParameter = _localization.GetString("Focus_Resonance"); // Would come from profile
                ImprovementAmount = 5.0;
            }
        }
        
        private void LoadScoreHistory()
        {
            if (_database == null) return;
            
            ScoreHistory.Clear();
            
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var scores = _database.GetFemVoiceScores(thirtyDaysAgo, DateTime.Now);
            
            if (scores.Count > 0)
            {
                ShowNoDataMessage = false;
                
                // Group by day and take average
                var dailyScores = scores
                    .GroupBy(s => s.CalculatedAt.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new { Date = g.Key, AvgScore = g.Average(s => s.OverallScore) })
                    .ToList();
                
                foreach (var day in dailyScores)
                {
                    ScoreHistory.Add(new ScoreHistoryItem
                    {
                        Date = day.Date,
                        Score = day.AvgScore,
                        BarHeight = day.AvgScore * 0.9, // Scale to max 90px
                        DayLabel = day.Date.Day.ToString(),
                        ScoreColor = GetScoreBarColor(day.AvgScore)
                    });
                }
            }
            else
            {
                ShowNoDataMessage = true;
            }
        }
        
        private Brush GetScoreBarColor(double score)
        {
            return score switch
            {
                >= 70 => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                >= 50 => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                _ => new SolidColorBrush(Color.FromRgb(255, 152, 0))
            };
        }
        
        private void LoadWeeklySummary()
        {
            if (_database == null) return;
            
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var sessions = _database.GetTrainingSessions(weekStart, DateTime.Now);
            
            SessionsThisWeek = sessions.Count;
            MinutesThisWeek = sessions.Sum(s => 
                s.EndTime.HasValue ? (int)(s.EndTime.Value - s.StartTime).TotalMinutes : 0);
            
            WeeklyAverageScore = sessions.Count > 0 
                ? sessions.Average(s => s.OverallScore) 
                : 0;
        }
        
        [RelayCommand]
        private void StartExercise()
        {
            // Navigate to exercise window - close the progression window and activate main window
            try
            {
                // Find the parent ProgressionWindow and close it
                var parentWindow = Window.GetWindow(_dashboard);
                if (parentWindow != null)
                {
                    parentWindow.Close();
                }
                
                // Activate the main window
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.Activate();
                    if (mainWindow.WindowState == WindowState.Minimized)
                    {
                        mainWindow.WindowState = WindowState.Normal;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("Starting exercise from Progression Dashboard");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartExercise Error: {ex.Message}");
            }
        }
        
        [RelayCommand]
        private void Refresh()
        {
            LoadData();
        }
    }
    
    /// <summary>
    /// Represents a single day's score history for the chart
    /// </summary>
    public class ScoreHistoryItem
    {
        public DateTime Date { get; set; }
        public double Score { get; set; }
        public double BarHeight { get; set; }
        public string DayLabel { get; set; } = "";
        public Brush ScoreColor { get; set; } = Brushes.Gray;
    }
}
